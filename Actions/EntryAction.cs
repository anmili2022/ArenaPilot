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

        if (TryClickEntryMenu(out error))
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

    private static unsafe bool TryClickEntryMenu(out string error)
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

        var labels = new List<string>((int)list->ListLength);
        var match = -1;
        var exclusion = string.Empty;
        for (var row = 0; row < list->ListLength; row++)
        {
            var label = list->GetItemLabel(row);
            if (label.Value == null)
                continue;
            var actual = new ReadOnlySeStringSpan(label.Value).ExtractText();
            labels.Add(actual);
            if (IsEntryMenuExcluded(actual))
            {
                exclusion = actual;
                continue;
            }
            if (match < 0 && IsEntryMenuTarget(actual))
                match = (int)row;
        }

        if (match < 0)
        {
            var dump = labels.Count > 0 ? string.Join(" / ", labels) : "（空）";
            error = exclusion.Length > 0
                ? $"入口菜单只识别到“{exclusion}”等非进入项，当前无可点击的进入项：{dump}"
                : $"入口菜单中没有找到进入项：{dump}";
            return false;
        }
        if (list->GetItemDisabledState(match) || !list->IsItemInteractionEnabled)
        {
            error = $"入口选项“{labels[match]}”当前不可用";
            return false;
        }

        list->SelectItem(match, false);
        list->DispatchItemEvent(match, AtkEventType.ListItemClick);
        error = string.Empty;
        return true;
    }

    private static bool IsEntryMenuTarget(string label)
        => EntryMenuTargets.Any(x => label.Contains(x, StringComparison.OrdinalIgnoreCase));

    public static bool IsEntryMenuExcluded(string label)
        => EntryMenuExclusions.Any(x => label.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static readonly string[] EntryMenuTargets =
    [
        "挑战“斗兽奇弈”", "斗兽奇弈", "鬥獸奇弈", "挑戰「鬥獸奇奕」", // 简中、繁中
        "闘獣練", "この盤面に挑む", "盤面に挑", // 日文
        "Challenge this board", "Crucible of the Unbroken", // 英文
    ];

    private static readonly string[] EntryMenuExclusions =
    [
        "中断", "再開", // 中断数据继续，不自动点击
        "魔獣図鑑", "魔物图鉴", "魔物圖鑑", "图鉴", "圖鑑", // 图鉴
        "について聞く", "聞く", "询问", "詢問", // 询问说明
        "話す", "对话", "對話", // 交谈
        "キャンセル", "取消", // 取消
    ];
}
