using System.Text.Json;
using System.Text.Json.Serialization;

namespace SLQuickMsg;

/// <summary>整个工具的配置：多个按键，每个按键一个独立轮盘。</summary>
public sealed class QuickMsgConfig
{
    /// <summary>发送时拼在正文前面的命令，例如 ".tc "。</summary>
    public string CommandPrefix { get; set; } = ".tc ";

    /// <summary>发送后是否弹托盘气泡提示（关掉更安静）。</summary>
    public bool NotifyOnSend { get; set; } = false;

    public List<WheelConfig> Wheels { get; set; } = new();
}

public sealed class WheelConfig
{
    /// <summary>触发键名（Unity KeyCode 风格：G、F1、Mouse3…）。</summary>
    public string Key { get; set; } = "G";

    /// <summary>轮盘名字，仅用于配置界面。</summary>
    public string Name { get; set; } = "默认轮盘";

    /// <summary>轮盘整体旋转角度（度）。0 = 第一个槽位在正上方，45 = 斜向。</summary>
    public double RotationDegrees { get; set; }

    /// <summary>按顺时针从正上方开始排列。</summary>
    public List<WheelSlot> Slots { get; set; } = new();
}

public sealed class WheelSlot
{
    /// <summary>轮盘上显示的文字。</summary>
    public string Title { get; set; } = "";

    /// <summary>实际发送的正文（不含命令前缀）。</summary>
    public string Body { get; set; } = "";
}

public static class ConfigIO
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string DefaultPath => Path.Combine(AppContext.BaseDirectory, "config", "SLQuickMsg.json");

    public static QuickMsgConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<QuickMsgConfig>(json, Options);
                if (config is not null)
                {
                    return config;
                }
            }
        }
        catch
        {
            // 配置损坏时退回默认值，不阻塞托盘启动
        }

        var fallback = Defaults();
        TrySave(path, fallback);
        return fallback;
    }

    public static void Save(string path, QuickMsgConfig config)
    {
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, Options));
        File.Move(tmp, path, overwrite: true);
    }

    private static void TrySave(string path, QuickMsgConfig config)
    {
        try
        {
            Save(path, config);
        }
        catch
        {
            // 只读目录等情况下忽略
        }
    }

    public static QuickMsgConfig Defaults() => new()
    {
        CommandPrefix = ".tc ",
        NotifyOnSend = false,
        Wheels =
        {
            new WheelConfig
            {
                Key = "G",
                Name = "战术",
                Slots =
                {
                    new WheelSlot { Title = "集合", Body = "所有人到检查点集合" },
                    new WheelSlot { Title = "撤离", Body = "准备撤离，跟我走" },
                    new WheelSlot { Title = "支援", Body = "需要支援，坐标已标记" },
                    new WheelSlot { Title = "小心", Body = "注意身后，小心埋伏" },
                },
            },
        },
    };
}
