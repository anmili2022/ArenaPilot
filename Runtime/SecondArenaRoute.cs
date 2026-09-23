using System.Numerics;

namespace ArenaPilot;

public static class SecondArenaRoute
{
    // 默认路线：0 → 1 → 2 → 3 → 5 → 7 → 8 → 9 → 11 → 12 → 13。
    public static ArenaStageRoute Route { get; } = new()
    {
        StageId = 2,
        TerritoryId = 1340,
        ContentId = 1089,
        Name = "第二盘",
        MinZ = -86f,
        Nodes =
        [
            new(0, new Vector3(-700f, 0f, 0f), [1], ArenaNodeKind.Start),
            new(1, new Vector3(-700.005f, 0f, -9.031f), [2], ArenaNodeKind.Battle),
            new(2, new Vector3(-700.020f, 0f, -17.779f), [3, 4], ArenaNodeKind.Shop),
            new(3, new Vector3(-704.988f, 0f, -25.317f), [5], ArenaNodeKind.Battle),
            new(4, new Vector3(-694.910f, 0f, -25.403f), [6], ArenaNodeKind.Battle),
            new(5, new Vector3(-704.886f, 0f, -34.211f), [7], ArenaNodeKind.Treasure),
            new(6, new Vector3(-694.862f, 0f, -34.268f), [7], ArenaNodeKind.Rest),
            new(7, new Vector3(-699.921f, 0f, -41.763f), [8], ArenaNodeKind.Battle),
            new(8, new Vector3(-699.928f, 0f, -50.748f), [9], ArenaNodeKind.Treasure),
            new(9, new Vector3(-700.021f, 0f, -59.706f), [10, 11], ArenaNodeKind.Battle),
            new(10, new Vector3(-704.830f, 0f, -67.459f), [12], ArenaNodeKind.Battle),
            new(11, new Vector3(-694.970f, 0f, -67.240f), [12], ArenaNodeKind.Treasure),
            new(12, new Vector3(-699.892f, 0f, -74.832f), [13], ArenaNodeKind.Shop),
            new(13, new Vector3(-700.082f, 0f, -83.978f), [], ArenaNodeKind.Boss),
        ],
        PreferredNext = new Dictionary<int, int>
        {
            [0] = 1,
            [1] = 2,
            [2] = 3,
            [3] = 5,
            [5] = 7,
            [7] = 8,
            [8] = 9,
            [9] = 11,
            [11] = 12,
            [12] = 13,
        },
    };
}
