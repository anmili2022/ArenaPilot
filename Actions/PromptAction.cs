namespace ArenaPilot;

public static class PromptAction
{
    public static string? ReadText(ArenaSnapshot snapshot)
        => snapshot.Addons
            .FirstOrDefault(x => x.Name == "SelectYesno" && x.IsReady)
            ?.Values.Select(x => x.Text)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    public static bool TryHandle(ArenaSnapshot snapshot, out string status)
    {
        var text = ReadText(snapshot);
        if (string.IsNullOrWhiteSpace(text))
        {
            status = string.Empty;
            return false;
        }

        var compact = string.Concat(text.Where(x => !char.IsWhiteSpace(x)));
        if (compact.Contains("兽笛未设置魔兽", StringComparison.Ordinal))
        {
            if (!AddonUi.TryFireCallback("SelectYesno", 1, out var rejectError))
            {
                status = rejectError;
                return false;
            }
            status = "已取消未设置兽笛提示";
            return true;
        }

        if (compact.Contains("魔兽未满", StringComparison.Ordinal)
            || (compact.Contains("开始挑战", StringComparison.Ordinal)
                && compact.Contains("兽级比推荐低", StringComparison.Ordinal))
            || (compact.Contains("只让玩家休息", StringComparison.Ordinal)
                && compact.Contains("恢复", StringComparison.Ordinal)
                && compact.Contains("体力", StringComparison.Ordinal))
            || (compact.Contains("确定要购买", StringComparison.Ordinal)
                && compact.Contains("吗？", StringComparison.Ordinal))
            || (compact.Contains("要卖掉", StringComparison.Ordinal)
                && compact.Contains("斗兽币", StringComparison.Ordinal))
            || compact.Contains("全部获取", StringComparison.Ordinal)
            || compact.Contains("确定要直接离开帐篷", StringComparison.Ordinal)
            || compact.Contains("确定要离开吗？", StringComparison.Ordinal)
            || compact.Contains("确定要结束寻宝", StringComparison.Ordinal)
            || compact.Contains("不拿道具直接结束", StringComparison.Ordinal)
            || compact.Contains("确定要结束购买", StringComparison.Ordinal)
            || compact.Contains("还有战利品没拿", StringComparison.Ordinal)
            || (compact.Contains("确定要获取", StringComparison.Ordinal) && compact.Contains("吗？", StringComparison.Ordinal)))
        {
            if (!AddonUi.TryFireCallback("SelectYesno", 0, out var confirmError))
            {
                status = confirmError;
                return false;
            }
            status = $"已确认提示：{text}";
            return true;
        }

        status = string.Empty;
        return false;
    }
}
