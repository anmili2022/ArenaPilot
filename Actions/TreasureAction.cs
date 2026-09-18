namespace ArenaPilot;

public sealed record TreasureCandidate(int Slot, uint ItemId, string Name);

public static class TreasureAction
{
    private static readonly uint[] RecoveryItemIds =
        [76, 77, 78, 79, 80, 81, 82, 140, 141];

    public static bool TryTakePreferred(
        ArenaSnapshot snapshot,
        IReadOnlyList<uint> equipmentPriority,
        IReadOnlyList<uint> itemPriority,
        out string selectedName,
        out string error)
    {
        selectedName = string.Empty;
        var addon = snapshot.Addons.FirstOrDefault(x => x.Name == "XBMContentsTreasure" && x.IsReady);
        if (addon == null)
        {
            error = "宝箱界面尚未准备好";
            return false;
        }

        var values = addon.Values.ToDictionary(x => x.Index);
        var candidates = ReadCandidates(values);
        if (candidates.Count == 0)
        {
            error = "没有读取到宝箱候选奖励";
            return false;
        }

        var ownedItems = ReadOwnedIds(values, 24, 5, 10);
        var ownedEquipment = ReadOwnedIds(values, 75, 5, 10);
        var hasRecoveryItem = ownedItems.Any(x => RecoveryItemIds.Contains(x));
        var hasElementalAxe = CrucibleItemCatalog.HasElementalAxe(ownedEquipment);
        var eligibleCandidates = candidates
            .Where(x => !hasElementalAxe || !CrucibleItemCatalog.IsElementalAxe(x.ItemId))
            .ToArray();
        if (eligibleCandidates.Length == 0)
        {
            error = "候选奖励只有重复的属性斧，已跳过领取";
            return false;
        }
        TreasureCandidate? selected = null;
        if (!hasRecoveryItem)
        {
            foreach (var itemId in itemPriority.Where(x => RecoveryItemIds.Contains(x)))
            {
                selected = eligibleCandidates.FirstOrDefault(x => x.ItemId == itemId);
                if (selected != null)
                    break;
            }
            selected ??= eligibleCandidates.FirstOrDefault(x => RecoveryItemIds.Contains(x.ItemId));
        }
        else
        {
            foreach (var itemId in equipmentPriority)
            {
                selected = eligibleCandidates.FirstOrDefault(x => x.ItemId == itemId);
                if (selected != null)
                    break;
            }
        }

        selected ??= eligibleCandidates[0];
        selectedName = string.IsNullOrWhiteSpace(selected.Name)
            ? CrucibleItemCatalog.GetName(selected.ItemId)
            : selected.Name;
        return AddonUi.TryRegisteredEvent("XBMContentsTreasure", (uint)(2 + selected.Slot), out error);
    }

    public static bool TryExit(out string error)
        => AddonUi.TryButtonRegisteredEvent("XBMContentsTreasure", 44, 0, out error);

    public static bool TryDismissPopup(out string error)
        => AddonUi.TryFireCallback("SelectOk", 0, out error);

    public static bool IsCapacityPopup(ArenaSnapshot snapshot)
    {
        var popup = snapshot.Addons.FirstOrDefault(x => x.Name == "SelectOk" && x.IsReady);
        if (popup == null)
            return false;

        var text = string.Concat(popup.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text));
        return text.Contains("超出可持有上限", StringComparison.Ordinal)
            || text.Contains("逐个挑选获取", StringComparison.Ordinal);
    }

    private static IReadOnlyList<TreasureCandidate> ReadCandidates(
        IReadOnlyDictionary<int, ArenaAddonValue> values)
    {
        var result = new List<TreasureCandidate>();
        for (var slot = 0; slot < 4; slot++)
        {
            var offset = 3 + slot * 5;
            if (!TryBool(values, offset, out var available) || !available
                || !TryNumber(values, offset + 3, out var itemId) || itemId <= 0)
                continue;
            result.Add(new TreasureCandidate(slot, (uint)itemId, CrucibleItemCatalog.GetName((uint)itemId)));
        }
        return result;
    }

    private static IReadOnlyList<uint> ReadOwnedIds(
        IReadOnlyDictionary<int, ArenaAddonValue> values,
        int start,
        int stride,
        int count)
    {
        var result = new List<uint>();
        for (var slot = 0; slot < count; slot++)
        {
            var offset = start + slot * stride;
            if (TryNumber(values, offset + 3, out var itemId) && itemId > 0)
                result.Add((uint)itemId);
        }
        return result;
    }

    private static bool TryBool(IReadOnlyDictionary<int, ArenaAddonValue> values, int index, out bool value)
    {
        value = values.TryGetValue(index, out var raw) && raw.Number == 1;
        return values.ContainsKey(index);
    }

    private static bool TryNumber(IReadOnlyDictionary<int, ArenaAddonValue> values, int index, out long value)
    {
        if (values.TryGetValue(index, out var raw) && raw.Number.HasValue)
        {
            value = raw.Number.Value;
            return true;
        }
        value = 0;
        return false;
    }
}
