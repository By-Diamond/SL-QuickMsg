namespace SLQuickMsg;

/// <summary>极简日志，便于排查注入/钩子/发送问题。</summary>
internal static class Log
{
    private static readonly object Gate = new();

    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SLQuickMsg",
        "app.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath)!;
            Directory.CreateDirectory(dir);
            lock (Gate)
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}" +
                    (exception is null ? "" : Environment.NewLine + exception) + Environment.NewLine);
            }
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
