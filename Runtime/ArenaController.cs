namespace ArenaPilot;

public sealed class ArenaController
{
    private enum ShopPendingAction
    {
        None,
        Buy,
        Sell,
    }

    private enum ShopStage
    {
        SellingItems,
        BuyingEquipment,
        BuyingItems,
    }

    private static readonly TimeSpan FullFlowTimeout = TimeSpan.FromMinutes(45);
    private static readonly TimeSpan NodeEventTimeout = TimeSpan.FromSeconds(12);
    private static readonly string Version = typeof(ArenaController).Assembly
        .GetName().Version?.ToString(3) ?? "0.2.3";

    private readonly ArenaUiReader reader;
    private readonly SnapshotExporter exporter;
    private readonly ShopCatalogCollector shopCatalogCollector;
    private readonly VnavmeshClient navigation;
    private readonly Configuration config;
    private readonly HashSet<int> processedNodes = [];

    private DateTime nextPartyActionUtc;
    private DateTime partyActionDeadlineUtc;
    private int partyActionAttempts;
    private bool assigningParty;
    private bool startBattleAfterParty;
    private bool startingBattle;
    private DateTime battleActionDeadlineUtc;
    private bool fullFlow;
    private DateTime fullFlowDeadlineUtc;
    private DateTime nextFullFlowActionUtc;
    private bool countdownSent;
    private DateTime battleStartedAtUtc;
    private bool challengeSent;
    private bool challengeConfirmed;
    private bool battleObserved;
    private bool rewardRequested;
    private bool rewardTakeAllRejected;
    private bool rewardSingleRequested;
    private DateTime resultSeenAtUtc;
    private DateTime resultNextPageAtUtc;
    private bool restLeaveSent;
    private bool restTried;
    private bool restConfirmed;
    private bool treasureTried;
    private bool shopTried;
    private ShopStage shopStage;
    private ShopPendingAction shopPendingAction;
    private uint pendingShopItemId;
    private int pendingShopSlot = -1;
    private int pendingShopGold;
    private int pendingShopItemCount;
    private DateTime shopPendingSinceUtc;
    private int shopPurchaseCount;
    private bool itemDisposeTried;
    private bool navigationStarted;
    private DateTime navigationStartedAtUtc;
    private DateTime arrivedAtUtc;
    private int navigationAttempts;
    private int? targetNode;
    private int? eventNode;
    private ArenaStageRoute? currentRoute;
    private bool waitingForAreaExit;
    private bool wasInResult;
    private DateTime nextShopCatalogScanUtc;
    private int shopCatalogScrollSlot;
    private int shopCatalogAwaitingSlot = -1;
    private bool shopCatalogScanComplete;

    public ArenaPhase Phase { get; private set; } = ArenaPhase.Idle;
    public ArenaSnapshot LastSnapshot { get; private set; }
    public string LastExportPath { get; private set; } = string.Empty;
    public string DiagnosticsDirectory { get; }
    public string ShopCatalogPath => shopCatalogCollector.FilePath;
    public int ShopCatalogCount => shopCatalogCollector.Count;
    public int? PreviousNode { get; private set; }
    public int? TargetNode => targetNode;
    public string CurrentRouteName => currentRoute?.Name ?? "未识别";
    public string RouteSuggestion { get; private set; } = "等待读取棋盘";
    public string PreparationCheck { get; private set; } = "尚未检查战斗准备";
    public string PartyActionStatus { get; private set; } = "尚未执行兽笛编队";
    public string BattleActionStatus { get; private set; } = "尚未执行战斗开始";
    public string FullFlowStatus
    {
        get => fullFlowStatus;
        private set => fullFlowStatus = value;
    }
    private string fullFlowStatus = "尚未开始总流程";
    public int ContinueFromNode { get; set; } = 1;

    public string GetStatusText()
    {
        return $"版本：v{Version}\n"
            + $"状态：{Phase}\n"
            + $"斗兽阶段：{LastSnapshot.PhaseReason}\n"
            + $"目标盘：{ArenaRoutes.GetStageName(config.TargetStage)}\n"
            + $"当前路线：{CurrentRouteName}\n"
            + $"盘数：{LastSnapshot.StageId}\n"
            + $"副本编号：{LastSnapshot.ContentId}\n"
            + $"区域：{LastSnapshot.TerritoryId}\n"
            + $"位置：{(LastSnapshot.PlayerX.HasValue ? $"{LastSnapshot.PlayerX:F2}, {LastSnapshot.PlayerY:F2}, {LastSnapshot.PlayerZ:F2}" : "未知")}\n"
            + $"棋盘节点：{(LastSnapshot.CurrentNode?.ToString() ?? "不在节点上")}\n"
            + $"节点类型：{LastSnapshot.CurrentNodeKind}\n"
            + $"目标节点：{(targetNode?.ToString() ?? "无")}\n"
            + $"兽笛配置：{string.Join("、", config.FlutePetNames)}\n"
            + $"准备检查：{PreparationCheck}\n"
            + $"兽笛操作：{PartyActionStatus}\n"
            + $"战斗操作：{BattleActionStatus}\n"
            + $"商店操作：{GetShopOperationStatus()}\n"
            + $"总流程：{FullFlowStatus}";
    }

    public string GetDebugDataText()
    {
        RefreshSnapshot();
        var text = GetStatusText()
            + $"\n可见Addon：{(LastSnapshot.VisibleAddons.Count == 0 ? "无" : string.Join(", ", LastSnapshot.VisibleAddons))}";

        if (LastSnapshot.VisibleAddons.Contains("XBMContentsItemDispose", StringComparer.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildAddonDump("XBMContentsItemDispose");

        if (LastSnapshot.VisibleAddons.Contains("XBMContentsItemShop", StringComparer.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildShopDataDump();

        if (ArenaUiReader.TryFindShopAddon(LastSnapshot.Addons, out var detectedShopName)
            && !string.Equals(detectedShopName, "XBMContentsItemShop", StringComparison.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildAddonDump(detectedShopName);

        if (LastSnapshot.VisibleAddons.Contains("SelectYesno", StringComparer.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildAddonDump("SelectYesno");

        if (LastSnapshot.VisibleAddons.Contains("XBMContentsTreasure", StringComparer.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildAddonDump("XBMContentsTreasure");

        if (LastSnapshot.VisibleAddons.Contains("SelectOk", StringComparer.Ordinal))
            text += "\n\n" + ArenaUiReader.BuildAddonDump("SelectOk");

        return text;
    }

    public ArenaController(string diagnosticsDirectory, Dalamud.Plugin.IDalamudPluginInterface pluginInterface, Configuration config)
    {
        DiagnosticsDirectory = diagnosticsDirectory;
        this.config = config;
        reader = new ArenaUiReader();
        exporter = new SnapshotExporter(DiagnosticsDirectory);
        shopCatalogCollector = new ShopCatalogCollector(
            Path.Combine(Path.GetDirectoryName(DiagnosticsDirectory)!, "ArenaShopItems.csv"));
        navigation = new VnavmeshClient(pluginInterface);
        LastSnapshot = reader.Read(Phase);
        Phase = LastSnapshot.Phase;
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        DalamudApi.Framework.Update += OnFrameworkUpdate;
    }

    public void StartFullFlow()
    {
        if (fullFlow || assigningParty || startingBattle)
            return;

        RefreshSnapshot();
        ResetFullFlow(entered: false);
        nextFullFlowActionUtc = DateTime.UtcNow;
        if (currentRoute != null
            && LastSnapshot.CurrentNode is int node
            && LastSnapshot.Phase == ArenaPhase.Board)
        {
            targetNode = node;
            FullFlowStatus = $"已在棋盘节点 {node}，开始处理当前格";
        }
        else
        {
            FullFlowStatus = "已启动：正在识别入口和当前阶段";
        }
    }

    public void ContinueFlow()
    {
        if (fullFlow || assigningParty || startingBattle)
            return;

        RefreshSnapshot();
        if (currentRoute == null)
        {
            FullFlowStatus = "无法继续：当前不是已支持的斗兽场地";
            return;
        }

        var currentNode = LastSnapshot.CurrentNode;
        if (currentNode == null)
        {
            currentNode = ResolveEventNode(LastSnapshot.Phase);
            if (currentNode == null)
            {
                FullFlowStatus = "无法继续：当前不在节点上，且无法按当前界面匹配事件节点";
                return;
            }
        }

        if (LastSnapshot.Phase is not (ArenaPhase.Board or ArenaPhase.PartySetup
            or ArenaPhase.Shop or ArenaPhase.Rest or ArenaPhase.Treasure or ArenaPhase.ItemDispose))
        {
            FullFlowStatus = $"无法继续：当前阶段为 {LastSnapshot.Phase}";
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.PartySetup && HasBlockingArenaAddon(LastSnapshot))
        {
            FullFlowStatus = "无法继续：请先关闭斗兽奇弈解说或其他遮挡窗口";
            return;
        }

        ContinueFromNode = currentNode.Value;
        ResetFullFlow(entered: true);
        nextFullFlowActionUtc = DateTime.UtcNow;
        if (LastSnapshot.Phase == ArenaPhase.Board)
        {
            MarkProcessed(ContinueFromNode);
            targetNode = currentRoute == null ? null : GetPreferredNext(currentRoute, ContinueFromNode);
            FullFlowStatus = targetNode.HasValue
                ? $"已从节点 {ContinueFromNode} 继续，下一格 {targetNode.Value}"
                : $"已从节点 {ContinueFromNode} 继续，等待结算";
        }
        else
        {
            eventNode = ContinueFromNode;
            targetNode = ContinueFromNode;
            FullFlowStatus = $"已按 {ArenaNodeKindLabels.Get(currentRoute!.GetKind(ContinueFromNode))} 界面匹配节点 {ContinueFromNode}，继续处理当前事件";
        }
    }

    private int? ResolveEventNode(ArenaPhase phase)
    {
        if (currentRoute == null)
            return null;

        var expectedKind = phase switch
        {
            ArenaPhase.Shop => ArenaNodeKind.Shop,
            ArenaPhase.Rest => ArenaNodeKind.Rest,
            ArenaPhase.Treasure or ArenaPhase.ItemDispose => ArenaNodeKind.Treasure,
            ArenaPhase.PartySetup => ArenaNodeKind.Battle,
            _ => ArenaNodeKind.Unknown,
        };
        if (expectedKind == ArenaNodeKind.Unknown)
            return null;

        var candidates = currentRoute.Nodes.Where(x =>
            x.Kind == expectedKind
            || (x.Kind == ArenaNodeKind.Random
                && expectedKind is ArenaNodeKind.Battle or ArenaNodeKind.Treasure)).ToArray();
        if (candidates.Length == 0)
            return null;
        if (candidates.Length == 1 || !LastSnapshot.PlayerX.HasValue)
            return candidates[0].Index;

        var x = LastSnapshot.PlayerX.Value;
        var z = LastSnapshot.PlayerZ ?? 0f;
        return candidates
            .OrderBy(node => MathF.Pow(node.Center.X - x, 2) + MathF.Pow(node.Center.Z - z, 2))
            .First().Index;
    }

    public void StartPartyAndBattle()
    {
        startBattleAfterParty = true;
        StartPartyAssignment();
        if (!assigningParty
            && LastSnapshot.Phase == ArenaPhase.PartySetup
            && !PartySetupAction.IsUnassignedFlutePrompt(LastSnapshot)
            && PartySetupAction.IsComplete(ReadPartyMembers(), config))
            StartBattle();
    }

    public void RefreshSnapshot()
    {
        var previous = LastSnapshot.CurrentNode;
        LastSnapshot = reader.Read(Phase);
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        if (LastSnapshot.CurrentNode.HasValue && previous != LastSnapshot.CurrentNode)
            PreviousNode = previous;
        if (Phase is not ArenaPhase.Paused and not ArenaPhase.Failed)
            Phase = LastSnapshot.Phase;
        RouteSuggestion = GetRouteSuggestion(LastSnapshot.CurrentNode, LastSnapshot.CurrentNodeKind);
        DalamudApi.Log.Information("Arena snapshot refreshed. Phase={Phase}, Addon={Addon}.", Phase, LastSnapshot.CurrentAddon);
    }

    public void CheckBattlePreparation()
    {
        RefreshSnapshot();
        if (currentRoute == null)
        {
            PreparationCheck = "检查失败：当前不是已支持的斗兽场地";
            return;
        }

        var hasParty = LastSnapshot.Addons.Any(x => x.Name == "XBMPetParty" && x.IsReady);
        var hasStageDetail = LastSnapshot.Addons.Any(x => x.Name == "XBMStageDetailList" && x.IsReady);
        if (!hasParty || !hasStageDetail)
        {
            PreparationCheck = "检查失败：编队界面或棋盘详情界面尚未准备好";
            return;
        }

        if (!PartySetupAction.TryRead(LastSnapshot, out var members, out var error))
        {
            PreparationCheck = $"检查失败：{error}";
            return;
        }

        var missing = config.FlutePetNames.Where(name => members.All(x => x.Name != name)).ToArray();
        PreparationCheck = missing.Length > 0
            ? $"准备未完成：缺少 {string.Join("、", missing)}"
            : PartySetupAction.IsComplete(members, config)
                ? $"准备检查通过：1、2、3号兽笛分别为{string.Join("、", config.FlutePetNames)}"
                : "所需魔兽齐全，但兽笛顺序尚未设置完成";
    }

    public void StartPartyAssignment()
    {
        if (assigningParty)
            return;

        CheckBattlePreparation();
        if (!PartySetupAction.TryRead(LastSnapshot, out var members, out var error))
        {
            PartyActionStatus = $"无法开始：{error}";
            return;
        }
        var missing = config.FlutePetNames.Where(name => members.All(x => x.Name != name || x.Hp == 0)).ToArray();
        if (missing.Length > 0)
        {
            PartyActionStatus = $"无法开始：缺少存活的 {string.Join("、", missing)}";
            return;
        }
        if (PartySetupAction.IsComplete(members, config))
        {
            PartyActionStatus = "兽笛顺序已经正确，无需操作";
            return;
        }

        assigningParty = true;
        partyActionAttempts = 0;
        partyActionDeadlineUtc = DateTime.UtcNow.AddSeconds(20);
        nextPartyActionUtc = DateTime.UtcNow;
        PartyActionStatus = "正在按顺序设置兽笛";
    }

    public void StartBattle()
    {
        if (startingBattle || assigningParty)
            return;

        RefreshSnapshot();
        if (currentRoute == null)
        {
            BattleActionStatus = "无法开始：当前不是已支持的斗兽场地";
            return;
        }
        if (LastSnapshot.Phase != ArenaPhase.PartySetup)
        {
            BattleActionStatus = $"无法开始：当前不是编队准备界面（{LastSnapshot.PhaseReason}）";
            return;
        }
        if (!PartySetupAction.TryRead(LastSnapshot, out var members, out var error))
        {
            BattleActionStatus = $"无法开始：{error}";
            return;
        }
        if (!PartySetupAction.IsComplete(members, config))
        {
            BattleActionStatus = $"无法开始：请先把 1、2、3 号兽笛设置为{string.Join("、", config.FlutePetNames)}";
            return;
        }
        if (members.Count(x => x.Slot == 0) != 1
            || members.Count(x => x.Slot == 1) != 1
            || members.Count(x => x.Slot == 2) != 1)
        {
            BattleActionStatus = "无法开始：三个兽笛槽位尚未得到唯一确认";
            return;
        }
        if (LastSnapshot.Addons.Any(x => x.Name == "SelectYesno" && x.IsReady))
        {
            BattleActionStatus = "无法开始：确认窗口尚未处理";
            return;
        }
        if (!LastSnapshot.Addons.Any(x => x.Name == "XBMStageDetailList" && x.IsReady))
        {
            BattleActionStatus = "无法开始：棋盘详情界面未就绪";
            return;
        }
        if (!BattleAction.TryStartBattle(out error))
        {
            BattleActionStatus = $"无法开始：{error}";
            return;
        }

        startingBattle = true;
        battleActionDeadlineUtc = DateTime.UtcNow.AddSeconds(10);
        BattleActionStatus = "已发送战斗开始，等待场景确认";
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        _ = framework;
        TickShopCatalogCollection();
        if (startingBattle)
        {
            VerifyBattleStart();
            return;
        }
        if (assigningParty)
        {
            TickPartyAssignment();
            return;
        }
        if (fullFlow)
            TickFullFlow();
    }

    private void TickShopCatalogCollection()
    {
        if (!config.AutoCollectShopItems || DateTime.UtcNow < nextShopCatalogScanUtc)
            return;

        nextShopCatalogScanUtc = DateTime.UtcNow.AddSeconds(1);
        var entries = ArenaUiReader.ReadShopCatalog();
        if (entries.Count == 0)
        {
            shopCatalogScrollSlot = 0;
            shopCatalogAwaitingSlot = -1;
            shopCatalogScanComplete = false;
            return;
        }

        shopCatalogCollector.Capture(entries);
        if (shopCatalogScanComplete)
            return;

        if (shopCatalogAwaitingSlot >= 0)
        {
            shopCatalogScrollSlot = shopCatalogAwaitingSlot + 1;
            shopCatalogAwaitingSlot = -1;
        }

        var scrolled = ShopAction.TryScrollToProduct(shopCatalogScrollSlot, out var productCount);
        if (productCount > 0 && shopCatalogScrollSlot >= productCount)
        {
            shopCatalogScanComplete = true;
            ShopAction.TryScrollToProduct(0, out _);
            return;
        }
        if (!scrolled)
            return;

        shopCatalogAwaitingSlot = shopCatalogScrollSlot;
    }

    private void TickPartyAssignment()
    {
        if (DateTime.UtcNow < nextPartyActionUtc)
            return;
        if (DateTime.UtcNow >= partyActionDeadlineUtc || partyActionAttempts >= 12)
        {
            CancelPartyAssignment("设置兽笛超时，已停止");
            return;
        }

        LastSnapshot = reader.Read(Phase);
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        if (currentRoute == null)
        {
            CancelPartyAssignment("场地已变化，已停止设置兽笛");
            return;
        }
        if (PartySetupAction.IsUnassignedFlutePrompt(LastSnapshot))
        {
            if (!EntryAction.TryRejectChallenge(out var rejectError))
            {
                CancelPartyAssignment($"取消未设置兽笛提示失败：{rejectError}");
                return;
            }
            PartyActionStatus = "已取消未设置兽笛提示，等待编队界面恢复";
            nextPartyActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }
        if (!PartySetupAction.TryRead(LastSnapshot, out var members, out var error))
        {
            CancelPartyAssignment($"读取失败：{error}");
            return;
        }
        if (PartySetupAction.IsComplete(members, config))
        {
            assigningParty = false;
            PartyActionStatus = $"设置完成：1号{config.FlutePetNames[0]}、2号{config.FlutePetNames[1]}、3号{config.FlutePetNames[2]}";
            PreparationCheck = $"准备检查通过：1、2、3号兽笛分别为{string.Join("、", config.FlutePetNames)}";
            if (startBattleAfterParty)
            {
                startBattleAfterParty = false;
                StartBattle();
            }
            return;
        }

        var next = PartySetupAction.NextToAssign(members, config);
        if (next == null || !PartySetupAction.TryClickRow(next.Row, out error))
        {
            CancelPartyAssignment(next == null ? "无法确定下一只魔兽" : error);
            return;
        }

        partyActionAttempts++;
        PartyActionStatus = $"已操作 {next.Name}，等待界面确认";
        nextPartyActionUtc = DateTime.UtcNow.AddMilliseconds(750);
    }

    private void TickFullFlow()
    {
        if (DateTime.UtcNow < nextFullFlowActionUtc)
            return;
        if (DateTime.UtcNow >= fullFlowDeadlineUtc)
        {
            StopFullFlow("总流程超时，已停止");
            return;
        }

        LastSnapshot = reader.Read(Phase);
        Phase = LastSnapshot.Phase;
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        RouteSuggestion = GetRouteSuggestion(LastSnapshot.CurrentNode, LastSnapshot.CurrentNodeKind);

        if (waitingForAreaExit)
        {
            if (currentRoute == null)
            {
                waitingForAreaExit = false;
                FullFlowStatus = "已离开副本区域，准备开始新一轮";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(2);
                return;
            }
            FullFlowStatus = "等待离开副本区域";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (wasInResult && LastSnapshot.Phase != ArenaPhase.Result)
        {
            wasInResult = false;
            resultSeenAtUtc = DateTime.MinValue;
            ResetFullFlow(entered: false);
            waitingForAreaExit = true;
            FullFlowStatus = "结算已关闭，等待离开副本区域";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        var promptText = PromptAction.ReadText(LastSnapshot);
        if (PromptAction.TryHandle(LastSnapshot, out var promptStatus))
        {
            var compactPrompt = string.Concat((promptText ?? string.Empty).Where(x => !char.IsWhiteSpace(x)));
            if (compactPrompt.Contains("只让玩家休息", StringComparison.Ordinal)
                && compactPrompt.Contains("恢复90%体力", StringComparison.Ordinal))
                restConfirmed = true;
            if (PartySetupAction.IsUnderfilledChallengePrompt(LastSnapshot)
                || PromptAction.ReadText(LastSnapshot)?.Contains("魔兽未满") == true)
                challengeConfirmed = true;
            FullFlowStatus = promptStatus;
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (shopPendingAction != ShopPendingAction.None
            && DateTime.UtcNow - shopPendingSinceUtc > TimeSpan.FromSeconds(20))
        {
            var action = shopPendingAction == ShopPendingAction.Buy ? "购买" : "出售";
            StopFullFlow($"商店{action}确认超时：{CrucibleItemCatalog.GetName(pendingShopItemId)}，槽位 {pendingShopSlot}，已等待20秒");
            return;
        }

        if (LastSnapshot.Phase != ArenaPhase.Loot
            && TreasureAction.IsCapacityPopup(LastSnapshot))
        {
            if (!TreasureAction.TryDismissPopup(out var capacityError))
            {
                FullFlowStatus = capacityError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            treasureTried = false;
            FullFlowStatus = "已确认无法全部获取，等待选择单件奖励";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.Loot
            && LastSnapshot.VisibleAddons.Contains("SelectOk", StringComparer.Ordinal))
        {
            if (!TreasureAction.TryDismissPopup(out var rewardCapacityError))
            {
                FullFlowStatus = rewardCapacityError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            rewardTakeAllRejected = true;
            rewardSingleRequested = false;
            FullFlowStatus = "战利品栏已满，正在改为选择单件奖励";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.VisibleAddons.Any(x => x.Contains("ContentsFinder", StringComparison.Ordinal))
            || AddonUi.HasReadyByNameContains("ContentsFinder"))
        {
            if (EntryAction.TryCommenceEntry(out _))
            {
                FullFlowStatus = "已点击出发，等待进入第一盘区域";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
        }

        if (LastSnapshot.Phase == ArenaPhase.Result)
        {
            wasInResult = true;
            navigation.Stop();
            navigationStarted = false;
            if (resultSeenAtUtc == DateTime.MinValue)
                resultSeenAtUtc = DateTime.UtcNow;

            var elapsed = DateTime.UtcNow - resultSeenAtUtc;
            if (resultNextPageAtUtc == DateTime.MinValue)
            {
                if (elapsed < TimeSpan.FromSeconds(5))
                {
                    FullFlowStatus = $"等待结算界面稳定（{5 - (int)elapsed.TotalSeconds}秒）";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }

                if (!ResultAction.TryNextPage(out var nextPageError))
                {
                    FullFlowStatus = nextPageError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }

                resultNextPageAtUtc = DateTime.UtcNow;
                FullFlowStatus = "已点击结算下一页，等待3秒";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            var afterNextPage = DateTime.UtcNow - resultNextPageAtUtc;
            if (afterNextPage < TimeSpan.FromSeconds(3))
            {
                FullFlowStatus = $"已点击结算下一页，等待关闭（{3 - (int)afterNextPage.TotalSeconds}秒）";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!ResultAction.TryClose(out var resultError))
            {
                FullFlowStatus = resultError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!LastSnapshot.Addons.Any(x => x.Name == "XBMResult" && x.IsReady))
            {
                FullFlowStatus = "结算已关闭，等待离开副本区域";
                resultSeenAtUtc = DateTime.MinValue;
                resultNextPageAtUtc = DateTime.MinValue;
                ResetFullFlow(entered: false);
                waitingForAreaExit = true;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            FullFlowStatus = "正在关闭斗兽结算界面";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.Loot)
        {
            navigation.Stop();
            navigationStarted = false;
            battleObserved = true;
            if (rewardTakeAllRejected)
            {
                if (!rewardSingleRequested && !RewardAction.TryTakeFirst(out var singleRewardError))
                {
                    FullFlowStatus = $"选择单件战利品失败：{singleRewardError}";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                rewardSingleRequested = true;
                rewardRequested = true;
                FullFlowStatus = "已选择第一件战利品，等待确认或替换道具";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            if (!rewardRequested && !RewardAction.TryTakeAll(out var rewardError))
            {
                FullFlowStatus = $"领取奖励失败：{rewardError}";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            rewardRequested = true;
            FullFlowStatus = "正在领取战斗奖励";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.ItemDispose)
        {
            navigation.Stop();
            navigationStarted = false;

            if (!ItemDisposeAction.TryRead(LastSnapshot, out var targetItemId, out var isPurchase,
                    out var disposeItems, out var disposeError))
            {
                FullFlowStatus = disposeError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (isPurchase)
            {
                FullFlowStatus = "商店购买意外进入直接替换界面，等待返回商店后先出售旧道具";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!itemDisposeTried)
            {
                var replacement = FindFirstUnprotectedSlot(
                    disposeItems.Select(x => (x.Slot, x.ItemId)), config.ProtectedItemIds);
                if (replacement == null)
                {
                    StopFullFlow($"宝箱奖励 {CrucibleItemCatalog.GetName(targetItemId)} 无可替换道具，已停止");
                    return;
                }
                if (!ItemDisposeAction.TrySelectSlot(replacement.Value.Slot, out var replaceError))
                {
                    FullFlowStatus = replaceError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                itemDisposeTried = true;
                treasureTried = true;
                FullFlowStatus = $"正在用 {CrucibleItemCatalog.GetName(targetItemId)} 替换 {CrucibleItemCatalog.GetName(replacement.Value.ItemId)}";
            }
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        // A board bridge has no tile index. Keep its navigation alive before
        // the HUD-only battle-preparation fallback can stop movement.
        if (currentRoute != null
            && LastSnapshot.Phase is ArenaPhase.Board or ArenaPhase.Loading
            && LastSnapshot.CurrentNode == null
            && targetNode.HasValue
            && LastSnapshot.PlayerX.HasValue
            && currentRoute.IsOnBoard(new System.Numerics.Vector3(
                LastSnapshot.PlayerX.Value,
                LastSnapshot.PlayerY ?? 0f,
                LastSnapshot.PlayerZ ?? 0f)))
        {
            MoveTo(targetNode.Value, $"正在沿棋盘连接桥前往节点 {targetNode.Value}");
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.Treasure)
        {
            navigation.Stop();
            navigationStarted = false;

            if (LastSnapshot.VisibleAddons.Contains("SelectOk", StringComparer.Ordinal))
            {
                if (TreasureAction.TryDismissPopup(out var popupError))
                    FullFlowStatus = "已关闭宝物重复提示";
                else
                    FullFlowStatus = popupError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!treasureTried)
            {
                if (TreasureAction.TryTakePreferred(LastSnapshot,
                        config.EquipmentPurchasePriority,
                        config.ShopPurchasePriority,
                        out var selectedTreasureName,
                        out _))
                {
                    treasureTried = true;
                    FullFlowStatus = $"正在领取宝箱奖励：{selectedTreasureName}";
                }
                else if (TreasureAction.TryExit(out var treasureError))
                {
                    treasureTried = true;
                    FullFlowStatus = "正在离开宝箱";
                }
                else
                {
                    FullFlowStatus = treasureError;
                }
            }
            RememberEventNode();
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.Shop)
        {
            navigation.Stop();
            navigationStarted = false;
            if (config.SkipShop)
            {
                if (!shopTried && !ShopAction.TryExit(out var shopError))
                {
                    FullFlowStatus = shopError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                shopTried = true;
                RememberEventNode();
                FullFlowStatus = "跳过商店，正在离开";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (config.AutoCollectShopItems && !shopCatalogScanComplete)
            {
                FullFlowStatus = "正在滚动采集全部商店商品，完成后开始自动购买";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!ShopAction.TryRead(LastSnapshot, out var shop, out var readShopError))
            {
                FullFlowStatus = readShopError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (shopPendingAction == ShopPendingAction.Buy)
            {
                var purchased = shop.Products.FirstOrDefault(x => x.Slot == pendingShopSlot)?.Purchased == true;
                var inventoryHasItem = shop.Items.Any(x => x.ItemId == pendingShopItemId)
                    || shop.Equipment.Any(x => x.ItemId == pendingShopItemId);
                if (purchased && shop.Gold < pendingShopGold && inventoryHasItem)
                {
                    var purchasedItemId = pendingShopItemId;
                    var spent = pendingShopGold - shop.Gold;
                    shopPendingAction = ShopPendingAction.None;
                    shopPurchaseCount++;
                    var category = CrucibleItemCatalog.GetCategory(purchasedItemId);
                    FullFlowStatus = $"已购买{category} {CrucibleItemCatalog.GetName(purchasedItemId)}，花费 {spent} 斗兽币";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                else
                {
                    FullFlowStatus = $"等待确认购买 {CrucibleItemCatalog.GetName(pendingShopItemId)}";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
            }
            else if (shopPendingAction == ShopPendingAction.Sell)
            {
                var remainingCount = shop.Items.Count(x => x.ItemId == pendingShopItemId);
                if (remainingCount < pendingShopItemCount && shop.Gold > pendingShopGold)
                {
                    var soldItemId = pendingShopItemId;
                    var earned = shop.Gold - pendingShopGold;
                    shopPendingAction = ShopPendingAction.None;
                    FullFlowStatus = $"已出售奇弈道具 {CrucibleItemCatalog.GetName(soldItemId)}，获得 {earned} 斗兽币";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                else
                {
                    FullFlowStatus = $"等待确认出售 {CrucibleItemCatalog.GetName(pendingShopItemId)}";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
            }

            if (shopStage == ShopStage.SellingItems)
            {
                var sell = FindShopSellTarget(shop.Items);
                if (sell != null)
                {
                    if (!ShopAction.TrySellItem(sell.Value.Slot, out var sellError))
                    {
                        FullFlowStatus = sellError;
                        nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                        return;
                    }
                    shopPendingAction = ShopPendingAction.Sell;
                    pendingShopItemId = sell.Value.ItemId;
                    pendingShopSlot = sell.Value.Slot;
                    pendingShopGold = shop.Gold;
                    pendingShopItemCount = shop.Items.Count(x => x.ItemId == sell.Value.ItemId);
                    shopPendingSinceUtc = DateTime.UtcNow;
                    FullFlowStatus = $"正在出售未保护道具 {CrucibleItemCatalog.GetName(sell.Value.ItemId)}";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }

                shopStage = ShopStage.BuyingEquipment;
                FullFlowStatus = "未保护道具已全部出售，开始购买斗兽装备";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (shopStage == ShopStage.BuyingEquipment)
            {
                var equipmentTarget = FindEquipmentPurchaseTarget(shop);
                if (equipmentTarget != null
                    && shop.Equipment.Count < shop.EquipmentCapacity)
                {
                    if (!TryStartShopPurchase(equipmentTarget, shop.Gold, out var equipmentBuyError))
                    {
                        FullFlowStatus = equipmentBuyError;
                        nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                        return;
                    }
                    FullFlowStatus = $"正在购买斗兽装备 {CrucibleItemCatalog.GetName(equipmentTarget.ItemId)}";
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }

                shopStage = ShopStage.BuyingItems;
                FullFlowStatus = shop.Equipment.Count >= shop.EquipmentCapacity
                    ? "斗兽装备栏已满，开始购买奇弈道具"
                    : "没有找到优先购买且余额足够的斗兽装备，开始购买奇弈道具";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            var targetProduct = FindPurchaseTarget(shop);
            if (targetProduct != null
                && shop.Items.Count < shop.ItemCapacity)
            {
                if (!TryStartShopPurchase(targetProduct, shop.Gold, out var buyError))
                {
                    FullFlowStatus = buyError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                FullFlowStatus = $"正在购买奇弈道具 {CrucibleItemCatalog.GetName(targetProduct.ItemId)}";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!shopTried && !ShopAction.TryExit(out var exitError))
            {
                FullFlowStatus = exitError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            shopTried = true;
            RememberEventNode();
            FullFlowStatus = shop.Items.Count >= shop.ItemCapacity && targetProduct != null
                    ? "奇弈道具栏已满，正在离开商店"
                    : "商店购买完成，正在离开";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (LastSnapshot.Phase == ArenaPhase.Rest || RestAction.IsRestUi(LastSnapshot))
        {
            navigation.Stop();
            navigationStarted = false;
            if (config.SkipRest)
            {
                if (!restLeaveSent && !RestAction.TryLeave(out var restError))
                {
                    FullFlowStatus = restError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                restLeaveSent = true;
                RememberEventNode();
                FullFlowStatus = "跳过休息，正在离开帐篷";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!restTried)
            {
                if (!RestAction.TryRest(out var restError))
                {
                    FullFlowStatus = restError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                restTried = true;
                RememberEventNode();
                FullFlowStatus = "已点击休息，等待确认";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!restConfirmed)
            {
                FullFlowStatus = "等待确认只让玩家休息";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            if (!restLeaveSent && !RestAction.TryLeave(out var leaveError))
            {
                FullFlowStatus = leaveError;
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            restLeaveSent = true;
            FullFlowStatus = "休息完成，正在离开帐篷";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        // Immediately after entering the arena the player can be between node 0 and node 1.
        // The HUD alone resembles battle preparation, but no node event has started yet.
        if (currentRoute != null
            && currentRoute.IsConfigured
            && !LastSnapshot.CurrentNode.HasValue
            && eventNode == null
            && targetNode == null
            && !battleObserved)
        {
            var start = currentRoute.Nodes.FirstOrDefault(n => n.Kind == ArenaNodeKind.Start);
            var first = start != null && start.Next.Count > 0 ? start.Next[0] : 1;
            MoveTo(first, $"已进入{currentRoute.Name}，正在前往节点 {first}");
            return;
        }

        if (LastSnapshot.Phase is ArenaPhase.BattlePreparation or ArenaPhase.Battle)
        {
            navigation.Stop();
            navigationStarted = false;
            RememberEventNode();

            if (config.AutoTargetBoss && !assigningParty)
            {
                if (BossAction.TryTargetBoss(out var targetError))
                    FullFlowStatus = "已自动选中BOSS";
                else
                    FullFlowStatus = $"自动选中BOSS：{targetError}";
            }

            if (LastSnapshot.Phase == ArenaPhase.Battle)
            {
                battleObserved = true;
                FullFlowStatus = "战斗已开始，战斗过程交给战斗插件";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(2);
                return;
            }
            if (LastSnapshot.Phase == ArenaPhase.BattlePreparation)
            {
                if (battleStartedAtUtc == DateTime.MinValue)
                    battleStartedAtUtc = DateTime.UtcNow;

                if (config.AutoApproach && !assigningParty)
                {
                    var player = DalamudApi.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        var dist = BossAction.GetDistanceToBoss(player);
                        if (!dist.HasValue)
                        {
                            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(3);
                            return;
                        }
                        if (dist.Value > 20f)
                        {
                            FullFlowStatus = $"正在接近BOSS（{dist.Value:F1}米）";
                            navigation.MoveTo(BossAction.GetClosePosition(player));
                            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                            return;
                        }
                        if (!countdownSent && DateTime.UtcNow - battleStartedAtUtc >= TimeSpan.FromSeconds(10))
                        {
                            if (!BattleAction.TryCountdown(out var countdownError))
                                FullFlowStatus = $"倒计时未发送：{countdownError}";
                            else
                                FullFlowStatus = "已发送10秒倒计时";
                            countdownSent = true;
                            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                            return;
                        }
                    }
                }
                FullFlowStatus = "战斗场景已准备，等待正式进入战斗";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
        }

        if (LastSnapshot.Phase == ArenaPhase.PartySetup)
        {
            navigation.Stop();
            navigationStarted = false;
            RememberEventNode();
            if (!challengeSent && currentRoute == null)
            {
                if (!EntryAction.TryAdvanceEntry(out var challengeError, config.TargetStage))
                {
                    FullFlowStatus = challengeError;
                    nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                    return;
                }
                challengeSent = true;
                FullFlowStatus = "已点击挑战此奇盘，等待编队界面确认";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }

            FullFlowStatus = "正在设置 1、2、3 号兽笛";
            StartPartyAndBattle();
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (currentRoute == null)
        {
            if (challengeSent)
            {
                FullFlowStatus = "挑战已确认，等待切换到斗兽区域";
                nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
                return;
            }
            if (!EntryAction.TryAdvanceEntry(out var error, config.TargetStage))
                FullFlowStatus = error;
            else
            {
                challengeSent = LastSnapshot.VisibleAddons.Contains("XBMStageDetailList", StringComparer.Ordinal)
                    && LastSnapshot.VisibleAddons.Contains("XBMPetParty", StringComparer.Ordinal);
                FullFlowStatus = challengeSent
                    ? "已点击挑战此奇盘，等待确认或出发窗口"
                    : "已发送入口操作，等待下一阶段确认";
            }
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        TickBoard();
    }

    private void TickBoard()
    {
        var route = currentRoute;
        if (route == null)
            return;

        CompleteCurrentEventIfReturned();

        var current = LastSnapshot.CurrentNode;
        if (LastSnapshot.Phase != ArenaPhase.Board && LastSnapshot.Phase != ArenaPhase.Loading)
        {
            FullFlowStatus = LastSnapshot.PhaseReason;
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        if (!current.HasValue)
        {
            var start = route.Nodes.FirstOrDefault(n => n.Kind == ArenaNodeKind.Start);
            var fallback = targetNode ?? (start != null && start.Next.Count > 0 ? start.Next[0] : 1);
            MoveTo(fallback, $"正在前往节点 {fallback}");
            return;
        }

        if (!processedNodes.Contains(current.Value))
        {
            eventNode = current;
            var kind = route.GetKind(current);
            if (kind == ArenaNodeKind.Start)
            {
                MarkProcessed(current.Value);
            }
            else if (kind is ArenaNodeKind.Battle or ArenaNodeKind.Boss)
            {
                if (battleObserved && rewardRequested)
                {
                    MarkProcessed(current.Value);
                }
                else
                {
                    WaitForNodeEvent(current.Value, kind);
                    return;
                }
            }
            else
            {
                WaitForNodeEvent(current.Value, kind);
                return;
            }
        }

        var next = GetPreferredNext(route, current.Value);
        if (!next.HasValue)
        {
            FullFlowStatus = "已到达 Boss 格，等待结算";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
            return;
        }

        targetNode = next;
        ResetNodeActionFlags();
        MoveTo(next.Value, $"节点 {current.Value} 已完成，正在前往节点 {next.Value}（{route.GetKind(next)}）");
    }

    private void WaitForNodeEvent(int node, ArenaNodeKind kind)
    {
        if (arrivedAtUtc == DateTime.MinValue)
            arrivedAtUtc = DateTime.UtcNow;

        if (DateTime.UtcNow - arrivedAtUtc > NodeEventTimeout)
        {
            MarkProcessed(node);
            FullFlowStatus = $"节点 {node}（{kind}）未出现事件，按已完成处理";
            nextFullFlowActionUtc = DateTime.UtcNow;
            return;
        }

        navigation.Stop();
        navigationStarted = false;
        FullFlowStatus = $"已到达节点 {node}（{kind}），等待事件";
        nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
    }

    private void MoveTo(int node, string status)
    {
        var route = currentRoute;
        if (route == null)
        {
            StopFullFlow("当前路线不可用，无法移动");
            return;
        }

        if (!navigation.IsReady)
        {
            FullFlowStatus = "等待 vnavmesh 导航网格准备好";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(2);
            return;
        }

        targetNode = node;
        if (navigationStarted
            && !navigation.IsRunning
            && DateTime.UtcNow - navigationStartedAtUtc > TimeSpan.FromSeconds(1))
        {
            navigationStarted = false;
            navigationAttempts++;
            if (navigationAttempts >= 3)
            {
                StopFullFlow($"导航连续 3 次提前结束，尚未到达节点 {node}");
                return;
            }
            FullFlowStatus = $"导航提前结束，正在重新前往节点 {node} ({navigationAttempts}/3)";
        }
        if (navigationStarted && DateTime.UtcNow - navigationStartedAtUtc > TimeSpan.FromSeconds(12))
        {
            navigationStarted = false;
            navigationAttempts++;
        }

        if (!navigationStarted && !navigation.MoveTo(route.GetCenter(node)))
        {
            navigationAttempts++;
            if (navigationAttempts >= 3)
            {
                StopFullFlow($"连续 3 次无法前往节点 {node}，请检查 vnavmesh 导航网格");
                return;
            }
            FullFlowStatus = $"前往节点 {node} 未启动，正在重试 ({navigationAttempts}/3)";
            nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(2);
            return;
        }

        if (!navigationStarted)
        {
            navigationStarted = true;
            navigationStartedAtUtc = DateTime.UtcNow;
            arrivedAtUtc = DateTime.MinValue;
        }

        FullFlowStatus = status;
        nextFullFlowActionUtc = DateTime.UtcNow.AddSeconds(1);
    }

    private void RememberEventNode()
    {
        eventNode = LastSnapshot.CurrentNode ?? targetNode ?? eventNode;
    }

    private void CompleteCurrentEventIfReturned()
    {
        if (LastSnapshot.Phase != ArenaPhase.Board || !LastSnapshot.CurrentNode.HasValue)
            return;

        var node = LastSnapshot.CurrentNode.Value;
        if (eventNode == node || targetNode == node)
        {
            var kind = currentRoute?.GetKind(node) ?? ArenaNodeKind.Unknown;
            if (kind is ArenaNodeKind.Battle or ArenaNodeKind.Boss)
            {
                if (battleObserved)
                    MarkProcessed(node);
            }
            else if (kind == ArenaNodeKind.Random)
            {
                if (battleObserved || treasureTried)
                    MarkProcessed(node);
            }
            else if (kind is ArenaNodeKind.Rest or ArenaNodeKind.Treasure or ArenaNodeKind.Shop)
            {
                if (restLeaveSent || treasureTried || shopTried
                    || (arrivedAtUtc != DateTime.MinValue && DateTime.UtcNow - arrivedAtUtc > NodeEventTimeout))
                    MarkProcessed(node);
            }
        }
    }

    private void MarkProcessed(int node)
    {
        processedNodes.Add(node);
        if (eventNode == node)
            eventNode = null;
        if (targetNode == node)
            targetNode = currentRoute == null ? null : GetPreferredNext(currentRoute, node);
        navigationStarted = false;
        navigationAttempts = 0;
        arrivedAtUtc = DateTime.MinValue;
        ResetNodeActionFlags();
    }

    private ShopProduct? FindPurchaseTarget(ShopSnapshot shop)
    {
        foreach (var itemId in config.ShopPurchasePriority)
        {
            var product = shop.Products.FirstOrDefault(x =>
                x.ItemId == itemId && !x.Purchased && x.Price <= shop.Gold);
            if (product != null)
                return product;
        }
        return null;
    }

    private ShopProduct? FindEquipmentPurchaseTarget(ShopSnapshot shop)
    {
        var hasElementalAxe = CrucibleItemCatalog.HasElementalAxe(
            shop.Equipment.Select(x => x.ItemId));
        foreach (var itemId in config.EquipmentPurchasePriority)
        {
            if (hasElementalAxe && CrucibleItemCatalog.IsElementalAxe(itemId))
                continue;
            var product = shop.Products.FirstOrDefault(x =>
                x.ItemId == itemId
                && !x.Purchased
                && x.Price <= shop.Gold
                && !shop.Equipment.Any(equipment => equipment.ItemId == itemId));
            if (product != null)
                return product;
        }
        return null;
    }

    private (int Slot, uint ItemId)? FindShopSellTarget(IReadOnlyList<ShopItemSlot> items)
    {
        var first = items
            .Where(x => !config.ProtectedItemIds.Contains(x.ItemId))
            .OrderBy(x => x.Slot)
            .FirstOrDefault();
        return first == null ? null : (first.Slot, first.ItemId);
    }

    private bool TryStartShopPurchase(ShopProduct product, int gold, out string error)
    {
        if (!ShopAction.TryBuy(product.Slot, out error))
            return false;
        shopPendingAction = ShopPendingAction.Buy;
        pendingShopItemId = product.ItemId;
        pendingShopSlot = product.Slot;
        pendingShopGold = gold;
        shopPendingSinceUtc = DateTime.UtcNow;
        return true;
    }

    private string GetShopOperationStatus()
        => shopPendingAction switch
        {
            ShopPendingAction.Buy => $"等待购买 {CrucibleItemCatalog.GetName(pendingShopItemId)}（商品槽位 {pendingShopSlot}）",
            ShopPendingAction.Sell => $"等待出售 {CrucibleItemCatalog.GetName(pendingShopItemId)}（道具槽位 {pendingShopSlot}）",
            _ => "无",
        };

    private static (int Slot, uint ItemId)? FindFirstUnprotectedSlot(
        IEnumerable<(int Slot, uint ItemId)> slots,
        IReadOnlyCollection<uint> protectedItems)
    {
        var first = slots
            .Where(x => !protectedItems.Contains(x.ItemId))
            .OrderBy(x => x.Slot)
            .FirstOrDefault();
        return first.ItemId == 0 ? null : first;
    }

    private void ResetNodeActionFlags()
    {
        countdownSent = false;
        battleObserved = false;
        rewardRequested = false;
        rewardTakeAllRejected = false;
        rewardSingleRequested = false;
        restLeaveSent = false;
        restTried = false;
        restConfirmed = false;
        treasureTried = false;
        shopTried = false;
        shopStage = ShopStage.SellingItems;
        shopPendingAction = ShopPendingAction.None;
        pendingShopItemId = 0;
        pendingShopSlot = -1;
        pendingShopGold = 0;
        pendingShopItemCount = 0;
        shopPendingSinceUtc = DateTime.MinValue;
        shopPurchaseCount = 0;
        itemDisposeTried = false;
    }

    private void ResetFullFlow(bool entered)
    {
        fullFlow = true;
        processedNodes.Clear();
        countdownSent = false;
        battleStartedAtUtc = DateTime.MinValue;
        challengeSent = entered;
        challengeConfirmed = entered;
        battleObserved = false;
        rewardRequested = false;
        rewardTakeAllRejected = false;
        rewardSingleRequested = false;
        resultSeenAtUtc = DateTime.MinValue;
        resultNextPageAtUtc = DateTime.MinValue;
        restLeaveSent = false;
        restTried = false;
        restConfirmed = false;
        treasureTried = false;
        shopTried = false;
        shopStage = ShopStage.SellingItems;
        shopPendingAction = ShopPendingAction.None;
        pendingShopItemId = 0;
        pendingShopSlot = -1;
        pendingShopGold = 0;
        pendingShopItemCount = 0;
        shopPendingSinceUtc = DateTime.MinValue;
        shopPurchaseCount = 0;
        itemDisposeTried = false;
        navigationStarted = false;
        navigationStartedAtUtc = DateTime.MinValue;
        arrivedAtUtc = DateTime.MinValue;
        navigationAttempts = 0;
        targetNode = null;
        eventNode = null;
        waitingForAreaExit = false;
        wasInResult = false;
        fullFlowDeadlineUtc = DateTime.UtcNow.Add(FullFlowTimeout);
    }

    private void StopFullFlow(string reason)
    {
        fullFlow = false;
        navigation.Stop();
        FullFlowStatus = reason;
        if (Phase == ArenaPhase.Completed)
            DalamudApi.Log.Information("Arena full flow finished: {Reason}.", reason);
        else
            DalamudApi.Log.Warning("Arena full flow stopped: {Reason}.", reason);
    }

    private static bool HasBlockingArenaAddon(ArenaSnapshot snapshot)
        => snapshot.VisibleAddons.Any(name =>
            (name.StartsWith("XBM", StringComparison.Ordinal)
                || name.Contains("Contents", StringComparison.Ordinal))
            && name is not "XBMStageList"
            && name is not "XBMStageDetailList"
            && name is not "XBMPetParty"
            && name is not "XBMContentsMainHUD"
            && name is not "XBMContentsBooty"
            && name is not "XBMContentsTreasure"
            && name is not "XBMContentsItemShop"
            && name is not "XBMContentsItemDispose"
            && name is not "XBMResult");

    private void VerifyBattleStart()
    {
        if (DateTime.UtcNow >= battleActionDeadlineUtc)
        {
            startingBattle = false;
            BattleActionStatus = "战斗开始未确认，已停止";
            return;
        }

        LastSnapshot = reader.Read(Phase);
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        if (LastSnapshot.Phase is ArenaPhase.BattlePreparation or ArenaPhase.Battle)
        {
            startingBattle = false;
            Phase = LastSnapshot.Phase;
            BattleActionStatus = LastSnapshot.Phase == ArenaPhase.Battle
                ? "战斗已开始"
                : "已进入战斗场景，等待战斗状态";
            return;
        }
        if (LastSnapshot.Phase is ArenaPhase.Unknown or ArenaPhase.Failed)
        {
            startingBattle = false;
            BattleActionStatus = "战斗开始后状态异常，已停止";
        }
    }

    private void CancelPartyAssignment(string reason)
    {
        assigningParty = false;
        startBattleAfterParty = false;
        PartyActionStatus = reason;
        DalamudApi.Log.Warning("Arena party assignment stopped: {Reason}.", reason);
    }

    private string GetRouteSuggestion(int? node, ArenaNodeKind kind)
    {
        var next = currentRoute != null && node.HasValue
            ? GetPreferredNext(currentRoute, node.Value)
            : null;
        return kind switch
        {
            ArenaNodeKind.Start => "起点：下一步前往下一个节点",
            ArenaNodeKind.Battle when next.HasValue => $"战斗节点：完成后前往节点 {next.Value}",
            ArenaNodeKind.Random when next.HasValue => $"随机节点：战斗或宝箱，完成后前往节点 {next.Value}",
            ArenaNodeKind.Rest when next.HasValue => $"休息节点：离开后前往节点 {next.Value}",
            ArenaNodeKind.Treasure when next.HasValue => $"宝箱节点：处理后前往节点 {next.Value}",
            ArenaNodeKind.Shop when next.HasValue => $"商店节点：离开后前往节点 {next.Value}",
            ArenaNodeKind.Boss => "Boss 节点：完成后等待最终结算",
            _ => "当前节点或路线尚未识别",
        };
    }

    private int? GetPreferredNext(ArenaStageRoute route, int node)
    {
        if (config.CustomRoutes.TryGetValue(route.StageId, out var customRoute))
        {
            var index = customRoute.IndexOf(node);
            if (index >= 0 && index + 1 < customRoute.Count)
            {
                var next = customRoute[index + 1];
                var currentNode = route.Nodes.FirstOrDefault(x => x.Index == node);
                if (currentNode?.Next.Contains(next) == true
                    && route.Nodes.Any(x => x.Index == next))
                    return next;
            }
        }
        return route.GetPreferredNext(node);
    }

    public string ExportSnapshot()
    {
        RefreshSnapshot();
        LastExportPath = exporter.Export(LastSnapshot);
        DalamudApi.Log.Information("Anonymous arena snapshot exported to {Path}.", LastExportPath);
        return LastExportPath;
    }

    public string RecordNodeCoordinate(int nodeIndex, ArenaNodeKind kind)
    {
        RefreshSnapshot();
        if (!LastSnapshot.PlayerX.HasValue)
            return "无法记录：当前位置未知";

        var routeName = currentRoute?.Name ?? $"区域{LastSnapshot.TerritoryId}";
        return $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {routeName}"
            + $" | 节点={nodeIndex}"
            + $" | 类型={ArenaNodeKindLabels.Get(kind)}"
            + $" | X={LastSnapshot.PlayerX:F2}"
            + $" | Y={LastSnapshot.PlayerY ?? 0f:F2}"
            + $" | Z={LastSnapshot.PlayerZ ?? 0f:F2}";
    }

    public string DumpBoardData()
        => ArenaUiReader.BuildBoardDump();

    public void OpenDiagnosticsDirectory()
    {
        Directory.CreateDirectory(DiagnosticsDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = DiagnosticsDirectory,
            UseShellExecute = true,
        });
    }

    public void OpenShopCatalogDirectory()
    {
        var directory = Path.GetDirectoryName(ShopCatalogPath)!;
        Directory.CreateDirectory(directory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = directory,
            UseShellExecute = true,
        });
    }

    public void Stop(string reason)
    {
        startingBattle = false;
        fullFlow = false;
        startBattleAfterParty = false;
        BattleActionStatus = reason;
        CancelPartyAssignment(reason);
        navigation.Stop();
        Phase = ArenaPhase.Idle;
        LastSnapshot = reader.Read(Phase, reason);
        currentRoute = ArenaRoutes.Find(LastSnapshot.TerritoryId, LastSnapshot.ContentId);
        DalamudApi.Log.Information("Arena automation stopped: {Reason}.", reason);
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        Stop("插件卸载");
    }

    private ArenaPartyMember[] ReadPartyMembers()
        => PartySetupAction.TryRead(LastSnapshot, out var members, out _) ? members : [];
}
