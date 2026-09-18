using System.Numerics;

namespace ArenaPilot;

public static class FirstArenaRoute
{
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 1,
        TerritoryId = 1339,
        ContentId = 1088,
        Name = "第一盘",
        NodeRadius = 1.25f,
        Nodes =
        [
            new(0, new Vector3(-700f, 0f, 0f), [1], ArenaNodeKind.Start),
            new(1, new Vector3(-700f, 0f, -9f), [2, 3], ArenaNodeKind.Battle),
            new(2, new Vector3(-705f, 0f, -16f), [4], ArenaNodeKind.Battle),
            new(3, new Vector3(-695f, 0f, -16f), [5], ArenaNodeKind.Battle),
            new(4, new Vector3(-705f, 0f, -25f), [6], ArenaNodeKind.Rest),
            new(5, new Vector3(-695f, 0f, -25f), [6], ArenaNodeKind.Treasure),
            new(6, new Vector3(-700f, 0f, -33f), [7], ArenaNodeKind.Battle),
            new(7, new Vector3(-700f, 0f, -42f), [8], ArenaNodeKind.Treasure),
            new(8, new Vector3(-700f, 0f, -51f), [9, 10], ArenaNodeKind.Battle),
            new(9, new Vector3(-705f, 0f, -58f), [11], ArenaNodeKind.Treasure),
            new(10, new Vector3(-695f, 0f, -58f), [11], ArenaNodeKind.Rest),
            new(11, new Vector3(-700f, 0f, -66f), [12], ArenaNodeKind.Shop),
            new(12, new Vector3(-700f, 0f, -75f), [], ArenaNodeKind.Boss),
        ],
        PreferredNext = new Dictionary<int, int>
        {
            [0] = 1,
            [1] = 3,
            [2] = 4,
            [3] = 5,
            [4] = 6,
            [5] = 6,
            [6] = 7,
            [7] = 8,
            // 固定路线选择左侧分支：节点 8 -> 节点 9。
            [8] = 9,
            [9] = 11,
            [10] = 11,
            [11] = 12,
        },
    };
}
