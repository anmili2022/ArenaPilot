using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace ArenaPilot;

public static class BossAction
{
    private static readonly uint[] BossPriority =
    [
        19334, // 奇子·骑士
        14531, // 奇子·骑士
        19335, // 奇子·主教
        14532, // 奇子·主教
        14567, // 奇子·主教
        19598, // 奇子·游侠骑士
        19600, // 奇子·主教
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
        19675, // 奇子·佛劳洛斯
        19696, // 奇子·火蛟
        19698, // 奇子·棘鼹
        19701, // 奇子·蓝闪蝶
        19700, // 奇子·狱蟾蜍
        19702, // 奇子·杜尔迦
        19710, // 奇子·斯芬克斯
        19715, // 奇子·巨人
        19717, // 凝胶化雷电
        19720, // 凝胶化火焰
        19742, // 魔斧之主 劳妲
        19684, // 奇子·爆弹怪
        19685, // 光元精
        19682, // 奇子·夜魔人
        19605, // 奇子·鱼人
        19604, // 奇子·尤弥尔之壳
        19603, // 奇子·尤弥尔
        19606, // 奇子·祖（忽略公雏鸟、母雏鸟和蛋）
        19614, // 奇子·拉哈穆（忽略巨像）
        19617, // 奇子·塞壬（忽略蹒跚鬼和爬行鬼）
        19626, // 贪食无厌 加特勒
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

    public static void ClearKnownBossTarget()
    {
        var target = DalamudApi.TargetManager.Target;
        if (target != null && BossPriority.Contains(target.BaseId))
            DalamudApi.TargetManager.Target = null;
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
