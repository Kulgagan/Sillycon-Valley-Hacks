using System.IO;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Forms;

namespace ProProSahur;

public partial class MainWindow : Window
{
    private readonly Config _config;
    private readonly SahurTalkService _sahurTalk;
    private readonly SpeechSynthesizer _speech = new();
    private readonly PiperTtsService _piperTts;
    private NotifyIcon? _trayIcon;
    private System.Threading.Timer? _detectionTimer;
    private readonly DispatcherTimer _movementTimer;
    private DateTime _lastAttack = DateTime.MinValue;
    private double _targetX, _targetY;
    private bool _isAttacking;
    private bool _isScolding;
    private IntPtr _attackTargetHwnd;
    private int _tickCount;
    private int _walkTickCount;
    private const int WalkFrameCount = 18;

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;

    public MainWindow()
    {
        InitializeComponent();
        _config = Config.Load();
        _sahurTalk = new SahurTalkService(_config);
        _piperTts = new PiperTtsService(_config);

        var (cornerX, cornerY) = GetBottomRightCorner();
        Left = cornerX;
        Top = cornerY;
        _targetX = cornerX;
        _targetY = cornerY;

        _movementTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _movementTimer.Tick += MovementTimer_Tick;

        Loaded += (s, e) =>
        {
            SetupTrayIcon();
            MakeClickThrough();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Win32Interop.SetWindowPos(hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE);
            _detectionTimer = new System.Threading.Timer(_ => Dispatcher.Invoke(() => DetectionTimer_Tick(null)),
                null, 0, _config.CheckIntervalMs);
            _movementTimer.Start();
        };

        Closing += (s, e) =>
        {
            _detectionTimer?.Dispose();
            _trayIcon?.Dispose();
        };
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Pro Pro Sahur - Right-click to exit"
        };

        var menu = new ContextMenuStrip();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        };
        menu.Items.Add(exitItem);
        _trayIcon.ContextMenuStrip = menu;
    }

    private void MakeClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var style = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TRANSPARENT | WS_EX_LAYERED);
    }

    private void DetectionTimer_Tick(object? _)
    {
        try
        {
            if (_isAttacking) return;

            var cooldown = DateTime.Now - _lastAttack;
            var minCooldownMs = Math.Max(_config.AttackCooldownMs, 15000);
            if (cooldown.TotalMilliseconds < minCooldownMs) return;

            var hwnd = Win32Interop.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return;

            var selfHwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (selfHwnd != IntPtr.Zero && hwnd == selfHwnd) return;

            var title = Win32Interop.GetWindowTitle(hwnd);
            if (string.IsNullOrWhiteSpace(title)) return;

            if (!Win32Interop.IsWindowVisible(hwnd) || Win32Interop.IsIconic(hwnd)) return;

            var isDistracting = _config.Blocklist.Any(term =>
                title.Contains(term, StringComparison.OrdinalIgnoreCase));

if (isDistracting)
        {
            _isAttacking = true;
            _isScolding = true;
            _walkTickCount = 0;
            UpdateCharacterImage();
                _attackTargetHwnd = hwnd;

                if (Win32Interop.GetWindowRect(hwnd, out var rect))
                {
                    var screenLeft = SystemParameters.VirtualScreenLeft;
                    var screenTop = SystemParameters.VirtualScreenTop;
                    var screenWidth = SystemParameters.VirtualScreenWidth;
                    var screenHeight = SystemParameters.VirtualScreenHeight;
                    _targetX = rect.Right - 100;
                    _targetY = rect.Top - 160;
                    _targetX = Math.Clamp(_targetX, screenLeft, screenLeft + screenWidth - WindowWidth);
                    _targetY = Math.Clamp(_targetY, screenTop, screenTop + screenHeight - WindowHeight);
                }

                var siteName = title.Split(" - ").FirstOrDefault() ?? "that site";
                _ = ScoldThenMoveAsync(siteName);
            }
        }
        catch
        {
            _isAttacking = false;
            _isScolding = false;
        }
    }

    private void MovementTimer_Tick(object? sender, EventArgs e)
    {
        _tickCount++;
        if (_tickCount % 20 == 0)
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            Win32Interop.SetWindowPos(hwnd, Win32Interop.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Interop.SWP_NOMOVE | Win32Interop.SWP_NOSIZE);
        }

        var dx = _targetX - Left;
        var dy = _targetY - Top;
        var dist = Math.Sqrt(dx * dx + dy * dy);

        if (_isAttacking)
        {
            if (_isScolding)
            {
                return;
            }

            if (!Win32Interop.IsWindowVisible(_attackTargetHwnd) || Win32Interop.IsIconic(_attackTargetHwnd))
            {
                _isAttacking = false;
                (_targetX, _targetY) = GetBottomRightCorner();
                UpdateCharacterImage();
                return;
            }

            if (dist < 50)
            {
                var title = Win32Interop.GetWindowTitle(_attackTargetHwnd);
                CloseDistractingTab();
                _lastAttack = DateTime.Now;
                _isAttacking = false;
                (_targetX, _targetY) = GetBottomRightCorner();
                UpdateCharacterImage();
                _ = SpeakAndShowAsync(title);
            }
            else
            {
                _walkTickCount++;
                const double attackLerp = 0.028;
                var zigzagAmplitude = 18.0;
                var zigzag = zigzagAmplitude * Math.Sin(_walkTickCount * 0.12);
                var moveX = (_targetX - Left) * attackLerp + zigzag * 0.25;
                var moveY = (_targetY - Top) * attackLerp;
                Left += moveX;
                Top += moveY;
                UpdateCharacterImage();
            }
        }
        else
        {
            if (dist >= 15)
            {
                const double idleLerp = 0.03;
                Left += (_targetX - Left) * idleLerp;
                Top += (_targetY - Top) * idleLerp;
            }
        }

        ClampToScreen();
        UpdateFacing(dx);
    }

    private const int WindowWidth = 350;
    private const int WindowHeight = 280;

    private static (double X, double Y) GetBottomRightCorner()
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;
        const int offsetFromBottom = 200;
        return (screenLeft + screenWidth - WindowWidth, screenTop + screenHeight - offsetFromBottom);
    }

    private void ClampToScreen()
    {
        var screenLeft = SystemParameters.VirtualScreenLeft;
        var screenTop = SystemParameters.VirtualScreenTop;
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;
        Left = Math.Clamp(Left, screenLeft, screenLeft + screenWidth - WindowWidth);
        Top = Math.Clamp(Top, screenTop, screenTop + screenHeight - WindowHeight);
    }

    private void UpdateFacing(double dx)
    {
        var scaleX = dx < 0 ? -1 : 1;
        CharacterCanvas.RenderTransform = new ScaleTransform(scaleX, 1, 60, 60);
    }

    private static readonly Uri NormalImageUri = new("pack://application:,,,/ProProSahur;component/Assets/tung-tung-sahur.png", UriKind.Absolute);
    private static readonly BitmapSource?[] _walkFrames = new BitmapSource[WalkFrameCount];

    private static BitmapSource GetWalkingFrame(int frameIndex)
    {
        var idx = ((frameIndex % WalkFrameCount) + WalkFrameCount) % WalkFrameCount;
        if (_walkFrames[idx] != null) return _walkFrames[idx]!;
        try
        {
            var uri = new Uri($"pack://application:,,,/ProProSahur;component/Assets/frame_{idx:D3}.png", UriKind.Absolute);
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo?.Stream != null)
            {
                using var src = new System.Drawing.Bitmap(streamInfo.Stream);
                using var bitmap = src.Clone(new System.Drawing.Rectangle(0, 0, src.Width, src.Height), System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                const int darkThreshold = 50;
                var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    unsafe
                    {
                        var ptr = (byte*)bmpData.Scan0;
                        for (var y = 0; y < bitmap.Height; y++)
                        {
                            var row = ptr + y * bmpData.Stride;
                            for (var x = 0; x < bitmap.Width; x++)
                            {
                                var b = row[x * 4];
                                var g = row[x * 4 + 1];
                                var r = row[x * 4 + 2];
                                if (r <= darkThreshold && g <= darkThreshold && b <= darkThreshold)
                                    row[x * 4 + 3] = 0;
                            }
                        }
                    }
                }
                finally { bitmap.UnlockBits(bmpData); }
                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var decoder = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames[0];
                frame.Freeze();
                _walkFrames[idx] = frame;
                return frame;
            }
        }
        catch { }
        return new BitmapImage(NormalImageUri);
    }

    private void UpdateCharacterImage()
    {
        if (_isAttacking)
        {
            var frameIdx = _isScolding ? 0 : (_walkTickCount / 1) % WalkFrameCount;
            CharacterImage.Source = GetWalkingFrame(frameIdx);
        }
        else
        {
            CharacterImage.Source = new BitmapImage(NormalImageUri);
        }
    }

    private async Task ScoldThenMoveAsync(string siteName)
    {
        string message;
        try
        {
            message = await _sahurTalk.GetScoldingMessageAsync(siteName);
        }
        catch
        {
            message = $"Bad boy! {siteName}? Pro Pro Sahur is watching!";
        }
        if (string.IsNullOrWhiteSpace(message)) message = $"Bad boy! {siteName}? Pro Pro Sahur is watching!";

        Dispatcher.Invoke(() =>
        {
            SpeechText.Text = message;
            SpeechBubble.Visibility = Visibility.Visible;
            ClampToScreen();
        });

        bool piperWorked = false;
        try
        {
            await _piperTts.SpeakAsync(message);
            piperWorked = true;
        }
        catch (Exception ex)
        {
            // Show error if Piper fails
            Dispatcher.Invoke(() =>
            {
                SpeechText.Text = $"Piper TTS failed: {ex.Message}\n{message}";
            });
        }
        if (!piperWorked)
        {
            // Only fallback if Piper fails
            _speech.SpeakAsync(message);
        }
        await Task.Delay(3500);
        try { Dispatcher.Invoke(() => _isScolding = false); } catch { }
    }

    private async Task SpeakAndShowAsync(string windowTitle)
    {
        var siteName = windowTitle.Split(" - ").FirstOrDefault() ?? "that site";
        var message = await _sahurTalk.GetMockingMessageAsync(siteName);
        if (string.IsNullOrWhiteSpace(message)) message = $"{siteName}? Really? Pro Pro Sahur says no.";

        Dispatcher.Invoke(() =>
        {
            SpeechText.Text = message;
            SpeechBubble.Visibility = Visibility.Visible;
            ClampToScreen();
            var hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            hideTimer.Tick += (_, _) =>
            {
                hideTimer.Stop();
                SpeechBubble.Visibility = Visibility.Collapsed;
            };
            hideTimer.Start();
        });

        bool piperWorked = false;
        try
        {
            await _piperTts.SpeakAsync(message);
            piperWorked = true;
        }
        catch (Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                SpeechText.Text = $"Piper TTS failed: {ex.Message}\n{message}";
            });
        }
        if (!piperWorked)
        {
            _speech.SpeakAsync(message);
        }
    }

    private void CloseDistractingTab()
    {
        var title = Win32Interop.GetWindowTitle(_attackTargetHwnd);
        var isBrowser = title.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                       title.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ||
                       title.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                       title.Contains("Brave", StringComparison.OrdinalIgnoreCase) ||
                       title.Contains("Opera", StringComparison.OrdinalIgnoreCase);

        if (isBrowser)
            Win32Interop.SendCtrlW(_attackTargetHwnd);
        else
            Win32Interop.SendAltF4(_attackTargetHwnd);
    }

}
