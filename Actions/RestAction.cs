namespace ArenaPilot;

public static class RestAction
{
    public static bool IsRestUi(ArenaSnapshot snapshot)
        => snapshot.Addons.FirstOrDefault(x => x.Name == "XBMPetParty" && x.IsReady)
            ?.Values.Any(x => x.Index == 1166 && x.Text == "休息") == true;

    public static bool TryLeave(out string error)
        => AddonUi.TryRegisteredEvent("XBMPetParty", 4, out error);

    public static bool TryRest(out string error)
        => AddonUi.TryClickButtonByText("XBMPetParty", "休息", out error);
}
