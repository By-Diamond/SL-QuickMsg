using System.Runtime.InteropServices;

namespace SLQuickMsg.Input;

/// <summary>Win32 SendInput 封装：模拟按键与 Unicode 文本输入。</summary>
internal static class InputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;

    public const ushort VkTilde = 0xC0;   // ` ~
    public const ushort VkReturn = 0x0D;
    public const ushort VkEscape = 0x1B;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern short VkKeyScan(char ch);

    /// <summary>按一下再松开一个虚拟键。</summary>
    public static void PressKey(ushort virtualKey)
    {
        var inputs = new[]
        {
            KeyDown(virtualKey),
            KeyUp(virtualKey),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    /// <summary>一次性把整段文本作为 Unicode 字符输入（不经剪贴板）。</summary>
    public static void TypeText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var inputs = new List<Input>(text.Length * 2);
        foreach (var ch in text)
        {
            inputs.Add(UnicodeChar(ch, up: false));
            inputs.Add(UnicodeChar(ch, up: true));
        }

        SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
    }

    private static Input KeyDown(ushort vk) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = vk } },
    };

    private static Input KeyUp(ushort vk) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = vk, Flags = KeyEventKeyUp } },
    };

    private static Input UnicodeChar(char ch, bool up) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                ScanCode = ch,
                Flags = KeyEventUnicode | (up ? KeyEventKeyUp : 0),
            },
        },
    };
}
