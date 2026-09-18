namespace ArenaPilot;

public sealed record ArenaPet(int Number, string Name);

public static class PetCatalog
{
    public static IReadOnlyList<ArenaPet> Pets { get; } =
    [
        new(1, "库西"),
        new(2, "松鼠"),
        new(3, "迷途羊羔"),
        new(4, "陆鱼"),
        new(5, "奥猴"),
        new(6, "渡渡鸟"),
        new(7, "矿爬虫"),
        new(8, "凶蛛蝎"),
        new(9, "巨型陆蟹"),
        new(10, "胡蜂"),
        new(11, "兀鹫"),
        new(12, "蔓德拉"),
        new(13, "死魂"),
        new(14, "跳蜥"),
        new(15, "壳蟹"),
        new(16, "螳螂"),
        new(17, "粘液怪"),
        new(18, "无头骑士"),
        new(19, "蝙蝠"),
        new(20, "陷阱草"),
        new(21, "席兹"),
        new(22, "仙人刺"),
        new(23, "巨像"),
        new(24, "碧企鹅"),
        new(25, "精金龟"),
        new(26, "大水牛"),
        new(27, "乌菊石"),
        new(28, "巨虫"),
        new(29, "魔石精"),
        new(30, "古菩猩猩"),
        new(31, "巨蟾蜍"),
        new(32, "蜂鸟"),
        new(33, "长须豹"),
        new(34, "盗龙"),
        new(35, "烈阳火蛟"),
        new(36, "树精"),
        new(37, "灵蚁"),
        new(38, "奇美拉"),
        new(39, "魔界花"),
        new(40, "妖魂"),
        new(41, "蝾螈"),
        new(42, "眼镜蛇"),
        new(43, "海德拉"),
        new(44, "灯心蜻蛉"),
        new(45, "腐坏古菩猩猩"),
        new(46, "祖"),
        new(47, "寒冰巨像"),
        new(48, "真红龙虾"),
        new(49, "大王花"),
        new(50, "贝希摩斯"),
    ];

    public static string GetName(int number)
        => Pets.FirstOrDefault(x => x.Number == number)?.Name ?? $"魔兽{number:00}";
}
