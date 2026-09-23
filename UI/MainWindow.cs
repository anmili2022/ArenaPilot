using Dalamud.Bindings.ImGui;
using System.Numerics;
using System.Reflection;

namespace ArenaPilot;

public sealed class MainWindow
{
    private static readonly string Version = typeof(MainWindow).Assembly
        .GetName().Version?.ToString(4) ?? "0.2.4.9";

    private readonly ArenaController controller;
    private readonly Configuration config;
    private bool isOpen;
    private bool showDebug;
    private bool configOpen;
    private string collectMessage = string.Empty;
    private int collectNodeIndex;
    private ArenaNodeKind collectNodeKind = ArenaNodeKind.Battle;
    private readonly Queue<string> flowLogLines = new();
    private string lastDisplayedFlowStatus = string.Empty;
    private bool scrollFlowLogToBottom;
    private uint purchaseItemSelection = 76;
    private uint equipmentPurchaseSelection = 1;
    private uint protectedItemSelection = 76;
    private string equipmentSearch = string.Empty;
    private string purchaseItemSearch = string.Empty;
    private string protectedItemSearch = string.Empty;
    private string itemStrategyMessage = string.Empty;
    private string bossNameInput = string.Empty;
    private int bossIdInput;
    private string bossConfigMessage = string.Empty;
    private readonly Dictionary<int, List<int>> routeDrafts = [];
    private string routeEditMessage = string.Empty;

    public MainWindow(ArenaController controller, Configuration config)
    {
        this.controller = controller;
        this.config = config;
    }

    public void Open() => isOpen = true;

    public void Draw()
    {
        DrawConfigWindow();

        if (!isOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(420f, 0f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"ArenaPilot-斗兽塔助手 v{Version}", ref isOpen, ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.End();
            return;
        }

        DrawActions();
        ImGui.Separator();
        DrawSettings();
        ImGui.Separator();
        DrawStatus();
        ImGui.Separator();
        DrawFlowLog();
        ImGui.Separator();
        DrawDebug();

        ImGui.End();
    }

    private void DrawStatus()
    {
        var phase = controller.Phase;
        var territory = controller.LastSnapshot.TerritoryName;
        var territoryId = controller.LastSnapshot.TerritoryId;
        var node = controller.LastSnapshot.CurrentNode;
        var bossInfo = BossAction.GetBossInfo(config.BossPriority);

        var phaseColor = phase switch
        {
            ArenaPhase.Idle => new Vector4(0.5f, 0.5f, 0.5f, 1f),
            ArenaPhase.Board => new Vector4(0.3f, 0.6f, 1f, 1f),
            ArenaPhase.BattlePreparation or ArenaPhase.Battle => new Vector4(0.9f, 0.27f, 0.38f, 1f),
            ArenaPhase.Result => new Vector4(0.8f, 0.6f, 0.2f, 1f),
            ArenaPhase.ItemDispose => new Vector4(0.95f, 0.55f, 0.2f, 1f),
            ArenaPhase.Loot or ArenaPhase.Treasure => new Vector4(0.2f, 0.7f, 0.5f, 1f),
            _ => new Vector4(0.6f, 0.6f, 0.6f, 1f),
        };

        ImGui.TextColored(phaseColor, $"[{phase}]");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 0.9f, 1f), $"{territory} ({territoryId})");
        ImGui.SameLine();
        ImGui.TextDisabled($"v{Version}");
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.82f, 0.7f, 0.4f, 1f), controller.CurrentRouteName);
        ImGui.SameLine();
        if (node.HasValue)
            ImGui.TextColored(new Vector4(0.56f, 0.87f, 0.94f, 1f), $"节点 {node.Value}");
        else
            ImGui.TextDisabled("无节点");

        ImGui.SameLine();
        if (bossInfo != null)
            ImGui.TextColored(new Vector4(0.47f, 0.82f, 0.68f, 1f), bossInfo);
    }

    private void DrawFlowLog()
    {
        ImGui.TextDisabled("流程信息");
        ImGui.SameLine();
        if (ImGui.SmallButton("复制全部##flowlog"))
            ImGui.SetClipboardText(string.Join(Environment.NewLine, flowLogLines));
        if (!string.IsNullOrWhiteSpace(controller.FullFlowStatus)
            && controller.FullFlowStatus != lastDisplayedFlowStatus)
        {
            lastDisplayedFlowStatus = controller.FullFlowStatus;
            flowLogLines.Enqueue($"[{DateTime.Now:HH:mm:ss}] {controller.FullFlowStatus}");
            scrollFlowLogToBottom = true;
        }

        var status = string.Join(Environment.NewLine, flowLogLines);
        var height = ImGui.GetTextLineHeightWithSpacing() * 3f
            + ImGui.GetStyle().FramePadding.Y * 2f + 4f;
        ImGui.BeginChild("##flowlog", new Vector2(-1f, height), true);
        ImGui.TextUnformatted(status);
        if (scrollFlowLogToBottom)
        {
            ImGui.SetScrollHereY(1f);
            scrollFlowLogToBottom = false;
        }
        ImGui.EndChild();
    }

    private void DrawActions()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(12f, 5f));

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.6f, 0.3f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.25f, 0.7f, 0.35f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.15f, 0.5f, 0.25f, 1f));
        if (ImGui.Button("开始", new Vector2(80f, 0f)))
            controller.StartFullFlow();
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.6f, 0.1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.7f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.5f, 0.05f, 1f));
        if (ImGui.Button("继续", new Vector2(80f, 0f)))
            controller.ContinueFlow();
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.7f, 0.2f, 0.2f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.8f, 0.25f, 0.25f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.6f, 0.15f, 0.15f, 1f));
        if (ImGui.Button("停止", new Vector2(80f, 0f)))
            controller.Stop("用户请求停止");
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.5f, 0.1f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.9f, 0.6f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.7f, 0.4f, 0.05f, 1f));
        if (ImGui.Button("状态", new Vector2(80f, 0f)))
            ImGui.SetClipboardText(controller.GetStatusText());
        ImGui.PopStyleColor(3);

        ImGui.PopStyleVar();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.45f, 0.6f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.35f, 0.55f, 0.7f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.25f, 0.4f, 0.55f, 1f));
        if (ImGui.Button("配置", new Vector2(80f, 0f)))
            configOpen = true;
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.4f, 0.5f, 0.35f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.5f, 0.6f, 0.4f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.35f, 0.45f, 0.3f, 1f));
        if (ImGui.Button("记录坐标", new Vector2(80f, 0f)))
        {
            var line = controller.RecordNodeCoordinate(collectNodeIndex, collectNodeKind);
            var existing = ImGui.GetClipboardText();
            ImGui.SetClipboardText(string.IsNullOrEmpty(existing) ? line : existing + "\n" + line);
            collectMessage = line;
        }
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        ImGui.SetNextItemWidth(70f);
        if (ImGui.BeginCombo("##collect-node", $"节点 {collectNodeIndex}"))
        {
            for (var i = 0; i <= 29; i++)
            {
                if (ImGui.Selectable($"节点 {i}", collectNodeIndex == i))
                    collectNodeIndex = i;
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();

        ImGui.SetNextItemWidth(70f);
        if (ImGui.BeginCombo("##collect-kind", ArenaNodeKindLabels.Get(collectNodeKind)))
        {
            foreach (var kind in new[] { ArenaNodeKind.Start, ArenaNodeKind.Battle, ArenaNodeKind.Treasure, ArenaNodeKind.Rest, ArenaNodeKind.Shop, ArenaNodeKind.Boss, ArenaNodeKind.Random })
            {
                if (ImGui.Selectable(ArenaNodeKindLabels.Get(kind), collectNodeKind == kind))
                    collectNodeKind = kind;
            }
            ImGui.EndCombo();
        }

        if (!string.IsNullOrWhiteSpace(collectMessage))
        {
            ImGui.TextDisabled("已记录：");
            ImGui.SameLine();
            ImGui.TextWrapped(collectMessage);
        }
    }

    private void DrawSettings()
    {
        ImGui.TextDisabled("设置");
        var a = false;

        a = config.SkipShop;
        if (ImGui.Checkbox("跳过商店", ref a)) { config.SkipShop = a; config.Save(); }
        DrawSettingTooltip("启用：进入商店后直接结束购买，不出售道具，也不购买装备或奇弈道具。\n关闭：按商店策略先出售未保护道具，再购买优先装备和奇弈道具。");
        ImGui.SameLine(200f);

        a = config.SkipRest;
        if (ImGui.Checkbox("跳过休息", ref a)) { config.SkipRest = a; config.Save(); }
        DrawSettingTooltip("启用：进入帐篷后直接离开，不恢复玩家或魔兽体力。\n关闭：执行休息确认流程，完成后自动离开帐篷。");

        a = config.AutoTargetBoss;
        if (ImGui.Checkbox("自动选中BOSS", ref a))
        {
            config.AutoTargetBoss = a;
            if (!a)
                BossAction.ClearKnownBossTarget(config.BossPriority);
            config.Save();
        }
        DrawSettingTooltip("启用：战斗准备阶段按 BOSS 选择页中的 BaseId 优先级自动选中当前战斗目标。\n关闭：立即清除本插件已选中的已知 BOSS，之后不再修改目标。其他战斗插件仍可能自行选怪。");
        ImGui.SameLine(200f);

        a = config.AutoApproach;
        if (ImGui.Checkbox("自动接近+倒计时", ref a)) { config.AutoApproach = a; config.Save(); }
        DrawSettingTooltip("启用：目标圈边距离超过20米时自动接近；目标实际出现并进入范围后等待5秒，再发送基础设置中的倒计时指令。\n关闭：不移动、不发送倒计时，战斗位置由玩家控制。");

        ImGui.SetNextItemWidth(100f);
        var repeatCount = config.RepeatCount;
        if (ImGui.InputInt("完成 X 场停止", ref repeatCount))
        {
            config.RepeatCount = Math.Max(0, repeatCount);
            config.Save();
        }
        DrawSettingTooltip("完成指定场数并关闭结算后停止自动流程。设置为 0 时不限制场数，会持续循环。");
        ImGui.SameLine();
        if (controller.IsFullFlowRunning)
            ImGui.TextDisabled($"当前第 {controller.CompletedRunCount + 1} 场");
        else if (controller.CompletedRunCount > 0)
            ImGui.TextDisabled($"已完成 {controller.CompletedRunCount} 场");
        else
            ImGui.TextDisabled("尚未开始");

    }

    private static void DrawSettingTooltip(string text)
    {
        if (!ImGui.IsItemHovered())
            return;
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(420f);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private void DrawDebug()
    {
        var label = showDebug ? "[-] 调试信息" : "[+] 调试信息";
        if (ImGui.Selectable(label, false, ImGuiSelectableFlags.SpanAllColumns))
            showDebug = !showDebug;

        if (!showDebug)
            return;

        ImGui.Indent(8f);
        var s = controller.LastSnapshot;
        DebugLine("阶段说明", s.PhaseReason);
        DebugLine("盘数", s.StageId == 0 ? "未知" : s.StageId.ToString());
        DebugLine("副本编号", s.ContentId.ToString());
        DebugLine("已登录", s.IsLoggedIn ? "是" : "否");
        DebugLine("位置", s.PlayerX.HasValue
            ? $"{s.PlayerX:F2}, {s.PlayerY:F2}, {s.PlayerZ:F2}" : "未知");
        DebugLine("节点类型", s.CurrentNodeKind.ToString());
        DebugLine("目标节点", controller.TargetNode?.ToString() ?? "无");
        DebugLine("上一节点", controller.PreviousNode?.ToString() ?? "无");
        DebugLine("路线建议", controller.RouteSuggestion);
        DebugLine("准备检查", controller.PreparationCheck);
        DebugLine("兽笛操作", controller.PartyActionStatus);
        DebugLine("战斗操作", controller.BattleActionStatus);
        DebugLine("商店物品采集", $"{controller.ShopCatalogCount} 条 | {controller.ShopCatalogPath}");
        DebugLine("可见 Addon", s.VisibleAddons.Count == 0 ? "无" : string.Join(", ", s.VisibleAddons));
        if (!string.IsNullOrWhiteSpace(s.FailureReason))
            DebugLine("错误", s.FailureReason);
        if (!string.IsNullOrWhiteSpace(controller.LastExportPath))
            DebugLine("最近导出", controller.LastExportPath);
        ImGui.Unindent(8f);
    }

    private static void DebugLine(string label, string value)
    {
        ImGui.TextDisabled($"{label}：");
        ImGui.SameLine();
        ImGui.TextWrapped(value);
    }

    private void DrawConfigWindow()
    {
        if (!configOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(560f, 640f), ImGuiCond.FirstUseEver);
        if (!ImGui.Begin($"ArenaPilot-斗兽塔助手 配置 v{Version}", ref configOpen))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##config-tabs"))
        {
            if (ImGui.BeginTabItem("基础设置"))
            {
                DrawGeneralConfig();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("商店策略"))
            {
                DrawStrategyConfig();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("BOSS选择"))
            {
                DrawBossConfig();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("路线选择"))
            {
                DrawRouteConfig();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void DrawGeneralConfig()
    {
        ImGui.TextDisabled("设置目标盘和 1、2、3 号兽笛使用的召唤兽");
        if (StageCombo(config.TargetStage, out var stage))
        {
            config.TargetStage = stage;
            config.Save();
        }
        ImGui.Separator();
        if (PetCombo("1 笛", config.Flute1PetId, out var flute1))
        {
            config.Flute1PetId = flute1;
            config.Save();
        }
        if (PetCombo("2 笛", config.Flute2PetId, out var flute2))
        {
            config.Flute2PetId = flute2;
            config.Save();
        }
        if (PetCombo("3 笛", config.Flute3PetId, out var flute3))
        {
            config.Flute3PetId = flute3;
            config.Save();
        }
        ImGui.Separator();
        ImGui.TextDisabled($"当前配置：{string.Join(" + ", config.FlutePetNames)}");
        ImGui.Separator();
        ImGui.SetNextItemWidth(260f);
        var countdownCommand = config.CountdownCommand;
        if (ImGui.InputText("倒计时指令", ref countdownCommand, 128))
        {
            config.CountdownCommand = countdownCommand;
            config.Save();
        }
        DrawSettingTooltip("自动接近 BOSS 后发送的游戏指令，默认 /beastmaster countdown 10。留空时不会发送倒计时指令。");
    }

    private void DrawStrategyConfig()
    {
        ImGui.TextDisabled("购买列表按优先级处理；商店出售和宝箱替换会按槽位处理所有未保护道具。");
        if (!ImGui.BeginTabBar("##item-strategies"))
            return;
        if (ImGui.BeginTabItem("装备购买"))
        {
            DrawItemStrategy("装备购买优先级", config.EquipmentPurchasePriority,
                ref equipmentPurchaseSelection, ref equipmentSearch,
                CrucibleItemCatalog.AllItems.Where(x => x.Id < 76));
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("道具购买"))
        {
            DrawItemStrategy("道具购买优先级", config.ShopPurchasePriority,
                ref purchaseItemSelection, ref purchaseItemSearch, CrucibleItemCatalog.Items);
            ImGui.EndTabItem();
        }
        if (ImGui.BeginTabItem("保护列表"))
        {
            DrawItemStrategy("保护列表", config.ProtectedItemIds,
                ref protectedItemSelection, ref protectedItemSearch, CrucibleItemCatalog.Items);
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawBossConfig()
    {
        ImGui.TextDisabled("列表顺序即自动选中优先级；ID 使用游戏对象 BaseId。");
        if (ImGui.SmallButton("导出##boss-priority"))
        {
            var lines = new List<string> { "优先级,ID,名称" };
            lines.AddRange(config.BossPriority.Select((boss, index) =>
                $"{index + 1},{boss.Id},{boss.Name.Replace(',', ' ')}"));
            ImGui.SetClipboardText(string.Join(Environment.NewLine, lines));
            bossConfigMessage = $"已导出 {config.BossPriority.Count} 项到剪贴板";
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("导入##boss-priority"))
        {
            var imported = ParseImportedBosses(ImGui.GetClipboardText());
            if (imported.Count == 0)
                bossConfigMessage = "导入失败：剪贴板中没有有效的 BOSS 名称和 ID";
            else
            {
                config.BossPriority.Clear();
                config.BossPriority.AddRange(imported);
                config.Save();
                bossConfigMessage = $"已导入 {imported.Count} 项";
            }
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("恢复默认##boss-priority"))
        {
            config.BossPriority = [.. BossAction.DefaultPriority];
            config.Save();
            bossConfigMessage = $"已恢复默认列表：{config.BossPriority.Count} 项";
        }
        if (!string.IsNullOrWhiteSpace(bossConfigMessage))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(bossConfigMessage);
        }

        ImGui.Separator();
        ImGui.SetNextItemWidth(220f);
        ImGui.InputTextWithHint("##boss-name", "BOSS 名称", ref bossNameInput, 128);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130f);
        ImGui.InputInt("##boss-id", ref bossIdInput, 0, 0);
        ImGui.SameLine();
        if (ImGui.Button("添加##boss-priority"))
        {
            var name = bossNameInput.Trim();
            if (string.IsNullOrWhiteSpace(name) || bossIdInput <= 0)
                bossConfigMessage = "添加失败：请输入 BOSS 名称和大于 0 的 ID";
            else if (config.BossPriority.Any(x => x.Id == (uint)bossIdInput))
                bossConfigMessage = $"添加失败：ID {bossIdInput} 已存在";
            else
            {
                config.BossPriority.Add(new ArenaBossTarget((uint)bossIdInput, name));
                config.Save();
                bossConfigMessage = $"已添加 {name} ({bossIdInput})";
                bossNameInput = string.Empty;
                bossIdInput = 0;
            }
        }

        ImGui.Separator();
        ImGui.BeginChild("##boss-priority-list", new Vector2(-1f, -1f), true);
        for (var index = 0; index < config.BossPriority.Count; index++)
        {
            var boss = config.BossPriority[index];
            ImGui.PushID($"boss-{index}-{boss.Id}");
            ImGui.TextUnformatted($"{index + 1}. {boss.Name} ({boss.Id})");
            ImGui.SameLine(300f);
            if (ImGui.SmallButton("置顶") && index > 0)
            {
                config.BossPriority.RemoveAt(index);
                config.BossPriority.Insert(0, boss);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("置底") && index < config.BossPriority.Count - 1)
            {
                config.BossPriority.RemoveAt(index);
                config.BossPriority.Add(boss);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("上移") && index > 0)
            {
                (config.BossPriority[index - 1], config.BossPriority[index]) =
                    (config.BossPriority[index], config.BossPriority[index - 1]);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("下移") && index < config.BossPriority.Count - 1)
            {
                (config.BossPriority[index + 1], config.BossPriority[index]) =
                    (config.BossPriority[index], config.BossPriority[index + 1]);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("删除"))
            {
                config.BossPriority.RemoveAt(index);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }
        ImGui.EndChild();
    }

    private static IReadOnlyList<ArenaBossTarget> ParseImportedBosses(string text)
    {
        var result = new List<ArenaBossTarget>();
        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split([',', '\t', ';'], StringSplitOptions.TrimEntries);
            if (fields.Length < 2)
                continue;

            uint id;
            string name;
            if (fields.Length >= 3 && uint.TryParse(fields[1], out id))
                name = string.Join(" ", fields.Skip(2)).Trim();
            else if (uint.TryParse(fields[0], out id))
                name = string.Join(" ", fields.Skip(1)).Trim();
            else
                continue;

            if (id == 0 || string.IsNullOrWhiteSpace(name) || result.Any(x => x.Id == id))
                continue;
            result.Add(new ArenaBossTarget(id, name));
        }
        return result;
    }

    private void DrawRouteConfig()
    {
        ImGui.TextDisabled("点击节点按顺序编排路线；只能连接到当前节点的下一节点。");
        if (!ImGui.BeginTabBar("##route-tabs"))
            return;

        foreach (var route in ArenaRoutes.DisplayRoutes)
        {
            if (!ImGui.BeginTabItem(route.Name))
                continue;
            DrawRouteEditor(route);
            ImGui.EndTabItem();
        }
        ImGui.EndTabBar();
    }

    private void DrawRouteEditor(ArenaStageRoute route)
    {
        if (!routeDrafts.TryGetValue(route.StageId, out var draft))
        {
            draft = config.CustomRoutes.TryGetValue(route.StageId, out var saved) && saved.Count > 0
                ? [.. saved]
                : BuildDefaultRoute(route);
            routeDrafts[route.StageId] = draft;
        }

        if (!route.IsConfigured)
            ImGui.BeginDisabled();
        if (ImGui.Button($"设为目标盘##route-{route.StageId}"))
        {
            config.TargetStage = route.StageId;
            config.Save();
            routeEditMessage = $"目标盘已设为 {route.Name}";
        }
        if (!route.IsConfigured)
            ImGui.EndDisabled();
        ImGui.SameLine();
        if (ImGui.Button($"撤销##route-{route.StageId}") && draft.Count > 1)
        {
            draft.RemoveAt(draft.Count - 1);
            routeEditMessage = "已撤销最后一个节点";
        }
        ImGui.SameLine();
        if (ImGui.Button($"恢复默认##route-{route.StageId}"))
        {
            draft.Clear();
            draft.AddRange(BuildDefaultRoute(route));
            routeEditMessage = "已恢复默认路线草稿";
        }
        ImGui.SameLine();
        if (!route.IsConfigured)
            ImGui.BeginDisabled();
        if (ImGui.Button($"保存路线##route-{route.StageId}"))
        {
            config.CustomRoutes[route.StageId] = [.. draft];
            config.TargetStage = route.StageId;
            config.Save();
            routeEditMessage = $"已保存 {route.Name}：{string.Join(" → ", draft)}";
        }
        if (!route.IsConfigured)
            ImGui.EndDisabled();

        ImGui.TextUnformatted($"当前路线：{string.Join(" → ", draft)}");
        if (!route.IsConfigured)
            ImGui.TextColored(new Vector4(0.95f, 0.62f, 0.2f, 1f), "仅展示：等待实测坐标后启用导航和路线保存");
        if (!string.IsNullOrWhiteSpace(routeEditMessage))
            ImGui.TextDisabled(routeEditMessage);

        var canvasSize = new Vector2(Math.Max(480f, ImGui.GetContentRegionAvail().X), 390f);
        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(canvasSize);
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(origin, origin + canvasSize,
            ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.09f, 0.1f, 0.45f)), 4f);
        var minX = route.Nodes.Min(x => x.Center.X);
        var maxX = route.Nodes.Max(x => x.Center.X);
        var minZ = route.Nodes.Min(x => x.Center.Z);
        var maxZ = route.Nodes.Max(x => x.Center.Z);
        const float padding = 32f;

        Vector2 NodePosition(ArenaStageNode node)
        {
            var xRange = Math.Max(1f, maxX - minX);
            var zRange = Math.Max(1f, maxZ - minZ);
            return new Vector2(
                origin.X + padding + (node.Center.X - minX) / xRange * (canvasSize.X - padding * 2f),
                origin.Y + padding + (node.Center.Z - minZ) / zRange * (canvasSize.Y - padding * 2f));
        }

        var defaultEdges = BuildDefaultRoute(route).Zip(BuildDefaultRoute(route).Skip(1)).ToHashSet();
        var customEdges = draft.Zip(draft.Skip(1)).ToHashSet();
        foreach (var node in route.Nodes)
        {
            foreach (var nextIndex in node.Next)
            {
                var next = route.Nodes.FirstOrDefault(x => x.Index == nextIndex);
                if (next == null)
                    continue;
                var edge = (node.Index, next.Index);
                var color = customEdges.Contains(edge)
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.95f, 0.58f, 0.16f, 1f))
                    : defaultEdges.Contains(edge)
                        ? ImGui.ColorConvertFloat4ToU32(new Vector4(0.25f, 0.75f, 0.4f, 1f))
                        : ImGui.ColorConvertFloat4ToU32(new Vector4(0.38f, 0.4f, 0.43f, 1f));
                drawList.AddLine(NodePosition(node), NodePosition(next), color, customEdges.Contains(edge) ? 4f : 2f);
            }
        }

        foreach (var node in route.Nodes)
        {
            var center = NodePosition(node);
            var nodeColor = node.IsNavigable
                ? GetRouteNodeColor(node.Kind)
                : ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 0.32f, 0.35f, 1f));
            drawList.AddCircleFilled(center, 15f, nodeColor);
            drawList.AddCircle(center, 15f,
                draft.Contains(node.Index)
                    ? ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.75f, 0.2f, 1f))
                    : ImGui.ColorConvertFloat4ToU32(new Vector4(0.85f, 0.85f, 0.85f, 1f)),
                0, draft.Contains(node.Index) ? 3f : 1f);
            drawList.AddText(center - new Vector2(node.Index >= 10 ? 8f : 4f, 7f), 0xFFFFFFFF, node.Index.ToString());

            ImGui.SetCursorScreenPos(center - new Vector2(17f, 17f));
            if (ImGui.InvisibleButton($"##route-node-{route.StageId}-{node.Index}", new Vector2(34f, 34f)))
                AddRouteNode(route, draft, node.Index);
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"节点 {node.Index} · {ArenaNodeKindLabels.Get(node.Kind)}");
                ImGui.TextDisabled(node.IsNavigable
                    ? $"{node.Center.X:F2}, {node.Center.Y:F2}, {node.Center.Z:F2}"
                    : "坐标未采集，仅用于路线图展示");
                ImGui.EndTooltip();
            }
        }
        ImGui.SetCursorScreenPos(origin + new Vector2(0f, canvasSize.Y));
    }

    private void AddRouteNode(ArenaStageRoute route, List<int> draft, int node)
    {
        var target = route.Nodes.First(x => x.Index == node);
        if (!target.IsNavigable)
        {
            routeEditMessage = $"节点 {node} 尚未采集坐标，不能加入路线";
            return;
        }

        var existingIndex = draft.IndexOf(node);
        if (existingIndex >= 0)
        {
            draft.RemoveRange(existingIndex + 1, draft.Count - existingIndex - 1);
            routeEditMessage = $"路线已截断到节点 {node}";
            return;
        }

        if (draft.Count == 0)
        {
            draft.Add(node);
            return;
        }
        var last = route.Nodes.First(x => x.Index == draft[^1]);
        if (!last.Next.Contains(node))
        {
            routeEditMessage = $"节点 {draft[^1]} 无法直接连接节点 {node}";
            return;
        }
        draft.Add(node);
        routeEditMessage = $"已添加节点 {node}";
    }

    private static List<int> BuildDefaultRoute(ArenaStageRoute route)
    {
        var result = new List<int>();
        var current = route.Nodes.FirstOrDefault(x => x.Kind == ArenaNodeKind.Start)?.Index
            ?? route.Nodes.Min(x => x.Index);
        var visited = new HashSet<int>();
        while (visited.Add(current))
        {
            result.Add(current);
            var next = route.GetPreferredNext(current);
            if (!next.HasValue)
                break;
            current = next.Value;
        }
        return result;
    }

    private static uint GetRouteNodeColor(ArenaNodeKind kind)
        => ImGui.ColorConvertFloat4ToU32(kind switch
        {
            ArenaNodeKind.Start => new Vector4(0.4f, 0.65f, 0.95f, 1f),
            ArenaNodeKind.Battle => new Vector4(0.82f, 0.25f, 0.3f, 1f),
            ArenaNodeKind.Boss => new Vector4(0.65f, 0.12f, 0.16f, 1f),
            ArenaNodeKind.Shop => new Vector4(0.92f, 0.65f, 0.18f, 1f),
            ArenaNodeKind.Rest => new Vector4(0.25f, 0.65f, 0.85f, 1f),
            ArenaNodeKind.Treasure => new Vector4(0.2f, 0.7f, 0.45f, 1f),
            ArenaNodeKind.Random => new Vector4(0.65f, 0.42f, 0.82f, 1f),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1f),
        });

    private void DrawItemStrategy(
        string title,
        List<uint> items,
        ref uint selection,
        ref string search,
        IEnumerable<CrucibleItem> options)
    {
        var availableOptions = options.ToArray();

        if (ImGui.SmallButton($"导出##{title}"))
        {
            var lines = new List<string> { "优先级,ID,名称" };
            lines.AddRange(items.Select((id, index) =>
                $"{index + 1},{id},{CrucibleItemCatalog.GetName(id)}"));
            ImGui.SetClipboardText(string.Join(Environment.NewLine, lines));
            itemStrategyMessage = $"已导出 {title}：{items.Count} 项";
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"导入##{title}"))
        {
            var allowedIds = availableOptions.Select(x => x.Id).ToHashSet();
            var imported = ParseImportedItemIds(ImGui.GetClipboardText(), allowedIds);
            if (imported.Count == 0)
            {
                itemStrategyMessage = $"导入 {title} 失败：剪贴板中没有有效 ID";
            }
            else
            {
                items.Clear();
                items.AddRange(imported);
                config.Save();
                itemStrategyMessage = $"已导入 {title}：{items.Count} 项";
            }
        }
        if (!string.IsNullOrWhiteSpace(itemStrategyMessage))
        {
            ImGui.SameLine();
            ImGui.TextDisabled(itemStrategyMessage);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var itemId = items[index];
            ImGui.PushID($"{title}-{index}");
            ImGui.TextUnformatted($"{index + 1}. {CrucibleItemCatalog.GetName(itemId)} ({itemId})");
            DrawItemTooltip(itemId);
            ImGui.SameLine(245f);
            if (ImGui.SmallButton("置顶") && index > 0)
            {
                items.RemoveAt(index);
                items.Insert(0, itemId);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("置底") && index < items.Count - 1)
            {
                items.RemoveAt(index);
                items.Add(itemId);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("上移") && index > 0)
            {
                (items[index - 1], items[index]) = (items[index], items[index - 1]);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("下移") && index < items.Count - 1)
            {
                (items[index + 1], items[index]) = (items[index], items[index + 1]);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("删除"))
            {
                items.RemoveAt(index);
                config.Save();
                ImGui.PopID();
                break;
            }
            ImGui.PopID();
        }

        ImGui.SetNextItemWidth(260f);
        ImGui.InputTextWithHint($"##search-{title}", "搜索名称或 ID", ref search, 128);

        var query = search.Trim();
        var filteredOptions = availableOptions
            .Where(item => string.IsNullOrEmpty(query)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Id.ToString().Contains(query, StringComparison.Ordinal))
            .ToArray();
        var currentSelection = selection;
        if (filteredOptions.Length > 0 && !filteredOptions.Any(x => x.Id == currentSelection))
            selection = filteredOptions[0].Id;

        ImGui.SetNextItemWidth(260f);
        var selectionText = filteredOptions.Length == 0
            ? "没有匹配项"
            : $"{CrucibleItemCatalog.GetName(selection)} ({selection})";
        if (ImGui.BeginCombo($"##add-{title}", selectionText))
        {
            foreach (var item in filteredOptions)
            {
                var selected = item.Id == selection;
                if (ImGui.Selectable($"{item.Name} ({item.Id})", selected))
                    selection = item.Id;
                DrawItemTooltip(item.Id);
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        currentSelection = selection;
        if (ImGui.Button($"添加##{title}")
            && filteredOptions.Any(x => x.Id == currentSelection)
            && !items.Contains(selection))
        {
            items.Add(selection);
            config.Save();
        }
    }

    private static void DrawItemTooltip(uint itemId)
    {
        if (!ImGui.IsItemHovered())
            return;

        var detail = CrucibleItemDetails.Get(itemId);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(420f);
        ImGui.TextUnformatted($"{CrucibleItemCatalog.GetName(itemId)} ({itemId})");
        ImGui.TextDisabled(CrucibleItemCatalog.GetCategory(itemId));
        if (detail?.Price is int price)
            ImGui.TextUnformatted($"售价：{price}");
        if (!string.IsNullOrWhiteSpace(detail?.Effect))
        {
            ImGui.Separator();
            ImGui.TextWrapped(detail.Effect);
        }
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static IReadOnlyList<uint> ParseImportedItemIds(string text, IReadOnlySet<uint> allowedIds)
    {
        var result = new List<uint>();
        var lines = text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var exportedCsv = lines.FirstOrDefault()?.Contains("ID", StringComparison.OrdinalIgnoreCase) == true;
        foreach (var line in lines)
        {
            var fields = line.Split([',', '\t', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
            var numbers = fields
                .Select(field => uint.TryParse(field.Trim(), out var value) ? value : 0)
                .Where(value => value > 0 && allowedIds.Contains(value))
                .ToArray();
            foreach (var itemId in exportedCsv ? numbers.TakeLast(1) : numbers)
            {
                if (!result.Contains(itemId))
                    result.Add(itemId);
            }
        }
        return result;
    }

    private static bool StageCombo(int current, out int next)
    {
        next = current;
        var changed = false;

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo("##target-stage", ArenaRoutes.GetStageName(current)))
        {
            foreach (var option in ArenaRoutes.StageOptions)
            {
                var selected = option.Id == current;
                if (ImGui.Selectable(option.Name, selected))
                {
                    next = option.Id;
                    changed = true;
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted("目标盘");
        return changed;
    }

    private static bool PetCombo(string label, int current, out int next)
    {
        next = current;
        var changed = false;

        ImGui.SetNextItemWidth(200f);
        if (ImGui.BeginCombo($"##flute-{label}", PetCatalog.GetName(current)))
        {
            foreach (var pet in PetCatalog.Pets)
            {
                var selected = pet.Number == current;
                if (ImGui.Selectable($"{pet.Number:00} {pet.Name}", selected))
                {
                    next = pet.Number;
                    changed = true;
                }
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        return changed;
    }
}
