using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Text;

namespace ArenaPilot;

public static class GameCommandService
{
    public static unsafe bool Execute(string command)
    {
        if (!command.StartsWith("/", StringComparison.Ordinal))
            return false;

        var bytes = Encoding.UTF8.GetBytes(command);
        if (bytes.Length is 0 or > 500)
            return false;

        var uiModule = UIModule.Instance();
        if (uiModule == null)
            return false;

        var message = Utf8String.FromSequence(bytes);
        try
        {
            uiModule->ProcessChatBoxEntry(message);
            return true;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "执行游戏命令失败：{Command}", command);
            return false;
        }
        finally
        {
            message->Dtor(true);
        }
    }
}
