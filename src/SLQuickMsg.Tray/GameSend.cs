using SLQuickMsg.Input;

namespace SLQuickMsg;

/// <summary>把一条命令快速打进游戏控制台：~ → 文本 → 回车 → Esc。</summary>
internal static class GameSend
{
    private const int ConsoleOpenDelayMs = 30;
    private const int BeforeSubmitDelayMs = 18;
    private const int BeforeCloseDelayMs = 18;

    public static void SendCommand(string fullCommand)
    {
        InputSender.PressKey(InputSender.VkTilde);
        Thread.Sleep(ConsoleOpenDelayMs);

        InputSender.TypeText(fullCommand);
        Thread.Sleep(BeforeSubmitDelayMs);

        InputSender.PressKey(InputSender.VkReturn);
        Thread.Sleep(BeforeCloseDelayMs);

        InputSender.PressKey(InputSender.VkEscape);
    }
}
