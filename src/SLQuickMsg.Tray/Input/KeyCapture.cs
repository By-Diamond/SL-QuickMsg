using System.Runtime.InteropServices;

namespace SLQuickMsg.Input;

/// <summary>
/// 按键捕获：用低级键盘钩子直接读虚拟键码。
/// WinForms 的 KeyDown 在中文输入法激活时会给出 Keys.ProcessKey(229)，拿不到真实按键，所以不用它。
/// 捕获期间吞掉键盘输入，避免输入法/游戏同时收到。
/// </summary>
internal sealed class KeyCapture : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkEscape = 0x1B;

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
    private IntPtr _hook;
    private bool _active;
    private int _swallowUpOf;

    public KeyCapture()
    {
        _callback = Handle;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandleW(null), 0);
    }

    /// <summary>收到一个虚拟键码（已经吞掉该按键）。</summary>
    public event Action<int>? Captured;

    public event Action? Cancelled;

    public void Start()
    {
        _swallowUpOf = 0;
        _active = true;
    }

    public void Stop() => _active = false;

    private IntPtr Handle(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is WmKeyDown or WmSysKeyDown;
        var isUp = message is WmKeyUp or WmSysKeyUp;

        var vk = Marshal.ReadInt32(lParam);

        if (_swallowUpOf == vk && isUp)
        {
            _swallowUpOf = 0;
            return (IntPtr)1;
        }

        if (!_active || !isDown)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        _active = false;
        _swallowUpOf = vk;

        if (vk == VkEscape)
        {
            Cancelled?.Invoke();
        }
        else
        {
            Captured?.Invoke(vk);
        }

        return (IntPtr)1;
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
