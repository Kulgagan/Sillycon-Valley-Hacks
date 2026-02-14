using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ProProSahur;

public class PiperTtsService
{
    private readonly Config _config;
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(2),
        DefaultRequestHeaders = { { "User-Agent", "ProProSahur/1.0" } }
    };

    // Default voice: Ryan (en_US), high quality — https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/ryan/high
    private const string DefaultModelUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx";
    private const string DefaultModelJsonUrl = "https://huggingface.co/rhasspy/piper-voices/resolve/main/en/en_US/ryan/high/en_US-ryan-high.onnx.json";
    private const string DefaultModelName = "en_US-ryan-high";

    public PiperTtsService(Config config) => _config = config;

    private string GetOrDownloadModelPath(string piperDir)
    {
        if (!string.IsNullOrWhiteSpace(_config.PiperModelPath))
        {
            var path = Path.GetFullPath(_config.PiperModelPath.Trim());
            if (File.Exists(path)) return path;
        }

        var onnxFiles = Directory.GetFiles(piperDir, "*.onnx");
        if (onnxFiles.Length > 0)
            return onnxFiles[0];

        var onnxPath = Path.Combine(piperDir, DefaultModelName + ".onnx");
        var jsonPath = Path.Combine(piperDir, DefaultModelName + ".onnx.json");
        if (File.Exists(onnxPath)) return onnxPath;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var onnxBytes = Http.GetByteArrayAsync(DefaultModelUrl, cts.Token).GetAwaiter().GetResult();
            File.WriteAllBytes(onnxPath, onnxBytes);
            try
            {
                var jsonBytes = Http.GetByteArrayAsync(DefaultModelJsonUrl, cts.Token).GetAwaiter().GetResult();
                File.WriteAllBytes(jsonPath, jsonBytes);
            }
            catch { /* optional */ }
            return onnxPath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "No Piper voice model found. Put a .onnx (and .onnx.json) in: " + piperDir + " Error: " + ex.Message);
        }
    }

    /// <summary>Run a process with stdin text; returns true if exit 0 and outfile exists.</summary>
    private static async Task<(bool ok, string stderr)> RunPiperProcessAsync(string exe, string arguments, string workingDir, string text, string outfile, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi);
        if (proc == null) return (false, "Failed to start process.");

        await proc.StandardInput.WriteAsync(text.AsMemory(), ct);
        proc.StandardInput.Close();

        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        var finished = await Task.Run(() => proc.WaitForExit(30000), ct);

        if (!finished)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            return (false, "Timed out. " + stderr);
        }

        if (proc.ExitCode != 0) return (false, "Exit " + proc.ExitCode + ": " + stderr);
        if (!File.Exists(outfile)) return (false, "No output file. " + stderr);
        return (true, stderr);
    }

    private static string? GetDefaultPiperExePath()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ProProSahur", "piper");
        var candidates = new[]
        {
            Path.Combine(baseDir, "piper_windows_amd64", "piper", "piper.exe"),
            Path.Combine(baseDir, "piper_windows_amd64", "piper.exe"),
            Path.Combine(baseDir, "piper", "piper.exe"),
            Path.Combine(baseDir, "piper.exe")
        };
        foreach (var p in candidates)
        {
            if (File.Exists(p)) return p;
        }
        try
        {
            var found = Directory.GetFiles(baseDir, "piper.exe", SearchOption.AllDirectories);
            if (found.Length > 0) return found[0];
        }
        catch { /* dir may not exist */ }
        return null;
    }

    private static string SanitizeForPiper(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Tung tung sahur.";
        var s = new string(text.Where(c => !char.IsControl(c) || c == '\n' || c == '\r').ToArray());
        s = s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        return string.IsNullOrEmpty(s) ? "Tung tung sahur." : (s.Length > 500 ? s[..500] : s);
    }

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        text = SanitizeForPiper(text);

        var exePath = _config.PiperExecutablePath?.Trim();
        if (string.IsNullOrEmpty(exePath))
            exePath = GetDefaultPiperExePath();
        if (string.IsNullOrEmpty(exePath))
            throw new InvalidOperationException(
                "Piper not found. Run Install-Dependencies.ps1 to download Piper + voice model, or set PiperExecutablePath in config.");

        exePath = Path.GetFullPath(exePath);
        if (!File.Exists(exePath))
            throw new FileNotFoundException("Piper not found at: " + exePath, exePath);

        var workingDir = Path.GetDirectoryName(exePath)!;
        if (!Directory.Exists(workingDir))
            throw new InvalidOperationException("Piper directory not found: " + workingDir);

        var modelPath = GetOrDownloadModelPath(workingDir);
        var outfile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".wav");
        var args = (_config.PiperArgsTemplate ?? "--model \"{model}\" --output_file \"{outfile}\"")
            .Replace("{model}", modelPath)
            .Replace("{outfile}", outfile)
            .Replace("{text}", "")
            .Replace("\"{text}\"", "")
            .Trim();

        var ok = false;
        var stderr = "";

        // Try Python piper first (more reliable; Windows exe often crashes with 0xC0000409)
        if (TryFindPython(out var pythonPath))
        {
            var pyArgs = $"-m piper --model \"{modelPath}\" --output_file \"{outfile}\"";
            (ok, stderr) = await RunPiperProcessAsync(pythonPath, pyArgs, workingDir, text, outfile, ct);
        }

        if (!ok)
        {
            (ok, stderr) = await RunPiperProcessAsync(exePath, args, workingDir, text, outfile, ct);
        }

        if (!ok)
            throw new InvalidOperationException("Piper failed. " + stderr);
        if (!File.Exists(outfile))
            throw new FileNotFoundException("Piper produced no output. " + stderr, outfile);

        try
        {
            using var player = new SoundPlayer(outfile);
            player.Load();
            player.PlaySync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Piper produced audio but playback failed: " + ex.Message);
        }
        finally
        {
            try { File.Delete(outfile); } catch { }
        }
    }

    private static bool TryFindPython(out string path)
    {
        path = "";
        foreach (var name in new[] { "python", "python3" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true
                };
                using var p = Process.Start(psi);
                if (p != null && p.WaitForExit(3000) && p.ExitCode == 0)
                {
                    path = name;
                    return true;
                }
            }
            catch { /* not found */ }
        }
        return false;
    }
}
