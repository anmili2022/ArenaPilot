using FFXIVClientStructs.FFXIV.Component.GUI;

namespace ArenaPilot;

public sealed record ArenaPartyMember(int Row, uint PetId, string Name, uint Hp, uint MaxHp, int Slot);

public static class PartySetupAction
{
    public static bool TryRead(ArenaSnapshot snapshot, out ArenaPartyMember[] members, out string error)
    {
        members = [];
        var party = snapshot.Addons.FirstOrDefault(x => x.Name == "XBMPetParty" && x.IsReady);
        if (party == null)
        {
            error = "兽笛编队界面尚未准备好";
            return false;
        }

        var values = party.Values.ToDictionary(x => x.Index);
        if (!TryNumber(values, 5, out var count) || count is < 1 or > 15)
        {
            error = "魔兽数量字段不匹配";
            return false;
        }

        var result = new List<ArenaPartyMember>((int)count);
        for (var row = 0; row < count; row++)
        {
            var offset = 6 + row * 77;
            if (!values.TryGetValue(offset + 3, out var nameValue)
                || string.IsNullOrWhiteSpace(nameValue.Text)
                || !TryNumber(values, offset + 5, out var hp)
                || !TryNumber(values, offset + 6, out var maxHp)
                || !TryNumber(values, offset + 74, out var slot)
                || !TryNumber(values, offset + 76, out var petId)
                || hp < 0 || maxHp <= 0 || hp > maxHp
                || slot is < 0 or > 3 || petId is < 1 or > 50)
            {
                error = $"第 {row + 1} 行魔兽数据尚未准备好";
                return false;
            }

            result.Add(new ArenaPartyMember(row, (uint)petId, nameValue.Text, (uint)hp, (uint)maxHp, (int)slot));
        }

        members = result.ToArray();
        error = string.Empty;
        return true;
    }

    public static bool IsComplete(IReadOnlyList<ArenaPartyMember> members, Configuration config)
    {
        var petIds = config.FlutePetIds;
        return petIds.Select((petId, slot) => members.Any(x =>
            x.PetId == petId
            && x.Slot == slot
            && x.Hp > 0)).All(x => x)
            && members.Count(x => x.Slot == 0) == 1
            && members.Count(x => x.Slot == 1) == 1
            && members.Count(x => x.Slot == 2) == 1;
    }

    public static ArenaPartyMember? NextToAssign(IReadOnlyList<ArenaPartyMember> members, Configuration config)
    {
        var desired = config.FlutePetIds
            .Select(petId => members.FirstOrDefault(x => x.PetId == petId && x.Hp > 0))
            .ToArray();
        if (desired.Any(x => x == null))
            return null;

        var prefix = 0;
        while (prefix < desired.Length && desired[prefix]!.Slot == prefix)
            prefix++;
        if (prefix == desired.Length)
            return null;

        return members
            .Where(x => x.Slot is >= 0 and < 3 && x.Slot >= prefix)
            .OrderByDescending(x => x.Slot)
            .FirstOrDefault() ?? desired[prefix];
    }

    public static unsafe bool TryClickRow(int row, out string error)
    {
        var addon = AddonUi.GetReady("XBMPetParty");
        if (addon == null)
        {
            error = "兽笛编队界面已关闭或尚未准备好";
            return false;
        }

        var list = addon->GetComponentListById(11);
        if (list == null
            || list->OwnerNode == null
            || !list->OwnerNode->IsVisible()
            || !list->IsItemInteractionEnabled
            || list->IsUpdatePending
            || row < 0
            || row >= list->ListLength
            || list->GetItemDisabledState(row))
        {
            error = "魔兽列表当前不可操作";
            return false;
        }

        list->SelectItem(row, false);
        list->DispatchItemEvent(row, AtkEventType.ListItemClick);
        error = string.Empty;
        return true;
    }

    public static bool IsUnderfilledChallengePrompt(ArenaSnapshot snapshot)
    {
        var prompt = snapshot.Addons.FirstOrDefault(x => x.Name == "SelectYesno" && x.IsReady);
        return prompt?.Values.Any(x => x.Text?.Contains("魔兽未满", StringComparison.Ordinal) == true) == true;
    }

    public static bool IsUnassignedFlutePrompt(ArenaSnapshot snapshot)
    {
        var prompt = snapshot.Addons.FirstOrDefault(x => x.Name == "SelectYesno" && x.IsReady);
        return prompt?.Values.Any(x => x.Text?.Contains("兽笛未设置魔兽", StringComparison.Ordinal) == true) == true;
    }

    public static bool TryStartBattle(out string error)
        => BattleAction.TryStartBattle(out error);

    public static bool TryTakeReward(out string error)
        => RewardAction.TryTakeAll(out error);

    public static bool TryCommenceEntry(out string error)
        => EntryAction.TryCommenceEntry(out error);

    public static bool TryConfirmChallenge(out string error)
        => EntryAction.TryConfirmChallenge(out error);

    public static bool TryRejectChallenge(out string error)
        => EntryAction.TryRejectChallenge(out error);

    public static bool TryCloseResult(out string error)
        => ResultAction.TryClose(out error);

    public static bool TryAdvanceEntry(out string error)
        => EntryAction.TryAdvanceEntry(out error);

    private static bool TryNumber(IReadOnlyDictionary<int, ArenaAddonValue> values, int index, out long number)
    {
        if (values.TryGetValue(index, out var value) && value.Number.HasValue)
        {
            number = value.Number.Value;
            return true;
        }

        number = 0;
        return false;
    }
}
