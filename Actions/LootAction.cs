namespace ArenaPilot;

public sealed record LootCard(uint Param, string Name, bool Obtained);

public static class LootAction
{
    private const int CardType = 1008;
    private const string ObtainedText = "已获得此道具";

    public static bool TryTakeAll(out string error)
        => AddonUi.TryRegisteredEvent("XBMContentsBooty", 1, out error);

    public static bool TryExit(out string error)
        => AddonUi.TryRegisteredEvent("XBMContentsBooty", 0, out error);

    public static bool TryDismissPopup(out string error)
        => AddonUi.TryFireCallback("SelectOk", 0, out error);

    public static IReadOnlyList<LootCard> ReadCards()
        => AddonUi.ReadCards("XBMContentsBooty", CardType, ObtainedText)
            .Select(x => new LootCard(x.Param, x.Name, x.Obtained))
            .ToArray();

    public static bool TryReadCardsAvailable(out IReadOnlyList<LootCard> available, out string error)
    {
        available = [];
        var all = ReadCards();
        if (all.Count == 0)
        {
            error = "战利品列表尚未准备好或没有卡片";
            return false;
        }
        available = all.Where(x => !x.Obtained).ToArray();
        error = string.Empty;
        return true;
    }

    public static bool TryTakeCard(LootCard card, out string error)
        => AddonUi.TrySendCardEvent("XBMContentsBooty", card.Param, out error);
}
