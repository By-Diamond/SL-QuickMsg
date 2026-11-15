using System.Windows.Forms;

namespace SLQuickMsg.Tray;

/// <summary>Unity KeyCode 名称 ↔ Win32 虚拟键码。</summary>
internal static class KeyCodeMap
{
    private static readonly Dictionary<string, uint> Map = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, string> Reverse = new();

    static KeyCodeMap()
    {
        for (var i = 1; i <= 12; i++)
        {
            Add("F" + i, (uint)(0x70 + i - 1));
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            Add(c.ToString(), c);
        }

        for (var d = '0'; d <= '9'; d++)
        {
            Add("Alpha" + d, d);
            Add("Keypad" + d, (uint)(0x60 + (d - '0')));
        }

        Add("Space", 0x20);
        Add("Tab", 0x09);
        Add("Escape", 0x1B);
        Add("Return", 0x0D);
        Add("Backspace", 0x08);
        Add("Insert", 0x2D);
        Add("Delete", 0x2E);
        Add("Home", 0x24);
        Add("End", 0x23);
        Add("PageUp", 0x21);
        Add("PageDown", 0x22);
        Add("LeftArrow", 0x25);
        Add("UpArrow", 0x26);
        Add("RightArrow", 0x27);
        Add("DownArrow", 0x28);
        Add("LeftShift", 0xA0);
        Add("RightShift", 0xA1);
        Add("LeftControl", 0xA2);
        Add("RightControl", 0xA3);
        Add("LeftAlt", 0xA4);
        Add("RightAlt", 0xA5);
        Add("Semicolon", 0xBA);
        Add("Equals", 0xBB);
        Add("Comma", 0xBC);
        Add("Minus", 0xBD);
        Add("Period", 0xBE);
        Add("Slash", 0xBF);
        Add("BackQuote", 0xC0);
        Add("LeftBracket", 0xDB);
        Add("Backslash", 0xDC);
        Add("RightBracket", 0xDD);
        Add("Quote", 0xDE);
    }

    private static void Add(string name, uint vk)
    {
        Map[name] = vk;
        Reverse.TryAdd(vk, name);
    }

    public static bool TryGetVirtualKey(string keyCodeName, out uint vk)
    {
        vk = 0;
        return !string.IsNullOrWhiteSpace(keyCodeName) && Map.TryGetValue(keyCodeName.Trim(), out vk);
    }

    public static string? ToName(int virtualKey) =>
        Reverse.TryGetValue((uint)virtualKey, out var name) ? name : null;

    /// <summary>配置界面按键捕获用：把 WinForms 键值转成配置里的名字。</summary>
    public static string FromKeys(Keys key)
    {
        var name = ToName((int)key);
        if (name is not null)
        {
            return name;
        }

        // Keys.D1 之类在部分键盘布局下会走到这里
        if (key is >= Keys.D0 and <= Keys.D9)
        {
            return "Alpha" + (key - Keys.D0);
        }

        return key.ToString();
    }
}
