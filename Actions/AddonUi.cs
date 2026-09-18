using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using InteropGenerator.Runtime;
using Lumina.Text.ReadOnly;

namespace ArenaPilot;

internal static class AddonUi
{
    public static unsafe AtkUnitBase* FindReadyByNameContains(string text)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
            return null;

        var units = &manager->AllLoadedUnitsList;
        var count = Math.Min((int)units->Count, 256);
        for (var i = 0; i < count; i++)
        {
            var addon = units->Entries[i].Value;
            if (addon != null && addon->IsVisible && addon->IsReady
                && addon->NameString.Contains(text, StringComparison.Ordinal))
                return addon;
        }

        return null;
    }

    public static unsafe bool HasReadyByNameContains(string text)
        => FindReadyByNameContains(text) != null;

    public static unsafe AtkUnitBase* GetReady(string name)
    {
        var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name, 1).Address;
        return addon != null && addon->IsReady ? addon : null;
    }

    public static unsafe bool IsReady(string name)
        => GetReady(name) != null;

    public static unsafe bool TryFireCallback(string addonName, int callback, out string error)
    {
        var addon = GetReady(addonName);
        if (addon == null)
        {
            error = $"{addonName} 界面已关闭或尚未准备好";
            return false;
        }

        var value = default(AtkValue);
        value.Type = AtkValueType.Int;
        value.Int = callback;
        try
        {
            addon->FireCallback(1, &value, true);
            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, "斗兽界面操作失败：{Addon} / {Callback}", addonName, callback);
            error = $"{addonName} 操作失败";
            return false;
        }
    }

    public static unsafe bool TryRegisteredEvent(string addonName, uint parameter, out string error)
    {
        var addon = GetReady(addonName);
        if (addon == null)
        {
            error = $"{addonName} 界面已关闭或尚未准备好";
            return false;
        }

        return TrySendRegisteredEvent(addon, (AtkEventType)25, parameter, $"{addonName} 操作", out error);
    }

    public static unsafe bool TrySendRegisteredEvent(
        AtkUnitBase* addon,
        AtkEventType eventType,
        uint parameter,
        string actionName,
        out string error)
    {
        var found = new HashSet<nint>();
        FindEvents(&addon->UldManager, (AtkEventListener*)addon, eventType, parameter, found, 0);
        if (found.Count == 0)
        {
            error = $"{actionName}尚未准备好";
            return false;
        }
        if (found.Count != 1)
        {
            error = $"{actionName}不唯一，已停止";
            return false;
        }

        var eventPtr = (AtkEvent*)found.Single();
        var eventValue = *eventPtr;
        var eventData = default(AtkEventData);
        eventPtr->Listener->ReceiveEvent(eventType, (int)parameter, &eventValue, &eventData);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TryButtonRegisteredEvent(
        string addonName,
        uint nodeId,
        uint parameter,
        out string error)
    {
        var addon = GetReady(addonName);
        if (addon == null)
        {
            error = $"{addonName} 界面已关闭或尚未准备好";
            return false;
        }

        var button = addon->GetComponentButtonById(nodeId);
        if (button == null || !button->IsEnabled || button->OwnerNode == null || !button->OwnerNode->IsVisible())
        {
            error = $"{addonName} 的按钮 {nodeId} 当前不可用";
            return false;
        }

        var eventPtr = button->OwnerNode->AtkEventManager.Event;
        AtkEvent* found = null;
        var count = 0;
        while (eventPtr != null && count++ < 64)
        {
            if (eventPtr->Listener == (AtkEventListener*)addon
                && eventPtr->State.EventType == (AtkEventType)25
                && eventPtr->Param == parameter)
            {
                if (found != null)
                {
                    error = $"{addonName} 的按钮 {nodeId} 事件不唯一";
                    return false;
                }
                found = eventPtr;
            }
            eventPtr = eventPtr->NextEvent;
        }

        if (found == null)
        {
            error = $"{addonName} 的按钮 {nodeId} 事件尚未准备好";
            return false;
        }

        var eventValue = *found;
        var eventData = default(AtkEventData);
        found->Listener->ReceiveEvent((AtkEventType)25, (int)parameter, &eventValue, &eventData);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TryClickButtonByText(string addonName, string text, out string error)
    {
        var addon = GetReady(addonName);
        if (addon == null)
        {
            error = $"{addonName} 界面已关闭或尚未准备好";
            return false;
        }

        var buttonNode = FindButtonNodeByText(&addon->UldManager, text, 0);
        if (buttonNode == null || !buttonNode->IsVisible() || (int)buttonNode->Type < 1000)
        {
            error = $"没有找到可用的“{text}”按钮";
            return false;
        }

        var component = ((AtkComponentNode*)buttonNode)->Component;
        if (component == null
            || (int)component->GetComponentType() != 1
            || !((AtkComponentButton*)component)->IsEnabled)
        {
            error = $"“{text}”按钮当前不可用";
            return false;
        }

        var events = new List<(nint Event, uint OwnerNodeId, bool Direct)>();
        CollectClickEvents(&addon->UldManager, buttonNode->NodeId, false, events, 0);
        var found = events.FirstOrDefault(x => x.Direct);
        if (found.Event == 0 && events.Count > 0)
            found = events[0];
        if (found.Event == 0)
        {
            error = $"“{text}”按钮事件尚未准备好";
            return false;
        }

        var eventPtr = (AtkEvent*)found.Event;
        var eventValue = *eventPtr;
        var eventData = default(AtkEventData);
        eventPtr->Listener->ReceiveEvent((AtkEventType)25, (int)eventPtr->Param, &eventValue, &eventData);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TrySendNodeEvent(
        string addonName,
        uint targetNodeId,
        AtkEventType eventType,
        uint parameter,
        out string error)
    {
        var addon = GetReady(addonName);
        if (addon == null)
        {
            error = $"{addonName} 界面已关闭或尚未准备好";
            return false;
        }

        var found = new List<nint>();
        CollectNodeEvents(&addon->UldManager, targetNodeId, false,
            eventType, parameter, found, 0);
        if (found.Count != 1)
        {
            error = found.Count == 0
                ? $"节点 {targetNodeId} 的操作事件尚未准备好"
                : $"节点 {targetNodeId} 的操作事件不唯一";
            return false;
        }

        var eventPtr = (AtkEvent*)found[0];
        var eventValue = *eventPtr;
        var eventData = default(AtkEventData);
        eventPtr->Listener->ReceiveEvent(eventType, (int)parameter, &eventValue, &eventData);
        error = string.Empty;
        return true;
    }

    public static unsafe AtkResNode* FindButtonNodeByText(AtkUldManager* manager, string text, int depth)
    {
        if (depth > 6 || manager->NodeList == null || manager->NodeListCount > 2048)
            return null;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null || !node->IsVisible())
                continue;

            if ((int)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null && (int)component->GetComponentType() == 1)
                {
                    var button = (AtkComponentButton*)component;
                    if (button->ButtonTextNode != null)
                    {
                        var span = new ReadOnlySeStringSpan(
                            ((Utf8String*)(&button->ButtonTextNode->NodeText))->StringPtr);
                        if (string.Equals(span.ExtractText().Trim(), text, StringComparison.Ordinal)
                            || FindChildText(&component->UldManager, text, depth + 1))
                            return node;
                    }
                }
            }

            if ((int)node->Type >= 1000)
            {
                var component = ((AtkComponentNode*)node)->Component;
                if (component != null)
                {
                    var found = FindButtonNodeByText(&component->UldManager, text, depth + 1);
                    if (found != null)
                        return found;
                }
            }
        }

        return null;
    }

    public static unsafe bool FindChildText(AtkUldManager* manager, string text, int depth)
    {
        if (depth > 6 || manager->NodeList == null || manager->NodeListCount > 2048)
            return false;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null || !node->IsVisible())
                continue;
            if ((int)node->Type == 3)
            {
                var span = new ReadOnlySeStringSpan(
                    ((Utf8String*)(&((AtkTextNode*)node)->NodeText))->StringPtr);
                if (string.Equals(span.ExtractText().Trim(), text, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    public static unsafe void CollectClickEvents(
        AtkUldManager* manager,
        uint targetNodeId,
        bool insideTarget,
        List<(nint Event, uint OwnerNodeId, bool Direct)> found,
        int depth)
    {
        if (depth > 8 || manager->NodeList == null || manager->NodeListCount > 2048)
            return;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;

            var inside = insideTarget || node->NodeId == targetNodeId;
            var current = node->AtkEventManager.Event;
            var count = 0;
            while (current != null && count++ < 64)
            {
                if (inside && current->State.EventType == (AtkEventType)25 && current->Listener != null)
                    found.Add(((nint)current, node->NodeId, node->NodeId == targetNodeId));
                current = current->NextEvent;
            }

            if ((int)node->Type < 1000)
                continue;
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
                CollectClickEvents(&component->UldManager, targetNodeId, inside, found, depth + 1);
        }
    }

    private static unsafe void CollectNodeEvents(
        AtkUldManager* manager,
        uint targetNodeId,
        bool insideTarget,
        AtkEventType eventType,
        uint parameter,
        List<nint> found,
        int depth)
    {
        if (depth > 8 || manager->NodeList == null || manager->NodeListCount > 2048)
            return;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;

            var inside = insideTarget || node->NodeId == targetNodeId;
            var current = node->AtkEventManager.Event;
            var count = 0;
            while (current != null && count++ < 64)
            {
                if (inside
                    && current->State.EventType == eventType
                    && current->Param == parameter)
                    found.Add((nint)current);
                current = current->NextEvent;
            }

            if ((int)node->Type < 1000)
                continue;
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
                CollectNodeEvents(&component->UldManager, targetNodeId, inside,
                    eventType, parameter, found, depth + 1);
        }
    }

    private static unsafe void FindEvents(
        AtkUldManager* manager,
        AtkEventListener* listener,
        AtkEventType eventType,
        uint parameter,
        HashSet<nint> found,
        int depth)
    {
        if (depth > 6 || manager->NodeList == null || manager->NodeListCount > 2048)
            return;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null || !node->IsVisible())
                continue;

            var current = node->AtkEventManager.Event;
            var eventCount = 0;
            while (current != null && eventCount++ < 64)
            {
                if (node->NodeFlags.HasFlag(NodeFlags.Enabled)
                    && current->Listener == listener
                    && current->State.EventType == eventType
                    && current->Param == parameter)
                {
                    found.Add((nint)current);
                }
                current = current->NextEvent;
            }

            if ((int)node->Type < 1000)
                continue;
            var component = ((AtkComponentNode*)node)->Component;
            if (component != null)
                FindEvents(&component->UldManager, listener, eventType, parameter, found, depth + 1);
        }
    }
}
