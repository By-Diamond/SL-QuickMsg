using System.Runtime.InteropServices;

namespace SLQuickMsg.Input;

/// <summary>
/// 强制把前台切到指定窗口。
/// 后台进程直接调用 SetForegroundWindow 会被 Windows 拒绝，需要先 AttachThreadInput 到当前前台线程。
/// </summary>
internal static class FocusHelper
{
    private const int SwShow = 5;
    private const int SwHide = 0;
    private const int AttachSuccess = 1;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attachTo, uint attachFrom, bool attach);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hwnd, int command);

    [DllImport("user32.dll")]
    private static extern bool ClipCursor(IntPtr rect);

    /// <summary>把前台交给 hwnd；成功返回 true。</summary>
    public static bool ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var currentThread = GetCurrentThreadId();
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground != IntPtr.Zero
            ? GetWindowThreadProcessId(foreground, out _)
            : 0;

        var attached = foregroundThread != 0
                       && foregroundThread != currentThread
                       && AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            ClipCursor(IntPtr.Zero);
            ShowWindow(hwnd, SwShow);
            SetForegroundWindow(hwnd);
            SetFocus(hwnd);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        return GetForegroundWindow() == hwnd;
    }

    public static void Hide(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SwHide);
        }
    }

    public static IntPtr ForegroundWindow => GetForegroundWindow();
}
