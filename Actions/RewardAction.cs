namespace ArenaPilot;

public static class RewardAction
{
    public static bool TryTakeAll(out string error)
        => AddonUi.TryRegisteredEvent("XBMContentsBooty", 1, out error);

    public static bool TryTakeFirst(out string error)
        => AddonUi.TryRegisteredEvent("XBMContentsBooty", 2, out error);

    public static bool TryExit(out string error)
        => AddonUi.TryButtonRegisteredEvent("XBMContentsBooty", 47, 0, out error);
}
