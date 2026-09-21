using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using System.Numerics;

namespace ArenaPilot;

public static class EntryAction
{
    public static unsafe bool TryCommenceEntry(out string error)
    {
        var addon = AddonUi.FindReadyByNameContains("ContentsFinder");
        if (addon == null)
        {
            error = "出发窗口尚未准备好";
            return false;
        }

        AtkResNode* buttonNode = null;
        foreach (var label in new[] { "出发", "出發", "突入", "Commence" })
        {
            buttonNode = AddonUi.FindButtonNodeByText(&addon->UldManager, label, 0);
            if (buttonNode != null)
                break;
        }
        if (buttonNode == null || (int)buttonNode->Type < 1000 || !buttonNode->IsVisible())
        {
            error = "出发按钮当前不可用";
            return false;
        }

        var buttonComponent = ((AtkComponentNode*)buttonNode)->Component;
        if (buttonComponent == null
            || (int)buttonComponent->GetComponentType() != 1
            || !((AtkComponentButton*)buttonComponent)->IsEnabled)
        {
            error = "出发按钮当前不可用";
            return false;
        }

        var clicks = new List<(nint Event, uint OwnerNodeId, bool Direct)>();
        AddonUi.CollectClickEvents(&addon->UldManager, buttonNode->NodeId, false, clicks, 0);
        var direct = clicks.FirstOrDefault(x => x.Direct);
        if (direct.Event == 0 && clicks.Count > 0)
            direct = clicks[0];

        if (direct.Event != 0)
        {
            var eventPtr = (AtkEvent*)direct.Event;
            var eventValue = *eventPtr;
            var eventData = default(AtkEventData);
            eventPtr->Listener->ReceiveEvent((AtkEventType)25, (int)eventPtr->Param, &eventValue, &eventData);
            error = string.Empty;
            return true;
        }

        var fallbackEvent = default(AtkEvent);
        var fallbackData = default(AtkEventData);
        addon->ReceiveEvent((AtkEventType)25, (int)buttonNode->NodeId, &fallbackEvent, &fallbackData);
        var fallbackComponent = ((AtkComponentNode*)buttonNode)->Component;
        if (fallbackComponent != null)
            fallbackComponent->ReceiveEvent((AtkEventType)25, 0, &fallbackEvent, &fallbackData);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TryConfirmChallenge(out string error)
        => AddonUi.TryFireCallback("SelectYesno", 0, out error);

    public static unsafe bool TryRejectChallenge(out string error)
        => AddonUi.TryFireCallback("SelectYesno", 1, out error);

    public static unsafe bool TryAdvanceEntry(out string error, int targetStage = 1)
    {
        var stageDetail = AddonUi.GetReady("XBMStageDetailList");
        var party = AddonUi.GetReady("XBMPetParty");
        if (stageDetail != null && party != null)
            return AddonUi.TrySendRegisteredEvent(stageDetail, (AtkEventType)25, 9, "挑战此奇盘按钮", out error);

        var stageList = AddonUi.GetReady("XBMStageList");
        if (stageList != null)
        {
            var list = stageList->GetComponentListById(2);
            if (list == null || list->ListLength < 1 || list->ListLength > 5 || list->IsUpdatePending)
            {
                error = "斗兽盘列表尚未加载完成";
                return false;
            }
            var item = Math.Clamp(targetStage - 1, 0, list->ListLength - 1);
            if (list->GetItemDisabledState(item) || !list->IsItemInteractionEnabled)
            {
                error = $"目标斗兽盘（第 {targetStage} 层）当前不可进入";
                return false;
            }
            list->SelectItem(item, false);
            list->DispatchItemEvent(item, AtkEventType.ListItemClick);
            error = string.Empty;
            return true;
        }

        if (TryClickEntryMenu("挑战“斗兽奇弈”", out error))
            return true;
        if (TryClickEntryMenu("斗兽奇弈", out error))
            return true;
        if (TryClickEntryMenu("鬥獸奇弈", out error))
            return true;
        if (TryClickEntryMenu("挑戰「鬥獸奇奕」", out error))
            return true;
        if (TryClickEntryMenuContains("闘獣練", out error))
            return true;
        if (TryClickEntryMenuContains("Crucible of the Unbroken", out error))
            return true;

        var npc = DalamudApi.ObjectTable
            .OfType<IGameObject>()
            .FirstOrDefault(x => new[] { "劳妲", "勞妲", "ラウダ", "Lauda" }
                .Contains(x.Name.TextValue, StringComparer.OrdinalIgnoreCase) && x.IsTargetable);
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (npc == null)
        {
            error = "没有找到劳妲，请先到斗兽塔入口";
            return false;
        }
        if (player == null || Vector3.Distance(player.Position, npc.Position) > 4f)
        {
            error = "距离劳妲太远，请先靠近她";
            return false;
        }

        DalamudApi.TargetManager.Target = npc;
        var targetSystem = TargetSystem.Instance();
        if (targetSystem == null)
        {
            error = "目标系统尚未准备好";
            return false;
        }
        targetSystem->InteractWithObject((GameObject*)npc.Address, true);
        error = "已与劳妲交互，等待菜单";
        return true;
    }

    private static unsafe bool TryClickEntryMenu(string expected, out string error)
    {
        var icon = AddonUi.GetReady("SelectIconString");
        var text = AddonUi.GetReady("SelectString");
        var addon = icon != null ? icon : text;
        if (addon == null || (icon != null && text != null))
        {
            error = "入口菜单尚未出现";
            return false;
        }

        var list = addon->GetComponentListById(3);
        if (list == null || list->ListLength < 1 || list->ListLength > 64 || list->IsUpdatePending)
        {
            error = "入口菜单尚未加载完成";
            return false;
        }
        for (var row = 0; row < list->ListLength; row++)
        {
            var label = list->GetItemLabel(row);
            if (label.Value == null)
                continue;
            var actual = new ReadOnlySeStringSpan(label.Value).ExtractText();
            if (!string.Equals(Normalize(actual), Normalize(expected), StringComparison.Ordinal))
                continue;
            if (list->GetItemDisabledState(row) || !list->IsItemInteractionEnabled)
            {
                error = $"入口选项“{expected}”当前不可用";
                return false;
            }
            list->SelectItem(row, false);
            list->DispatchItemEvent(row, AtkEventType.ListItemClick);
            error = string.Empty;
            return true;
        }
        error = $"入口菜单中没有找到“{expected}”";
        return false;
    }

    private static unsafe bool TryClickEntryMenuContains(string expected, out string error)
    {
        var icon = AddonUi.GetReady("SelectIconString");
        var text = AddonUi.GetReady("SelectString");
        var addon = icon != null ? icon : text;
        if (addon == null || (icon != null && text != null))
        {
            error = "入口菜单尚未出现";
            return false;
        }

        var list = addon->GetComponentListById(3);
        if (list == null || list->IsUpdatePending || !list->IsItemInteractionEnabled)
        {
            error = "入口菜单尚未加载完成";
            return false;
        }
        for (var row = 0; row < list->ListLength; row++)
        {
            var label = list->GetItemLabel(row);
            if (label.Value == null)
                continue;
            var actual = new ReadOnlySeStringSpan(label.Value).ExtractText();
            if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
                continue;
            if (list->GetItemDisabledState(row))
            {
                error = $"入口选项“{expected}”当前不可用";
                return false;
            }
            list->SelectItem(row, false);
            list->DispatchItemEvent(row, AtkEventType.ListItemClick);
            error = string.Empty;
            return true;
        }
        error = $"入口菜单中没有找到“{expected}”";
        return false;
    }

    private static string Normalize(string text)
        => string.Concat(text.Where(x => !char.IsWhiteSpace(x)));
}
