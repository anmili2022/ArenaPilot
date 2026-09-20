using System.Numerics;

namespace ArenaPilot;

public static class FourthArenaRoute
{
    // 高段第一盘固定路线：0 → 1 → 3 → 4 → 5 → 7 → 9 → 11 → 13 → 14。
    // 节点 2 已收录但不在当前自动路线；节点 6、8、10、12 仅展示，等待实测坐标。
    // 节点 3、6、7、8、12 为随机节点。
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 4,
        TerritoryId = 1342,
        ContentId = 1091,
        Name = "高段第一盘",
        NodeRadius = 1.25f,
        Nodes =
        [
            new(0, new Vector3(-700.00f, 0f, 0.00f), [1], ArenaNodeKind.Start),
            new(1, new Vector3(-700.07f, 0f, -8.70f), [2, 3], ArenaNodeKind.Battle),
            new(2, new Vector3(-704.93f, 0f, -16.60f), [4], ArenaNodeKind.Battle),
            new(3, new Vector3(-695.18f, 0f, -16.52f), [4], ArenaNodeKind.Random),
            new(4, new Vector3(-699.92f, 0f, -23.86f), [5, 6], ArenaNodeKind.Shop),
            new(5, new Vector3(-704.86f, 0f, -31.34f), [7], ArenaNodeKind.Rest),
            new(6, new Vector3(-695.00f, 0f, -31.34f), [7], ArenaNodeKind.Random, false),
            new(7, new Vector3(-700.08f, 0f, -38.98f), [8, 9], ArenaNodeKind.Random),
            new(8, new Vector3(-705.00f, 0f, -46.56f), [10], ArenaNodeKind.Random, false),
            new(9, new Vector3(-694.96f, 0f, -46.56f), [11], ArenaNodeKind.Treasure),
            new(10, new Vector3(-705.00f, 0f, -55.49f), [12], ArenaNodeKind.Battle, false),
            new(11, new Vector3(-694.97f, 0f, -55.49f), [13], ArenaNodeKind.Battle),
            new(12, new Vector3(-705.00f, 0f, -64.41f), [14], ArenaNodeKind.Random, false),
            new(13, new Vector3(-695.07f, 0f, -64.41f), [14], ArenaNodeKind.Shop),
            new(14, new Vector3(-699.12f, 0f, -70.80f), [], ArenaNodeKind.Boss),
        ],
        PreferredNext = new Dictionary<int, int>
        {
            [0] = 1,
            [1] = 3,
            [2] = 4,
            [3] = 4,
            [4] = 5,
            [5] = 7,
            [7] = 9,
            [9] = 11,
            [11] = 13,
            [13] = 14,
        },
    };
}
