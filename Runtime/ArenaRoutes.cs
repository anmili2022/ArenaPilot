using System.Numerics;

namespace ArenaPilot;

public static class ArenaRoutes
{
    public static IReadOnlyList<(int Id, string Name)> StageOptions { get; } =
    [
        (1, "第一盘"),
        (2, "第二盘"),
        (3, "第三盘"),
        (4, "高段第一盘"),
        (5, "高段第二盘"),
    ];

    public static IReadOnlyList<ArenaStageRoute> All { get; } =
    [
        FirstArenaRoute.Route,
        SecondArenaRoute.Route,
        ThirdArenaRoute.Route,
        FourthArenaRoute.Route,
        FifthArenaRoute.Route,
    ];

    public static IReadOnlyList<ArenaStageRoute> DisplayRoutes { get; } =
    [
        FirstArenaRoute.Route,
        SecondArenaRoute.Route,
        ThirdArenaRoute.Route,
        FourthArenaRoute.Route,
        FifthArenaRoute.Route,
    ];

    public static string GetStageName(int stageId)
        => StageOptions.FirstOrDefault(x => x.Id == stageId).Name ?? $"第 {stageId} 层";

    public static ArenaStageRoute? Find(uint territoryId, uint contentId)
    {
        if (territoryId == 0)
            return null;

        var exact = All.FirstOrDefault(r =>
            r.TerritoryId == territoryId && r.ContentId == contentId);
        if (exact != null)
            return exact;

        // During the loading phase ContentId may briefly read as 0.
        if (contentId == 0)
            return All.FirstOrDefault(r => r.TerritoryId == territoryId);

        return null;
    }

    public static ArenaStageRoute? GetByStage(int stageId)
        => DisplayRoutes.FirstOrDefault(r => r.StageId == stageId);

    public static void ApplyCoordinateOverrides(
        IReadOnlyDictionary<int, Dictionary<int, SerializableVector3>> overrides)
    {
        var result = new Dictionary<int, IReadOnlyDictionary<int, Vector3>>();
        foreach (var (stageId, nodes) in overrides)
        {
            var map = new Dictionary<int, Vector3>();
            foreach (var (nodeIndex, value) in nodes)
                map[nodeIndex] = new Vector3(value.X, value.Y, value.Z);
            result[stageId] = map;
        }
        ArenaStageRoute.CoordinateOverrides = result;
    }
}
