using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace ArenaPilot;

public static class BossAction
{
    public static IReadOnlyList<ArenaBossTarget> DefaultPriority { get; } =
    [
        new(19334, "奇子·骑士"), new(14531, "奇子·骑士"),
        new(19335, "奇子·主教"), new(14532, "奇子·主教"), new(14567, "奇子·主教"),
        new(19598, "奇子·游侠骑士"), new(19600, "奇子·主教"), new(19338, "奇子·夺灵魔"),
        new(19339, "奇子·祸蛛蝎"), new(19341, "奇子·食人魔"), new(19342, "奇子·妖火"),
        new(19344, "魅惑女妖 帕德索"), new(19924, "奇子·大妖火"), new(19638, "奇子·博学林鸮"),
        new(19641, "高段第一盘节点2战斗目标"), new(19645, "奇子·尸生花"), new(19650, "奇子·石像鬼"),
        new(19663, "奇子·花苗"), new(19662, "奇子·蛞蝓"), new(19661, "奇子·树精"),
        new(19672, "怨毒龙 博尔格尼"), new(19678, "节点2 奇子·毒性蘑菇"), new(19677, "节点2 奇子·夺心魔"),
        new(19675, "节点3 奇子·佛劳洛斯"), new(19676, "节点3 雷元精"), new(19683, "节点7 奇子·深瞳"),
        new(19682, "节点7 奇子·夜魔人"), new(19684, "节点7 奇子·爆弹怪"), new(19685, "节点7 光元精"),
        new(19687, "节点8 奇子·红格雷姆林"), new(19688, "节点8 奇子·格雷姆林"), new(19692, "节点8 奇子·石像魔"),
        new(19689, "节点8 奇子·软糊怪"), new(19690, "节点8 奇子·奶冻怪"), new(19691, "节点8 奇子·甜羹怪"),
        new(19686, "节点8 奇子·阿托莫斯"), new(19693, "节点8 奇子·威胁扎哈克"), new(19696, "节点9 奇子·火蛟"),
        new(19698, "节点9 奇子·棘鼹"), new(19701, "节点9 奇子·蓝闪蝶"), new(19700, "节点9 奇子·狱蟾蜍"),
        new(19704, "节点11 奇子·转盘堡"), new(19702, "节点11 奇子·杜尔迦"), new(19706, "节点13 奇子·拉米亚"),
        new(19707, "节点13 奇子·独眼巨人"), new(19705, "节点13 奇子·美杜莎"), new(19708, "节点17 奇子·奇美拉"),
        new(19719, "节点18 凝胶化火焰（大）"), new(19717, "节点18 凝胶化雷电（大）"),
        new(19720, "节点18 凝胶化火焰（小）"), new(19718, "节点18 凝胶化雷电（小）"),
        new(19716, "节点18 奇子·独眼巨人"), new(19715, "节点18 奇子·巨人"), new(19710, "节点19 奇子·斯芬克斯"),
        new(19730, "节点20 奇子·莫古小贼"), new(19724, "节点20 奇子·莫古大医"), new(19723, "节点20 奇子·莫古大术"),
        new(19727, "节点20 奇子·莫古大剑"), new(19721, "节点20 奇子·莫古小剑"), new(19722, "节点20 奇子·莫古小猛"),
        new(19725, "节点20 莫古小术"), new(19726, "节点20 奇子·莫古小医"), new(19728, "节点20 奇子·莫古小歌"),
        new(19733, "节点23 雷元精"), new(19732, "节点23 奇子·铁巨人"), new(19731, "节点23 奇子·贝希摩斯"),
        new(19737, "节点25 死亡沙漏"), new(19741, "节点25 奇子·肮脏之眼"), new(19740, "节点25 奇子·哈帕利特"),
        new(19736, "节点25 奇子·冥鬼之眼王"), new(19742, "节点26 魔斧之主 劳妲"),
        new(19605, "奇子·鱼人"), new(19604, "奇子·尤弥尔之壳"), new(19603, "奇子·尤弥尔"),
        new(19606, "奇子·祖"), new(19614, "奇子·拉哈穆"), new(19617, "奇子·塞壬"),
        new(19748, "奇子·塔纳托斯"),
        new(19746, "魔刃"),
        new(19626, "贪食无厌 加特勒"),
        new(19539, "节点1 奇子·曼提克"),
        new(19540, "节点3 奇子·牛头魔"),
        new(19543, "节点3 奇子·冥鬼之眼"),
        new(19544, "节点4 奇子·双足飞龙"),
        new(19548, "节点7 奇子·虚灵法师"),
        new(19549, "节点7 奇子·僵尸"),
        new(19551, "节点9 奇子·牛魔老哥"),
        new(19552, "节点9 奇子·牛魔老弟"),
        new(19553, "节点10 奇子·恶魔"),
        new(19554, "节点10 奇子·恶魔兵装"),
        new(19555, "节点10 奇子·小恶魔"),
        new(19557, "节点13 寻兽探奇 路斯福洛克斯"),
        new(19558, "节点13 小地豆"),
    ];

    public static unsafe bool TryTargetBoss(IReadOnlyList<ArenaBossTarget> priority, out string error)
    {
        var player = DalamudApi.ObjectTable.LocalPlayer;
        if (player == null)
        {
            error = "玩家尚未准备好";
            return false;
        }

        var boss = FindBoss(priority);
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

    public static void ClearKnownBossTarget(IReadOnlyList<ArenaBossTarget> priority)
    {
        var target = DalamudApi.TargetManager.Target;
        if (target != null && priority.Any(x => x.Id == target.BaseId))
            DalamudApi.TargetManager.Target = null;
    }

    public static float? GetDistanceToBoss(IReadOnlyList<ArenaBossTarget> priority, Dalamud.Game.ClientState.Objects.Types.ICharacter player)
    {
        var boss = FindBoss(priority);
        if (boss == null)
            return null;

        var playerPos = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        var bossPos = new Vector3(boss.Position.X, boss.Position.Y, boss.Position.Z);
        return Math.Max(0f, Vector3.Distance(playerPos, bossPos) - boss.HitboxRadius);
    }

    public static Vector3 GetClosePosition(IReadOnlyList<ArenaBossTarget> priority, Dalamud.Game.ClientState.Objects.Types.ICharacter player)
    {
        var boss = FindBoss(priority);
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

    private static IGameObject? FindBoss(IReadOnlyList<ArenaBossTarget> priority)
    {
        var candidates = DalamudApi.ObjectTable
            .OfType<ICharacter>()
            .Where(x => x.IsTargetable && !x.IsDead)
            .ToArray();

        foreach (var target in priority)
        {
            var boss = candidates.FirstOrDefault(x => x.BaseId == target.Id);
            if (boss != null)
                return boss;
        }

        return null;
    }

    public static string? GetBossInfo(IReadOnlyList<ArenaBossTarget> priority)
    {
        var boss = FindBoss(priority);
        if (boss is not ICharacter npc)
            return null;
        return $"BaseId={npc.BaseId}, NameId={npc.NameId}, Name={npc.Name.TextValue}";
    }
}
