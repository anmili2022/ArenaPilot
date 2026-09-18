using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ArenaPilot;

public sealed record ShopProduct(int Slot, uint ItemId, int Price, bool Purchased, uint NodeId);
public sealed record ShopItemSlot(int Slot, uint ItemId, string Name);
public sealed record ShopEquipmentSlot(int Slot, uint ItemId, string Name);
public sealed record ShopSnapshot(
    int Gold,
    IReadOnlyList<ShopProduct> Products,
    IReadOnlyList<ShopItemSlot> Items,
    int ItemCapacity,
    IReadOnlyList<ShopEquipmentSlot> Equipment,
    int EquipmentCapacity);

public static class ShopAction
{
    public static bool TryExit(out string error)
        => AddonUi.TryButtonRegisteredEvent("XBMContentsItemShop", 40, 0, out error);

    public static bool TryRead(ArenaSnapshot snapshot, out ShopSnapshot shop, out string error)
    {
        shop = new ShopSnapshot(0, [], [], 0, [], 0);
        var addon = snapshot.Addons.FirstOrDefault(x => x.Name == "XBMContentsItemShop" && x.IsReady);
        if (addon == null)
        {
            error = "商店界面尚未准备好";
            return false;
        }

        var values = addon.Values.ToDictionary(x => x.Index);
        if (!TryNonNegativeInt(values, 1, out var gold)
            || !TryNumber(values, 2, out var countRaw)
            || countRaw is < 0 or > 30)
        {
            error = "商店基础数据尚未准备好";
            return false;
        }

        var products = new List<ShopProduct>();
        for (var slot = 0; slot < countRaw; slot++)
        {
            var offset = 3 + slot * 5;
            if (!TryBool(values, offset, out var exists) || !exists
                || !TryNumber(values, offset + 1, out var itemIdRaw)
                || itemIdRaw <= 0
                || !TryNonNegativeInt(values, offset + 2, out var price)
                || !TryBool(values, offset + 4, out var purchased))
                continue;

            var nodeId = slot == 0 ? 3u : (uint)(31000 + slot);
            products.Add(new ShopProduct(slot, (uint)itemIdRaw, price, purchased, nodeId));
        }

        var items = new List<ShopItemSlot>();
        var capacity = 0;
        for (var slot = 0; slot < 10; slot++)
        {
            var offset = 154 + slot * 5;
            if (!TryBool(values, offset, out var open) || !open)
                break;
            capacity++;
            if (!TryNumber(values, offset + 3, out var itemIdRaw) || itemIdRaw <= 0)
                continue;
            TryText(values, offset + 4, out var name);
            items.Add(new ShopItemSlot(slot, (uint)itemIdRaw, name));
        }

        var equipment = new List<ShopEquipmentSlot>();
        var equipmentCapacity = 0;
        for (var slot = 0; slot < 10; slot++)
        {
            var offset = 205 + slot * 5;
            if (offset + 4 >= addon.ValueCount)
                break;
            equipmentCapacity++;
            if (!TryBool(values, offset, out var occupied) || !occupied
                || !TryNumber(values, offset + 3, out var itemIdRaw) || itemIdRaw <= 0)
                continue;
            TryText(values, offset + 4, out var name);
            equipment.Add(new ShopEquipmentSlot(slot, (uint)itemIdRaw, name));
        }

        shop = new ShopSnapshot(gold, products, items, capacity, equipment, equipmentCapacity);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TryBuy(int productSlot, out string error)
    {
        var addon = AddonUi.GetReady("XBMContentsItemShop");
        if (addon == null)
        {
            error = "商店界面尚未准备好";
            return false;
        }

        var list = addon->GetComponentListById(5);
        if (list == null
            || list->IsUpdatePending
            || !list->IsItemInteractionEnabled
            || productSlot < 0
            || productSlot >= list->ListLength
            || list->GetItemDisabledState(productSlot))
        {
            error = $"商店商品槽位 {productSlot} 当前不可操作";
            return false;
        }

        list->SelectItem(productSlot, false);
        list->DispatchItemEvent(productSlot, AtkEventType.ListItemClick);
        error = string.Empty;
        return true;
    }

    public static unsafe bool TryScrollToProduct(int productSlot, out int productCount)
    {
        productCount = 0;
        var addon = AddonUi.GetReady("XBMContentsItemShop");
        if (addon == null)
            return false;

        var list = addon->GetComponentListById(5);
        if (list == null || list->IsUpdatePending || list->ListLength <= 0)
            return false;

        productCount = list->ListLength;
        if (productSlot < 0 || productSlot >= productCount)
            return false;

        list->ScrollToItem((short)productSlot);
        return true;
    }

    public static bool TrySellItem(int itemSlot, out string error)
        => AddonUi.TrySendNodeEvent("XBMContentsItemShop", (uint)(16 + itemSlot),
            (FFXIVClientStructs.FFXIV.Component.GUI.AtkEventType)9, (uint)(6 + itemSlot), out error);

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

    private static bool TryNonNegativeInt(IReadOnlyDictionary<int, ArenaAddonValue> values, int index, out int value)
    {
        if (TryNumber(values, index, out var number) && number is >= 0 and <= int.MaxValue)
        {
            value = (int)number;
            return true;
        }

        if (TryText(values, index, out var text))
        {
            var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
            if (int.TryParse(digits, out value))
                return true;
        }

        value = 0;
        return false;
    }

    private static bool TryText(IReadOnlyDictionary<int, ArenaAddonValue> values, int index, out string value)
    {
        if (values.TryGetValue(index, out var raw) && raw.Text != null)
        {
            value = raw.Text;
            return true;
        }
        value = string.Empty;
        return false;
    }
}
