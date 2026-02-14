using System.IO;
using System.Text.Json;

namespace ProProSahur;

public class Config
{
    private static readonly string[] DefaultBlocklist =
    [
        "YouTube", "youtube.com", "Twitter", "twitter.com", "X -", " - X", "x.com",
        "Facebook", "facebook.com", "Instagram", "instagram.com", "Reddit", "reddit.com",
        "TikTok", "tiktok.com", "Twitch", "twitch.tv", "Netflix", "netflix.com", "Steam"
    ];

    public List<string> Blocklist { get; set; } = new List<string>(DefaultBlocklist);

    public int CheckIntervalMs { get; set; } = 2000;
    public int AttackCooldownMs { get; set; } = 45000;

    public string LlmProvider { get; set; } = "ark";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.2";
    public string? OpenAiApiKey { get; set; }
    public string OpenAiModel { get; set; } = "gpt-4o-mini";
    public string? ArkApiKey { get; set; } = ".storefront.fa4e25100a05749b73d0dab7d30e39146fcf1686d0272a89b72723267ccc2974";
    public string ArkUrl { get; set; } = "https://api.ark-labs.cloud/api/v1";
    public string ArkModel { get; set; } = "gpt-4o";

    // Piper TTS: path to piper.exe. If null, app uses default at %LocalAppData%\ProProSahur\piper\...
    public string? PiperExecutablePath { get; set; }
    public string? PiperModelPath { get; set; }  // Optional: full path to .onnx; else use .onnx in piper folder or auto-download.
    public string? PiperArgsTemplate { get; set; } = @"--model ""{model}"" --output_file ""{outfile}""";

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProProSahur",
        "config.json");

    private static void MergeDefaultBlocklist(Config config)
    {
        var blocklist = config.Blocklist ?? [];
        var set = new HashSet<string>(blocklist, StringComparer.OrdinalIgnoreCase);
        foreach (var term in DefaultBlocklist)
        {
            if (!set.Contains(term))
            {
                blocklist.Add(term);
                set.Add(term);
            }
        }
        config.Blocklist = blocklist;
    }

    public static Config Load()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<Config>(json) ?? new Config();
                MergeDefaultBlocklist(config);
                return config;
            }
        }
        catch { }

        return new Config();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }
}
