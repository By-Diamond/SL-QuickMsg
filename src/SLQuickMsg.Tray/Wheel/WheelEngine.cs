using System.Diagnostics;
using System.Runtime.InteropServices;
using SLQuickMsg.Input;
using SLQuickMsg.Tray;

namespace SLQuickMsg.Wheel;

/// <summary>轮盘交互状态机：按住触发键 → 原始鼠标选方向 → 松开发送。</summary>
internal sealed class WheelEngine : IDisposable
{
    private const int DeadZonePixels = 38;

    private readonly KeyboardHook _hook;
    private readonly RawMouse _rawMouse;
    private readonly WheelOverlay _overlay;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Dictionary<int, WheelConfig> _wheelsByKey = new();

    private QuickMsgConfig _config = new();
    private WheelConfig? _activeWheel;
    private int _selection = -1;
    private IntPtr _previousForeground;
    private Point _wheelCenter;

    public WheelEngine(KeyboardHook hook, RawMouse rawMouse, WheelOverlay overlay)
    {
        _hook = hook;
        _rawMouse = rawMouse;
        _overlay = overlay;

        _hook.KeyDown += OnKeyDown;
        _hook.KeyUp += OnKeyUp;
        _hook.ShouldIntercept = _ => _activeWheel is not null || IsGameForeground();

        _timer = new System.Windows.Forms.Timer { Interval = 25 };
        _timer.Tick += (_, _) => UpdateSelection();
    }

    /// <summary>发送前检查：正文与命令拼接。</summary>
    public event Action<string>? SendText;

    public void Reload(QuickMsgConfig config)
    {
        _config = config;
        _wheelsByKey.Clear();

        var suppressed = new List<int>();
        foreach (var wheel in config.Wheels)
        {
            if (!KeyCodeMap.TryGetVirtualKey(wheel.Key, out var vk))
            {
                continue;
            }

            // 同一个键配置多个轮盘时，后者覆盖前者
            _wheelsByKey[(int)vk] = wheel;
            suppressed.Add((int)vk);
        }

        _hook.SetSuppressedKeys(suppressed);
    }

    private void OnKeyDown(int vk)
    {
        if (_activeWheel is not null || !_wheelsByKey.TryGetValue(vk, out var wheel))
        {
            return;
        }

        if (!IsGameForeground())
        {
            return;
        }

        if (wheel.Slots.Count == 0 || wheel.Slots.All(s => string.IsNullOrWhiteSpace(s.Body)))
        {
            return;
        }

        _previousForeground = GetForegroundWindow();
        _activeWheel = wheel;
        _selection = -1;
        _rawMouse.Begin();

        _wheelCenter = GetWheelCenter();
        _overlay.ShowWheel(_wheelCenter, wheel.Slots.Select(s => s.Title).ToArray(), wheel.RotationDegrees);
        Log.Info($"轮盘打开：{wheel.Name}，前台是否已接管={FocusHelper.ForegroundWindow == _overlay.Handle}");
        _timer.Start();
    }

    private void OnKeyUp(int vk)
    {
        if (_activeWheel is null)
        {
            return;
        }

        if (!_wheelsByKey.ContainsKey(vk))
        {
            return;
        }

        UpdateSelection();
        var wheel = _activeWheel;
        var index = _selection;

        _timer.Stop();
        _rawMouse.End();
        _overlay.HideWheel();

        // 把前台还给游戏，发送按键才有地方去
        var restored = _previousForeground != IntPtr.Zero && FocusHelper.ForceForeground(_previousForeground);
        Log.Info($"轮盘关闭：选中={index}，恢复前台={restored}");

        _previousForeground = IntPtr.Zero;
        _activeWheel = null;
        _selection = -1;

        if (index >= 0 && index < wheel.Slots.Count)
        {
            var body = wheel.Slots[index].Body;
            if (!string.IsNullOrWhiteSpace(body))
            {
                SendText?.Invoke(_config.CommandPrefix + body);
            }
        }
    }

    private void UpdateSelection()
    {
        if (_activeWheel is null)
        {
            return;
        }

        var count = _activeWheel.Slots.Count;

        // 抢到焦点后系统光标可自由移动：优先用光标相对轮盘中心的位移
        var cursor = System.Windows.Forms.Cursor.Position;
        double dx = cursor.X - _wheelCenter.X;
        double dy = cursor.Y - _wheelCenter.Y;

        // 焦点没抢到（光标仍被游戏锁住）时，退回原始输入累计位移
        if (Math.Sqrt(dx * dx + dy * dy) < DeadZonePixels)
        {
            dx = _rawMouse.DeltaX;
            dy = _rawMouse.DeltaY;
        }

        var distance = Math.Sqrt(dx * dx + dy * dy);

        if (count == 0 || distance < DeadZonePixels)
        {
            _selection = -1;
            _overlay.SetActive(-1);
            return;
        }

        var degrees = Math.Atan2(-dy, dx) * 180d / Math.PI; // 0=右, 90=上
        var step = 360d / count;
        var relative = degrees - _activeWheel.RotationDegrees;
        var index = (int)Math.Round((90d - relative) / step);
        index = ((index % count) + count) % count;

        _selection = index;
        _overlay.SetActive(index);
    }

    private static Point GetWheelCenter()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rect))
        {
            return new Point((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
        }

        var screen = System.Windows.Forms.Screen.PrimaryScreen!.Bounds;
        return new Point(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    public bool IsGameForeground()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        GetWindowThreadProcessId(hwnd, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return string.Equals(process.ProcessName, "SCPSL", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    public void Dispose()
    {
        _timer.Dispose();
        _hook.KeyDown -= OnKeyDown;
        _hook.KeyUp -= OnKeyUp;
    }
}
