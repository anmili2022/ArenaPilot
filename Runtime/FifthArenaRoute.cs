using System.Numerics;

namespace ArenaPilot;

public static class FifthArenaRoute
{
    // 高段第二盘当前固定路线使用已实测节点；其他分支待采集坐标后补充。
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 5,
        TerritoryId = 1343,
        ContentId = 1092,
        Name = "高段第二盘",
        NodeRadius = 1.25f,
        MaxX = -680f,
        MinZ = -82f,
        Nodes =
        [
            new(0, new Vector3(-700.00f, 0f, -0.00f), [2, 3, 4], ArenaNodeKind.Start),
            new(2, new Vector3(-709.00f, 0f, -9.00f), [5, 6], ArenaNodeKind.Unknown, false),
            new(3, new Vector3(-700.02f, 0f, -8.65f), [4, 6], ArenaNodeKind.Battle),
            new(4, new Vector3(-691.05f, 0f, -9.00f), [6, 7], ArenaNodeKind.Battle),
            new(5, new Vector3(-709.00f, 0f, -18.00f), [8], ArenaNodeKind.Unknown, false),
            new(6, new Vector3(-700.00f, 0f, -18.00f), [9], ArenaNodeKind.Unknown, false),
            new(7, new Vector3(-690.91f, 0f, -18.03f), [10], ArenaNodeKind.Battle),
            new(8, new Vector3(-709.00f, 0f, -27.00f), [11], ArenaNodeKind.Unknown, false),
            new(9, new Vector3(-700.00f, 0f, -27.00f), [11], ArenaNodeKind.Unknown, false),
            new(10, new Vector3(-690.92f, 0f, -26.62f), [11], ArenaNodeKind.Battle),
            new(11, new Vector3(-700.01f, 0f, -35.93f), [12, 14, 16], ArenaNodeKind.Battle),
            new(12, new Vector3(-718.00f, 0f, -45.00f), [13, 17], ArenaNodeKind.Unknown, false),
            new(13, new Vector3(-709.00f, 0f, -45.00f), [14], ArenaNodeKind.Unknown, false),
            new(14, new Vector3(-700.00f, 0f, -45.12f), [15], ArenaNodeKind.Rest),
            new(15, new Vector3(-691.10f, 0f, -44.99f), [16], ArenaNodeKind.Shop),
            new(16, new Vector3(-682.16f, 0f, -44.97f), [19], ArenaNodeKind.Treasure),
            new(17, new Vector3(-718.00f, 0f, -54.00f), [18], ArenaNodeKind.Unknown, false),
            new(18, new Vector3(-699.88f, 0f, -54.00f), [20, 21, 22], ArenaNodeKind.Battle),
            new(19, new Vector3(-681.97f, 0f, -53.81f), [18], ArenaNodeKind.Battle),
            new(20, new Vector3(-709.00f, 0f, -63.00f), [21, 23], ArenaNodeKind.Unknown, false),
            new(21, new Vector3(-700.04f, 0f, -63.06f), [24], ArenaNodeKind.Battle),
            new(22, new Vector3(-691.00f, 0f, -63.00f), [21, 25], ArenaNodeKind.Unknown, false),
            new(23, new Vector3(-709.00f, 0f, -72.00f), [26], ArenaNodeKind.Unknown, false),
            new(24, new Vector3(-699.94f, 0f, -71.78f), [26], ArenaNodeKind.Rest),
            new(25, new Vector3(-691.00f, 0f, -72.00f), [26], ArenaNodeKind.Unknown, false),
            new(26, new Vector3(-700.04f, 0f, -79.33f), [], ArenaNodeKind.Boss),
        ],
        PreferredNext = new Dictionary<int, int>
        {
            [0] = 3,
            [3] = 4,
            [4] = 7,
            [7] = 10,
            [10] = 11,
            [11] = 14,
            [14] = 15,
            [15] = 16,
            [16] = 19,
            [19] = 18,
            [18] = 21,
            [21] = 24,
            [24] = 26,
        },
    };
}
