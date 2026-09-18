# ArenaPilot 技术规格

## 1. 项目定位

ArenaPilot 是一个独立开发的 Dalamud 插件，目标是自动执行斗兽奇弈流程。

本项目只参考可观察的功能、流程和公开接口，采用独立的数据模型、命名和实现。项目不引用 Pawprint、Beastmaster.Core 或 Omni.Verification 程序集，不包含原插件的授权、签名、密钥、连接配置或专有发布资源。

## 2. 当前目标

首个可用闭环限定为：

1. 选择一个固定关卡。
2. 使用一套固定队伍入场。
3. 读取当前棋盘和可用节点。
4. 按基础路线策略选择节点。
5. 处理普通战斗、休息、宝箱和 Boss。
6. 确认战斗和结算结果。
7. 支持暂停、继续、停止。
8. 遇到未知界面或无法确认的状态时暂停并导出诊断。

首版暂不承诺商店、复杂随机事件、装备优化、连续多轮和完整第三方战斗联动。

## 3. 非目标

- 野外自动捕获。
- 图鉴读取和练级。
- AutoDuty 副本捕获。
- 复制原插件的授权或完整性校验逻辑。
- 绕过任何第三方授权、签名或访问控制。
- 使用其他插件的私有类型、私有字段或反射内部实现。

## 4. 建议架构

```text
ArenaPilot/
  ArenaPilot.csproj
  Plugin.cs
  Configuration.cs
  Services.cs
  ArenaSpec.md
  WORKLOG.md

  Runtime/
    ArenaController.cs
    ArenaState.cs
    ArenaContext.cs
    ArenaSnapshot.cs
    TransitionResult.cs

  Reader/
    ArenaUiReader.cs
    ArenaBoardReader.cs
    ArenaPartyReader.cs
    ArenaBattleReader.cs

  Actions/
    EntryAction.cs
    BoardAction.cs
    BattleAction.cs
    RestAction.cs
    TreasureAction.cs
    RewardAction.cs

  Strategy/
    RoutePlanner.cs
    PartyStrategy.cs
    BattleStrategy.cs
    ResourceStrategy.cs

  Integration/
    VnavmeshClient.cs
    CombatClient.cs
    BossModClient.cs

  UI/
    MainWindow.cs
    SettingsWindow.cs
    DiagnosticsWindow.cs

  Diagnostics/
    ArenaRecorder.cs
    SnapshotExporter.cs
```

## 5. 运行状态机

```text
Idle
  -> Validate
  -> Entry
  -> PartySetup
  -> BoardRead
  -> ChooseNode
  -> ExecuteNode
  -> Battle
  -> VerifyNodeResult
  -> Reward
  -> Repeat
  -> Completed

任意状态 -> Paused
任意运行状态 -> Failed
```

每个状态必须具备：

- 进入时间和最大持续时间。
- 取消、暂停和停止检查。
- 当前界面或游戏状态确认。
- 操作后的结果确认。
- 有限重试次数。
- 可读的失败原因。
- 匿名诊断快照。

禁止通过固定延迟直接假定操作成功。例如点击入场后，必须确认界面、Agent、场景或棋盘状态发生了预期变化。

## 6. 独立中间模型

读取器只负责把游戏状态转换为独立快照，策略不直接访问 Dalamud 或内存结构。

```csharp
public sealed record ArenaSnapshot(
    ArenaPhase Phase,
    int StageId,
    int? CurrentNode,
    IReadOnlyList<ArenaNodeSnapshot> Nodes,
    IReadOnlyList<ArenaMemberSnapshot> Party,
    ArenaResourceSnapshot Resources);
```

实际类型和字段必须根据当前客户端诊断结果确定，不预先假设反编译结果中的索引和偏移永远有效。

## 7. 依赖原则

基础依赖：

- Dalamud API 15。
- FFXIVClientStructs。
- Lumina。
- Dalamud.Bindings.ImGui。

可选公开 IPC：

- vnavmesh：移动和接近目标。
- 战斗插件：技能循环。
- BossMod：躲避或战斗移动。

每项可选依赖都必须有安装、加载、接口能力和运行中断检测。任务停止、暂停、插件卸载或异常时，必须恢复由本插件临时改变的外部状态。

## 8. 诊断优先

第一阶段只读，不自动点击：

- 当前 Addon 名称和可见状态。
- 当前 Agent。
- 当前关卡和阶段。
- 当前节点及其可达节点。
- 队伍成员和状态。
- 当前资源、道具和装备信息。
- 当前战斗阶段。
- 可识别的按钮、回调和操作结果。

快照必须匿名化，不记录账号、角色名、Content ID、机器标识或授权凭据。

## 9. 验收标准

- 插件加载和卸载不残留事件、IPC 或任务。
- 未识别界面和节点时自动暂停。
- 暂停、继续、停止行为可预测。
- UI 操作前后都有状态确认。
- 传送、移动、战斗或切图超时会安全停止。
- 死亡、断线、中途退出和窗口关闭不会继续盲目操作。
- 日志能解释状态转移和路线选择。
- 游戏版本不匹配时拒绝执行危险操作。

## 10. 开发顺序

1. 创建 `net10.0`、`x64` 的 Dalamud 插件骨架。
2. 实现主窗口、配置、日志和安全停止。
3. 实现只读诊断和匿名快照导出。
4. 手动录制一轮完整流程，验证 Addon、Agent 和状态模型。
5. 实现棋盘读取和固定路线选择。
6. 逐个实现入场、节点、战斗和结算动作。
7. 最后增加商店、随机事件、重复运行和外部插件联动。
