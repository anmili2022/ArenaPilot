using System.Numerics;

namespace ArenaPilot;

public static class ThirdArenaRoute
{
    // 第三盘默认避开困难节点 5，按棋盘红线经过节点 4、6、10 和休息节点 14。
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 3,
        TerritoryId = 1341,
        ContentId = 1090,
        Name = "第三盘",
        NodeRadius = 1.25f,
        MinZ = -102f,
        Nodes =
        [
            new(0, new Vector3(-700.01f, 0f, 0.06f), [1], ArenaNodeKind.Start),
            new(1, new Vector3(-699.94f, 0f, -8.97f), [2], ArenaNodeKind.Battle),
            new(2, new Vector3(-699.90f, 0f, -17.99f), [3], ArenaNodeKind.Battle),
            new(3, new Vector3(-699.95f, 0f, -26.86f), [4, 5], ArenaNodeKind.Shop),
            new(4, new Vector3(-704.75f, 0f, -34.22f), [6], ArenaNodeKind.Battle),
            new(5, new Vector3(-694.92f, 0f, -34.35f), [7], ArenaNodeKind.Battle),
            new(6, new Vector3(-704.85f, 0f, -43.25f), [8], ArenaNodeKind.Treasure),
            new(7, new Vector3(-694.99f, 0f, -43.40f), [8], ArenaNodeKind.Rest),
            new(8, new Vector3(-700.05f, 0f, -50.76f), [9, 10], ArenaNodeKind.Battle),
            new(9, new Vector3(-704.78f, 0f, -58.47f), [11], ArenaNodeKind.Treasure),
            new(10, new Vector3(-695.17f, 0f, -58.20f), [11], ArenaNodeKind.Shop),
            new(11, new Vector3(-699.73f, 0f, -65.87f), [12], ArenaNodeKind.Battle),
            new(12, new Vector3(-699.94f, 0f, -74.84f), [13, 14], ArenaNodeKind.Treasure),
            new(13, new Vector3(-704.54f, 0f, -82.33f), [15], ArenaNodeKind.Random),
            new(14, new Vector3(-695.03f, 0f, -82.22f), [15], ArenaNodeKind.Rest),
            new(15, new Vector3(-699.70f, 0f, -89.97f), [16], ArenaNodeKind.Shop),
            new(16, new Vector3(-699.95f, 0f, -98.93f), [], ArenaNodeKind.Boss),
        ],
        PreferredNext = new Dictionary<int, int>
        {
            [0] = 1,
            [1] = 2,
            [2] = 3,
            [3] = 4,
            [4] = 6,
            [5] = 7,
            [6] = 8,
            [7] = 8,
            [8] = 10,
            [9] = 11,
            [10] = 11,
            [11] = 12,
            [12] = 14,
            [13] = 15,
            [14] = 15,
            [15] = 16,
        },
    };
}
