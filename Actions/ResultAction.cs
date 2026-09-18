namespace ArenaPilot;

public static class ResultAction
{
    public static bool TryNextPage(out string error)
        => AddonUi.TryButtonRegisteredEvent("XBMResult", 61, 0, out error);

    public static bool TryClose(out string error)
        => AddonUi.TryButtonRegisteredEvent("XBMResult", 61, 0, out error);
}
