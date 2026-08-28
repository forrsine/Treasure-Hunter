# 宝藏猎手客户端架构

## 1. 架构目标

客户端采用“QFramework 玩法核心 + Unity 组件 + 数据源适配”的混合结构，主要解决三个问题：

1. 玩家属性、背包、装备、金币和任务不能同时由 UI、场景脚本和存档各保存一份。
2. 新职业、新物品或新界面不应迫使现有模块互相引用。
3. 游客 JSON 与在线服务端需要复用同一套角色规则和表现流程。

```text
Unity 输入 / 碰撞 / UI / 场景
              ↓ Command / Query
TreasureHunterArchitecture
              ↓
Model ← System → Event
              ↓
Unity 动画 / 音效 / UI / 存档服务
              ↓
GameApiClient
       ↙             ↘
本地 JSON       TCP / Protobuf / SQL Server
```

`TreasureHunterArchitecture` 是既有技术类名，游戏对外名称为《宝藏猎手》。

## 2. QFramework 分层职责

### Model：权威运行时数据

Model 只保存业务状态，不引用 GameObject、Animator、AudioSource 或 UI。

| Model | 职责 |
| --- | --- |
| `PlayerModel` | 角色属性、等级、经验和职业存档引用 |
| `PlayerSkillModel` | 已学习技能、等级、冷却和待选择次数 |
| `DeveloperModeModel` | 高攻、无敌和零冷却临时开关 |
| `InventoryModel` | 24 格背包运行时数据 |
| `EquipmentModel` | 6 个装备槽的当前穿戴 |
| `EconomyModel` | 角色金币 |
| `ShopModel` | 商人引导与角色限购状态 |
| `QuestModel` | 任务状态和目标计数 |

对表现层优先暴露只读接口或不可变快照，例如 `IPlayerStatsReadOnly`、`PlayerStatsSnapshot` 和 `QuestSnapshot`。

### System：可复用业务规则

| System | 主要规则 |
| --- | --- |
| `DeveloperModeSystem` | 临时调试状态和有效攻击查询 |
| `PlayerResourceSystem` | 魔法消耗与恢复 |
| `PlayerCombatSystem` | 伤害、治疗、暴击、闪避、减伤和吸血 |
| `PlayerSkillSystem` | 技能学习、升级、候选和释放校验 |
| `PlayerProgressionSystem` | 经验、等级和八类属性成长 |
| `InventorySystem` | 堆叠、移除、使用、恢复和容量规则 |
| `EquipmentSystem` | 穿戴、卸下、原子交换、属性重算和恢复 |
| `EconomySystem` | 金币增加、扣除和上限 |
| `ShopSystem` | 商品、限购、钱包与背包协作 |
| `QuestSystem` | 接取、计数、完成、领奖和恢复 |

### Command：修改入口

MonoBehaviour 和 UI 不直接写 Model，而是发送 Command。例如：

- `TakePlayerDamageCommand`
- `AddInventoryItemCommand`
- `EquipInventoryItemCommand`
- `PurchaseShopItemCommand`
- `AcceptQuestCommand`
- `ClaimQuestRewardCommand`

Command 让调用方只表达“想做什么”，具体规则由 System 决定。

### Query：只读查询

Query 返回数值、只读集合或快照，避免把 Model 内部可变对象泄漏给 UI。例如：

- `GetPlayerStatsQuery`
- `GetEquippedItemsQuery`
- `GetGoldQuery`
- `GetQuestSnapshotsQuery`
- `GetPlayerProgressSaveDataQuery`

### Event：数据变化通知

System 完成业务后发送 Event，由 UI、音效或存档服务响应。数据层事件不携带场景 GameObject。

典型事件包括 `PlayerStatsChangedEvent`、`InventoryChangedEvent`、`EquipmentChangedEvent`、`GoldChangedEvent`、`QuestProgressChangedEvent` 和 `QuestRewardClaimedEvent`。

## 3. Unity 表现层

MonoBehaviour 负责 Unity 专属行为：输入采样、CharacterController、Collider、物理查询、Animator、粒子、材质、音频、场景加载、Prefab 生命周期和 UI 控件引用。

它们可以发送 Command、Query 和订阅 Event，但不能另外保存一套长期角色数据。

## 4. 玩家组件化运行时

```text
GameplayCharacterSpawner
-> 生成 PlayerRuntime
-> 读取 CharacterDefine
-> 装配职业 Visual Prefab 与 Animator
-> PlayerRuntimeController 初始化子组件
```

| 组件 | 职责 |
| --- | --- |
| `PlayerRuntimeController` | 装配依赖并协调执行顺序 |
| `PlayerMovementComponent` | 移动、奔跑、跳跃、翻滚和体力 |
| `PlayerCombatComponent` | 普攻输入、连击、攻击窗口和伤害请求 |
| `PlayerHealthComponent` | 受击入口、死亡与材质反馈 |
| `PlayerProgressionComponent` | 经验与成长选择入口 |
| `PlayerPresentationComponent` | Animator 参数和职业表现适配 |
| `PlayerRangedAttackComponent` | 法师/弓箭手投射物 |
| `PlayerChargedAttackComponent` | 战士蓄力状态机 |
| `PlayerSkillCastComponent` | 技能输入、延迟命中和特效 |
| `PlayerAudioComponent` | 玩家动作音效 |

职业差异来自 `CharacterDefine.json` 与职业表现 Prefab。通用逻辑不复制到四套玩家脚本中。

## 5. 背包、装备与经济事务

### 背包数据分离

```text
InventoryItemDefinition：物品是什么
InventoryModel：玩家当前有哪些物品
NInventoryItemSave：存档需要保存什么
InventoryPanel：怎样显示
```

ScriptableObject 只保存静态定义；运行时与网络只传稳定 `itemId`、格子下标和数量。

### 装备原子交换

穿戴时先确认背包格和装备定义，再把新装备移出背包，并把旧装备放回原格。只有交换成功后才更新 `EquipmentModel`。

属性结算流程：

```text
重新遍历整套装备
-> 计算新的 EquipmentBonusTotals
-> 旧总量与新总量做差
-> 更新 PlayerModel
```

这样可以避免反复穿脱造成属性漂移。

### 商店事务边界

```text
商品合法性
-> 角色限购
-> 金币余额
-> 背包容量
-> 扣款
-> 入包
-> 失败退款 / 成功事件
```

当前规则运行在 Unity 主线程。未来如果改成异步服务端购买，需要用服务端事务结果替代这段本地原子假设。

## 6. 任务事件流

任务系统不在 UI 中统计击杀：

```text
SlimeCo 正式死亡
-> MonsterQuestProgressReporter
-> RecordMonsterDefeatedCommand
-> QuestSystem 过滤任务目标和状态
-> QuestModel 更新
-> QuestProgressChangedEvent
-> QuestPanel 与自动存档
```

领奖时先把任务切换为已领取，再通过统一金币 Command 发放奖励。这样金币变化立即触发保存时，读取到的任务和金币属于同一份完整状态。

## 7. 持久化与数据源

`CharacterProgressSaveService` 监听成长、背包、装备、金币、商店和任务事件。

- 高频变化使用 1 秒实时防抖。
- 接任务、领奖、死亡和退出使用立即保存。
- 保存请求串行执行，并按保存模式合并优先级。
- 数据源确认成功后再恢复本地权威状态。

```text
GetPlayerProgressSaveDataQuery
-> PlayerProgressSaveData
-> GameApiClient
-> Online: Protobuf -> UserService -> DBService -> SQL 事务
-> Guest: LocalGuestSaveService -> v5 JSON / 临时文件 / 备份
```

保存快照包含等级、经验、属性强化、背包、装备、金币、商店和任务，不包含当前血蓝、Animator 状态和场景引用。

## 8. UI 架构

玩法 UI 由场景级 `GameplayUiRoot.prefab` 持有，玩家 Prefab 不挂载界面。

UI 规则：

1. 通过 Query 读取快照。
2. 通过 Command 请求修改。
3. 通过 Event 刷新显示。
4. 不在 Update 中重建背包、装备、商店或任务列表。
5. 正式运行只使用 Prefab 序列化引用；缺失引用时明确报错。
6. 背包、商店、任务、设置和暂停使用统一的模态互斥与光标恢复逻辑。

## 9. 对象池与资源生命周期

怪物、投射物、技能特效、世界掉落、金币和飘字使用对象池。

对象回收必须清理计时器、协程、拥有者、目标、事件、Collider、Rigidbody、Renderer、粒子、Trail、材质反馈和业务状态。

技能特效由 Addressables 本地异步加载。释放资源句柄前先清空相关池，避免池中对象继续引用已经卸载的资源。

## 10. 网络边界

当前联网层负责注册、登录、四角色槽、角色会话和长期进度持久化，并在服务端执行身份绑定、范围校验和 SQL 事务。

当前不负责玩家位置、多人技能、Boss 权威战斗或房间断线重连。简历和面试中应描述为“账号与角色持久化联调”，不能写成“完成多人联机 ARPG”。

## 11. 扩展约定

- 新增属性：扩展 Model/System，再通过 Query/Event 接 UI。
- 新增职业：增加职业配置和表现 Prefab，不复制玩家逻辑。
- 新增技能：扩展配置和可复用执行流程，不在输入层堆职业分支。
- 新增物品：创建稳定 `itemId` 的定义，并加入数据库和存档白名单。
- 新增装备槽：同步枚举、模型、UI、协议和数据库约束；已有枚举序号不能随意修改。
- 新增任务目标：增加事件适配器和目标解析，不让怪物直接引用任务 UI。
- 新增 UI：放入场景级 Prefab，使用 Query/Event，不在运行时临时搭正式界面。
- Prefab 引用缺失：修复资产本身，不使用 `Find` 或 `new GameObject` 掩盖装配错误。

## 12. 自动验证

`Assets/Editor/Tests` 当前包含 27 个 EditMode 测试脚本，覆盖角色、技能、背包、装备、商店、任务、存档、设置、音频、场景与 UI 资产边界。

项目还提供多个 Editor 菜单用于配置生成和专项校验。菜单路径中的 `Treasure Hunter` 是既有技术菜单标识，不代表游戏对外名称。

仓库当前未保存最新完整测试结果。执行完整测试并保留结果文件后，才能在简历中写通过数量、通过率或覆盖率。
