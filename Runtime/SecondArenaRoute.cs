using System.Numerics;

namespace ArenaPilot;

public static class SecondArenaRoute
{
    // 仅展示棋盘图拓扑；所有节点等待实测坐标后再启用导航。
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 2,
        TerritoryId = 1340,
        ContentId = 1089,
        Name = "第二盘",
        Nodes =
        [
            new(0, new Vector3(-700f, 0f, 0f), [1], ArenaNodeKind.Start, false),
            new(1, new Vector3(-700f, 0f, -9f), [2], ArenaNodeKind.Battle, false),
            new(2, new Vector3(-700f, 0f, -18f), [3, 4], ArenaNodeKind.Shop, false),
            new(3, new Vector3(-709f, 0f, -27f), [5], ArenaNodeKind.Battle, false),
            new(4, new Vector3(-691f, 0f, -27f), [6], ArenaNodeKind.Battle, false),
            new(5, new Vector3(-709f, 0f, -36f), [7], ArenaNodeKind.Treasure, false),
            new(6, new Vector3(-691f, 0f, -36f), [7], ArenaNodeKind.Rest, false),
            new(7, new Vector3(-700f, 0f, -45f), [8], ArenaNodeKind.Battle, false),
            new(8, new Vector3(-700f, 0f, -54f), [9], ArenaNodeKind.Treasure, false),
            new(9, new Vector3(-700f, 0f, -63f), [10, 11], ArenaNodeKind.Battle, false),
            new(10, new Vector3(-709f, 0f, -72f), [12], ArenaNodeKind.Random, false),
            new(11, new Vector3(-691f, 0f, -72f), [12], ArenaNodeKind.Treasure, false),
            new(12, new Vector3(-700f, 0f, -81f), [13], ArenaNodeKind.Shop, false),
            new(13, new Vector3(-700f, 0f, -90f), [], ArenaNodeKind.Boss, false),
        ],
        PreferredNext = new Dictionary<int, int>(),
    };
}
