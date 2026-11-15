using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SLQuickMsg.Input;

/// <summary>
/// 原始鼠标输入（WM_INPUT，RIDEV_INPUTSINK）。
/// 游戏锁鼠标时系统光标会被反复拉回中心，绝对坐标不可用；这里只取相对位移。
/// </summary>
internal sealed class RawMouse : NativeWindow, IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RidevInputSink = 0x00000100;

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawMouseData
    {
        public ushort Flags;
        public ushort ButtonFlags;
        public ushort ButtonData;
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);

    private bool _active;

    public RawMouse()
    {
        CreateHandle(new CreateParams
        {
            Caption = "SLQuickMsgRawInput",
            X = 0,
            Y = 0,
            Width = 1,
            Height = 1,
        });

        var devices = new[]
        {
            new RawInputDevice
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RidevInputSink,
                Target = Handle,
            },
        };

        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new InvalidOperationException("注册原始鼠标输入失败，错误码 " + Marshal.GetLastWin32Error());
        }
    }

    /// <summary>累计的相对位移（仅在激活期间变化）。</summary>
    public int DeltaX { get; private set; }

    public int DeltaY { get; private set; }

    public void Begin()
    {
        DeltaX = 0;
        DeltaY = 0;
        _active = true;
    }

    public void End() => _active = false;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmInput)
        {
            ReadInput(m.LParam);
        }

        base.WndProc(ref m);
    }

    private void ReadInput(IntPtr rawInput)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        GetRawInputData(rawInput, RidInput, IntPtr.Zero, ref size, headerSize);
        if (size == 0 || size > 512)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(rawInput, RidInput, buffer, ref size, headerSize) != size)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != 0) // RIM_TYPEMOUSE
            {
                return;
            }

            var mouse = Marshal.PtrToStructure<RawMouseData>(IntPtr.Add(buffer, (int)headerSize));
            if (!_active)
            {
                return;
            }

            if ((mouse.Flags & 0x01) != 0) // MOUSE_MOVE_RELATIVE
            {
                DeltaX += mouse.LastX;
                DeltaY += mouse.LastY;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose() => DestroyHandle();
}
