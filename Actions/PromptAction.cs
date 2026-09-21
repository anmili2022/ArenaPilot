namespace ArenaPilot;

public static class PromptAction
{
    public static bool TryHandle(ArenaSnapshot snapshot, bool challengeSent, out string status)
    {
        var hasPrompt = snapshot.Addons.Any(x => x.Name == "SelectYesno" && x.IsReady);
        var contextualConfirmation = hasPrompt && snapshot.Phase switch
        {
            ArenaPhase.Loot => snapshot.Addons.Any(x => x.Name == "XBMContentsBooty" && x.IsReady),
            ArenaPhase.Shop => snapshot.Addons.Any(x => x.Name == "XBMContentsItemShop" && x.IsReady),
            ArenaPhase.Treasure => snapshot.Addons.Any(x => x.Name == "XBMContentsTreasure" && x.IsReady),
            ArenaPhase.Rest => snapshot.Addons.Any(x => x.Name == "XBMPetParty" && x.IsReady),
            ArenaPhase.PartySetup => challengeSent
                && snapshot.Addons.Any(x => x.Name == "XBMPetParty" && x.IsReady),
            ArenaPhase.Entry => challengeSent
                && snapshot.Addons.Any(x => x.Name == "XBMPetParty" && x.IsReady)
                && snapshot.Addons.Any(x => x.Name == "XBMStageDetailList" && x.IsReady),
            _ => false,
        };
        if (contextualConfirmation)
        {
            if (!AddonUi.TryFireCallback("SelectYesno", 0, out var contextualConfirmError))
            {
                status = contextualConfirmError;
                return false;
            }
            status = $"已按 {snapshot.Phase} 阶段确认提示";
            return true;
        }

        status = string.Empty;
        return false;
    }
}
