# Treasure Hunter 项目学习记录

> 本文档记录已经实际落地并验证过的功能。重点关注职责划分、调用流程、Unity 配置、测试方法和面试表达。

## 功能名称：四职业共用玩家运行时框架（第一阶段）

### 1. 实现目标

原项目只有刺客 Prefab 挂载了 `PlayerCo`、武器和 UI 脚本，战士、法师、弓箭手基本只有模型和 Animator，无法复用完整玩家逻辑。

本阶段把玩家拆成两部分：

- `PlayerRuntime`：所有职业共用的逻辑壳，提供移动、战斗、生命、成长、碰撞和 UI 入口。
- `CharacterVisual`：当前职业的模型和 Animator，只负责外观表现。

这样新增职业时不需要复制一套玩家代码，只需要增加职业配置和模型。当前阶段先保证四职业共享移动、生命、成长和基础攻击；法师、弓箭手的投射物技能留到后续技能系统实现。

### 2. 涉及脚本

- `GameplayCharacterSpawner`：先生成 `PlayerRuntime`，再把职业模型装到它下面。
- `PlayerCo`：玩家总入口，负责注册玩家、初始化各功能组件以及绑定职业模型。
- `PlayerPresentationComponent`：把通用动作转换成不同 Animator Controller 的参数。
- `PlayerAnimationEventRelay`：把模型子物体上的动画事件转发给父物体玩家逻辑。
- `PlayerMovementComponent`：处理移动、跑步、跳跃、翻滚和体力。
- `PlayerCombatComponent`：处理攻击输入、连击、伤害、暴击和吸血。
- `PlayerHealthComponent`：处理受伤、闪避、减伤、回血、受击反馈和死亡。
- `PlayerProgressionComponent`：处理职业初始数值、经验、升级和属性三选一。
- `WeaponCo`：负责攻击触发器命中目标后的伤害流程，优先查找武器所属玩家。
- `CharacterDefine`：保存职业模型路径、基础属性、动画适配类型和基础攻击时长。

相关资源：

- `Resources/Characters/PlayerRuntime.prefab`：四职业共用的玩家根 Prefab。
- `Resources/Data/CharacterDefine.json`：四职业静态配置。

### 3. 调用流程

角色生成流程：

```text
角色选择
-> SelectedCharacterState
-> GameplayCharacterManager.EnterCurrentCharacter
-> CharacterEntered 事件
-> GameplayCharacterSpawner
-> 生成 PlayerRuntime
-> 读取 CharacterDefine.visualPrefabPath
-> 生成 CharacterVisual 子物体
-> PlayerCo.BindCharacterVisual
-> PlayerPresentationComponent.BindVisual
-> PlayerCo.ApplyCharacterEntryData
```

移动流程：

```text
InputCo
-> GameplayRuntime.CurrentInput
-> PlayerMovementComponent
-> CharacterController.Move
-> PlayerPresentationComponent.SetMovement
-> 当前职业 Animator
```

攻击流程：

```text
鼠标左键
-> PlayerCombatComponent
-> PlayerPresentationComponent.SetCombo
-> AttackHitbox / WeaponCo
-> FighterInterface.Hit
-> PlayerHealthComponent 或敌人/金库受击逻辑
```

### 4. 核心原理

可以把它理解成“遥控车底盘和车壳”：

- `PlayerRuntime` 是底盘，决定怎么移动、怎么扣血、怎么升级。
- 战士、法师、弓箭手、刺客模型是不同车壳，只改变看起来是什么样。
- `PlayerPresentationComponent` 是转接头，把“开始跑步”翻译成不同 Animator 认识的参数。

四个 Animator 并不统一。刺客使用 `SpeedX`、`SpeedY`、`ComboIndex`，其他三个职业使用 `Speed`、`Attack`、`isAttacking`。移动和战斗组件不能直接写死这些参数，否则换职业就会报 Animator 参数不存在。表现组件会先缓存当前 Animator 真实存在的参数，只设置存在的参数，因此核心玩法不会依赖具体美术控制器。

刺客动画原本带有攻击窗口事件，所以继续使用动画事件开关攻击盒；其他职业资源没有这些事件，暂时由 `PlayerCombatComponent` 使用短时间计时打开公共 `AttackHitbox`。这是兼容方案，不是最终的远程职业技能设计。

Animator 现在位于 `CharacterVisual` 子物体，而 `PlayerCo` 位于父物体。Unity 动画事件只会发送到 Animator 所在物体，因此使用 `PlayerAnimationEventRelay` 转发 `OpenComboWindow`、`WeaponEnable`、`WeaponDisable` 等事件，否则刺客的原有连击会在拆分层级后失效。

### 5. Unity 测试方式

1. 打开 Unity，等待脚本编译完成，先确认 Console 没有红色错误。
2. 打开 `Assets/Scenes/LoginScene.unity`，按正常流程登录。
3. 进入 `CharacterSelectScene`，分别创建或选择战士、法师、弓箭手、刺客。
4. 进入 `MainScene` 后，在 Hierarchy 中检查：
   - 应生成一个 `Character_角色ID_角色名`。
   - 根对象应包含 `PlayerCo`、移动、战斗、生命、成长、表现组件和 CharacterController。
   - 根对象下面应有 `AttackHitbox` 和 `CharacterVisual`。
5. 测试 WASD 移动、Shift 跑步、Space 跳跃、鼠标右键翻滚、鼠标左键攻击。
6. 检查生命、体力、经验、升级选择和属性面板是否正常。
7. 分别用四个职业进入游戏，Console 不应出现 Animator 参数不存在或 PlayerCo 空引用。
8. 让怪物攻击玩家，再攻击怪物和金库，确认受伤、伤害数字、暴击和吸血流程。

当前阶段 Inspector 一般不需要手动拖四个职业模型：`GameplayCharacterSpawner` 会根据 JSON 自动加载。`MainScene` 中原有的 `spawnPoint` 和 `gameplayCamera` 引用仍需保持有效。

### 6. 面试表达

这个项目原来只有刺客 Prefab 挂了完整玩家脚本，另外三个职业只是模型，代码很难复用。我第一步没有直接复制四套 PlayerCo，而是把玩家分成通用逻辑壳和职业表现层。通用 PlayerRuntime 负责移动、生命、战斗、成长和碰撞，职业 Prefab 只作为模型子物体装进去。因为四个 Animator 的参数不一样，我又增加了一个表现适配组件，把移动和攻击组件发出的通用动作转换成各职业 Animator 的参数。这样新增职业时不需要修改核心玩法代码，只需要配置模型路径和动画类型，降低了角色逻辑和美术资源之间的耦合。

### 7. 面试追问

1. **为什么不直接让四个角色都继承一个 PlayerBase？**
   - Unity 更适合用组件组合能力。移动、生命、战斗可以独立替换或关闭，比多层 MonoBehaviour 继承更灵活。

2. **为什么要单独做 PlayerPresentationComponent？**
   - Animator 参数属于表现细节。隔离后，更换模型或 Animator Controller 不会影响移动和战斗规则。

3. **如何避免设置不存在的 Animator 参数时报错？**
   - 绑定模型时缓存 `animator.parameters` 的哈希，只对真实存在的参数调用 SetFloat、SetBool 或 SetTrigger。

4. **为什么模型自己的 Collider 要关闭？**
   - 玩家根对象已经有统一 CharacterController 和攻击盒。模型 Collider 如果同时启用，容易产生重复碰撞、移动抖动或误伤自己。

5. **法师和弓箭手为什么现在还是公共攻击盒？**
   - 本阶段目标是先验证角色框架复用。后续技能系统会把攻击抽象成近战、投射物和范围技能，并结合对象池生成箭矢和法术。

6. **PlayerCo 是否已经完全解耦？**
   - 还没有。本阶段先解决四职业装配和 Animator 耦合。PlayerCo 中仍保留旧字段和兼容转发，后续会逐个把属性所有权和 UI 事件迁出，而不是一次删除两千多行代码。

### 8. 本次涉及知识点

- 组合优于继承
- 数据层、逻辑层、表现层的职责划分
- `Resources.Load` 和运行时 Prefab 装配
- `CharacterController` 移动
- Animator 参数、Trigger 和动画事件
- 事件驱动的角色生成流程
- `GetComponentInParent` 与对象所属关系
- Unity Layer、Trigger 和公共攻击判定
- JSON 静态配置与运行时数据的区别
- 渐进式重构和兼容层设计

### 9. 当前限制与后续计划

- 继续把属性状态从 `PlayerCo` 迁移到独立玩家属性组件。
- 让 UI 订阅属性和成长事件，不再直接读取 `PlayerCo` 大量字段。
- 将武器伤害结果抽象为统一结构，减少 `WeaponCo` 对史莱姆和金库具体类型的判断。
- 建立技能系统，让战士/刺客使用近战技能，法师/弓箭手使用对象池投射物。
- 完成项目自有客户端和服务端关键代码的中文注释整理。

## 功能名称：玩家运行时属性数据层解耦

### 1. 实现目标

原来的生命、等级、攻击力、暴击、减伤等运行时数据全部散落在 `PlayerCo`。虽然移动、战斗、生命和成长已经拆成组件，这些组件仍要频繁访问 `owner.xxx`，本质上仍依赖一个“大总管”。

本次新增纯 C# 的 `PlayerRuntimeStats`，让战斗、生命和成长组件共同读写同一份运行时属性。`PlayerCo` 暂时保留同名属性和事件作为兼容门面，保证现有 UI、怪物和金库不需要同时大改。

### 2. 涉及脚本

- `PlayerRuntimeStats`：运行时属性唯一数据源和属性变化事件。
- `PlayerCo`：创建属性模型，并为旧调用者提供兼容属性与方法。
- `PlayerCombatComponent`：从属性模型读取攻击、暴击和吸血。
- `PlayerHealthComponent`：从属性模型读写生命、闪避、减伤和回血。
- `PlayerProgressionComponent`：修改等级、经验和升级属性。

### 3. 调用流程

```text
CharacterDefine / GameConfig
-> PlayerProgressionComponent
-> PlayerRuntimeStats
-> StatsChanged
-> PlayerCo 兼容事件
-> PlayerAttributePanel / 其他 UI
```

受伤流程：

```text
Enemy / Bullet
-> PlayerCo.Hit（兼容入口）
-> PlayerHealthComponent
-> PlayerRuntimeStats.CurrentHp
-> StatsChanged
-> UI 刷新
```

### 4. 核心原理

可以把 `PlayerRuntimeStats` 理解成角色的“账本”。战斗组件只从账本读取攻击力，生命组件只修改账本中的血量，成长组件负责给账本增加等级和属性。`PlayerCo` 不再是数据本身，而更像前台接待：旧系统仍然可以找它，但它会把请求转交给真正的数据模型和功能组件。

旧 Prefab 的公开字段通过 `FormerlySerializedAs` 迁移到兼容字段，再在运行时初始化属性模型，避免改字段名称后 Inspector 数值丢失。

### 5. Unity 测试方式

1. 分别选择四个职业进入 `MainScene`。
2. 检查职业初始生命、攻击力和移动速度是否符合 JSON 配置。
3. 受到怪物攻击，确认血量和血条同时变化。
4. 测试回血、吸血、闪避和减伤。
5. 获得经验并连续升级，确认经验条和属性三选一正常。
6. 选择攻击、生命、暴击等升级，检查属性面板立即更新。
7. 退出并重新进入游戏，确认运行时属性重新初始化。

### 6. 面试表达

玩家功能虽然已经拆成移动、战斗、生命和成长组件，但之前数据还都放在 PlayerCo，组件之间实际仍通过 PlayerCo 强耦合。我新增了一个纯 C# 的 PlayerRuntimeStats 作为运行时属性唯一数据源，战斗读取攻击和暴击，生命修改血量，成长修改等级和升级属性。PlayerCo 暂时作为兼容门面转发旧接口，这样可以渐进式迁移现有 UI 和怪物逻辑，避免一次重构导致大量 Prefab 和脚本同时失效。

### 7. 面试追问

1. **为什么 PlayerRuntimeStats 不继承 MonoBehaviour？**
   - 它不需要 Unity 生命周期，只保存数据和事件。普通 C# 类更容易测试，也不会依赖场景对象。
2. **如何保证只有一份真实数据？**
   - 战斗、生命和成长统一读写同一个实例；PlayerCo 的公开属性只是转发入口。
3. **为什么不立刻删除 PlayerCo 旧字段？**
   - 旧 Prefab 和 UI 仍依赖这些名称，先保留兼容迁移可以降低序列化丢失风险。
4. **UI 为什么不每帧读取？**
   - 属性模型变化后发送事件，UI 只在数据变化时刷新。
5. **后续怎么继续解耦？**
   - 让 UI 直接依赖只读属性接口，再逐步删除 PlayerCo 中已无调用的旧实现。

### 8. 本次涉及知识点

- 纯 C# 数据模型
- 单一数据源
- 事件驱动 UI
- 门面模式与兼容层
- Unity 序列化迁移
- 渐进式重构

## 功能名称：全项目关键代码注释与服务端日志安全

### 1. 实现目标

为项目自有客户端 `Assets/Script` 和服务端 `TreasureHunter.Server` 补充中文职责注释、生命周期说明、事件流程和网络收发说明。第三方 `AllResources` 与旧 `mmorpg` 参考工程不修改。

同时移除服务端登录、注册日志中的明文密码输出，只记录账号和处理结果。

### 2. 涉及脚本

- 客户端：核心上下文、职业配置、角色生成、输入、网络、协议、场景服务、登录与选角 UI、测试脚本。
- 服务端：启动配置、TCP 监听、连接、粘包拆包、消息线程、Session、用户业务、数据库、实体和协议。

### 3. 调用流程

```text
客户端 UI
-> GameApiClient
-> NetClient
-> PackageHandler
-> TCP
-> 服务端 NetConnection
-> PackageHandler
-> MessageDistributer
-> UserService
-> DBService
-> NetSession.SendResponse
```

### 4. 核心原理

注释重点解释“为什么这样设计”和“数据如何流动”，不为每一行代码重复描述表面动作。网络层重点说明长度头解决 TCP 粘包、后台线程不直接操作 Unity 对象、事件订阅必须成对注销；数据库层重点说明事务和密码哈希。

### 5. Unity 测试方式

注释本身不改变客户端功能。重新编译后完整测试注册、登录、创建角色、进入游戏和退出流程，并检查服务端控制台不再出现密码内容。

### 6. 面试表达

我把项目的关键模块都补充了职责和调用流程注释，重点覆盖 Unity 生命周期、事件注册注销、TCP 粘包拆包和客户端服务端职责划分。另外在审查服务端时发现登录日志会输出密码，我把日志调整为只记录账号，数据库仍只保存 BCrypt 哈希。这既提高了项目可维护性，也体现了基本的安全意识。

### 7. 面试追问

1. **为什么 TCP 需要长度头？** TCP 是字节流，没有天然消息边界。
2. **为什么网络线程不能直接操作 Unity UI？** Unity 大部分 API 只能在主线程使用。
3. **为什么密码用 BCrypt？** 它带随机盐且计算成本可调，能提高离线破解成本。
4. **为什么不能记录密码日志？** 日志权限和保留周期通常更宽，会扩大泄露范围。
5. **怎样避免注释过时？** 注释职责、约束和设计原因，修改流程时同步更新，不重复显而易见代码。

### 8. 本次涉及知识点

- TCP 粘包拆包
- Protobuf 协议
- 消息队列与线程安全
- 事件订阅和注销
- 数据库事务
- BCrypt 密码哈希
- 安全日志
- 可维护的代码注释

## 功能名称：QFramework 玩家架构与四职业真正解耦

### 1. 实现目标

把玩家数据和业务规则从巨型 MonoBehaviour 中完全拆出。即使旧玩家控制脚本不参与编译，客户端其他代码仍可成功生成；所有玩家和职业 Prefab 也不再挂载它。

### 2. 涉及脚本

- `TreasureHunterArchitecture`：注册玩家 Model 和业务 System。
- `PlayerModel`：保存一局游戏中的权威玩家数据。
- `PlayerCombatSystem`：伤害、暴击、减伤、闪避、回血和吸血规则。
- `PlayerProgressionSystem`：经验、升级和属性三选一规则。
- `PlayerCommands` / `PlayerQueries` / `PlayerEvents`：规范修改、读取和通知方向。
- `PlayerRuntimeController`：连接 Unity 生命周期与 QFramework，不保存业务公式。
- 移动、战斗、生命、成长、表现和音效组件：分别处理单一 Unity 职责。
- 玩家属性与升级 UI：通过 Query/Event 工作，不再引用具体玩家控制脚本。

### 3. 调用流程

```text
InputCo
-> PlayerMovementComponent / PlayerCombatComponent
-> Command
-> PlayerCombatSystem / PlayerProgressionSystem
-> PlayerModel
-> Event
-> PlayerRuntimeController / PlayerAttributePanel / PlayerLevelUpPanel
```

受击流程：

```text
怪物近战或 BulletCo
-> FighterInterface
-> PlayerHealthComponent
-> TakePlayerDamageCommand
-> PlayerCombatSystem
-> PlayerModel
-> PlayerDamagedEvent / PlayerDiedEvent
-> 受击表现或死亡菜单
```

### 4. 核心原理

可以把这套结构理解成公司：

- `PlayerModel` 是唯一账本，只记录真实数据。
- `System` 是业务部门，负责按规则计算伤害和升级。
- `Command` 是业务申请单，UI 和 Unity 组件不能绕过它随便改账。
- `Query` 是只读报表。
- `Event` 是广播通知，告诉 UI“数据变了”，但 UI 不能反过来控制数据层。
- `PlayerRuntimeController` 是装配人员，只负责把 Unity 组件接起来，不亲自做伤害公式。

四个职业共享同一套账本和业务部门，只从 `CharacterDefine` 读取初始属性、模型和动画适配方式，因此新增职业不需要复制玩家逻辑。

### 5. Unity 测试方式

1. 打开 `PlayerRuntime.prefab`，确认没有旧玩家控制组件。
2. 检查根物体包含 RuntimeController、移动、战斗、生命、成长、表现和音效组件。
3. 检查 `PlayerCombatComponent.weaponCollider` 指向 `AttackHitbox` 的 SphereCollider。
4. 从登录、选角进入 `MainScene`，分别测试四个职业。
5. 测试移动、跳跃、翻滚、三段攻击、受伤、闪避、吸血、升级和属性面板。
6. 执行菜单 `Tools/Treasure Hunter/Validate Player Architecture`。

### 6. 面试表达

这个项目早期虽然把移动、战斗、生命和成长拆成了多个 MonoBehaviour，但它们仍然反向依赖一个巨型玩家脚本，所以只是物理拆文件，没有真正解耦。我后来接入 QFramework 核心架构，把玩家属性放到 PlayerModel，把伤害和升级规则放到 System，并规定所有修改走 Command、读取走 Query、UI 刷新走 Event。Unity 组件只保留输入、碰撞、动画和音效职责。四个职业共用 PlayerRuntime，只通过配置和表现 Prefab 区分。最后我把旧脚本从编译列表临时排除并成功构建，证明新链路已经没有隐式依赖。

### 7. 面试追问

1. **为什么不把所有东西都做成 QFramework 类？**  
   CharacterController、Animator、Collider 仍需要 Unity 生命周期，强行抽走会增加适配成本，所以只把数据和业务规则放入架构层。
2. **如何保证只有一个数据源？**  
   所有权威属性只存于 PlayerModel，MonoBehaviour 不维护第二份血量或等级。
3. **为什么修改必须走 Command？**  
   它让修改入口可追踪，后续容易加入日志、回放、网络校验和自动测试。
4. **Event 和 Query 为什么同时存在？**  
   Event 只说明何时刷新，Query 提供刷新时需要的最新数据，避免事件携带巨大可变对象。
5. **怎样新增第五个职业？**  
   增加 CharacterDefine 配置和视觉 Prefab；公共移动、生命、战斗和成长代码不复制。

### 8. 本次涉及知识点

- QFramework Architecture
- Model / System / Command / Query / Event
- 单一数据源
- 依赖倒置
- Unity 组件化
- 配置驱动职业
- 事件驱动 UI
- Prefab 序列化引用迁移
- 编辑器自动验证
- 渐进式重构与回归测试

## 功能名称：PlayerModel 只读化与场景级 UI 解耦

### 1. 实现目标

解决两个残留耦合：第一，表现层虽然通过 Model 读数据，但仍可能拿到可写对象并直接改属性；第二，三个玩法 UI 组件仍由玩家控制器自动添加，导致玩家 Prefab 同时承担角色逻辑和界面装配。

本次把对外玩家属性收紧为 getter-only 接口，Query 返回不可变值快照；同时把会话、属性、升级 UI 装进独立 `GameplayUiRoot.prefab`，由 `MainScene` 显式持有。即使场景中完全不存在 `PlayerCo`，玩家业务和 UI 仍可通过 Command、Query、Event 协作。

### 2. 涉及脚本

- `IPlayerStatsReadOnly`：表现层能够读取的最小属性契约，没有 setter。
- `PlayerStatsSnapshot`：复制查询时刻的玩家数据，避免调用者长期持有 Model 内部对象。
- `PlayerRuntimeStats`：Model 内部的可变实现，setter 仅在程序集内部可见。
- `PlayerModel`：公开只读 Stats，向 System 提供内部 MutableStats，并生成快照。
- `PlayerCombatSystem` / `PlayerProgressionSystem`：唯一拥有业务属性修改权限的系统。
- `PlayerRuntimeController` 与玩家功能组件：只读取接口；体力、跑步、翻滚和攻击中状态由对应组件自己维护。
- `GameplayUiRoot`：场景级 UI 组合根，显式持有 Canvas 和三个 View。
- `GameSessionUi`：监听 `PlayerDiedEvent` 显示结束菜单，不再由生命组件直接查找 UI。
- `GameplayUiRootMigration`：生成 UI Prefab、迁移 MainScene，并保留原 Canvas 的无关界面。
- `PlayerModelReadOnlyTests`：保护只读边界、业务公式和 Prefab/场景装配规则。

### 3. 调用流程

写数据流程：

```text
Unity 输入/碰撞/UI 按钮
-> Command
-> PlayerCombatSystem / PlayerProgressionSystem
-> PlayerModel.MutableStats
-> Event
-> UI / 动画 / 音效表现
```

读数据流程：

```text
UI / Unity 组件
-> Query 或 IPlayerStatsReadOnly
-> PlayerStatsSnapshot
-> 只显示结果，不能反向修改 Model
```

死亡 UI 流程：

```text
PlayerHealthComponent
-> TakePlayerDamageCommand
-> PlayerCombatSystem
-> PlayerDiedEvent
-> GameSessionUi.ShowGameOver
```

### 4. 核心原理

可以把 `PlayerModel` 想成银行账户。以前外部代码虽然说自己只是“查余额”，拿到的却是一本可以直接涂改的账本；现在外部只能看到只读账单 `IPlayerStatsReadOnly`。Query 更严格，它返回一张拍照后的账单 `PlayerStatsSnapshot`，之后账户再变化，旧照片也不会跟着变。

真正修改余额必须提交 Command，由 System 按统一规则处理。这样伤害减免、升级上限、回血上限只有一套公式，也能脱离场景写单元测试。

UI 则像商场里的电子屏，它属于场景，不属于某一个顾客。`GameplayUiRoot` 显式持有 Canvas 和三个界面，玩家死亡时只广播事件，结束菜单自己响应。玩家 Prefab 因此可以被替换、销毁或切换职业，而 UI 不需要跟着重新挂载。

### 5. Unity 测试方式

1. 打开 `Assets/Scenes/MainScene.unity`，确认层级根节点有且只有一个 `GameplayUiRoot`。
2. 打开 `Assets/Prefabs/UI/GameplayUiRoot.prefab`，确认根物体有 Canvas、GameplayUiRoot、GameSessionUi、PlayerAttributePanel、PlayerLevelUpPanel。
3. 打开 `Assets/Resources/Characters/PlayerRuntime.prefab`，确认没有上述 UI 组件，也没有 `PlayerCo`。
4. 从登录、选角进入游戏，测试 ESC 暂停、Tab 属性、升级三选一和死亡菜单。
5. 打开 Test Runner，执行 EditMode 测试 `PlayerModelReadOnlyTests` 与 `PlayerUiStructureTests`。
6. 执行 `Tools/Treasure Hunter/Validate Player Architecture`，确认 Console 输出验证成功。

### 6. 面试表达

玩家解耦后我又收紧了数据边界。PlayerModel 对外只暴露 getter-only 接口，Query 返回不可变快照，所以 UI 和 MonoBehaviour 不能绕过 Command 直接改权威属性；所有伤害和成长修改集中在 System。瞬时的跑步、翻滚、攻击中和体力状态留在各自 Unity 组件，避免全局 Model 变成状态垃圾桶。UI 也从玩家 Prefab 搬到独立 GameplayUiRoot，通过 Query 读数据、Event 刷新。最后我用 EditMode 测试和编辑器验证器同时保护数值公式与 Prefab 装配边界。

### 7. 面试追问

1. **getter-only 接口和 private setter 有什么区别？**
   - private/internal setter保护实现类，但外部若拿到具体类型仍看得到其完整 API；只暴露接口能从编译期限制调用者只读。
2. **为什么 Query 还要返回快照，直接返回只读接口不行吗？**
   - 只读接口仍可能指向实时可变对象；快照是值副本，调用者不会持有 Model 内部引用，也更适合日志、回放和测试断言。
3. **为什么体力不放 PlayerModel？**
   - 当前体力只服务本地移动表现，每帧变化且不需要跨场景、存档或网络同步，放组件内职责更清楚。若以后服务端校验体力，再提升为权威 Model 数据。
4. **UI 为什么不挂在玩家下面？**
   - UI 生命周期属于场景和游戏会话。角色死亡、换职业或重新生成时，UI 不应被销毁重建，也不应依赖某个玩家 GameObject。
5. **如何防止以后同事又把 UI 挂回玩家？**
   - `PlayerUiStructureTests` 和 `PlayerArchitectureValidator` 都会检查玩家 Prefab 不含 UI，并检查 MainScene 只有一个 UI 根。

### 8. 本次涉及知识点

- 接口隔离与最小权限原则
- 不可变值对象和数据快照
- Command / Query 职责分离
- 单一数据源与写入口收口
- 领域事件驱动 UI
- 场景级 UI 生命周期
- Prefab 显式装配
- EditMode NUnit 单元测试
- Editor 自动化与架构守护测试

## 功能名称：玩法 UI 纯 Prefab 引用重构

### 1. 实现目标

删除玩法 UI 的运行时创建兜底。过去脚本在 Canvas、面板或按钮引用缺失时，会通过查找和 `new GameObject` 临时生成界面；属性面板甚至每次刷新都先 Destroy 再重建全部属性行。这会掩盖 Prefab 装配错误，也会增加运行时分配、GC 和维护成本。

现在会话 UI、升级 UI、属性 UI 和新手引导都只使用场景或 Prefab 中保存的序列化引用。引用缺失时直接输出具体字段名并停止对应功能。

### 2. 涉及脚本

- `GameSessionUi`：只更新分数、暂停和结束菜单内容。
- `PlayerLevelUpPanel`：只填充三个固定按钮并发送升级 Command。
- `PlayerAttributePanel`：把 Query 数据按 key 分发到静态属性行。
- `PlayerAttributeRowView`：管理单条属性的文本和变化高亮。
- `GameplayStartupGuidePopup`：只控制已配置弹窗的内容和显隐。
- `GameplayUiRoot`：验证场景级 UI 组合引用，不再自动 GetComponent。
- `GameplayUiRootMigration`：仅在编辑器中生成 12 条属性行并写入 Prefab 引用。
- `PlayerArchitectureValidator`：检查 UI 引用、属性行数量和 EventSystem。

### 3. 调用流程

```text
PlayerStatsChangedEvent
-> PlayerAttributePanel
-> GetPlayerAttributeEntriesQuery
-> entry.Key
-> PlayerAttributeRowView
-> 更新 Prefab 中已有 Text/Image
```

```text
升级事件
-> PlayerLevelUpPanel
-> 填充 Prefab 中三个固定 Button
-> 玩家点击
-> ResolvePlayerUpgradeCommand
```

### 4. 核心原理

Prefab 可以理解为已经装修好的房子，脚本只是入住后改变电视上的文字。旧方案发现房间缺了家具，就在玩家进入后临时制作家具；虽然能显示，但问题会被隐藏，而且每次重建都有性能和状态风险。

纯 Prefab 方案要求所有控件提前存在并通过 Inspector 保存引用。运行时只更新数据和显隐。属性行使用稳定 key 与 Query 返回的数据匹配，因此 UI 不需要引用 PlayerModel 的可写对象。

编辑器迁移工具仍然可以批量生成静态层级，但这些创建代码只存在于 `Assets/Editor`，不会进入最终客户端。

### 5. Unity 测试方式

1. 打开 `GameplayUiRoot.prefab`，确认包含 12 个 `PlayerAttributeRowView`。
2. 打开 `MainScene`，确认只有一个 GameplayUiRoot 和一个 EventSystem。
3. 运行游戏，按 Tab 检查 12 项属性及高亮。
4. 测试 ESC 暂停、死亡菜单、升级三选一和开局说明弹窗。
5. 执行 `Tools/Treasure Hunter/Validate Player Architecture`。
6. 在 Test Runner 执行 `PlayerModelReadOnlyTests` 和 `PlayerUiStructureTests`。

### 6. 面试表达

我把玩法 UI 从运行时动态构建改成了纯 Prefab 引用。会话菜单和升级面板只操作预先配置的控件；属性面板使用 12 条静态 RowView，通过 key 接收 Query DTO 并更新文本。引用不完整时系统会明确报错，不会用 Find 或 new GameObject 掩盖问题。UI 生成逻辑只保留在 Editor 工具中，因此最终客户端减少了运行时分配和层级查找，也让美术和策划可以直接在 Prefab 中调整样式。

### 7. 面试追问

1. **为什么不继续运行时创建 UI？**
   - 固定界面没有动态创建的必要；Prefab 更直观、可预览，也能减少运行时分配和装配不确定性。
2. **属性数据变化时还会重建节点吗？**
   - 不会，只通过 key 找到固定 RowView 并修改 Text 和高亮颜色。
3. **以后增加属性怎么办？**
   - 扩展 Query DTO，并在 Prefab 增加相同 key 的 RowView，验证器会检查引用。
4. **动态背包格子也必须完全静态吗？**
   - 不一定。数量动态且可滚动的列表可以使用条目 Prefab 和对象池；固定 HUD 更适合纯 Prefab。
5. **怎样避免 Prefab 引用丢失？**
   - 启动验证、Editor 验证器和 EditMode 架构测试共同检查。

### 8. 本次涉及知识点

- Unity Prefab 序列化引用
- View 与数据 DTO 映射
- 稳定 key 和字典查询
- EventSystem
- UI LayoutGroup
- 避免运行时 Find/Instantiate/Destroy
- Editor 工具与 Runtime 程序集隔离
- 架构守护测试

## 功能名称：核心脚本中文注释整理

### 1. 实现目标

这次不是新增玩法功能，而是把客户端和服务端核心脚本补上面向新手学习的中文注释。
目标是让你后面再看这套代码时，能快速看懂“这个类负责什么、这个函数为什么这样写、调用链是怎么走的”，同时也方便你准备秋招面试表达。

### 2. 涉及脚本

- 客户端架构层：
  - `TreasureHunterArchitecture`：QFramework 架构入口。
  - `PlayerModel`：玩家运行时权威数据模型。
  - `PlayerCommands`：玩法层统一命令入口。
  - `PlayerCombatSystem`：伤害、暴击、闪避、吸血、回血规则。
  - `PlayerProgressionSystem`：经验、升级、加点、升级候选生成。
- 客户端运行时层：
  - `GameplayRuntime`：当前游戏局的全局运行时上下文。
  - `PlayerRuntimeController`：玩家运行时主调度器。
  - `PlayerMovementComponent`：移动、跳跃、翻滚、体力。
  - `PlayerCombatComponent`：攻击输入、连击、武器碰撞盒。
  - `PlayerHealthComponent`：受伤、闪避、回血、死亡反馈。
  - `PlayerProgressionComponent`：Unity 世界到成长系统的桥接。
  - `PlayerPresentationComponent`：通用动作到不同 Animator 参数的适配。
  - `PlayerAudioComponent`：玩家音效播放。
- 客户端网络与流程层：
  - `NetClient` / `PackageHandler` / `MessageDispatch` / `MessageDistributer`：客户端 TCP 通信与消息分发。
  - `GameApiClient`：UI 调用的业务 API 门面。
  - `SceneFlowService`：登录、选角、进入游戏、重开、登出流程。
  - `LoginPanelController` / `GameSessionUi`：登录 UI 与局内 UI。
- 服务端核心层：
  - `Program` / `GameServer` / `Settings` / `CommandHelper`：程序入口与配置。
  - `UserService`：注册、登录、建角、进游戏、离场。
  - `DBService`：数据库访问、事务、角色表维护。
  - `NetService` / `NetConnection` / `NetSession` / `TcpSocketListener`：服务端网络会话。
  - `MessageDispatch` / `MessageDistributer` / `PackageHandler`：协议分发与拆包。
  - `CharacterManager` / `Character` / `TUser` / `TPlayer` / `TCharacter`：在线角色与数据库模型。

### 3. 调用流程

登录链路：

```text
LoginPanelController
-> GameApiClient.Login
-> NetClient.SendMessage
-> PackageHandler.PackMessage
-> 服务端 TcpSocketListener / NetConnection
-> 服务端 PackageHandler
-> MessageDistributer
-> UserService.OnLogin
-> DBService.FindUserByUsername
-> NetSession.Response
-> 客户端 MessageDistributer
-> GameApiClient.OnUserLogin
-> LoginPanelController 回调
-> SceneFlowService.LoadCharacterSelectScene
```

局内玩家链路：

```text
GameplayCharacterSpawner
-> PlayerRuntimeController
-> PlayerMovementComponent / PlayerCombatComponent / PlayerHealthComponent / PlayerProgressionComponent
-> PlayerCommands
-> PlayerCombatSystem / PlayerProgressionSystem
-> PlayerModel
-> PlayerEvents
-> GameSessionUi / 飘字 / 表现层
```

### 4. 核心原理

这次整理的重点，不是“让代码多几行注释”，而是把你最容易迷糊的几个层次讲清楚：

- 第一层是“数据层”。
  - 玩家真正的生命、经验、暴击率这些核心数据，统一放在 `PlayerModel` 里。
  - 这样谁都不能随便改数据，改数据必须走命令或系统。
- 第二层是“规则层”。
  - 比如受伤怎么算、升级怎么算、吸血怎么算，这些都在 `PlayerCombatSystem` 和 `PlayerProgressionSystem` 里。
  - 好处是四个职业可以复用同一套规则，不用每个职业复制一遍。
- 第三层是“Unity 表现层”。
  - `PlayerRuntimeController`、移动组件、战斗组件、UI 组件这些脚本，主要负责接输入、播动画、显示 UI、处理场景对象。
  - 它们不直接写复杂公式，而是把业务请求交给系统。
- 第四层是“网络层”。
  - 客户端 `NetClient` 只处理收发字节流。
  - `PackageHandler` 负责拆包和封包。
  - `MessageDistributer` 把消息交给真正业务层。
  - 服务端再由 `UserService` 和 `DBService` 完成账号与角色逻辑。

你可以把整个项目理解成一家店：

- `PlayerModel` 像账本，记着店里真实有多少钱。
- `System` 像店长，决定钱该怎么算。
- `MonoBehaviour/UI` 像前台员工，只负责接客、展示、操作界面。
- `NetClient / NetService` 像快递员，只负责把包裹送到对应部门。

### 5. Unity 测试方式

1. 打开 Unity，等待脚本编译完成，先确认 `Console` 没有新的红色报错。
2. 打开 `Assets/Scenes/LoginScene.unity`，测试注册和登录。
3. 成功后进入 `CharacterSelectScene`，检查角色列表、创建角色流程是否正常。
4. 进入 `MainScene`，测试移动、攻击、升级、暂停、死亡结算。
5. 同时启动服务端，确认客户端与服务端链路仍然正常。
6. 这次重点不是看新玩法，而是确认“只加注释没有改坏逻辑”。

### 6. 面试表达

这次我专门对项目的核心脚本做了一轮中文注释整理，不是简单写“这一行干了什么”，而是按架构层、规则层、表现层和网络层去补注释。比如玩家数据统一收敛到 `PlayerModel`，伤害和升级规则放在 `System` 里，Unity 组件只负责输入、动画、UI 和场景表现；服务端则把登录、建角、会话、拆包和数据库事务这些链路补成了可读的业务流程。这样做的价值，一方面是提升我自己复盘项目和继续扩功能的效率，另一方面是在面试里我能更清楚地讲出每个模块的职责边界，而不是只会说“这个功能能跑”。

### 7. 面试追问

1. **为什么注释还要专门分层写，而不是随便写两句？**
   - 因为商业项目最重要的是职责边界。分层写注释能让我后面一眼看出数据是谁管、规则是谁算、表现是谁播。
2. **为什么不把所有逻辑都写在 MonoBehaviour 里，注释也更集中？**
   - 因为那样短期看简单，长期会变成巨型脚本。现在把规则放进 System，更容易复用和扩展。
3. **为什么网络层要专门注释拆包和消息分发？**
   - 因为这是服务端面试高频问题。TCP 没有消息边界，必须靠长度头拆包；消息也不能在 IO 回调里直接跑耗时业务。
4. **为什么服务端登录逻辑要强调密码哈希和事务？**
   - 这是后端基础素养。密码不能明文保存；用户表和玩家档案表要一起成功或一起失败，避免脏数据。
5. **注释会不会过时？**
   - 会，所以注释不能只写表面现象，应该重点写职责、设计原因和边界。这种内容比“这一行做了什么”更不容易过时。

### 8. 本次涉及知识点

- QFramework 架构分层
- Model / System / Command 的职责划分
- Unity 生命周期职责分配
- 组件化解耦
- 事件注册与注销
- TCP 粘包拆包
- 消息分发总线
- 服务端 Session 管理
- BCrypt 密码哈希
- SQL 事务与并发槽位保护
- 面向面试的代码讲解方式

## 功能名称：核心脚本函数级注释补全

### 1. 实现目标

在上一轮类职责注释的基础上，继续把核心脚本里最关键的方法补上函数级中文注释。
重点目标不是“每个方法都机械写一句”，而是把最容易看不懂的调用入口、状态切换、桥接方法和 UI/网络流程讲清楚。

### 2. 涉及脚本

- 客户端：
  - `GameplayCharacter`
  - `GameplayCharacterManager`
  - `GameplayCharacterSpawner`
  - `SelectedCharacterState`
  - `CharacterDataManager`
  - `GameplayUiRoot`
  - `GameplayStartupGuidePopup`
  - `PlayerAttributePanel`
  - `PlayerLevelUpPanel`
  - `CharacterSaveSlot`
  - `CharacterPreviewController`
  - `WeaponCo`
  - `BulletCo`
  - `InputCo`
  - `BoxCo`
  - `CameraCo`
- 服务端：
  - `Singleton`
  - `Log`

### 3. 调用流程

角色进入主场景的链路：

```text
SelectedCharacterState
-> GameplayCharacterManager.EnterCurrentCharacter
-> CharacterEntered 事件
-> GameplayCharacterSpawner.CreateCharacterObject
-> PlayerRuntimeController.BindCharacterVisual
-> PlayerRuntimeController.ApplyCharacterEntryData
-> Movement / Combat / Health / Progression 组件开始工作
```

局内升级面板链路：

```text
PlayerProgressionSystem 升级
-> PlayerUpgradeQueueChangedEvent
-> PlayerLevelUpPanel.HandlePendingUpgradeSelectionsChanged
-> ShowNextSelection
-> 玩家点击按钮
-> ResolvePlayerUpgradeCommand
-> PlayerProgressionSystem 应用升级
```

### 4. 核心原理

这轮补注释的核心价值，在于把“桥接层函数”讲清楚。

- `GameplayCharacterManager` 负责“登记角色数据”。
- `GameplayCharacterSpawner` 负责“把角色数据变成场景对象”。
- `PlayerRuntimeController` 负责“让这个对象在 Unity 里真正跑起来”。

如果没有这些函数级注释，你很容易只看到很多脚本名字差不多，却不知道它们分工有什么不同。

可以这样理解：

- `GameplayCharacter` 是一张角色资料卡。
- `GameplayCharacterManager` 是前台登记员。
- `GameplayCharacterSpawner` 是把资料卡交给工人去装配模型的人。
- `PlayerRuntimeController` 是装好后的总控开关。

### 5. Unity 测试方式

1. 打开 `LoginScene`，完成登录。
2. 进入 `CharacterSelectScene`，点不同角色槽位，观察预览模型切换和高亮是否正常。
3. 进入 `MainScene`，确认角色能正常生成，摄像机跟随正常，输入、攻击、金库、升级面板都能正常工作。
4. 检查 `Console`，确认没有因为补注释产生新的编译错误。

### 6. 面试表达

我后面又专门做了一轮函数级注释补全，重点放在角色生成链路、升级选择链路和核心交互脚本上。比如我把 `GameplayCharacterManager`、`GameplayCharacterSpawner` 和 `PlayerRuntimeController` 这三个看起来名字很像的脚本，分别标清楚它们是“登记数据”“生成场景对象”“调度运行时组件”的不同职责。这样做的好处是我后面继续扩展多人角色、技能系统或者 UI 时，不容易把逻辑混在一起，同时也更方便我在面试里把整条调用链讲清楚。

### 7. 面试追问

1. **为什么要专门给桥接层方法写注释？**
   - 因为桥接层最容易让人混淆，尤其是“管理器”和“生成器”这种名字相近的脚本。
2. **为什么不是所有方法都写注释？**
   - 因为过度注释反而会影响阅读，我主要注释的是入口、状态切换、桥接和容易误解的方法。
3. **为什么 `GameplayCharacterManager` 不直接生成 Prefab？**
   - 因为那样会把数据管理和场景实例化耦合到一起，不利于扩展。
4. **为什么 `PlayerLevelUpPanel` 不直接改属性？**
   - 因为 UI 只负责显示和收集选择，真正属性结算必须交给成长系统统一处理。
5. **为什么 `BulletCo` 和 `WeaponCo` 都要强调统一受击接口？**
   - 因为这样远程和近战都能复用同一套目标受击入口，降低耦合。

### 8. 本次涉及知识点

- 数据与场景对象分离
- 管理器与生成器职责边界
- Unity UI 静态 Prefab 装配
- 升级面板与事件驱动
- 输入集中采样
- 世界空间血条与摄像机对齐
- 近战命中与远程命中统一接口

## 功能名称：统一调整弹出鼠标位置

### 1. 实现目标

把暂停菜单、升级面板、开局提示、游戏结束面板等 UI 弹出时的鼠标位置，从窗口左上角统一改成窗口横向中心、纵向从上往下四分之一的位置。这样玩家打开弹窗时鼠标不会默认压在中间按钮上，也不会跑到太靠边的位置。

### 2. 涉及脚本

- `CursorPopupPositioner`：统一负责显示鼠标，并通过 Windows API 把系统鼠标移动到指定窗口坐标。

### 3. 调用流程

```text
暂停/升级/开局提示/结束面板
-> CursorPopupUtility.ShowAtUpperCenterQuarter
-> CursorPopupUtility.MoveToUpperCenterQuarterNow
-> GetUpperCenterQuarterTargetPosition
-> GetDefaultPopupClientPoint
-> ClientToScreen
-> SetCursorPos
```

### 4. 核心原理

游戏战斗时鼠标通常是锁定并隐藏的，弹出 UI 面板时需要把鼠标显示出来。这个项目没有让每个面板各自处理鼠标位置，而是统一调用 `CursorPopupUtility`，所以只要修改这个工具内部的目标坐标，所有弹窗都会一起生效。

这次目标点使用屏幕比例计算：`Screen.width * 0.5f` 表示横向中心，`Screen.height * 0.25f` 表示从上往下四分之一。算出来的是游戏窗口内部坐标，最后通过 `ClientToScreen` 转成 Windows 系统屏幕坐标，再交给 `SetCursorPos` 真正移动鼠标。

### 5. Unity 测试方式

1. 打开主场景并运行游戏。
2. 触发升级面板，观察鼠标是否出现在屏幕上方偏中间的位置。
3. 打开暂停/会话菜单、开局提示或游戏结束面板，确认这些弹窗也使用同一个位置。
4. 如果鼠标没有移动，优先确认当前是否在 Windows 编辑器或 Windows 打包版本中测试，因为这个移动逻辑依赖 `user32.dll`。

### 6. 面试表达

这个鼠标弹出位置不是写在每个 UI 面板里的，而是做成了一个统一工具。暂停、升级、开局提示、结束面板都调用同一个入口显示鼠标，所以我要调整位置时只需要改工具里的坐标计算。这里用 `Screen.width` 和 `Screen.height` 按比例算窗口内部坐标，再用 Windows API 转成系统屏幕坐标并移动鼠标。这样做的好处是逻辑集中，后续如果 UI 交互规则变化，不需要到每个面板里重复修改。

### 7. 面试追问

1. **为什么不在每个 UI 面板里单独设置鼠标位置？**  
   因为那样会产生重复代码，后续想统一调整位置时容易漏改。
2. **为什么要连续几帧移动鼠标？**  
   因为鼠标从锁定状态切到显示状态时，Unity 或系统可能在下一帧把鼠标拉回中心，补几次能让最终位置更稳定。
3. **为什么使用 `Screen.width` 和 `Screen.height`？**  
   因为目标位置是相对游戏窗口的比例，不同分辨率下都能保持类似的视觉位置。
4. **为什么需要 `ClientToScreen`？**  
   因为我们先算的是窗口内部坐标，而 `SetCursorPos` 需要的是整个桌面的屏幕坐标。
5. **这个方案有什么平台限制？**  
   当前真正移动系统鼠标的部分只在 Windows 编辑器和 Windows 打包版本启用，其它平台不会编译 `user32.dll` 相关代码。

### 8. 本次涉及知识点

- Unity 鼠标锁定与显示
- `Screen.width` / `Screen.height` 屏幕坐标
- 窗口内部坐标和系统屏幕坐标
- Windows API `ClientToScreen` / `SetCursorPos`
- UI 公共工具类的复用价值

## 功能名称：四职业预览模型与运行时模型分离

### 1. 实现目标

这次主要解决选角界面和进游戏模型职责混在一起的问题。战士、法师、弓箭手新增独立预览 Prefab，选角界面只加载预览资源；进入游戏时仍然通过 `PlayerRuntime.prefab` 统一提供移动、攻击、血量和成长逻辑，再把职业模型挂到运行时玩家对象下面。

### 2. 涉及脚本

- `CharacterPreviewController`：根据 `previewPrefabPath` 加载选角预览模型，并支持鼠标拖动旋转。
- `CharacterSelectPanelController`：切换职业按钮时读取职业配置，刷新职业文字和预览模型。
- `GameplayCharacterSpawner`：进入主场景时生成 `PlayerRuntime.prefab`，再加载 `visualPrefabPath` 对应的职业模型。
- `PlayerRuntimeController`：作为玩家运行时总入口，统一调度移动、战斗、血量和成长组件。
- `GameplayUiRoot`：统一承载 HUD、属性面板和升级面板，不挂在某一个职业模型身上。

### 3. 调用流程

```text
选角界面：
职业按钮 -> CharacterSelectPanelController.SelectClass
-> CharacterDataManager.GetCharacter
-> CharacterPreviewController.ShowCharacter
-> Resources.Load(previewPrefabPath)

进入游戏：
SceneFlowService.StartGameplay
-> GameplayCharacterSpawner
-> Resources.Load("Characters/PlayerRuntime")
-> Resources.Load(visualPrefabPath)
-> PlayerRuntimeController.BindCharacterVisual
-> PlayerPresentationComponent 绑定 Animator
```

### 4. 核心原理

你可以把角色分成两层：`PlayerRuntime` 是“能操作的玩家外壳”，职业 Prefab 是“外观和动画”。这样四个职业不用各自复制一份移动、攻击、血量、UI 逻辑，后续修玩家控制时只改一处。

预览模型单独拆出来，是为了让选角界面的展示需求不影响实战模型。例如以后想给预览模型加展示动作、调整朝向、隐藏碰撞体，都不会破坏进游戏后的玩家对象。

UI 也同理，HUD、属性面板、升级面板属于玩法场景 UI，不属于某个角色模型。它们统一放在 `GameplayUiRoot.prefab`，避免每生成一个角色就多出一份 UI 控制器。

### 5. Unity 测试方式

1. 打开 `CharacterSelectScene`。
2. 点击战士、法师、弓箭手、刺客四个职业按钮。
3. 确认四个职业都能显示预览模型，并且拖动鼠标可以旋转。
4. 分别选择四个职业进入 `MainScene`。
5. 测试移动、跑步、跳跃、翻滚和攻击动画。
6. 按 `Tab` 检查属性面板，按 `Esc` 检查暂停界面。
7. 查看 Console，确认没有 `GameSessionUi`、`PlayerAttributePanel`、`PlayerLevelUpPanel` 引用缺失报错。

### 6. 面试表达

这个角色生成我没有让四个职业各自挂一整套玩家脚本，而是拆成了“通用运行时外壳”和“职业表现模型”。选角界面通过 `previewPrefabPath` 加载独立预览 Prefab，进入游戏时通过 `PlayerRuntime.prefab` 提供统一控制逻辑，再加载 `visualPrefabPath` 的职业模型作为子物体。这样做的好处是职责清楚，预览展示、角色控制和玩法 UI 互不耦合，后面扩展新职业时只需要加模型配置，不需要复制一堆控制脚本。

### 7. 面试追问

1. **为什么要区分预览 Prefab 和进游戏 Prefab？**  
   因为选角界面只关心展示，主场景关心控制和战斗。两者分开后，调整预览动作或朝向不会影响实战角色。

2. **为什么 UI 不挂在角色 Prefab 上？**  
   因为 UI 是场景级系统，不属于单个模型。挂在角色上会导致多角色或重复生成时出现多份 UI 控制器。

3. **为什么四个职业不各自复制一套控制脚本？**  
   复制会让后续维护成本变高。统一 `PlayerRuntime` 后，移动、攻击、血量、升级逻辑只维护一份。

4. **职业模型里的 Animator 怎么接到玩家逻辑？**  
   生成器实例化职业模型后调用 `BindCharacterVisual`，再由 `PlayerPresentationComponent` 找到可用 Animator 并缓存参数。

5. **后续新增第五个职业要改哪里？**  
   新增一个职业模型和预览模型，再在 `CharacterDefine.json` 增加一条配置，通常不需要改玩家控制逻辑。

### 8. 本次涉及知识点

- Unity `Resources.Load` 路径配置
- Prefab 职责拆分
- 选角预览模型和运行时模型分离
- 通用玩家运行时外壳
- Animator 表现层绑定
- 场景级 UI 与角色模型解耦

## 功能名称：玩家常驻 HUD 刷新

### 1. 实现目标

这次解决主场景左上角常驻 UI 没有显示和刷新玩家血量、等级、经验、体力的问题。血量、等级和经验由玩家属性变化事件驱动刷新，体力因为跑步、跳跃、翻滚会频繁变化，所以由 HUD 缓存当前玩家移动组件后轻量同步。

### 2. 涉及脚本

- `PlayerHudUi`：负责主界面玩家 HUD 的文本和进度条刷新。
- `GameplayUiRoot`：把 `PlayerHudUi` 纳入玩法 UI 根节点统一校验。
- `PlayerMovementComponent`：对外提供当前体力、最大体力和体力百分比的只读属性。
- `GameplayUiRootMigration`：编辑器迁移工具，负责自动绑定 HUD 文本和进度条引用。
- `PlayerArchitectureValidator`：编辑器验收工具，检查 `GameplayUiRoot.prefab` 是否完整绑定 HUD。

### 3. 调用流程

```text
扣血 / 治疗 / 加经验 / 升级
-> PlayerCombatSystem 或 PlayerProgressionSystem
-> SendEvent(PlayerStatsChangedEvent)
-> PlayerHudUi.HandlePlayerStatsChanged
-> GetPlayerStatsQuery
-> 刷新 Lv / HP / EXP 文本和进度条

玩家跑步 / 跳跃 / 翻滚消耗体力
-> PlayerMovementComponent 更新 currentStamina
-> PlayerHudUi.Update
-> 读取 CurrentStamina / MaxStamina / StaminaPercent
-> 刷新 SP 文本和体力条
```

### 4. 核心原理

HUD 只负责显示，不负责修改玩家数据。玩家核心数值保存在 `PlayerModel` 中，系统修改数值后发送事件，HUD 收到事件再用 Query 读取快照。这样 UI 不需要每帧查询血量、等级、经验，也不会反过来影响战斗和成长逻辑。

体力比较特殊，因为它会随着跑步持续下降、停止后持续恢复，如果每一点变化都发事件，会让移动组件和 UI 强耦合。所以这里让 `PlayerHudUi` 缓存当前玩家的 `PlayerMovementComponent`，只在显示值变化时更新体力 UI。

### 5. Unity 测试方式

1. 打开 `MainScene`。
2. 确认场景里有 `GameplayUiRoot`，其 Inspector 上有 `PlayerHudUi`、`GameSessionUi`、`PlayerAttributePanel`、`PlayerLevelUpPanel`。
3. 进入运行，左上角应该看到 `Lv`、`HP`、`SP`、`EXP`。
4. 让玩家受到伤害或回血，观察 HP 文本和血条是否变化。
5. 打怪或用测试入口获得经验，观察 EXP 和 Lv 是否变化。
6. 按住跑步、跳跃或翻滚，观察 SP 文本和体力条是否变化，停止后应逐渐恢复。

### 6. 面试表达

主界面 HUD 我没有直接挂在玩家模型上，而是做成场景级 UI，由 `GameplayUiRoot` 统一管理。血量、等级、经验这些低频变化的数据通过 QFramework 事件通知 UI，再用 Query 读取只读快照刷新；体力因为变化频率高，我让 HUD 缓存当前玩家移动组件，只在数值变化时更新体力条。这样 UI 只负责显示，战斗、成长和移动系统仍然各自维护自己的逻辑，后续换 UI 样式或扩展属性不会影响核心玩法。

### 7. 面试追问

1. **为什么 HP/EXP 不放在 Update 里每帧刷新？**  
   因为这些数据只在扣血、治疗、加经验、升级时变化，用事件驱动更省性能，也更清楚。

2. **为什么体力用了 Update？**  
   体力跑步时几乎每帧变化，如果每帧发事件反而会增加移动系统和 UI 的耦合，所以 HUD 轻量读取并做变化判断。

3. **为什么 HUD 不直接改 PlayerModel？**  
   UI 是表现层，只应该显示数据。修改数据应该交给战斗系统、成长系统或移动组件，避免逻辑混乱。

4. **为什么要通过 `GameplayRuntime.CurrentPlayerChanged` 缓存玩家？**  
   因为玩家是运行时生成的，UI 不能在编辑器里固定拖某个场景玩家，用事件拿当前玩家更稳定。

5. **如果以后做蓝条或技能冷却条怎么扩展？**  
   可以继续新增只读数据入口和独立 UI 字段，低频数据用事件刷新，高频数据用缓存组件轻量同步。

### 8. 本次涉及知识点

- Unity UI `Text` 和 `Image.fillAmount`
- 场景级 HUD 与玩家对象解耦
- QFramework 事件和 Query
- 只读属性暴露运行时数据
- 高频 UI 刷新与低频事件刷新的取舍
- Prefab 序列化引用校验

## 功能名称：玩家魔法值与技能资源接口

### 1. 实现目标

本次给玩家补充魔法值数据和蓝条显示，为后续技能系统预留统一的资源消耗入口。虽然当前还没有具体技能，但后续技能释放前可以先调用魔法消耗 Command，蓝量不足就取消释放，避免技能表现和资源数据不同步。

### 2. 涉及脚本

- `PlayerRuntimeStats`：新增当前魔法、最大魔法、基础最大魔法和额外最大魔法。
- `PlayerModel`：从职业配置 `CharacterDefine.mp` 初始化魔法值。
- `PlayerResourceSystem`：负责判断蓝量、消耗魔法、恢复魔法和回满魔法。
- `PlayerCommands`：提供 `TrySpendPlayerManaCommand`、`RestorePlayerManaCommand` 等外部入口。
- `PlayerQueries`：提供 `CanSpendPlayerManaQuery`，用于技能按钮或释放前预判断。
- `PlayerHudUi`：在主界面 HUD 显示 MP 文本和蓝条。
- `GameplayUiRoot.prefab`：新增 `MpText`、`MpBar/Fill`。

### 3. 调用流程

```text
进入游戏
-> PlayerProgressionSystem.InitializePlayer
-> PlayerModel.Reset
-> 读取 CharacterDefine.mp
-> CurrentMp = MaxMp
-> PlayerStatsChangedEvent
-> PlayerHudUi 刷新 MP

后续技能释放
-> SkillController
-> TrySpendPlayerManaCommand(cost)
-> PlayerResourceSystem.TrySpendMana
-> 扣除 CurrentMp
-> PlayerManaChangedEvent + PlayerStatsChangedEvent
-> PlayerHudUi 刷新 MP 蓝条
```

### 4. 核心原理

魔法值属于玩家运行时资源，和血量一样应该放在 `PlayerModel` 的权威数据里，而不是放在 UI 或技能脚本里。技能系统只表达“我要消耗多少魔法”，真正判断够不够、扣不扣蓝、通知 UI 刷新，都交给 `PlayerResourceSystem`。

这样做的好处是后续扩展技能、药水、装备、Buff 或联网同步时，不需要到每个技能脚本里重复写扣蓝逻辑。所有资源变化都从统一入口经过，问题更容易定位。

### 5. Unity 测试方式

1. 打开 `MainScene`。
2. 运行游戏，左上角 HUD 应显示 `MP 当前/最大` 和蓝条。
3. 不同职业进入游戏时，MP 应读取 `CharacterDefine.json` 中对应职业的 `mp`。
4. 在后续技能或测试入口中调用 `TrySpendPlayerManaCommand(cost)`，蓝量足够时 MP 会下降。
5. 蓝量不足时，Command 返回 `false`，MP 不应变化。
6. 按 `Tab` 打开属性面板，顶部摘要应显示 HP、MP 和 EXP。

### 6. 面试表达

我把魔法值做成玩家运行时模型的一部分，而不是直接写在技能脚本或 UI 里。职业配置里本来就有 `mp` 字段，玩家初始化时会读取它作为最大魔法值。技能释放前通过 `TrySpendPlayerManaCommand` 进入资源系统，系统判断蓝量是否足够，成功后扣除 MP 并发送事件刷新 HUD。这样技能逻辑只依赖统一接口，不关心 UI 怎么显示，也不会绕过数据层直接改属性。

### 7. 面试追问

1. **为什么技能不直接改 CurrentMp？**  
   因为直接改会绕过事件、UI 刷新和后续同步逻辑，资源变化应该集中在系统里处理。

2. **为什么要有 CanSpend 和 TrySpend 两个入口？**  
   `CanSpend` 只检查，适合按钮置灰；`TrySpend` 会真实扣蓝，适合释放技能时调用。

3. **为什么魔法值放在 PlayerModel？**  
   PlayerModel 是玩家运行时权威数据，UI、技能、装备都应该读取或请求修改这里的数据。

4. **蓝量变化后 UI 怎么刷新？**  
   `PlayerResourceSystem` 扣蓝或回蓝成功后发送 `PlayerStatsChangedEvent`，HUD 收到事件后重新读取快照。

5. **以后装备增加最大魔法怎么做？**  
   可以增加 `BonusMaxMp`，修改后调用 `RecalculateMaxMp`，再发送属性变化事件。

### 8. 本次涉及知识点

- 玩家运行时资源建模
- Command / Query 分离
- 技能资源消耗接口设计
- 事件驱动 UI 刷新
- 职业配置数据接入运行时模型
- HUD 蓝条与数据层解耦
## 功能名称：直接运行玩法场景时自动补齐职业配置管理器

### 1. 实现目标
解决直接打开 `MainScene` 测试刺客时，因没有经过登录和选角场景，导致 `CharacterDataManager.Instance` 为空、无法读取 `CharacterDefine.json` 的问题。修复后，玩法场景可以独立启动，用 `fallbackClassId` 直接生成指定职业，方便测试移动、攻击和动画表现。

### 2. 涉及脚本
- `GameplayCharacterSpawner`：在生成玩家前检查并补齐 `CharacterDataManager`。
- `CharacterDataManager`：负责读取 `Resources/Data/CharacterDefine.json`，提供 `classId` 到职业配置的查询。
- `GameplayCharacterManager`：根据选角数据或 `fallbackClassId` 获取职业配置并创建运行时角色数据。

### 3. 调用流程
```text
MainScene Play
-> GameplayCharacterSpawner.Start
-> EnsureCharacterDataManager
-> CharacterDataManager 读取 CharacterDefine.json
-> EnterSelectedCharacter
-> GameplayCharacterManager.EnterCurrentCharacter
-> 根据 fallbackClassId 找到 Assassin 配置
-> 创建 PlayerRuntime + Assassin 职业模型
```

### 4. 核心原理
正式流程中，玩家会先进入登录和选角场景，这些场景会提前创建 `CharacterDataManager` 并通过 `DontDestroyOnLoad` 保留下来。直接打开 `MainScene` 时，这个初始化步骤被跳过了，所以角色生成器查不到职业配置。

这次修补是在 `GameplayCharacterSpawner.Start` 里做一次轻量检查：如果已经有 `CharacterDataManager`，就继续使用；如果没有，就自动创建一个。这样不影响正式流程，同时让玩法场景可以独立测试。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`。
2. 选中 `GameplayCharacterSpawner`。
3. 将 `Fallback Class Id` 设置为 `4`。
4. Clear Console 后点击 Play。
5. 正常结果：Console 不再出现 `CharacterDataManager is missing`，场景中生成刺客角色。

### 6. 面试表达
为了提升开发效率，我让玩法场景支持独立启动。正式流程里职业配置管理器由前置场景创建并跨场景保留，但开发时直接打开玩法场景会跳过这一步，所以我在角色生成器启动时做了一个兜底检查：如果没有 `CharacterDataManager`，就自动创建并读取职业配置。这样既不破坏正式登录选角流程，又能快速测试某个职业的移动、攻击和动画。

### 7. 面试追问
1. **为什么不直接把职业配置写死在 Spawner 里？**  
   因为职业数据属于配置层，Spawner 只负责生成角色，不应该维护战士、法师、刺客的具体数值。
2. **为什么只在缺失时创建 CharacterDataManager？**  
   正式流程中它已经存在，如果重复创建会出现多份配置和单例冲突。
3. **为什么用 `fallbackClassId`？**  
   它是开发测试的兜底职业 ID，没有选角数据时也能生成一个临时角色。
4. **这个修复会不会影响服务器登录？**  
   不会。登录和选角流程已有管理器时，检查会直接返回。
5. **后续更完整的做法是什么？**  
   可以做一个 `GameBootstrapper`，统一补齐配置、网络、本地测试角色等全局启动依赖。

### 8. 本次涉及知识点
- Unity 场景直接启动和正式流程启动的差异
- `DontDestroyOnLoad` 全局对象
- 单例初始化顺序
- Resources 配置加载
- 开发测试兜底逻辑与正式流程解耦
## 功能名称：HUD Scrollbar 资源条与刺客翻滚恢复

### 1. 实现目标
把主界面的血量、魔法值、经验值和体力值从 `Image.fillAmount` 改成 `Scrollbar` 驱动的资源条，并且在数值变化时平滑过渡。恢复刺客翻滚输入，让右键或 LeftAlt 可以触发翻滚，移动中按输入方向滚，没有方向输入时按角色当前朝向向前滚。

### 2. 涉及脚本
- `PlayerHudUi`：读取玩家属性和移动组件体力数据，控制四个 `Scrollbar` 的显示百分比。
- `InputCo`：统一采样移动、攻击和翻滚输入，并通过 `RollDown` 暴露给玩法组件。
- `IGameplayInput`：新增 `RollDown` 只读属性，让移动组件不直接依赖具体输入脚本。
- `PlayerMovementComponent`：使用 `RollDown` 触发翻滚，消耗体力，并在没有方向输入时使用角色前方作为翻滚方向。
- `GameplayUiRootMigration`：编辑器工具同步改成自动绑定和修复 HUD 的 `Scrollbar`。
- `GameplayUiRoot.prefab`：四个资源条对象新增 `Scrollbar` 组件，Fill 图片改为普通图片。

### 3. 调用流程
```text
玩家血量/蓝量/经验变化
-> PlayerStatsChangedEvent
-> PlayerHudUi.RefreshPlayerStats
-> 设置 HP/MP/EXP 目标百分比
-> PlayerHudUi.Update
-> Scrollbar.size 平滑追到目标值

玩家翻滚
-> InputCo.Update 采样右键或 LeftAlt
-> IGameplayInput.RollDown
-> PlayerRuntimeController.Update
-> PlayerMovementComponent.TryStartRoll
-> ConsumeStamina
-> PlayerPresentationComponent.PlayRoll
-> CharacterController 按 rollDirection 位移
```

### 4. 核心原理
`Scrollbar` 自带 `value` 和 `size` 两个概念。普通滚动条里 `value` 表示把手位置，`size` 表示把手长度；资源条更像“从左往右显示百分比”，所以这里固定 `value = 0`，用 `size = 当前百分比` 控制 Fill 宽度。这样血量减少时 Fill 变短，回蓝或恢复体力时 Fill 变长。

翻滚逻辑分成输入层和移动层：`InputCo` 只负责告诉系统“这一帧按下了翻滚键”，`PlayerMovementComponent` 负责判断是否攻击中、是否正在翻滚、体力是否足够，再真正进入翻滚状态。这样后续要换键位、接手柄或做技能闪避，都不用改核心移动逻辑。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`。
2. 运行游戏，观察 HUD 里的 HP、MP、SP、EXP 四个条是否能显示。
3. 让角色获得经验、受伤、回蓝或消耗体力时，资源条应该平滑变长或变短。
4. 选择刺客，按住 WASD 后按鼠标右键，角色应按输入方向翻滚并消耗体力。
5. 不按 WASD 时按鼠标右键或 LeftAlt，角色应向当前面朝方向翻滚。
6. 如果翻滚动作不播放，优先检查刺客模型 Animator 是否使用 `Assets/Ani/Player.controller`，并确认有 `Roll`、`RollX`、`RollY` 参数。

### 6. 面试表达
这次我把 HUD 资源条从直接操作 `Image.fillAmount` 改成了 `Scrollbar` 方案。因为 Scrollbar 的 `size` 可以表示当前百分比，所以我固定 `value` 为 0，用 `size` 控制填充区域，并在 Update 里用 `MoveTowards` 做平滑过渡。翻滚这块我没有让移动组件直接读 `Input.GetMouseButtonDown`，而是把右键输入放进 `IGameplayInput.RollDown`，移动组件只关心“是否请求翻滚”和“体力是否足够”。这样输入、移动、动画表现之间的职责比较清楚，也方便之后扩展闪避技能或手柄输入。

### 7. 面试追问
1. **为什么 Scrollbar 资源条用 size 而不是 value？**  
   因为 `value` 表示把手位置，`size` 才表示把手长度。资源条需要改变长度，所以用 `size` 更合适。
2. **为什么不继续用 Image.fillAmount？**  
   `fillAmount` 也能做资源条，但这次需求指定想要 Scrollbar 形式；同时 Scrollbar 在 Inspector 里更直观，可以直接看到资源百分比对应的组件。
3. **为什么输入要抽成 IGameplayInput？**  
   为了让移动和战斗逻辑不依赖具体输入实现，后续换新输入系统、手柄或网络回放时，只需要替换输入层。
4. **为什么翻滚不再强制要求有方向输入？**  
   玩家按下翻滚键时如果完全没反馈，手感会很差；没有方向输入时默认向角色前方滚，更符合直觉。
5. **体力条为什么还在 Update 刷新？**  
   体力会因为跑步、翻滚和自动恢复连续变化，如果每一点变化都发事件会让移动组件变复杂，所以 HUD 只缓存当前移动组件并轻量查询。

### 8. 本次涉及知识点
- Unity UI `Scrollbar` 的 `value` 与 `size`
- UI 数据变化平滑过渡
- 输入层接口抽象
- Unity 脚本执行顺序 `DefaultExecutionOrder`
- CharacterController 翻滚位移
- 体力消耗与恢复流程

## 功能名称：角色属性面板 Row 布局调整

### 1. 实现目标
把角色属性面板里的属性小绿框调整得更协调：缩小 `Rows` 容器宽度，降低每一行属性框高度，让属性行总高度不超过背景框；同时把属性名称和属性数值统一改成黑色、20 号字、居中显示。

### 2. 涉及脚本
- `GameplayUiRoot.prefab`：属性面板的 UI 结构，包含 `PlayerAttributePanel`、三个 `Rows` 容器和所有 `Row_xxx` 属性行。
- `PlayerAttributePanel`：运行时刷新属性数据，并通过 `valueColor` 控制数值文字颜色。
- `PlayerAttributeRowView`：单行属性显示组件，负责把 `PlayerAttributeEntry` 的名称和值写入对应 Text。

### 3. 调用流程
```text
玩家按 Tab 打开属性面板
-> PlayerAttributePanel.RefreshView
-> GetPlayerAttributeEntriesQuery 读取属性数据
-> PlayerAttributeRowView.SetContent
-> Label / Value Text 显示属性名称和数值
-> Horizontal Layout Group 控制一行内的文字排布
-> Vertical Layout Group 控制多行属性从上到下排列
```

### 4. 核心原理
这次不是改属性数值逻辑，而是改 UI 布局。`Rows` 是属性行的父容器，它的宽度决定每个小绿框能占多宽；每个 `Row_xxx` 自己的 `RectTransform Height` 决定一行有多高。因为这些 UI 使用了 `Vertical Layout Group` 和 `Horizontal Layout Group`，所以不能只靠手动拖动子物体，应该通过父物体的 Layout Group 和子物体的 Layout Element 控制布局。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，展开 `GameplayUiRoot -> PlayerAttributePanel -> Content`，检查三个 `Rows` 容器宽度是否为 280，所有 `Row_xxx` 高度是否为 64。运行游戏后按 `Tab` 打开属性面板，确认属性文字是黑色、字号 20，并且在小绿框中居中显示。

### 6. 面试表达
这个属性面板我没有用手动拖拽每个文字的方式调整，而是利用 Unity 的自动布局系统处理。外层 `Rows` 负责控制属性行整体宽度，`Vertical Layout Group` 负责纵向排列每一行，单个 `Row` 里的 `Horizontal Layout Group` 负责让属性名和属性值在框内居中。这样后续新增属性行时，只要按同样结构添加 Row，就能保持统一排版。

### 7. 面试追问
1. **为什么有 Layout Group 时不直接改子物体位置？**  
   因为子物体的位置和大小会被父物体 Layout Group 重新计算，手动改 RectTransform 很容易被覆盖。
2. **Rows 宽度和 Row 高度分别控制什么？**  
   `Rows` 宽度控制整组属性行的横向范围，`Row` 高度控制每一个小绿框的高度。
3. **为什么要同步改 valueColor？**  
   因为运行时 `PlayerAttributePanel` 会刷新数值颜色，如果只改 Text 组件，运行时可能被脚本刷回原来的颜色。
4. **如果以后属性变多怎么办？**  
   可以继续让 `Rows` 使用自动布局，也可以加 Scroll View，让属性行超过背景高度时滚动显示。
5. **这类 UI 调整最容易出什么问题？**  
   最常见是父物体 Layout Group、子物体 Layout Element 和 Content Size Fitter 互相抢控制权，导致手动改尺寸不生效。

### 8. 本次涉及知识点
- Unity UI 自动布局
- `Vertical Layout Group`
- `Horizontal Layout Group`
- `Layout Element`
- `RectTransform`
- Text 颜色、字号和对齐
- Prefab 与场景 Override 的关系

## 功能名称：角色属性行背景透明度修复

### 1. 实现目标
解决属性面板运行后小绿框透明度自动变成 15 的问题。修复后属性行背景在普通状态和高亮状态下都会保持不透明，也就是 Unity Color 面板里的 A 为 255。

### 2. 涉及脚本
- `PlayerAttributePanel`：保存属性行普通颜色 `rowColor` 和变化高亮颜色 `rowHighlightColor`。
- `PlayerAttributeRowView`：在刷新属性行时把 `rowColor` 应用到每一行的背景 Image。
- `GameplayUiRoot.prefab`：保存当前属性面板实例的序列化颜色值。

### 3. 调用流程
```text
PlayerAttributePanel.RefreshView
-> PlayerAttributeRowView.SetContent
-> ApplyHighlight
-> background.color = rowColor
-> 属性行背景 Image 使用脚本传入的颜色
```

### 4. 核心原理
Unity 的颜色 Alpha 在代码里是 0 到 1，在 Inspector 的颜色面板里是 0 到 255。原来的 `rowColor.a = 0.06`，换算成 255 制就是 15 左右，所以一进游戏脚本刷新 UI 时，属性框就会变得很透明。把 Alpha 改成 `1f` 后，对应 Inspector 里的 255。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，运行游戏后按 `Tab` 打开属性面板。选中任意 `Row_xxx`，查看 Image 组件的 Color，A 应该保持 255，不会再变成 15。

### 6. 面试表达
这个问题是 UI 表现被脚本刷新覆盖了。属性行背景不是只看 Inspector 当前颜色，运行时 `PlayerAttributePanel` 会把 `rowColor` 传给每个 `PlayerAttributeRowView`，再设置到背景 Image 上。原来 `rowColor` 的 Alpha 是 0.06，所以运行后透明度变成 15。我把脚本默认值和 Prefab 序列化值都改成了 Alpha 1，保证运行时刷新也保持不透明。

### 7. 面试追问
1. **为什么 Inspector 里手动调到 255，运行后还会变？**  
   因为运行时脚本又执行了一次 `background.color = rowColor`，覆盖了手动调的颜色。
2. **为什么要同时改脚本和 Prefab？**  
   现有 Prefab 会保存序列化值，如果只改脚本默认值，已经存在的组件可能仍然使用 Prefab 里的旧值。
3. **Unity Color 的 Alpha 为什么代码里是 1，面板里是 255？**  
   代码使用 0 到 1 的浮点数，Inspector 可以显示成 0 到 255 的颜色通道。
4. **高亮颜色为什么也要改 Alpha？**  
   否则属性值变化高亮时，背景会短暂变半透明。
5. **如何排查这种 UI 颜色被改的问题？**  
   全局搜索 `background.color`、`Image.color` 或相关字段名，看运行时哪里在写颜色。

### 8. 本次涉及知识点
- Unity `Color` 的 0 到 1 表示
- Inspector 颜色 Alpha 0 到 255 显示
- UI Image 颜色会被脚本运行时覆盖
- Prefab 序列化值与脚本默认值的区别

## 功能名称：属性变化高亮颜色改为淡红色

### 1. 实现目标
把属性面板中属性值变化时的高亮提示色从深绿色/黄色改成淡红色，让变化提示更明显，同时不影响普通状态下属性框的颜色。

### 2. 涉及脚本
- `PlayerAttributePanel`：保存属性行高亮颜色 `rowHighlightColor`。
- `PlayerAttributeRowView`：根据 `highlightTimer` 判断是否使用高亮颜色。
- `GameplayUiRoot.prefab`：保存当前 UI Prefab 的高亮颜色序列化值。

### 3. 调用流程
```text
属性值变化
-> PlayerAttributeRowView.SetContent 检测 lastValue 不同
-> 设置 highlightTimer
-> ApplyHighlight
-> background.color = rowHighlightColor
-> 高亮时间结束后恢复 rowColor
```

### 4. 核心原理
属性行高亮不是单独的动画组件，而是通过代码在普通背景色和高亮背景色之间切换。`rowHighlightColor` 决定变化提示期间的颜色，`valueHighlightDuration` 决定持续时间。因为 Prefab 会保存 Inspector 中的颜色值，所以这类 UI 效果通常要同时修改脚本默认值和 Prefab 序列化值。

### 5. Unity 测试方式
运行 `MainScene`，按 `Tab` 打开属性面板。刚发生变化的属性行应该短暂显示淡红色，然后恢复普通颜色。

### 6. 面试表达
属性变化提示我用的是一个轻量的颜色高亮机制。每个属性行会记录上一次显示的值，如果新值不同，就启动一个高亮计时器。在计时器结束前，背景 Image 使用 `rowHighlightColor`，结束后恢复普通 `rowColor`。这样不用额外做动画状态机，也能让玩家快速注意到属性变化。

### 7. 面试追问
1. **为什么高亮颜色要放在 Panel 上？**  
   因为这是整个属性面板统一的表现配置，放在 Panel 上方便统一调整。
2. **为什么不在每个 Row 上单独配颜色？**  
   当前所有属性行使用同一种高亮样式，统一配置更简单，也避免每行样式不一致。
3. **高亮持续时间在哪里控制？**  
   `PlayerAttributePanel` 的 `valueHighlightDuration` 控制持续时间。
4. **为什么要同步修改 Prefab？**  
   已经存在的 UI 组件会优先使用 Prefab 保存的序列化值，只改脚本默认值可能不生效。
5. **后续想做更好看的高亮怎么办？**  
   可以把颜色瞬变改成渐变，或者用 Tween/Animation 做淡入淡出。

### 8. 本次涉及知识点
- UI 状态高亮
- 颜色配置字段
- Prefab 序列化值
- 运行时 UI 表现刷新

## 功能名称：小地图系统第一版

### 1. 实现目标
这次完成了玩法场景中的小地图功能。第一版使用独立的正交相机从玩家上方俯视拍摄场景，并把画面输出到 RenderTexture，再通过 UI 的 RawImage 显示在屏幕右上角。随后又补充了玩家方向箭头、怪物红点和宝箱黄点，让玩家可以快速判断自己、敌人和目标物的大致位置。

### 2. 涉及脚本
- `MiniMapCameraController`：控制小地图相机跟随当前玩家，并保持俯视角度。
- `MiniMapPlayerIcon`：控制小地图中心的玩家箭头旋转，使箭头方向和玩家朝向一致。
- `MiniMapIconTarget`：挂在怪物、宝箱等世界对象上，负责把自己注册为小地图标记目标。
- `MiniMapIconRenderer`：挂在小地图 UI 图标层上，负责把世界坐标转换成小地图里的 UI 坐标，并生成红点、黄点等标记。
- `GameplayRuntime`：提供当前玩家引用，避免小地图脚本在运行时频繁查找玩家对象。
- `GameplayUiRoot.prefab`：承载小地图 RawImage、玩家箭头和图标层。

### 3. 调用流程
```text
PlayerRuntimeController.Awake
-> GameplayRuntime.RegisterPlayer
-> MiniMapCameraController 监听 CurrentPlayerChanged
-> 小地图相机跟随玩家位置
-> MiniMapCamera 渲染到 MiniMapRT
-> MiniMapView(RawImage) 显示 MiniMapRT
```

```text
Slime / Box 启用
-> MiniMapIconTarget.OnEnable 注册到 ActiveTargets
-> MiniMapIconRenderer.Update 遍历 ActiveTargets
-> 世界坐标 - 玩家坐标 = 相对位置
-> 相对位置换算成 MiniMapIconLayer 上的 anchoredPosition
-> 显示红点或黄点
```

### 4. 核心原理
小地图可以理解成两层：底图层和图标层。底图层由一个专门的小地图相机负责，它从玩家头顶往下看，把看到的画面渲染到 `MiniMapRT`，UI 的 `RawImage` 再显示这张纹理。图标层不依赖相机拍摄，而是用 UI 图标显示怪物和宝箱的位置，这样红点、黄点会更清晰，也方便以后扩展任务点、传送门、NPC 标记。

玩家是运行时生成的，所以小地图脚本没有直接在 Inspector 里拖玩家引用，而是监听 `GameplayRuntime.Instance.CurrentPlayerChanged`。这样角色生成完成后，小地图相机、玩家箭头和图标渲染器都会自动拿到当前玩家。这个设计避免了运行时频繁 `FindObjectOfType`，也让小地图和角色生成流程保持解耦。

怪物红点的坐标换算核心是：先用“怪物世界坐标 - 玩家世界坐标”得到怪物相对玩家的位置，再根据小地图相机的 `Orthographic Size` 算出每个世界单位对应多少 UI 像素，最后设置图标的 `anchoredPosition`。如果红点超出小地图范围，`RectMask2D` 会把它裁剪掉，避免图标跑到小地图外面。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，运行游戏并进入玩法场景。观察右上角小地图是否显示场景俯视画面，移动玩家时小地图画面应该跟随玩家移动。旋转玩家时，小地图中心的箭头应该同步旋转；如果箭头默认图片朝右，需要在 `MiniMapPlayerIcon` 中设置 `iconRotationOffset = 90`。

继续观察怪物和宝箱标记：`Slime1.prefab`、`Slime2.prefab` 挂上 `MiniMapIconTarget` 后，运行时怪物附近应该显示红点；`Box.prefab` 或金库对象挂上 `MiniMapIconTarget` 后，应该显示黄色点。检查 `MiniMapIconRenderer` 的 `orthographicSize` 是否和 `MiniMapCamera` 的 Camera Size 一致，否则图标位置会和小地图画面对不上。

### 6. 面试表达
这个小地图我分成了底图渲染和图标标记两部分。底图用一个独立的正交相机从玩家上方俯视，把画面渲染到 RenderTexture，再用 RawImage 显示到 HUD 上。玩家对象是运行时生成的，所以我没有在场景里硬拖引用，而是通过 GameplayRuntime 监听当前玩家变化来绑定目标。怪物和宝箱标记没有直接靠相机拍出来，而是挂一个 MiniMapIconTarget 注册自己，再由 MiniMapIconRenderer 把世界坐标转换成小地图 UI 坐标。这样做的好处是结构清楚，显示效果稳定，后续扩展任务点、NPC、传送门或大地图都比较方便。

### 7. 面试追问
1. **为什么小地图用正交相机？**  
   因为正交相机没有近大远小的透视变形，俯视地图时比例更稳定，玩家更容易判断位置关系。
2. **实时小地图会不会影响性能？**  
   会有额外消耗，因为相当于多渲染了一次场景。所以可以降低 RenderTexture 分辨率、限制 Culling Mask、关闭后处理和阴影，必要时把小地图相机改成低频刷新。
3. **为什么怪物红点不直接让相机拍？**  
   直接拍 3D 对象可能不够清晰，也不方便统一控制颜色和大小。用 UI 图标可以让标记更稳定，后续扩展也更简单。
4. **为什么用注册列表，而不是每帧查找怪物？**  
   每帧查找对象会产生不必要的性能开销。`MiniMapIconTarget` 在启用时注册、禁用或销毁时注销，小地图只遍历当前有效目标。
5. **图标位置是怎么从世界坐标换成 UI 坐标的？**  
   先计算目标相对玩家的世界偏移，再用小地图 UI 高度除以相机可视世界高度，也就是 `orthographicSize * 2`，得到像素和世界单位的换算比例，最后设置 UI 图标的 `anchoredPosition`。

### 8. 本次涉及知识点
- Unity Camera 正交投影
- RenderTexture 与 RawImage 显示
- UI `RectTransform` 锚点和 `anchoredPosition`
- `RectMask2D` 裁剪子物体
- 世界坐标到 UI 坐标转换
- 运行时对象注册与注销
- 事件监听和取消监听
- 小地图性能优化思路
- UI 表现层和玩法对象解耦

## 功能名称：刺客攻击时可移动

### 1. 实现目标
本次解决刺客普攻时角色被锁在原地的问题。修改后，刺客点击鼠标左键攻击时仍然可以通过 WASD 水平移动；为了保持动作优先级，攻击中仍然不允许翻滚，并且暂时限制起跳，避免普攻和跳跃同时触发导致动作表现发飘。

### 2. 涉及脚本
- `PlayerRuntimeController`：新运行时玩家主调度入口，攻击后调用移动时不再把 `IsAttacking` 当作水平移动阻塞。
- `PlayerMovementComponent`：把“完全阻塞移动”和“只阻塞跳跃”拆成两个参数。
- `PlayerPresentationComponent`：根据 `ComboIndex` 动态开关刺客 `Attack Layer` 权重。
- `Player.controller`：刺客 Animator Controller 新增 `Attack Layer`，移动和攻击不再抢同一个 Base Layer。
- `AssassinUpperBodyAttack.mask`：攻击层使用的上半身 AvatarMask。

### 3. 调用流程
```text
鼠标左键
-> PlayerCombatComponent.Tick
-> SetCombo(1/2/3)
-> PlayerPresentationComponent 写入 ComboIndex 并打开 Attack Layer
-> Animator 的 Attack Layer 播放 Atk1/Atk2/Atk3
```

```text
WASD 输入
-> PlayerRuntimeController.Update
-> PlayerMovementComponent.TickNormalMovement(false, combat.IsAttacking)
-> Move()
-> CharacterController.Move
-> Base Layer 继续播放 WalkBlend / RunBlend
```

### 4. 核心原理
原来的问题有两层：脚本层把 `combat.IsAttacking` 传给移动组件，导致攻击时不执行 `Move()`；动画层把移动和攻击都放在 Base Layer，进入攻击状态后移动状态也会被切走。

这次把逻辑拆开：脚本上允许攻击中继续计算水平速度，只把攻击状态当作跳跃限制；动画上让 Base Layer 专门负责移动，Attack Layer 专门负责攻击。这样玩家输入移动时，角色位置仍然由 `CharacterController` 推动，Animator 的移动层也能继续响应走跑参数，攻击动作则通过独立层叠加上去。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，进入游戏并选择刺客。按住 `WASD` 移动，同时点击鼠标左键攻击。正常结果是刺客仍然能移动，并能播放普攻和三段连击。再按住 `Shift` 测试跑动攻击，观察角色是否继续移动；攻击中按空格或右键时，默认不应该起跳或翻滚。

还需要打开 `Assets/Ani/Player.controller` 检查 Animator：`Base Layer` 应只负责移动、跳跃、翻滚等状态，`Attack Layer` 中有 `Empty / Atk1 / Atk2 / Atk3`，并使用 `AssassinUpperBodyAttack.mask`。

### 6. 面试表达
这个问题我分脚本层和动画层一起处理。脚本层原来把攻击状态当作移动阻塞，所以攻击时不会执行水平移动；我把它拆成了两个概念，一个是是否完全禁止移动，一个是是否只禁止跳跃。这样普攻时还能走位，但不会同时触发跳跃或翻滚。动画层原来攻击和移动都在 Base Layer，状态互斥，所以我新增了 Attack Layer，把三段攻击放到独立层，用 AvatarMask 叠加到上半身，Base Layer 继续处理走跑。这样设计的好处是移动、攻击职责更清楚，后面扩展技能、蓄力攻击或者不同职业动作也更容易。

### 7. 面试追问
1. **为什么不能只改脚本？**  
   只改脚本可以让角色位置移动，但 Animator 仍然停在攻击状态，视觉上容易变成滑动攻击；状态机分层后，移动层和攻击层可以同时工作。

2. **为什么攻击中不开放翻滚？**  
   翻滚是高优先级动作，会打断攻击节奏和碰撞判定。当前先保持攻击中不能翻滚，后续如果要做取消后摇，可以专门设计“攻击取消窗口”。

3. **为什么用 Attack Layer？**  
   因为移动和攻击是两个可以同时存在的表现需求。Base Layer 负责下半身移动，Attack Layer 负责上半身攻击，比把所有状态塞到一个层里更清晰。

4. **AvatarMask 的作用是什么？**  
   AvatarMask 用来限制某个动画层影响哪些骨骼。这里攻击层主要影响上半身和武器，避免它完全覆盖腿部走跑动作。

5. **如果攻击动画和移动动画混合后穿模怎么办？**  
   可以调整 AvatarMask 覆盖范围、攻击层权重、动画过渡时间，必要时给移动攻击单独做动画资源。

### 8. 本次涉及知识点
- `CharacterController.Move` 和 Animator 状态互不等价
- 输入层、移动逻辑、战斗逻辑、动画表现的职责拆分
- Animator Base Layer 和额外 Layer
- AvatarMask 上半身动画叠加
- `Animator.SetLayerWeight`
- 普攻连击 `ComboIndex`
- 动作优先级：移动、跳跃、翻滚、攻击

### 9. Bug 修复补充：攻击动画不播放
分层后攻击动画位于 `Attack Layer`，该层默认权重是 0。正确修复方向不是让旧脚本继续兼容，而是把动画参数统一收口到 `PlayerPresentationComponent.SetCombo`：`PlayerCombatComponent` 只负责判断当前攻击段数，不直接操作 `Animator`、`ComboIndex` 或 `Attack Layer`。`SetCombo` 在攻击开始时打开攻击层权重并写入 `ComboIndex`，重置连击时关闭攻击层权重。`AssassinUpperBodyAttack.mask` 的根路径权重保持为 1，避免 Transform Mask 从根节点把整层过滤掉。

如果攻击时已经有声音和伤害，但模型没有明显动作，说明攻击 Clip 大概率已经播放并触发了动画事件，问题更可能出在 Layer Mask 把骨骼输出过滤掉。本次先把 `Player.controller` 的 `Attack Layer` Mask 设为 None，让攻击动画以全身层输出，优先恢复可见攻击动作；后续如果要做到更精细的“上半身攻击 + 下半身跑步”，再重新制作一份确认骨骼路径正确的 AvatarMask。

### 10. PlayerCo 解耦补充
本次补充检查了 `PlayerCo.cs` 以外的脚本引用，确认新运行链路不再依赖 `PlayerCo`。现在攻击移动链路是 `GameplayCharacterSpawner -> PlayerRuntimeController -> PlayerCombatComponent / PlayerMovementComponent -> PlayerPresentationComponent`。即使 `PlayerCo` 被注释或后续删除，攻击输入、攻击动画、攻击时移动和武器碰撞盒仍由 `PlayerRuntime` 这套组件独立完成。

### 11. 动画过渡和攻击限速补充
本次继续优化刺客攻击时的手感：`Player.controller` 中所有状态切换都保留非 0 的 `Transition Duration`，攻击段回到 `Empty` 时开启 `Has Exit Time`，避免 `ComboIndex` 归零后立刻切掉收刀动作。`PlayerPresentationComponent.SetCombo(0)` 不再立即关闭 `Attack Layer`，而是先让状态机过渡，再通过 0.55 秒延迟和淡出把攻击层权重降到 0。

移动方面，`PlayerRuntimeController` 在攻击中给 `PlayerMovementComponent` 传入 `AttackMoveSpeedLimit`，`Move` 方法最终用 `Mathf.Min` 限制水平速度，默认上限为 3。这样攻击时仍然可以走位，但不会一边播放攻击动作一边高速奔跑，动作表现会更稳。

### 12. 跑步状态和 RunBlend 补充
本次修复了刺客跑步相关的三个表现问题。脚本层在 `PlayerMovementComponent.Move` 中增加落地判断，只有 `CharacterController.isGrounded` 为 true 时才允许进入 `isRunning`，空中按住 Shift 只保留普通水平移动，不再切到跑步动画。

动画参数层在 `PlayerPresentationComponent.SetMovement` 中把 `WalkBlend` 和 `RunBlend` 的方向参数保持同步：移动时两套参数都写入当前输入方向，停止移动时 `WalkBlend` 回到 `(0,0)` 播放待机，但 `RunBlend` 保留最后一次有效方向。这样走跑互切的过渡帧不会因为 `SpeedX_Run/SpeedY_Run` 瞬间变成 `(0,0)` 而采样出错误的左前方跑步。

状态机层给 `RunBlend` 增加了一个 `(0,0)` 中心点，使用正向跑步动作作为兜底；同时把跳跃落地进入跑步的 `Transition Offset` 归 0，避免从跑步动画中段切入。这个问题的核心经验是：2D BlendTree 如果没有中心 Motion，就不要在过渡中把参数写成 `(0,0)`，否则 Unity 会根据周围 Motion 做不符合预期的混合。

### 13. 攻击结束后立刻跑步补充
本次修复了“攻击结束后立刻跑步，速度已经上来但动作还卡在攻击最后姿势”的问题。原因是攻击动画位于独立 `Attack Layer`，普通站立结束时为了保留收刀动作，脚本会延迟淡出攻击层；但玩家立刻进入跑步时，移动速度已经由 `Base Layer` 接管，攻击层如果继续盖在上面，就会出现速度和动作不同步。

修复思路是保留两套出口：站立不跑时，`Atk1/Atk2/Atk3 -> Empty` 仍然等待 `Has Exit Time`，让收刀动作自然播完；如果 `IsRunning == true`，攻击层过渡不再等待 Exit Time，而是用很短的 0.06 秒过渡退出。同时 `PlayerPresentationComponent.SetMovement` 在检测到跑步并且攻击层正在淡出时，会清掉淡出延迟，用 0.06 秒快速降低 `Attack Layer` 权重。

这个设计可以理解成“跑步取消攻击后摇”：玩家不移动时保留动作完整性，玩家跑步时优先响应移动手感。面试里可以说明这是动作游戏常见的状态优先级处理，既保证表现完整，又避免输入响应滞后。

## 功能名称：三段攻击命中判定修复

### 1. 实现目标
本次修复“三段攻击碰撞体每次都出现，但伤害只触发一次”的问题。原因是武器伤害原来只依赖 `OnTriggerEnter`，当敌人一直停留在攻击范围内时，后续攻击窗口不一定会再次产生 Enter 事件。修复后，每一次 `WeaponEnable` 都会生成新的攻击判定窗口，同一窗口内同一目标只受伤一次，下一段攻击可以重新造成伤害。

### 2. 涉及脚本
- `PlayerCombatComponent`：记录当前攻击判定窗口编号，每次打开武器碰撞盒时递增。
- `WeaponCo`：统一处理 `OnTriggerEnter` 和 `OnTriggerStay`，并用 `HashSet` 记录本次窗口已经命中的目标。

### 3. 调用流程
```text
攻击动画事件 WeaponEnable
-> PlayerCombatComponent.AttackHitWindowId + 1
-> 武器碰撞盒启用
-> WeaponCo.OnTriggerEnter / OnTriggerStay
-> 判断当前攻击窗口是否变化
-> 清空本窗口命中过的目标
-> 同一窗口未命中过则调用 FighterInterface.Hit
-> PlayerCombatComponent.HandleDamageDealt 处理吸血等后结算
```

### 4. 核心原理
`OnTriggerEnter` 只代表“刚进入触发器”，不代表“每次攻击都会触发”。连击攻击通常会反复开关同一个武器碰撞盒，如果敌人一直站在范围里，Unity 可能不会在第二、三段攻击时再次派发 Enter。  
所以这里给每次攻击开启一个“判定窗口编号”，`WeaponCo` 发现窗口编号变化后，就清空本窗口的已命中列表。`OnTriggerStay` 负责补上“敌人已经在范围内”的情况，`HashSet` 负责防止同一段攻击每个物理帧都重复扣血。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，选择刺客进入游戏，让史莱姆站在攻击范围内，连续点击鼠标左键打出三段攻击。正常结果是三段攻击都可以各造成一次伤害，但同一段攻击不会连续跳出多次伤害数字。  
如果仍然不扣血，优先检查 `AttackHitbox` 的 `SphereCollider` 是否为 Trigger、是否被 `PlayerCombatComponent.weaponCollider` 引用，以及攻击动画事件里是否真的调用了 `WeaponEnable` 和 `WeaponDisable`。

### 6. 面试表达
这个问题本质上是攻击判定不能只依赖 `OnTriggerEnter`。因为近战连击里敌人可能一直在武器范围内，Unity 不一定会给后续攻击重新派发 Enter 事件。我给每次 `WeaponEnable` 增加了一个攻击窗口编号，武器脚本根据窗口编号清空本次命中缓存；同时用 `OnTriggerStay` 处理已经重叠的目标，再用 `HashSet` 保证同一攻击窗口内同一个敌人只受一次伤害。这样既解决了连击后两段不扣血，也避免了 Stay 每帧重复扣血的问题。

### 7. 面试追问
1. **为什么不用单纯打开关闭 Collider 解决？**  
   因为物理触发事件和 FixedUpdate 时序有关，Collider 重新启用不一定能稳定制造新的 Enter，尤其目标一直重叠时更明显。
2. **为什么要用 `HashSet`？**  
   `OnTriggerStay` 会持续触发，如果不记录本窗口已经命中过的目标，同一段攻击会在多个物理帧里重复扣血。
3. **为什么命中缓存放在 `WeaponCo`？**  
   命中去重属于武器碰撞判定职责，伤害公式仍然放在战斗系统里，职责更清楚。
4. **多敌人同时在范围内怎么办？**  
   `HashSet` 按目标对象去重，不是全局只打一只怪，所以同一窗口可以命中多个不同敌人。
5. **后续扩展技能时怎么做？**  
   可以把“攻击窗口 ID + 命中缓存”抽成通用命中检测模块，让普通攻击、技能范围、子弹穿透都复用这套去重逻辑。

### 8. 本次涉及知识点
- Unity `OnTriggerEnter` 与 `OnTriggerStay` 的区别
- 物理检测和动画事件的时序关系
- 攻击判定窗口设计
- `HashSet` 去重
- 战斗检测和伤害结算的职责分离

## 功能名称：刺客大旋转技能动画与三连击卡住修复

### 1. 实现目标
本次把刺客第三技能“镰刀大旋转”和角色动作表现接起来：按数字键 `3` 成功释放技能后，攻击层播放 `Skill` 状态，状态使用现有 `Atk3.anim`。同时修复刺客三连击换动画后可能卡住的问题，即使新攻击动画漏掉 `ResetCombo` 动画事件，脚本也会在短时间内兜底复位。

### 2. 涉及脚本
- `PlayerPresentationComponent`：新增 `PlaySkill`，负责触发 `Skill` 动画，并管理 Attack Layer 的权重淡出。
- `PlayerSkillCastComponent`：第三技能释放成功后调用表现层播放技能动画，并继续使用 `SkillDefine.json` 中的半径做范围伤害。
- `PlayerCombatComponent`：技能动画期间忽略 `Atk3.anim` 自带的普攻动画事件，避免技能额外打开普通攻击碰撞盒；同时增加攻击事件丢失时的兜底复位。
- `PlayerRuntimeController`：把运行时控制器注入给技能释放组件。
- `Player.controller`：攻击层新增 `Skill` 状态和 `Skill` Trigger 参数。

### 3. 调用流程
```text
按下数字键 3
-> InputCo.Skill3Down
-> PlayerSkillCastComponent.TryCast(2001)
-> PlayerSkillSystem 校验技能是否学会、蓝量和冷却
-> PlayerPresentationComponent.PlaySkill
-> Animator Attack Layer 进入 Skill 状态
-> PlayerSkillCastComponent.CastScytheSpin
-> Physics.OverlapSphere 按配置半径检测敌人
-> FighterInterface.Hit
```

### 4. 核心原理
技能动画和技能伤害分开处理。动画只负责表现，所以 `Skill` 状态复用 `Atk3.anim`；真正的大旋转范围来自 `SkillDefine.json` 的 `radius`，脚本用同一个半径做 `OverlapSphere` 伤害检测和特效缩放。  
因为 `Atk3.anim` 原本是普通攻击动画，里面带有 `WeaponEnable`、`WeaponDisable` 和 `ResetCombo` 事件，所以技能播放期间战斗组件会忽略这些普攻事件，避免一次技能额外触发普通攻击伤害。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，选择刺客进入游戏。先确认第三技能“镰刀大旋转”已经学会，然后按 `3`。正常结果是刺客播放 `Skill` 动作，周围半径内敌人受到技能伤害，并且不会额外触发普通攻击碰撞盒伤害。  
连续点击鼠标左键测试三段普攻，正常结果是即使某个动画事件漏掉，也不会长时间卡在攻击姿势。

### 6. 面试表达
这个技能我分成了“释放规则、动画表现、范围伤害”三部分。按下 3 键后，技能系统先判断是否学会、蓝量和冷却，成功后表现层触发 Animator 的 `Skill` Trigger 播放动作；伤害不是靠动画碰撞盒，而是用配置表里的半径做 `Physics.OverlapSphere`，这样技能等级改变时范围和伤害可以直接由数据驱动。因为技能复用了普通攻击的 `Atk3` 动画，我还在战斗组件里屏蔽了技能期间的普攻动画事件，避免同一个动作同时造成技能伤害和普通攻击伤害。

### 7. 面试追问
1. **为什么技能不用普通攻击碰撞盒？**  
   大旋转是范围技能，半径会随技能等级变化，用配置半径做 OverlapSphere 更直观，也更容易扩展。
2. **为什么要屏蔽 Atk3 的动画事件？**  
   因为 `Atk3.anim` 原本服务于普通攻击，复用成技能动画时不能再让它打开普通攻击碰撞盒。
3. **为什么不用 ComboIndex 触发技能？**  
   `ComboIndex` 是普攻连击状态，技能用独立 Trigger 可以避免技能和普攻状态互相污染。
4. **为什么要脚本兜底复位？**  
   动画事件容易在换资源时丢失，脚本兜底可以保证状态不会因为少一个事件就卡死。
5. **技能范围怎么和表现对应？**  
   技能伤害半径、特效缩放和 Scene 视图调试 Gizmo 都围绕同一个大旋转半径设计，避免表现范围和实际伤害范围不一致。

### 8. 本次涉及知识点
- Animator Trigger 和独立动画状态
- Animator Layer 权重控制
- 动画事件和玩法逻辑解耦
- `Physics.OverlapSphere` 范围技能检测
- 配置表驱动技能半径、伤害和冷却
- 技能表现和技能伤害分离

## 功能名称：升级面板复用为属性选择和技能选择

### 1. 实现目标
本次目标是让玩家在 5、10、15 级时，不仅获得一次技能学习/升级机会，也仍然保留原来的属性三选一机会。
为了避免再新建一套技能选择 UI，本次直接改造原来的 `PlayerLevelUpPanel`，让同一个面板可以先显示属性选择，再显示技能选择。

### 2. 涉及脚本
- `Assets/Script/UI/PlayerLevelUpPanel.cs`：统一显示属性三选一和技能三选一。
- `Assets/Script/Architecture/Systems/PlayerProgressionSystem.cs`：升级时先增加属性选择次数，再在 5 的倍数等级增加技能选择次数。
- `Assets/Script/Architecture/Systems/PlayerSkillSystem.cs`：维护技能选择队列，并处理学习新技能或升级已有技能。
- `Assets/Script/Architecture/Models/PlayerSkillModel.cs`：保存玩家已拥有技能和待处理技能选择次数。

### 3. 调用流程
玩家获得经验 -> `PlayerProgressionSystem.DoLevelUp` -> 增加属性选择队列 -> 触发 `PlayerUpgradeQueueChangedEvent` -> `PlayerLevelUpPanel` 显示属性三选一 -> 玩家点击属性 -> `ResolvePlayerUpgradeCommand` -> 面板再次检查队列 -> 如果 5/10/15 级有技能选择队列，显示技能三选一 -> 玩家点击技能 -> `ResolvePlayerSkillChoiceCommand`。

### 4. 核心原理
这个功能的关键不是“弹两个窗口”，而是把奖励做成两个队列：属性队列和技能队列。
UI 每次刷新时先看属性队列，如果属性还没选完，就显示属性选项；属性选完后再看技能队列，如果技能还没选，就显示技能选项。
这样同一个 UI 面板可以连续处理不同类型的奖励，而且不会把技能规则硬写进属性升级系统。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，运行游戏后通过打怪或临时调高经验让角色升到 5 级。
正常结果是先弹出属性三选一；选择属性后，面板不会立刻关闭，而是继续弹出技能三选一。
10 级和 15 级也应该重复这个流程：先选属性，再选技能学习或升级。

### 6. 面试表达
这个升级奖励 UI 我没有为技能再单独做一套面板，而是把原来的升级三选一面板改造成通用奖励面板。它监听 QFramework 里的属性队列事件和技能队列事件，刷新时先消费属性选择，再消费技能选择。这样 5、10、15 级可以连续弹出两轮选择，但 UI 还是复用同一个 Prefab，逻辑上也保持属性成长和技能成长分开，后续要扩展装备词条、天赋点也可以继续复用这个队列式思路。

### 7. 面试追问
1. **为什么属性和技能不共用同一个数据结构？**  
   因为属性成长和技能成长的规则不同，属性是直接改数值，技能需要判断是否已拥有、是否满级、职业限制等，分开维护更清晰。
2. **为什么 UI 要先显示属性再显示技能？**  
   因为普通升级奖励是每级都有的基础流程，技能是 5 的倍数等级的额外奖励。固定顺序能减少玩家困惑，也方便测试。
3. **为什么用事件通知 UI？**  
   因为 UI 不需要每帧查询玩家等级或队列数量，数据变化时系统发事件，UI 再刷新，性能和结构都更好。
4. **如果以后奖励类型更多怎么办？**  
   可以把属性、技能、天赋、装备奖励都抽象成奖励队列项，UI 只负责渲染当前队列项，具体结算交给对应系统。
5. **为什么不直接在升级系统里打开 UI？**  
   升级系统应该只负责成长规则，UI 显示属于表现层。用事件/命令连接两者，可以降低耦合。

### 8. 本次涉及知识点
- QFramework 的事件、命令、查询用法
- UI 复用和显示状态切换
- 奖励队列设计
- 属性成长系统和技能成长系统解耦
- 升级流程中的暂停、鼠标显示和恢复

## 功能名称：按 P 增加 100 经验调试入口

### 1. 实现目标
本次目标是在玩法场景中按下 `P` 键时，给当前玩家增加 100 点经验。
这个功能主要用于快速测试经验条、升级、升级奖励面板和经验飘字，不需要每次都通过击杀怪物慢慢触发。

### 2. 涉及脚本
- `Assets/Script/Input/IGameplayInput.cs`：新增 `DebugAddExpDown` 输入字段，让输入来源通过接口暴露调试按键。
- `Assets/Script/Input/InputCo.cs`：每帧检测 `KeyCode.P`，把按键结果缓存起来。
- `Assets/Script/Player/PlayerRuntimeController.cs`：读取调试输入，并调用现有 `AddExp(100)` 入口。

### 3. 调用流程
玩家按下 P -> `InputCo.Update` 缓存 `DebugAddExpDown` -> `PlayerRuntimeController.Update` 读取输入 -> `AddExp(100)` -> `AddPlayerExpCommand` -> `PlayerProgressionSystem.AddExp` -> 刷新经验、升级和 UI 事件。

### 4. 核心原理
按键本身属于输入层，经验计算属于成长系统，所以本次没有在 `InputCo` 里直接修改经验。
`InputCo` 只告诉外部“P 这一帧被按下了”，`PlayerRuntimeController` 再把这个调试输入转成“给当前玩家加经验”的请求。
最终经验增加、升级判断、升级面板和飘字仍然走原来的正式流程，这样测试入口不会破坏项目结构。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，运行游戏并等待玩家生成。
观察 HUD 上的经验显示，按下键盘 `P`。
正常结果是经验增加 100，并出现经验飘字；如果经验达到升级需求，会触发原有升级和奖励选择流程。

### 6. 面试表达
我给项目加了一个调试用的经验快捷键，但没有在输入脚本里直接改玩家经验。输入系统只负责缓存 P 键是否按下，玩家运行时控制器读取到这个输入后调用统一的 `AddExp` 入口，真正的经验累加、升级判断和 UI 刷新仍然交给 `PlayerProgressionSystem`。这样做的好处是调试功能复用了正式业务流程，既方便测试，也不会让经验逻辑散落在多个脚本里。

### 7. 面试追问
1. **为什么不在 `InputCo` 里直接加经验？**  
   因为输入层只应该负责采集输入，不应该直接修改玩家成长数据，否则输入、数据和业务规则会耦合在一起。
2. **为什么要加到 `IGameplayInput` 接口？**  
   现有移动和战斗都通过输入接口读取数据，调试按键也走同一套方式，后续换新输入系统或手柄时更容易统一处理。
3. **为什么调用 `AddExp(100)` 而不是直接改 `CurrentExp`？**  
   `AddExp` 后面会触发命令、升级判断、事件和 UI 刷新，直接改字段会绕过这些流程，容易出现经验变了但 UI 或升级没更新的问题。
4. **这个功能正式上线要保留吗？**  
   正式版本一般会移除或包进开发者调试开关里，避免玩家误触或作弊。
5. **如果以后要做调试面板怎么扩展？**  
   可以把 `P` 键替换成调试 UI 按钮或开发者命令，但仍然调用同一个 `AddExp` 入口。

### 8. 本次涉及知识点
- 输入层和业务逻辑分离
- 接口化输入读取
- QFramework Command 流程
- 经验、升级和 UI 事件复用
- 调试功能如何避免破坏正式架构

## 功能名称：技能选择按钮文本换行优化

### 1. 实现目标
解决技能选择按钮里“范围 3”“冷却 8”这类数字被 Unity Text 自动挤到单独一行的问题。
本次没有改 UI Prefab 尺寸，而是缩短技能按钮文本，并在代码里主动控制换行。

### 2. 涉及脚本
- `Assets/Script/Architecture/Systems/PlayerSkillSystem.cs`：调整 `GetSkillChoiceText` 和 `BuildLevelText` 的文本拼接格式。

### 3. 调用流程
`PlayerLevelUpPanel.ShowSkillSelection` -> `GetPlayerSkillChoiceTextQuery` -> `PlayerSkillSystem.GetSkillChoiceText` -> `BuildLevelText` -> 写入按钮 `Text`。

### 4. 核心原理
按钮宽度有限，如果把蓝耗、冷却、伤害倍率、范围、持续时间、技能描述全部拼成很长一句，Unity 的旧版 `Text` 会根据组件宽度自动换行。
自动换行不理解你的排版意图，所以容易把单个数字挤到下一行。
解决方式是删掉按钮里的长描述，把核心数值拆成短行，让代码主动决定哪里换行。

### 5. Unity 测试方式
运行游戏并升到 5 级，进入技能选择面板。
正常结果是按钮显示类似“蓝耗25  冷却4s”“伤害1.5x  范围3”，不会再出现单个数字独占一行。

### 6. 面试表达
技能选择 UI 的文本我没有简单依赖 Unity Text 自动换行，而是在技能系统里统一生成短格式文本。这样 UI 面板只负责显示，技能系统负责把配置数据整理成适合按钮显示的文案，既避免数字被挤到单独一行，也保持了 UI 和技能规则的分离。

### 7. 面试追问
1. **为什么不直接把 Text 的 Horizontal Overflow 改成 Overflow？**  
   那样文字可能横向超出按钮，看起来更不稳定。按钮文本应该先从内容长度上控制。
2. **为什么删掉技能描述？**  
   三选一按钮空间有限，描述适合放详情面板，按钮里优先显示决策最需要的数值。
3. **为什么在系统里拼文本？**  
   因为这些文本来自技能配置和技能等级，统一放在技能系统里更容易维护。

### 8. 本次涉及知识点
- Unity Legacy Text 自动换行
- UI 文本长度控制
- 配置数据到 UI 文案的转换
- UI 显示和规则数据分离

## 功能名称：技能释放规则层编译错误修复

### 1. 实现目标
修复新增技能释放规则时出现的编译错误，让 `TryCastPlayerSkillCommand` 和 `PlayerSkillSystem.TryCastSkill` 可以正常通过编译。

### 2. 涉及脚本
- `Assets/Script/Architecture/Commands/PlayerSkillCommands.cs`：把 `TryCastPlayerSkillCommand` 从 `UpgradePlayerSkillCommand` 内部移出来，改成同级 Command。
- `Assets/Script/Architecture/Systems/PlayerSkillSystem.cs`：把 System 内部的 `SendCommand` 调用改成直接调用 `PlayerResourceSystem.TrySpendMana`。

### 3. 调用流程
技能释放组件 -> `TryCastPlayerSkillCommand` -> `PlayerSkillSystem.TryCastSkill` -> `PlayerResourceSystem.TrySpendMana` -> 扣蓝并触发 MP/UI 相关事件。

### 4. 核心原理
`TryCastPlayerSkillCommand` 必须是独立的顶级类，不能写在 `UpgradePlayerSkillCommand` 的大括号里面，否则外部脚本无法直接 `new TryCastPlayerSkillCommand(...)`。
另外，QFramework 里 `SendCommand` 通常由 Controller 层使用；System 内部如果要复用另一个 System 的能力，可以通过 `this.GetSystem<目标System>()` 调用统一入口。

### 5. Unity 测试方式
打开 Unity 等待脚本编译，Console 不应再出现 `TryCastPlayerSkillCommand could not be found` 或 `PlayerSkillSystem 不包含 SendCommand` 这类错误。

### 6. 面试表达
这次问题本质上是架构层级使用错误。Command 类需要保持平级，方便 UI、输入组件或其他 Controller 统一发送命令；而 System 之间复用能力时，我没有让技能系统自己乱改 MP，而是调用资源系统的扣蓝入口，这样 MP 事件和 HUD 刷新仍然集中在资源系统里。

### 7. 面试追问
1. **为什么 Command 不能嵌套在另一个 Command 里？**  
   嵌套后类型路径变了，外部直接使用 `TryCastPlayerSkillCommand` 会找不到。
2. **为什么 System 里不用 `SendCommand`？**  
   当前 QFramework 扩展方法要求接收者实现对应发送接口，System 更适合通过 `GetSystem` 调用其他 System。
3. **这样会不会破坏扣蓝封装？**  
   不会，因为仍然走 `PlayerResourceSystem.TrySpendMana`，没有直接修改 PlayerModel。

### 8. 本次涉及知识点
- C# 大括号和类嵌套
- QFramework Command 与 System 的职责
- 编译错误定位
- 资源系统统一扣蓝入口

## 功能名称：技能真实特效 Prefab 接入

### 1. 实现目标
把原本只靠线稿显示的技能表现升级为真实 Prefab 特效。大火球使用飞行特效和爆炸特效，毒雾使用毒酸区域特效，镰刀旋转使用旋转类特效，同时保留线稿特效作为资源加载失败时的兜底表现。

### 2. 涉及脚本
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：在技能释放成功后生成对应 VFX Prefab，并在需要时自动销毁。
- `Assets/Resources/SkillVFX/*.prefab`：存放技能默认特效，便于 `Resources.Load` 在运行时加载。
- `Assets/Script/Skills/SkillLineEffect.cs`：继续作为兜底线稿表现使用。

### 3. 调用流程
玩家按技能键 -> `PlayerSkillCastComponent.TryCast` -> `TryCastPlayerSkillCommand` -> `PlayerSkillSystem.TryCastSkill` 校验蓝量/冷却/是否已学习 -> 返回成功 -> `PlayerSkillCastComponent` 根据技能类型播放 VFX 并结算伤害。

### 4. 核心原理
技能规则和技能表现分离。技能能不能释放仍然由 `PlayerSkillSystem` 判断；释放成功后，表现层才负责生成火球、爆炸、毒雾和旋转特效。默认特效放在 `Resources/SkillVFX` 下，因此运行时自动补上的 `PlayerSkillCastComponent` 也能加载到资源。

### 5. Unity 测试方式
进入玩法场景，升到 5 级学习技能。按 `1` 测试大火球飞行和爆炸，按 `2` 测试毒雾区域特效，按 `3` 测试镰刀旋转特效。如果某个 Prefab 方向或大小不合适，可以优先调整 `PlayerSkillCastComponent` 里的 VFX Scale 字段。

### 6. 面试表达
我的技能系统没有把特效写死在规则层里。规则层只判断技能是否能释放、扣蓝和进入冷却；表现层根据技能类型播放对应 VFX。大火球拆成飞行特效和爆炸特效，飞到目标点后再结算范围伤害。这样后续替换美术资源或接对象池时，不需要改技能规则。

### 7. 面试追问
1. **为什么默认特效放在 Resources 下？**  因为当前技能释放组件是运行时自动挂载的，第一版用 `Resources.Load` 可以保证不用手动拖引用也能看到效果。
2. **为什么还保留线稿特效？**  它是兜底表现，防止资源路径或 Prefab 丢失时技能完全没有反馈。
3. **为什么火球到达后才结算伤害？**  这样表现和逻辑一致，玩家看到爆炸时才受到范围伤害，更符合技能直觉。
4. **现在用 Instantiate/Destroy 有什么问题？**  频繁创建销毁特效可能带来性能波动，后续可以把这些 VFX 接入对象池。
5. **如果接服务器怎么处理？**  客户端播放表现，服务器校验释放合法性和命中结果，客户端不能完全决定最终伤害。

### 8. 本次涉及知识点
- Unity Prefab 特效接入
- `Resources.Load` 运行时资源加载
- 技能逻辑和表现层解耦
- 协程控制火球飞行流程
- 粒子特效实例化和生命周期管理

## 功能名称：刺客三连击动画重排与大旋转音效修复

### 1. 实现目标
本次把刺客普通三连击调整为 `ATK4 -> Atk1 -> Atk2`，其中第一段 `ATK4` 使用 1.5 倍速。大旋转技能继续使用 `Atk3` 作为 `Skill` 动画，并补上技能音效。由于 `ATK4.anim` 没有攻击动画事件，脚本额外提供第一段攻击的碰撞、连击窗口和收尾兜底，避免没伤害或卡动作。

### 2. 涉及脚本
- `PlayerCombatComponent`：负责普通攻击输入、连击段数、攻击碰撞窗口和无动画事件兜底。
- `PlayerPresentationComponent`：负责把 ComboIndex 和 Skill Trigger 同步给 Animator，并在技能结束时淡出攻击层。
- `PlayerAudioComponent`：新增技能音效播放入口。
- `PlayerSkillCastComponent`：大旋转释放成功后播放技能动画和技能音效。
- `Player.controller`：攻击层三段动画改为 `ATK4 / Atk1 / Atk2`，`Skill` 状态继续使用 `Atk3`。

### 3. 调用流程
普通攻击：`PlayerRuntimeController.Update -> PlayerCombatComponent.Tick -> SetCombo(1/2/3) -> PlayerPresentationComponent -> Animator Attack Layer`。

大旋转技能：`PlayerSkillCastComponent.TryCast -> PlayScytheSpinAnimation -> CancelAttackForSkill -> PlaySkill -> PlaySkill音效 -> CastScytheSpin范围伤害`。

### 4. 核心原理
Animator 负责播放动作，脚本负责游戏规则。三连击仍然用 `ComboIndex` 控制第几段攻击，但具体 Motion 已经换成新的动画顺序。`ATK4` 没有动画事件，所以脚本模拟了动画事件该做的事：延迟一小段时间开启攻击碰撞，打开连击输入窗口，到时间后重置攻击状态。

技能使用独立的 `Skill` Trigger，不占用普通攻击的 `ComboIndex`。大旋转开始前先取消普通攻击状态，避免 `Atk3` 作为技能动画时误触发普通攻击碰撞体。

### 5. Unity 测试方式
打开 `Assets/Scenes/MainScene.unity`，运行后选择刺客。连续点击鼠标左键，确认普通攻击顺序是 `ATK4 -> Atk1 -> Atk2`，第一段速度明显更快。按 `3` 释放大旋转，确认播放 `Atk3` 技能动画、出现技能音效、范围伤害生效，并且技能结束后不会卡在旋转或攻击姿势。

### 6. 面试表达
这次我处理的是动作资源替换后的连击适配问题。三连击逻辑没有写死具体动画，而是通过 Animator 的 `ComboIndex` 选择不同状态，所以换动画主要改状态机映射。因为第一段 `ATK4` 资源缺少动画事件，我在战斗组件里加了兜底计时，模拟开碰撞、开连击窗口和攻击结束，保证资源事件不完整时玩法仍然稳定。

### 7. 面试追问
1. **为什么不直接给 ATK4 动画手动加事件？**  可以加，但脚本兜底能防止以后再次替换动画时同类问题复发。
2. **为什么技能不用 ComboIndex？**  技能不是普通三连击的一段，用独立 Trigger 可以降低技能和普攻的耦合。
3. **为什么技能开始前要取消普通攻击？**  避免普通攻击状态、碰撞体和技能动画抢同一层表现。
4. **攻击层为什么要淡出？**  攻击层叠在移动层上，淡出可以让攻击结束后自然回到移动或待机动作。
5. **如果以后每段攻击动画都没有事件怎么办？**  可以把每段攻击的碰撞时间做成配置数据，而不是只给第一段写兜底字段。

### 8. 本次涉及知识点
- Animator Layer 和 Trigger
- ComboIndex 连击状态驱动
- 动画事件和脚本兜底
- 攻击碰撞窗口
- 技能音效与战斗逻辑解耦

## 功能名称：刺客三连击运行时顺序强制修复

### 1. 实现目标
修复“Animator Controller 文本里已经把三连击改成 `ATK4 -> Atk1 -> Atk2`，但运行时仍像旧顺序播放”的问题。本次让表现层在设置 `ComboIndex` 的同时，直接把 Attack Layer CrossFade 到本段指定状态，保证运行时顺序稳定。

### 2. 涉及脚本
- `PlayerPresentationComponent`：新增三连击状态名常量和 `CrossFadeComboAttackState`，负责强制播放 `Atk4 / Atk1 / Atk2`。
- `PlayerRuntime.prefab`：新增 `comboAttackCrossFadeDuration`，用于调整连击段切换的过渡时间。

### 3. 调用流程
`PlayerCombatComponent` 判断当前是第几段攻击 -> `PlayerPresentationComponent.SetCombo(comboIndex)` -> 设置 `ComboIndex` -> `CrossFadeComboAttackState` 强制进入 `Atk4 / Atk1 / Atk2`。

### 4. 核心原理
`ComboIndex` 是状态机参数，但它只告诉 Animator“现在是第几段”，最终走哪个过渡还会受 Any State、当前状态、过渡条件和优先级影响。为了保证运行时一定播放正确顺序，表现层在写入参数后主动调用 `Animator.CrossFadeInFixedTime`，直接指定攻击层的目标状态。

### 5. Unity 测试方式
运行 `MainScene`，使用刺客连续点击左键。观察 Animator 的 Attack Layer 当前状态，应依次进入 `Atk4 -> Atk1 -> Atk2`。第一段 `Atk4` 使用 `ATK4.anim`，速度为 1.5 倍。

### 6. 面试表达
我遇到的问题是状态机资源看起来改对了，但运行时仍受过渡链影响，没有按预期进入目标攻击状态。我的处理是保留 `ComboIndex` 作为状态机参数，同时在表现层根据连击段数直接 CrossFade 到指定状态。这样战斗逻辑不关心动画名，但表现层可以保证播放顺序稳定。

### 7. 面试追问
1. **为什么不用纯状态机过渡？**  状态机过渡受当前状态和优先级影响，资源复杂后容易出现运行时不符合预期。
2. **为什么不把动画名写在战斗组件里？**  战斗组件只负责规则，动画名属于表现层细节。
3. **CrossFade 的好处是什么？**  它能指定目标状态，同时保留一个短过渡，不会硬切。
4. **如果状态名写错怎么办？**  代码会先用 `Animator.HasState` 检查，找不到就退回原本的 `ComboIndex` 过渡。
5. **后续怎么更数据化？**  可以把职业连击状态名和过渡时间放到职业配置或 ScriptableObject。

### 8. 本次涉及知识点
- `Animator.CrossFadeInFixedTime`
- `Animator.HasState`
- 状态机参数和直接播放状态的区别
- 表现层与战斗逻辑解耦
- 连击动画运行时调试

## 功能名称：刺客三连击运行时状态机入口补全

### 1. 实现目标
解决刺客三连击资源已经配置为 `ATK4 -> Atk1 -> Atk2`，但运行后仍可能看起来像旧顺序的问题。本次把攻击层 Any State 到三段攻击的入口补齐，并在表现层输出运行时日志，确认每次点击实际进入了哪个 Animator 状态。

### 2. 涉及脚本
- `PlayerPresentationComponent`：根据 `ComboIndex` 显式播放 `Atk4 / Atk1 / Atk2`，并在 Unity Editor Console 输出连击映射。
- `Player.controller`：Attack Layer 增加 `ComboIndex == 2` 和 `ComboIndex == 3` 的 Any State 入口。
- `PlayerRuntime.prefab`：打开连击动画调试日志开关，方便运行时确认。

### 3. 调用流程
`PlayerCombatComponent.Tick -> currentCombo 变化 -> PlayerPresentationComponent.SetCombo -> Animator 设置 ComboIndex -> CrossFade 到 Attack Layer 指定攻击状态`

### 4. 核心原理
Animator Controller 里的状态和过渡是静态配置，但运行时会受“当前停在哪个状态”“过渡优先级”“Any State 入口是否存在”等因素影响。为了避免第二段、第三段只能依赖上一段状态内过渡，本次给攻击层补了三段攻击的直接入口，同时脚本仍然显式 CrossFade 到目标状态。这样状态机和脚本形成双保险：状态机可视化清楚，脚本运行时也能稳定命中目标动画。

### 5. Unity 测试方式
运行 `MainScene`，使用刺客连续点击左键。Console 应该依次打印 `Combo 1 -> Atk4`、`Combo 2 -> Atk1`、`Combo 3 -> Atk2`。如果画面仍不对，优先看 Console 里绑定的 Animator 和 Controller 是否是 `Player.controller`。

### 6. 面试表达
这次问题不是单纯换动画资源，而是运行时状态机入口不够稳定。我把战斗逻辑和动画表现继续分开：战斗组件只负责算当前是第几段，表现组件负责把段数映射成具体动画状态。状态机上我补全了攻击层 Any State 到三段攻击的入口，脚本上也通过 CrossFade 显式播放目标状态，这样既能在 Animator 里看清流程，也能保证运行时播放顺序稳定。

### 7. 面试追问
1. **为什么 Any State 要补三段入口？** 因为运行时不一定总停在上一段攻击状态，补入口能让任意攻击层状态都能直接进目标段。
2. **为什么 Any State 入口不能切到自身？** 如果允许切到自身，`ComboIndex` 没归零时可能反复重播当前段。
3. **为什么还保留状态内过渡？** 它让 Animator 图更符合连击流程，脚本强制播放只是运行时保险。
4. **为什么日志只在 Editor 用？** 调试时能确认状态，正式构建时不刷无用日志。
5. **后续怎么扩展多职业连击？** 可以把每个职业的连击状态名放到配置表或 ScriptableObject，由表现层读取。

### 8. 本次涉及知识点
- Animator Any State 过渡
- `Can Transition To Self`
- 攻击段数和动画状态名映射
- 运行时 Debug 日志定位动画问题
- 状态机配置与脚本保险配合

## 功能名称：刺客连击 Animator 编辑器级修复工具

### 1. 实现目标
解决 Unity Animator 窗口里仍显示旧连击顺序的问题。之前磁盘上的 `Player.controller` 已经改成 `ATK4 -> Atk1 -> Atk2`，但 Unity 当前会话里可能没有正确刷新，所以新增 Editor 工具用 Unity 官方 AnimatorController API 直接修复资源。

### 2. 涉及脚本
- `AssassinComboAnimatorControllerRepairTool`：放在 `Assets/Editor`，只在 Unity 编辑器里运行，负责修复 `Player.controller` 的 Attack Layer。

### 3. 调用流程
Unity 脚本编译完成 -> `InitializeOnLoad` 自动执行检查 -> 定位 `Assets/Ani/Player.controller` -> 找到 Attack Layer -> 重绑三段攻击和 Skill 动画 -> 保存 AnimatorController。

### 4. 核心原理
AnimatorController 是 Unity 资源，手动改 YAML 有时会遇到编辑器缓存或当前打开资源没有刷新。Editor 工具通过 `AssetDatabase` 加载动画控制器，再用 `AnimatorState` 和 `AnimatorStateTransition` 修改状态、Motion、过渡条件，最后 `SaveAssets` 保存，等于让 Unity 自己完成资源修改。

补充排错：项目的 Suriyun 美术资源包里也有一个默认命名空间下的 `AnimatorController` MonoBehaviour。如果 Editor 工具里直接写 `AnimatorController`，Unity 编译时可能解析到这个同名脚本，而不是 `UnityEditor.Animations.AnimatorController`，就会报“没有 layers / parameters / AddParameter”。解决方式是给 UnityEditor 的类型起别名，例如 `EditorAnimatorController = UnityEditor.Animations.AnimatorController`，避免类型撞名。

### 5. Unity 测试方式
等 Unity 编译完成后，打开 `Assets/Ani/Player.controller` 的 Attack Layer。第一段攻击状态应显示为 `Atk4`，Motion 为 `ATK4`，Speed 为 `1.5`。如果没有自动变化，点击菜单 `Tools/Treasure Hunter/Repair Assassin Combo Animator` 手动执行一次。

### 6. 面试表达
这次我没有继续只改 Animator 的文本文件，而是写了一个 Editor 工具通过 UnityEditor.Animations API 修复控制器。这样可以避免资源缓存导致 Unity 窗口和磁盘文件不一致，也能把状态、动画片段、Any State 入口、攻击结束回 Empty 的过渡统一校验。它属于编辑器辅助工具，不会进入运行时逻辑，也不会让战斗系统依赖 `PlayerCo`。

### 7. 面试追问
1. **为什么 Editor 工具不会影响运行时性能？** 因为它放在 `Assets/Editor`，只在编辑器编译和菜单执行时运行，不会打进正式运行时代码。
2. **为什么不用继续手改 controller 文件？** AnimatorController 是 Unity 序列化资源，用 API 修改更稳定，也能避免打开的编辑器资源没刷新的问题。
3. **为什么要同时补 Any State 和状态内过渡？** Any State 保证脚本可以直接进目标段，状态内过渡让 Animator 图的连击流程清楚。
4. **为什么攻击结束要回 Empty？** 攻击层是叠加层，回 Empty 后才能把表现交还给 Base Layer 的移动、待机等动作。
5. **如果以后新增第四段连击怎么办？** 可以把状态名和动画片段抽成配置，Editor 工具按配置批量生成状态和过渡。

### 8. 本次涉及知识点
- Unity Editor 脚本
- `AssetDatabase`
- `AnimatorController`
- `AnimatorStateTransition`
- 编辑器工具和运行时代码隔离

## 功能名称：刺客 ATK4 双段攻击碰撞窗口

### 1. 实现目标
刺客第一段普通攻击 `ATK4` 是镰刀连续挥舞两下，应该造成两次伤害。本次让第一段攻击在没有动画事件的情况下，由脚本开启两次独立武器碰撞窗口：第一次关闭后等待 `0.1s`，再重新打开并关闭一次。

### 2. 涉及脚本
- `PlayerCombatComponent`：负责攻击输入、连击计时、武器碰撞器开关。`ATK4` 没有动画事件，所以这里用脚本计时模拟两次攻击窗口。
- `WeaponCo`：负责在武器碰撞器触发时结算伤害，并用 `AttackHitWindowId` 保证同一个攻击窗口内同一目标只受一次伤害。
- `PlayerRuntime.prefab`：保存 `ATK4` 双段攻击窗口的时间参数。

### 3. 调用流程
玩家点击左键 -> `PlayerCombatComponent.StartFirstAttack` -> 播放 `ATK4` -> 脚本定时 `WeaponEnable` 第一次 -> `WeaponDisable` -> 等待 `0.1s` -> `WeaponEnable` 第二次 -> `WeaponDisable` -> `WeaponCo` 在两个不同窗口内分别结算伤害。

### 4. 核心原理
攻击碰撞器真正打开的位置是 `PlayerCombatComponent.WeaponEnable`。每次开启时都会让 `attackHitWindowId++`，`WeaponCo` 发现窗口 ID 变化后会清空本窗口已经受击的目标列表。这样同一个敌人在第一次挥镰里受过伤，第二次挥镰打开新窗口时仍然可以再次受伤，但不会在同一个窗口里因为 `OnTriggerStay` 每帧重复掉血。

### 5. Unity 测试方式
打开 `MainScene`，使用刺客靠近史莱姆，单点一次左键不要马上接第二段。正常情况下第一段 `ATK4` 会出现两次伤害数字。再连续点击左键，确认第一段之后仍能接 `Atk1 -> Atk2`。

### 6. 面试表达
这个近战攻击不是让碰撞器一直开着，而是按攻击动作拆成短时间的“攻击窗口”。每打开一次窗口都会生成新的 HitWindowId，武器脚本用这个 ID 做受击去重：同一窗口内一个敌人只受一次伤害，不同窗口可以再次受伤。ATK4 这个动画没有帧事件，所以我在战斗组件里用计时器模拟了两个攻击窗口，既符合动画表现，也不会让伤害每帧乱跳。

### 7. 面试追问
1. **为什么不用一直开碰撞器？** 会导致待机或持续接触时反复触发伤害，难以控制打击点。
2. **为什么 `OnTriggerStay` 也要处理？** 敌人可能已经在碰撞器里，第二次打开窗口时不一定触发 `OnTriggerEnter`。
3. **为什么同一窗口不会一直扣血？** `WeaponCo` 用 HashSet 记录当前窗口已经打过的目标。
4. **为什么第二次能再次扣血？** 第二次 `WeaponEnable` 会增加 `AttackHitWindowId`，目标去重列表会被刷新。
5. **更好的做法是什么？** 最理想是给 `ATK4.anim` 加动画事件，让打击窗口完全跟随动画帧。

### 8. 本次涉及知识点
- 近战攻击碰撞窗口
- `OnTriggerEnter` 与 `OnTriggerStay`
- HashSet 受击去重
- 动画事件缺失时的脚本兜底
- 连击窗口和伤害窗口的时序配合

## 功能名称：刺客 ATK4 结束抽搐修复

### 1. 实现目标
修复刺客第一段攻击 `ATK4` 播放完回到待机/移动状态时出现短暂抽搐的问题。问题来源是攻击层状态机和脚本参数清理时机不一致，导致 `Atk4` 结束后可能被 Any State 再次拉回。

### 2. 涉及脚本
- `PlayerPresentationComponent`：新增脚本计时攻击专用的 `ClearComboIndexForScriptedAttack` 和 `FadeOutAttackLayerAfterScriptedAttack`，用于及时清理 Animator 参数和淡出攻击层。
- `PlayerCombatComponent`：在 ATK4 第二次伤害窗口开始时清掉 `ComboIndex`，第二次伤害窗口结束后立即淡出攻击层。
- `Player.controller`：`Atk4 -> Empty` 过渡增加 `ComboIndex == 0` 条件，避免 `ComboIndex` 仍为 1 时提前回 Empty 后又被 Any State 拉回。
- `AssassinComboAnimatorControllerRepairTool`：同步修复工具逻辑，防止自动修复时把攻击结束过渡改回无条件。

### 3. 调用流程
ATK4 第二次攻击窗口开启 -> `PlayerPresentationComponent.ClearComboIndexForScriptedAttack` -> 状态机允许 ATK4 正常收尾但不会再被 Any State 拉回 -> 第二次攻击窗口关闭 -> `FadeOutAttackLayerAfterScriptedAttack` -> Attack Layer 平滑淡出到 Base Layer。

### 4. 核心原理
Animator 的 Any State 过渡只看参数是否满足。如果 `ComboIndex == 1` 没有及时清掉，`Atk4` 通过无条件过渡回到 `Empty` 后，Any State 会再次发现 `ComboIndex == 1`，于是又进入 `Atk4`。画面上就表现为攻击结束后抽一下。解决方式是让回 `Empty` 的过渡必须等 `ComboIndex == 0`，并在脚本确认 ATK4 进入最后一段伤害窗口时提前清参数。

### 5. Unity 测试方式
打开 `MainScene`，使用刺客单点一次左键，不接第二段攻击。观察 `ATK4` 播完后是否平滑回到待机或移动。再连续点左键，确认仍能接 `Atk1 -> Atk2`。

### 6. 面试表达
这个问题不是单纯动画资源抖，而是状态机参数和过渡条件配合不严谨。`ComboIndex` 还保持 1 时，攻击状态回到 Empty 后会被 Any State 再拉回攻击状态。我把 `Atk4 -> Empty` 改成只有 `ComboIndex == 0` 才能退出，同时在脚本驱动的 ATK4 第二段攻击窗口开始时提前清参数，窗口结束后淡出攻击层。这样既保留攻击动作收尾，又避免状态机重复进入攻击。

### 7. 面试追问
1. **为什么 Any State 会导致抽搐？** 因为 Any State 不关心当前状态，只要条件满足就能跳转。
2. **为什么不直接删 Any State？** Any State 能保证连击从任意攻击层状态进入目标段，运行时更稳定。
3. **为什么回 Empty 要加 ComboIndex 条件？** 这样只有脚本确认攻击可结束后，状态机才允许回空状态。
4. **为什么还要淡出攻击层？** Attack Layer 权重为 1 时会覆盖 Base Layer，淡出能平滑交还给待机/移动。
5. **为什么 ATK4 要脚本兜底？** 当前 ATK4 没有动画事件，只能用计时器模拟命中窗口和结束时机。

### 8. 本次涉及知识点
- Animator Any State
- Animator 参数清理时机
- Attack Layer 权重淡出
- 条件过渡和无条件过渡
- 动画表现与战斗计时配合

## 功能名称：升级回满蓝、开发者快捷键与视角速度调整

### 1. 实现目标
角色每次实际升级后自动回满魔法值，并让 HUD 蓝条通过正式资源事件同步刷新。新增只在 Unity 编辑器或 Development Build 中可用的开发者模式，用 F1 开关模式，L 增加 15 级但跳过属性选择，P 增加 100 经验，O 立即回满蓝。同时把第三人称镜头水平和垂直旋转速度都调整为原来的一半。

### 2. 涉及脚本
- `PlayerProgressionSystem`：统一处理正常经验、开发者快速升级、升级回蓝以及属性/技能选择队列。
- `PlayerResourceSystem`：提供正式的回满蓝入口，并发送蓝量变化和玩家属性变化事件。
- `AddPlayerLevelsForDevelopmentCommand`：把开发者增加等级请求转交给成长系统，避免测试组件直接修改玩家数据。
- `IGameplayInput`、`InputCo`：集中采样 F1、L、P、O 快捷键。
- `PlayerDeveloperModeComponent`：维护开发者模式开关，执行测试命令并显示调试提示。
- `PlayerRuntimeController`：装配并调度开发者模式组件，不再自己保存具体测试规则。
- `CameraCo`：水平速度由 200 调整为 100，垂直速度由 125 调整为 62.5。

### 3. 调用流程
正常升级回蓝：
`经验奖励 -> AddPlayerExpCommand -> PlayerProgressionSystem.AddExp -> DoLevelUp -> PlayerResourceSystem.FullRestoreMana -> PlayerManaChangedEvent -> PlayerHudUi`

开发者快速升级：
`F1 开启开发者模式 -> L -> PlayerDeveloperModeComponent -> AddPlayerLevelsForDevelopmentCommand -> PlayerProgressionSystem.AddLevelsForDevelopment -> 连升 15 级 -> 只在 5 的倍数等级加入技能选择`

开发者回蓝：
`O -> PlayerDeveloperModeComponent -> PlayerRuntimeController.FullRestoreMana -> FullRestorePlayerManaCommand -> PlayerResourceSystem`

### 4. 核心原理
正常经验和开发者快速升级共用同一个升级循环，区别只是传入“是否生成属性选择”的开关。正常升级传入 true，所以每一级都会加入属性三选一；L 快速升级传入 false，因此不会积累 15 次属性选择，但升级回血、回蓝和每 5 级一次的技能选择仍然正常执行。

开发者组件不直接写 `Level`、`CurrentExp` 或 `CurrentMp`，而是发送 Command 并调用正式资源入口。这样测试功能不会绕过核心规则，也能同时验证 HUD、事件和技能选择流程。正式发布包通过运行环境判断关闭开发者功能，避免测试快捷键误带到线上版本。

### 5. Unity 测试方式
1. 打开 `MainScene` 并进入 Play Mode。
2. 释放技能消耗蓝量，正常获得经验并升级，确认蓝量回满且蓝条同步刷新。
3. 开发者模式关闭时按 L、P、O，确认角色数据不变化。
4. 按 F1，确认左上角出现开发者模式提示。
5. 按 L，确认当前等级增加 15，只出现 3 次技能选择，不出现 15 次属性选择。
6. 按 P，确认增加 100 点经验，并继续走正常升级规则。
7. 消耗蓝量后按 O，确认蓝量立即回满。
8. 左右、上下移动鼠标，确认镜头旋转速度约为修改前的一半，滚轮缩放速度不变。

### 6. 面试表达
我把开发者快捷键单独拆成了一个测试组件，并且只允许它在编辑器或 Development Build 中启用。快捷键不会直接修改玩家等级和蓝量，而是通过 Command 调用正式的成长系统和资源系统。快速升 15 级会复用正常升级循环，但关闭属性选择入队，只保留每 5 级一次的技能选择，所以能快速测试技能成长，同时又不会连续点 15 次属性面板。升级回蓝也复用了统一的资源入口，因此数据变化和 HUD 刷新能保持同步。

### 7. 面试追问
1. **为什么不直接把 Level 加 15？** 直接改等级会跳过经验扣除、技能选择、回血回蓝和 UI 事件，测试结果不可信。
2. **为什么快速升级不会出现属性选择？** 成长系统内部把“等级结算”和“是否生成属性选择”拆开，开发者命令只关闭属性队列。
3. **为什么仍然会出现 3 次技能选择？** 技能选择属于每 5 级一次的正式规则，增加 15 级会跨过 3 个对应等级节点。
4. **为什么 O 键不直接设置 CurrentMp？** 统一调用 `PlayerResourceSystem.FullRestoreMana`，可以保证数值限制、事件通知和 HUD 刷新同步执行。
5. **怎么避免测试功能进入正式包？** 开启前检查 `Application.isEditor` 或 `Debug.isDebugBuild`，普通 Release Build 无法启用开发者模式。

### 8. 本次涉及知识点
- QFramework Command、System 和 Model 分层
- 正式流程复用与开发者工具隔离
- 经验换算和连续升级循环
- 属性选择队列与技能选择队列解耦
- 输入采样接口
- 魔法值变化事件与 HUD 刷新
- Unity Development Build
- 第三人称相机灵敏度配置

## 功能名称：技能真实特效对象池接入

### 1. 实现目标
把火球飞行、火球爆炸、毒雾和镰刀旋转这些真实 Prefab 特效从直接 `Instantiate / Destroy` 改成优先通过对象池获取和回收，减少技能频繁释放时的对象创建销毁开销。

### 2. 涉及脚本
- `Assets/Script/Skills/SkillVisualPool.cs`：新增 Prefab 特效对象的获取和回收接口。
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：释放技能时通过对象池生成真实特效，并在播放结束后回收。

### 3. 调用流程
玩家按技能键 -> `PlayerSkillCastComponent.TryCast` -> `PlayerSkillSystem.TryCastSkill` 校验蓝耗和冷却 -> 具体技能释放方法 -> `SpawnVfx` -> `SkillVisualPool.GetPrefabVfx` -> 播放特效 -> `ReleaseVfxAfterSeconds` 或火球飞行结束 -> `SkillVisualPool.ReleasePrefabVfx`。

### 4. 核心原理
对象池的核心思想是“用完不销毁，隐藏起来下次复用”。技能特效属于高频生成对象，如果每次释放技能都创建和销毁，容易产生性能波动和 GC。现在释放技能时先从池子里取对象，如果池子没有才创建；特效结束后隐藏并挂回对象池节点，下次释放同类技能时直接复用。

### 5. Unity 测试方式
1. 打开主游戏场景并进入 Play Mode。
2. 确认场景中存在挂有 `SkillVisualPool` 脚本的对象。
3. 学会火球、毒雾或镰刀旋转技能。
4. 连续按 `1/2/3` 释放技能。
5. 在 Hierarchy 中观察特效对象播放后会隐藏并回到 `SkillVisualPool` 节点下，而不是每次都销毁。

### 6. 面试表达
我把技能特效接入了对象池。释放技能时，技能逻辑不会每次都直接 Instantiate 特效，而是先向对象池申请一个同类型特效对象；播放完成后不 Destroy，而是隐藏并回收到池子中等待下次复用。这样火球爆炸、毒雾、镰刀旋转这类高频表现对象可以减少运行时创建销毁带来的 GC 和性能波动，也方便后面把伤害数字、子弹等对象继续接入同一套池化思路。

### 7. 面试追问
1. **为什么技能特效适合用对象池？** 因为它们生命周期短、创建频繁、类型固定，复用收益明显。
2. **对象池回收时为什么要 SetActive(false)？** 隐藏对象并停止参与场景表现，避免回收对象继续显示或执行逻辑。
3. **为什么火球飞行特效不能只靠定时回收？** 火球飞行结束时间由协程控制，飞到目标点就应该立即回收，所以单独在飞行结束处调用回收。
4. **对象池可能有什么问题？** 最大风险是状态残留，所以重新取出时要重置位置、旋转、缩放，并重新播放粒子。
5. **如果没有 SkillVisualPool 会怎么样？** 代码会退回普通 Instantiate/Destroy，保证测试场景不会因为缺少对象池直接失效。

### 8. 本次涉及知识点
- Unity 对象池思想
- Prefab 实例复用
- `Instantiate` / `Destroy` 性能问题
- 协程延迟回收
- 粒子系统 `Clear` / `Play` 重播
- 技能逻辑和技能表现解耦

## 功能名称：技能伤害数字与飘字对象池

### 1. 实现目标
让技能命中后有明确的伤害数字反馈，并把飘字对象从直接创建销毁改成对象池复用。火球爆炸、镰刀旋转和毒雾持续伤害都会显示数字，玩家更容易看出技能是否命中、毒雾是否持续生效。

### 2. 涉及脚本
- `Assets/Script/Combat/FloatingCombatText.cs`：飘字对象改为优先从 `SkillVisualPool` 获取和回收，并在同文件中加入战斗反馈工具。
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：技能范围伤害接入伤害数字和实际伤害结算。
- `Assets/Script/Skills/PoisonAreaEffect.cs`：毒雾每次 Tick 伤害接入伤害数字和实际伤害结算。

### 3. 调用流程
火球/镰刀：`PlayerSkillCastComponent.DealDamageInRadius -> ApplySkillDamageWithFeedback -> CombatFeedbackUtility.PreviewAppliedDamage -> fighter.Hit -> FloatingCombatText.ShowDamage -> SkillVisualPool.GetEffectObject -> 飘字播放 -> ReleaseVisual`

毒雾：`PoisonAreaEffect.TickDamage -> ApplyTickDamageWithFeedback -> CombatFeedbackUtility.PreviewAppliedDamage -> fighter.Hit -> FloatingCombatText.ShowDamage -> 对象池回收`

### 4. 核心原理
伤害数字属于短生命周期、高频生成的表现对象，适合对象池。现在飘字结束后不会 Destroy，而是隐藏并回收到 `SkillVisualPool` 下次复用。技能伤害在调用目标 `Hit` 前，会先预估目标当前血量，避免怪物只剩 3 血时飘出 100 这种溢出数字，也避免吸血按溢出伤害结算。

### 5. Unity 测试方式
1. 打开 `MainScene` 并进入 Play Mode。
2. 学会火球、毒雾和镰刀旋转。
3. 用 `1/2/3` 命中史莱姆。
4. 正常结果：史莱姆头顶会飘出伤害数字，毒雾每次 Tick 都会飘字。
5. 观察 Hierarchy 中的 `SkillVisualPool`，飘字对象结束后会回收到池节点下。

### 6. 面试表达
我在技能命中流程里加入了战斗反馈层。技能逻辑负责范围检测和调用目标受击，伤害数字由 `FloatingCombatText` 负责显示，并且这个对象也接入了对象池。为了避免显示溢出伤害，我在扣血前根据目标当前血量预估实际生效伤害，例如怪物只剩 3 点血时，技能即使理论伤害是 100，飘字和吸血也只按 3 处理。这样既提升了表现反馈，也让后续吸血等战斗结算更准确。

### 7. 面试追问
1. **为什么飘字也要对象池？** 飘字生成频率很高，尤其是毒雾持续伤害和群体技能，池化可以减少临时对象和 GC。
2. **为什么要预估实际伤害？** 为了避免溢出伤害影响显示和吸血，比如敌人只剩少量血时不应该按完整伤害吸血。
3. **为什么不让 FighterInterface.Hit 返回实际伤害？** 第一版为了小改动兼容旧接口；后续更商业化可以把 `Hit` 改成返回 `DamageResult`。
4. **飘字为什么放在表现层？** 因为它只影响视觉反馈，不应该参与核心扣血规则。
5. **毒雾为什么每次 Tick 都显示？** 持续技能需要让玩家知道它正在持续生效，数字反馈比单纯特效更清晰。

### 8. 本次涉及知识点
- 战斗表现和伤害逻辑解耦
- 对象池复用 UI/3D 文本对象
- 溢出伤害处理
- 持续伤害 Tick 反馈
- Unity TextMesh 世界空间飘字
- 技能伤害、吸血和反馈的调用顺序

## 功能名称：技能范围预览与松手释放

### 1. 实现目标
把技能释放从“按下立即释放”改成“按住技能键显示范围预览，松开技能键才真正释放”。这样玩家在释放火球、毒雾、镰刀旋转前，可以先看到技能影响范围，操作体验更接近 ARPG/MMO 技能系统。

### 2. 涉及脚本
- `Assets/Script/Input/IGameplayInput.cs`：新增技能键 Held 和 Up 输入状态。
- `Assets/Script/Input/InputCo.cs`：使用 `Input.GetKey` 和 `Input.GetKeyUp` 缓存技能按住、松开状态。
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：新增技能范围预览 LineRenderer，并把技能输入改为按下预览、松开释放。

### 3. 调用流程
`InputCo.Update -> Skill1Down/Skill1Held/Skill1Up -> PlayerSkillCastComponent.Tick -> HandleSkillPreviewInput -> BeginSkillPreview/RefreshSkillPreview -> 松开按键 -> EndSkillPreviewAndCast -> TryCastPlayerSkillCommand -> PlayerSkillSystem.TryCastSkill -> 具体技能释放`

### 4. 核心原理
输入层只负责采样按键状态，不直接释放技能。技能释放组件根据 Down/Held/Up 三种状态拆分流程：Down 创建或刷新范围圈，Held 每帧更新范围圈位置，Up 隐藏范围圈并调用原来的 TryCast。预览阶段只读取技能配置和玩家已学技能等级，不扣蓝、不进冷却；真正释放仍然走原来的 QFramework Command 校验流程。

### 5. Unity 测试方式
1. 打开 `MainScene` 并进入 Play Mode。
2. 学会火球、毒雾、镰刀旋转任意一个技能。
3. 按住 `1`，应看到玩家前方火球落点范围圈；松开后才释放火球。
4. 按住 `2`，应看到玩家前方毒雾范围圈；松开后生成毒雾。
5. 按住 `3`，应看到玩家自身周围的镰刀旋转范围圈；松开后释放旋转。
6. 没学技能、冷却中或蓝量不足时，松开按键仍会走原来的失败提示。

### 6. 面试表达
我把技能输入拆成了按下、按住和松开三个阶段。按下和按住阶段只做范围预览，不改变战斗数据；松开阶段才进入正式释放流程，并复用原来的 QFramework Command 做蓝耗、冷却和是否已学习的校验。范围预览用运行时 LineRenderer 根据技能配置表里的 radius 绘制，所以技能升级后预览范围也会跟着变大。这样实现既提升了操作反馈，也没有破坏技能系统原来的规则层。

### 7. 面试追问
1. **为什么预览阶段不扣蓝？** 因为预览只是表现，不代表技能已经释放，扣蓝和冷却必须放在松手后的正式释放阶段。
2. **为什么输入层不直接释放技能？** 输入层只采样按键，释放规则由技能组件和 SkillSystem 处理，降低耦合。
3. **技能升级后范围预览怎么变化？** 预览每次读取当前技能等级对应的 `SkillLevelDefine.radius`，所以升级后自动变大。
4. **为什么用 LineRenderer 做第一版预览？** 它不依赖额外美术资源，能快速稳定表达技能范围，后续可以替换成地面贴花或特效 Prefab。
5. **冷却中按住技能会怎样？** 当前第一版仍允许查看范围，松开后由 SkillSystem 拦截并提示冷却或蓝量不足。

### 8. 本次涉及知识点
- Unity 输入 Down/Held/Up 拆分
- LineRenderer 绘制地面范围圈
- 技能配置驱动表现
- 预览表现和正式释放解耦
- QFramework Command 复用
- 技能交互手感设计

## 功能名称：怪物巡逻点层级修复

### 1. 实现目标
修复刷怪器不断生成史莱姆后，巡逻点被移动到场景根节点并长期残留的问题。修复后，巡逻点继续保留为所属怪物的子物体，怪物死亡时会随怪物一起销毁，同时史莱姆仍能前往出生时配置的巡逻位置。

### 2. 涉及脚本
- `Assets/Script/Enemies/SlimeCo.cs`：怪物出生时缓存有效巡逻点的世界坐标，巡逻状态改为读取缓存坐标，不再解除巡逻点的父子关系。
- `Assets/Script/Enemies/MonsSpawner.cs`：先计算最终出生位置，再使用带位置、旋转和父节点的 `Instantiate` 重载创建怪物，保证怪物初始化时已经处于正确位置。

### 3. 调用流程
`MonsSpawner.Spawner -> 计算出生位置 -> Instantiate怪物并设置父节点 -> SlimeCo.Start -> CachePatrolWorldPositions -> DoPatrol读取缓存坐标 -> 怪物死亡 -> 怪物和子级巡逻点一起销毁`

### 4. 核心原理
巡逻点原本是怪物 Prefab 的子物体。旧逻辑为了防止巡逻点跟随怪物移动，直接把它们的父节点设置为 `null`，这会让它们进入场景根节点；怪物死亡时，这些已经脱离的巡逻点不会被销毁。现在改为在怪物出生后只读取一次巡逻点的世界坐标，并把坐标保存到 `Vector3[]` 中。后续怪物移动时，子物体的位置虽然会跟着变化，但巡逻逻辑使用的是已经保存的坐标，所以目标位置保持不变，也不需要破坏原来的层级关系。

### 5. Unity 测试方式
1. 打开 `MainScene` 并进入 Play Mode。
2. 展开任意一个 `monsSp` 刷怪点，观察生成出来的史莱姆。
3. 确认 `partrol0~3` 仍然位于对应史莱姆下面，没有进入场景根节点。
4. 观察史莱姆仍会前往原来的多个巡逻位置，并能正常追击和攻击玩家。
5. 连续击杀并等待多批怪物刷新，确认 Hierarchy 根节点不会不断增加巡逻点。
6. 确认怪物死亡销毁时，其子级巡逻点也一起消失。

### 6. 面试表达
我在排查刷怪后 Hierarchy 不断出现巡逻点的问题时，发现旧逻辑为了固定巡逻位置，把 Prefab 里的巡逻点全部解除父子关系。这样虽然目标点不会跟随怪物移动，但怪物死亡后巡逻点会残留。我把实现改成了出生时缓存巡逻点世界坐标，运行时巡逻只读取坐标数据，巡逻点本身仍属于怪物。这样既保持了固定巡逻范围，也保证了对象生命周期和层级关系一致，并且为后续怪物对象池重新初始化巡逻坐标做好了基础。

### 7. 面试追问
1. **为什么不能一直读取子物体的 Transform.position？** 因为子物体会跟随怪物本体移动，目标点也会不断移动，怪物可能永远无法到达目标。
2. **为什么不继续把巡逻点移到场景根节点？** 解除父子关系后，巡逻点生命周期不再跟随怪物，怪物销毁时容易产生残留对象。
3. **为什么缓存世界坐标而不是局部坐标？** 巡逻移动需要一个固定的场景目标，世界坐标可以直接表示怪物出生时周围的实际巡逻位置。
4. **为什么刷怪器要先确定位置再 Instantiate？** 怪物初始化时会读取巡逻点坐标，先设置好出生位置可以保证缓存结果正确。
5. **以后接对象池需要注意什么？** 对象重新取出时要根据新的出生位置重新缓存巡逻坐标，并重置血量、状态、碰撞体和 Animator。

### 8. 本次涉及知识点
- Unity Transform 父子层级
- 世界坐标与局部坐标
- Prefab 实例生命周期
- `Instantiate` 带位置和父节点的重载
- 缓存运行时数据
- 怪物巡逻状态
- 对象池前的状态重置准备

## 功能名称：技能 20 级成长与边界 Bug 修复

### 1. 实现目标
把现有三个技能从 3 级扩展到 20 级。Lv.4 以后伤害倍率按每级 10% 复利增长，蓝耗、冷却、范围、持续时间、Tick 间隔和减速比例保持 Lv.3 上限。同时修复翻滚或输入缺失时冷却停止、非法技能类型错误回退成自身范围技能，以及技能候选耗尽后待选择次数永久残留的问题。

### 2. 涉及脚本
- `Assets/Resources/Data/SkillDefine.json`：补齐三个技能的 Lv.4～20 数值。
- `Assets/Script/Data/SkillDefine.cs`：增加 `Invalid` 类型和安全的 `TryGetSkillType` 解析入口。
- `Assets/Script/Data/SkillDataManager.cs`：加载时校验技能类型、等级连续性和基础数值。
- `Assets/Script/Architecture/Systems/PlayerSkillSystem.cs`：释放前再次检查类型，并阻止无候选选择次数继续累计。
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：冷却改由独立 `Update` 推进，不再依赖输入处理流程。
- `Assets/Editor/Tests/SkillConfigValidationTests.cs`：保护 20 级曲线、Lv.3 封顶规则和非法类型拦截。

### 3. 调用流程
`SkillDataManager.Awake -> 读取 JSON -> TryValidateSkill -> 合法技能进入技能池`

`PlayerSkillCastComponent.Update -> PlayerSkillSystem.TickSkillCooldowns -> PlayerSkillModel.TickCooldowns`

`玩家升级 -> PlayerSkillSystem.AddPendingSkillSelection -> HasAvailableSkillChoice -> 有候选才增加次数`

`技能按键松开 -> TryCastPlayerSkillCommand -> PlayerSkillSystem.TryCastSkill -> 校验类型/学习状态/冷却/蓝量 -> 开始冷却 -> 执行技能表现`

### 4. 核心原理
指数成长使用 `Lv.3 伤害倍率 × 1.1^(当前等级 - 3)`，让后期伤害保持成长感；非伤害字段固定在 Lv.3，避免攻击范围、控制强度和持续时间无限扩大。冷却属于战斗时间状态，不属于输入行为，因此它使用独立的 Unity `Update` 推进，即使玩家正在翻滚或输入对象暂时不存在也能正常减少。技能类型解析采用“失败即无效”的方式，错误配置不会再猜测成另一种技能。技能选择次数增加前会先检查候选，最后一个技能升满时也会清理已经无法使用的剩余次数。

### 5. Unity 测试方式
1. 打开 `MainScene`，确认 Console 显示三个技能配置加载成功，没有配置校验错误。
2. 打开 `Window > General > Test Runner`，在 EditMode 运行 `SkillConfigValidationTests`。
3. 按 `F1` 开启开发者模式，使用 `L` 快速升级并重复升级技能，确认技能可以超过 Lv.3。
4. 对比 Lv.3 和 Lv.4，确认伤害提高，但范围、蓝耗、冷却和控制数值不变。
5. 释放技能后连续翻滚，确认技能栏冷却数字仍持续下降。
6. 技能全部满级后继续升级，确认不再弹出空技能面板，待选择次数不会残留。
7. 临时把一个 `skillType` 写错，确认 Console 报配置错误，并且该技能不会进入候选或被释放；测试后恢复配置。

### 6. 面试表达
我这次主要补了技能系统的成长曲线和三个边界问题。技能 Lv.4 到 Lv.20 的伤害按每级 10% 复利增长，但范围、冷却和控制效果封顶在 Lv.3，避免后期数值破坏战斗空间。冷却从输入 Tick 中拆到了独立 Update，所以翻滚或输入缺失不会让冷却暂停。配置加载采用失败即拒绝的校验策略，非法类型不会再自动当成其他技能执行；技能候选耗尽时也不会继续累计无法处理的选择次数。最后我用 EditMode 测试锁住了这些配置规则。

### 7. 面试追问
1. **为什么伤害可以指数增长，范围不能一起增长？** 伤害是纵向数值成长，范围和控制属于战斗空间与操作规则，持续增长容易让走位和怪物数量失去意义。
2. **为什么冷却不能放在输入处理方法里？** 输入可能因为翻滚、UI 或设备状态被跳过，但冷却是独立的时间状态，两者耦合会造成计时冻结。
3. **为什么非法类型不提供默认值？** 默认值会把配置错误隐藏成错误玩法，失败即拒绝能更早暴露问题，也能避免错误扣蓝和冷却。
4. **为什么候选耗尽后要清理剩余次数？** 快速升级可能一次积累多次选择，最后一个技能满级后如果不清理，UI 会不断收到一个永远无法完成的任务。
5. **为什么还要写配置测试？** 技能表会频繁被编辑，自动测试能在进入场景前发现漏级、数值曲线或类型拼写错误。

### 8. 本次涉及知识点
- 指数成长曲线与复利公式
- 静态配置和运行时状态分离
- Unity `Update` 与 `Time.deltaTime`
- Fail Fast 配置校验
- 技能选择队列边界处理
- NUnit EditMode 测试
- UTF-8 源码编码

## 功能名称：刺客 Atk1 / Atk2 攻击速度调整

### 1. 实现目标
把刺客三连击第二段 `Atk1` 的播放速度调整为 2 倍，第三段 `Atk2` 的播放速度调整为 1.5 倍。由于第二段速度变快，二段完整播放后再进入三段的缓存释放延迟也从 `0.08s` 缩短为 `0.04s`，避免动作播完后多停顿。

### 2. 涉及脚本
- `Assets/Ani/Player.controller`：直接修改 Attack Layer 中 `Atk1` 和 `Atk2` 状态的 `Speed`。
- `Assets/Editor/AssassinComboAnimatorControllerRepairTool.cs`：同步修改一键修复工具的默认速度，防止之后修复状态机时又覆盖回旧值。
- `Assets/Script/Player/PlayerCombatComponent.cs`：调整第二段进入第三段的缓存释放默认延迟。
- `Assets/Resources/Characters/PlayerRuntime.prefab`：同步运行时 Prefab 上的缓存延迟配置。

### 3. 调用流程
`鼠标左键连击 -> PlayerCombatComponent 设置 ComboIndex -> PlayerPresentationComponent 播放 Atk1/Atk2 -> Animator 使用状态 Speed 倍速播放动画 -> Atk1 ResetCombo 事件后延迟 0.04s -> 进入 Atk2`

### 4. 核心原理
Animator State 的 `Speed` 会影响该状态下动画片段的整体播放速度，动画事件的触发时间也会跟着缩放。比如 `Atk1` 设为 2 倍速后，原本 0.8 秒附近触发的收尾事件，实际会更早触发。为了保持“第二段完整播放后再进入第三段”的手感，需要把脚本里用于补尾巴的等待时间也同步变短。

### 5. Unity 测试方式
打开 `MainScene`，运行后选择刺客，连续点击鼠标左键。正常结果应该是三连击顺序仍然为 `ATK4 -> Atk1 -> Atk2`，其中 `Atk1` 明显更快，`Atk2` 比原来更快但慢于 `Atk1`，并且第二段不会在动作中途切到第三段。

### 6. 面试表达
这次我调整的是 Animator 状态本身的播放速度，而不是在代码里硬改 Animator 全局速度。这样只影响指定攻击段，不会影响移动、技能或其他角色动画。同时我把状态机修复工具里的默认值也一起同步，避免后续自动修复 Animator 时把配置覆盖回去。因为动画速度会影响动画事件触发时间，所以我也同步调整了二段到三段的输入缓存释放延迟，保证手感和状态切换仍然稳定。

### 7. 面试追问
1. **为什么不直接改 Animator.speed？** `Animator.speed` 会影响整个 Animator，移动、技能、受击等动画都会被加速，不适合只调某一段攻击。
2. **为什么要改 Editor 修复工具？** 这个工具会重建刺客连击状态，如果工具里还是旧速度，后续一键修复会把 Animator 覆盖回旧配置。
3. **动画速度改变会影响动画事件吗？** 会，状态速度越快，动画片段和动画事件都会按比例更快执行。
4. **为什么二段到三段的延迟也要缩短？** 因为 `Atk1` 变成 2 倍速后，动画尾巴真实持续时间变短，原来的 `0.08s` 会显得像卡了一下。
5. **如果以后还要调攻击节奏怎么办？** 可以继续把每段攻击速度、连击窗口和碰撞窗口做成配置数据，避免每次都进代码改。

### 8. 本次涉及知识点
- Animator State Speed
- 动画事件与播放速度的关系
- 连击输入缓存
- Animator Controller 配置持久化
- Prefab 序列化字段同步

## 功能名称：怪物状态切换入口与对象池生命周期预留

### 1. 实现目标
在不大拆现有怪物 AI 的前提下，把史莱姆状态切换统一收口到 `ChangeState` 方法中，减少后续维护时到处查找 `enemyState = xxx` 的成本。同时提前给怪物对象池准备 `ResetEnemyForSpawn` 和 `PrepareRecycle` 两个生命周期入口，方便以后从 `Instantiate / Destroy` 平滑改成对象池复用。

### 2. 涉及脚本
- `Assets/Script/Enemies/SlimeCo.cs`：保留原来的枚举状态机结构，新增统一状态切换方法、对象池取出重置方法、对象池回收清理方法，并让死亡延迟销毁协程可以被主动停止。

### 3. 调用流程
当前正式流程：`SlimeCo.Update -> EveryFrame -> DoIdle/DoPatrol/DoPersuit/DoAtk/DoDie -> ChangeState`

后续对象池流程预留：`MonsterPool.Get -> 设置怪物位置和父节点 -> SlimeCo.ResetEnemyForSpawn -> 怪物重新进入 Idle`

后续回收流程预留：`怪物死亡 -> MonsterPool.Release -> SlimeCo.PrepareRecycle -> SetActive(false) -> 等待下次复用`

### 4. 核心原理
状态机的核心是“同一时间只处于一种主要行为状态”。这次没有把每个状态拆成独立类，而是继续使用 `enum + switch`，因为普通怪物行为稳定、种类不再扩展，大拆收益不高。`ChangeState` 的作用是先建立统一入口，后续如果要加进入状态、退出状态、统计日志、对象池重置，都可以集中写在一个地方。对象池复用最怕状态残留，所以 `ResetEnemyForSpawn` 会重置血量、死亡标记、攻击碰撞、巡逻索引、受击锁定、Animator 和当前难度；`PrepareRecycle` 则负责回收前停止旧协程、关闭攻击碰撞和恢复材质。

### 5. Unity 测试方式
1. 打开 `MainScene` 进入 Play Mode。
2. 观察史莱姆是否仍会在待机、巡逻、追击、攻击之间正常切换。
3. 靠近史莱姆，确认它能追击并攻击玩家。
4. 攻击史莱姆，确认受击变色、血条扣减和受击动画正常。
5. 击杀史莱姆，确认死亡动画、经验奖励和延迟销毁正常。
6. 等待刷怪器继续生成史莱姆，确认新史莱姆行为没有变化。

### 6. 面试表达
我这个项目里的普通怪物行为比较固定，所以没有把它拆成复杂的状态类，而是保留了轻量的枚举状态机。为了提升可维护性，我把状态切换统一封装成 `ChangeState`，后续如果需要做进入状态、退出状态或者调试日志，都有统一入口。考虑到之后可能给怪物接对象池，我又提前做了两个生命周期方法：怪物从池里取出时重置血量、状态、协程、攻击碰撞和 Animator，回收前清理运行时状态，避免复用时出现死亡动画残留、攻击碰撞没关、旧销毁协程继续执行这类问题。这样改动比较小，也保留了现有怪物稳定性。

### 7. 面试追问
1. **为什么不把状态机拆成多个状态类？** 因为普通怪物行为稳定、种类少，完整拆分类会增加迁移风险；当前阶段统一切换入口的收益更直接。
2. **`ChangeState` 的价值是什么？** 它把状态变化集中到一个方法，后续扩展进入状态、退出状态、调试日志或对象池重置时不用全项目搜索直接赋值。
3. **对象池复用怪物最容易出什么问题？** 最容易出现状态残留，比如残血、死亡标记、攻击碰撞开启、Animator 停在死亡动画、旧协程继续销毁对象。
4. **为什么要保存死亡协程引用？** 因为对象池回收后对象不会真的销毁，如果旧的延迟 `Destroy` 协程还在跑，怪物下次取出后可能突然被销毁。
5. **Boss 还会用这套吗？** Boss 可以借鉴统一状态切换和生命周期重置思路，但 Boss 行为更复杂，适合单独做 Boss 状态机和阶段逻辑。

### 8. 本次涉及知识点
- FSM 有限状态机
- `enum + switch` 轻量状态机
- 状态切换统一入口
- Unity 协程生命周期
- Animator 重置
- CharacterController 启用与禁用
- 对象池复用前后的状态清理

## 功能名称：怪物对象池接入

### 1. 实现目标
把普通史莱姆从反复 `Instantiate / Destroy` 改成对象池复用。刷怪器生成怪物时优先从 `MonsterPool` 取出旧对象，池子没有可用对象时才创建新怪物；怪物死亡动画播放完并延迟结束后，不再直接销毁，而是清理状态、隐藏对象并回收到池节点下。

### 2. 涉及脚本
- `Assets/Script/Enemies/MonsterPool.cs`：新增怪物专用对象池，按 Prefab 分类保存可复用的 `SlimeCo`。
- `Assets/Script/Enemies/MonsSpawner.cs`：刷怪改为调用 `MonsterPool.Instance.GetMonster`，活怪数量改为统计当前刷怪点下激活的 `SlimeCo`。
- `Assets/Script/Enemies/SlimeCo.cs`：死亡延迟结束后优先回收到对象池；池化取出时重置血量、状态、协程、攻击碰撞、Animator 和移动速度。
- `Assembly-CSharp.csproj`：补充新脚本编译项，保证本地 `dotnet build` 能识别 `MonsterPool`。

### 3. 调用流程
刷怪流程：`MonsSpawner.Update -> Spawner -> MonsterPool.GetMonster -> SlimeCo.BindPool -> SlimeCo.ResetEnemyForSpawn -> 怪物进入场景`

死亡回收流程：`SlimeCo.Hit -> ChangeState(Die) -> DoDie -> ScheduleDestroy -> DestorySelf -> FinishDeathLifecycle -> MonsterPool.ReleaseMonster -> SlimeCo.PrepareRecycle -> SetActive(false)`

复用流程：`下一次刷怪 -> MonsterPool.GetMonster -> 从 Queue 取出旧 SlimeCo -> 设置位置/父物体 -> SetActive(true) -> ResetEnemyForSpawn`

### 4. 核心原理
对象池的核心思路是“用完不销毁，先隐藏起来，下次再拿出来用”。怪物属于会反复生成和死亡的对象，如果每次都创建和销毁，会带来性能波动和 GC 压力。接入对象池后，刷怪器只关心“我要一只怪”，具体是新建还是复用由 `MonsterPool` 决定。怪物死亡后仍然先播放死亡动画和发经验，等死亡延迟结束才回收，这样玩家看到的表现不会突兀。回收前必须清理状态，否则下一次取出来可能残血、还在死亡状态、攻击碰撞没关、移动速度被毒雾减慢，或者旧的协程继续执行。

### 5. Unity 测试方式
1. 打开 `MainScene` 进入 Play Mode。
2. 观察场景中是否会自动出现 `MonsterPool` 节点。
3. 击杀几只史莱姆，确认死亡动画、经验奖励和延迟消失正常。
4. 展开 `MonsterPool`，确认死亡后的史莱姆会进入 `Slime1_Pool` 或 `Slime2_Pool` 节点并处于隐藏状态。
5. 等刷怪点继续刷怪，确认池子里的旧怪物会被重新取出，挂回对应 `MonsSpawner` 下。
6. 连续击杀多轮，确认 Hierarchy 不会因为死亡怪物无限增加而变得混乱。
7. 使用毒雾等会修改速度的技能后，确认新刷出的怪物移动速度恢复正常。

### 6. 面试表达
我把普通怪物接入了对象池。刷怪器不再直接 `Instantiate`，而是调用 `MonsterPool.GetMonster`，池子里有同 Prefab 的隐藏怪物就复用，没有才创建。怪物死亡时仍然先走原来的死亡状态、死亡动画和经验奖励，等死亡延迟结束后通过 `MonsterPool.ReleaseMonster` 回收。为了避免对象池常见的状态残留问题，我在 `SlimeCo` 里做了取出重置和回收清理，包括血量、状态、攻击碰撞、受击协程、死亡协程、Animator、巡逻点和移动速度。这样可以减少频繁创建销毁带来的性能波动，也让刷怪系统更接近商业项目里常见的写法。

### 7. 面试追问
1. **为什么怪物适合用对象池？** 怪物会频繁生成和死亡，生命周期重复，复用可以减少 `Instantiate / Destroy` 带来的开销和 GC。
2. **为什么不用技能特效池直接管理怪物？** 技能特效和怪物生命周期不同，怪物有 AI、血量、事件、协程和动画状态，混用池子容易导致职责不清。
3. **对象池回收前为什么要清理状态？** 因为对象没有真正销毁，所有字段都会保留；不清理就可能残血复活、攻击碰撞继续开启或 Animator 停在死亡动画。
4. **刷怪器为什么不能继续只用 childCount？** 池化后隐藏怪物可能不再代表活怪，正确做法是统计当前刷怪点下仍然激活的怪物对象。
5. **为什么死亡后不立刻回收？** 立刻回收会让死亡动画消失得很突兀，所以保留死亡延迟，表现结束后再隐藏回池。

### 8. 本次涉及知识点
- Unity 对象池
- Prefab 分类复用
- `Queue<T>` 管理空闲对象
- `SetActive(true/false)` 与 `OnEnable/OnDisable`
- 怪物死亡生命周期
- 对象池状态残留
- 刷怪器活怪数量统计
- 运行时自动创建管理节点

## 功能名称：怪物对象池风险修复

### 1. 实现目标
修复怪物对象池接入后的几个边缘风险：刷怪器开局补怪可能死循环、同一只怪物可能重复回收到对象池、毒雾减速可能影响对象池复用后的新怪物。修复后，开局补怪不会卡死 Unity，怪物回收队列不会重复塞入同一个对象，毒雾只会恢复同一轮生命中的怪物速度。

### 2. 涉及脚本
- `Assets/Script/Enemies/MonsSpawner.cs`：活怪数量改用 `activeSelf`，`FillToMax` 从无限 `while` 改成有限 `for`，并让 `Spawner` 返回生成是否成功。
- `Assets/Script/Enemies/MonsterPool.cs`：新增 `pooledMonsters` 集合，防止同一只怪物被重复回收进队列。
- `Assets/Script/Enemies/SlimeCo.cs`：新增 `ReuseVersion`，怪物每次取出或回收都会更新版本号。
- `Assets/Script/Skills/PoisonAreaEffect.cs`：毒雾减速记录怪物当时的 `ReuseVersion`，只恢复同一轮生命中的速度。

### 3. 调用流程
安全补怪流程：`MonsterManager.Start -> MonsSpawner.FillToMax -> 计算 needSpawnCount -> for 有限补怪 -> Spawner 返回成功/失败`

重复回收防护：`SlimeCo 死亡 -> MonsterPool.ReleaseMonster -> pooledMonsters.Add -> 已在池中则直接 return`

毒雾减速防护：`PoisonAreaEffect.TryApplySlow -> 记录 SlimeCo.ReuseVersion -> SlimeCo 回收/复用时版本号变化 -> RestoreAllSlimes 只恢复版本一致的怪物`

### 4. 核心原理
对象池让对象“不销毁而复用”，所以很多以前依赖销毁自动解决的问题都要显式处理。刷怪器不能用无限 `while` 依赖生成数量一定增加，否则 Prefab 配错或父物体未激活时会卡死。对象池也要防止重复回收，因为一个对象如果被放进队列两次，后续可能被取出两次。毒雾减速不能只保存 `SlimeCo` 引用，因为对象池复用后同一个引用可能代表新一轮怪物，所以用 `ReuseVersion` 区分不同生命轮次。

### 5. Unity 测试方式
1. 打开 `MainScene` 进入 Play Mode，确认不会再卡死在进入游戏阶段。
2. 观察开局刷怪数量，正常应按场景中 6 个刷怪点、每个 `maxNum=4`，最多补出 24 只普通怪。
3. 连续击杀怪物，确认怪物死亡后进入 `MonsterPool` 节点，再次刷怪时可复用。
4. 使用毒雾技能攻击怪物，等怪物死亡并复用后，确认新怪物移动速度正常。
5. 临时清空某个刷怪点的 `monsPrefab` 测试时，Unity Console 应提示生成失败，但不会卡死。

### 6. 面试表达
我在怪物对象池接入后做了一轮风险修复。第一个是刷怪器补满怪物时不能用无限 `while`，因为对象池统计活怪数量时可能受到父物体激活状态或 Prefab 配置影响，所以我改成了有限次数的 `for` 并让生成函数返回成功状态。第二个是对象池用 `HashSet` 防止同一个怪物重复回收。第三个是毒雾减速这类持续状态不能只按对象引用恢复，因为池化后同一个实例会代表新怪物，所以我给怪物加了复用版本号，毒雾只恢复同一轮生命中的速度。这样对象池不只是能跑，而是把复用带来的状态残留问题也处理掉了。

### 7. 面试追问
1. **为什么 `FillToMax` 不能用 `while`？** 如果生成失败或数量统计没增加，`while` 会一直执行，导致 Unity 卡死。
2. **为什么统计活怪用 `activeSelf`？** `activeInHierarchy` 会受父物体激活状态影响，刷怪点父物体没激活时会误判为没有怪。
3. **为什么要防重复回收？** 同一个对象进池两次，后续可能被取出两次，造成同一实例被多个刷怪流程同时使用。
4. **为什么毒雾要记录版本号？** 对象池复用后同一个 `SlimeCo` 引用可能代表新怪物，版本号能区分不同生命轮次。
5. **为什么不直接让毒雾结束时无条件恢复速度？** 无条件恢复可能把已经复用的新怪物速度改回旧怪物的数据，造成状态污染。

### 8. 本次涉及知识点
- 防御式编程
- 对象池重复回收保护
- `activeSelf` 和 `activeInHierarchy` 的区别
- 有限循环替代危险 `while`
- 对象复用版本号
- 持续状态和对象池的冲突处理

## 功能名称：五次击破箱子后开启 Boss 传送门

### 1. 实现目标
当玩家在主玩法场景中累计把金库/箱子击破到第 5 次时，普通刷怪流程停止，场景中现有普通怪、怪物子弹和箱子被清理，最后一次箱子所在位置生成 Boss 传送门。玩家角色接触传送门后切换到 `BossRoomScene`，Boss 房间会补齐玩家出生点、输入、摄像机、基础地面和 Spider King Boss。

### 2. 涉及脚本
- `Assets/Script/World/BoxCo.cs`：已有的箱子击破事件源，`HandleDestroyed` 中通过 `OnVaultDestroyed` 广播击破。
- `Assets/Script/Boss/Flow/BossPortalUnlockController.cs`：新增 Boss 入口流程控制器，监听箱子击破次数，负责清场和生成传送门。
- `Assets/Script/Boss/Flow/BossScenePortal.cs`：新增传送门触发器，检测玩家接触后加载 Boss 房间。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：新增 Boss 房间运行时启动器，兜底创建 Boss 房间所需基础对象。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：新增编辑器菜单工具，一键创建或刷新可编辑的 Boss 房间场景。
- `Assets/Script/Enemies/MonsSpawner.cs`：新增停止刷怪和清理活怪接口。
- `Assets/Script/Enemies/MonsterManager.cs`：新增统一停止刷怪并清理普通怪的入口。
- `Assets/Script/Player/GameplayCharacterSpawner.cs`：新增场景引用自动补齐和外部配置入口，让新 Boss 场景不用手动拖引用也能生成玩家。
- `Assets/Script/Core/GameSceneNames.cs`：新增 `BossRoomScene` 场景名常量。
- `Assets/Script/Services/SceneFlowService.cs`：新增统一进入 Boss 房间的场景切换方法。

### 3. 调用流程
箱子击破流程：`玩家攻击 -> BoxCo.Hit -> TakeDamage -> HandleDestroyed -> OnVaultDestroyed`

Boss 入口解锁流程：`BoxCo.OnVaultDestroyed -> BossPortalUnlockController.HandleVaultDestroyed -> 判断 DestroyedCount >= 5 -> StopSpawningAndClearMonsters -> HideVaultsInScene -> SpawnPortal`

进入 Boss 房间流程：`玩家碰到传送门 -> BossScenePortal.OnTriggerEnter/Stay -> 检测 PlayerRuntimeController -> SceneFlowService.LoadBossRoomScene -> SceneManager.LoadScene("BossRoomScene")`

Boss 房间初始化流程：`BossRoomScene 加载 -> BossRoomSceneBootstrap.BuildRoomIfNeeded -> 创建 PlayerSpawnPoint / BossSpawnPoint / Camera / Input / Spawner / Spider King -> GameplayCharacterSpawner 生成玩家`

### 4. 核心原理
这个功能本质上是一个“关卡阶段切换”。箱子负责广播“我被击破了”，Boss 入口控制器负责判断击破次数是否达到阈值，传送门只负责检测玩家进入并切场景。这样拆分后，箱子不用知道 Boss 房间在哪里，传送门也不用知道箱子被打了几次，每个类职责更清晰。

清场时先停止刷怪器，再清理已经生成的普通怪，可以避免传送门出现后场景又继续刷怪。传送门使用 Trigger Collider + Kinematic Rigidbody，这样玩家的 CharacterController 或碰撞体进入区域时可以稳定触发 `OnTriggerEnter/OnTriggerStay`。Boss 房间用独立场景承载，是商业项目里常见做法，后续可以单独做 Boss AI、镜头、UI、音乐和结算，不会把主场景逻辑越塞越乱。

### 5. Unity 测试方式
1. 打开 `MainScene`，进入 Play Mode。
2. 正常攻击箱子，连续击破 5 次。
3. 第 5 次击破后，观察普通史莱姆是否消失、刷怪点是否停止继续刷怪、场景中箱子是否隐藏。
4. 观察箱子原位置附近是否出现蓝绿色的临时传送门。
5. 控制玩家走进传送门，确认场景切换到 `BossRoomScene`。
6. 进入 Boss 房间后，确认玩家在 `PlayerSpawnPoint` 附近生成，Spider King 在 `BossSpawnPoint` 附近生成。
7. 如果想让 Boss 房间在编辑器里直接可见，点击菜单 `Treasure Hunter/Boss/Create Or Refresh Boss Room Scene`，工具会重新生成 `Assets/Scenes/BossRoomScene.unity` 并加入 Build Settings。

### 6. 面试表达
我把 Boss 入口做成了一个关卡阶段切换流程。箱子本身只负责受击、奖励和广播击破事件；BossPortalUnlockController 监听这个事件，当击破次数达到 5 次后停止刷怪、清理场景怪物和箱子，并在箱子位置生成传送门。传送门只负责检测玩家进入，然后通过统一的 SceneFlowService 切换到 BossRoomScene。Boss 房间又单独用 Bootstrap 补齐玩家出生点、摄像机、输入和 Spider King，这样主场景和 Boss 战场景解耦，后续我要扩展 Boss 行为树、Boss 血条、战斗结算，都可以在 BossRoomScene 里继续做，不会污染主关卡逻辑。

### 7. 面试追问
1. **为什么不用箱子脚本直接切 Boss 场景？** 箱子只应该管受击和奖励，如果直接切场景会让箱子耦合关卡流程，后续换成打怪开门或任务开门就很难复用。
2. **为什么传送门单独一个脚本？** 传送门是表现和触发入口，它不关心解锁条件，只关心玩家是否进入，这样职责清楚，也方便以后做多个传送门。
3. **为什么先停止刷怪再清怪？** 如果只清掉现有怪物，刷怪器下一帧可能又生成新怪，所以要先关闭刷怪入口，再清理场上对象。
4. **为什么 Boss 房间做成新场景？** Boss 战通常有独立地形、镜头、音乐、AI、UI 和结算流程，拆成场景更利于管理和扩展。
5. **后续怎么接行为树 Boss？** 可以在 Spider King 上挂 BossBlackboard、BossAIController 和行为树节点，让 BossRoomSceneBootstrap 只负责生成 Boss，不直接写 Boss 行为。

### 8. 本次涉及知识点
- Unity 事件解耦
- Trigger Collider 和 Rigidbody 触发条件
- 场景切换 `SceneManager.LoadScene`
- Build Settings 场景配置
- 运行时场景 Bootstrap
- 刷怪器停止和清场流程
- `.meta` 文件在 Unity 版本管理中的作用
- 主场景和 Boss 战场景解耦设计

## 功能名称：Boss 入口开发者调试与玩家状态继承

### 1. 实现目标
补充 Boss 入口测试和跨场景继承能力。开发者模式开启后可以按 `N` 自动击破一次宝箱，方便快速测到第 5 次开门；Boss 传送门生成位置改为宝箱远离玩家的一侧，避免刚击破宝箱就误触传送；玩家进入 Boss 房间前会保存主场景角色、属性和技能快照，Boss 房间生成玩家后恢复这些数据，保证刺客进 Boss 房间还是刺客，并延续血量、蓝量、等级、经验、战斗属性和技能状态。

### 2. 涉及脚本
- `Assets/Script/Input/IGameplayInput.cs`：新增 `DebugBreakVaultDown` 输入接口。
- `Assets/Script/Input/InputCo.cs`：新增 `N` 键采样。
- `Assets/Script/Tests/PlayerDeveloperModeComponent.cs`：开发者模式下按 `N` 调用宝箱开发击破入口。
- `Assets/Script/World/BoxCo.cs`：新增 `BreakOnceForDevelopment`，复用正式击破流程。
- `Assets/Script/Boss/Flow/BossPortalUnlockController.cs`：传送门生成时增加水平偏移，并根据玩家位置选择远离玩家的一侧。
- `Assets/Script/Boss/Flow/BossScenePortal.cs`：玩家进入传送门前捕获跨场景快照。
- `Assets/Script/Player/PlayerSceneTransferState.cs`：新增跨场景玩家快照数据和暂存状态。
- `Assets/Script/Architecture/Models/PlayerModel.cs`：新增从快照恢复玩家属性的入口。
- `Assets/Script/Architecture/Commands/PlayerSceneTransferCommands.cs`：新增恢复玩家快照的 Command。
- `Assets/Script/Player/GameplayCharacterSpawner.cs`：Boss 房间生成玩家时优先消费快照，并在生成后恢复属性和技能。

### 3. 调用流程
开发者击破流程：`F1 开启开发者模式 -> InputCo 采样 N -> PlayerDeveloperModeComponent.BreakVaultOnce -> BoxCo.BreakOnceForDevelopment -> HandleDestroyed -> OnVaultDestroyed`

传送门偏移流程：`BoxCo 第 5 次击破 -> BossPortalUnlockController.CalculatePortalPosition -> 根据玩家与宝箱方向计算远离玩家的位置 -> SpawnPortal`

玩家状态继承流程：`玩家进入传送门 -> BossScenePortal.TryEnterBossRoom -> PlayerSceneTransferState.TryCaptureFrom -> SceneManager.LoadScene(BossRoomScene) -> GameplayCharacterSpawner.TryConsume -> EnterCurrentCharacter -> ApplyCharacterEntryData -> RestorePlayerSceneTransferSnapshotCommand`

### 4. 核心原理
开发者模式仍然走正式宝箱击破流程，而不是直接把 Boss 门次数加 1，这样测试时能覆盖真实事件链，避免“调试能过、正式玩法不能过”。传送门位置不是写死在世界坐标，而是根据玩家与宝箱的相对方向动态计算，把门放在宝箱远离玩家的一侧，减少误触。

跨场景玩家继承采用“数据快照”而不是 `DontDestroyOnLoad` 玩家 GameObject。因为玩家 GameObject 上会带着场景引用、摄像机跟随、碰撞父节点、动画状态等，如果直接跨场景保留，容易出现引用错乱。快照只保存职业、存档、属性和技能等权威数据；Boss 房间重新生成一个干净的玩家对象，再把数据恢复进去。这样既能保持玩家成长结果，又能让新场景拥有自己的出生点、摄像机和场景结构。

### 5. Unity 测试方式
1. 打开 `MainScene`，进入 Play Mode。
2. 选择或确认当前玩家是刺客。
3. 按 `F1` 开启开发者模式，屏幕左上角会显示调试提示。
4. 按 `N` 自动击破一次宝箱；等宝箱重生动画结束后继续按，直到第 5 次。
5. 第 5 次后观察传送门是否出现在宝箱旁边，而不是宝箱原位置。
6. 走进传送门，进入 `BossRoomScene`。
7. 检查 Boss 房间内玩家模型是否仍是刺客。
8. 检查玩家等级、血量、蓝量、攻击力、移速、已学技能和技能冷却是否延续主场景状态。
9. 在 Boss 房间测试移动、攻击、翻滚和技能释放是否正常。

### 6. 面试表达
我给 Boss 入口补了一个开发者调试链路和跨场景玩家状态继承。开发者模式下按 N 会调用宝箱自己的开发击破方法，这个方法仍然复用正式击破流程，所以奖励、事件和 Boss 门解锁逻辑都能一起测到。进入 Boss 房间前，我没有直接把玩家 GameObject 带过去，而是保存玩家的运行时快照，包括职业、等级、血蓝、战斗属性和技能状态。Boss 场景重新生成干净的玩家对象后，再通过 Command 恢复这份快照。这样既保证 Boss 房间里的角色和主场景一致，也避免跨场景保留 GameObject 导致摄像机、UI、碰撞引用混乱。

### 7. 面试追问
1. **为什么调试击破不直接让 Boss 门计数 +1？** 因为那会绕过正式宝箱事件，测不到奖励、难度成长、HUD 和怪物系统是否正常响应。
2. **为什么传送门要根据玩家位置偏移？** 玩家击破宝箱时通常站在宝箱附近，把门放在远离玩家的一侧可以降低误触概率。
3. **为什么不用 `DontDestroyOnLoad` 保留玩家？** 玩家对象持有很多场景引用，直接跨场景容易引用旧摄像机、旧 UI 或旧父节点，数据快照更安全。
4. **哪些状态应该继承？** 职业、等级、血量、蓝量、经验、攻击、移速、成长属性、已学技能和冷却应该继承。
5. **哪些状态不建议继承？** 翻滚中、攻击连击中、跳跃中、升级面板激活中这类短动作状态不适合继承，进入新场景应回到干净操作状态。

### 8. 本次涉及知识点
- 开发者模式 Debug 快捷键
- 正式流程复用与调试入口设计
- 跨场景数据快照
- 数据继承与 GameObject 继承的区别
- QFramework Command 恢复运行时数据
- 玩家属性模型和技能模型恢复
- 根据相对方向计算生成位置
- Boss 场景玩家一致性设计

## 功能名称：Spider King Boss 行为树与 Boss 战 UI

### 1. 实现目标
把场景里的 Spider King 正式接成 Boss：玩家进入 BossRoomScene 后会自动生成 Boss 战 UI，Spider King 会通过轻量行为树进行待机、追击、近战攻击、远程法术和狂暴阶段切换。Boss 实现 `FighterInterface`，所以玩家普通攻击和技能都能复用现有命中流程打到 Boss。Boss 死亡后 UI 会显示胜利面板，并提供返回主场景按钮。

### 2. 涉及脚本
- `Assets/Script/Boss/AI/BossBehaviorTree.cs`：轻量行为树框架，包含选择节点、顺序节点、条件节点和行为节点。
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：Spider King 的 Boss 控制器，负责生命、受击、死亡、AI 决策、移动、近战和法术攻击。
- `Assets/Script/Boss/UI/BossBattleHudUi.cs`：Boss 战 HUD，运行时自动创建 Boss 血条、阶段文本、玩家状态和胜利面板。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：Boss 房间启动器，进入 BossRoomScene 后自动补齐 Spider King Boss 控制器和 Boss 战 UI。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：编辑器一键生成 BossRoomScene 时，也会自动创建 Spider King 和 BossBattleHudUi。
- `Assets/Script/Combat/WeaponCo.cs`：普通攻击命中 Boss 时，按 Boss 当前剩余血量计算真实伤害飘字和吸血。
- `Assets/Script/Combat/FloatingCombatText.cs`：技能伤害命中 Boss 时，同样按 Boss 当前血量预估真实生效伤害。

### 3. 调用流程
Boss 房间初始化：`BossRoomScene 加载 -> BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureSpiderKing -> AddComponent<SpiderKingBossController> -> EnsureBossBattleUi -> BossBattleHudUi.BindBoss`

Boss AI 流程：`SpiderKingBossController.Update -> behaviorTree.Tick -> Selector 按优先级判断 -> 近战 / 法术 / 追击 / 待机`

玩家打 Boss：`PlayerCombatComponent / PlayerSkillCastComponent -> FighterInterface.Hit -> SpiderKingBossController.Hit -> 扣血 -> BossStatsChanged -> BossBattleHudUi.RefreshBoss`

Boss 打玩家：`行为树选择攻击节点 -> 播放攻击动画 -> 延迟结算伤害 -> PlayerHealthComponent.Hit -> PlayerModel 扣血 -> BossBattleHudUi.RefreshPlayerStats`

### 4. 核心原理
行为树可以理解成一棵“优先级决策树”。这次根节点是选择节点，它每帧从左到右判断：如果 Boss 正在攻击动作锁定，就继续当前动作；如果玩家在近战范围并且冷却好了，就执行近战；如果玩家在法术范围并且冷却好了，就释放法术；如果玩家在检测范围，就追击；否则待机。

`SpiderKingBossController` 不直接改玩家攻击系统，而是实现项目已有的 `FighterInterface`。这样玩家武器、火球、毒雾、旋风斩只需要继续找 `FighterInterface`，不需要知道目标是史莱姆、宝箱还是 Boss。UI 也没有直接去控制 Boss 行为，只监听 Boss 的血量变化事件，这样逻辑层和表现层分开。

Boss UI 使用运行时自动生成，是为了降低场景装配成本。以后要替换成正式美术 Prefab 时，只需要把 `BossBattleHudUi` 的创建方式换成加载 Prefab，Boss AI 和战斗逻辑不用动。

### 5. Unity 测试方式
1. 打开 `MainScene`，进入 Play Mode。
2. 按 `F1` 开启开发者模式，再按 `N` 快速击破宝箱 5 次。
3. 进入传送门，确认切换到 `BossRoomScene`。
4. 进入后确认屏幕顶部出现 Spider King 血条，左上角出现玩家 HP / MP / ATK。
5. 靠近 Boss，观察 Boss 是否转向并追击玩家；近距离应触发咬/爪击，中距离应释放紫色范围法术。
6. 用普通攻击或技能攻击 Boss，确认 Boss 血条下降，并有伤害飘字。
7. 把 Boss 打到 35% 以下，确认阶段文本变成“狂暴阶段”，Boss 攻击和移动节奏变快。
8. 击败 Boss 后，确认屏幕中央出现胜利面板，点击“返回主场景”能回到 `MainScene`。

### 6. 面试表达
这个 Boss 战我主要分成三层：第一层是 Boss 房间启动器，负责进入 BossRoomScene 后补齐玩家、相机、Boss 和 UI；第二层是 SpiderKingBossController，负责 Boss 的生命、受击和 AI 行为；第三层是 BossBattleHudUi，只负责显示 Boss 血条、阶段和玩家状态。Boss AI 我用了一个轻量行为树，根节点是 Selector，按“动作锁定、近战、法术、追击、待机”的优先级决策。Boss 受击没有改玩家攻击系统，而是实现了已有的 FighterInterface，所以普通攻击和技能都能复用原来的命中流程。这样做的好处是职责比较清楚，Boss 后续要加二阶段、召唤小怪或者正式 UI Prefab，都可以在当前结构上扩展。

### 7. 面试追问
1. **为什么 Boss 用行为树，不直接写一堆 if else？** 行为树能把条件和动作拆成节点，优先级清楚，后续新增技能或阶段时只需要加分支，不容易把 Update 写乱。
2. **Selector 和 Sequence 有什么区别？** Selector 是“从多个方案里选一个能执行的”，Sequence 是“一串条件和动作必须按顺序都成功”。Boss 的近战分支就是 Sequence：先判断能否近战，再执行近战动作。
3. **Boss 为什么实现 FighterInterface？** 这样玩家攻击系统只依赖“可受击接口”，不依赖具体 Boss 类，史莱姆、宝箱、Boss 都能走统一 Hit 流程。
4. **为什么攻击伤害要延迟结算？** 因为 Boss 播放攻击动画后，真正命中通常发生在动画中段，延迟结算能让表现和逻辑更一致。
5. **后续怎么扩展成更商业化的 Boss？** 可以把 Boss 数值和技能做成 ScriptableObject 配置，把行为树节点做成可视化或配置化，并加入二阶段、技能预警圈、召唤小怪、硬直和结算奖励。

### 8. 本次涉及知识点
- 行为树 Selector / Sequence / Condition / Action
- Boss AI 优先级决策
- `FighterInterface` 接口解耦
- Boss 血量、受击、死亡事件
- UI 监听事件刷新，而不是每帧强耦合查询
- Animator 状态名播放与兜底检查
- CharacterController 追击移动
- 延迟伤害结算
- Boss 狂暴阶段数值倍率
- 场景 Bootstrap 自动装配

## 功能名称：粉红传送门与 Boss 胜利返回入口

### 1. 实现目标
把主场景中五次击破宝箱后出现的 Boss 入口传送门改为粉红色，并在 Boss 被击败后，在 Boss 房间生成一个新的粉红色返回传送门。玩家接触返回传送门后会回到主玩法场景 `MainScene`，并在切换前保存当前玩家快照，尽量保持角色、血量、蓝量、等级、属性和技能状态延续。

### 2. 涉及脚本
- `Assets/Script/Boss/Flow/BossPortalUnlockController.cs`：入口传送门颜色改为粉红色，同时对自定义传送门 Prefab 做染色兜底。
- `Assets/Script/Boss/Flow/BossScenePortal.cs`：从“只进入 Boss 房间”的脚本扩展为“可配置目标场景”的通用传送门。
- `Assets/Script/Boss/Flow/BossVictoryPortalSpawner.cs`：新增 Boss 胜利传送门生成器，监听 Boss 死亡事件并生成返回主场景的传送门。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：Boss 房间启动时自动创建并绑定 `BossVictoryPortalSpawner`。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：编辑器一键生成 Boss 房间时同步创建胜利传送门生成器。

### 3. 调用流程
入口传送门流程：`BoxCo 第 5 次击破 -> BossPortalUnlockController.SpawnPortal -> BossScenePortal.ConfigureTargetScene(BossRoomScene) -> 玩家接触 -> 保存玩家快照 -> LoadBossRoomScene`

胜利返回流程：`SpiderKingBossController.Die -> BossDied 事件 -> BossVictoryPortalSpawner.HandleBossDied -> 延迟生成 ReturnToMainScenePortal -> BossScenePortal.ConfigureTargetScene(MainScene) -> 玩家接触 -> 保存玩家快照 -> RestartGameplay`

### 4. 核心原理
这次重点是把传送门触发逻辑复用起来。原本 `BossScenePortal` 默认只负责进入 Boss 房间，现在给它增加了目标场景配置入口，所以同一个脚本既可以作为“进 Boss 门”，也可以作为“Boss 胜利后的回城门”。这样做比复制一个 `ReturnPortal` 脚本更干净，因为触发检测、快照保存、Build Settings 检查和场景切换都能共用。

Boss 胜利传送门没有放在 UI 里生成，而是单独做了 `BossVictoryPortalSpawner`。原因是 UI 只应该负责提示和显示，真正的场景物体生成属于玩法流程。这样以后即使把胜利 UI 换成正式结算界面，Boss 死亡后开门的逻辑也不会受影响。

### 5. Unity 测试方式
1. 打开 `MainScene` 并进入 Play Mode。
2. 按 `F1` 开启开发者模式，再按 `N` 快速击破宝箱 5 次。
3. 确认出现的 Boss 入口传送门是粉红色。
4. 走进入口门，进入 `BossRoomScene`。
5. 击败 Spider King。
6. 等约 1.2 秒，确认 Boss 附近出现粉红色 `ReturnToMainScenePortal`。
7. 走进返回传送门，确认切回 `MainScene`。
8. 检查玩家角色和属性是否延续。

### 6. 面试表达
我把传送门逻辑做了一次小抽象。原本传送门只负责进入 Boss 房间，我把它改成可以配置目标场景，所以主场景入口和 Boss 胜利返回入口都能复用同一个触发脚本。Boss 死亡后不是 UI 直接切场景，而是由 `BossVictoryPortalSpawner` 监听 Boss 死亡事件，在场景里生成一个返回传送门。这样 UI、Boss AI 和场景流转职责分开，后续要做正式传送门 Prefab、结算动画或者多个 Boss 房间，也不用重写传送逻辑。

### 7. 面试追问
1. **为什么不直接在 Boss 死亡后 LoadScene？** 直接切场景会缺少玩家主动选择和胜利后的停留反馈，生成传送门更像完整关卡流程。
2. **为什么复用 BossScenePortal？** 传送门的触发检测和切场景流程相同，只是目标场景不同，复用能减少重复代码。
3. **为什么返回前也保存玩家快照？** 因为 Boss 战中玩家血量、蓝量、等级或技能状态可能变化，切回主场景时应该延续当前状态。
4. **为什么胜利传送门生成器不放 UI 脚本里？** UI 只负责显示，生成场景物体属于玩法流程，分开后更容易替换 UI。
5. **如果要回到 Boss 前完全一样的主场景状态怎么做？** 需要进一步做关卡状态快照，保存宝箱、怪物、传送门、掉落物等运行时状态，而不只是玩家快照。

### 8. 本次涉及知识点
- 通用传送门脚本设计
- 目标场景可配置
- Boss 死亡事件监听
- 胜利后场景物体生成
- 运行时材质染色和发光色
- 玩家跨场景快照保存
- UI 提示与玩法生成逻辑解耦

## 功能名称：Boss 房间封闭长方体空间

### 1. 实现目标
将 BossRoomScene 从单块地板升级为封闭长方体内部空间，包含地板、四面墙和天花板，避免玩家或 Boss 跑出边界后因重力掉落。这个房间属于灰盒关卡，先保证战斗边界和体验稳定，后续可以再替换成正式美术场景。

### 2. 涉及脚本
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：运行时进入 BossRoomScene 后自动创建或更新封闭房间。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：编辑器一键生成 BossRoomScene 时同步创建同尺寸的封闭房间。

### 3. 调用流程
运行时流程：`BossRoomScene 加载 -> BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureArenaRoom -> CreateOrUpdateArenaPiece -> 生成 Floor / 四面 Wall / Ceiling`

编辑器流程：`Treasure Hunter/Boss/Create Or Refresh Boss Room Scene -> CreateArenaRoom -> CreateArenaPiece -> 保存 BossRoomScene`

### 4. 核心原理
Boss 房间使用 Unity 的 Cube 搭建灰盒空间。Cube 自带 BoxCollider，所以地板可以承接玩家，四面墙可以阻挡 CharacterController，天花板让它更像一个封闭的内部战斗房间。尺寸被做成公共常量，运行时 Bootstrap 和编辑器工具共用同一套默认尺寸，避免“运行时生成的房间”和“编辑器里看到的房间”不一致。

### 5. Unity 测试方式
1. 打开 `MainScene`，进入 Play Mode。
2. 按 `F1` 开启开发者模式，再按 `N` 快速击破宝箱 5 次。
3. 进入粉红色 Boss 入口传送门，切换到 `BossRoomScene`。
4. 在 Hierarchy 中检查 `BossRoomRoot` 下是否有 `BossArenaFloor`、四个 `BossArena...Wall` 和 `BossArenaCeiling`。
5. 控制玩家贴着四个方向的墙移动、翻滚或释放技能，确认不会掉出场景。
6. 击败 Boss 后确认返回传送门仍然能在房间内部生成，并能传送回 `MainScene`。

### 6. 面试表达
Boss 房间我没有只放一块地板，而是做了一个运行时可自动生成的封闭灰盒场景。它由地板、四面墙和天花板组成，每个部分都用 Cube 的 BoxCollider 提供物理边界，这样玩家和 Boss 的 CharacterController 不会跑出场景掉落。我把房间尺寸做成公共常量，让运行时兜底生成逻辑和编辑器一键生成工具共用同一套配置，保证开发阶段和实际运行时效果一致。后续如果要替换正式美术场景，也可以保留这套边界生成逻辑作为安全兜底。

### 7. 面试追问
1. **为什么先用灰盒而不是直接做正式场景？** 灰盒能先验证玩法空间、碰撞边界、镜头和 Boss 追击距离，等战斗体验稳定后再替换美术资源，开发效率更高。
2. **为什么墙和地板用 Cube？** Cube 自带 BoxCollider，适合快速搭建关卡边界，不需要额外写碰撞脚本。
3. **为什么运行时也要生成房间？** 防止场景漏放地板或墙体时 Boss 战无法正常测试，Bootstrap 可以自动兜底补齐必要对象。
4. **如果角色还是掉下去怎么排查？** 先看地板是否存在、Collider 是否启用、玩家 CharacterController 的高度和中心点是否正常，再看是否有脚本把玩家传到房间外。
5. **后续怎么升级成正式关卡？** 可以把灰盒替换成美术 Prefab，同时保留隐藏碰撞墙；尺寸也可以改成 ScriptableObject 配置，支持多个 Boss 房间复用。

### 8. 本次涉及知识点
- Unity 灰盒关卡搭建
- Cube / BoxCollider 作为关卡边界
- CharacterController 与碰撞体的阻挡关系
- 场景 Bootstrap 兜底生成
- 编辑器工具和运行时代码共用配置
- Boss 战斗空间尺寸设计

## 功能名称：Boss 战运行期 Bug 修复

### 1. 实现目标
修复 Boss 战闭环中几个高概率运行期问题：胜利 UI 返回主场景时没有保存玩家快照、Boss 死在墙边时返回传送门可能刷到墙里、旧 BossRoomScene 序列化字段导致房间尺寸兜底不稳、手动放入场景的 Spider King 可能不在 Boss 出生点。修复后，Boss 战从进入、战斗、胜利到返回主场景的流程更稳定。

### 2. 涉及脚本
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：补齐 SkillVisualPool，强化房间尺寸兜底，并统一校正 Spider King 的出生位置和父节点。
- `Assets/Script/Boss/Flow/BossVictoryPortalSpawner.cs`：限制胜利返回传送门生成在 Boss 房间内部，避免刷到墙体或房间外。
- `Assets/Script/Boss/UI/BossBattleHudUi.cs`：点击胜利 UI 返回主场景前，先保存玩家跨场景快照。

### 3. 调用流程
进入 Boss 房间：`BossRoomScene 加载 -> BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureSkillVisualPool / EnsureArenaRoom / EnsureSpiderKing -> 生成玩家和 Boss`

Boss 胜利返回：`SpiderKingBossController.Die -> BossDied -> BossVictoryPortalSpawner.SpawnReturnPortal -> ClampPortalPositionInsideArena -> BossScenePortal 返回主场景`

UI 按钮返回：`BossBattleHudUi.ReturnToMainScene -> PlayerSceneTransferState.TryCaptureFrom -> SceneFlowService.RestartGameplay`

### 4. 核心原理
这次修复的核心不是新增玩法，而是保证场景流转过程中的“状态一致性”和“位置安全”。玩家跨场景不能直接带 GameObject，因为旧场景的摄像机、UI、碰撞引用会一起带过去，所以仍然使用快照保存血量、蓝量、等级、技能等数据。传送门位置不能完全相信 Boss 死亡点，因为 Boss 可能死在墙边，所以生成后要根据房间尺寸夹取到安全范围内。手动放进场景的 Spider King 也不应该绕过 Bootstrap 管理，否则出生点、缩放、父节点可能不一致。

### 5. Unity 测试方式
1. 打开 `MainScene` 进入 Play Mode。
2. 按 `F1` 开启开发者模式，再按 `B` 击破宝箱 5 次。
3. 进入粉红色入口传送门，确认进入 BossRoomScene。
4. 在 Boss 房间中确认玩家仍是主场景角色，并且血量、蓝量、等级、技能状态保持。
5. 把 Spider King 拉到墙边附近击败，确认返回传送门仍生成在房间内部。
6. 分别测试走进返回传送门、点击胜利 UI 返回按钮，两种方式都应回到 `MainScene` 并保留玩家状态。

### 6. 面试表达
这次我主要修的是 Boss 战跨场景流程的稳定性。玩家进入和离开 Boss 房间时，我没有直接 DontDestroyOnLoad 携带玩家对象，而是用快照保存角色、属性和技能状态，再让新场景重新生成玩家并恢复数据，这样不会把旧场景的摄像机、UI 和碰撞引用带进新场景。另外，Boss 死亡后的返回传送门会根据 Boss 房间尺寸做位置夹取，避免 Boss 死在墙边时传送门刷到墙里。Bootstrap 也会统一校正手动摆放的 Spider King，保证场景对象最终进入同一套运行时管理流程。

### 7. 面试追问
1. **为什么 UI 返回按钮也要保存快照？** 因为它和传送门一样会切场景，如果不保存快照，Boss 战里的当前血蓝和技能冷却就可能丢失。
2. **为什么不直接让玩家对象跨场景不销毁？** 直接带对象会把旧场景引用一起带过去，容易出现摄像机、UI、输入或碰撞残留问题；快照恢复更干净。
3. **为什么传送门位置要 Clamp？** Boss 可能死在墙边或角落，如果只按方向偏移，传送门可能生成到墙里，Clamp 可以保证它仍在战斗区域内。
4. **为什么手动放的 Spider King 还要运行时校正？** 手动摆放容易有位置、缩放、父节点不统一的问题，Bootstrap 统一处理后，Boss 战流程更可控。
5. **这类 Bug 怎么排查？** 先看编译是否通过，再沿着场景流转链路查：触发入口、状态保存、场景加载、对象生成、事件绑定和返回流程。

### 8. 本次涉及知识点
- 跨场景状态快照
- UI 按钮和场景传送门复用同一状态保存原则
- 运行时对象兜底生成
- 传送门生成位置安全夹取
- 手动场景对象与 Bootstrap 自动装配的一致性
- Boss 战运行期 Bug 排查思路
## 功能名称：Boss 战场景复用主场景 UI

### 1. 实现目标
让 BossRoomScene 不再维护一套重复的玩家 HUD，而是直接复用主场景的 `GameplayUiRoot.prefab`。玩家血量、蓝量、体力、经验、技能栏、暂停面板等通用玩法 UI 都来自同一个 Prefab；Boss 场景只额外叠加 Boss 血条、阶段提示和胜利面板。

### 2. 涉及脚本
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：Boss 房间初始化时确保场景里存在 `GameplayUiRoot`，再创建 Boss 专属叠加 UI。
- `Assets/Script/Boss/UI/BossBattleHudUi.cs`：职责收窄，只显示 Boss 血条、阶段文本、提示和胜利返回按钮，不再显示重复的玩家状态面板。
- `Assets/Script/Core/GameplayRuntime.cs`：增加金库进度缓存，让 Boss 场景没有宝箱对象时，主 UI 仍能显示进入 Boss 前的分数和宝箱次数。
- `Assets/Script/Services/SceneFlowService.cs`：进入 Boss 房间前缓存当前宝箱进度，新开局或退出登录时清理缓存。
- `Assets/Script/UI/GameSessionUi.cs`：运行时统一读取 `GameplayRuntime` 的真实数据，编辑器预览时才使用预览分数。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：一键生成 BossRoomScene 时同步放入 `GameplayUiRoot.prefab`。

### 3. 调用流程
进入 Boss 房间 UI 流程：`BossScenePortal -> SceneFlowService.LoadBossRoomScene -> GameplayRuntime.CacheCurrentVaultProgress -> BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureGameplayUiRoot -> EnsureBossBattleUi`

玩家 HUD 刷新流程：`GameplayUiRoot.prefab -> PlayerHudUi / PlayerSkillBarUi / GameSessionUi -> GameplayRuntime / PlayerModel / PlayerSkillModel`

Boss 专属 UI 刷新流程：`SpiderKingBossController -> BossStatsChanged / BossDied -> BossBattleHudUi.RefreshBoss`

### 4. 核心原理
通用 UI 和 Boss 专属 UI 要分开。玩家血量、蓝量、技能栏这些属于所有玩法场景都会用到的“通用玩法 UI”，所以应该复用同一个 `GameplayUiRoot.prefab`。Boss 血条和 Boss 阶段提示只属于 Boss 战，因此保留在 `BossBattleHudUi` 里作为叠加层。

这样做的好处是后续你改玩家 HUD 样式、技能栏、暂停菜单时，主场景和 Boss 场景会一起生效，不需要改两份 UI。`GameplayRuntime` 增加缓存，是为了解决 Boss 场景没有宝箱对象时主 UI 取不到分数的问题；进入 Boss 前缓存一次，Boss 场景 UI 就能继续显示上一段主场景进度。

### 5. Unity 测试方式
1. 打开 `MainScene` 进入 Play Mode。
2. 选择角色进入主场景，确认主场景 UI 正常显示血量、蓝量、体力、技能栏、分数和宝箱次数。
3. 按 `F1` 开启开发者模式，再按 `N` 自动击破宝箱 5 次。
4. 进入粉色 Boss 传送门。
5. 到 Boss 房间后检查：玩家 HUD、技能栏、暂停 UI 应该和主场景一致；屏幕上方额外显示 Boss 血条和阶段文字。
6. 确认不会再出现 Boss 专用的重复玩家状态面板。
7. 击败 Boss 后，胜利面板和返回传送门仍然正常出现。

### 6. 面试表达
我把 Boss 战 UI 拆成了“通用玩法 UI”和“Boss 专属 UI”两层。通用玩法 UI 使用主场景的 `GameplayUiRoot.prefab`，所以玩家血量、蓝量、体力、技能栏和暂停面板在主场景和 Boss 场景里完全复用；Boss 专属 UI 只负责 Boss 血条、阶段提示和胜利面板。进入 Boss 房间前，我会把当前宝箱分数和击破次数缓存到 `GameplayRuntime`，因为 Boss 场景没有宝箱对象，但复用的主 UI 仍然需要显示上一段玩法进度。这样设计可以减少重复 UI 代码，也方便后续统一改 HUD 或扩展多个 Boss 场景。

### 7. 面试追问
1. **为什么不直接复制一套 Boss 战玩家 HUD？** 复制会导致两套 UI 同时维护，后续改血条、技能栏或暂停面板时容易漏改，复用 Prefab 更符合组件化思路。
2. **BossBattleHudUi 为什么还保留？** 因为 Boss 血条、阶段文字和胜利提示是 Boss 战特有信息，不应该塞进所有场景都会使用的通用玩家 HUD。
3. **Boss 场景没有宝箱，分数怎么显示？** 进入 Boss 场景前由 `SceneFlowService` 调用 `GameplayRuntime.CacheCurrentVaultProgress()` 缓存当前分数和宝箱次数，Boss 场景复用 UI 时读取缓存。
4. **如果以后有多个 Boss 房间怎么办？** 多个 Boss 房间都可以复用 `GameplayUiRoot.prefab`，只需要给不同 Boss 绑定不同的 Boss 专属 UI 或 Boss 数据。
5. **正式打包时要注意什么？** 如果 Prefab 不在 `Resources` 目录，运行时不能直接通过路径加载；所以 BossRoomScene 应该通过编辑器工具提前放入 `GameplayUiRoot` 实例，或者后续改成 Addressables/Resources 管理。

### 8. 本次涉及知识点
- UI Prefab 复用
- 通用 UI 与战斗专属 UI 的职责拆分
- 跨场景运行时数据缓存
- Bootstrap 场景初始化
- Unity 编辑器工具同步生成场景对象
- UI 数据来源与表现层解耦

## 功能名称：Boss 近身防重叠与受击动画节流

### 1. 实现目标
修复 Spider King 靠近玩家后挤到玩家头顶、导致玩家攻击不到 Boss 的问题；同时把 Boss 房间地板改为浅绿色，并补充 Boss 被玩家攻击时的受击反馈规则：第一次受击播放受击动画，受击动画结束后的 3 秒内再次受击只闪色，3 秒后才允许重新播放受击动画。

### 2. 涉及脚本
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：负责 Boss 行为树、移动、近身距离控制、受击动画、闪色、伤害结算。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：负责 Boss 房间运行时兜底生成，包含地板颜色和出生点位置修正。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：负责编辑器一键生成 Boss 房间，同步浅绿色地板。

### 3. 调用流程
玩家攻击 Boss：`WeaponCo / PlayerSkillCastComponent -> FighterInterface.Hit -> SpiderKingBossController.Hit -> StartHitFlash -> TryPlayTakeDamageReaction`

Boss 靠近玩家：`SpiderKingBossController.Update -> 行为树 Tick -> ShouldSeparateFromTarget / CanChasePlayer -> DoSeparateFromTarget / DoChase -> CharacterController.Move`

Boss 房间生成：`BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureArenaRoom -> EnsureSpawnPoint -> EnsureSpiderKing`

### 4. 核心原理
Boss 贴到玩家头顶的主要原因是 Boss 追击逻辑只有“检测范围”，没有“到达安全近身距离后停止”的判断，导致它持续朝玩家中心移动；再叠加 `CharacterController.stepOffset` 较大时，Unity 可能把玩家碰撞体当成可以跨上去的台阶。修复方式是给 Boss 加近身停止距离和过近后退逻辑，同时降低 Boss 的 `stepOffset`，让它不能轻易爬到玩家碰撞体上。

受击动画不能每次受击都强制播放，否则玩家连续攻击时 Boss 动画会疯狂重启，看起来像抽搐。现在受击拆成两层：每次受击都闪色，保证打击反馈及时；受击动画有独立冷却，动画结束后 3 秒内只闪色，不重复打断 Boss 行为。

### 5. Unity 测试方式
1. 打开主场景进入 Play Mode。
2. 用开发者模式快速打破 5 次宝箱，进入粉色传送门到 Boss 房间。
3. 观察地板是否为浅绿色。
4. 让 Spider King 主动追近玩家，确认它不会继续挤到玩家中心或站到玩家头顶。
5. 玩家攻击 Boss，确认第一次命中会触发 `Take Damage` 受击动画。
6. 在受击动画结束后的 3 秒内继续攻击，确认 Boss 只闪色，不重复播放受击动画。
7. 等 3 秒后再次攻击，确认 Boss 可以重新播放受击动画。

### 6. 面试表达
这个 Bug 我定位到不是玩家攻击判定本身，而是 Boss AI 追击时缺少近身停止距离，Boss 会持续朝玩家中心点移动，再加上 `CharacterController.stepOffset` 可能把玩家碰撞体当成台阶，所以出现 Boss 挤到玩家头顶、玩家打不到的情况。我在行为树里加了一个更高优先级的“过近分离”节点，距离太近时先后退，正常追击时也会在安全距离外停止。同时 Boss 的受击反馈分成闪色和动画两层：闪色每次命中都触发，受击动画有动画时长加 3 秒冷却，避免连续命中导致动画反复重播。

### 7. 面试追问
1. **为什么不用一直追到玩家位置？** 因为 Boss 和玩家都有碰撞体，追到同一个中心点会产生挤压和重叠，近战 AI 应该有最小停距。
2. **为什么要降低 stepOffset？** `CharacterController.stepOffset` 会允许角色跨上较低障碍，如果过大，Boss 可能把玩家碰撞体当成台阶往上爬。
3. **为什么受击闪色和受击动画要分开？** 闪色适合高频反馈，动画适合低频硬直；分开后既能保证手感，也不会让动画被连续攻击刷爆。
4. **行为树里为什么把过近分离放在攻击前？** 因为如果已经贴得太近，优先恢复空间比继续攻击更重要，否则会继续卡位。
5. **后续怎么扩展？** 可以把停距、后退距离、受击硬直、受击动画冷却做成 Boss 配置数据，不同 Boss 用不同参数。

### 8. 本次涉及知识点
- Unity `CharacterController` 的 `stepOffset`、`radius`、`height` 对碰撞表现的影响
- 行为树节点优先级
- 近战 AI 的攻击距离、追击停止距离、过近分离距离
- 受击反馈拆分：视觉闪色与动画硬直
- 动画冷却与动作锁定

## 功能名称：Spider King Boss 可视化配置与地板颜色回退

### 1. 实现目标
把 Spider King 从“主要依赖运行时动态补组件”的方式，调整为 Prefab/场景中可以直接看到 `SpiderKingBossController` 和 `CharacterController`。这样打开 BossRoomScene 后，可以直接在 Inspector 里调整 Boss 的碰撞体大小。Boss 房间地板颜色也从浅绿色回退为原来的深灰色。

### 2. 涉及脚本
- `Assets/AllResources/Monsters Ultimate Pack 01 Cute Series/Spider King Cute Series/Prefabs/Spider King.prefab`：给 Spider King Prefab 根节点补上 Boss 控制器和 `CharacterController`。
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：移除 `Awake` 中自动覆盖碰撞体参数的逻辑，新增“应用推荐 Boss 碰撞体参数”的手动入口。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：优先使用场景里已有的 Boss，只在动态新建 Boss 或缺少 Boss 控制器时应用推荐碰撞体参数。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：一键生成 BossRoomScene 时直接生成带可调碰撞体的 Spider King，并把地板颜色改回深灰。

### 3. 调用流程
打开 BossRoomScene 调参：`BossRoomScene -> BossRoomRoot -> Spider King -> Inspector -> CharacterController`

运行时使用 Boss：`BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureSpiderKing -> 场景已有 SpiderKingBossController 优先使用 -> 不覆盖手调碰撞体`

动态兜底生成 Boss：`EnsureSpiderKing -> 实例化 Spider King Prefab -> EnsureBossController -> ApplyRecommendedCharacterControllerDefaults`

### 4. 核心原理
运行时动态生成适合快速兜底，但不方便美术或策划调参。Boss 这种需要反复调碰撞体、攻击范围、动画表现的对象，更适合做成场景中可见、可选中、可配置的对象。代码只负责缺失时兜底，不应该每次运行都覆盖 Inspector 中已经调好的参数。

这次把推荐碰撞体参数从 `Awake` 里移出来，改成一个可手动调用的方法。这样默认生成时仍然有合理参数，但你手动调完后，Play Mode 不会自动改回去。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/BossRoomScene.unity`。
2. 在 Hierarchy 中展开 `BossRoomRoot`，选中 `Spider King`。
3. 在 Inspector 中确认能看到 `SpiderKingBossController` 和 `CharacterController`。
4. 调整 `CharacterController` 的 `Radius / Height / Center / Step Offset`。
5. 进入 Play Mode，确认参数不会被 `Awake` 自动覆盖。
6. 检查 Boss 房间地板颜色是否恢复为深灰。

### 6. 面试表达
之前 Boss 主要依赖运行时 Bootstrap 补组件，这样虽然能跑，但调参体验不好。我把 Spider King 的 Boss 控制器和 CharacterController 放回 Prefab/场景可见层，让它成为一个可以在 Inspector 里配置的 Boss 实体。代码层面保留 Bootstrap 兜底，但只在动态新建或缺组件时应用默认碰撞体，避免覆盖手动调参。这样既保证运行稳定，也符合 Unity 项目里“Prefab 配置 + 运行时兜底”的工作方式。

### 7. 面试追问
1. **为什么不完全依赖运行时生成？** 运行时生成适合兜底，但 Boss 需要频繁调碰撞体和动画参数，放在场景或 Prefab 上更直观。
2. **为什么不能在 Awake 每次设置 CharacterController？** 因为这样会覆盖 Inspector 里手动调好的参数，导致调参不生效。
3. **Bootstrap 还要保留吗？** 要保留。它负责防止场景漏配，保证 Boss 房间最低限度能运行。
4. **Prefab 和场景实例调参有什么区别？** 调 Prefab 会影响所有实例；调场景实例只影响当前场景里的这个 Boss。
5. **正式项目里这些参数会怎么管理？** 可以进一步放进 ScriptableObject 或配置表，由不同 Boss 读取不同数据。

### 8. 本次涉及知识点
- Prefab 组件配置
- 场景实例 Override
- `CharacterController` 碰撞体调参
- 运行时兜底与 Inspector 调参的边界
- Boss 配置可视化

## 功能名称：Boss 血条同步扣血与 Boss 战 BGM 配置入口

### 1. 实现目标
修复 Spider King 被玩家攻击后，Boss 血条显示没有稳定同步扣血的问题。Boss 战 UI 现在会在 Boss 血量变化时同步刷新血量文字和血条长度，同时 Boss 房间新增了可手动拖拽 AudioClip 的背景音乐入口，方便后续配置 Boss 战专属音乐。

### 2. 涉及脚本
- `Assets/Script/Boss/UI/BossBattleHudUi.cs`：负责 Boss 血条、阶段文字、胜利面板的显示刷新。
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：负责 Boss 扣血、受击反馈、死亡事件通知。
- `Assets/Script/Boss/Flow/BossRoomSceneBootstrap.cs`：负责 Boss 房间运行时补齐对象，并创建/配置 Boss 战 BGM 音源。
- `Assets/Editor/BossRoomSceneSetupTool.cs`：一键生成 BossRoomScene 时预留 `BossBattleBgm` 音源对象。

### 3. 调用流程
Boss 扣血 UI 流程：`玩家攻击 -> FighterInterface.Hit -> SpiderKingBossController.Hit -> BossStatsChanged -> BossBattleHudUi.RefreshBoss -> 更新血量文字和血条长度`

Boss 血条兜底刷新：`BossBattleHudUi.Update -> RefreshBossIfStatsChanged -> 检查 Boss 当前血量是否变化 -> RefreshBoss`

Boss 战 BGM 流程：`BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureBossBattleBgm -> 创建/复用 BossBattleBgm -> 应用 AudioClip / 音量 / 循环配置 -> 播放音乐`

### 4. 核心原理
Boss 血条属于表现层，真正的数据来源是 `SpiderKingBossController` 里的 `CurrentHp / MaxHp / HpPercent`。Boss 被打时先修改血量，再通过事件通知 UI 刷新。为了避免 UI 绑定时机错过事件，这次额外加了一个轻量兜底：只在检测到 Boss 血量、最大血量或死亡状态变化时才刷新 UI。

血条显示没有继续依赖 `Image.Type.Filled`，而是改成直接缩放填充条的 `RectTransform.localScale.x`。这样即使运行时创建的是普通纯色 Image，也能稳定表现“血条长度变短”。

BGM 没有塞进 Boss AI，而是放在 Boss 房间启动器里统一补齐。这样 Boss 的战斗逻辑、UI 表现、音乐播放互相分离，后续替换音乐或扩展音量控制不会影响 Boss 行为树。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`，进入 Play Mode。
2. 打破 5 次宝箱进入 Boss 房间。
3. 攻击 Spider King，观察 Boss 顶部血量数字和红色血条是否同步减少。
4. 连续攻击直到 Boss 死亡，确认血条归零、胜利面板和返回传送门正常出现。
5. 如果要配置 BGM，打开 `Assets/Scenes/BossRoomScene.unity`，选中 `BossRoomSceneBootstrap`。
6. 在 Inspector 的 `Boss 战背景音乐` 分组里，把你的音乐文件拖到 `Boss Battle Music` 字段。
7. 确认 `Play Boss Battle Music On Start` 勾选，运行进入 Boss 房间后应自动播放音乐。

### 6. 面试表达
Boss 血条我做成了事件驱动刷新，Boss 扣血后会触发 `BossStatsChanged`，UI 收到事件后读取 Boss 当前血量和最大血量，刷新文字和血条比例。为了提高稳定性，我还加了一个轻量兜底检测，只在血量状态变化时才补刷新，避免因为绑定时机问题导致 UI 不动。血条表现上我没有依赖运行时 Image 的 Filled 模式，而是直接缩放填充条 RectTransform，这样对动态创建的纯色 UI 更稳定。Boss 战音乐则放在 Boss 房间 Bootstrap 里作为场景表现配置，不和 Boss AI 混在一起，方便后续替换音乐或扩展音频管理。

### 7. 面试追问
1. **为什么 UI 不直接自己扣血？** UI 不应该保存战斗数据，它只显示 Boss 控制器里的真实血量，避免数据源不一致。
2. **为什么要事件刷新加兜底刷新？** 事件刷新性能更好，兜底刷新解决初始化顺序或绑定时机导致的漏刷问题。
3. **为什么不用每帧直接刷新血条？** 每帧无脑刷新会让 UI 和数据耦合更粗糙；现在只在检测到血量变化时刷新，成本更低也更清晰。
4. **为什么用 RectTransform 缩放血条？** 运行时代码创建的纯色 Image 不一定适合 Filled 模式，缩放宽度更直观稳定。
5. **为什么 BGM 不写进 BossController？** BossController 应该负责战斗逻辑，BGM 属于场景表现，分开后更容易维护和扩展。

### 8. 本次涉及知识点
- UI 事件驱动刷新
- UI 数据源与表现层分离
- RectTransform 控制血条填充
- Unity `AudioSource` 播放 2D 背景音乐
- 场景 Bootstrap 兜底生成对象
- Inspector 可配置字段设计

## 功能名称：Boss 死亡结算暂停与传送门返回流程

### 1. 实现目标
调整 Spider King 的死亡结算流程：Boss 血量归零后先播放死亡动画，动画结束后隐藏 Boss，再触发胜利弹窗和返回传送门。胜利弹窗出现时暂停游戏并呼出鼠标；点击按钮只关闭弹窗并恢复游戏，不再直接切回主场景，玩家需要主动走进粉色传送门返回。

### 2. 涉及脚本
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：负责 Boss 死亡动画等待、死亡后隐藏、延迟触发胜利事件。
- `Assets/Script/Boss/UI/BossBattleHudUi.cs`：负责胜利弹窗显示、暂停游戏、呼出鼠标、关闭弹窗后恢复游戏。
- `Assets/Script/Boss/Flow/BossVictoryPortalSpawner.cs`：负责监听 Boss 真正死亡完成事件，并生成返回传送门。

### 3. 调用流程
Boss 死亡流程：`玩家攻击 -> SpiderKingBossController.Hit -> currentHp 归零 -> Die -> 播放 Die 动画 -> WaitForSecondsRealtime(deathAnimationDuration) -> 隐藏 Boss -> BossDied`

胜利弹窗流程：`BossDied -> BossBattleHudUi.RefreshBoss -> SetVictoryVisible(true) -> Time.timeScale = 0 -> 显示鼠标 -> 点击关闭弹窗 -> 恢复 Time.timeScale 和鼠标状态`

返回主场景流程：`BossDied -> BossVictoryPortalSpawner -> WaitForSecondsRealtime(spawnDelay) -> 生成 ReturnToMainScenePortal -> 玩家接触传送门 -> BossScenePortal -> SceneFlowService.RestartGameplay`

### 4. 核心原理
Boss 死亡不能只看“血量是不是 0”，还要区分“刚死亡”和“死亡表现完成”。如果血量刚归零就立刻弹窗，会打断玩家观察死亡动画，也会显得结算流程很突兀。因此这次在 Boss 控制器里增加了 `IsDeathSequenceFinished`，只有死亡动画等待结束、Boss 隐藏后，才触发 `BossDied` 事件。

胜利弹窗属于 UI 状态，打开时要暂停游戏并切到鼠标可交互模式。关闭弹窗时只恢复游戏，不切场景。这样“结算确认”和“返回主场景”被拆成两个行为：弹窗负责展示胜利，传送门负责场景流转。

传送门生成延迟使用 `WaitForSecondsRealtime`，因为胜利弹窗会把 `Time.timeScale` 设为 0。如果继续用普通 `WaitForSeconds`，传送门协程会被暂停卡住。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`。
2. 进入 Play Mode，通过宝箱传送门进入 Boss 房间。
3. 攻击 Spider King 到血量归零。
4. 观察 Boss 是否先播放死亡动画。
5. 等待死亡动画结束，确认 Boss 模型消失。
6. 确认胜利弹窗弹出，游戏暂停，鼠标显示。
7. 点击“关闭弹窗”，确认弹窗消失，玩家恢复操作。
8. 走进粉色返回传送门，确认这时才返回主场景。

### 6. 面试表达
Boss 死亡流程我没有简单地在血量归零时马上弹结算，而是拆成了两个阶段：第一阶段是 `IsDead`，表示 Boss 已经不能行动并开始播放死亡动画；第二阶段是 `IsDeathSequenceFinished`，表示死亡动画播完，Boss 可以隐藏并进入胜利结算。UI 和传送门都监听真正完成后的 `BossDied` 事件，这样不会打断死亡表现。胜利弹窗出现时我会缓存 `Time.timeScale` 和鼠标状态，然后暂停游戏并显示鼠标；按钮只关闭弹窗，返回主场景交给传送门处理。这个拆分能让战斗表现、UI 结算和场景切换职责更清晰。

### 7. 面试追问
1. **为什么不在血量为 0 时马上 Destroy Boss？** 这样会看不到死亡动画，也可能让依赖 Boss 位置生成传送门的逻辑拿不到参考对象。
2. **为什么隐藏 Boss 而不是 Destroy？** 隐藏模型和碰撞体既能让画面上 Boss 消失，又保留 transform 给传送门生成逻辑使用。
3. **为什么传送门延迟用 WaitForSecondsRealtime？** 胜利弹窗会暂停 `Time.timeScale`，普通 `WaitForSeconds` 会被暂停影响，真实时间等待更可靠。
4. **为什么按钮不直接返回主场景？** 因为弹窗只负责结算展示，返回主场景交给传送门，玩家行为更明确，也能展示完整 Boss 房间闭环。
5. **如果以后要做多个 Boss 怎么扩展？** 可以把死亡动画时长、消失方式、胜利 UI 文案和传送门配置抽到 ScriptableObject，让不同 Boss 使用不同配置。

### 8. 本次涉及知识点
- Boss 死亡状态拆分
- 死亡动画与结算事件解耦
- `Time.timeScale` 暂停游戏
- 鼠标锁定与显示状态恢复
- `WaitForSecondsRealtime` 与 `WaitForSeconds` 区别
- UI 弹窗职责与场景传送职责分离

## 功能名称：Spider King 远程攻击紫色小圆球表现

### 1. 实现目标
给 Spider King 的远程法术攻击增加可视化飞行子弹。Boss 释放远程攻击时，会从身前生成一个紫色发光小圆球，小圆球飞向玩家位置，到达后再生成原有的范围法术效果并结算伤害。

### 2. 涉及脚本
- `Assets/Script/Boss/AI/SpiderKingBossController.cs`：新增远程子弹表现参数，生成紫色小圆球，并把远程攻击流程改成“飞行表现 -> 爆炸结算”。

### 3. 调用流程
Boss 远程攻击流程：`行为树 CanSpellAttack -> DoSpellAttack -> BeginAction -> ScheduleDamageAfterDelay -> DealDamageAfterDelay -> LaunchSpellProjectileToImpact -> CreateSpellProjectileVisual -> 小圆球飞行 -> SpawnSpellImpact -> TryDamagePlayerAtPoint`

### 4. 核心原理
这次没有复用普通怪物的 `BulletCo`，因为 Boss 远程法术本质上是一个“飞向目标点后范围爆炸”的表现，而不是普通碰撞子弹。紫色小圆球只负责表现，真正的伤害仍然由 `SpiderKingBossController` 统一结算，避免 Boss AI 和普通怪物子弹逻辑互相影响。

小圆球用 `GameObject.CreatePrimitive(PrimitiveType.Sphere)` 动态生成，设置紫色材质和点光源，然后用 `Vector3.MoveTowards` 每帧移动到目标点。到达后销毁小球，再调用原有的范围法术落点和伤害检测。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`。
2. 进入 Play Mode，通过宝箱传送门进入 Boss 房间。
3. 和 Spider King 保持中距离，等待它释放远程攻击。
4. 观察 Boss 身前是否生成紫色发光小圆球。
5. 确认小圆球会飞向玩家所在位置。
6. 小圆球到达后应出现原来的范围法术球，并对范围内玩家造成伤害。
7. 如果小球太小、太快或太慢，可以选中 `Spider King`，在 `远程子弹表现` 分组里调整半径、速度、发射高度等参数。

### 6. 面试表达
Boss 远程攻击我分成了表现和伤害两部分。行为树决定释放远程攻击后，Boss 先播放远程动画，等到出手时间点生成一个紫色发光小圆球，小圆球飞到目标点后再触发范围爆炸和伤害检测。小圆球本身不负责伤害，它只是视觉表现；伤害仍然由 Boss 控制器统一处理。这样做的好处是表现和规则分离，之后要替换成正式特效、轨迹线或者对象池，都不需要改 Boss 的伤害判定规则。

### 7. 面试追问
1. **为什么不直接用 BulletCo？** Boss 法术是飞到目标点后范围爆炸，不是碰撞立即扣血，语义和普通子弹不同，所以先单独做表现更清晰。
2. **为什么小圆球不挂 Collider？** 它只是表现对象，伤害由目标点范围检测结算，避免飞行过程中误触发碰撞。
3. **为什么用 MoveTowards？** 逻辑简单稳定，适合当前阶段；后续可以替换为曲线、DOTween 或对象池特效。
4. **怎么避免小球无限存在？** 增加了最大飞行时间 `spellProjectileMaxTravelTime`，目标异常时也会结束。
5. **后续怎么优化？** 可以把小圆球做成 Prefab，用对象池复用，并增加拖尾、粒子、音效和命中特效。

### 8. 本次涉及知识点
- Boss 远程攻击表现与伤害解耦
- Unity 运行时创建 Sphere
- 材质发光与点光源
- `Vector3.MoveTowards` 飞行表现
- 协程串联动画出手、投射物飞行和范围伤害
- Inspector 参数化调试

## 功能名称：失败测试与脚本编码修复

### 1. 实现目标
修复 MainScene 缺少新手引导装配导致的 EditMode 测试失败，并把 18 个使用 GBK/ANSI 编码的技能、小地图和 UI 脚本统一转换为 UTF-8，确保 Unity、Git 和代码编辑器都能稳定显示中文。

### 2. 涉及脚本
- `Assets/Editor/GameplayUiRootMigration.cs`：增加只更新新手引导 Prefab 的窄范围装配入口，避免覆盖主场景和其他玩法 UI。
- `Assets/Script/UI/GameplayStartupGuidePopup.cs`：继续负责新手引导显示、暂停游戏、关闭按钮和鼠标状态恢复，本次不修改运行时逻辑。
- 技能架构、技能表现、小地图和技能 UI 共 18 个脚本：仅把文件编码从 GBK/ANSI 转换为 UTF-8，保留原有代码与中文语义。

### 3. 调用流程
编辑器装配流程：`GameplayUiRootMigration.UpgradePrefabsFromCommandLine -> UpgradeStartupGuidePrefab -> UpgradeStartupGuideInGameplayPrefab -> WireStartupGuide -> SaveAsPrefabAsset`

运行时流程：`MainScene -> GameplayUiRoot Prefab -> GameplayStartupGuidePopup.OnEnable -> ShowPopup -> 点击关闭按钮 -> ClosePopup -> 恢复 Time.timeScale 和鼠标状态`

### 4. 核心原理
失败测试不是断言写错，而是资源装配不完整：新手引导视图 Prefab 已经存在，控制脚本也已经存在，但它们没有被放进 MainScene 使用的 GameplayUiRoot。修复时把独立的新手引导 Prefab 嵌套进玩法 UI 根，并把 Canvas、遮罩、面板、文本和按钮通过序列化引用连接起来。运行时不需要 `Find` 或临时创建 UI，能更早发现 Prefab 配置错误。

乱码问题来自文件编码不统一。文件内容原本是有效的 GBK 中文，但项目工具按 UTF-8 解码时会显示替换字符。把原始字节按 GBK 正确读取，再以 UTF-8 无 BOM 保存，就能无损恢复中文，而不需要猜测或重写代码内容。

### 5. Unity 测试方式
1. 打开 `Assets/Scenes/MainScene.unity`。
2. 进入 Play Mode，确认开局显示新手引导弹窗。
3. 确认弹窗出现时游戏暂停、鼠标可见。
4. 点击关闭按钮，确认弹窗隐藏并恢复游戏与鼠标状态。
5. 打开 Test Runner，运行 EditMode 测试，确认 `MainScene_ContainsExactlyOneGameplayUiRoot` 通过。
6. 检查技能栏、技能三选一面板和小地图组件的中文文案与 Tooltip 是否正常。

### 6. 面试表达
这次测试失败的原因是代码和资源之间的装配断开了：新手引导脚本和界面 Prefab 都存在，但主场景实际引用的 GameplayUiRoot 没有包含它。我没有删除测试或增加运行时兜底，而是通过编辑器装配工具把引导 Prefab 嵌套到 UI 根，并显式写入所有序列化引用。这样测试验证的就是最终场景资源，运行时也不需要动态查找。项目里的 18 个乱码脚本则是 GBK 和 UTF-8 混用导致的，我按原编码读取后统一转换为 UTF-8，保留了原始中文和业务逻辑。

### 7. 面试追问
1. **为什么不直接删除失败断言？** 断言发现了真实的场景装配缺失，删除只会隐藏问题，新手引导在正式运行时仍不会出现。
2. **为什么不用运行时 Instantiate 弹窗？** 当前项目采用 Prefab 静态装配，显式引用更容易检查，也能避免资源路径、加载时机和重复实例问题。
3. **为什么迁移工具只更新新手引导？** 旧迁移逻辑依赖历史 UI 层级，窄范围更新能避免覆盖已经调整好的其他界面和场景内容。
4. **为什么乱码不能直接全局替换？** 乱码可能影响注释、日志和运行时文案，直接猜测容易改变含义；按正确源编码解码才能无损恢复。
5. **怎样防止编码问题再次出现？** 团队统一使用 UTF-8，并在提交前通过严格 UTF-8 扫描和测试检查非法字节或替换字符。

### 8. 本次涉及知识点
- Unity Prefab 嵌套与序列化引用
- EditMode 场景结构测试
- 编辑器迁移工具与幂等装配
- `ExecuteAlways`、`OnEnable` 与 `OnValidate`
- `Time.timeScale` 和鼠标状态恢复
- GBK、UTF-8 与 Unicode 替换字符
- 通过失败测试定位资源装配问题

## 功能名称：Addressables 第一阶段——技能特效本地异步加载

### 1. 实现目标
把 5 个技能特效 Prefab 从 `Resources` 迁移到 Addressables 本地分组，技能释放组件在玩家进入场景时异步预加载 4 个入口特效。技能伤害与资源加载解耦：特效尚未完成或加载失败时，技能逻辑继续执行，并使用原有线稿表现兜底。

### 2. 涉及脚本
- `Assets/Script/Services/AddressableAssetService.cs`：统一发起 Prefab 异步加载、检查加载结果和释放句柄。
- `Assets/Script/Skills/SkillVfxAddresses.cs`：集中保存分组名、标签、资源地址和对象池 Key。
- `Assets/Script/Skills/PlayerSkillCastComponent.cs`：在 `Start` 中预加载技能特效，在销毁时清理池对象并释放句柄。
- `Assets/Script/Skills/SkillVisualPool.cs`：追踪 Prefab 特效的活动与闲置实例，支持卸载资源前按类型完整清理。
- `Assets/Editor/SkillVfxAddressablesMigration.cs`：保留 GUID 地移动资源，并自动创建本地 LZ4 分组、地址和标签。
- `Assets/Editor/Tests/SkillVfxAddressablesTests.cs`：校验资源位置、地址、标签和分组 Schema，防止配置回退。

### 3. 调用流程
`PlayerRuntimeController -> PlayerSkillCastComponent.Start -> AddressableAssetService.LoadPrefabAsync -> Addressables.LoadAssetAsync -> PlayerSkillCastComponent 保存 Prefab -> SkillVisualPool.GetPrefabVfx -> 播放并回收特效`

释放流程：`PlayerSkillCastComponent.OnDestroy -> SkillVisualPool.ClearPrefabVfxPool -> Destroy 特效实例 -> AddressableAssetService 下一帧 Addressables.Release`

### 4. 核心原理
Addressables 可以理解成“用稳定地址查资源的资源管理层”。资源不再因为放在 `Resources` 目录而被统一打进包并通过同步 API 读取，而是由分组决定如何构建，再通过地址异步加载。

加载得到的不只是 Prefab，还会得到一个 `AsyncOperationHandle`。句柄代表本次资源引用，Addressables 会据此维护引用计数。加载和释放必须成对出现，否则会造成内存长期占用；同时也不能在实例仍存活时先释放资源。因此退出场景时先清理对象池实例，等 Unity 在帧末真正销毁对象后，下一帧才释放句柄。

技能逻辑没有等待特效加载完成。玩家按键后，技能系统仍按原流程校验蓝量、冷却和伤害；表现层有 Prefab 就播放真实特效，没有就走兜底效果。这样资源系统异常不会破坏核心战斗逻辑。

### 5. Unity 测试方式
1. 打开 `Window > Asset Management > Addressables > Groups`，确认存在 `Local_SkillVFX`。
2. 确认分组内有 5 个 Prefab，地址以 `skill-vfx/` 开头，并带有 `skill-vfx` 标签。
3. 确认分组使用 Local Build/Load Path、LZ4、Pack Together 和 Include In Build。
4. 打开 `Assets/Scenes/MainScene.unity` 进入 Play Mode，依次释放火球、毒雾和镰刀旋转技能。
5. 正常结果是技能伤害、冷却和特效均正常；重复释放时对象池会复用特效实例。
6. 打开 Test Runner 运行 EditMode 测试，确认 Addressables 配置测试和原有测试全部通过。

### 6. 面试表达
我在项目里分阶段接入了 Addressables，第一阶段先选择频繁使用、又适合和对象池结合的技能特效，避免一次迁移全部角色、配置和场景带来过大风险。我把 5 个特效移出 Resources，配置成本地 LZ4 分组，玩家创建时并行异步预加载 4 个入口 Prefab，释放技能时仍通过原有对象池复用实例。生命周期上由玩家组件持有加载句柄，销毁时先清理池中的活动和闲置实例，再延后一帧释放句柄，保证引用计数对称。即使资源还没加载完，战斗伤害也不会被阻塞，表现层会使用兜底效果。

### 7. 面试追问
1. **为什么第一阶段只迁移技能特效？** 特效加载入口集中、没有核心数据依赖，能用较小改动验证分组、异步加载、对象池和释放生命周期。
2. **为什么不用同步等待加载完成？** 同步等待可能造成主线程卡顿；提前异步预加载可以把读取成本放到进入场景后的空闲时间。
3. **为什么 Addressables 还要配对象池？** Addressables 解决资源定位和加载，对象池解决实例频繁创建销毁；两者负责不同层次的性能问题。
4. **为什么要保留加载句柄？** Addressables 通过句柄进行引用计数，丢失句柄就难以在正确时机对称释放资源。
5. **为什么先销毁池对象再释放句柄？** 池里的活动对象和闲置对象都依赖 Prefab 及其材质、贴图；先卸载可能造成引用失效或显示异常。

### 8. 本次涉及知识点
- Addressables Group、Address、Label 和 Profile Path
- `AsyncOperationHandle<T>`、异步加载状态与引用计数
- LZ4 与 Pack Together 的本地资源分组策略
- `Resources` 迁移与重复打包风险
- Prefab GUID 保留和 `AssetDatabase.MoveAsset`
- 对象池实例追踪与状态清理
- `Start`、`OnDestroy`、协程和帧末 `Destroy` 生命周期
- 核心逻辑与表现层降级解耦

## 功能名称：Boss 周回进度保持与 Spider King 成长

### 1. 实现目标
本次把 Boss 战从一次性挑战改成可循环挑战：主场景每累计击破 5 次宝箱开启一次 Boss 入口，击败 Boss 后返回主场景继续保留宝箱累计进度和玩家状态。每一轮进入 Boss 房间时仍然生成 Spider King，但根据当前 Boss 轮次提升血量、伤害、移动速度并降低攻击冷却。

### 2. 涉及脚本
- `BossRunProgressState`：跨场景保存宝箱累计击破次数、Boss 已完成轮次、当前 Boss 轮次和返回主场景位置。
- `BossPortalUnlockController`：监听宝箱击破事件，改为按全局累计次数判断每 5 次开启一次 Boss 入口。
- `BoxCo`：新增 `RestoreProgress`，让主场景重载后能恢复宝箱等级和分数。
- `BossScenePortal`：进入 Boss 房间前记录玩家返回位置和当前 Boss 轮次。
- `BossRoomSceneBootstrap`：生成 Spider King 后按 Boss 轮次套成长倍率。
- `SpiderKingBossController`：新增 Boss 周回数值成长接口，并让死亡收尾优先使用真实死亡动画长度。
- `BossVictoryPortalSpawner`：Boss 死亡流程完成后记录本轮 Boss 已击败，并立即生成返回传送门。
- `GameplayCharacterSpawner`：返回主场景时使用进入 Boss 门前的位置生成玩家。
- `SceneFlowService`：开始新局和退出登录时清空 Boss 周回进度，避免状态残留。

### 3. 调用流程
主场景宝箱被击破：
`BoxCo.HandleDestroyed -> BoxCo.OnVaultDestroyed -> BossPortalUnlockController.HandleVaultDestroyed -> BossRunProgressState.RecordVaultDestroyed -> 达到 5 的倍数后生成 BossScenePortal`

进入 Boss 房间：
`BossScenePortal.TryEnterPortal -> PlayerSceneTransferState.TryCaptureFrom -> BossRunProgressState.BeginBossChallenge -> SceneFlowService.LoadBossRoomScene`

Boss 房间初始化：
`BossRoomSceneBootstrap.BuildRoomIfNeeded -> EnsureSpiderKing -> SpiderKingBossController.ApplyBossRoundScaling`

击败 Boss 并返回：
`SpiderKingBossController.FinishDeathSequenceAfterAnimation -> BossDied -> BossVictoryPortalSpawner.HandleBossDied -> BossRunProgressState.MarkBossDefeated -> 返回传送门 -> MainScene -> GameplayCharacterSpawner -> BoxCo.RestoreProgress`

### 4. 核心原理
Unity 切换场景时，场景里的 GameObject 会销毁并重新生成，所以不能直接保存宝箱对象或传送门对象。正确做法是保存“能恢复对象的数据”，例如累计击破次数、Boss 轮次、玩家返回位置。主场景重新加载后，新宝箱先正常 Awake，再由 Boss 进度状态把它恢复到之前的层级。

Boss 成长没有复制多个 Spider King，而是复用同一个 Boss 控制器和同一棵行为树，只在进入 Boss 房间时按轮次修改数值。这样能体现“同一套逻辑，多轮难度成长”的设计思路。

### 5. Unity 测试方式
1. 打开 `MainScene`。
2. 用开发者模式或正常攻击击破 5 次宝箱。
3. 进入粉色 Boss 传送门并击败 Spider King。
4. 等死亡动画结束，确认 Boss 立刻隐藏，返回门立即出现。
5. 走返回门回主场景，确认角色回到进入 Boss 门前的位置，宝箱累计次数没有回到 0。
6. 再击破 5 次宝箱，确认第 10 次击破时再次出现 Boss 入口。
7. 第二轮进入 Boss 房间，观察 Boss 血量和攻击压力是否比第一轮更高。

### 6. 面试表达
这个 Boss 战我做成了一个周回挑战系统。因为 Unity 切场景会销毁场景对象，所以我没有直接保存宝箱或传送门对象引用，而是设计了一个运行时进度状态，保存累计宝箱击破次数、Boss 完成轮次和玩家返回位置。每次宝箱击破后，入口控制器会根据全局累计次数判断是否达到 5 的倍数，达到后生成 Boss 入口。进入 Boss 房间时，Spider King 仍然使用同一套行为树和控制器，但会按当前 Boss 轮次套血量、伤害、速度和冷却倍率。击败 Boss 后记录本轮完成，返回主场景时恢复玩家和宝箱进度，保证不是重新开一局。

### 7. 面试追问
1. **为什么不让主场景常驻？** 当前项目用普通 `LoadScene` 流程更简单稳定，保存数据再恢复对象更适合这个体量；后续如果做大型无缝地图，可以考虑 Additive Scene。
2. **为什么用静态状态类？** 这是局内运行时状态，不需要落盘存档；静态类实现成本低，适合保存一次 Play 会话中的跨场景数据。
3. **如果要做存档怎么办？** 可以把 `BossRunProgressState` 中的数据序列化到 JSON 或 PlayerPrefs，读档后再恢复。
4. **Boss 为什么不复制多个 Prefab？** 复用同一个 Spider King 和同一套行为树，只改配置倍率，避免重复代码，也方便以后扩展成 ScriptableObject 配置。
5. **为什么宝箱恢复不广播 OnVaultDestroyed？** 恢复进度不是一次新的击破行为，如果广播会重复发奖励或重复开 Boss 门。

### 8. 本次涉及知识点
- Unity `LoadScene` 后对象重建与运行时数据恢复
- 静态运行时状态保存
- 事件驱动：`BoxCo.OnVaultDestroyed`、`BossDied`
- 数据恢复与事件广播的区别
- Boss 周回成长倍率设计
- 玩家跨场景快照恢复
- 动画长度读取与死亡收尾时机

## 功能名称：开发者键位调整与基础完整背包

### 1. 实现目标

把开发者模式的“快速击破一次宝箱”从 `B` 调整为 `N`，释放 `B` 作为所有玩家都能使用的背包快捷键。背包首版提供 24 个固定格子、同类物品优先堆叠、选中物品详情、宝箱随机掉落、满包剩余提示，以及主场景和 Boss 房之间的会话数据继承。打开背包时暂停游戏并释放鼠标，关闭时恢复打开前的时间缩放与鼠标状态；开始新的角色会话或登出时清空背包，死亡重开和 Boss 场景切换则保留。

### 2. 涉及脚本

- `InventoryItemDefinition`：ScriptableObject 静态物品配置，保存 ID、名称、分类、品质、图标、描述和堆叠上限。
- `InventoryDatabase`：集中保存 24 格容量、物品列表和宝箱加权掉落表。
- `InventorySlotData`、`InventoryModel`：保存当前角色会话中的格子和数量，只对外提供只读访问。
- `InventorySystem`：统一处理优先叠加、跨栈、占用空格、满包剩余数量和清空。
- `AddInventoryItemCommand`、`ResetInventoryCommand`：作为外部修改背包数据的统一入口。
- `InventoryChangedEvent`、`InventoryItemAddedEvent`、`InventoryFullEvent`：把数据变化、获得物品和满包结果通知给表现层。
- `VaultLootRewardController`：监听自身宝箱的正式击破事件，抽取掉落并发送加物品命令。
- `InventoryPanel`、`InventorySlotView`：处理背包开关、静态格子刷新、选择详情、品质颜色和获得物品提示。
- `InputCo`、`IGameplayInput`：分别采样 `B` 背包输入与 `N` 开发者击破输入。
- `GameSessionUi`、`MiniMapPanelController`：协调 ESC、暂停菜单、属性面板和大地图的输入焦点。
- `SceneFlowService`：开始新角色会话或登出时发送背包重置命令。
- `InventoryFeatureSetupTool`：幂等创建物品资源、数据库、24 格 UI，并装配两个 Prefab。
- `InventorySystemTests`、`InventoryPrefabStructureTests`：验证规则边界与 Prefab/配置结构。

### 3. 调用流程

打开背包：`InputCo(B) -> InventoryPanel.Open -> 检查其他模态 UI -> 收起大地图 -> 缓存 timeScale/鼠标 -> 暂停游戏 -> InventoryModel -> 刷新 24 格与详情`

宝箱奖励：`普通攻击或开发者 N -> BoxCo.HandleDestroyed -> BoxCo.OnVaultDestroyed -> VaultLootRewardController -> InventoryDatabase 加权抽取 -> AddInventoryItemCommand -> InventorySystem -> InventoryModel -> InventoryChangedEvent/InventoryItemAddedEvent -> InventoryPanel`

会话清理：`重新选择角色或登出 -> SceneFlowService -> ResetInventoryCommand -> InventorySystem.ResetInventory -> InventoryChangedEvent`

### 4. 核心原理

可以把背包理解成三层。物品配置负责回答“这个物品是什么”，例如名称、品质和最多能堆多少；运行时 Model 负责回答“玩家现在有什么、每格有多少”；UI 只负责把只读数据画出来。三层分离后，修改 UI 样式不会改变堆叠规则，新增物品主要通过创建配置资源完成，也不会把宝箱、背包数据和界面写成互相强引用的一大块代码。

所有加物品操作都经过 `InventorySystem`。系统先扫描同类未满堆叠，再扫描空格，因此相同物品不会随意占用多个半空格子；当空间不足时返回 `InventoryAddResult`，调用方同时能知道成功加入和未加入的数量。UI 不在 `Update` 中每帧读取整个背包，而是在系统发送变化事件后刷新，减少无意义工作并保持数据入口唯一。

背包属于模态 UI。打开时先保存原来的 `Time.timeScale`、鼠标锁定和显示状态，再把时间设为 0；关闭、禁用或切场景销毁时恢复缓存。物品提示使用 `WaitForSecondsRealtime`，所以即使背包暂停了游戏，提示仍能按真实时间消失。

跨场景不保存 UI，也不把格子放在宝箱对象上，而是让 QFramework Architecture 中的 `InventoryModel` 保存当前会话数据。主场景和 Boss 房重新生成各自的 UI 后会读取同一个 Model；只有开始新角色会话或登出才显式重置。

### 5. Unity 测试方式

1. 打开 `Assets/Scenes/MainScene.unity`，进入 Play Mode。
2. 不开启开发者模式时按 `N`，确认宝箱不会被快速击破；按 `B`，确认背包正常打开。
3. 打开背包后确认游戏冻结、鼠标可见、界面有 6×4 共 24 格；再次按 `B`、按 `Esc` 或点击关闭按钮都应恢复游戏与鼠标。
4. 按 `F1` 开启开发者模式，确认提示显示 `N：击破一次宝箱`；按 `N` 击破宝箱并观察获得物品提示。
5. 也用普通攻击击破宝箱，确认两种方式都会获得生命药水、经验结晶或古代卷轴。
6. 重复击破，确认同类物品优先堆叠；点击有物品的格子，确认右侧名称、品质、分类、数量和描述同步变化。
7. 先按 `M` 展开大地图，再按 `B`，确认地图先收起再打开背包；背包打开时 `M` 和属性面板不应抢夺焦点。
8. 在开场引导、升级选择或暂停菜单显示时按 `B`，确认不会叠加打开背包。
9. 在主场景获得物品后进入 `BossRoomScene`，再按 `B`，确认物品仍存在；返回主场景后再次确认。
10. 死亡后重新开始，确认当前会话背包保留；退出登录或重新选择角色进入游戏，确认背包清空。
11. 打开 `Window > General > Test Runner`，在 EditMode 中运行 `InventorySystemTests` 与 `InventoryPrefabStructureTests`；本次实现验证结果为 7 项通过、0 项失败。

### 6. 面试表达

我把背包拆成静态配置、运行时数据、规则系统和 UI 四部分。物品名称、品质、图标和堆叠上限放在 ScriptableObject，玩家当前每格的物品和数量放在 InventoryModel。所有加物品操作都通过 Command 进入 InventorySystem，系统先补已有堆叠再占空格，并返回加入数量和满包剩余数量；UI 只监听背包事件刷新，不直接修改格子。宝箱掉落通过独立组件监听正式击破事件，所以正常攻击和开发者快捷键共用一条奖励链。背包打开时会缓存并恢复时间缩放和鼠标状态，跨主场景与 Boss 房则依靠 Architecture 中的会话 Model 保留数据。

### 7. 面试追问

1. **为什么静态物品数据用 ScriptableObject？** 名称、图标、品质等可以在 Inspector 配置并被多个格子复用，新增物品通常不需要改规则代码，也避免每个格子重复保存相同描述。
2. **为什么运行时数量不直接写进 ScriptableObject？** ScriptableObject 是共享资产，把玩家数量写进去会污染编辑器资源，并且多个玩家或存档会互相影响，所以数量必须放在独立运行时 Model。
3. **如何实现优先堆叠？** 加物品时先遍历相同物品且未满的格子，填满后再遍历空格；剩余数量继续跨栈，直到全部加入或没有空间。
4. **UI 为什么不每帧刷新？** 背包数据只在拾取、使用、丢弃等离散行为后变化，用事件通知可以减少重复遍历和文本、图片赋值，也让数据流更清楚。
5. **后续如何扩展装备、使用和存档？** 增加物品行为类型与对应 Command，装备系统通过事件更新角色属性；存档时只序列化稳定的 `itemId + count + slotIndex`，读档后再从数据库恢复 ScriptableObject 引用。

### 8. 本次涉及知识点

- ScriptableObject 数据配置与运行时数据分离
- QFramework Model / System / Command / Event 分层
- 背包堆叠、跨栈、容量和部分成功结果设计
- C# 只读接口、事件注册与注销
- Unity UI GridLayoutGroup、Button、Image、Text 与静态 Prefab 装配
- 模态 UI 的 `Time.timeScale`、鼠标焦点和 ESC 优先级协调
- `WaitForSecondsRealtime` 与非缩放时间
- 静态事件来源过滤和组件职责拆分
- 跨场景会话数据保留与显式重置边界
- NUnit EditMode 规则测试和 Prefab 结构测试

## 功能名称：小怪药水掉落、地面拾取与背包使用

### 1. 实现目标

普通史莱姆进入真实死亡流程时有 10% 概率掉落一瓶药水，掉落成功后生命药水和魔法药水各占 50%。药水会出现在死亡位置并以对象池方式管理，玩家进入触发范围后自动拾取到现有背包，同类物品继续沿用优先堆叠规则。生命药水和魔法药水每格最多堆叠 20 瓶，通过背包右侧“使用”按钮分别恢复最大生命值或最大魔法值的 30%；资源已满时不消耗。没有被拾取的地面药水会在 45 秒正常游戏时间后回收，暂停游戏期间倒计时也会暂停。

### 2. 涉及脚本

- `InventoryItemDefinition`：新增使用效果、恢复百分比和显示色，用同一套配置驱动背包图标与地面药水表现。
- `InventoryDatabase`：保存普通怪总掉落率、生命/魔法药水权重、地面拾取 Prefab，并提供可传入固定随机数的抽取方法。
- `InventorySystem`：新增可加入数量查询和按格子减少物品，数据变化后继续统一发送背包变化事件。
- `UseInventoryItemCommand`、`InventoryUseResult`：负责校验格子、执行恢复、确认实际恢复量并决定是否扣除药水。
- `SlimeCo`：只增加实例级 `Died` 事件，在首次进入正式死亡流程时广播，不承载掉落业务。
- `MonsterLootDropController`：监听所属史莱姆死亡，执行 10% 判定并向地面掉落池申请药水。
- `WorldLootPool`：按拾取 Prefab 分类缓存实例，负责获取、复用和回收，避免反复创建销毁。
- `WorldItemPickup`：负责药水颜色、旋转悬浮、45 秒寿命、玩家触发识别、满包重试和拾取成功回收。
- `InventoryPanel`、`InventorySlotView`：显示药水颜色和“使用”按钮，根据使用结果显示恢复或失败提示。
- `InventoryFeatureSetupTool`：幂等创建魔法药水、地面拾取 Prefab，升级生命药水、数据库、背包 UI 和两个史莱姆 Prefab。

### 3. 调用流程

掉落流程：`SlimeCo.DoDie -> SlimeCo.Died -> MonsterLootDropController -> InventoryDatabase.TryRollMonsterLoot -> WorldLootPool.Get -> WorldItemPickup.Configure`

拾取流程：`玩家进入 Trigger -> WorldItemPickup -> AddInventoryItemCommand -> InventorySystem.TryAddItem -> InventoryModel -> InventoryChangedEvent -> InventoryPanel -> WorldLootPool.Release`

使用流程：`背包使用按钮 -> UseInventoryItemCommand -> PlayerModel 只读属性 -> PlayerCombatSystem.Heal / PlayerResourceSystem.RestoreMana -> InventorySystem.TryRemoveItemAt -> InventoryChangedEvent -> InventoryPanel`

### 4. 核心原理

这个功能解决的是“怪物奖励怎样安全进入背包并真正影响玩家状态”。怪物自身只需要说明“我已经正式死亡”，独立掉落组件再决定是否掉落以及掉什么。这样怪物 AI、随机奖励和背包不会挤在同一个脚本里，后续增加金币、装备或不同怪物掉落表时更容易扩展。

地面药水使用触发器检测玩家，但不依赖尚未统一配置的 Player Tag，而是从碰撞体父节点查找 `PlayerRuntimeController`。拾取前先询问背包还能加入多少；有空间才发送正式加物品命令，背包满时物品继续留在地面，并以低频方式重试，避免 `OnTriggerStay` 每个物理帧重复刷提示。

使用药水采用类似事务的顺序：先检查格子与效果，再计算 `CeilToInt(最大值 × 30%)`，接着调用正式生命或魔法系统；只有实际恢复量大于 0 才扣除一瓶。因此满血、满蓝或无效格子都不会造成数据丢失。UI 只根据返回结果展示提示，不直接修改玩家属性和背包数量。

对象池把“创建实例”和“重复使用”分开。首次需要时才实例化拾取物，之后拾取成功或超时就禁用并归还队列；再次取出时 `Configure` 会重置物品、数量、寿命、玩家触发记录和外观，避免对象池常见的状态残留问题。

### 5. Unity 测试方式

1. 打开 `Assets/Scenes/MainScene.unity` 并进入 Play Mode，完成开场引导。
2. 连续击杀普通史莱姆，确认总体约 10% 的击杀会在死亡点出现红色生命药水或蓝色魔法药水；Boss 与 Boss 门清场销毁不应走这条掉落链。
3. 走入药水触发范围，确认药水消失并进入背包；连续拾取同类药水时应优先堆进同一格，单格上限为 20。
4. 让角色受伤，按 `B` 打开背包，选中生命药水并点击“使用”，确认恢复最大生命值的 30% 且数量减 1。
5. 消耗魔法后使用魔法药水，确认恢复最大魔法值的 30%；满血或满蓝时再次点击，数量应保持不变并出现明确提示。
6. 把背包填满后接近药水，确认药水留在地面且不会持续刷满包提示；腾出空间后，仍在范围内时应自动重试拾取。
7. 观察未拾取药水，确认正常游戏时间 45 秒后回收；按 `B` 暂停期间寿命不减少。
8. 从主场景进入 Boss 房，确认已经进入背包的药水仍然存在并可使用；未拾取地面物不跨场景保留。
9. 在 `Window > General > Test Runner` 的 EditMode 中运行 `InventorySystemTests` 和 `InventoryPrefabStructureTests`，检查掉落边界、恢复量、满资源不扣除、堆叠与 Prefab 引用；当前自动验证结果为 12 项通过、0 项失败。

### 6. 面试表达

我把小怪药水掉落拆成死亡事件、掉落规则、地面表现和背包使用四部分。史莱姆真实死亡时只广播一次实例事件，独立掉落组件通过 ScriptableObject 数据库做 10% 总概率和二次权重抽取，再从对象池获取地面拾取物。玩家进入触发器后，拾取物通过 Command 把物品加入背包，满包就保留在地面并低频重试。药水使用也通过 Command 处理，先调用正式生命或魔法系统，确认实际恢复后才扣除一瓶，所以满血、满蓝不会误消耗。这个结构让 AI、奖励、背包数据和 UI 解耦，后续扩展装备掉落、不同怪物掉落表或存档会比较自然。

### 7. 面试追问

1. **为什么掉落逻辑不直接写在 `SlimeCo.DoDie`？** `SlimeCo` 应专注状态机和死亡流程；通过事件交给独立组件后，掉落规则可以单独测试和替换，也不会让 AI 脚本越来越臃肿。
2. **怎样保证怪物对象池复用后不会重复掉落？** `DoDie` 原有 `isDie` 保证一次生命只进入一次正式死亡；掉落组件在 `OnEnable/OnDisable` 注册与注销实例事件，并在重新启用时重置本轮标记。
3. **为什么地面物也要用对象池？** 战斗中掉落物会反复生成和消失，对象池能减少 `Instantiate/Destroy` 带来的 CPU 开销、内存分配和 GC 波动；回收与再取出时必须重置所有运行时状态。
4. **为什么满血时不先扣药再恢复？** 扣除和恢复应该组成一个可靠操作。先验证并取得实际恢复量，只有恢复成功才扣物品，可以避免玩家因无效操作损失道具。
5. **后续怎样扩展不同怪物和装备掉落？** 可以把普通怪掉落条目进一步拆成每种怪物引用的 LootTable ScriptableObject，条目支持数量区间、品质和条件；生成器仍复用同一对象池与背包 Command，不需要改怪物状态机。

### 8. 本次涉及知识点

- C# 实例事件、事件注册与注销、对象池生命周期
- ScriptableObject 掉落表与加权随机抽取
- 确定性随机输入和概率边界测试
- Trigger Collider、Kinematic Rigidbody 与父节点组件识别
- 对象池获取、回收和运行时状态重置
- `Time.deltaTime`、游戏暂停和缩放时间倒计时
- 背包堆叠、容量预查询和满包低频重试
- Command 返回结果、事务式道具消耗和事件驱动 UI
- 最大生命/魔法百分比恢复与 `Mathf.CeilToInt`
- Prefab、ScriptableObject 和编辑器幂等装配

## 功能名称：背包 Slot 选中框半透明白色统一

### 1. 实现目标

把背包 24 个 Slot 下的 `SelectedFrame` 从不透明金色统一调整为半透明白色，即 RGBA `(255, 255, 255, 200)`。这样选中框保留原图细节，同时不会用过重的颜色遮挡物品图标。

### 2. 涉及脚本

- `InventoryFeatureSetupTool`：修改新建 Slot 的默认选中框颜色，并对现有 Prefab 做仅颜色字段的幂等迁移。
- `GameplayUiRoot.prefab`：保存 24 个 `SelectedFrame` Image 的新颜色。
- `InventoryPrefabStructureTests`：验证 24 个选中框都存在 Image，且颜色完全符合配置。

### 3. 调用流程

新建 UI：`InventoryFeatureSetupTool.BuildSlots -> CreateImageObject(SelectedFrame) -> Color32(255,255,255,200)`

旧 Prefab 迁移：`脚本重载 -> NeedsSelectedFrameColorUpgrade -> UpgradeSelectedFrameColorsInPrefab -> 只修改 SelectedFrame Image.color -> 保存 Prefab`

### 4. 核心原理

Unity Inspector 的颜色通道使用 0–255 显示，但 `Color` 在代码和 Prefab 序列化中使用 0–1 浮点数。因此 Alpha 200 对应 `200 / 255 ≈ 0.7843137`。代码使用 `Color32` 表达设计值，能直接写出 255、255、255、200，语义比手动填写浮点数更清楚。

迁移逻辑只遍历现有 `InventorySlotView` 的 `SelectedFrame` 并修改 Image 颜色，不删除或重建背包节点。装配工具的新建默认值也同步修改，保证以后重新生成 UI 时不会恢复旧金色。

### 5. Unity 测试方式

1. 打开 `Assets/Prefabs/UI/GameplayUiRoot.prefab`。
2. 展开 `InventoryOverlay/InventoryWindow/ItemGrid`，任选多个 Slot 的 `SelectedFrame`。
3. 检查 Image Color 为白色，Inspector 中 A 为 `200`。
4. 进入游戏按 `B` 打开背包，点击不同格子，确认选中框显示为半透明白色。
5. 在 EditMode Test Runner 中运行 `InventoryPrefabStructureTests`，确认颜色断言通过。

### 6. 面试表达

这次 UI 调整虽然很小，但我同时处理了现有 Prefab 和编辑器生成工具。颜色使用 `Color32(255,255,255,200)` 表达设计稿中的 0–255 数值，迁移时只修改 24 个选中框的 Image 颜色，不重建整个背包，避免覆盖其他 UI 调整；测试还会检查数量和颜色，防止以后重新装配时发生回退。

### 7. 面试追问

1. **为什么 Alpha 200 在 Prefab 中不是 200？** `Color` 使用 0–1 浮点范围，200 会被换算成约 0.7843137。
2. **为什么使用 `Color32`？** 它直接使用 byte 通道，和美术提供的 0–255 数值一致，可读性更好。
3. **为什么不能只在 Inspector 修改一个 Slot？** 24 个 Slot 是独立节点，而且装配工具重建时会使用代码默认值，所以现有资源和生成源都要同步。
4. **为什么迁移时不重建整个背包 UI？** 只改目标 Image 的颜色可以减少修改范围，避免覆盖其他已经调整好的布局和引用。
5. **怎么防止颜色以后被改回去？** 编辑器测试会逐个断言 24 个 `SelectedFrame` 的 RGBA 值。

### 8. 本次涉及知识点

- Unity `Color` 与 `Color32` 的区别
- 0–255 颜色通道与 0–1 浮点转换
- Prefab Contents API
- 幂等资源迁移
- Unity UI Image 颜色
- EditMode Prefab 结构测试

## 功能名称：箱子掉落移除、小怪站位修正与 Boss 掉落/场景流程优化

### 1. 实现目标

本次修正了几个影响体验的问题：箱子不再掉落背包物品，普通小怪继续按 10% 总概率掉落生命/魔法药水；小怪移动和出生时会避开玩家、箱子和其他怪物，减少站到玩家头顶、箱子上或互相堆叠；Boss 房进入方式不同导致的黑墙/暗 Boss 问题通过运行时统一光照解决；Boss 死亡后不再弹胜利窗口，而是生成可拾取进背包的发光小球掉落物；游戏说明只在当前角色会话第一次进入主场景时显示，Boss 房和返回主场景不再重复弹出；从 Boss 房回主场景时玩家回到初始出生点。

### 2. 涉及脚本

- `InventoryDatabase`：保留小怪 10% 药水掉落，新增 Boss 专用掉落表和 Boss 光球 Prefab 引用。
- `BossLootDropController`：监听 `SpiderKingBossController.BossDied`，Boss 死亡后按 Boss 表生成发光小球。
- `WorldItemPickup`：复用地面拾取逻辑，新增运行时发光球体和点光源表现。
- `SlimeCo`：追击时先判断攻击距离再移动，并加入水平防堆叠推开。
- `MonsSpawner`：生成怪物前检查出生点附近是否被玩家、箱子或怪物占用。
- `BossRoomSceneBootstrap`：进入 Boss 房时统一方向光、环境光、补光和房间材质亮度。
- `BossBattleHudUi`：Boss 死亡后不再显示胜利弹窗。
- `GameplayStartupGuidePopup`：使用当前角色会话状态控制说明只显示一次。
- `GameplayCharacterSpawner`：从 Boss 房返回主场景时消费旧返回点状态，但不再用它覆盖默认出生点。

### 3. 调用流程

小怪掉落：`SlimeCo.DoDie -> SlimeCo.Died -> MonsterLootDropController -> InventoryDatabase.TryRollMonsterLoot -> WorldLootPool -> WorldItemPickup -> AddInventoryItemCommand -> InventorySystem`

Boss 掉落：`SpiderKingBossController.BossDied -> BossLootDropController -> InventoryDatabase.TryRollBossLoot -> WorldLootPool -> WorldItemPickup -> AddInventoryItemCommand -> InventorySystem -> InventoryPanel Toast`

Boss 场景：`BossScenePortal -> SceneFlowService.LoadBossRoomScene -> BossRoomSceneBootstrap -> EnsureBossRoomLighting / EnsureBossLootDropController / EnsureBossVictoryPortalSpawner`

说明弹窗：`SceneFlowService.StartGameplay -> GameplayStartupGuideState.ResetSession -> GameplayStartupGuidePopup.ShouldShowAtRuntimeStartup -> MarkShown -> ShowPopup`

### 4. 核心原理

箱子之所以会掉落物品，是因为之前给 `Box.prefab` 挂了 `VaultLootRewardController`，它会监听箱子的正式击破事件并直接给背包加物品。本次把这个组件从 Box 上移除，并让装配工具以后也不再加回去；小怪药水掉落表仍然是总概率 10%，生命和魔法权重 1:1，所以之前感觉生命药水多，很可能是箱子高权重生命药水混入了测试结果。

小怪站位问题主要来自两个点：追击时先移动再判断攻击距离，会让怪物冲进玩家身体；刷怪点生成时不检查周围占用，会让怪物一出生就重叠。本次改成先判断距离，进入攻击范围就停步，同时在移动速度里叠加一个只作用于水平面的分离方向，避免把怪物往 Y 轴顶起来。

Boss 光照问题不只看场景里 Directional Light 的数值，还要看运行时进入场景后有没有统一环境光、补光和材质亮度。Bootstrap 是两种进入方式都会执行的地方，所以把光照修正在这里最稳。

### 5. Unity 测试方式

1. 打开 `MainScene`，打破箱子，确认不会弹出“获得物品”，背包数量不变。
2. 击杀普通史莱姆，观察只有小怪可能掉红/蓝药水，长期统计总掉落约 10%，红蓝接近 1:1。
3. 多拉几只小怪到玩家和箱子附近，确认小怪不会明显站到玩家头顶、箱子上或完全重叠。
4. 从主场景进入 Boss 房，确认四面墙、天花板和 Boss 都能看清；直接打开 BossRoomScene 测试也应接近一致。
5. 击败 Boss，确认没有胜利弹窗，地上出现不同颜色发光小球。
6. 玩家触碰小球后，小球消失，背包获得对应物品并显示获得文本；背包满时小球不消失。
7. 首次进入主场景显示游戏说明；进入 Boss 房、从 Boss 房返回主场景都不再显示。
8. 从 Boss 房返回主场景后，确认玩家回到 `PlayerSpawnPoint`，不是箱子或 Boss 入口附近。

### 6. 面试表达

这次我主要做的是掉落来源拆分和场景流程修正。之前箱子和小怪都能给背包加物品，所以测试小怪药水概率时会被箱子的高权重生命药水干扰；我把箱子掉落组件从 Prefab 和装配工具里移除，小怪仍然使用独立的 10% 掉落判定，Boss 则新增自己的掉落表。地面拾取没有重复写一套，而是复用 `WorldItemPickup + InventorySystem`，Boss 只换成发光小球表现。小怪堆叠问题我没有直接大改成 NavMesh，而是在现有 CharacterController 状态机上做最小修复：先判断攻击距离再移动，出生和移动时都做轻量碰撞避让。Boss 房光照放在 Bootstrap 里统一处理，保证直接开场景和从主场景跳转进去表现一致。

### 7. 面试追问

1. **为什么箱子会掉落物品？** 因为 Box 上挂了监听击破事件的掉落桥接器，击破后会直接发送加背包命令。
2. **为什么小怪概率看起来不准？** 之前箱子掉落会混入测试，而且小样本随机波动很大；正确验证要固定随机输入或统计大量小怪死亡。
3. **为什么不直接上 NavMesh？** 当前项目已有 CharacterController 状态机，小步修复更安全；等怪物 AI 复杂后再升级 NavMesh 更合适。
4. **Boss 掉落为什么也走 WorldItemPickup？** 拾取、满包、堆叠是同一套规则，Boss 只需要不同视觉，复用能减少重复逻辑。
5. **为什么 Boss 光照放在 Bootstrap？** 因为无论直接测试 BossRoomScene 还是从主场景跳转，都会走 Bootstrap，放这里能保证两条路径一致。

### 8. 本次涉及知识点

- ScriptableObject 掉落表拆分
- 事件驱动 Boss/小怪死亡奖励
- CharacterController 移动顺序和防堆叠
- Physics.CheckSphere / OverlapSphereNonAlloc
- 地面拾取物对象池复用
- 运行时生成发光球体和点光源
- RenderSettings 环境光与补光
- 跨场景玩家状态恢复和出生点控制
- UI 弹窗会话状态管理

## 功能名称：背包 UI 手动编辑保护与 Prefab 预览

### 1. 实现目标

让 `GameplayUiRoot.prefab` 中的背包界面在编辑状态直接可见，方便手动修改布局、图片和颜色；同时阻止背包装配工具在脚本重载或补齐其他掉落资源时删除并重新生成现有 UI，避免手动调整再次丢失。

### 2. 涉及脚本

- `InventoryFeatureSetupTool`：已有 `InventoryPanel` 时只补齐数据、掉落物和怪物装配，不再自动重建背包视图；需要恢复默认视图时必须通过带二次确认的菜单主动执行。
- `GameplayUiRoot.prefab`：让 `InventoryOverlay` 在编辑状态保持激活，作为背包 UI 的唯一手动编辑入口。
- `InventoryPanel`：继续通过 `Start` 在运行时隐藏背包，保证进入游戏后仍需按 `B` 打开。

### 3. 调用流程

编辑流程：`打开 GameplayUiRoot.prefab -> InventoryOverlay -> InventoryWindow -> 手动修改并保存 Prefab`

运行流程：`进入玩法场景 -> InventoryPanel.Start -> 隐藏 InventoryOverlay -> 按 B -> InventoryPanel.Open -> 显示手动修改后的 Prefab`

自动装配：`脚本重载 -> NeedsSetup -> Setup -> HasExistingInventoryUi -> 保留现有 UI，只补数据和掉落资源`

### 4. 核心原理

Prefab 是背包界面的资源源头，场景中的 `GameplayUiRoot` 只是它的实例。以前装配工具会先删除 `InventoryOverlay`，再根据代码中的固定坐标重新创建节点，因此旧节点的 FileID 和场景覆盖会失效，手动修改看起来就像“恢复了旧版”。

现在使用 `InventoryPanel` 是否存在来判断 UI 是否已经正式装配。只要组件存在，自动流程就尊重当前 Prefab，不再用代码模板覆盖美术结果。真正的重建操作被放到单独菜单中并增加确认提示，使“补数据”和“重做界面”成为两个不同操作。

对应的 EditMode 测试也只保护 24 个格子、`SelectedFrame` 和必要 Image 组件，不再断言具体颜色。结构属于程序运行契约，颜色则属于可手动调整的表现数据，二者不应该混在同一条测试规则里。

`InventoryOverlay` 在 Prefab 中保持激活只是为了编辑预览；进入 Play Mode 后，`InventoryPanel.Start` 会在首帧渲染前关闭它，所以不会导致游戏开始时背包自动弹出。

### 5. Unity 测试方式

1. 双击打开 `Assets/Prefabs/UI/GameplayUiRoot.prefab`。
2. 展开 `InventoryOverlay/InventoryWindow`，确认背包界面在 Scene 视图中直接可见。
3. 修改标题颜色或窗口位置并保存 Prefab。
4. 运行 `MainScene`，确认开始时背包关闭，按 `B` 后显示修改后的样式。
5. 修改任意脚本触发 Unity 重新编译，再次运行，确认手动样式没有被恢复。
6. 不要执行 `Regenerate Inventory UI (Overwrites Manual Layout)`；它只用于确认要放弃手动布局并恢复默认结构的情况。

### 6. 面试表达

背包 UI 最初由编辑器工具生成，但如果工具在每次资源补齐时都删除重建界面，策划或美术在 Prefab 上做的调整就会丢失。我把数据装配和视觉重建拆开：自动流程只负责配置、掉落物和组件装配，检测到已有 `InventoryPanel` 后会保留当前 Prefab；视觉重建则放到带确认提示的独立菜单。这样既保留一键恢复默认结构的能力，也把 Prefab 明确成 UI 的唯一数据源，避免运行时样式回退。

### 7. 面试追问

1. **为什么手动修改会丢失？** 旧工具会销毁并重新创建 UI 节点，新节点的 FileID 不同，旧 Prefab 修改和场景覆盖无法继续对应。
2. **为什么用 `InventoryPanel` 判断 UI 已存在？** 它是背包视图的控制入口，比依赖某个可能被美术重命名的子节点路径更稳定。
3. **为什么不完全删除生成工具？** 生成工具仍适合首次创建和结构损坏后的恢复，只是不应该在普通脚本重载时自动覆盖视觉资源。
4. **Prefab 默认激活会不会让游戏一开始显示背包？** 不会，`InventoryPanel.Start` 会在首帧显示前关闭面板，按 `B` 后才重新打开。
5. **场景实例和 Prefab 应该改哪一个？** 通用 UI 应修改 Prefab；只改场景实例会产生 Override，容易让不同场景的表现不一致。

### 8. 本次涉及知识点

- Prefab 与场景实例 Override
- Unity Prefab FileID
- Editor `InitializeOnLoadMethod`
- Prefab Contents API
- 编辑状态预览与运行时初始化
- 数据装配和表现资源解耦
- 破坏性编辑器菜单的二次确认
