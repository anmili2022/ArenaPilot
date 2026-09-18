using System.Numerics;

namespace ArenaPilot;

public sealed record ArenaStageNode(int Index, Vector3 Center, IReadOnlyList<int> Next, ArenaNodeKind Kind);

public sealed class ArenaStageRoute
{
    public required int StageId { get; init; }
    public required uint TerritoryId { get; init; }
    public required uint ContentId { get; init; }
    public required string Name { get; init; }
    public float NodeRadius { get; init; } = 1.25f;
    public float MinX { get; init; } = -710f;
    public float MaxX { get; init; } = -690f;
    public float MinZ { get; init; } = -79f;
    public float MaxZ { get; init; } = 4f;
    public float MaxAbsY { get; init; } = 1f;
    public required IReadOnlyList<ArenaStageNode> Nodes { get; init; }
    public required IReadOnlyDictionary<int, int> PreferredNext { get; init; }

    public bool IsConfigured => Nodes.Count > 0 && PreferredNext.Count > 0;

    public IReadOnlyList<int> AllIndexes => Nodes.Select(x => x.Index).ToArray();

    public bool IsOnBoard(Vector3 position)
        => float.IsFinite(position.X)
            && float.IsFinite(position.Y)
            && float.IsFinite(position.Z)
            && position.X >= MinX && position.X <= MaxX
            && Math.Abs(position.Y) < MaxAbsY
            && position.Z >= MinZ && position.Z <= MaxZ;

    public int? FindNode(Vector3 position)
    {
        if (!IsOnBoard(position))
            return null;

        var point = new Vector2(position.X, position.Z);
        foreach (var node in Nodes)
        {
            var center = new Vector2(node.Center.X, node.Center.Z);
            if (Vector2.Distance(point, center) <= NodeRadius)
                return node.Index;
        }

        return null;
    }

    public Vector3 GetCenter(int index)
        => Nodes.First(x => x.Index == index).Center;

    public ArenaNodeKind GetKind(int? index)
    {
        if (index is not int node)
            return ArenaNodeKind.Unknown;
        foreach (var item in Nodes)
        {
            if (item.Index == node)
                return item.Kind;
        }
        return ArenaNodeKind.Unknown;
    }

    public int? GetPreferredNext(int index)
        => PreferredNext.TryGetValue(index, out var next) ? next : null;

    public bool IsBattle(int index)
        => GetKind(index) is ArenaNodeKind.Battle or ArenaNodeKind.Boss;
}
