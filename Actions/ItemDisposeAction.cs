using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ArenaPilot;

public sealed record DisposeItemSlot(int Slot, uint ItemId, string Name);

public static class ItemDisposeAction
{
    public static bool TryRead(ArenaSnapshot snapshot, out uint targetItemId, out bool isPurchase,
        out bool isEquipment, out IReadOnlyList<DisposeItemSlot> items, out string error)
    {
        targetItemId = 0;
        isPurchase = false;
        isEquipment = false;
        items = [];
        var addon = snapshot.Addons.FirstOrDefault(x => x.Name == "XBMContentsItemDispose" && x.IsReady);
        if (addon == null)
        {
            error = "道具清理界面尚未准备好";
            return false;
        }

        var values = addon.Values.ToDictionary(x => x.Index);
        if (!TryNumber(values, 5, out var targetRaw) || targetRaw <= 0)
        {
            error = "待获取道具尚未识别";
            return false;
        }
        targetItemId = (uint)targetRaw;
        isPurchase = snapshot.Addons.Any(x => x.Name == "XBMContentsItemShop" && x.IsReady)
            || values.TryGetValue(1, out var prompt)
                && (prompt.Text?.Contains("想购买", StringComparison.Ordinal) == true
                    || prompt.Text?.Contains("購入", StringComparison.Ordinal) == true
                    || prompt.Text?.Contains("purchase", StringComparison.OrdinalIgnoreCase) == true);

        var header = string.Join(" ", values.Values
            .Where(x => x.Index is 1 or 2 or 3 && !string.IsNullOrWhiteSpace(x.Text))
            .Select(x => x.Text));
        isEquipment = header.Contains("斗兽装备", StringComparison.Ordinal)
            || header.Contains("鬥獸裝備", StringComparison.Ordinal)
            || header.Contains("装備", StringComparison.Ordinal)
            || header.Contains("equipment", StringComparison.OrdinalIgnoreCase);

        var result = new List<DisposeItemSlot>();
        if (isEquipment)
        {
            for (var slot = 0; slot < 10; slot++)
            {
                var offset = 8 + slot * 5;
                if (!TryNumber(values, offset + 1, out var itemRaw) || itemRaw <= 0)
                    continue;
                var name = values.TryGetValue(offset + 2, out var nameValue)
                    ? nameValue.Text ?? string.Empty
                    : string.Empty;
                result.Add(new DisposeItemSlot(slot, (uint)itemRaw, name));
            }
        }
        else
        {
            for (var slot = 0; slot < 10; slot++)
            {
                var offset = 6 + slot * 5;
                if (!TryNumber(values, offset + 3, out var itemRaw) || itemRaw <= 0)
                    continue;
                var name = values.TryGetValue(offset + 4, out var nameValue)
                    ? nameValue.Text ?? string.Empty
                    : string.Empty;
                result.Add(new DisposeItemSlot(slot, (uint)itemRaw, name));
            }
        }

        items = result;
        error = string.Empty;
        return true;
    }

    public static bool TrySelectSlot(int slot, out string error)
        => AddonUi.TrySendNodeEvent("XBMContentsItemDispose", (uint)(14 + slot * 2),
            (AtkEventType)9, (uint)(slot + 1), out error);

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
