# 任务：扩展斗兽奇弈副本层数

## 任务状态

- 状态：待开始
- 优先级：中
- 当前版本：`0.2.2`
- 目标：在保持第一层行为不变的前提下，增加第二层支持，并为后续更多层数预留结构
- 当前范围：第二层固定路线
- 暂不包含：随机路线、复杂资源策略、自动购买策略和多副本并行运行

## 任务背景

当前 ArenaPilot 已经可以执行第一层固定流程，但以下内容仍然写死在第一层实现中：

- Territory ID
- Content ID
- 节点坐标
- 节点类型
- 默认下一节点
- Boss BaseId
- 部分入口和路线判断

如果直接在 `ArenaController` 中增加 `StageId == 2` 分支，后续增加第三层时会产生大量重复逻辑。因此本任务先将路线数据抽象为通用的关卡路线对象，再接入第二层。

## 总体目标

完成后应支持：

```text
入口选择第二层
  -> 进入第二层副本区域
  -> 识别第二层路线
  -> 按第二层固定路线移动
  -> 处理战斗、宝箱、休息和商店
  -> 处理第二层 Boss
  -> 处理结算并离开副本
```

第一层必须继续使用原有路线并通过回归测试。

## 阶段一：采集第二层数据

### 目标

在游戏内确认第二层的真实数据，不根据第一层数据推测第二层。

### 需要采集

- `StageId`
- `TerritoryId`
- `ContentId`
- 入口菜单中的第二层选项文本
- 节点实际编号
- 节点中心坐标
- 节点类型
- 每个节点的所有出口
- 固定路线推荐选择
- 第二层 Boss 的 `BaseId`
- 第二层 Boss 的名称和战斗场景表现
- 第二层结算、奖励、宝箱、休息和商店 Addon

### 每个节点至少记录

```text
节点编号：
X：
Y：
Z：
节点类型：Start / Battle / Treasure / Rest / Shop / Boss
可达节点：
固定选择的下一节点：
节点事件 Addon：
备注：
```

### 数据质量要求

- 每个节点坐标至少确认两次。
- 坐标应尽量接近格子中心。
- 不假设节点编号从0开始或连续递增。
- 不假设第二层和第一层使用相同坐标。
- Boss BaseId 必须通过游戏内对象数据或匿名快照确认。
- 保存匿名快照，不记录角色名、账号信息或授权凭据。

### 阶段验收

- 能确认第二层的 `StageId`、`TerritoryId` 和 `ContentId`。
- 能列出第二层完整节点表。
- 能确认一条可完成的固定路线。
- 能确认第二层 Boss BaseId。
- 能确认第二层是否复用第一层的阶段 Addon。

## 阶段二：抽象通用路线模型

### 目标

让控制器和读取器使用当前路线，而不是直接依赖 `FirstArenaRoute`。

### 建议文件

```text
Runtime/ArenaStageRoute.cs
Runtime/ArenaRoutes.cs
Runtime/FirstArenaRoute.cs
Runtime/SecondArenaRoute.cs
```

### 建议模型

```csharp
public sealed record ArenaStageNode(
    int Index,
    Vector3 Center,
    IReadOnlyList<int> Next,
    ArenaNodeKind Kind);

public sealed class ArenaStageRoute
{
    public int StageId { get; init; }
    public uint TerritoryId { get; init; }
    public uint ContentId { get; init; }
    public float NodeRadius { get; init; } = 1.25f;
    public IReadOnlyList<ArenaStageNode> Nodes { get; init; }
    public IReadOnlyDictionary<int, int> PreferredNext { get; init; }
}
```

### 路线注册表要求

增加统一查找入口：

```csharp
ArenaRoutes.Find(stageId, territoryId, contentId)
```

匹配优先级：

1. `TerritoryId + ContentId + StageId` 精确匹配。
2. 在 `ContentId == 0` 的加载阶段使用 `TerritoryId + StageId`。
3. 无法确认时返回 `null`，不得默认使用第一层路线。

### 阶段验收

- 第一层路线迁移到通用模型后，路线、坐标和节点半径不变。
- 未知层数不会错误使用第一层路线。
- 路线对象可以独立提供节点类型、坐标、出口和默认下一节点。
- 编译成功。

## 阶段三：改造状态读取和流程控制

### ArenaUiReader

将以下第一层写死调用改为当前路线：

```text
FirstArenaRoute.FindNode
FirstArenaRoute.GetKind
FirstArenaRoute.IsOnBoard
```

读取器无法确认当前路线时：

- 不识别节点。
- 不返回第一层默认节点。
- 返回可读的“未识别路线”原因。

### ArenaController

增加本轮路线和当前路线概念：

```csharp
private ArenaStageRoute? currentRoute;
private ArenaStageRoute? runRoute;
```

说明：

- `currentRoute`：根据当前快照实时识别。
- `runRoute`：本轮进入副本时确定，结算和离场阶段继续使用。

将所有以下逻辑改为基于路线对象：

- 区域检查。
- 副本检查。
- 节点坐标。
- 节点类型。
- 下一节点。
- 棋盘范围。
- 结算后离场判断。

### 必须保留的现有保护

- 节点统一识别半径 `1.25` 米。
- `PartySetup` 阶段不得被连接桥导航保护拦截。
- `Result` 阶段停止导航。
- 结算后等待离开副本区域。
- 不识别路线时不得盲目移动。

### 阶段验收

- 第一层完整流程回归通过。
- `PartySetup`、`Battle`、`Loot`、`Treasure`、`Shop`、`Rest`、`Result` 逻辑没有重复实现。
- 未知区域或未知层数会暂停或等待，并显示原因。
- 第二层不会使用第一层坐标。

## 阶段四：接入第二层路线

### 目标

将真实采集的第二层数据写入独立的 `SecondArenaRoute`。

### 实施内容

- 添加第二层 `StageId`。
- 添加第二层 `TerritoryId`。
- 添加第二层 `ContentId`。
- 添加第二层所有节点。
- 添加节点类型。
- 添加所有可达出口。
- 添加固定路线 `PreferredNext`。
- 设置节点识别半径为 `1.25` 米，除非实测证明需要单独配置。

### 分支路线规则

如果第二层存在分支：

- `Next` 保存所有可行出口。
- `PreferredNext` 保存当前自动流程选择。
- 不删除未选择的出口。
- 未来再增加用户路线选择或随机策略。

### 阶段验收

- 能识别第二层所有已采集节点。
- 能从起点走到第二层 Boss。
- 节点到达不会提前停止。
- 节点事件处理完成后能进入正确下一节点。
- 到达 Boss 后不会继续移动到不存在的节点。

## 阶段五：接入第二层 Boss

### 目标

让 Boss 自动选中和自动接近按层使用正确的 BaseId。

### 建议结构

```csharp
IReadOnlyDictionary<int, HashSet<uint>> BossDataIdsByStage
```

第一层保留现有 Boss BaseId，第二层只添加经过实测确认的 BaseId。

### 验收要求

- 第二层能够找到正确 Boss。
- 不会把普通怪物误认成 Boss。
- 自动选中使用正确对象。
- 自动接近距离计算正确。
- Boss 不存在时静默等待或显示可读原因。
- 自动接近开启时倒计时逻辑仍为：距离不超过20米后等待5秒，再发送 `/驯兽师 倒计时 10`。

## 阶段六：入口层数选择

### 第一版方案

先增加简单的目标层数配置：

```text
目标层数：第一层 / 第二层
```

入口操作根据目标层数选择对应菜单项，但进入副本后必须使用实际识别出的 `StageId` 校验。

### 必须避免

- 仅依赖用户选择而不验证实际层数。
- 第二层入口未加载时点击错误选项。
- 无法识别菜单时重复点击。
- 入口阶段误进入第一层默认流程。

### 验收要求

- 用户选择第一层时行为与当前版本一致。
- 用户选择第二层时能正确选择第二层入口。
- 入口菜单加载不完整时等待，不误点击。
- 实际进入层数与目标层数不一致时停止或提示。

## 阶段七：UI 和状态文本

### UI 增加内容

在当前状态区域显示：

```text
版本：v0.2.2
当前层数：第1层 / 第2层 / 未知
路线：第一层固定路线 / 第二层固定路线 / 未加载
```

继续保留四个操作按钮：

```text
开始  继续  停止  状态
```

### 状态复制文本增加

```text
版本：v0.2.2
目标层数：
当前层数：
区域：
副本编号：
棋盘节点：
节点类型：
路线：
```

### 验收要求

- 未知路线时状态文本明确显示未知。
- 复制状态可区分第一层和第二层。
- UI 不显示过期的上一轮路线信息。
- 版本号仍从程序集或项目版本统一读取。

## 阶段八：完整测试

### 第一层回归

- 入口选择第一层。
- 进入第一层区域。
- 识别节点0到12。
- 节点半径为 `1.25` 米。
- `PartySetup` 不被连接桥逻辑拦截。
- 战斗、宝箱、休息和商店处理正常。
- Boss 节点不会提前判定。
- 结算关闭后等待离场。
- 下一轮从入口重新开始。

### 第二层测试

- 入口选择第二层。
- 正确识别 `StageId`、`TerritoryId` 和 `ContentId`。
- 正确匹配第二层路线。
- 不使用第一层坐标。
- 所有节点均能到达。
- 所有节点事件均能处理。
- Boss 自动选中和接近正确。
- 结算和离场正确。

### 异常测试

- 第二层路线数据缺失。
- Territory ID 不匹配。
- Content ID 暂时为0。
- Addon 尚未加载。
- 节点坐标不在棋盘范围。
- vnavmesh 未就绪。
- Boss BaseId 未找到。
- 入口菜单文字变化。
- 中途断线或退出副本。
- 结算界面消失但地图尚未切换。

## 停止条件

遇到以下情况不得继续扩展功能，应先补充数据或修复基础问题：

- 无法确认第二层 Territory ID 或 Content ID。
- 无法确认节点坐标。
- Boss BaseId 只能通过名称猜测。
- 第一层回归流程失败。
- 未知路线会继续使用第一层路线。
- 结算后仍然会执行旧节点导航。
- 状态读取和节点识别出现持续误判。

## 交付物

完成后应包含：

- `Runtime/ArenaStageRoute.cs`
- `Runtime/ArenaRoutes.cs`
- `Runtime/SecondArenaRoute.cs`
- 更新后的 `ArenaController.cs`
- 更新后的 `ArenaUiReader.cs`
- 更新后的 `BossAction.cs`
- 更新后的 `MainWindow.cs`
- 第二层匿名快照或数据记录
- 更新后的 `DESIGN.md`
- 更新后的 `WORKLOG.md`
- Release 编译成功的 `output/ArenaPilot.dll`

## 推荐执行顺序

```text
1. 采集第二层数据
2. 抽象通用路线模型
3. 迁移并回归第一层
4. 改造读取器和控制器
5. 接入第二层路线
6. 接入第二层 Boss
7. 接入入口层数选择
8. 更新 UI 和状态文本
9. 执行完整测试
10. 更新设计文档和工作记录
```

## 完成定义

本任务只有在以下条件全部满足时才算完成：

- 第一层行为没有回归。
- 第二层可以从入口开始并完成一轮。
- 第二层没有使用第一层路线数据。
- 未知路线不会盲目操作。
- Boss、结算、离场和下一轮流程均经过游戏内验证。
- Release 构建成功。
- 设计文档和工作记录已同步。
