namespace ArenaPilot;

public enum ArenaPhase
{
    Unknown,
    Idle,
    Entry,
    StageList,
    PartySetup,
    Board,
    BattlePreparation,
    Battle,
    Loot,
    Rest,
    Treasure,
    ItemDispose,
    Shop,
    Result,
    Loading,
    Paused,
    Failed,
    Completed,
}

public enum ArenaNodeKind
{
    Unknown,
    Start,
    Battle,
    Treasure,
    Rest,
    Shop,
    Boss,
    Random,
}

public static class ArenaNodeKindLabels
{
    public static string Get(ArenaNodeKind kind) => kind switch
    {
        ArenaNodeKind.Start => "起点",
        ArenaNodeKind.Battle => "战斗",
        ArenaNodeKind.Treasure => "宝箱",
        ArenaNodeKind.Rest => "休息",
        ArenaNodeKind.Shop => "商店",
        ArenaNodeKind.Boss => "BOSS",
        ArenaNodeKind.Random => "随机",
        _ => "未知",
    };
}

public sealed record ArenaSnapshot(
    DateTime TimestampUtc,
    ArenaPhase Phase,
    int StageId,
    uint ContentId,
    bool IsLoggedIn,
    uint TerritoryId,
    string TerritoryName,
    float? PlayerX,
    float? PlayerY,
    float? PlayerZ,
    int? CurrentNode,
    ArenaNodeKind CurrentNodeKind,
    string CurrentAddon,
    IReadOnlyList<string> VisibleAddons,
    string FailureReason,
    string PhaseReason,
    IReadOnlyList<ArenaAddonSnapshot> Addons);

public sealed record ArenaAddonSnapshot(
    string Name,
    bool IsReady,
    int ValueCount,
    IReadOnlyList<ArenaAddonValue> Values);

public sealed record ArenaAddonValue(
    int Index,
    string Type,
    long? Number,
    string? Text);
