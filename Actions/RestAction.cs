namespace ArenaPilot;

public static class RestAction
{
    public static bool IsRestUi(ArenaSnapshot snapshot)
        => snapshot.Phase == ArenaPhase.Rest
            && snapshot.Addons.Any(x => x.Name == "XBMPetParty" && x.IsReady);

    public static bool TryLeave(out string error)
        => AddonUi.TryRegisteredEvent("XBMPetParty", 4, out error);

    public static bool TryRest(out string error)
    {
        foreach (var label in new[] { "休息", "休憩", "休む", "Rest", "Take a Rest" })
        {
            if (AddonUi.TryClickButtonByText("XBMPetParty", label, out error))
                return true;
        }
        error = "没有找到可用的休息按钮";
        return false;
    }
}
