using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace ArenaPilot;

public static class BossAction
{
    private static readonly uint[] BossPriority =
    [
        19334, // 奇子·骑士
        19335, // 奇子·主教
        19338, // 奇子·夺灵魔
        19339, // 奇子·祸蛛蝎
        19341, // 奇子·食人魔
        19342, // 奇子·妖火
        19344, // 魅惑女妖 帕德索
        19924, // 奇子·大妖火
        19638, // 奇子·博学林鸮
        19641, // 高段第一盘节点 2 BOSS
        19645, // 奇子·尸生花
        19650, // 奇子·石像鬼
        19663, // 奇子·花苗
        19662, // 奇子·蛞蝓
        19661, // 奇子·树精
        19672, // 怨毒龙 博尔格尼
    ];

    public static unsafe bool TryTargetBoss(out string error)
    {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
        {
            error = "玩家尚未准备好";
            return false;
        }

        var boss = FindBoss();
        if (boss == null)
        {
            error = "未找到BOSS";
            return false;
        }

        var targetSystem = TargetSystem.Instance();
        if (targetSystem == null)
        {
            error = "目标系统尚未准备好";
            return false;
        }

        targetSystem->SetHardTarget((FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)boss.Address, false, false, 0);
        error = string.Empty;
        return true;
    }

    public static float? GetDistanceToBoss(Dalamud.Game.ClientState.Objects.Types.ICharacter player)
    {
        var boss = FindBoss();
        if (boss == null)
            return null;

        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        var bossPos = new Vector3(boss.Position.X, boss.Position.Y, boss.Position.Z);
        return Math.Max(0f, Vector3.Distance(playerPos, bossPos) - boss.HitboxRadius);
    }

    public static Vector3 GetClosePosition(Dalamud.Game.ClientState.Objects.Types.ICharacter player)
    {
        var boss = FindBoss();
        if (boss == null)
            return player.Position;

        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        var bossPos = new Vector3(boss.Position.X, boss.Position.Y, boss.Position.Z);
        var offset = bossPos - playerPos;
        if (offset.LengthSquared() < 0.0001f)
            return player.Position;

        var dir = Vector3.Normalize(offset);
        return bossPos - dir * (boss.HitboxRadius + 18f);
    }

    private static IGameObject? FindBoss()
    {
        var candidates = DalamudApi.ObjectTable
            .OfType<ICharacter>()
            .Where(x => x.IsTargetable && !x.IsDead)
            .ToArray();

        foreach (var dataId in BossPriority)
        {
            var boss = candidates.FirstOrDefault(x => x.BaseId == dataId);
            if (boss != null)
                return boss;
        }

        return null;
    }

    public static string? GetBossInfo()
    {
        var boss = FindBoss();
        if (boss is not ICharacter npc)
            return null;
        return $"BaseId={npc.BaseId}, NameId={npc.NameId}, Name={npc.Name.TextValue}";
    }
}
