using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Text.ReadOnly;
using System.Numerics;

namespace ArenaPilot;

public sealed record ShopCatalogEntry(string Category, uint Id, string Name, bool NameResolved);

public sealed class ArenaUiReader
{
    private static readonly string[] CandidateAddons =
    [
        "XBMStageList",
        "XBMStageDetailList",
        "XBMStageMap",
        "XBMPetParty",
        "XBMMonsterNotebook",
        "XBMContentsMainHUD",
        "XBMContentsBooty",
        "XBMContentsTreasure",
        "XBMContentsItemShop",
        "XBMContentsItemDispose",
        "XBMResult",
        "ContentsFinder",
        "ContentsFinderConfirm",
        "SelectString",
        "SelectIconString",
        "SelectYesno",
        "SelectOk",
        "Talk",
        "ContextMenu",
        "JournalDetail",
    ];

    public unsafe ArenaSnapshot Read(ArenaPhase requestedPhase, string failureReason = "")
    {
        var visibleAddons = CandidateAddons
            .Where(name => IsAddonVisible(name))
            .Concat(ReadVisibleArenaAddonNames())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var currentAddon = visibleAddons.FirstOrDefault() ?? string.Empty;
        var territoryId = DalamudApi.ClientState.TerritoryType;
        var contentId = GameMain.Instance() == null
            ? 0u
            : GameMain.Instance()->CurrentContentFinderConditionId;
        var stageId = contentId is >= 1088 and <= 1092 ? (int)contentId - 1087 : 0;
        var playerPosition = DalamudApi.ObjectTable.LocalPlayer?.Position;
        var route = ArenaRoutes.Find(territoryId, contentId);
        var isOnArenaBoard = route != null
            && playerPosition.HasValue
            && route.IsOnBoard(playerPosition.Value);
        var currentNode = route != null && playerPosition.HasValue
            ? route.FindNode(playerPosition.Value)
            : null;
        var currentNodeKind = route?.GetKind(currentNode) ?? ArenaNodeKind.Unknown;
        var addonSnapshots = ReadAddonSnapshots(visibleAddons);
        var (phase, phaseReason) = requestedPhase is ArenaPhase.Paused or ArenaPhase.Failed
            ? (requestedPhase, "当前读取已暂停")
            : DetectPhase(territoryId, contentId, visibleAddons, route, currentNode, isOnArenaBoard, addonSnapshots);

        return new ArenaSnapshot(
            DateTime.UtcNow,
            phase,
            stageId,
            contentId,
            DalamudApi.ClientState.IsLoggedIn,
            territoryId,
            territoryId == 0 ? string.Empty : territoryId.ToString(),
            playerPosition?.X,
            playerPosition?.Y,
            playerPosition?.Z,
            currentNode,
            currentNodeKind,
            currentAddon,
            visibleAddons,
            failureReason,
            phaseReason,
            addonSnapshots);
    }

    private static unsafe IReadOnlyList<ArenaAddonSnapshot> ReadAddonSnapshots(IReadOnlyList<string> addonNames)
    {
        var result = new List<ArenaAddonSnapshot>();
        foreach (var name in addonNames)
        {
            var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name, 1).Address;
            if (addon == null)
                continue;

            var count = Math.Clamp((int)addon->AtkValuesCount, 0, 4096);
            var values = new List<ArenaAddonValue>();
            if (addon->AtkValues != null)
            {
                for (var i = 0; i < count; i++)
                {
                    var value = addon->AtkValues[i];
                    var type = value.Type & (AtkValueType)0x1f;
                    long? number = type switch
                    {
                        AtkValueType.Bool => value.Bool ? 1 : 0,
                        AtkValueType.Int => value.Int,
                        AtkValueType.UInt => value.UInt,
                        AtkValueType.Int64 => value.Int64,
                        AtkValueType.UInt64 when value.UInt64 <= long.MaxValue => (long)value.UInt64,
                        _ => null,
                    };
                    string? text = null;
                    if ((type is AtkValueType.String or AtkValueType.ConstString) && value.String.Value != null)
                    {
                        var span = new ReadOnlySeStringSpan(value.String.Value);
                        text = span.ExtractText();
                        if (text.Length > 2048)
                            text = text[..2048];
                    }

                    // Empty values are not useful for diagnostics and make snapshots unnecessarily large.
                    if (number.HasValue || !string.IsNullOrEmpty(text) || type != AtkValueType.Undefined)
                        values.Add(new ArenaAddonValue(i, type.ToString(), number, text));
                }
            }

            result.Add(new ArenaAddonSnapshot(name, addon->IsReady, count, values));
        }

        return result;
    }

    private static (ArenaPhase Phase, string Reason) DetectPhase(
        uint territoryId,
        uint contentId,
        IReadOnlyList<string> addons,
        ArenaStageRoute? route,
        int? currentNode,
        bool isOnArenaBoard,
        IReadOnlyList<ArenaAddonSnapshot> addonSnapshots)
    {
        bool Has(string name) => addons.Contains(name, StringComparer.Ordinal);
        if (territoryId is >= 1339 and <= 1343 && contentId is >= 1088 and <= 1092)
        {
            if (Has("XBMResult"))
                return (ArenaPhase.Result, "已打开斗兽结算界面");
            if (Has("XBMContentsItemDispose"))
                return (ArenaPhase.ItemDispose, "奇弈道具栏已满，等待选择丢弃道具");
            if (Has("XBMContentsTreasure"))
                return (ArenaPhase.Treasure, "正在选择宝箱奖励");
            if (Has("XBMContentsItemShop") || TryFindShopAddon(addonSnapshots, out _))
                return (ArenaPhase.Shop, "正在处理商店");
            if (Has("XBMContentsBooty"))
                return (ArenaPhase.Loot, "正在领取战斗奖励");
            if (Has("XBMPetParty") && Has("XBMStageDetailList"))
                return (ArenaPhase.PartySetup, "正在编队准备战斗");
            if (Has("XBMPetParty") && route?.GetKind(currentNode) == ArenaNodeKind.Rest)
                return (ArenaPhase.Rest, "正在处理休息帐篷");
            if (Has("XBMPetParty") && IsRestAddon(addonSnapshots))
                return (ArenaPhase.Rest, "正在处理休息帐篷");
            if (Has("XBMPetParty") && (IsConditionActive(31) || IsConditionActive(33)))
                return (ArenaPhase.PartySetup, "正在编队或处理休息界面");
            if (IsConditionActive(26))
                return (ArenaPhase.Battle, "正在战斗");
            if (currentNode.HasValue)
                return (ArenaPhase.Board, $"已回到棋盘，当前节点 {currentNode.Value}");
            if (isOnArenaBoard)
                return (ArenaPhase.Board, "正在棋盘节点之间移动");
            if (Has("XBMContentsMainHUD") && !Has("XBMStageDetailList"))
                return (ArenaPhase.BattlePreparation, currentNode.HasValue
                    ? "已离开棋盘，战斗场景已加载，等待战斗开始"
                    : "战斗场景已加载，等待玩家位置数据");
            if (Has("XBMContentsMainHUD") || Has("XBMStageDetailList"))
                return (ArenaPhase.Board, "正在棋盘界面");
            return (ArenaPhase.Loading, "已进入斗兽副本，等待界面加载");
        }

        if (territoryId == 148 && (Has("SelectString") || Has("SelectIconString") || Has("XBMStageList")
            || Has("XBMStageDetailList") || Has("XBMPetParty")))
            return (Has("XBMStageList") ? ArenaPhase.StageList : ArenaPhase.Entry, "正在入口菜单或选择关卡");

        return (ArenaPhase.Unknown, "当前不是已识别的斗兽界面");
    }

    private static bool IsRestAddon(IReadOnlyList<ArenaAddonSnapshot> addons)
        => addons.FirstOrDefault(x => x.Name == "XBMPetParty" && x.IsReady)
            ?.Values.Any(x => x.Index == 1166 && x.Text == "休息") == true;

    public static bool TryFindShopAddon(IReadOnlyList<ArenaAddonSnapshot> addons, out string name)
    {
        var shop = addons.FirstOrDefault(x => x.IsReady && x.Values.Any(v =>
            v.Text?.Contains("请选择要购买的临时道具", StringComparison.Ordinal) == true
            || v.Text?.Contains("可以贩卖当前持有的道具", StringComparison.Ordinal) == true));
        name = shop?.Name ?? string.Empty;
        return shop != null;
    }

    private static bool IsConditionActive(int value)
    {
        try
        {
            return DalamudApi.Condition[(ConditionFlag)value];
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Debug(ex, "Unable to inspect condition flag {ConditionFlag}.", value);
            return false;
        }
    }

    private static bool IsAddonVisible(string name)
    {
        try
        {
            return DalamudApi.GameGui.GetAddonByName(name, 1) != nint.Zero;
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Debug(ex, "Unable to inspect addon {AddonName}.", name);
            return false;
        }
    }

    private static unsafe IReadOnlyList<string> ReadVisibleArenaAddonNames()
    {
        var result = new List<string>();
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null)
            return result;

        var units = &manager->AllLoadedUnitsList;
        var count = Math.Min((int)units->Count, 256);
        for (var i = 0; i < count; i++)
        {
            var addon = units->Entries[i].Value;
            if (addon == null || !addon->IsVisible)
                continue;

            var name = addon->NameString;
            if (name.StartsWith("XBM", StringComparison.Ordinal)
                || name.Contains("Contents", StringComparison.Ordinal)
                || name.Contains("斗兽", StringComparison.Ordinal))
            {
                result.Add(name);
            }
        }

        return result;
    }

    public static unsafe string BuildBoardDump()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var name in new[] { "XBMStageMap", "XBMStageDetailList", "XBMStageList", "XBMContentsMainHUD", "XBMPetParty" })
        {
            DumpAddon(sb, name);
        }
        return sb.ToString();
    }

    public static unsafe string BuildAddonDump(string name)
    {
        var sb = new System.Text.StringBuilder();
        DumpAddon(sb, name);
        return sb.ToString();
    }

    public static unsafe string BuildShopDataDump()
    {
        var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMContentsItemShop", 1).Address;
        var sb = new System.Text.StringBuilder().AppendLine("===== 商店道具与装备 =====");
        if (addon == null || !addon->IsReady || addon->AtkValues == null)
            return sb.AppendLine("商店界面不存在或尚未准备好。").ToString();

        var values = addon->AtkValues;
        var valueCount = (int)addon->AtkValuesCount;
        var gold = valueCount > 1 ? ReadValueText(values[1]) : string.Empty;
        var productCount = valueCount > 2 ? (int)values[2].UInt : 0;
        sb.AppendLine($"金币：{gold}");
        sb.AppendLine($"商品总数：{productCount}");
        sb.AppendLine("商品（已过滤食料）：");

        var equipmentNames = new Dictionary<uint, string>();
        for (var slot = 0; slot < 10; slot++)
        {
            var offset = 205 + slot * 5;
            if (offset + 4 >= valueCount)
                break;
            var itemId = values[offset + 3].UInt;
            var name = ReadValueText(values[offset + 4]).Trim();
            if (values[offset].Bool && itemId > 0 && !string.IsNullOrWhiteSpace(name))
                equipmentNames[itemId] = name;
        }

        for (var slot = 0; slot < Math.Min(productCount, 30); slot++)
        {
            var offset = 3 + slot * 5;
            if (offset + 4 >= valueCount || !values[offset].Bool)
                continue;

            var itemId = values[offset + 1].UInt;
            // 当前已确认：76~143 为奇弈道具，低于76为斗兽装备；144以上为食料。
            if (itemId >= 144 || itemId == 0)
                continue;

            var category = itemId >= 76 ? "奇弈道具" : "斗兽装备";
            var price = ReadValueText(values[offset + 2]);
            var discounted = values[offset + 3].Bool;
            var purchased = values[offset + 4].Bool;
            var nodeId = slot == 0 ? 3u : (uint)(31000 + slot);
            var fallbackName = itemId >= 76
                ? CrucibleItemCatalog.GetName(itemId)
                : equipmentNames.GetValueOrDefault(itemId, $"未知斗兽装备 {itemId}");
            var name = ReadShopProductName(addon, slot, nodeId, fallbackName);
            sb.AppendLine($"  槽位={slot} | 类别={category} | ID={itemId} | 名称={name} | 价格={price} | 打折={discounted} | 已购买={purchased} | NodeId={nodeId}");
        }

        var itemCapacity = ReadSlotCapacity(values, valueCount, 154, 5, 10);
        sb.AppendLine($"当前奇弈道具栏：容量={itemCapacity}");
        for (var slot = 0; slot < itemCapacity; slot++)
        {
            var offset = 154 + slot * 5;
            var itemId = values[offset + 3].UInt;
            if (itemId == 0)
            {
                sb.AppendLine($"  槽位={slot} | 空");
                continue;
            }
            sb.AppendLine($"  槽位={slot} | ID={itemId} | 名称={ReadValueText(values[offset + 4])}");
        }

        var equipmentCapacity = 0;
        var equipmentCount = 0;
        var equipmentLines = new List<string>();
        for (var slot = 0; slot < 10; slot++)
        {
            var offset = 205 + slot * 5;
            if (offset + 4 >= valueCount)
                break;
            equipmentCapacity++;
            var itemId = values[offset + 3].UInt;
            if (!values[offset].Bool || itemId == 0)
                continue;
            equipmentCount++;
            equipmentLines.Add($"  槽位={slot} | ID={itemId} | 名称={ReadValueText(values[offset + 4])}");
        }
        sb.AppendLine($"当前斗兽装备栏：容量={equipmentCapacity}，已有={equipmentCount}，空位={equipmentCapacity - equipmentCount}");
        foreach (var line in equipmentLines)
            sb.AppendLine(line);

        return sb.ToString();
    }

    public static unsafe IReadOnlyList<ShopCatalogEntry> ReadShopCatalog()
    {
        var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName("XBMContentsItemShop", 1).Address;
        if (addon == null || !addon->IsReady || addon->AtkValues == null)
            return [];

        var values = addon->AtkValues;
        var valueCount = (int)addon->AtkValuesCount;
        if (valueCount <= 2)
            return [];

        var equipmentNames = new Dictionary<uint, string>();
        for (var slot = 0; slot < 10; slot++)
        {
            var offset = 205 + slot * 5;
            if (offset + 4 >= valueCount)
                break;
            var itemId = values[offset + 3].UInt;
            var name = ReadValueText(values[offset + 4]).Trim();
            if (values[offset].Bool && itemId > 0 && !string.IsNullOrWhiteSpace(name))
                equipmentNames[itemId] = name;
        }

        var productCount = Math.Clamp((int)values[2].UInt, 0, 30);
        var result = new List<ShopCatalogEntry>();
        result.AddRange(equipmentNames.Select(x =>
            new ShopCatalogEntry("斗兽装备", x.Key, x.Value, true)));
        for (var slot = 0; slot < productCount; slot++)
        {
            var offset = 3 + slot * 5;
            if (offset + 4 >= valueCount || !values[offset].Bool)
                continue;

            var itemId = values[offset + 1].UInt;
            if (itemId == 0)
                continue;

            var category = GetShopItemCategory(itemId);
            var fallbackName = GetShopItemFallbackName(category, itemId, equipmentNames);
            var nodeId = slot == 0 ? 3u : (uint)(31000 + slot);
            var name = ReadShopProductName(addon, slot, nodeId, fallbackName);
            result.Add(new ShopCatalogEntry(category, itemId, name,
                !IsUnknownShopItemName(name)
                && (!string.Equals(name, fallbackName, StringComparison.Ordinal)
                    || IsKnownShopItemName(category, itemId, equipmentNames))));
        }
        return result;
    }

    private static string GetShopItemCategory(uint itemId)
        => itemId switch
        {
            < 76 => "斗兽装备",
            <= 143 => "奇弈道具",
            >= 144 => "食料",
        };

    private static string GetShopItemFallbackName(
        string category,
        uint itemId,
        IReadOnlyDictionary<uint, string> equipmentNames)
    {
        var known = CrucibleItemCatalog.AllItems.FirstOrDefault(x => x.Id == itemId);
        if (known != null)
            return known.Name;
        if (category == "斗兽装备" && equipmentNames.TryGetValue(itemId, out var equipmentName))
            return equipmentName;
        return $"未知{category} {itemId}";
    }

    private static bool IsKnownShopItemName(
        string category,
        uint itemId,
        IReadOnlyDictionary<uint, string> equipmentNames)
        => CrucibleItemCatalog.AllItems.Any(x => x.Id == itemId)
            || category == "斗兽装备" && equipmentNames.ContainsKey(itemId);

    private static bool IsUnknownShopItemName(string name)
        => string.IsNullOrWhiteSpace(name)
            || name.StartsWith("未知", StringComparison.Ordinal)
            || name.StartsWith("奇弈道具 ", StringComparison.Ordinal);

    private static unsafe string ReadShopProductName(
        AtkUnitBase* addon,
        int productSlot,
        uint cardNodeId,
        string fallbackName)
    {
        var gridNode = FindDirectNode(&addon->UldManager, 5);
        if (gridNode == null || (int)gridNode->Type < 1000)
            return fallbackName;

        var grid = ((AtkComponentNode*)gridNode)->Component;
        var list = (AtkComponentList*)grid;
        if (list != null && productSlot >= 0 && productSlot < list->ListLength)
        {
            var renderer = list->GetItemRenderer(productSlot);
            var rendererName = renderer == null
                ? string.Empty
                : ReadShopProductNameFromManager(&renderer->UldManager);
            if (!string.IsNullOrWhiteSpace(rendererName))
                return rendererName;
        }

        var cardNode = grid == null ? null : FindNode(&grid->UldManager, cardNodeId, 0);
        if (cardNode == null || (int)cardNode->Type < 1000)
            return fallbackName;

        var card = ((AtkComponentNode*)cardNode)->Component;
        var cardName = card == null ? string.Empty : ReadShopProductNameFromManager(&card->UldManager);
        return string.IsNullOrWhiteSpace(cardName) ? fallbackName : cardName;
    }

    private static unsafe string ReadShopProductNameFromManager(AtkUldManager* manager)
    {
        var buttonNode = FindDirectNode(manager, 2);
        var button = buttonNode != null && (int)buttonNode->Type >= 1000
            ? ((AtkComponentNode*)buttonNode)->Component
            : null;
        var name = button == null ? string.Empty : ReadShopProductNameFromButton(&button->UldManager);
        return !string.IsNullOrWhiteSpace(name)
            ? name
            : ReadShopProductNameFromButton(manager);
    }

    private static unsafe string ReadShopProductNameFromButton(AtkUldManager* manager)
    {
        var detailNode = FindDirectNode(manager, 13);
        if (detailNode == null || (int)detailNode->Type < 1000)
            return string.Empty;

        var detail = ((AtkComponentNode*)detailNode)->Component;
        var nameNode = detail == null ? null : FindDirectNode(&detail->UldManager, 3);
        if (nameNode == null || (int)nameNode->Type != 3)
            return string.Empty;

        var textNode = (AtkTextNode*)nameNode;
        return new ReadOnlySeStringSpan(((Utf8String*)(&textNode->NodeText))->StringPtr).ExtractText().Trim();
    }

    private static unsafe AtkResNode* FindNode(AtkUldManager* manager, uint nodeId, int depth)
    {
        if (depth > 6 || manager->NodeList == null || manager->NodeListCount > 2048)
            return null;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;
            if (node->NodeId == nodeId)
                return node;
            if ((int)node->Type < 1000)
                continue;
            var component = ((AtkComponentNode*)node)->Component;
            var found = component == null ? null : FindNode(&component->UldManager, nodeId, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }

    private static unsafe AtkResNode* FindDirectNode(AtkUldManager* manager, uint nodeId)
    {
        if (manager->NodeList == null || manager->NodeListCount > 2048)
            return null;
        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node != null && node->NodeId == nodeId)
                return node;
        }
        return null;
    }

    private static unsafe int ReadSlotCapacity(AtkValue* values, int valueCount, int start, int stride, int maximum)
    {
        var capacity = 0;
        for (var slot = 0; slot < maximum; slot++)
        {
            var offset = start + slot * stride;
            if (offset + stride - 1 >= valueCount || !values[offset].Bool)
                break;
            capacity++;
        }
        return capacity;
    }

    private static unsafe string ReadValueText(AtkValue value)
    {
        var type = value.Type & (AtkValueType)0x1f;
        return type switch
        {
            AtkValueType.String or AtkValueType.ConstString when value.String.Value != null
                => ReadText(value.String.Value, 512) ?? string.Empty,
            AtkValueType.Int => value.Int.ToString(),
            AtkValueType.UInt => value.UInt.ToString(),
            AtkValueType.Int64 => value.Int64.ToString(),
            AtkValueType.UInt64 => value.UInt64.ToString(),
            _ => string.Empty,
        };
    }

    private static unsafe void DumpAddon(System.Text.StringBuilder sb, string name)
    {
        var addon = (AtkUnitBase*)DalamudApi.GameGui.GetAddonByName(name, 1).Address;
        sb.AppendLine();
        sb.AppendLine($"===== {name} =====");
        if (addon == null)
        {
            sb.AppendLine("  (不存在)");
            return;
        }

        sb.AppendLine($"  Ready={addon->IsReady} Visible={addon->IsVisible} AtkValuesCount={addon->AtkValuesCount}");

        if (addon->AtkValues != null)
        {
            var count = Math.Clamp((int)addon->AtkValuesCount, 0, 4096);
            for (var i = 0; i < count; i++)
            {
                var value = addon->AtkValues[i];
                var type = value.Type & (AtkValueType)0x1f;
                string? rendered = type switch
                {
                    AtkValueType.Bool => value.Bool ? "true" : "false",
                    AtkValueType.Int => value.Int.ToString(),
                    AtkValueType.UInt => value.UInt.ToString(),
                    AtkValueType.Int64 => value.Int64.ToString(),
                    AtkValueType.UInt64 => value.UInt64.ToString(),
                    AtkValueType.Float => value.Float.ToString("0.####"),
                    AtkValueType.String or AtkValueType.ConstString when value.String.Value != null
                        => ReadText(value.String.Value, 512),
                    _ => null,
                };
                if (rendered != null)
                    sb.AppendLine($"  [{i}] {type} = {rendered}");
            }
        }

        DumpComponentTree(sb, &addon->UldManager, 1);
    }

    private static unsafe string? ReadText(byte* value, int max)
    {
        var span = new ReadOnlySeStringSpan(value);
        var text = span.ExtractText();
        return text.Length > max ? text[..max] : text;
    }

    private static unsafe void DumpComponentTree(System.Text.StringBuilder sb, AtkUldManager* manager, int depth)
    {
        if (depth > 6 || manager->NodeList == null || manager->NodeListCount > 2048)
            return;

        for (var i = 0; i < manager->NodeListCount; i++)
        {
            var node = manager->NodeList[i];
            if (node == null)
                continue;

            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}Node id={node->NodeId} type={(int)node->Type} x={node->X} y={node->Y} w={node->Width} h={node->Height} visible={node->IsVisible()}");

            var currentEvent = node->AtkEventManager.Event;
            var eventCount = 0;
            while (currentEvent != null && eventCount++ < 64)
            {
                sb.AppendLine($"{indent}  Event type={(int)currentEvent->State.EventType} param={currentEvent->Param} listener=0x{(nint)currentEvent->Listener:X}");
                currentEvent = currentEvent->NextEvent;
            }

            if ((int)node->Type == 3)
            {
                var textNode = (AtkTextNode*)node;
                var span = new ReadOnlySeStringSpan(((Utf8String*)(&textNode->NodeText))->StringPtr);
                var text = span.ExtractText();
                if (!string.IsNullOrEmpty(text))
                    sb.AppendLine($"{indent}  Text: {text}");
            }

            if ((int)node->Type >= 1000)
            {
                var componentNode = (AtkComponentNode*)node;
                var component = componentNode->Component;
                if (component != null)
                {
                    sb.AppendLine($"{indent}  ComponentType={(int)component->GetComponentType()}");
                    DumpComponentTree(sb, &component->UldManager, depth + 1);
                }
            }
        }
    }
}
