using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ArenaPilot;

public static class BattleAction
{
    public static unsafe bool TryStartBattle(out string error)
    {
        var addon = AddonUi.GetReady("XBMStageDetailList");
        if (addon == null)
        {
            error = "棋盘详情界面已关闭或尚未准备好";
            return false;
        }

        // XBMStageDetailList uses callback 8 to start a node battle.
        // Registered event 25/8 belongs to another control and opens the guide.
        var value = default(AtkValue);
        value.Type = AtkValueType.Int;
        value.Int = 8;
        try
        {
            addon->FireCallback(1, &value, true);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "发送战斗开始操作失败");
            error = "发送战斗开始操作失败";
            return false;
        }
    }

    public static bool TryCountdown(string command, out string error)
    {
        if (!DalamudApi.ClientState.IsLoggedIn || DalamudApi.ObjectTable.LocalPlayer == null)
        {
            error = "玩家尚未准备好，无法发送倒计时指令";
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            error = "倒计时指令为空";
            return false;
        }

        if (!GameCommandService.Execute(command.Trim()))
        {
            error = "倒计时指令发送失败";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
