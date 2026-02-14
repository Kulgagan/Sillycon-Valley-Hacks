using System.Runtime.InteropServices;
using System.Text;

namespace ProProSahur;

public static class Win32Interop
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;

    public const uint WM_CLOSE = 0x0010;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_W = 0x57;
    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static string GetWindowTitle(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static void SendCtrlW(IntPtr hWnd)
    {
        SetForegroundWindow(hWnd);
        Thread.Sleep(50);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event(VK_W, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event(VK_W, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    public static void CloseWindow(IntPtr hWnd)
    {
        PostMessage(hWnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    public static void SendAltF4(IntPtr hWnd)
    {
        const byte VK_MENU = 0x12;
        const byte VK_F4 = 0x73;
        SetForegroundWindow(hWnd);
        Thread.Sleep(50);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event(VK_F4, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        keybd_event(VK_F4, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
}
