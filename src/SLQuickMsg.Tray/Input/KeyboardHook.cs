using System.Runtime.InteropServices;

namespace SLQuickMsg.Input;

/// <summary>全局低级键盘钩子：检测按下/抬起，并可吞掉指定按键（避免游戏收到 G）。</summary>
internal sealed class KeyboardHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? name);

    private readonly LowLevelKeyboardProc _callback;
    private readonly HashSet<int> _suppressed = new();
    private IntPtr _hook;

    public KeyboardHook()
    {
        _callback = Handle;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandleW(null), 0);
        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("安装键盘钩子失败，错误码 " + Marshal.GetLastWin32Error());
        }
    }

    /// <summary>需要吞掉的虚拟键码（触发键）。</summary>
    public IReadOnlyCollection<int> SuppressedKeys => _suppressed;

    /// <summary>是否现在真的拦截该键（例如只在游戏前台时才拦）。</summary>
    public Func<int, bool>? ShouldIntercept { get; set; }

    public void SetSuppressedKeys(IEnumerable<int> keys)
    {
        _suppressed.Clear();
        foreach (var key in keys)
        {
            _suppressed.Add(key);
        }
    }

    public event Action<int>? KeyDown;

    public event Action<int>? KeyUp;

    private IntPtr Handle(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is WmKeyDown or WmSysKeyDown;
        var isUp = message is WmKeyUp or WmSysKeyUp;
        if (!isDown && !isUp)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var vk = Marshal.ReadInt32(lParam);
        if (_suppressed.Contains(vk) && (ShouldIntercept?.Invoke(vk) ?? true))
        {
            if (isDown)
            {
                KeyDown?.Invoke(vk);
            }
            else
            {
                KeyUp?.Invoke(vk);
            }

            return (IntPtr)1; // 吞掉，游戏收不到
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
