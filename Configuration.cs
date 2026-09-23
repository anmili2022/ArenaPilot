using Dalamud.Configuration;
using Dalamud.Plugin;
using Newtonsoft.Json;

namespace ArenaPilot;

public sealed record ArenaBossTarget(uint Id, string Name);

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    private static readonly uint[] DefaultEquipmentPurchasePriority =
    [
        3,  // 治愈戒指
        39, // 缠冰斧
        40, // 缠雷斧
        41, // 缠土斧
        42, // 缠水斧
        43, // 缠风斧
        38, // 缠火斧
        27, // 英雄王冠：最大体力+30%，造成伤害+18%
        24, // 大师盾：最大体力+35%，受到伤害-25%
        49, // 烈风铠甲：受到伤害-15%，造成魔法伤害+20%
        51, // 秘银铠甲：受到伤害-15%，最大体力+15%
        35, // 星极上衣：高速复唱+25%，受到伤害-7%，最大体力+15%
        58, // 盗贼上衣：战利品掉落率+80%，受到伤害-8%，最大体力+18%
        44, // 忍者服：最大体力+17%，回避率+8%
        25, // 神秘面纱：造成魔法伤害+18%，受到魔法伤害-15%
        7,  // 野生头冠：造成物理伤害+10%，暴击发动率+3%
        8,  // 理性之杖：造成魔法伤害+20%
        9,  // 力量护臂：造成物理伤害+12%，回避率+3%
        54, // 猛兽面具：受到物理伤害-10%，暴击发动率+15%
        11, // 绿色贝雷帽：造成伤害+3%，最大体力+7%
        23, // 天使白衣：受到伤害-7%，体力持续恢复
        32, // 灵极上衣：最大体力+12%，技力恢复
        13, // 坚守戒指：受到伤害-10%，最大体力+7%
        52, // 秘银护手：攻防与最大体力各+5%
        56, // 猛兽耳饰：暴击发动率+15%，最大体力+2%
        22, // 银框眼镜：暴击发动率+15%
    ];

    private static readonly uint[] DefaultProtectedItemIds =
    [
        76, 77, 78, 79,
        80, 81, 82,
        128, 129, 130, 131, 132, 133, 134,
        140, 141,
    ];

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public int Version { get; set; } = 14;
    public string SelectedStageKey { get; set; } = string.Empty;
    public bool DiagnosticsEnabled { get; set; } = true;
    public int RepeatCount { get; set; }
    public bool SkipShop { get; set; }
    public bool SkipRest { get; set; }
    public bool AutoTargetBoss { get; set; }
    public bool AutoApproach { get; set; }
    public string CountdownCommand { get; set; } = "/beastmaster countdown 10";
    public bool AutoCollectShopItems { get; set; }
    public int TargetStage { get; set; } = 1;
    public int Flute1PetId { get; set; } = 16;
    public int Flute2PetId { get; set; } = 10;
    public int Flute3PetId { get; set; } = 1;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<uint> ShopPurchasePriority { get; set; } = [140, 79, 78, 77, 76, 82, 81, 80, 135];
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<uint> EquipmentPurchasePriority { get; set; } = [.. DefaultEquipmentPurchasePriority];
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<uint> ProtectedItemIds { get; set; } = [.. DefaultProtectedItemIds];
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<int, List<int>> CustomRoutes { get; set; } = [];
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<ArenaBossTarget> BossPriority { get; set; } = [.. BossAction.DefaultPriority];

    public uint[] FlutePetIds => [(uint)Flute1PetId, (uint)Flute2PetId, (uint)Flute3PetId];
    public string[] FlutePetNames =>
    [
        PetCatalog.GetName(Flute1PetId),
        PetCatalog.GetName(Flute2PetId),
        PetCatalog.GetName(Flute3PetId),
    ];

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
        CustomRoutes ??= [];
        BossPriority ??= [];

        var changed = false;
        if (Version < 2)
        {
            EquipmentPurchasePriority = [.. DefaultEquipmentPurchasePriority];
            Version = 2;
            changed = true;
        }
        if (Version < 3)
        {
            ProtectedItemIds = [.. DefaultProtectedItemIds];
            Version = 3;
            changed = true;
        }
        if (Version < 4)
        {
            EquipmentPurchasePriority = [.. DefaultEquipmentPurchasePriority];
            Version = 4;
            changed = true;
        }
        if (Version < 5)
        {
            AutoCollectShopItems = false;
            Version = 5;
            changed = true;
        }
        if (Version < 6)
        {
            SkipShop = false;
            SkipRest = false;
            Version = 6;
            changed = true;
        }
        if (Version < 7)
        {
            CountdownCommand = "/cd 5";
            Version = 7;
            changed = true;
        }
        if (Version < 8)
        {
            CountdownCommand = "/驯兽师 倒计时 10";
            Version = 8;
            changed = true;
        }
        if (Version < 9)
        {
            RepeatCount = 0;
            Version = 9;
            changed = true;
        }
        if (Version < 10)
        {
            CountdownCommand = "/beastmaster countdown 10";
            Version = 10;
            changed = true;
        }
        if (Version < 11)
        {
            BossPriority = [.. BossAction.DefaultPriority];
            Version = 11;
            changed = true;
        }
        if (Version < 12)
        {
            var knownBossIds = BossPriority.Select(x => x.Id).ToHashSet();
            BossPriority.AddRange(BossAction.DefaultPriority.Where(x => knownBossIds.Add(x.Id)));
            Version = 12;
            changed = true;
        }
        if (Version < 13)
        {
            var higherPriorityTargets = BossPriority
                .Where(x => x.Id is 19748 or 19746)
                .ToArray();
            BossPriority.RemoveAll(x => x.Id is 19748 or 19746);
            var finalBossIndex = BossPriority.FindIndex(x => x.Id == 19626);
            BossPriority.InsertRange(finalBossIndex >= 0 ? finalBossIndex : BossPriority.Count,
                higherPriorityTargets);
            Version = 13;
            changed = true;
        }
        if (Version < 14)
        {
            var wrongIdIndex = BossPriority.FindIndex(x => x.Id == 19476);
            BossPriority.RemoveAll(x => x.Id == 19476);
            if (BossPriority.All(x => x.Id != 19746))
            {
                var finalBossIndex = BossPriority.FindIndex(x => x.Id == 19626);
                var insertAt = wrongIdIndex >= 0
                    ? Math.Min(wrongIdIndex, BossPriority.Count)
                    : finalBossIndex >= 0 ? finalBossIndex : BossPriority.Count;
                BossPriority.Insert(insertAt, new ArenaBossTarget(19746, "魔刃"));
            }
            Version = 14;
            changed = true;
        }

        changed |= Normalize(ShopPurchasePriority, out var purchase);
        changed |= Normalize(EquipmentPurchasePriority, out var equipmentPurchase);
        changed |= Normalize(ProtectedItemIds, out var protectedItems);
        ShopPurchasePriority = purchase;
        EquipmentPurchasePriority = equipmentPurchase;
        ProtectedItemIds = protectedItems;
        var normalizedBosses = BossPriority
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .Select(x => new ArenaBossTarget(x.Key, x.First().Name.Trim()))
            .ToList();
        changed |= normalizedBosses.Count != BossPriority.Count
            || !normalizedBosses.SequenceEqual(BossPriority);
        BossPriority = normalizedBosses;
        if (changed)
            Save();
    }

    private static bool Normalize(List<uint>? source, out List<uint> result)
    {
        source ??= [];
        result = source.Where(x => x > 0).Distinct().ToList();
        return result.Count != source.Count;
    }

    public void Save()
        => pluginInterface?.SavePluginConfig(this);
}
