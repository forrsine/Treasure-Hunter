# 宝藏猎手项目学习记录

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

## 功能名称：场景异步加载界面与同步进度条

### 1. 实现目标

给登录、选角、主场景、Boss 房、重开和登出等正式场景跳转加入统一加载界面。玩家触发跳转后先进入轻量 `LoadingScene`，进度条根据 Unity 真实异步加载进度更新，避免加载体量较大的主场景时画面卡住或没有反馈。

### 2. 涉及脚本

- `SceneFlowService`：保存待加载目标、阻止重复请求，并把所有正式跳转统一转到 `LoadingScene`。
- `LoadingSceneController`：调用 `LoadSceneAsync`，换算真实进度，平滑刷新 Slider 和百分比文本，最后激活目标场景。
- `GameSceneNames`：集中保存 `LoadingScene` 场景名称，避免散落字符串。
- `BossScenePortal`：保留玩家快照和 Boss 流程，只把最后的直接场景加载改为统一入口。
- `LoadingSceneSetupTool`：在编辑器中生成加载界面、绑定引用并维护 Build Settings。
- `LoadingSceneConfigurationTests`：验证 Loading 场景、Canvas、进度 UI、默认参数和 Build Settings 配置。

### 3. 调用流程

选角进入游戏：`CharacterSelectPanelController -> SceneFlowService.StartGameplay -> LoadSceneWithLoading(MainScene) -> LoadingScene -> LoadingSceneController -> LoadSceneAsync(MainScene)`

进入 Boss 房：`BossScenePortal -> 保存玩家快照 -> SceneFlowService.LoadBossRoomScene -> 缓存宝箱进度 -> LoadSceneWithLoading(BossRoomScene) -> LoadingSceneController -> BossRoomScene`

返回或重开：`GameSessionUi / BossScenePortal -> SceneFlowService -> LoadingSceneController -> 目标场景`

### 4. 核心原理

可以把场景流程理解成中转站：原来的按钮和传送门不再直接进入目标场景，而是先把“下一站是哪里”交给 `SceneFlowService`，然后进入很轻的 `LoadingScene`。加载场景拿到目标名称后，在后台异步准备真正的目标场景，同时把进度显示给玩家。

当 `allowSceneActivation` 为 `false` 时，Unity 会在目标场景准备完成后把 `AsyncOperation.progress` 停在 `0.9`。因此 UI 使用 `progress / 0.9f` 把它换算成 0%-100%，等显示进度到达 100% 且加载界面至少显示 0.8 秒后，再允许 Unity 激活目标场景。

进度显示使用 `Mathf.MoveTowards` 追赶真实进度，并记录已经出现过的最高真实进度。这样进度条不会倒退，也不会跑到真实加载进度前面。计时和动画使用 `Time.unscaledDeltaTime`，即使上一个场景处于暂停状态，加载界面仍然可以正常刷新。

场景切换前原有的数据处理没有移动：选角仍会设置当前角色和重置新会话，进入 Boss 房仍会先保存玩家快照和宝箱进度，登出仍会先清理登录和运行时数据。新功能只统一“最后如何加载场景”，因此不会把 UI 表现和玩法数据强耦合。

### 5. Unity 测试方式

1. 在 Build Settings 中确认顺序为 `LoginScene`、`CharacterSelectScene`、`LoadingScene`、`MainScene`、`BossRoomScene`，并且全部启用。
2. 从 `LoginScene` 登录，确认出现加载界面后进入选角场景。
3. 在选角场景分别测试返回登录、选择角色进入主场景。
4. 主场景击破要求数量的宝箱并进入 Boss 传送门，确认经过加载界面进入 Boss 房。
5. 击败 Boss 后从胜利传送门返回，确认玩家状态和主场景流程正常恢复。
6. 测试暂停菜单重开、死亡后重开和退出登录，确认都经过加载界面。
7. 观察进度条只前进不倒退，百分比到 100% 后才进入目标场景。
8. 直接打开 `LoadingScene` 运行，确认 Console 提示没有目标场景，并自动回到登录场景。
9. 在 EditMode Test Runner 中运行 `LoadingSceneConfigurationTests`，确认两项配置测试通过。

### 6. 面试表达

这个项目的主场景资源比较多，直接用同步 `LoadScene` 时玩家看不到加载反馈，所以我增加了一个独立的轻量 Loading 场景。所有跳转入口仍然通过 `SceneFlowService`，服务只保存目标场景并进入 Loading 场景；`LoadingSceneController` 再使用 `LoadSceneAsync` 后台加载目标。因为 Unity 禁止激活场景时进度最大只到 0.9，我会除以 0.9 转成 UI 的 100%，显示值只平滑追赶真实进度，不会虚报。目标准备完成、进度显示到 100% 且满足最低展示时间后才允许激活。这样场景流程入口统一，加载表现和选角、背包、Boss 数据也保持解耦。

### 7. 面试追问

1. **为什么要增加独立 Loading 场景？** 它自身资源很少，可以快速显示 UI，再异步加载体量更大的目标场景，流程也比在每个旧场景重复放遮罩更统一。
2. **为什么进度要除以 0.9？** `allowSceneActivation` 为 false 时，Unity 会在场景准备完成后把异步进度停在 0.9，剩余阶段属于场景激活，所以需要归一化给 UI 使用。
3. **为什么不直接把进度条设成真实值？** 小场景的真实进度可能瞬间跳变，视觉上像闪屏；平滑值只追赶真实值且不超过它，可以改善观感又不虚报。
4. **怎么防止玩家连续点击触发多次加载？** `SceneFlowService` 在请求开始时设置跳转锁，后续请求会被忽略；目标即将激活或加载失败时再解除锁。
5. **加载界面会不会破坏玩家跨场景数据？** 不会。待传递的数据保存在现有静态运行时状态和架构 Model 中，场景对象引用不会被保存；快照和清理仍在原业务入口执行，Loading 场景只负责加载表现。

### 8. 本次涉及知识点

- `SceneManager.LoadSceneAsync`
- `AsyncOperation.progress` 与 `allowSceneActivation`
- 协程和逐帧进度刷新
- `Time.unscaledDeltaTime`
- 静态跨场景请求状态与重复请求保护
- Legacy uGUI `Canvas`、`CanvasScaler`、`Slider`、`Text`
- Build Settings 场景管理
- Editor 场景生成工具和序列化引用
- EditMode 场景配置测试
- 场景表现层与玩法数据层解耦

## 功能名称：角色属性与关卡进度数据库存档

### 1. 实现目标

为同一账号的四个角色槽位提供相互隔离的 SQL Server 存档。每个角色保存等级、经验、待选择属性次数、8 类属性强化次数、累计宝箱击破数和已完成 Boss 轮数；重新登录或重新选角后恢复成长数值并回满生命、魔法。死亡或主动重开只清空本局属性强化和待选择次数，长期等级、经验、宝箱和 Boss 进度继续保留。

### 2. 涉及脚本

- `message.cs`、`MessageDispatch`：客户端和服务端同步追加角色进度字段、进入角色 ID 与保存请求/响应。
- `DBService`、`TCharacter`、`Character`、`UserService`：幂等迁移数据库、事务保存进度、校验账号角色归属和数值范围。
- `GameApiClient`：提供进入角色、保存角色进度、离开角色三个协程接口，并用服务端返回值刷新本地角色缓存。
- `PlayerRuntimeStats`、`PlayerModel`、`PlayerProgressionSystem`：统一记录 8 类强化次数，并按职业基础数据、等级和正式强化公式恢复最终属性。
- `CharacterProgressSaveService`：监听成长事件，使用真实时间防抖、版本号和串行协程合并保存请求；重开时强制等待数据库确认。
- `BossRunProgressState`、`BossPortalUnlockController`：恢复长期宝箱/Boss 进度和入口状态，不重复发放奖励。
- `CharacterSaveSlot`、`CharacterSelectPanelController`、`GameSessionUi`、`ReStartPanel`、`LogoutButton`：展示角色摘要，并把进入、重开、退出、登出接入保存流程。
- `CharacterProgressPersistenceTests`：覆盖属性恢复、跨场景次数、重开清理、Boss 进度、槽位摘要和协议序列化。

### 3. 调用流程

进入角色：`CharacterSaveSlot -> CharacterSelectPanelController -> GameApiClient.EnterCharacter -> UserService -> 校验账号归属 -> SceneFlowService.StartGameplay -> PlayerProgressionSystem.InitializePlayer`

自动保存：`经验/强化/宝箱/Boss 事件 -> CharacterProgressSaveService -> 1 秒真实时间防抖 -> GetPlayerProgressSaveDataQuery -> GameApiClient.SaveCharacterProgress -> UserService 校验 -> DBService 事务更新 -> 服务端权威角色返回客户端`

死亡或重开：`PlayerDiedEvent / 重开按钮 -> CharacterProgressSaveService.FlushNow(clearUpgrades: true) -> 清空待选择次数和 8 类强化保存负载 -> 数据库确认成功 -> SceneFlowService.RestartGameplay`

### 4. 核心原理

角色的最终攻击力、最大生命、移速等浮点数没有直接写进数据库，数据库只保存“某种强化选过几次”。读取时先按职业和等级建立基础属性，再循环调用正式强化公式。这样以后修改职业基础值或数值平衡时，旧存档不会保存一份已经过期的最终面板值，也能避免客户端和数据库各维护一套公式。

数据库把角色的单值进度放在 `PlayerCharacters`，把 8 类可重复数据放在 `CharacterAttributeUpgrades`。强化表使用 `(CharacterId, AttributeType)` 联合主键，一种强化对一个角色最多一行。保存时主表更新与强化表删除、重建在同一个事务中完成，任何一步失败都会回滚，避免只保存了一半。

服务端不接受客户端指定任意待保存角色，而是使用当前网络会话已经进入的 `Session.Character`。进入角色时先验证角色属于登录账号；保存时再验证负数、重复强化类型、非法类型、进度倒退，以及“强化次数 + 待选择次数不能超过升级可获得次数”。当前仍是客户端权威原型，但至少防止跨账号写角色和明显异常数据。

自动保存使用 1 秒 `realtimeSinceStartup` 防抖。多个经验或进度变化只生成一个请求，请求之间严格串行，并用变化版本号判断保存期间是否又有新数据，避免旧响应覆盖新状态。强制保存失败时不切场景，让玩家可以在服务恢复后重试。

### 5. Unity 测试方式

1. 启动 SQL Server，再运行 `TreasureHunter.Server`。
2. 从 `LoginScene` 登录并创建角色，选中槽位进入主场景。
3. 获得经验、升级并选择多类强化，再击破宝箱、击败 Boss；等待约 1 秒自动保存。
4. 正常退出登录后重新登录，确认角色槽两行摘要显示正确等级、经验、Boss 轮数和宝箱次数。
5. 重新进入角色，检查属性面板恢复强化结果，生命和魔法为满值；已满足门槛时 Boss 入口应恢复，但不重复发经验或掉落。
6. 让角色死亡或点击主动重开，确认保存成功后才重载；等级、经验、宝箱和 Boss 保留，8 类强化与待选择次数归零。
7. 保存期间关闭服务端，确认界面显示失败并停留在当前场景；恢复服务端后再次操作可以重试。
8. 覆盖已有角色槽，确认新角色为 1 级、0 经验、0 强化、0 关卡进度。
9. 在 Test Runner 运行全部 EditMode 测试，确认 `CharacterProgressPersistenceTests` 7 项以及项目原有测试全部通过。

### 6. 面试表达

这个存档系统我分成了运行时数据、网络协议和数据库三层。客户端不保存容易过期的最终攻击力等数值，而是保存 8 类属性分别强化了几次，读档时先按职业和等级初始化，再复用正式强化公式重算，因此调整平衡配置后旧存档仍能工作。经验、强化、宝箱和 Boss 事件会通知一个常驻存档服务，它用 1 秒防抖合并请求，并保证请求串行。服务端只保存当前会话已经进入的角色，会校验账号归属和数值范围，再用 SQL 事务同时更新角色主表和强化明细表。死亡或主动重开会先把强化清零写入数据库，确认成功后才切场景，避免客户端看起来重开成功但数据库还是旧数据。

### 7. 面试追问

1. **为什么保存强化次数而不是最终属性？** 最终属性依赖职业、等级和配置公式，保存次数可以在配置变化后重新计算，减少冗余和版本兼容问题。
2. **为什么强化要单独建表？** 它是一对多数据，规范化表便于增加属性类型、加联合主键防重复，也避免主表不断增加 8 个甚至更多字段。
3. **怎么避免频繁经验事件造成大量数据库请求？** 客户端用一秒真实时间防抖合并变化，并串行发送；保存期间的新变化通过版本号触发下一轮请求。
4. **怎么防止玩家保存其他账号的角色？** 请求不携带可任意写入的角色 ID，服务端只使用登录会话中已校验的 `Session.Character`。
5. **为什么重开失败时不能直接切场景？** 如果先切场景但清零写库失败，下次登录会恢复旧强化；因此强制保存是场景跳转的前置条件。

### 8. 本次涉及知识点

- SQL Server 幂等表结构迁移、联合主键、外键与事务
- Protobuf 向后兼容字段追加
- 客户端/服务端会话与账号归属校验
- 数据快照、深拷贝和服务端权威响应
- 防抖、协程串行请求、脏标记与版本号
- QFramework Event / Query / System 分层
- 属性公式重放与派生数据恢复
- Unity 跨场景状态和静态进度恢复
- 事件驱动 UI 与角色槽摘要
- EditMode 数值、协议和场景流程测试

## 功能名称：ESC 暂停界面保存并返回角色选择

### 1. 实现目标

把 ESC 暂停界面的退出按钮改成“保存并退出”。玩家点击后先保存当前角色成长并通知服务端离开角色，成功后返回角色选择界面；账号登录态继续保留，不退出到登录场景，也不关闭游戏。死亡结算界面的“退出游戏”保持原行为。

### 2. 涉及脚本

- `GameSessionUi`：修改暂停按钮文案与回调，保存期间显示状态、禁用按钮，失败时留在原界面。
- `SceneFlowService`：新增保留登录态的 `ReturnToCharacterSelect`，集中清理当前角色的局内状态。
- `CharacterProgressPersistenceTests`：验证暂停和死亡界面的文案边界，以及返回选角不会清除登录会话。

### 3. 调用流程

`ESC -> GameSessionUi.ShowPauseMenu -> 保存并退出 -> PersistCurrentScore -> CharacterProgressSaveService.FlushAndLeave(false) -> GameApiClient.SaveCharacterProgress -> GameApiClient.LeaveCharacter -> SceneFlowService.ReturnToCharacterSelect -> LoadingScene -> CharacterSelectScene`

### 4. 核心原理

“离开角色”和“退出账号”是两个不同层级。`FlushAndLeave(false)` 会保存当前角色并清理服务端在线角色，但不会删除登录信息；`ReturnToCharacterSelect` 只清理背包、Boss 状态、当前选角和跨场景快照。原有 `LogoutToLogin` 才会调用 `ClearSession` 删除账号登录态。

保存失败时不能先切场景，否则玩家会误以为进度已成功落库。因此按钮会等待保存和离场都成功后才进入 LoadingScene；失败时恢复按钮并显示错误，玩家可以在服务恢复后重试。

### 5. Unity 测试方式

1. 登录并进入 `MainScene`，获得经验、强化和宝箱进度。
2. 按 ESC，确认次按钮显示“保存并退出”。
3. 点击后确认出现“正在保存并返回角色选择...”，随后返回选角界面。
4. 确认账号仍然登录，角色槽显示最新进度；再次进入后强化仍保留。
5. 关闭服务端再点击，确认不会切场景，暂停界面显示失败信息并允许重试。
6. 让玩家死亡，确认结算界面按钮仍显示“退出游戏”。

### 6. 面试表达

我把角色离场和账号登出拆成了两个场景流程。ESC 的“保存并退出”会先强制保存角色，再发送离开角色请求，只有两步都成功才返回角色选择；返回时保留账号和最新角色缓存，只清理当前角色的局内状态。这样玩家可以直接切换角色，同时避免保存失败却已经切走，或者误把正常退出当成死亡重开清掉强化。

### 7. 面试追问

1. **为什么不能直接加载角色选择场景？** 必须先等待保存和服务端离场，否则可能丢进度或残留在线角色。
2. **为什么不复用 LogoutToLogin？** 它会清除账号登录态和角色缓存，与“返回选角”语义不同。
3. **为什么传 `clearUpgrades: false`？** 正常离场要保留强化，只有死亡和主动重开才清零。
4. **保存失败怎么处理？** 不切场景，恢复按钮并显示错误，让玩家重试。
5. **返回选角需要清理哪些状态？** 当前选角、背包、Boss/宝箱局内状态、玩家跨场景快照和新手引导会话状态。

### 8. 本次涉及知识点

- 账号会话与角色会话分层
- 强制保存与服务端离场顺序
- 协程异步流程和失败回退
- 场景切换前运行时状态清理
- Unity UI 动态文案和按钮事件
- EditMode Prefab 与源代码边界测试

## 功能名称：战士角色完整可玩化

### 1. 实现目标

让战士复用现有玩家运行时架构，具备移动、奔跑、跳跃、冲刺、普通攻击、两个公共技能、受伤减伤和公共音效。由于 Human Pack 没有独立走路与翻滚动作，走路使用原速 `Human Run`，奔跑和冲刺使用 2 倍速 `Human Run`；普通攻击为不可续接连击的单段剑击。

### 2. 涉及脚本

- `PlayerPresentationComponent`：把通用移动、攻击、技能和冲刺指令转换为战士 Animator 参数；缺少 Roll 参数时切到快速跑步表现。
- `PlayerCombatComponent`：为没有动画事件的战士按攻击时长计算前摇和有效帧，只开启一次公共攻击盒。
- `PlayerModel`、`CharacterDefine`：把职业 `defense` 解释为百分比基础减伤，战士的 20 对应 20% 减伤。
- `WarriorAnimatorControllerSetupTool`：用 UnityEditor API 生成项目自有战士 Animator、上半身 AvatarMask，并绑定战士游戏/预览 Prefab。
- `WarriorPlayableTests`：验证职业数值、20% 减伤、Animator 参数与 2 倍速动画，以及 Prefab 控制器引用。
- `Warrior.controller`、`WarriorUpperBody.mask`：分别负责战士移动/动作状态机和攻击上半身覆盖范围。

### 3. 调用流程

移动：`PlayerRuntimeController -> PlayerMovementComponent -> PlayerPresentationComponent.SetMovement -> Warrior.controller Locomotion BlendTree`

冲刺：`右键输入 -> PlayerMovementComponent.StartRoll/HandleRoll -> CharacterController 快速位移 -> PlayerPresentationComponent.PlayRoll -> Speed=1 的 2 倍速跑步动作 -> PlayerAudioComponent.PlayRoll`

普攻：`左键输入 -> PlayerCombatComponent -> PlayerPresentationComponent.SetCombo(1) -> Attack Trigger -> 单段剑击 -> 前摇计时 -> AttackHitbox 开启一次 -> WeaponCo -> 敌人受伤`

技能：`技能输入 -> PlayerSkillComponent -> PlayerPresentationComponent.PlaySkill -> Skill Trigger -> 剑击占位动作 + 原公共技能效果/音效`

受伤：`TakePlayerDamageCommand -> PlayerCombatSystem -> CharacterDefine.defense / 100 -> DamageReduction -> 扣除实际生命`

### 4. 核心原理

战士没有复制一套玩家控制代码。`PlayerRuntime.prefab` 继续负责输入、位移、体力、攻击、技能、生命和音效，战士 Prefab 只提供模型与 Animator。表现层根据 `SimpleSpeedAttack` 风格写入 `Speed`、`Attack`、`Skill` 和 `IsGrounded` 参数，因此同一套玩法逻辑可以驱动不同职业外观。

移动 BlendTree 使用三个阈值：0 是待机，0.5 是原速跑步动作，1 是 2 倍速跑步动作。攻击单独放在带上半身遮罩的层里，角色攻击时下半身仍可继续播放移动，表现不会完全锁死。战士素材没有动画事件，所以攻击系统按 `basicAttackDuration` 的比例计算前摇和攻击盒持续时间，让伤害发生在挥剑中段，而不是按键瞬间。

职业防御统一转换为 0 到 0.95 的减伤比例，再进入现有伤害公式。这样 100 点伤害打到 `defense=20` 的战士时实际扣除 80 点，也能继续与已有减伤升级逻辑组合。

### 5. Unity 测试方式

1. 打开 `LoginScene`，登录后创建或选择战士（classId=1），进入 `MainScene`。
2. 使用 WASD 移动，按 Shift 奔跑；确认走路是原速跑步动作，奔跑动作播放速度明显约为两倍。
3. 按空格确认仍能起跳和落地；因为没有跳跃素材，空中保持中立姿势属于预期降级表现。
4. 按右键冲刺，确认战士快速冲向当前输入方向，并播放快速跑步动作及原公共翻滚音效。
5. 连续点击左键，确认每次只播放一段剑击，不进入刺客三连击；靠近敌人时挥剑中段只结算一次伤害。
6. 释放 Fireball 和 Poison 两个公共技能，确认技能效果、冷却、占位剑击动作和公共技能音效正常。
7. 让敌人对战士造成 100 点基础伤害，确认未触发其他减伤时实际扣除 80 点。
8. 在 Test Runner 的 EditMode 中运行 `WarriorPlayableTests`，确认职业配置、减伤和动画装配测试全部通过。

### 6. 面试表达

战士可玩化我没有复制刺客控制器，而是复用了统一的 PlayerRuntime。输入、移动、体力、攻击、技能、生命和音效仍在公共组件中，职业配置只决定基础属性和动画适配风格。我为战士单独生成了项目自有 Animator：用一维 BlendTree 把同一个 Run 动作分别作为原速走路和二倍速奔跑，缺少翻滚动画时保留原冲刺位移并播放二倍速跑步。战士普通攻击是单段攻击，因为素材没有动画事件，所以我根据攻击时长在挥剑中段开启一次命中盒。职业 defense 则统一解释为百分比减伤，战士 20 点防御就是 20% 减伤。这样实现范围小，又保留了后续替换正式动画和扩展职业技能的接口。

### 7. 面试追问

1. **为什么不直接复制刺客脚本改成战士？** 公共玩法逻辑已经组件化，复制会产生两套输入、攻击和生命逻辑，后续修 Bug 容易不一致；通过动画适配参数即可复用。
2. **没有走路和翻滚动画怎么处理？** 走路用 Run 原速，奔跑与冲刺用 Run 二倍速；冲刺的真实位移仍由 `CharacterController` 驱动，动画只负责表现。
3. **没有动画事件时怎么保证攻击时机？** 根据职业配置的攻击总时长，按比例设置前摇和攻击盒有效时间，只在挥剑中段开启一次碰撞判定。
4. **为什么攻击放在单独 Animator 层？** 上半身播放挥剑、下半身保留移动 BlendTree，既能复用移动状态，也便于以后替换更多攻击或技能动作。
5. **防御为什么用百分比而不是直接减固定数值？** 百分比在不同伤害量下更稳定，并且可以直接接入已有 `DamageReduction` 公式和强化系统；同时用 95% 上限避免完全免伤。

### 8. 本次涉及知识点

- AnimatorController、Animator 参数和状态过渡
- 一维 BlendTree、动画播放速度与素材降级方案
- Animator Layer、AvatarMask 和上下半身混合
- 代码计时攻击有效帧与 Collider 命中盒
- 职业配置数据与运行时属性初始化
- 百分比减伤、数值 Clamp 和伤害结算
- 组件复用、表现层与玩法逻辑解耦
- UnityEditor Prefab 编辑 API 与资源生成工具
- NUnit EditMode 回归测试

## 功能名称：弓箭手与法师完整可玩化

### 1. 实现目标

让弓箭手和法师接入与刺客、战士相同的 `PlayerRuntime` 操作流程，具备移动、奔跑、跳跃、冲刺、普通攻击、公共技能、受伤、死亡、属性和音效。两个职业的普通攻击改为沿角色正前方飞行的小球：弓箭手使用较小、较快的金色球体，法师使用较大、较慢的蓝紫色球体；小球命中第一个有效敌人或实体障碍后回收。缺少走路和翻滚素材时，继续使用原速/两倍速跑步动作作为降级表现。

### 2. 涉及脚本

- `CharacterDefine`、`CharacterDefine.json`：新增普通攻击类型和投射物释放比例、速度、寿命、半径、颜色配置，职业差异不写死在攻击脚本里。
- `PlayerRuntimeController`：装配并初始化公共 `PlayerRangedAttackComponent`，近战职业不会预热无用投射物。
- `PlayerCombatComponent`：区分近战碰撞盒和远程释放点，动画事件优先、代码计时兜底，并保证一次攻击只生成一个小球。
- `PlayerAnimationEventRelay`：把 Human Pack 攻击动画中的 `shoot` 事件转发到公共战斗组件。
- `PlayerRangedAttackComponent`：按职业配置计算出生点和外观，并维护 8 个小球的可扩容对象池。
- `PlayerBasicAttackProjectile`：负责直线飞行、寿命、碰撞过滤和幂等回收。
- `PlayerBasicAttackDamageResolver`、`WeaponCo`：近战与远程共用攻击力、暴击、实际伤害、飘字和吸血结算。
- `RangedCharacterAnimatorControllerSetupTool`：生成弓箭手/法师项目自有 Animator 与上半身遮罩，并绑定游戏、预览 Prefab。
- `RangedCharacterPlayableTests`：验证职业配置、动画参数、两倍速移动、`shoot` 事件、Prefab 引用和运行时组件。

### 3. 调用流程

移动：`输入 -> PlayerMovementComponent -> PlayerPresentationComponent.SetMovement -> Archer/Wizard Locomotion BlendTree`

冲刺：`右键 -> PlayerMovementComponent -> CharacterController 快速位移 -> PlayerPresentationComponent.PlayRoll -> Speed=1 两倍速跑步 -> 公共翻滚音效`

远程普攻：`左键 -> PlayerCombatComponent -> Attack Trigger -> 攻击动画 shoot 事件 -> PlayerAnimationEventRelay -> TryReleaseRangedBasicAttack -> PlayerRangedAttackComponent 对象池 -> PlayerBasicAttackProjectile`

命中：`Projectile.OnTriggerEnter -> PlayerBasicAttackDamageResolver -> RollPlayerAttackCommand -> FighterInterface.Hit -> FloatingCombatText -> RecordPlayerDamageDealtCommand/吸血 -> Projectile.Release 回池`

事件缺失兜底：`攻击开始 -> basicAttackDuration × projectileReleaseRatio + 宽限时间 -> TryReleaseRangedBasicAttack -> 同一个去重标记阻止重复发射`

### 4. 核心原理

远程职业仍然复用公共玩家运行时，职业 Prefab 只负责模型和 Animator。`CharacterDefine` 通过 `basicAttackType` 告诉战斗组件当前是近战还是投射物攻击，再通过速度、寿命、半径、颜色和释放比例描述表现差异。因此新增另一种直线远程职业时，可以先增加配置而不是复制整套控制代码。

攻击动画负责提供最贴合动作的释放帧。弓箭手的 `shoot` 事件约位于动作 40%，法师约位于 50%，事件经 Relay 转给战斗组件。战斗组件同时安排一个略晚的代码计时兜底；无论事件还是兜底先到，都会设置同一个 `projectileReleasedThisAttack` 标记，所以不会生成两颗球。

投射物池预热 8 个球体，发射时从队列 `Get`，命中或寿命结束后 `Release` 回队列。回收时清空速度、拥有者回调和激活状态，防止上一次攻击状态残留。极端攻击频率超过预热数量时允许临时扩容，之后新对象同样回池复用，减少频繁 `Instantiate/Destroy` 带来的性能波动和 GC。

近战武器碰撞盒与远程投射物最终都调用 `PlayerBasicAttackDamageResolver`。它只保留一份暴击、目标实际生命、伤害飘字和吸血后结算逻辑，避免修正近战公式后远程职业仍使用旧算法。投射物只负责“飞行与碰到谁”，不负责定义玩家伤害公式。

### 5. Unity 测试方式

1. 打开 `LoginScene`，分别创建或选择法师（classId=2）与弓箭手（classId=3），进入 `MainScene`。
2. 用 WASD、Shift、空格测试移动、奔跑和跳跃；确认移动正常，走路为原速跑步动作，奔跑为两倍速动作。
3. 按右键冲刺，确认角色快速跑向输入方向，播放两倍速跑步和原公共翻滚音效，没有翻滚动作属于预期降级。
4. 面向敌人点击左键：弓箭手应在射击动作中发出较小较快的金色球，法师发出较大较慢的蓝紫色球。
5. 确认小球沿角色正前方飞行，命中第一个敌人只结算一次伤害并消失；打到墙体也会消失，普通区域 Trigger 不会拦截。
6. 观察伤害飘字、暴击和吸血效果，确认与刺客/战士普通攻击规则相同；连续点击时每个攻击周期只发射一个小球。
7. 按技能键释放原 Fireball、Poison 技能，确认技能冷却、效果和公共音效仍正常，技能攻击动画不会额外发射普攻球。
8. 打开 `Window > General > Test Runner`，在 EditMode 运行 `RangedCharacterPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

弓箭手和法师我没有各复制一套玩家控制脚本，而是继续复用公共 PlayerRuntime，只在职业配置中声明普攻类型和投射物参数。攻击动画的 shoot 事件会转发给战斗组件，在准确释放帧从对象池取出一个小球；同时有代码计时兜底，并通过单次攻击标记防止事件和兜底重复发射。小球组件只处理直线飞行、碰撞和回收，真正的伤害、暴击、飘字与吸血抽到公共 DamageResolver，让近战攻击盒和远程子弹共用同一套规则。对象池预热 8 个并支持扩容，减少连续攻击时频繁创建销毁和 GC。动画方面用一维 BlendTree 复用同一个 Run 动作，原速模拟走路、两倍速表现奔跑和无翻滚素材时的快速冲刺。

### 7. 面试追问

1. **为什么动画事件之外还需要代码兜底？** 第三方动画可能在重导入或替换时丢失事件；兜底保证玩法不会完全失效，去重标记又能避免正常情况下发射两次。
2. **为什么投射物和伤害结算要拆成两个类？** 投射物是表现与碰撞载体，伤害属于战斗规则；拆开后可复用伤害公式，也能把直线球替换为箭矢或法术模型而不改数值系统。
3. **对象池为什么预热 8 个还允许扩容？** 8 个覆盖正常攻速，预热减少首轮卡顿；允许扩容可以避免极端攻速或生命周期重叠时攻击直接丢失，回收后仍会继续复用。
4. **如何避免子弹打到玩家自己？** 发射时保存玩家根节点，碰撞时先比较 `other.transform.root`；自身碰撞直接忽略，普通 Trigger 也不会阻挡。
5. **现在的直线小球以后怎么升级？** 可以在配置中新增投射物 Prefab、命中效果、穿透数和弹道类型，再让池按配置创建；伤害 Resolver 不需要跟着改。

### 8. 本次涉及知识点

- Animator Event、事件转发和代码计时兜底
- Animator Layer、AvatarMask、一维 BlendTree 与动画倍速
- 配置驱动的职业差异和枚举分支
- Rigidbody Kinematic、Trigger 碰撞与 FixedUpdate 移动
- 对象池预热、动态扩容、状态重置和幂等回收
- 伤害逻辑复用、接口目标查找、暴击/吸血后结算
- MaterialPropertyBlock 无材质实例化换色
- Prefab 自动装配与 UnityEditor 资源生成 API
- NUnit EditMode 回归测试

## 功能名称：战士方向、尺寸、跳跃与攻击修复

### 1. 实现目标

修复战士进入游戏后背对移动方向、模型尺寸过大、跳跃高度偏低，以及左键攻击没有稳定播放剑击和命中敌人的问题。游戏内战士模型缩放为原来的一半并移除额外的 180 度旋转；选角预览保持不变。公共玩家跳跃高度从 1 米提高到 2 米，因此四个职业都会获得更明显的跳跃表现。

### 2. 涉及脚本

- `Warrior.prefab`：游戏模型根旋转归零，统一缩放改为 0.5。
- `PlayerRuntime.prefab`：公共 `jumpHeight` 从 1 改为 2，CharacterController 和 AttackHitbox 尺寸保持不变。
- `PlayerPresentationComponent`：简单动画职业攻击时优先直接切入 `Attack Layer.Attack`，没有标准状态时才使用 Trigger 兜底。
- `WarriorAnimatorControllerSetupTool`：重新生成战士 Animator 时自动恢复游戏 Prefab 的正确朝向和缩放，但不处理预览 Prefab 变换。
- `WarriorPlayableTests`：增加模型变换、公共跳跃高度、真实攻击状态和单窗口伤害去重测试。

### 3. 调用流程

角色生成：`GameplayCharacterSpawner -> 实例化 PlayerRuntime -> 实例化 Warrior.prefab -> 保留 Prefab 局部变换 -> 0 度朝向、0.5 倍模型与运行时前方对齐`

跳跃：`空格 -> PlayerMovementComponent.TryJump -> sqrt(jumpHeight × -2 × gravity) -> CharacterController.Move -> 2 米目标跳高`

攻击表现：`左键 -> PlayerCombatComponent.StartFirstAttack -> PlayerPresentationComponent.SetCombo(1) -> Attack Layer 权重恢复为 1 -> CrossFade Attack Layer.Attack`

攻击伤害：`攻击开始 -> 0.7 × 0.25 秒前摇 -> WeaponEnable -> 前方 AttackHitbox -> WeaponCo -> PlayerBasicAttackDamageResolver -> FighterInterface.Hit`

### 4. 核心原理

玩家真正的移动方向来自外层 `PlayerRuntime` 的 Transform，职业模型只是它的子物体。原战士模型子物体旋转了 180 度，所以代码向前移动、攻击盒也在运行时前方，但玩家看到的人物和剑却朝向相反。把游戏模型根旋转恢复为单位旋转后，模型、移动和攻击判定使用同一个前方方向，既修复视觉也修复“看起来砍中但没有伤害”的空间错位。

模型缩放只发生在职业表现 Prefab 上。CharacterController 继续使用公共玩家尺寸，AttackHitbox 也继续位于局部前方 0.85 米、半径 0.65 米，因此不会因为缩小模型而把移动碰撞和攻击距离一起缩短。

简单职业的攻击层在绑定模型时会先设为 0，攻击开始时再恢复为 1。为了避免 Trigger 与攻击层启用发生在同一帧时状态切换不稳定，现在会先检查标准 `Attack Layer.Attack` 是否存在，存在就直接 CrossFade；只有第三方控制器没有该状态时才回退到 Trigger。战斗伤害仍采用代码计时有效帧，不依赖战士动画事件。

### 5. Unity 测试方式

1. 从 `LoginScene` 选择战士进入 `MainScene`。
2. 按 W 前进，确认战士面部和身体朝向移动方向，而不是倒着移动。
3. 对比原效果，确认游戏内模型约为原来一半；返回选角界面时预览大小和朝向应保持原样。
4. 分别使用四个职业按空格，确认跳跃高度均比原来的 1 米明显提高，目标高度为 2 米。
5. 战士靠近并面向敌人点击左键，确认立即播放单段剑击，在动作中段出现伤害飘字并扣除敌人生命。
6. 让敌人停留在攻击盒内，确认同一剑只结算一次；下一次点击可以再次造成伤害。
7. 打开 `Window > General > Test Runner`，运行 `WarriorPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

这个问题本质上是表现坐标和玩法坐标没有对齐。玩家移动和攻击盒使用 PlayerRuntime 的正前方，但战士模型 Prefab 自己又旋转了 180 度，所以画面看起来是在向后走，剑砍向的视觉方向也和攻击盒相反。我只修正了游戏模型根节点的旋转和缩放，没有改公共碰撞体。攻击动画方面，简单职业攻击时先恢复上半身攻击层，再直接 CrossFade 到标准 Attack 状态，状态不存在才回退到 Trigger；伤害仍通过代码计时开启公共攻击盒，并对同一命中窗口做目标去重。跳跃则把公共 Prefab 中实际覆盖的 1 米参数恢复为 2 米，因此所有职业手感保持一致。

### 7. 面试追问

1. **为什么旋转模型而不是旋转 PlayerRuntime？** PlayerRuntime 决定移动、摄像机和攻击盒方向，旋转它会影响整个玩法坐标；问题只来自战士美术资源，所以只校正表现子物体。
2. **为什么模型缩小但碰撞体不缩小？** 这次需求是修复视觉尺寸，公共玩家壳的碰撞和攻击距离已经用于四职业；一起缩放会改变战士玩法平衡。
3. **为什么不用纯 Trigger 播攻击？** Trigger 依赖 Animator 在同一帧完成层权重和过渡判断，直接 CrossFade 可以确定进入目标状态，同时保留 Trigger 作为第三方控制器兼容兜底。
4. **战士动画没有攻击事件，伤害帧怎么控制？** 使用职业基础攻击时长乘以前摇和有效帧比例，动作中段开启攻击盒，结束后自动关闭。
5. **如何避免敌人站在攻击盒里连续掉血？** WeaponCo 用攻击窗口 ID 和 HashSet 记录本窗口已命中的目标，OnTriggerStay 也不会对同一目标重复结算。

### 8. 本次涉及知识点

- 父子 Transform、局部旋转与模型坐标系
- 视觉缩放和玩法碰撞体解耦
- 抛体运动初速度公式与 CharacterController
- Animator Layer、状态哈希、HasState 和 CrossFadeInFixedTime
- 代码计时攻击有效帧
- Trigger Enter/Stay 与单窗口目标去重
- UnityEditor PrefabUtility 资源修改
- SerializedObject 与 EditMode 集成测试

## 功能名称：三职业朝向、0.7 倍尺寸与操作一致性复查

### 1. 实现目标

以刺客的公共操作流程为基准，复查战士、法师和弓箭手的移动、奔跑、跳跃、冲刺、普通攻击与技能入口。修正法师和弓箭手模型额外旋转 180 度造成的视觉方向错误，并把三个职业的游戏模型统一调整为 0.7 倍；选角预览保持原来的构图。

### 2. 涉及脚本

- `Warrior.prefab`、`Archer.prefab`、`Wizard.prefab`：游戏表现根节点统一使用单位旋转和 0.7 倍缩放。
- `WarriorAnimatorControllerSetupTool`：重新生成战士控制器时继续写入正确游戏 Transform。
- `RangedCharacterAnimatorControllerSetupTool`：区分游戏与预览 Prefab，只校正游戏内法师和弓箭手 Transform。
- `WarriorPlayableTests`、`RangedCharacterPlayableTests`：验证 Transform、Attack 状态、远程发射方向、伤害和对象池回收。

### 3. 调用流程

公共操作：`InputCo -> GameplayRuntime -> PlayerRuntimeController -> PlayerMovementComponent / PlayerCombatComponent / PlayerSkillCastComponent`

游戏模型生成：`GameplayCharacterSpawner -> PlayerRuntime -> 职业游戏 Prefab -> 保留单位旋转和 0.7 缩放`

远程普攻：`左键 -> StartFirstAttack -> SetCombo(1) -> Attack Layer.Attack -> shoot/计时兜底 -> PlayerRangedAttackComponent -> PlayerBasicAttackProjectile -> DamageResolver -> 回池`

### 4. 核心原理

四个职业使用同一个 `PlayerRuntime`，所以操作一致性由公共输入、移动、战斗和技能组件保证，职业模型不应该再读取另一套输入。职业差异只留在配置和表现层：刺客是三连击，战士是单段近战，法师和弓箭手发射不同参数的小球。

移动、近战攻击盒和投射物都使用 `PlayerRuntime.transform.forward`。如果模型 Prefab 自己再旋转 180 度，代码仍然向正确方向移动和发射，但玩家看到的人物会面对反方向。把游戏模型根旋转归零后，视觉和玩法坐标重新一致。

0.7 倍缩放只影响职业模型，不会改变外层 CharacterController、公共 AttackHitbox 或投射物出生距离。生成工具区分游戏 Prefab 与预览 Prefab，可以防止以后重新生成 Animator 时覆盖本次修复，也不会破坏选角界面构图。

### 5. Unity 测试方式

1. 分别选择战士、法师和弓箭手进入 `MainScene`，确认模型尺寸为 0.7 倍并面朝移动方向。
2. 对照刺客测试 WASD、Shift、Space、右键或 LeftAlt、左键以及数字键 1/2/3。
3. 战士面对敌人左键，确认播放单段剑击且同一剑只命中一次。
4. 法师和弓箭手面对敌人左键，确认进入 Attack 动画并从正前方发射小球，命中后造成伤害并消失。
5. 返回选角界面，确认三个预览模型的尺寸和朝向没有变化。
6. 在 EditMode Test Runner 中运行全部测试，确认没有失败。

### 6. 面试表达

这次我先确认了四个职业都通过同一个 PlayerRuntime 接收输入，所以没有复制控制代码。问题出在法师和弓箭手的表现 Prefab 额外旋转了 180 度，而移动、攻击盒和小球都使用 PlayerRuntime 的正前方，导致视觉与玩法方向不一致。我把三个游戏模型统一为单位旋转和 0.7 倍缩放，但不动公共碰撞体和选角预览。同时把这些规则写入 Animator 生成工具，并补充了攻击状态、投射物方向、实际命中和对象池回收测试，防止资源重新生成后问题复发。

### 7. 面试追问

1. **如何保证不同职业操作一致？** 四个职业复用同一套输入、移动、战斗和技能组件，职业配置只决定属性与攻击表现。
2. **为什么不旋转投射物方向来迁就模型？** 玩法方向应以 PlayerRuntime 为权威，单独反转投射物会让摄像机、移动和其他技能继续不一致。
3. **为什么不缩放 CharacterController？** 需求只调整视觉尺寸，缩放公共碰撞体会改变通行能力和近战距离。
4. **如何保证 Animator 重新生成后修复不丢失？** 生成工具在保存游戏 Prefab 时重新写入单位旋转和 0.7 缩放，预览 Prefab则跳过该步骤。
5. **远程攻击测试验证了什么？** 验证 Attack 状态、单次攻击去重、正前方出生、实际命中以及回收到对象池的完整链路。

### 8. 本次涉及知识点

- 公共输入接口与职业运行时复用
- Transform 父子坐标和 `transform.forward`
- Animator Layer、CrossFade 和动画事件
- 近战与投射物攻击表现差异
- 对象池获取、命中与回收
- Prefab 生成工具的幂等配置
- EditMode 集成测试与回归保护

## 功能名称：法师紫色火球与弓箭手箭矢普攻重构

### 1. 实现目标

解决法师和弓箭手点击一次普攻却同时出现 Human Pack 演示投射物与公共彩色球体的问题。法师现在只发射一个紫色 MagicMissile 1，视觉尺寸为技能火球来源尺寸的 0.7 倍，沿 3 米高度的抛物线飞行，并在碰撞或终点产生 1.5 米范围伤害；弓箭手只发射一个原始 Human Bolt，沿人物正前方直线飞行并命中第一个目标。两者继续复用公共攻击力、暴击、飘字、吸血和对象池。

### 2. 涉及脚本

- CharacterDefine.cs、CharacterDefine.json：增加直线/抛物线、弧高、视觉倍率、实例染色和爆炸半径配置。
- PlayerRangedAttackComponent：按职业选择真实投射物 Prefab，优先使用模型 shootPoint，维护投射物池并播放可回收爆炸表现。
- PlayerBasicAttackProjectile：处理直线或抛物线飞行、碰撞、视觉重置、单体命中或范围爆炸。
- PlayerBasicAttackDamageResolver：新增按 FighterInterface 去重的范围普通攻击入口。
- triggerProjectile：禁用状态收到同名 Animation Event 时立即退出，阻止第二套演示投射物。
- PlayerRuntime.prefab、RangedCharacterAnimatorControllerSetupTool：固化火球、爆炸和箭矢 Prefab 引用。
- RangedCharacterPlayableTests：覆盖职业配置、资源引用、旧事件保护、轨迹、范围去重、单体命中和对象池回收。

### 3. 调用流程

公共释放：左键 -> PlayerCombatComponent -> Attack 动画 -> shoot Animation Event -> PlayerAnimationEventRelay -> TryReleaseRangedBasicAttack -> PlayerRangedAttackComponent.Fire

法师：Fire -> MagicMissile 对象池 -> Arc 轨迹 -> 敌人/墙体/终点 -> Explode -> OverlapSphereNonAlloc -> FighterInterface 去重 -> 公共伤害结算 -> 紫色爆炸 -> 回池

弓箭手：Fire -> Human Bolt 对象池 -> PlayerRuntime.forward 直线 -> 第一个 FighterInterface -> 公共伤害结算 -> 回池

重复保护：同名 shoot 事件 -> 禁用的 triggerProjectile.shoot -> isActiveAndEnabled 检查失败 -> 不生成 Human Pack 演示副本

### 4. 核心原理

重复投射物不是一次公共攻击执行了两次，而是动画事件按方法名广播时，同时找到了 Human Pack 的 triggerProjectile.shoot 和项目的 PlayerAnimationEventRelay.shoot。只设置组件 enabled=false 不足以表达业务保护，因此旧方法自身也检查 isActiveAndEnabled 与必要引用；公共 Relay 成为唯一正式发射入口。

对象池不再创建临时 Sphere，而是分别实例化 MagicMissile 1 与 Human Bolt。Prefab 原始缩放会在首次入池时缓存，法师的 0.7 倍是在原始 0.8 缩放上相乘，所以实际根缩放为 0.56；弓箭保持资源原尺寸。紫色通过 MaterialPropertyBlock、粒子颜色、Trail 和 Light 写到运行时实例，不会修改共享材质，因此技能火球仍保留原颜色和尺寸。

法师轨迹由“前向匀速距离 + sin(π × 进度) × 弧高”组成：起点和终点高度不变，中点达到 3 米最高点。爆炸使用 OverlapSphereNonAlloc 复用固定数组，并用 HashSet<FighterInterface> 去重，解决一个敌人有多个 Collider 时重复扣血的问题。每个唯一目标仍调用原公共 Apply，所以暴击、飘字和吸血规则没有分叉。

弓箭手不做范围扫描，碰到第一个有效目标后立即回池；普通区域 Trigger 被忽略，实体墙体会结束飞行。回收时统一停止粒子、清空 Trail、清零 Rigidbody、清除拥有者、计时与回调，避免对象池状态残留。

### 5. Unity 测试方式

1. 打开 MainScene，分别选择法师和弓箭手进入游戏。
2. 法师面向空地点击左键：只应出现一个较小紫色火球，轨迹明显向上拱起，到终点也会爆炸。
3. 法师对准聚集敌人攻击：火球碰到敌人或墙体立即爆炸，1.5 米内每个敌人各受伤一次，同一敌人的多个碰撞体不会重复扣血。
4. 弓箭手点击左键：只应出现一个原材质箭矢/木棍，沿人物正前方直线飞行，命中第一个敌人后消失并造成一次伤害。
5. 连续攻击，确认没有额外紫色/黄色基础球，也没有无伤害的第二套抛物线投射物；观察伤害飘字、暴击和吸血仍正常。
6. 按数字键释放法师原技能火球，确认技能火球仍是原尺寸与原颜色，不受普攻紫色染色影响。
7. 打开 Window > General > Test Runner，运行 RangedCharacterPlayableTests，再运行全部 EditMode 测试。

### 6. 面试表达

这个问题来自一个动画事件被两套同名接收器处理：第三方模型脚本生成演示投射物，项目 Relay 又生成正式伤害球。我把第三方接收器做了禁用状态保护，让公共战斗链成为唯一入口。远程攻击组件改为按职业从对象池取真实 Prefab，法师用配置驱动的 sin 抛物线和 1.5 米范围结算，弓箭手用直线单体箭矢。范围伤害通过非分配物理查询和 FighterInterface 集合去重，但每个目标仍进入同一个普通攻击 Resolver，所以攻击力、暴击、飘字和吸血完全复用。紫色只通过实例级 MaterialPropertyBlock 和粒子参数实现，没有修改技能火球共享材质。

### 7. 面试追问

1. **为什么禁用组件后还要在 shoot 方法里判断？** Animation Event 是按方法名寻找接收器，禁用行为不足以作为可靠业务边界；方法入口保护可以从源头阻止演示脚本实例化。
2. **为什么用 sin 曲线而不是 Rigidbody 重力？** 这类美术轨迹需要固定时间、距离和最高点，参数曲线更可控，也不受场景重力配置影响；如果需要真实弹道再改为初速度加重力。
3. **范围伤害如何避免多 Collider 重复扣血？** 物理查询得到 Collider 后向上寻找 FighterInterface，并放进 HashSet；只有第一次加入成功的目标才结算。
4. **为什么紫色不会污染技能火球？** 没有修改 sharedMaterial，只给普通攻击实例写 MaterialPropertyBlock、粒子、Trail 和 Light 参数。
5. **对象池回收需要重置什么？** Rigidbody 速度、碰撞参数、飞行进度、拥有者、回调、粒子和 Trail 都要重置，否则下一发可能继承旧方向、残影或重复回调。

### 8. 本次涉及知识点

- Animation Event 的方法名分发与重复接收保护
- Prefab 驱动的职业投射物工厂
- 正弦抛物线参数化轨迹
- Rigidbody Kinematic、Trigger 与 FixedUpdate
- OverlapSphereNonAlloc 与 HashSet 多 Collider 去重
- 公共伤害 Resolver、暴击、飘字和吸血复用
- MaterialPropertyBlock、ParticleSystem、TrailRenderer 与 Light 实例染色
- 对象池预热、动态扩容、幂等回收和状态重置
- SerializedObject、PrefabUtility 与 EditMode 回归测试

## 功能名称：出生点小地图渲染性能优化

### 1. 实现目标

解决玩家停留在 MainScene 出生点时帧率持续偏低的问题。出生点附近场景实例密集，而小地图相机原本会以主画面帧率再次完整渲染这片区域，因此本次把小地图3D背景改为固定10 FPS刷新，并缩小渲染范围和关闭不必要的图形选项。玩家、怪物和宝箱图标仍由UI系统每帧更新，不会因为背景降频而失去操作反馈。

### 2. 涉及脚本

- `MiniMapCameraController.cs`：关闭 Camera 自动渲染，在 `LateUpdate` 更新跟随位置后按10 FPS手动调用 `Camera.Render`。
- `MainScene.unity`：小地图相机只渲染 Default 与 Water，Far Clip 调整为80，关闭 HDR、MSAA，并移除多余的 AudioListener。
- `MiniMapRT.renderTexture`：保留512×512分辨率，关闭2x MSAA。
- `MiniMapPerformanceConfigurationTests.cs`：验证小地图相机、RenderTexture和 AudioListener 的性能配置没有被误改。

### 3. 调用流程

玩家运行时生成 -> GameplayRuntime 发布 CurrentPlayerChanged -> MiniMapCameraController.SetTarget -> LateUpdate 跟随玩家 -> 到达0.1秒刷新间隔 -> Camera.Render 更新 MiniMapRT -> RawImage 显示3D背景

图标流程保持独立：MiniMapIconTarget 注册 -> MiniMapIconRenderer.Update -> 每帧换算UI坐标 -> 玩家、怪物和宝箱图标流畅移动

### 4. 核心原理

主相机和小地图相机是两次独立的场景渲染。即使小地图只占屏幕右上角，只要它的 Camera 每帧启用，Unity 仍要再次做物体剔除、提交 Draw Call，并把画面写入 RenderTexture。出生点附近模型越密集，这次重复渲染的成本越明显。

本次没有降低图标刷新率，只降低不需要60 FPS的3D背景刷新率。Camera 在场景和运行时都保持 disabled，控制器每0.1秒主动调用一次 `Render`；切换玩家时通过 `forceRender` 立即刷新，避免显示旧位置。使用 `Time.unscaledTime` 是为了让暂停或升级界面改变 `timeScale` 时，刷新计时仍保持稳定。

Layer Mask 只保留 Default 和 Water，因为地形与环境负责构成地图背景；Player、Enemy、Box 等动态目标已经有独立UI图标，再渲染一遍3D模型既不清晰也浪费性能。Far Clip、HDR和MSAA的调整进一步减少小地图的像素与剔除成本。

### 5. Unity 测试方式

1. 打开 MainScene，进入游戏后在出生点静止30秒，通过 Profiler 记录 CPU/GPU Frame Time、Batches、SetPass 和 Camera.Render。
2. 离开出生点后再记录30秒，比较两个位置的平均帧时间。
3. 在 Hierarchy 临时关闭 MiniMapCamera 对象做A/B对照；关闭前后差距应比优化前明显缩小。
4. 确认右上角小地图背景正常跟随，玩家、怪物和宝箱图标仍然流畅；按M放大和关闭地图功能正常。
5. Console 不应再出现“场景中存在多个 AudioListener”的警告。
6. 在 Test Runner 运行 `MiniMapPerformanceConfigurationTests`，再运行全部 EditMode 测试。
7. 最终使用 Development Build + Autoconnect Profiler 验证，小地图背景每秒最多渲染约10次。

### 6. 面试表达

我通过 Profiler 和场景配置排查发现，出生点附近模型比较密集，而小地图使用第二台 Camera 每帧把同一区域完整渲染到 RenderTexture，所以主画面和小地图产生了重复的剔除与 Draw Call。我把小地图 Camera 改成关闭自动渲染，由控制器按10 FPS手动刷新背景，同时保留UI图标每帧更新；另外限制了小地图的 Layer 和 Far Clip，并关闭 HDR、MSAA。这样把表现层按更新频率拆开，既保留了小地图可读性，也降低了出生点的持续渲染成本。

### 7. 面试追问

1. **为什么只降低背景刷新率，图标仍每帧更新？** 背景是静态环境，低刷新率不影响信息读取；图标代表动态目标，需要及时反馈位置，两者更新频率应该按职责区分。
2. **为什么禁用 Camera 后还能显示小地图？** `Camera.enabled=false` 只关闭自动逐帧渲染，代码仍可以按需要调用 `Camera.Render()`，结果会继续写入目标 RenderTexture。
3. **为什么使用 unscaledTime？** 暂停、升级或弹窗可能把 `timeScale` 设为0，unscaledTime 不受影响，可以避免刷新计时停住或恢复后异常。
4. **Layer Mask 为什么能优化？** Camera 在剔除阶段就排除不需要的层，不会继续为这些对象提交渲染；动态目标已经通过UI图标表达，所以无需重复画模型。
5. **如果优化后出生点仍然慢怎么办？** 再用 Frame Debugger 判断是否仍是主相机 Draw Call 过高，然后分阶段做静态标记、Occlusion Culling烘焙、GPU Instancing和LOD，不直接大改整个场景。

### 8. 本次涉及知识点

- Camera 自动渲染与手动 `Camera.Render`
- RenderTexture、MSAA与HDR成本
- Camera Culling Mask和 Far Clip Plane
- `LateUpdate` 相机跟随
- `Time.unscaledTime` 与固定刷新频率
- Profiler、Frame Debugger和 Development Build
- 3D背景与UI图标的更新频率解耦
- EditMode场景配置回归测试

## 功能名称：弓箭手左键攻击与箭矢发射修复

### 1. 实现目标

修复弓箭手移动和技能正常，但左键没有明显攻击动作、也看不到箭矢的问题。普通攻击现在会先登记释放任务，再播放动画；即使 Animation Event 丢失，仍会在配置释放帧发射一支 Human Bolt。

### 2. 涉及脚本

- `PlayerCombatComponent`：从真实左键入口创建攻击序号，提前注册释放计时，并让动画事件与计时兜底安全去重。
- `PlayerPresentationComponent`：恢复 Attack Layer 权重并从起点直接播放 Attack 状态，结束后回到 Empty。
- `PlayerRangedAttackComponent`：检查武器口是否被墙体占用，必要时改用 PlayerRuntime 的安全出生点。
- `PlayerBasicAttackProjectile`：对象池取出时恢复 Renderer、Trail、Collider 和 Rigidbody 状态。
- `RangedCharacterPlayableTests`：按实际生成顺序绑定弓箭手，并从模拟左键输入验证动画、延迟发射、伤害和回池。

### 3. 调用流程

InputCo.LeftMouseDown -> PlayerCombatComponent.CheckAttackInput -> 创建攻击序号并登记 0.3 秒释放任务 -> PlayerPresentationComponent 播放 Attack Layer.Attack -> Animation Event 或代码计时 -> TryReleaseRangedBasicAttack -> PlayerRangedAttackComponent.Fire -> Human Bolt 直线飞行 -> DamageResolver -> 回对象池

### 4. 核心原理

动画属于表现，箭矢和伤害属于玩法逻辑。之前的测试直接调用私有攻击方法和发射方法，绕过了真实输入与计时，无法发现运行时入口的问题。本次让攻击逻辑先建立释放任务，再请求 Animator 播放；因此动画资源异常不会让玩法攻击一起失效。

每次攻击都会得到递增序号。动画 `shoot` 事件和代码计时可能在同一帧到达，但只有尚未消费的当前序号能成功取出投射物，所以既保留精准动画帧，又不会重复发射。如果第三方动画在释放帧之前误发 `ResetCombo`，战斗组件会暂时保留当前攻击和释放任务，等箭矢成功发射后再按攻击超时正常收尾。箭矢回池时停止 Trail，下一次取出再重新启用并清空残影，避免对象池状态残留。

### 5. Unity 测试方式

1. 从角色选择界面选择弓箭手并进入 `MainScene`。
2. 面向空地单击左键，确认立即播放弩射击动作，约 0.3 秒后只出现一支箭。
3. 靠近墙体射击，箭矢不应在弩口生成后立刻消失。
4. 对准敌人连续单击，确认每次一箭、直线飞行、命中一次伤害并正常显示飘字。
5. 在 Test Runner 运行 `RangedCharacterPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

弓箭手的问题不是输入系统整体失效，因为法师和弓箭手移动、技能都正常。我继续检查后发现原回归测试绕过了真实左键和逐帧释放过程，所以我把普通攻击改成先创建攻击序号和释放任务，再播放 Animator。动画事件和代码兜底共用同一个发射入口，用攻击序号保证一次攻击只生成一支箭。箭矢继续走对象池，取出时恢复渲染、Trail、碰撞和物理状态，同时检查武器口是否卡在实体里。这样表现层出问题时玩法仍有兜底，而且测试真正覆盖了玩家操作链路。

### 7. 面试追问

1. **为什么不能只依赖 Animation Event？** 第三方动作可能丢事件或状态没有进入，玩法攻击会随表现一起失效，因此需要代码计时兜底。
2. **动画事件和计时同时触发会怎样？** 两者携带同一个攻击序号，第一次成功发射后序号被标记为已消费，第二次调用直接返回。
3. **为什么先登记释放任务再播放动画？** Animator 调用可能因状态或资源异常失败，先登记能保证玩法逻辑不会被表现异常中断。
4. **为什么要重置 TrailRenderer？** 池对象会保留上一发的轨迹数据，不清除就可能在下一次启用时出现跨屏残影或不可见状态。
5. **武器口在墙里怎么处理？** 发射瞬间用非分配范围查询检查实体重叠，排除玩家自身后，必要时改用玩家前上方的通用出生点。

### 8. 本次涉及知识点

- 输入接口与可测试性
- Animator Layer、AvatarMask 与 `Animator.Play`
- Animation Event 与代码计时兜底
- 攻击序号和幂等去重
- `OverlapSphereNonAlloc` 出生点检测
- Rigidbody、Collider、Renderer 与 TrailRenderer 状态重置
- 对象池生命周期与真实操作链回归测试

## 功能名称：登录与选角界面鼠标状态修复

### 1. 实现目标

解决登录界面或角色选择界面进入运行模式后，点击 Game 视图时鼠标偶尔被锁定并隐藏、导致 UI 无法正常操作的问题。玩法场景仍然可以按原逻辑锁定鼠标，本次只在纯 UI 界面启用时恢复适合菜单操作的鼠标状态。

### 2. 涉及脚本

- `UiCursorStateUtility.cs`：统一把全局鼠标切换为可见、未锁定的 UI 模式。
- `LoginPanelController.cs`：登录面板启用时恢复 UI 鼠标状态。
- `CharacterSelectPanelController.cs`：选角面板启用时恢复 UI 鼠标状态。
- `UiCursorStateTests.cs`：验证公共入口会设置 `CursorLockMode.None` 和 `Cursor.visible = true`。

### 3. 调用流程

玩法场景 `CameraCo` 锁定并隐藏鼠标 -> 加载登录或选角场景 -> 面板对象执行 `OnEnable` -> `UiCursorStateUtility.EnsureVisibleAndUnlocked` -> 解除鼠标锁定并显示系统鼠标 -> 玩家正常点击 UI。

### 4. 核心原理

`Cursor.lockState` 和 `Cursor.visible` 是 Unity 的全局鼠标状态，不会因为进入了另一个场景就自动恢复。玩法相机会把鼠标设为 `Locked` 并隐藏；当 Game 视图重新获得焦点时，Unity 会真正应用这项锁定，所以问题看起来像是“点击场景后鼠标才突然消失”。停止运行时，编辑器会释放鼠标捕获，因此再次运行偶尔又表现正常。

登录和选角控制器在 `OnEnable` 中主动声明自己需要 UI 鼠标模式。选择 `OnEnable` 而不是只放在 `Awake`，是因为同一个面板关闭后再次打开时也需要重新校正鼠标状态。公共工具只修改可见性和锁定模式，不移动鼠标位置，也不改动玩法相机的控制逻辑。

### 5. Unity 测试方式

1. 打开 `LoginScene`，连续进入和退出 Play Mode 三次；每次点击 Game 视图后鼠标都应保持可见，并能点击登录与注册按钮。
2. 登录进入角色选择场景，确认鼠标可见且存档槽位、创建角色、返回登录按钮都能正常点击。
3. 进入 `MainScene`，确认玩法相机仍会按原逻辑锁定并隐藏鼠标，不影响角色视角操作。
4. 从玩法流程返回登录或选角界面，确认鼠标会自动重新显示并解除锁定。
5. 在 Test Runner 的 EditMode 中运行 `UiCursorStateTests`，再运行全部 EditMode 测试。

### 6. 面试表达

我排查到这个问题不是登录 UI 自己隐藏了鼠标，而是 Unity 的 Cursor 状态属于全局状态。玩法相机进入时会锁定并隐藏鼠标，切换到登录或选角场景后这个状态不会自动按场景恢复，Game 视图获得焦点时就会再次应用锁定。我的处理方式是抽出一个 UI 鼠标状态工具，在登录和选角面板的 `OnEnable` 中明确设置为未锁定和可见。这样玩法与 UI 场景分别声明自己需要的鼠标模式，修改范围小，也方便以后新增暂停菜单或主菜单时复用。

### 7. 面试追问

1. **为什么问题在点击 Game 视图后才明显？** Game 视图获得焦点后，编辑器才会捕获并应用 `CursorLockMode.Locked`，所以焦点变化让残留状态暴露出来。
2. **为什么不用停止运行来解决？** 停止运行只是编辑器临时释放鼠标捕获，不能保证构建后的场景切换流程，业务代码必须主动管理状态。
3. **为什么使用 `OnEnable` 而不是 `Start`？** `Start` 每个组件生命周期只执行一次，面板再次启用时不会重跑；`OnEnable` 每次启用都会校正状态。
4. **为什么抽成公共工具？** 登录和选角都需要同一组设置，统一入口可以避免漏改其中一个字段，也方便其他 UI 场景复用。
5. **会不会影响玩法场景锁定鼠标？** 不会。工具只由 UI 面板启用时调用，进入玩法场景后原有相机控制脚本仍会重新设置 `Locked` 和隐藏状态。

### 8. 本次涉及知识点

- `Cursor.lockState` 与 `Cursor.visible`
- Unity 全局状态与场景生命周期
- `Awake`、`Start`、`OnEnable` 的执行差异
- Game 视图焦点和编辑器鼠标捕获
- 公共工具类与重复逻辑收敛
- EditMode 状态测试与手动场景回归

## 功能名称：四职业跳跃高度恢复

### 1. 实现目标

将刺客、战士、法师和弓箭手的公共跳跃高度从 2 米恢复为项目最初刺客使用的 1 米，解决四个职业起跳过高、滞空感过强的问题。

### 2. 涉及脚本

- `PlayerMovementComponent.cs`：把新建移动组件时的默认 `jumpHeight` 恢复为 1。
- `PlayerRuntime.prefab`：把四职业实际运行时共用的序列化 `jumpHeight` 恢复为 1。
- `WarriorPlayableTests.cs`：更新公共跳跃高度回归断言，防止以后再次意外改成 2。

### 3. 调用流程

空格输入 -> PlayerMovementComponent.UpdateJumpTimers -> TryJump -> 根据 jumpHeight 与 gravity 计算初速度 -> CharacterController 执行竖直移动 -> PlayerPresentationComponent 播放跳跃表现

### 4. 核心原理

四个职业只替换 PlayerRuntime 下的视觉模型，移动、重力和跳跃逻辑都来自同一个 `PlayerMovementComponent`。跳跃初速度使用 `Mathf.Sqrt(jumpHeight * -2f * gravity)` 计算，因此把公共高度从 2 恢复为 1 后，四个职业会一起恢复到最初刺客的跳跃手感，不需要分别修改职业 Prefab。

代码默认值与 Prefab 序列化值同时修改，是因为 Unity 实际运行优先读取 Prefab 保存的值，而新建组件时使用代码默认值。保持两处一致可以避免场景实例正常、以后新建 Prefab 却再次出现两米跳高。

### 5. Unity 测试方式

1. 分别选择刺客、战士、法师和弓箭手进入 `MainScene`。
2. 在平地按空格，确认四个职业的跳跃高度都恢复为最初刺客的高度。
3. 检查跳跃动画、落地判断、土狼时间、输入缓冲和体力消耗仍然正常。
4. 在 Test Runner 的 EditMode 中运行 `WarriorPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

这个项目的四个职业共用一个 PlayerRuntime，职业 Prefab 只负责模型和动画，所以跳跃过高不是四份配置的问题，而是公共 `PlayerMovementComponent` 的高度从 1 改成了 2。我把运行时 Prefab 和脚本默认值都恢复为 1，并更新自动化测试锁定这个数值。跳跃初速度继续根据目标高度和重力计算，没有修改输入缓冲、土狼时间或体力系统，因此修改范围小，也不会破坏其他移动手感。

### 7. 面试追问

1. **为什么四个职业只需要改一个地方？** 四个职业共用 PlayerRuntime 的移动组件，职业模型不保存独立跳跃逻辑。
2. **为什么代码和 Prefab 都要改？** Prefab 实例使用序列化值，新建组件使用代码默认值，两处一致可以避免配置漂移。
3. **跳跃高度如何转换成初速度？** 根据竖直匀减速运动公式，使用 `sqrt(height * -2 * gravity)` 计算向上速度。
4. **为什么不修改重力来降低跳跃？** 重力还影响下落速度和整体滞空手感，需求只是恢复原高度，直接修改高度更准确。
5. **如何防止以后误改？** EditMode 测试读取 PlayerRuntime 的序列化字段，并断言它必须等于 1。

### 8. 本次涉及知识点

- Prefab 序列化值与 C# 字段默认值
- 四职业公共运行时组件复用
- 竖直运动与跳跃初速度计算
- CharacterController 移动
- 输入缓冲与土狼时间
- EditMode Prefab 配置测试

## 功能名称：弓箭手近距离箭矢可见性修复

### 1. 实现目标

修复弓箭手贴近怪物攻击时只有音效、看不到箭矢的问题。近距离箭矢仍在命中帧立即结算伤害，但会冻结在命中点显示 0.1 秒后再回到对象池；法师火球流程保持不变。

### 2. 涉及脚本

- `PlayerRangedAttackComponent.cs`：区分攻击目标与实体墙体，只有墙体阻挡真实弩口时才改用安全出生点，并向弓箭传入 0.1 秒命中停留参数。
- `PlayerBasicAttackProjectile.cs`：过滤攻击范围类 Trigger，负责箭矢命中冻结、延迟回池以及对象池复用时的状态恢复。
- `RangedCharacterAnimatorControllerSetupTool.cs`：重新生成远程职业资源时写回命中停留参数，防止 Prefab 配置丢失。
- `PlayerRuntime.prefab`：保存弓箭手命中停留时间配置。
- `RangedCharacterPlayableTests.cs`：覆盖怪物发射前已贴近弩口、前置 Trigger 过滤、0.1 秒可见和对象池复用。

### 3. 调用流程

左键攻击 -> `PlayerCombatComponent` 到达释放时机 -> `PlayerRangedAttackComponent.Fire` -> 从 Human Bolt 对象池取箭 -> 从真实 `shootPoint` 发射 -> `PlayerBasicAttackProjectile` 初始重叠或逐帧扫掠 -> `PlayerBasicAttackDamageResolver` 立即结算伤害 -> 箭身在命中点停留 0.1 秒 -> 清理 Trail 并回池。

### 4. 核心原理

旧逻辑在 `Launch` 当帧发现弩口和怪物身体重叠后，会立即伤害并关闭箭矢。因为渲染帧还没有发生，玩家只能听见发射音效，看不到任何箭身。现在伤害结算和视觉回收被拆成两个时间点：伤害仍立即发生，箭矢则关闭碰撞并冻结 0.1 秒，保证至少有一段可见时间。

出生安全检查只应该处理墙体，不能把可以受伤的怪物当作墙；否则箭可能被推入目标内部甚至目标身后。弓箭还会忽略怪物攻击范围、`shootPos` 等 Trigger，只由正式非 Trigger 身体碰撞体触发伤害。真正回池时统一清理 Trail，下一次取出时重新启用碰撞体、Renderer 和 Trail，避免对象池状态残留。

### 5. Unity 测试方式

1. 打开 `MainScene`，选择弓箭手进入游戏。
2. 分别在贴脸、近距离和正常距离对 `Slime1`、`Slime2` 与 Boss 左键攻击。
3. 确认每次音效对应一支可见箭；贴脸命中时怪物立即扣血，箭在命中点短暂停留后消失。
4. 连续攻击同一怪物，确认每支箭只造成一次伤害，后续箭的 Mesh 和 Trail 仍正常显示。
5. 面向墙体射击，确认墙体仍会阻挡箭；法师普通火球仍按原逻辑爆炸并立即回池。
6. 在 Test Runner 的 EditMode 中运行 `RangedCharacterPlayableTests`，然后运行全部 EditMode 测试。

### 6. 面试表达

弓箭手贴脸攻击只有音效，是因为箭矢在发射当帧就与怪物重叠，伤害结算后马上回池，渲染系统还没机会显示它。我把逻辑命中和视觉回收拆开：命中时立即扣血并关闭碰撞，箭身冻结在命中点 0.1 秒后再回池。同时出生点检测不再把怪物当墙，箭矢也会忽略怪物的攻击范围 Trigger，只命中正式身体 Collider。对象池复用时统一恢复 Collider、Renderer 和 Trail，既解决了可见性，也避免重复伤害和状态残留。

### 7. 面试追问

1. **为什么不把伤害也延迟 0.1 秒？** 操作反馈应及时，延迟的只是视觉回收，不改变原有攻击手感和战斗结算时机。
2. **怎样避免停留期间重复扣血？** 进入停留状态后立即关闭箭矢 Collider，并让 Trigger、扫掠和初始重叠入口都检查同一个停留标记。
3. **为什么弓箭手忽略 Trigger？** 当前怪物都有正式非 Trigger 身体 Collider，而攻击范围和发射点 Trigger 不是受击盒；忽略它们能避免箭在身体前被提前消费。
4. **墙体和怪物在出生检查中怎样区分？** 通过 `FighterInterface` 目标解析区分；能参与正式伤害结算的是目标，其他非 Trigger 实体才视为环境阻挡。
5. **对象池复用要重置哪些状态？** 包括命中停留计时、拥有者和回调、Rigidbody 速度、Collider、Renderer、ParticleSystem 与 Trail，防止上一发状态影响下一发。

### 8. 本次涉及知识点

- Unity 渲染帧与物理帧的执行时机
- 初始重叠、Trigger 与连续碰撞扫掠
- 逻辑结算和视觉表现解耦
- `FighterInterface` 目标识别
- 对象池生命周期与完整状态重置
- TrailRenderer 的 `emitting`、`Clear` 和复用
- 幂等命中入口与重复伤害防护
- Prefab 序列化参数和 Editor 生成工具同步

## 功能名称：三职业动画过渡与弓箭手连续碰撞普攻修复

### 1. 实现目标

为战士、弓箭手和法师的移动、跳跃、攻击、技能与动作层返回增加统一的 0.1 秒过渡，刺客原控制器保持不变。弓箭手普攻周期由 0.75 秒缩短为 0.375 秒，箭矢在约 0.15 秒释放；箭速改为 12m/s、寿命改为 1.25 秒，在保持约 15 米射程的同时提高可见性。箭矢增加逐物理帧球形扫掠和出生点重叠检查，解决高速移动时跨过怪物 Collider、看得到箭却没有伤害的问题。

### 2. 涉及脚本

- `PlayerPresentationComponent.cs`：简单动画职业使用移动参数阻尼、固定时间动作 CrossFade，以及攻击层 0.1 秒淡入淡出。
- `PlayerBasicAttackProjectile.cs`：在每个 `FixedUpdate` 对完整移动路径执行 `SphereCastNonAlloc`，并用 `OverlapSphereNonAlloc` 处理贴脸命中。
- `CharacterDefine.json`：保存弓箭手 0.375 秒攻击周期、12m/s 速度和 1.25 秒寿命。
- `RangedCharacterAnimatorControllerSetupTool.cs`：分别换算弓箭手 Attack 与 Skill 状态速度，普通攻击加速两倍而技能维持 0.75 秒。
- `WarriorAnimatorControllerSetupTool.cs`：固化战士状态机 0.1 秒固定时间过渡。
- `Archer.controller`、`Wizard.controller`、`Warrior.controller`：同步当前项目实际使用的过渡和弓箭手攻击速度。
- `RangedCharacterPlayableTests.cs`、`WarriorPlayableTests.cs`：验证配置、Animator、层权重淡入、薄目标连续碰撞和真实史莱姆伤害链。

### 3. 调用流程

左键输入 -> `PlayerCombatComponent` 创建攻击令牌并启动 0.15 秒释放计时 -> `PlayerPresentationComponent` 用 0.1 秒 CrossFade 播放 Attack、同步淡入 Attack Layer -> `PlayerRangedAttackComponent` 从对象池取得 `Human Bolt` -> `PlayerBasicAttackProjectile.FixedUpdate` 计算下一位置并球形扫掠 -> 选择路径上最近的 `FighterInterface` -> `PlayerBasicAttackDamageResolver` 结算暴击、伤害、飘字和吸血 -> 箭矢回收到对象池。

### 4. 核心原理

只依赖 `OnTriggerEnter` 时，12m/s 的箭矢在默认 0.02 秒物理步长内会移动约 0.24 米；如果怪物碰撞体或接触区域比这段距离薄，箭矢可能从碰撞体一侧直接跳到另一侧，中间没有产生 Trigger 回调。现在每个物理帧不只检查终点，而是用箭矢半径对“当前位置到下一位置”整段路径做球形扫掠，并从结果中选择最近的有效怪物或实体墙体。

Trigger 回调、路径扫掠和出生点重叠都进入同一个命中方法。投射物的 `released` 状态让回收和伤害入口保持幂等，即使同一帧既发生 Trigger 又被扫掠命中，也只会扣一次血、回收一次。查询使用预分配数组和 NonAlloc API，避免快速连射时每个物理帧产生托管内存分配。

动画方面，移动 BlendTree 的 `Speed` 参数使用 0.1 秒阻尼；攻击和技能使用 `CrossFadeInFixedTime`，攻击层权重也按时间渐变。弓箭手 Attack 和 Skill 虽复用同一个动作素材，但生成工具分别计算速度，因此只提升普攻频率，不会意外加速技能动画。

### 5. Unity 测试方式

1. 打开 `MainScene`，分别选择战士、法师和弓箭手，验证待机、走路、奔跑、跳跃、攻击、技能与返回移动不再瞬切；刺客表现应保持原样。
2. 使用弓箭手连续左键，确认约每 0.375 秒可攻击一次，箭在约 0.15 秒出现，飞行速度清晰可见且总射程仍约 15 米。
3. 分别对 `Slime1`、`Slime2` 和 Boss 射击，确认箭命中第一个目标后扣血、显示飘字并回池；贴脸和擦过较薄碰撞区域也应命中。
4. 面向墙体射击，确认箭被实体墙阻挡，不会穿墙伤害后方怪物。
5. 在 Test Runner 的 EditMode 中运行 `RangedCharacterPlayableTests` 与 `WarriorPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

弓箭手原来能看到箭但经常没有伤害，根因是高速投射物只依赖 Trigger。箭在一个物理帧内的位移可能大于怪物的有效碰撞厚度，所以会发生穿透。我保留了对象池和原来的 Human Bolt 表现，在每个 FixedUpdate 用 `SphereCastNonAlloc` 检查完整移动路径，并选择最近的有效目标；贴脸情况再用初始 Overlap 补充。三个碰撞入口共用一个幂等结算方法，避免重复伤害。同时我把弓箭手普攻周期减半，但单独保留技能动画速度，并为三个简单职业加了 0.1 秒动画过渡，让玩法节奏更快、表现也更平滑。

### 7. 面试追问

1. **为什么用 SphereCast 而不是 Raycast？** 箭矢有 0.12 米碰撞半径，SphereCast 能用实际体积扫过路径，边缘命中比中心射线更符合视觉。
2. **为什么查询放在 FixedUpdate？** Rigidbody 移动和物理世界以固定时间步更新，在同一节奏里计算路径更稳定，也便于用速度乘固定步长理解位移。
3. **怎样避免 SphereCast 和 OnTriggerEnter 重复扣血？** 两者调用同一个命中入口，第一次结算后立即把 `released` 设为 true，后续入口会直接返回。
4. **为什么使用 NonAlloc API？** 箭矢可能高频发射，复用查询数组可以避免每物理帧创建新数组，减少 GC 抖动。
5. **为什么弓箭手技能没有一起加速？** Attack 和 Skill 暂时共用素材但承担不同玩法时长，生成控制器时分别计算状态速度，防止配置耦合。

### 8. 本次涉及知识点

- Animator BlendTree 参数阻尼与 `CrossFadeInFixedTime`
- Animator Layer、AvatarMask 与权重渐变
- 攻击前摇、攻击周期和幂等攻击令牌
- `FixedUpdate`、固定物理步长与高速物体穿透
- `SphereCastNonAlloc`、`OverlapSphereNonAlloc` 和最近命中选择
- Trigger、实体障碍与 `FighterInterface` 目标过滤
- 对象池状态重置、回收幂等与 GC 优化
- 配置数据、生成工具和 Controller 资源一致性

## 功能名称：操作说明关闭按钮文字清理

### 1. 实现目标

删除操作说明面板关闭按钮上重复叠加的旧版 `X` 文本，只保留按钮自身的“×”图片，同时保证点击关闭功能不受影响。

### 2. 涉及脚本

- `GameplayStartupGuidePopup.cs`：移除不再需要的关闭按钮文本引用和运行时赋值。
- `GameplayUiRootMigration.cs`：迁移时清理关闭按钮旧文本，不再重新创建和绑定它。
- `GameplayUiRoot.prefab`：删除 `CloseButton` 下的 `Text (Legacy)` 子对象。
- `StartupGuideCloseButtonConfigurationTests.cs`：保护关闭图标、按钮组件和无文本子对象的配置。

### 3. 调用流程

进入 `MainScene` -> `GameplayStartupGuidePopup` 显示操作说明 -> `CloseButton` 使用自身 Image 显示“×”图标 -> 玩家点击按钮 -> `ClosePopup` 隐藏面板并恢复游戏状态。

### 4. 核心原理

关闭按钮本身已经配置了“×”Sprite，旧的 `Text (Legacy)` 又在图片上显示一个 `X`，因此会产生重复或错位。删除文字对象后，按钮的 `Image` 仍负责视觉表现，`Button` 仍负责接收点击，两种职责互不依赖。

运行时代码和编辑器迁移工具也必须一起清理，否则只改 Prefab 后，代码可能继续访问空引用，迁移工具也可能在以后重新生成文字。

### 5. Unity 测试方式

1. 打开 `MainScene` 并进入 Play Mode。
2. 等待操作说明面板出现，确认右上角只显示一个清晰的“×”图片，没有额外文字。
3. 点击关闭按钮，确认面板正常关闭，游戏时间和鼠标状态正常恢复。
4. 在 Test Runner 的 EditMode 中运行 `StartupGuideCloseButtonConfigurationTests`，再运行全部 EditMode 测试。

### 6. 面试表达

这个关闭按钮原本同时使用了“×”图片和一个 Legacy Text 显示 `X`，导致同一个视觉元素重复叠加。我删除了文字子对象，并同步移除运行时序列化引用和迁移工具里的生成逻辑，只保留 Image 负责显示、Button 负责交互。另外增加了 Prefab 配置测试，确保关闭按钮仍有图标和点击组件，同时不再包含文本子对象。

### 7. 面试追问

1. **删除 Text 会影响按钮点击吗？** 不会，点击由父对象的 `Button` 和 `Image` 接收，文字只是子级视觉元素。
2. **为什么不能只把文字内容清空？** 空 Text 仍是冗余组件，也可能被后续代码重新赋值，直接移除并清理引用更彻底。
3. **为什么还要修改迁移工具？** 否则以后重新执行 UI 迁移时，旧文字会被再次创建。
4. **为什么要修改引用校验？** 文本字段被删除后，它不再是操作说明面板正常运行的必要依赖。
5. **测试保护了什么？** 验证按钮、关闭图标仍存在，按钮下没有 Text，并确认弹窗其他序列化引用完整。

### 8. 本次涉及知识点

- Unity `Image`、`Button`、`Text` 的职责区别
- Prefab YAML 序列化引用
- 运行时引用校验
- 编辑器迁移工具
- EditMode Prefab 配置测试

## 功能名称：本地游客模式

### 1. 实现目标

在不启动服务端、也不输入账号密码的情况下，让玩家可以通过登录面板的“游客模式”进入游戏。游客拥有一份保存在当前电脑上的独立档案，继续使用与普通账号相同的四个角色槽，并持久化角色名、职业、等级、经验、待分配属性点、属性强化次数、宝箱数和 Boss 数。

游客退出登录后只清理运行时会话，不删除本地文件；下次启动游戏再次点击游客模式，就能继续读取原来的角色。当前普通账号本身不会保存背包，所以游客模式也保持相同边界，不额外保存背包、血蓝、动画或场景对象。

### 2. 涉及脚本

- `LocalGuestSaveService.cs`：负责游客 JSON 的读取、校验、创建角色、进入角色、成长保存、临时文件替换和备份恢复。
- `GameApiClient.cs`：使用 `GameSessionMode` 区分在线账号与游客账号，并把现有角色接口路由到服务端或本地文件。
- `LoginPanelController.cs`：处理游客按钮状态、结果提示和选角场景跳转。
- `CharacterSelectPanelController.cs`：继续使用统一 API，并把仅描述服务器的命名调整为“当前数据源”。
- `CharacterProgressSaveService.cs`：保留原有自动存档协调逻辑，同时支持在线数据库和本地游客文件。
- `GuestModePersistenceTests.cs`：验证存档往返、覆盖槽位、写盘失败、备份恢复和登录场景按钮绑定。

### 3. 调用流程

登录界面点击游客模式 -> `LoginPanelController.LoginAsGuest` -> `GameApiClient.LoginAsGuest` -> `LocalGuestSaveService.TryLoad` -> `CharacterSelectPanelController` 读取四个角色槽 -> 创建或选择角色 -> `GameApiClient.EnterCharacter` -> `LocalGuestSaveService.TryEnterCharacter` -> `SceneFlowService.StartGameplay` -> 游戏内成长事件 -> `CharacterProgressSaveService` -> `GameApiClient.SaveCharacterProgress` -> `LocalGuestSaveService.TrySaveCharacterProgress` -> 本地 JSON。

在线账号仍然使用同一套上层调用链，只是在 `GameApiClient` 内部改为发送网络协议，因此 UI 和玩法逻辑不需要维护两份实现。

### 4. 核心原理

可以把 `GameApiClient` 理解成一个统一柜台：角色选择界面只向柜台提出“读取角色、创建角色、进入角色、保存角色”的请求，不需要知道资料最终放在哪里。普通账号会由柜台把请求交给服务端，游客账号则交给 `LocalGuestSaveService` 写入电脑本地。

游客存档使用带版本号的 JSON，并通过 `Application.persistentDataPath` 获取各平台合适的用户数据目录。写盘时先生成临时文件，再替换正式文件并保留上一份备份，避免程序在写到一半时留下不完整 JSON。读取主文件失败时会验证备份；只有备份有效才进行恢复，两份都损坏时会提示失败而不是静默创建新档覆盖玩家数据。

创建或保存时先生成候选数据，只有文件写入成功后才替换内存缓存。这样即使磁盘无权限或空间不足，UI 也不会显示成功，内存里也不会出现“这次运行看得到、重启后却丢失”的假存档。

### 5. Unity 测试方式

1. 不启动 `TreasureHunter.Server`，打开 `LoginScene` 并运行。
2. 点击开始按钮打开登录面板，确认登录、注册下方存在蓝色“游客模式”按钮。
3. 点击游客模式，确认没有网络超时并进入 `CharacterSelectScene`。
4. 选择一个槽位创建角色，进入游戏后获得经验、选择属性强化、开启宝箱或击败 Boss。
5. 使用暂停界面的“保存并退出”返回选角，确认槽位摘要已经更新。
6. 停止运行并重新启动，再次点击游客模式，确认角色和进度仍然存在。
7. 返回登录并使用普通账号登录，确认看不到游客角色。
8. 在 Test Runner 的 EditMode 中运行 `GuestModePersistenceTests`、`CharacterProgressPersistenceTests`，然后运行全部 EditMode 测试。

### 6. 面试表达

我在登录系统中增加了一个完全离线的游客模式，但没有复制一套选角和存档 UI。我在 `GameApiClient` 中增加会话模式，让上层仍然调用读取角色、创建角色、进入角色和保存进度这些统一接口；在线账号走服务端，游客账号走本地 JSON。游客档保存在 `Application.persistentDataPath`，数据结构复用现有 `NCharacter`，所以等级、经验、属性强化和 Boss 进度与正式账号保持一致。写盘采用临时文件替换和备份恢复，而且只有写盘成功才更新内存缓存，避免显示成功但实际丢档。这样既降低了 UI 与存储方式的耦合，也方便以后扩展云存档或游客转正式账号。

### 7. 面试追问

1. **为什么不直接在登录按钮脚本里用 PlayerPrefs 保存所有角色？** PlayerPrefs 适合少量设置或简单数值，结构化的四角色成长数据更适合 JSON；独立服务也能把文件 I/O 与 UI 分开测试。
2. **怎样保证游客和正式账号不会串档？** `GameSessionMode` 明确记录当前数据源，切换或退出会话时会清空角色缓存和活动角色；游客文件也不会被普通登录分支读取。
3. **为什么要先写临时文件再更新内存？** 如果先改内存再写盘，写盘失败时玩家本次运行会看到新进度，但重启后数据消失。候选数据成功落盘后再提交，可以保证内存与磁盘一致。
4. **本地 JSON 能防作弊吗？** 不能。游客模式是离线体验，明文 JSON 便于调试和展示；需要防作弊的数据仍应由服务端校验和持久化。
5. **以后怎样做游客转正式账号？** 可以在注册成功后读取游客 JSON，把四个槽位作为迁移请求发送给服务端，由服务端校验冲突和数据合法性，确认成功后再提示是否删除本地游客档。

### 8. 本次涉及知识点

- `Application.persistentDataPath` 与跨平台用户数据目录
- `JsonUtility`、可序列化数据模型和存档版本号
- 临时文件、备份文件与失败恢复
- 候选数据提交，避免内存和磁盘状态不一致
- 门面模式与按会话模式路由数据源
- 在线存档和本地存档共用业务接口
- 深拷贝与缓存隔离
- 协程、回调和 UI 按钮防重复点击
- Unity Scene YAML 序列化引用
- EditMode 文件 I/O 与场景配置测试

## 功能名称：弓箭手怪物聚集区域贴脸箭视觉前飞

### 1. 实现目标

修复弓箭手在怪物聚集区域攻击时，箭矢出生后立即命中并原地停留、看起来像没有射出去的问题。贴脸目标仍在发射帧立即受到一次伤害，但箭矢关闭后续碰撞后继续沿正前方飞行 1.5 米，再回收到对象池。

### 2. 涉及脚本

- `PlayerBasicAttackProjectile.cs`：区分初始重叠和正常飞行命中，增加贴脸命中后的无伤害视觉前飞状态。
- `PlayerRangedAttackComponent.cs`：向弓箭手投射物传入 1.5 米视觉前飞距离。
- `PlayerRuntime.prefab`、`RangedCharacterAnimatorControllerSetupTool.cs`：保存并固化前飞距离配置。
- `RangedCharacterPlayableTests.cs`：验证怪群目标选择、立即伤害、精确前飞距离、禁止二次伤害和对象池状态恢复。

### 3. 调用流程

左键攻击 -> `PlayerCombatComponent` 到达释放时机 -> `PlayerRangedAttackComponent.Fire` -> Human Bolt 从真实弩口生成 -> `TryResolveInitialOverlap` 选择瞄准线上最合适的前方目标 -> `PlayerBasicAttackDamageResolver` 立即结算一次伤害 -> 关闭箭矢 Collider -> `FixedUpdate` 继续视觉前飞 1.5 米 -> 清理 Trail 并回池。

### 4. 核心原理

怪物靠近时，弩口可能已经在怪物的 `CharacterController` 内部。初始重叠检测是伤害正确性的保障，不能简单删除，否则最近的怪物反而不会受伤。本次把“命中逻辑”和“命中后的视觉运动”拆开：伤害立即结算，箭矢随后进入只负责表现的状态，不再执行 Trigger 或球形扫掠，因此不会穿透伤害第二只怪物。

怪物聚集时，物理查询返回顺序不稳定。初始重叠目标会先排除身后目标，再比较目标身体中心到发射射线的横向距离；更接近瞄准线的目标优先，横向距离相同时再选择前向距离较近的目标。视觉前飞按距离累计，每个物理帧最多移动 `speed * fixedDeltaTime`，最后一步截断到剩余距离，因此箭速变化后仍精确飞行 1.5 米。

### 5. Unity 测试方式

1. 打开 `MainScene`，选择弓箭手。
2. 让多个 Slime 靠近或包围玩家，然后持续左键攻击。
3. 确认弩口与怪物重叠时，最近的正前方怪物立即扣血，箭矢仍会带着 Trail 飞出怪群。
4. 确认视觉前飞不会继续伤害其他怪物，一支箭仍只结算一个目标。
5. 在正常距离攻击怪物，确认箭矢仍命中最近目标并保留正常命中停留效果。
6. 面向墙体射击，确认墙体阻挡未失效；再切换法师确认火球轨迹和爆炸未变化。
7. 在 Test Runner 的 EditMode 中运行 `RangedCharacterPlayableTests`，然后运行全部 EditMode 测试。

### 6. 面试表达

弓箭手在怪群中看不到箭，不是对象池没生成，而是弩口已经落在怪物碰撞体里，`Launch` 当帧就完成伤害并进入停留状态。我保留了初始重叠伤害，但把后续视觉单独做成一个状态：目标立即扣血，箭矢关闭 Collider 和扫掠后继续前飞 1.5 米，所以画面上能明确看到射击，同时不会穿透伤害第二个怪物。怪物聚集时还会按瞄准线距离选择前方目标，避免依赖不稳定的物理查询顺序。

### 7. 面试追问

1. **为什么不直接删除初始重叠检测？** SphereCast 不会稳定报告起点内部的 Collider，删除后贴脸怪物可能完全不受伤。
2. **为什么视觉箭不继续进行碰撞？** 伤害已经结算，关闭碰撞可以保证单体箭不会因为视觉补偿变成穿透群攻。
3. **为什么用距离而不是固定时间？** 距离是视觉需求，按距离累计后即使调整箭速，箭仍固定飞出 1.5 米。
4. **怪物聚集时怎样选择目标？** 排除身后目标，优先选择身体中心最接近发射射线的目标，再以较近前向距离作为并列规则。
5. **对象池复用时要重置什么？** 视觉前飞标记、剩余距离、Collider、Rigidbody、Trail、Renderer、拥有者和回调都必须恢复。

### 8. 本次涉及知识点

- 初始重叠与连续碰撞检测的差异
- 逻辑命中和视觉投射物解耦
- 向量点积、射线横向距离与目标排序
- `FixedUpdate` 中按距离推进
- 单体伤害幂等保护
- Collider、TrailRenderer 与对象池状态恢复
- Prefab 参数与 Editor 生成工具同步

## 功能名称：弓箭手普通攻击最终简化——点击即发射与双条件回收

### 1. 实现目标

按最终验收需求简化弓箭手普通攻击：玩家每次按下左键都在当帧从弩口发射一支 Human Bolt，不再等待攻击动画事件或上一段攻击结束。箭矢只在后续飞行中碰到正式怪物身体时造成一次伤害并回池，或者飞满 `1.25s` 寿命后自动回池；出生重叠、地面、墙体、环境物件和 Trigger 都不能再让箭矢生成后秒消失。

本节方案取代上一节的“贴脸立即伤害 + 视觉前飞”规则。最终需求更强调每次点击都必须看见箭飞出去，因此出生点已经覆盖弩口的怪物不会被本支箭命中，箭会先脱离怪群并继续飞行。

### 2. 涉及脚本

- `PlayerCombatComponent.cs`：识别弓箭手普通攻击，每个 `LeftMouseDown` 建立独立攻击令牌并立即调用远程发射。
- `PlayerRangedAttackComponent.cs`：保留 Human Bolt 对象池和真实弩口出生点，移除弓箭手命中停留与贴脸视觉前飞配置。
- `PlayerBasicAttackProjectile.cs`：关闭弓箭手箭矢 Trigger，记录出生重叠目标，并用逐物理帧球形扫掠只检测正式怪物身体。
- `PlayerRuntime.prefab`、`RangedCharacterAnimatorControllerSetupTool.cs`：删除已经不再使用的弓箭停留和视觉前飞序列化参数。
- `RangedCharacterPlayableTests.cs`：验证连续点击立即生成、出生重叠不回收、环境不阻挡、后续怪物命中和寿命回收。

### 3. 调用流程

左键按下 -> `PlayerCombatComponent.CheckAttackInput` -> 创建新的攻击令牌 -> `StartImmediateArcherAttack` -> 同帧调用 `PlayerRangedAttackComponent.Fire` -> 从对象池取得 Human Bolt -> `Launch` 记录出生重叠怪物并关闭 Trigger -> `FixedUpdate` 执行直线移动和 `SphereCastNonAlloc` -> 后续命中正式怪物时由 `PlayerBasicAttackDamageResolver` 结算并回池；没有命中则在 `Update` 检查到 `1.25s` 寿命结束后回池。

### 4. 核心原理

这次把弓箭手的输入、命中和回收规则收紧成三条清晰规则。第一，输入层只认离散的左键按下，每次按下都创建新令牌并立即发射，所以攻击动画慢、`shoot` 事件丢失或上一支箭仍在飞行，都不会吞掉这次点击。动画事件仍可调用统一释放入口，但同一个令牌已经发射后会被幂等检查拦住，不会重复生成第二支箭。

第二，Human Bolt 的根 Trigger Collider 在发射时关闭。箭矢不再依赖第三方 Prefab 的 Trigger 回调，而是在每个 `FixedUpdate` 对完整移动线段执行球形扫掠。筛选条件只接受非 Trigger 且能找到 `FighterInterface` 的碰撞体，因此地面、墙体、攻击范围 Trigger 和 `shootPos` 都不会回收箭矢。

第三，发射时用一次 `OverlapSphereNonAlloc` 记录已经覆盖弩口的正式怪物身体，但不结算也不回收。这些 Collider 在该支箭整个生命周期内都被忽略，避免怪物聚集或弩口落在怪物体内时箭矢同帧消失。对象回池时清空忽略集合、拥有者、计时和特效状态，保证下一次复用不会继承旧数据。法师火球仍使用原来的弧线、初始重叠、环境碰撞与范围爆炸流程。

### 5. Unity 测试方式

1. 打开 `MainScene`，在选角后进入弓箭手。
2. 在完全空旷的位置快速单击和连续单击左键，确认每次点击都立即出现一支 Human Bolt，多支箭可以同时飞行。
3. 让多个 Slime 或 Boss 靠近并包围弩口，再快速点击，确认箭矢不会在 Hierarchy 中只存在一帧或 `0.1s` 就消失，而是能飞离怪群。
4. 在正常距离放置怪物，确认箭矢碰到正式身体后立即消失，怪物只扣一次血。
5. 面向地面、墙体和怪物攻击范围 Trigger 射击，确认箭不会因此消失；当前最终规则允许箭穿墙并命中墙后的怪物。
6. 在空旷处射击，确认约 `1.25s` 后箭矢自动回池。
7. 切换法师，确认紫色弧线火球和范围爆炸没有改变。
8. 在 Test Runner 的 EditMode 中运行 `RangedCharacterPlayableTests`，再运行全部 EditMode 测试。

### 6. 面试表达

弓箭手偶发看不到箭的根因不是没有创建，而是箭出生时弩口已经处在怪物或复杂 Trigger 中，旧逻辑会在生成帧立刻命中并回池。我把弓箭手普攻简化成点击即发射，并用攻击令牌保证动画事件和代码入口不会重复生成。碰撞方面关闭 Prefab 自带 Trigger，改为每个物理帧对运动路径做球形扫掠，只接受正式怪物身体；出生时已经重叠的目标会在本支箭生命周期内忽略，地面和墙也不会回收箭。最终只有后续命中怪物或寿命结束两种回收条件，对象池回收时再统一重置状态，所以发射表现稳定，也保留了高速投射物防穿透能力。

### 7. 面试追问

1. **为什么每次点击还要使用攻击令牌？** 动画事件和代码都可能请求发射，令牌能保证同一次点击只成功生成一支箭，同时允许下一次点击立即创建新的箭。
2. **为什么不用普通 `OnTriggerEnter`？** 第三方箭 Prefab 周围可能有复杂 Trigger，而且高速箭一帧能跨过薄碰撞体；对完整位移做球形扫掠更稳定。
3. **为什么出生重叠目标要忽略整个生命周期？** 如果只忽略一帧，下一帧箭仍可能位于同一个大 Collider 内并立即回池。生命周期级忽略能保证箭真正飞离弩口。
4. **这样会有什么玩法取舍？** 箭会穿过墙体，且与弩口重叠的贴脸怪物不会被该支箭伤害。这是为了严格满足“只有后续碰怪或超时消失、每次点击都看得到发射”的最终验收规则。
5. **对象池复用要重置哪些状态？** 要清空出生重叠集合、飞行计时、拥有者、伤害回调、Rigidbody、Renderer、粒子和 Trail，避免上一支箭影响下一支。

### 8. 本次涉及知识点

- 输入边沿 `LeftMouseDown` 与持续按住的区别
- 攻击令牌和幂等释放
- `FixedUpdate`、`SphereCastNonAlloc` 与高速投射物连续检测
- Collider、Trigger 与 `FighterInterface` 目标筛选
- 出生重叠记录与 `HashSet<Collider>` 生命周期管理
- 对象池获取、回收和状态重置
- 逻辑伤害与动画表现解耦
- 明确玩法规则后的技术取舍

## 功能名称：项目 README 重写与求职简历包装

### 1. 实现目标

根据当前仓库的真实代码、场景、配置、服务端和测试情况，重写已经过时的项目 README。新版文档要让第一次打开仓库的人快速理解游戏玩法、完成度、核心架构、运行方法和项目边界，同时把能体现 Unity 客户端能力的内容提炼成简历和面试语言。

本次只修改项目文档，不改动客户端脚本、服务端脚本、Prefab、场景或配置资源。

### 2. 涉及脚本

- `README.md`：更新项目定位、快速体验、操作方式、系统介绍、架构、调用链、联网配置、测试、已知不足和后续计划。
- `ProjectLearningNotes.md`：记录项目包装时如何从真实代码中提炼求职亮点。
- `Assets/Script`：只读扫描客户端的 Architecture、Player、Combat、Skills、Enemies、Boss、Inventory、UI、Network 和 Services 模块。
- `TreasureHunter.Server`：只读扫描 TCP/Protobuf、Session、用户业务、BCrypt 和 SQL Server 存档逻辑。
- `Assets/Editor/Tests`：只读核对现有测试覆盖与当前配置之间的一致性。

### 3. 调用流程

项目结构与代码扫描 -> 核对构建场景、职业和配置数据 -> 抽取客户端与服务端核心调用链 -> 区分已实现功能、第一阶段能力和后续规划 -> 重写 README -> 提炼简历项目描述与面试表达。

README 中重点展示的完整玩法链路为：

`登录/游客模式 -> 四槽位选角 -> LoadingScene -> MainScene -> 战斗成长 -> 击破 5 次金库 -> BossRoomScene -> Spider King -> 返回主场景继续周回`。

### 4. 核心原理

求职项目文档不能只罗列脚本名，也不能把未来计划写成已经完成。写 README 时需要从代码中寻找证据：场景是否加入 Build Settings、Prefab 是否存在、配置表有多少职业和技能、System 是否真正被架构注册、存档字段是否真的经过协议和数据库、测试是“存在”还是“已经运行通过”。

简历内容应优先描述自己负责的技术问题、采取的设计和得到的结果。例如“写了对象池”信息量较低，而“将怪物、投射物、技能特效和掉落物接入对象池，并在回收时重置事件、协程、Collider、Trail 和业务状态，降低频繁创建销毁及状态残留风险”更能体现客户端经验。

项目存在历史兼容代码并不是必须隐藏的问题。更合适的表达是：在保持原型可运行的前提下，将玩家逻辑逐步迁移到 QFramework 与组件化 PlayerRuntime，并说明当前仍有遗留模块待收敛。这比直接声称“架构已经完全解耦”更真实，也更容易回答面试追问。

### 5. Unity 测试方式

1. 在 Markdown 预览中检查 README 的标题、表格、代码块和 Mermaid 流程图。
2. 核对 README 中列出的 5 个场景均已加入 Build Settings。
3. 从 `LoginScene` 运行，使用游客模式创建或选择角色，确认经过 `LoadingScene` 进入 `MainScene`。
4. 分别检查四职业移动和普通攻击，并验证 `B`、`Tab`、`Esc`、`1/2/3` 等正式操作。
5. 在 Development Build 或编辑器中使用 F1 开启调试，再验证 L、P、O、N。
6. 检查联网说明没有包含真实连接串、密码或 Token。
7. 在 Test Runner 中运行全部 EditMode 测试；当前工作区同时包含 v3.0 数值调整，需要重点核对 JSON、ScriptableObject、Prefab 和回归断言是否一致。

### 6. 面试表达

这个项目是我个人持续迭代的 Unity 3D 动作 Roguelite。玩法上有四职业、怪物战斗、随机成长、背包掉落和 Boss 周回；架构上我把玩家数据和规则放到 QFramework 的 Model、System、Command、Query 和 Event 中，MonoBehaviour 主要处理输入、动画、物理和 UI。项目还实现了怪物 FSM、Boss 行为树、多类对象池、Addressables 技能特效加载，以及 TCP/Protobuf 登录和角色存档。为了方便演示，我又做了不依赖服务端的游客 JSON 存档，并加入临时文件和备份恢复。当前项目仍保留少量历史兼容代码，我会通过测试保护逐步迁移，而不是一次性大改导致原有玩法失效。

### 7. 面试追问

1. **为什么玩家要拆成多个组件？** 通用控制器只负责装配，移动、战斗、生命、成长、表现和远程攻击分别维护，修改某个职业表现时不需要同时改权威属性或存档规则。
2. **为什么使用 QFramework？** 主要利用 Model、System、Command、Query 和 Event 明确读写边界，让 UI 与 MonoBehaviour 不直接修改权威数据，也方便用 EditMode 测试规则。
3. **项目里的网络功能做到什么程度？** 当前完成账号注册登录、四角色槽、进入离开和成长存档，不是多人战斗同步；服务端通过 Session 绑定当前角色并校验客户端提交的成长范围。
4. **对象池最容易出现什么问题？** 回收后状态残留，包括事件未注销、协程未停止、Collider 或 Trail 状态错误、旧目标引用和伤害幂等标记未清理，所以每类池化对象都有明确的重置入口。
5. **项目目前最大的不足是什么？** 玩家旧逻辑尚未完全迁移，背包没有持久化，Addressables 还是本地加载阶段，v3.0 数值配置也需要完成资源、Prefab 和测试的最终一致性验证，这些都有明确的后续收敛顺序。

### 8. 本次涉及知识点

- 求职项目 README 的信息层级
- 从代码、Prefab、场景和配置中核实功能
- 已实现、原型阶段和后续计划的边界
- Unity 客户端简历的 STAR/问题—方案—结果表达
- Mermaid 架构图与调用链表达
- 技术亮点和代码数量之间的区别
- 第三方资源授权说明
- 网络、存档和测试能力的准确表述
- 遗留代码迁移与风险控制

## 功能名称：v3.0 全局战斗与成长数值重平衡

### 1. 实现目标

把策划案 v3.0 中已经确认的数值真正同步到游戏运行时，解决职业强度差异失控、升级收益过高、怪物指数膨胀、技能后期倍率失控和 Boss 首轮过弱的问题。同时让角色、升级、怪物、金库、技能、体力、掉落和 Boss 都读取同一套明确公式，方便后续继续做数据测试和求职展示。

### 2. 涉及脚本

- `GameConfig.cs`：保存等级经验、通用兜底属性、升级幅度、升级次数上限、权重、治疗和普通怪 V/B 成长系数。
- `PlayerModel.cs`、`PlayerProgressionSystem.cs`：初始化职业属性，应用升级公式与次数上限，固定生命恢复成长，并限制本局等级为 20。
- `PlayerMovementComponent.cs`：应用 100 点体力、跳跃/翻滚/冲刺消耗和恢复速度。
- `SlimeCo.cs`、`MonsSpawner.cs`：应用怪物基础属性、攻击间隔、V/B 双维度成长和刷怪节奏。
- `BoxCo.cs`：应用金库血量、经验、伤害分、击破分、20% 治疗和历史分数重建公式。
- `SpiderKingBossController.cs`、`BossRoomSceneBootstrap.cs`：应用 Boss 基础数值、狂暴倍率、周回成长与移速上限。
- `GameplayRuntime.cs`、`BossVictoryPortalSpawner.cs`：保存跨场景的 Boss 击杀奖励分。
- `CharacterDefine.json`、`SkillDefine.json`：保存四职业基础值和三种技能的四级精确配置。
- `InventoryDatabase.asset`、`HealingPotion.asset`、`ManaPotion.asset`：保存 12% 小怪掉率、55:45 药水权重、药水效果与 Boss 不放回掉落数量。
- `Slime1.prefab`、`Slime2.prefab`、`Box.prefab`、`PlayerRuntime.prefab`、`Spider King.prefab`、`MainScene.unity`、`BossRoomScene.unity`：同步 Inspector 序列化值。
- `InventorySystemTests.cs`、`SkillConfigValidationTests.cs`、`RangedCharacterPlayableTests.cs`、`WarriorPlayableTests.cs`：把资源回归断言同步到 v3.0 规则。

### 3. 调用流程

职业配置加载 -> `PlayerModel.Reset` 建立基础属性 -> 击杀怪物/击破金库获得经验 -> `PlayerProgressionSystem.DoLevelUp` -> 生成带权且不重复的属性三选一 -> `CanApplyAttributeUpgrade` 同时判断次数上限和数值上限 -> 应用属性并保存选择次数。

金库击破 -> `BoxCo.HandleDestroyed` -> 按 `70 + 15V + 30B` 发经验并回复 20% 最大生命 -> 金库层级加一 -> `SlimeCo.ApplyVaultDifficulty` 按 V/B 重新从基础值计算怪物生命、攻击和经验 -> 每 5 个金库开放 Boss -> `SpiderKingBossController.ApplyBossRoundScaling` 按周回线性成长 -> Boss 死亡增加 `1000 × 当前轮次` 分并抽取两个不重复掉落。

### 4. 核心原理

这次数值平衡没有把结果直接写死在战斗代码里，而是采用“基础值 + 配置系数 + 运行时计数”的方式。职业和技能用 JSON，掉落用 ScriptableObject，场景通用规则用 `GameConfig`，Prefab 只保存自己独有的基础值。这样修改角色攻击力不会影响怪物公式，调整掉率也不需要碰战斗逻辑。

普通怪和金库从指数成长改成线性双维度成长。V 代表已经击破的金库数，B 代表已经击败的 Boss 数。生命、攻击和经验每次都从基础值重新计算，而不是拿当前值继续乘，能避免浮点误差和对象池复用后重复叠乘。Boss 周回也使用线性成长，并给冷却和移动速度加下限/上限，避免高轮次进入不可读的攻击频率。

升级系统同时限制“选择次数”和“最终数值”。次数上限决定一局构筑能投入多少次，数值上限处理职业初始减伤不同等情况。例如战士自带 20% 减伤，再选择五次 4% 后正好达到 40%；其他职业即使没有达到 40%，也会在五次后停止出现该选项。这让不同职业保留差异，同时不会无限堆叠。

### 5. Unity 测试方式

1. 打开 `MainScene`，依次选择战士、法师、弓箭手和刺客，检查 HUD/属性面板中的生命、魔法、攻击、减伤和移动速度。
2. 使用开发者升级功能或正常击杀怪物，确认 Lv.1 到 Lv.20 使用新经验表，Lv.20 不再升级，并在 5/10/15/20 级出现技能选择。
3. 重复选择同一种属性，确认攻击/生命最多 6 次、移速与回血/吸血最多 4 次、暴击最多 7 次、闪避与减伤最多 5 次，达到上限后不再出现在三选一中。
4. 消耗体力测试跳跃、翻滚和奔跑，确认上限 100、跳跃 20、翻滚 35、奔跑每秒 15、恢复每秒 20，体力低于 15 时不能新开冲刺。
5. 击破第一个金库，确认基础血量 250、重生无敌 2 秒、获得 70 经验、回复 20% 最大生命；继续击破并观察血量和经验按 V/B 线性增长。
6. 检查六个刷怪点每点最多 3 只、重生间隔 7 秒，总上限 18，近战与远程刷怪点比例为 4:2。
7. 击杀普通怪，统计较多样本确认总掉率接近 12%，生命/魔法药水比例接近 55:45；验证生命药水回复 25%、魔法药水回复 30%、单格最多 5 个。
8. 每击破 5 个金库进入 `BossRoomScene`，确认首轮 Boss 生命 4000，35% 血量进入狂暴；击杀后获得当前轮次对应的 1000、2000、3000……分，并掉落两个不同类型光球。
9. 在 Test Runner 中运行全部 EditMode 测试；如果 Unity 已经打开项目，请直接在当前编辑器运行，避免第二个 Unity 实例被项目锁拦截。

### 6. 面试表达

我对项目做过一次完整的数值重平衡。数据层上，我把职业和技能放在 JSON，掉落放在 ScriptableObject，通用成长系数集中在 GameConfig；逻辑层不写具体平衡数值，只根据配置和局内进度计算。普通怪和金库原来是指数叠乘，后期很容易失控，我改成以金库击破数 V 和 Boss 击败数 B 为变量的线性公式，而且每次从基础值重算，避免对象池复用后重复叠乘。升级系统同时有次数上限和属性上限，技能固定四级，Boss 周回有速度上限和冷却下限。这样既能控制单局节奏，也方便后续用配置表做 AB 调参和自动化回归测试。

### 7. 面试追问

1. **为什么普通怪不用指数成长？** 当前项目单局会连续打多个金库，指数成长会让后段数值快速失控；线性 V/B 公式更容易预测 TTK，也更方便策划调参。
2. **为什么每次从基础值重算？** 怪物会进入对象池，如果拿当前值继续乘，复用或重复收到事件时会多乘一次；基础值乘最终倍率具有幂等性。
3. **次数上限和数值上限为什么都需要？** 次数上限控制构筑投入，数值上限保证暴击、闪避和减伤不会破坏战斗规则；两者解决的问题不同。
4. **跨场景 Boss 分数怎么保存？** `GameplayRuntime` 把宝箱原始分和额外奖励分分开缓存，Boss 场景只增加奖励分，回到主场景后再与 `BoxCo.Score` 合并显示。
5. **如何验证数值不是只改了 Inspector？** 我同步修改代码默认值、Prefab/场景序列化值和资源回归测试，并用编译、JSON 解析和 Test Runner 检查三者一致。

### 8. 本次涉及知识点

- 数据驱动设计与配置职责划分
- 线性成长、指数成长与 TTK 控制
- V/B 双变量难度公式
- 幂等计算与对象池状态复用
- 带权不放回随机抽取
- 属性次数上限与最终数值上限
- Unity Prefab、Scene YAML 与 ScriptableObject 序列化
- 跨场景运行时状态与分数合并
- EditMode 回归测试与配置边界测试

## 功能名称：Windows 构建 UI 稳定性与弓箭手连续攻击

### 1. 实现目标

修复编辑器与 Windows 包体表现不一致的问题：Boss 房间在包体中缺少玩家 HUD、登录背景不能适配分辨率、登录页无法直接退出，以及普通包无法开启开发者模式。同时把弓箭手普攻改成有明确攻速上限的长按连续射击，并降低单箭伤害。

### 2. 涉及脚本

- `BossRoomScene.unity`、`BossBattleHudUi.cs`：让 Boss 场景直接依赖公共玩家 UI，并保证 Boss 血条显示在最上层。
- `LoginScene.unity`、`ApplicationQuitButton.cs`：完成登录画布适配、全屏背景和退出桌面入口。
- `IGameplayInput.cs`、`InputCo.cs`：增加左键持续按住状态。
- `PlayerCombatComponent.cs`、`CharacterDefine.json`：实现弓箭手 0.72 秒攻击间隔和 30 点基础攻击。
- `PlayerDeveloperModeComponent.cs`：允许 PC 演示包使用 F1 调试入口。
- `RangedCharacterPlayableTests.cs`、`StandaloneBuildConfigurationTests.cs`：保护连续射击、Boss UI 和登录场景装配。

### 3. 调用流程

`InputCo.GetMouseButton(0) -> IGameplayInput.LeftMouseHeld -> PlayerCombatComponent.CheckAttackInput -> 攻击间隔判断 -> StartImmediateArcherAttack -> PlayerRangedAttackComponent.Fire -> PlayerBasicAttackProjectile -> PlayerBasicAttackDamageResolver`。

Boss UI 链路为：`BossRoomScene 直接引用 GameplayUiRoot Prefab -> 玩家 HUD 初始化`，同时 `BossRoomSceneBootstrap -> BossBattleHudUi -> sortingOrder 6000 -> Boss 血条覆盖显示`。

### 4. 核心原理

编辑器能够使用 `AssetDatabase` 按项目路径加载资源，但这个 API 不会进入最终包体。运行时需要通过场景引用、Resources 或 Addressables 建立构建依赖。本次让 Boss 场景直接引用公共 UI Prefab，使 Unity 构建时自动收集完整 HUD，也避免复制两套玩家界面。

长按攻击不能简单地在每帧检测到按键后都发射，因为帧率越高伤害就越高。输入层只记录“当前是否按住”，战斗层维护剩余攻击间隔；第一箭立即触发，之后必须等 0.72 秒才能发下一箭。快速点击和持续按住都经过相同计时器，因此攻速稳定且不依赖电脑帧率。

登录 Canvas 使用 1920×1080 参考分辨率和宽高混合缩放，背景采用四边拉伸。这样 UI 位置由锚点表达，而不是依赖某台电脑的固定像素尺寸。

### 5. Unity 测试方式

1. 从登录场景运行，切换 1280×720、1920×1080、2560×1440 和 16:10 分辨率，确认背景没有露边。
2. 点击右上角“退出游戏”，编辑器应停止播放；Windows 包应直接退出桌面。
3. 选择弓箭手进入主场景，单击立即发箭，按住左键后每 0.72 秒发一箭，快速连点不能突破间隔。
4. 进入 Boss 房间，确认玩家 HUD、技能栏和 Boss 血条同时显示，Boss 血条位于最上层。
5. 构建一个未勾选 Development Build 的 Windows 包，按 F1 后测试 L、P、O、N。
6. 在 Test Runner 中运行 `RangedCharacterPlayableTests` 和 `StandaloneBuildConfigurationTests`，再运行全部 EditMode 测试。

### 6. 面试表达

我处理过一次 Unity 编辑器与正式包表现不一致的问题。Boss 玩家 UI 原来依赖编辑器专用的 AssetDatabase 兜底，所以编辑器正常、包体缺失；我改成让 Boss 场景直接引用公共 GameplayUiRoot Prefab，使构建系统能追踪资源依赖，并用 Canvas sortingOrder 管理 Boss HUD 层级。弓箭手方面，我把左键按住状态放在统一输入接口里，战斗组件用配置的 0.72 秒维护独立攻击间隔，保证长按和快速点击都不能绕过攻速。最后用 EditMode 测试直接检查构建场景装配，避免同类问题回归。

### 7. 面试追问

1. **为什么编辑器能加载但包体加载不到？** `AssetDatabase` 只存在于 UnityEditor 程序集，正式包不会包含；资源必须建立场景、Resources 或 Addressables 依赖。
2. **为什么不在 Update 中按住就直接发箭？** 那会使攻击次数依赖帧率，必须用时间间隔控制真实攻击频率。
3. **为什么快速点击也要受同一个冷却限制？** 如果点击和长按走两套规则，玩家可以用连点绕过平衡配置，输入方式会影响 DPS。
4. **Canvas Scaler 的参考分辨率有什么作用？** 它把设计稿坐标映射到实际屏幕尺寸，配合锚点保持 UI 在不同分辨率下的相对位置。
5. **为什么 Boss UI 单独使用更高 sortingOrder？** 玩家 HUD 和 Boss HUD 都是 Screen Space Overlay Canvas，排序值可以明确控制关键战斗信息的覆盖关系。

### 8. 本次涉及知识点

- Unity 构建资源依赖与 `AssetDatabase` 的编辑器边界
- Prefab 场景引用和公共 UI 复用
- Canvas Scaler、锚点与 Canvas 排序
- `GetMouseButtonDown` 与 `GetMouseButton` 的区别
- 基于时间的攻击频率和帧率无关设计
- 输入层与战斗逻辑层解耦
- 条件编译与 PC Standalone 调试入口
- EditMode 场景装配回归测试

## 功能名称：弓箭手高速连续射击平衡调整

### 1. 实现目标

将弓箭手普攻间隔从 0.72 秒缩短为 0.3 秒，让长按左键时的射击反馈更加紧凑；同时把基础攻击力从 30 降低为 15，避免单箭伤害和高攻速同时过强。

### 2. 涉及脚本

- `CharacterDefine.json`：保存弓箭手基础攻击力和普攻间隔配置。
- `RangedCharacterPlayableTests.cs`：验证配置数值，并验证按住左键时不能绕过 0.3 秒攻击间隔。
- `PlayerCombatComponent.cs`：继续读取职业配置控制弓箭手攻击冷却，本次无需修改战斗逻辑。

### 3. 调用流程

`CharacterDefine.json` -> `CharacterDefine` -> `PlayerCombatComponent.Initialize` -> `basicAttackDuration` -> `CheckAttackInput` -> `StartImmediateArcherAttack`

### 4. 核心原理

攻速和伤害属于职业配置数据，不需要为某个职业在战斗代码中增加写死分支。弓箭手完成一次射击后，战斗组件把冷却设置为配置中的 0.3 秒；冷却归零前，无论持续按住还是快速点击都不能发射下一箭。这样数值策划可以直接调整手感，而输入和攻击实现保持稳定。

### 5. Unity 测试方式

在 `MainScene` 选择弓箭手，单击左键应立即发射第一箭；持续按住左键时约每 0.3 秒发射一次，快速连点不能突破该间隔。攻击固定目标时，确认单箭基础伤害按 15 计算，并观察攻击动画和箭矢对象池是否稳定。

### 6. 面试表达

弓箭手的攻速和攻击力由职业配置统一管理，战斗组件只读取配置并执行冷却，不针对职业写死数值。这次我把射击间隔从 0.72 秒调整到 0.3 秒，同时把基础攻击力从 30 降到 15，并同步更新自动化测试。这样既改善了长按射击的操作反馈，也保留了数据驱动和防止快速点击绕过攻速的机制。

### 7. 面试追问

1. **为什么攻速要用时间而不是帧数？** 不同设备帧率不同，用秒计时可以保证实际攻击频率一致。
2. **为什么数值写在配置文件中？** 数值调整不需要修改战斗逻辑，能够降低耦合并方便后续平衡迭代。
3. **快速点击为什么不能提升攻速？** 点击和长按共用同一攻击冷却，避免输入方式破坏职业平衡。
4. **伤害减半后 DPS 是否也减半？** 不会，因为攻速同时提升；调整后理论基础 DPS 约为每秒 50 点。
5. **高攻速有什么性能风险？** 同屏投射物会增加，需要关注对象池容量、碰撞检测和特效开销。

### 8. 本次涉及知识点

- 数据驱动的职业数值配置
- 攻击冷却与帧率无关计时
- 长按输入与单帧输入的区别
- 投射物对象池容量评估
- 单次伤害、攻速与 DPS 的关系
- 配置回归测试

## 功能名称：弓箭手攻速微调与失败后返回角色选择

### 1. 实现目标

将弓箭手普攻间隔从 0.3 秒继续缩短为 0.25 秒，基础攻击力保持 15。游戏失败界面的“退出游戏”改为“返回角色选择”，点击后保存角色进度、结束当前角色会话，并返回选角界面，不再关闭客户端。

### 2. 涉及脚本

- `CharacterDefine.json`：配置弓箭手 0.25 秒普攻间隔。
- `GameSessionUi.cs`：新版失败界面复用保存并返回选角流程。
- `ReStartPanel.cs`：同步修改后备失败面板，避免不同路径行为不一致。
- `RangedCharacterPlayableTests.cs`：验证弓箭手伤害和攻速配置。
- `CharacterProgressPersistenceTests.cs`：验证失败界面文案和返回选角绑定。
- `SceneFlowService.cs`：提供既有的玩法状态清理与返回选角流程，本次直接复用。

### 3. 调用流程

弓箭手攻击：`CharacterDefine.json -> PlayerCombatComponent.Initialize -> 0.25 秒冷却 -> StartImmediateArcherAttack -> 箭矢结算 15 点基础攻击`。

失败返回：`玩家死亡 -> GameSessionUi.ShowGameOver -> 返回角色选择按钮 -> FlushAndLeave -> PrepareForSceneTransition -> SceneFlowService.ReturnToCharacterSelect -> LoadingScene -> CharacterSelectScene`。

### 4. 核心原理

弓箭手仍然使用配置驱动的攻击间隔，点击和长按共用同一个冷却，所以提高攻速不需要改输入或投射物代码。失败返回时先异步保存角色数据，成功后再恢复时间缩放、清理本局临时状态并切换场景；如果保存失败，玩家会留在失败界面重试，避免直接切场景造成进度丢失。

### 5. Unity 测试方式

在 `MainScene` 选择弓箭手，按住左键应约每 0.25 秒发射一箭，快速连点不能突破间隔。让玩家生命值归零，确认失败界面显示“返回角色选择”；点击后应经过 LoadingScene 返回 CharacterSelectScene，账号保持登录，并且重新进入角色后能读取最近保存的进度。

### 6. 面试表达

这次我继续通过职业配置把弓箭手攻击间隔调整为 0.25 秒，没有给战斗代码增加职业数值分支。游戏失败流程则从直接关闭程序改为先保存角色数据，再通过统一场景服务返回角色选择。场景服务负责恢复 Time.timeScale、清理背包和 Boss 等本局状态，同时保留账号登录态，使存档、UI 和场景切换的职责保持清晰。

### 7. 面试追问

1. **为什么失败后不能直接加载角色选择场景？** 需要先等待异步存档完成，否则可能丢失本局进度。
2. **存档失败时怎么处理？** 保持失败面板并重新启用按钮，显示错误信息让玩家重试。
3. **为什么返回选角不清除账号登录态？** 退出的是角色会话，不是账号会话，玩家应能直接重新选择角色。
4. **为什么要恢复 Time.timeScale？** 失败界面会把时间缩放设为零，不恢复会让后续场景保持暂停。
5. **为什么还要修改 ReStartPanel？** 它是旧版后备失败界面，同步修改可以避免边缘路径仍然退出程序。

### 8. 本次涉及知识点

- 配置驱动的攻击间隔
- 单次伤害、攻击频率与 DPS
- Coroutine 异步存档流程
- 角色会话与账号会话的区别
- 场景切换前状态清理
- `Time.timeScale` 跨场景影响
- UI 按钮事件绑定与回归测试

## 功能名称：战士蓄力重击系统

### 1. 实现目标

给战士增加区别于刺客三段连击的职业机制：短按左键释放普通单次攻击，长按则在挥剑出手前固定上半身攻击姿势并积累伤害倍率。蓄力最多 1.6 秒，倍率从 1 倍线性提升到 3 倍；松手后恢复剩余动画并延迟 0.08 秒开启一次近战判定。蓄力期间保留低速移动，但禁止翻滚、跳跃和技能。

### 2. 涉及脚本

- `IGameplayInput.cs`、`InputCo.cs`：增加左键松开帧 `LeftMouseUp`，让状态机能准确区分按下、持续和释放。
- `CharacterDefine.cs`、`CharacterDefine.json`：新增可选的 `CharacterChargedAttackDefine`，只为战士配置最大时间、倍率、动画固定点、命中延迟和移动限速。
- `PlayerChargedAttackComponent.cs`：维护 `Inactive -> Windup -> Holding -> Releasing` 状态机和蓄力进度。
- `PlayerCombatComponent.cs`：提供受控普攻开始、释放和取消入口，并在暴击结算后应用蓄力倍率。
- `PlayerPresentationComponent.cs`：只固定 `Attack Layer.Attack` 的归一化时间，不暂停整个 Animator。
- `PlayerRuntimeController.cs`、`PlayerRuntime.prefab`：装配蓄力组件，并协调技能禁用、移动限速和异常取消。
- `PlayerChargeBarUi.cs`、`GameplayUiRoot.cs`、`GameplayUiRoot.prefab`：显示橙色/金色蓄力进度和当前倍率，主场景与 Boss 房间共用同一套 UI。
- `WarriorChargedAttackTests.cs`：回归配置、短按、倍率封顶、动画固定、取消恢复、伤害和 UI 装配。

### 3. 调用流程

`InputCo -> IGameplayInput -> PlayerCombatComponent.CheckAttackInput -> PlayerChargedAttackComponent`。

按下时：`BeginControlledBasicAttack -> PlayerPresentationComponent.SetCombo(1) -> 播放攻击前摇`。

持续按住时：`TryHoldSimpleAttackPose(0.20) -> Holding -> ChargeProgress -> PlayerChargeBarUi`。

松手时：`ReleaseSimpleAttackPose -> ReleaseControlledBasicAttack(倍率, 0.08秒) -> OpenWeaponHitWindow -> WeaponCo -> PlayerBasicAttackDamageResolver -> RollAttackDamage`。

### 4. 核心原理

这个功能用状态机管理“未攻击、前摇、保持、释放”，而不是在一个 Update 里堆很多布尔值。前摇阶段只播放动画，不开启伤害；动画达到 20% 后进入保持阶段，蓄力计时才开始。这样画面中的准备动作、UI 进度和真正伤害窗口能保持一致。

动画固定没有使用 `Animator.speed = 0`，因为全局暂停会连基础移动层一起停止。表现组件在 `LateUpdate` 中把攻击层的 `Attack` 状态重新采样到 0.20，只固定上半身蒙版层；下半身仍可播放移动动画。松手时停止重复采样，Animator 会从固定点继续完成挥剑。

伤害顺序是“基础攻击 -> 暴击 -> 蓄力倍率 -> 四舍五入”。本次攻击的倍率保存在战斗组件中，直到攻击窗口结束才重置，因此同一挥剑命中的多个目标使用相同倍率，而每个目标仍通过攻击窗口编号保证只受伤一次。

蓄力 UI 只读取状态机公开的只读属性，不反向修改计时或倍率。这样更换 UI 样式不会影响战斗逻辑，主场景和 Boss 房间也能直接复用公共 Prefab。

### 5. Unity 测试方式

1. 在角色选择界面选择战士并进入 `MainScene`，短按左键，确认不显示蓄力条且按 1 倍伤害出手。
2. 长按左键，确认攻击动画到出手前姿势后固定，技能栏上方出现 360×32 的蓄力条。
3. 蓄力约 0.8 秒时确认文字接近 `蓄力 x2.0`；1.6 秒后变成金色并显示 `蓄力完成 x3.0`，继续按住不会自动出手。
4. 蓄力期间尝试移动、翻滚、跳跃和技能：只能以最高 1.5 的速度移动，其他动作应被阻止；受到普通伤害后蓄力继续。
5. 松开左键，确认动画自然播放剩余部分，0.08 秒后攻击盒开启一次，同一目标只结算一次伤害。
6. 蓄力时打开暂停、触发升级选择或让角色死亡，确认动画恢复、攻击盒关闭、蓄力条隐藏。
7. 在 `BossRoomScene` 重复长按测试，确认蓄力条位于技能栏上方且不遮挡 Boss 血条。
8. 在 Test Runner 运行 `WarriorChargedAttackTests`，再运行全部 EditMode 测试。

### 6. 面试表达

为了让战士和刺客玩法区分开，我给战士做了一个蓄力重击。输入层统一提供鼠标按下、按住和松开，蓄力组件用四状态状态机控制前摇、保持和释放；战斗组件只提供受控普攻接口，松手前不会创建伤害窗口。动画上我没有暂停整个 Animator，而是在 LateUpdate 中只把带上半身蒙版的 Attack Layer 固定在 20% 归一化时间，所以角色蓄力时仍能低速移动。伤害先走原有暴击公式，再乘 1 到 3 倍的蓄力倍率，UI 只读取只读进度。这样的拆分既保留了现有刺客连击和弓箭手连射，也方便以后扩展霸体、蓄力特效或装备词条。

### 7. 面试追问

1. **为什么要用状态机？** 蓄力包含前摇、保持、释放和取消，不同阶段允许的输入与伤害行为不同；显式状态比多个布尔值组合更容易避免非法状态。
2. **为什么不用 `Animator.speed = 0`？** 它会暂停所有动画层，下半身移动也会停止；固定单独攻击层可以保留低速移动表现。
3. **蓄力倍率为什么在暴击后计算？** 这是明确的数值规则，能让暴击和蓄力同时生效；集中在 `RollAttackDamage` 后处理也避免每个目标重复写公式。
4. **如何防止一刀多次伤害同一目标？** 每次开启攻击盒都会增加攻击窗口编号，`WeaponCo` 按目标和窗口去重；同一窗口只命中一次。
5. **异常退出蓄力怎么处理？** 死亡、暂停、升级、对象禁用都调用统一取消入口，恢复攻击层、关闭碰撞盒并把倍率重置为 1。

### 8. 本次涉及知识点

- 有限状态机和输入边沿事件
- `GetMouseButtonDown`、`GetMouseButton`、`GetMouseButtonUp`
- Animator 分层、Avatar Mask、归一化时间与 `LateUpdate`
- 受控攻击窗口和碰撞去重
- 暴击与倍率的伤害计算顺序
- 配置驱动的可选职业机制
- UI 只读绑定与公共 Prefab 复用
- Unity 生命周期中的取消与资源状态恢复
- EditMode 动画、配置和 Prefab 装配测试

## 功能名称：战士满蓄力反馈与临时减伤

### 1. 实现目标

战士蓄力达到 1.6 秒后，让角色以金黄色闪烁提示“重击准备完成”，并获得 15% 额外乘算减伤。减伤覆盖满蓄力保持和松手后的释放动画，攻击结束、取消、死亡、暂停或对象禁用时立即清除，使战士可以更稳定地完成重击，但不会变成完全无敌。

### 2. 涉及脚本

- `CharacterDefine.cs`、`CharacterDefine.json`：为战士蓄力配置增加 15% 满蓄力临时减伤。
- `PlayerChargedAttackComponent.cs`：公开满蓄力防护是否生效及当前减伤值，并用原有状态机控制生命周期。
- `PlayerCommands.cs`、`PlayerCombatSystem.cs`：让受伤命令携带可选临时减伤，并集中完成一次取整的乘算结算。
- `PlayerHealthComponent.cs`：读取满蓄力状态、传递临时减伤，并统一管理受击红色和蓄力黄色材质反馈。
- `PlayerRuntime.prefab`：配置金黄色 `#FFD54A` 和 0.18 秒闪烁间隔。
- `WarriorChargedAttackTests.cs`：回归状态生命周期、68 点伤害结果、颜色优先级和恢复逻辑。

### 3. 调用流程

状态流程：`PlayerChargedAttackComponent.Holding -> ChargeProgress 达到 1 -> IsFullChargeGuardActive -> Releasing -> 攻击结束 -> Inactive`。

受伤流程：`怪物/Boss/子弹 -> PlayerHealthComponent.Hit -> TakePlayerDamageCommand(15% 临时减伤) -> PlayerCombatSystem.TakeDamage -> PlayerModel 扣血 -> 受伤事件与飘字`。

表现流程：`PlayerRuntimeController.Update -> PlayerHealthComponent.TickHitFlash -> 受击红色优先 -> 满蓄力黄色 -> 原始材质颜色`。

### 4. 核心原理

临时减伤没有直接加到玩家常驻的 `DamageReduction` 上，而是作为本次受伤命令的上下文参数传入战斗系统。战斗系统把常驻减伤和临时减伤分别转换成“剩余承伤倍率”后相乘，例如战士 20% 常驻减伤与 15% 临时减伤会得到 `100 × 0.8 × 0.85 = 68`，并在最后只进行一次四舍五入。这样不会突破常驻属性 40% 的成长规则，也不会污染存档。

黄色和红色都需要修改同一批角色材质，因此由生命表现组件统一决定颜色优先级。材质实例只在绑定角色模型时缓存一次，Update 中只切换缓存材质的颜色；满蓄力不会关闭 Renderer，所以玩家能区分“黄色减伤提示”和“闪避无敌的显隐闪烁”。实际受到伤害时先显示红色，0.1 秒后如果防护仍生效就继续黄色闪烁。

### 5. Unity 测试方式

1. 在 `MainScene` 选择战士，蓄力未满时确认角色颜色不变。
2. 按住左键 1.6 秒，确认蓄力条满后角色以金黄色闪烁，模型不会消失。
3. 保持满蓄力受到攻击，记录掉血；取消蓄力后受到同一次攻击，满蓄力时应额外减少 15% 剩余伤害。
4. 满蓄力受到实际伤害时应先闪红，随后恢复黄色；松手后黄色覆盖释放动画并在攻击结束时消失。
5. 分别测试暂停、升级选择、死亡和切换场景，确认没有黄色材质或临时减伤残留。
6. 在 `BossRoomScene` 重复验证，并在 Test Runner 运行 `WarriorChargedAttackTests` 和完整 EditMode 测试。

### 6. 面试表达

我在战士蓄力重击上增加了一个满蓄力防护机制。蓄力状态机达到 100% 后会公开一个只读防护状态，生命组件受击时把 15% 临时减伤作为命令参数传给战斗系统。系统没有修改玩家常驻属性，而是让常规减伤和临时减伤乘算，并且只在最终结果上取整，所以既不会影响存档，也不会挤占属性升级上限。表现层把受击红色、满蓄力黄色和角色原色放在同一个组件里按优先级仲裁，同时缓存材质避免每帧创建实例。这样逻辑、数据和视觉职责比较清楚，后续也容易扩展其他临时防御 Buff。

### 7. 面试追问

1. **为什么使用乘算减伤？** 乘算按剩余伤害继续减免，不会把临时效果直接堆到常驻上限上，数值更稳定。
2. **为什么不直接修改 PlayerModel 的 DamageReduction？** 满蓄力是一次攻击期间的临时上下文，写进常驻模型容易在取消、保存或切场景时残留。
3. **为什么伤害只取整一次？** 中途多次取整会让计算顺序改变最终数值，一次取整能保证公式结果一致。
4. **红色和黄色同时触发怎么办？** 生命表现组件统一按红色受击、黄色蓄力、原色的顺序选择最终材质颜色。
5. **为什么黄色闪烁不关闭 Renderer？** 显隐闪烁已经表示闪避无敌，黄色只改变颜色可以避免玩家误解状态含义。

### 8. 本次涉及知识点

- 临时战斗上下文与常驻角色属性的区别
- 加算减伤与乘算减伤
- 浮点数计算和最终一次取整
- 有限状态机派生只读状态
- 材质实例缓存与 Renderer 表现
- 多种视觉状态的优先级仲裁
- Command、System、Model 的职责边界
- 对象禁用和场景切换时的临时状态清理

## 功能名称：战士满蓄力旋转范围重斩

### 1. 实现目标

解决战士单体近战判定导致清怪效率偏低的问题。战士只有蓄满 1.6 秒后松手，才会在继续播放剩余攻击动画的同时原地旋转 360 度，并对自身周围 3 米内的所有可攻击目标结算一次 3 倍重击；短按和未满蓄力仍保持原有单体攻击。

### 2. 涉及脚本

- `CharacterDefine.cs`、`CharacterDefine.json`：配置 3 米范围、0.6 秒旋转时间和 360 度角度。
- `PlayerChargedAttackComponent.cs`：识别满蓄力释放、按绝对进度旋转根节点，并在完成或取消时恢复原朝向。
- `PlayerRuntimeController.cs`：在战斗输入后推进旋转，旋转期间只锁水平移动并保留重力。
- `PlayerCombatComponent.cs`：延迟 0.08 秒执行一次圆形范围扫描，保持普通武器碰撞盒关闭。
- `PlayerChargedSpinEffect.cs`：使用 `LineRenderer` 显示金黄色扩散圆环，并接入公共表现对象池。
- `WarriorChargedAttackTests.cs`：回归配置、旋转进度、取消恢复、360 度命中、多 Collider 去重和特效复用。

### 3. 调用流程

`InputCo 松开左键 -> PlayerChargedAttackComponent.ReleaseAttack -> 判断 ChargeProgress == 1 -> PlayerCombatComponent.ReleaseControlledBasicAttack(3倍, 0.08秒, 3米)`。

表现流程：`PlayerRuntimeController.Update -> TickFullChargeSpin -> 初始朝向 × 绝对旋转进度 -> 0.6秒后恢复初始朝向`。

伤害流程：`TickEventlessAttackReleaseDelay -> ResolveControlledAreaAttack -> PlayerBasicAttackDamageResolver.ApplyInRadius -> HashSet按FighterInterface去重 -> 每个目标独立暴击与伤害结算`。

### 4. 核心原理

旋转不采用每帧 `Rotate(本帧角度)` 的累加写法，而是先记录释放瞬间的朝向，再根据 `计时 / 0.6秒` 得到绝对进度，直接计算“初始朝向乘当前角度”。这样帧率波动不会让最终朝向多转或少转；完成、暂停、死亡和对象禁用也都能回到同一份初始朝向。

范围攻击复用现有 `ApplyInRadius`，通过固定长度的 Collider 数组避免每次攻击分配新数组，再用 `HashSet<FighterInterface>` 处理一个怪物拥有多个 Collider 的情况。满蓄力范围结算会替代武器攻击盒，而不是与攻击盒同时工作，因此每个目标只会受伤一次。每个目标进入公共伤害方法时会单独掷暴击，但都共享本次 3 倍蓄力倍率。

移动锁定放在移动组件现有的 `movementBlocked` 参数上。该参数只让水平速度归零，`CharacterController.Move` 仍会处理垂直速度、重力与落地，避免旋转时在空中悬停。圆环表现从 `SkillVisualPool` 获取并回收，伤害逻辑即使找不到表现池也能独立完成。

### 5. Unity 测试方式

1. 在 `MainScene` 选择战士，短按或未满蓄力松手，确认不旋转、不出现圆环并继续使用原攻击盒。
2. 蓄满后松手，确认角色在约 0.6 秒内原地转一圈；约 0.3 秒时应面向反方向，结束后恢复释放前朝向。
3. 在角色前、后、左、右 3 米内放置目标，确认金色圆环出现且每个目标只受一次 3 倍基础伤害；3 米外不受伤。
4. 旋转时按方向键、跳跃、翻滚和技能键：不能水平移动或执行其他动作，但从空中释放时仍应正常下落。
5. 旋转中测试暂停、升级选择、死亡和禁用角色，确认旋转立即停止、朝向恢复、武器攻击盒保持关闭。
6. 在 `BossRoomScene` 验证 Boss 只受到一次 3 倍伤害，并确认相机不会跟随角色朝向旋转。
7. 在 Test Runner 运行 `WarriorChargedAttackTests`，再运行完整 EditMode 测试。

### 6. 面试表达

为了改善战士清怪慢的问题，我没有直接提高常驻攻击力，而是把满蓄力重击扩展成一个 360 度范围攻击。蓄力状态机只在进度达到 100% 时传入 3 米范围，战斗组件在松手 0.08 秒后用 `OverlapSphereNonAlloc` 扫描，并通过 `HashSet<FighterInterface>` 对多 Collider 目标去重；这次范围扫描会替代武器 Collider，避免重复伤害。表现上我记录释放前朝向，用 0.6 秒绝对进度驱动根节点旋转 360 度，完成或异常取消后统一恢复朝向。旋转期间只锁水平移动，重力仍由原移动组件处理，圆环特效则通过对象池复用。这样既提高了战士清怪能力，也保留了“需要满蓄力换取范围收益”的职业特色。

### 7. 面试追问

1. **为什么不用逐帧累加旋转角度？** 累加会受帧率和浮点误差影响，绝对进度能保证任意帧率都在 0.6 秒完成并恢复同一朝向。
2. **怎样防止多 Collider 怪物重复受伤？** 物理查询用 Collider 数组收集结果，再把它们解析为 `FighterInterface`，通过 HashSet 保证同一战斗目标只进入一次结算。
3. **为什么范围攻击要关闭武器碰撞盒？** 圆形扫描已经负责本次命中，如果武器盒同时开启，前方目标可能同时走两条伤害入口。
4. **为什么每个目标可以独立暴击？** 范围扫描只负责筛选和去重，每个目标仍调用公共普通攻击伤害结算，所以暴击掷骰独立，而蓄力倍率保持一致。
5. **原地旋转时为什么角色不会悬空？** 调度层只把移动组件的水平移动标记为阻塞，没有停止移动组件更新，垂直速度和重力仍会继续累积并交给 CharacterController。

### 8. 本次涉及知识点

- `Quaternion`、绝对旋转进度与浮点误差控制
- `OverlapSphereNonAlloc` 非分配物理查询
- `HashSet` 与多 Collider 目标去重
- 伤害逻辑和攻击表现解耦
- 水平移动锁定与重力保留
- `LineRenderer` 程序化圆环
- 对象池获取、状态重置与回收
- 状态取消和角色朝向恢复
- 配置驱动的职业差异化设计

## 功能名称：刺客专属技能3槽位显隐

### 1. 实现目标

修复战士、法师和弓箭手也会显示刺客“镰刀大旋转”技能3图标的问题。非刺客进入玩法场景时隐藏整个技能3槽位，包括图标、按键文字、技能状态和冷却遮罩；刺客继续正常显示和刷新。

### 2. 涉及脚本

- `PlayerSkillBarUi.cs`：根据当前职业与技能配置控制技能3槽位根节点显隐。
- `GameplayUiRoot.prefab`：显式绑定 `Skill3Slot`，确保隐藏的是完整槽位而不只是文字。
- `PlayerSkillBarUiTests.cs`：验证职业1、2、3隐藏技能3，职业4显示，并保护技能1、2不受影响。

### 3. 调用流程

`PlayerSkillBarUi.RefreshAllSlots -> PlayerModel.CharacterSave/CharacterDefine -> SkillDataManager.GetSkill(2001) -> SkillDefine.CanLearnByClass -> Skill3Slot.SetActive`。

### 4. 核心原理

技能栏没有直接写死“刺客职业ID等于4”，而是复用技能配置中的 `isCommon` 和 `allowedClassIds`。UI先取得当前职业ID，再询问技能配置当前职业是否允许学习；不允许时隐藏 `Skill3Slot` 根节点，因此其下的图标、文字和冷却遮罩会一起消失。以后如果把大旋转改给其他职业，只需修改配置表，UI逻辑不用改。

角色数据尚未初始化时职业ID为0，技能3会先保持隐藏；玩家模型完成初始化后，技能栏原有的刷新流程会重新判断并为刺客显示槽位。隐藏时还会清空旧文字和冷却状态，避免复用同一个UI实例时残留上一职业的表现。

### 5. Unity 测试方式

1. 分别选择战士、法师和弓箭手进入 `MainScene`，确认技能栏只显示技能1和技能2。
2. 选择刺客进入 `MainScene`，确认技能3图标、数字3、名称、学习状态和冷却正常显示。
3. 在 `BossRoomScene` 重复检查，两处场景应因复用同一个 `GameplayUiRoot.prefab` 而表现一致。
4. 在 Test Runner 运行 `PlayerSkillBarUiTests` 和完整 EditMode 测试。

### 6. 面试表达

我修复了职业专属技能在其他职业技能栏中残留的问题。技能3槽位的显示不是直接判断刺客ID，而是读取当前玩家职业，再复用技能配置的 `CanLearnByClass` 规则。非允许职业会隐藏整个槽位根节点，所以图标、文字和冷却遮罩能同步消失；刺客则继续进入原有的等级和冷却刷新流程。这样技能规则和UI使用同一份配置来源，避免两套职业判断以后出现不一致。

### 7. 面试追问

1. **为什么隐藏整个槽位而不是单独隐藏文字和图片？** 根节点显隐可以一次覆盖图标、文字和冷却遮罩，减少遗漏状态。
2. **为什么不直接判断 classId == 4？** 使用技能配置后，策划调整允许职业时不需要修改UI代码。
3. **角色数据初始化前怎么处理？** 未取得有效职业ID时默认隐藏，模型初始化后的刷新会得到最终结果。
4. **切换职业会不会残留旧文字？** 隐藏槽位时会同时清空文字并重置冷却遮罩。
5. **Boss房间为什么不用再改一次？** 主场景和Boss房间引用同一个公共玩法UI Prefab。

### 8. 本次涉及知识点

- 配置驱动的职业限制
- UI根节点显隐和子节点生命周期
- Model只读数据查询
- Prefab序列化引用
- UI状态清理与复用
- EditMode参数化回归测试

## 功能名称：刺客高风险高回报平衡调整

### 1. 实现目标

解决刺客同时拥有最高攻击、最高移速、首段双判定和即时范围技能，导致输出高但失误代价不足的问题。本次保留刺客 44 攻击、6 移速、首段双击和技能3原伤害曲线，把生命从 280 调整为 240、基础减伤从 10% 调整为 5%，并为镰刀大旋转增加 0.35 秒命中前摇和 1.05 秒动作占用，使职业定位变成真正的“高风险高回报”。

### 2. 涉及脚本

- `CharacterDefine.json`：调整刺客生命和基础减伤，不修改爆发与移速。
- `SkillDefine.cs`、`SkillDefine.json`：增加可选的技能出手承诺配置，并为技能3配置命中点、锁定时间和移动限速。
- `SkillDataManager.cs`：校验命中延迟、动作时长和移动限速，阻止非法配置进入运行时。
- `PlayerSkillCastComponent.cs`：管理技能3从前摇、命中到恢复控制的状态，并保证范围伤害只结算一次。
- `PlayerRuntimeController.cs`：把承诺状态接入翻滚、跳跃、普攻、技能和移动限速调度。
- `AssassinBalanceTests.cs`：保护刺客数值、技能曲线、延迟命中、单次结算、暂停冻结和禁用清理。

### 3. 调用流程

`InputCo 技能3松开 -> PlayerSkillCastComponent.TryCast -> TryCastPlayerSkillCommand -> 扣除MP并开始冷却 -> TryBeginCommittedScytheSpin -> 播放技能动画`。

`PlayerRuntimeController.Update -> PlayerSkillCastComponent.TickCommittedCast -> 0.35秒到达命中点 -> ResolveScytheSpin -> DealDamageInRadius -> FighterInterface.Hit`。

控制流程：`IsCommittedCastActive -> 禁止开始翻滚 -> 跳过新技能输入 -> PlayerCombatComponent因技能动画拒绝普攻 -> TickNormalMovement限速1.5并禁止跳跃 -> 1.05秒后恢复控制`。

### 4. 核心原理

“降低伤害”和“降低容错”不是同一件事。如果只降低攻击力，刺客仍然可以高速移动、立即结算范围伤害并迅速撤离，只是击杀时间变长。因此本次保留输出上限，通过降低等效生命和增加技能出手承诺，让错误时机释放技能会真实承受伤害。

承诺配置放在技能静态数据中，而不是把 0.35、1.05 和 1.5 写死在技能脚本里。技能组件只负责推进计时：释放成功时先保存本次伤害和半径，命中点到达后以玩家当时的位置扫描范围；`committedHitResolved` 保证即使一帧跨过命中点也只结算一次。蓝量和冷却在技能规则系统中立即消费，表现组件只管理延迟伤害，因此死亡取消不会错误返还资源。

运行时控制器负责动作优先级。它读取技能组件公开的只读状态，阻止翻滚和跳跃，把水平速度限制为 1.5，但仍调用移动组件处理重力和落地。暂停或升级选择时控制器不推进技能 Tick，`Time.deltaTime` 也为零，所以前摇与动作时长会冻结；死亡、禁用和切场景则清空待结算伤害，防止对象失效后继续命中。

### 5. Unity 测试方式

1. 重新选择刺客进入 `MainScene`，确认基础生命为 240，攻击和移动手感保持原样。
2. 学会技能3，松开按键后观察：伤害不应立即出现，而是在约 0.35 秒的挥砍点结算。
3. 技能动作的 1.05 秒内尝试翻滚、跳跃、普攻和其他技能，均不应执行；方向键只能让角色以最高 1.5 速度缓慢移动。
4. 在前摇期间受到攻击，确认正常扣血且技能不会获得无敌、减伤或霸体；死亡时本次范围伤害不再发生。
5. 在前摇期间暂停，等待后恢复，确认技能从原进度继续而不是在暂停中命中。
6. 在 `BossRoomScene` 重复测试，确认技能原伤害、范围、蓝耗和冷却没有变化。
7. 在 Test Runner 运行 `AssassinBalanceTests`、`SkillConfigValidationTests` 和完整 EditMode 测试。

### 6. 面试表达

我对刺客采用的是高风险高回报平衡，而不是直接砍伤害。排查后发现刺客不仅面板攻击和移速最高，首段攻击还有两次完整判定，专属范围技能又是即时结算并且可以很快撤离，所以问题核心是失误成本不足。我保留了爆发上限，把基础等效承伤降低，并为技能增加了配置驱动的出手承诺：释放时立即扣蓝和进入冷却，0.35 秒后才命中，1.05 秒内限制移动并禁止翻滚、跳跃、普攻和其他技能。技能组件管理命中状态，运行时控制器只读取状态处理输入优先级，伤害和控制没有塞在同一个类里，后续其他高威力技能也能复用这套配置。

### 7. 面试追问

1. **为什么不直接降低刺客攻击？** 直接降攻击只能降低输出，不会解决即时范围伤害和高速撤离带来的低风险问题。
2. **为什么命中数据要在释放成功时保存？** 可以确保一次技能使用同一份攻击结果，不会因为前摇期间属性变化导致显示、消耗和伤害规则不一致。
3. **为什么命中中心使用打击点时的位置？** 技能允许低速移动修正站位，玩家需要承担接近风险，同时仍有有限的操作空间。
4. **怎样保证范围伤害只触发一次？** 状态中保存 `committedHitResolved`，跨过命中时间后立即置为 true，后续帧不会再次进入结算。
5. **死亡取消为什么不返还蓝量和冷却？** 资源已经在规则层确认消费，死亡属于错误时机释放技能的代价；表现层也不应反向修改权威资源数据。

### 8. 本次涉及知识点

- 高风险高回报职业平衡
- 等效生命与乘算减伤
- 配置驱动的技能前摇和动作后摇
- 动作状态、输入优先级与控制权管理
- 延迟伤害的数据快照和单次结算
- `Time.deltaTime`、暂停冻结与生命周期清理
- 战斗逻辑、Unity表现和运行时调度解耦
- EditMode配置及状态回归测试

## 功能名称：Boss房间摄像机穿墙与遮挡恢复

### 1. 实现目标

解决 Boss 房间靠近墙体时，第三人称摄像机被避障射线强制推近、镜头穿到外侧后墙面继续挡住角色的问题。Boss 房间的四面墙和天花板允许镜头穿过；当它们位于角色与摄像机之间时临时停止渲染，离开视线后自动恢复，同时保留 Collider 限制战斗区域。

### 2. 涉及脚本

- `CameraCo.cs`：识别允许镜头穿过的遮挡物，不再用它们缩短镜头距离。
- `CameraPassThroughOccluder.cs`：标记可穿透墙体并缓存其 Renderer。
- `CameraOcclusionController.cs`：检测角色与镜头之间的标记墙体，管理隐藏和恢复。
- `BossRoomSceneBootstrap.cs`：为动态生成的四面墙、天花板和 Boss 相机补齐组件。
- `BossRoomSceneSetupTool.cs`：保证编辑器重新生成 Boss 场景时也得到相同装配。
- `BossRoomCameraOcclusionTests.cs`：验证镜头穿墙、普通障碍避让、显示恢复和 Collider 保留。

### 3. 调用流程

`CameraCo.LateUpdate -> ResolveCameraPosition -> SphereCastAll -> ShouldIgnoreHit -> CameraPassThroughOccluder -> 保持期望镜头位置`。

`CameraOcclusionController.LateUpdate -> 角色观察点到镜头的 RaycastNonAlloc -> 找到 CameraPassThroughOccluder -> Renderer.forceRenderingOff = true -> 遮挡消失后恢复原始值`。

### 4. 核心原理

镜头“被墙挡住”实际包含两个问题。第一层是物理避障：`CameraCo` 原本会从角色向镜头做球形检测，碰到墙就缩短距离，因此镜头突然贴近角色。现在墙体通过标记组件告诉相机“这个物体可以穿过”，只跳过这些墙，不会关闭主场景全部避障。

第二层是渲染遮挡：即使相机位置能够穿墙，墙的模型仍可能位于角色和镜头之间。遮挡控制器在相机完成移动后做一次无分配射线检测，只把命中的标记墙设置为 `forceRenderingOff`。该属性只影响 Renderer，不会关闭 BoxCollider，所以画面能看到角色，但玩家和 Boss 仍无法走出房间。

标记、镜头移动和遮挡表现拆成不同职责：标记描述物体属性，`CameraCo` 计算位置，遮挡控制器负责显示。射线缓冲、Renderer 引用和 HashSet 都会复用，避免每帧创建数组或反复查找组件。

### 5. Unity 测试方式

1. 打开 `BossRoomScene`，运行后靠近四面墙并旋转、拉远镜头，确认镜头不会突然贴近角色。
2. 让镜头移动到墙外，确认遮挡墙立即隐藏，角色和 Boss 仍可见。
3. 转回房间内部，确认墙体恢复；在墙角测试两面墙同时遮挡和恢复。
4. 尝试让角色或 Boss 穿墙，确认墙体 Collider 始终有效。
5. 返回 `MainScene` 靠近普通障碍物，确认原来的镜头避障仍然工作。
6. 在 Test Runner 运行 `BossRoomCameraOcclusionTests` 和完整 EditMode 测试。

### 6. 面试表达

Boss 房间的相机问题我分成了位置避障和视觉遮挡两层处理。原相机会用 SphereCast 防止穿墙，我没有全局关闭它，而是给 Boss 墙体增加可穿透标记，让相机只忽略这些对象。镜头穿到墙外后，再由独立遮挡组件用 RaycastNonAlloc 检测角色到镜头之间的墙，通过 `forceRenderingOff` 临时隐藏 Renderer，但保留 Collider。这样主场景避障不受影响，Boss 房间又能始终看到角色，并且通过缓存射线数组和 Renderer 避免了每帧 GC。

### 7. 面试追问

1. **为什么不能只关闭摄像机碰撞？** 镜头能穿墙不代表角色可见，镜头在墙外时墙的 Renderer 仍会挡住画面。
2. **为什么使用标记组件？** 可以精确指定哪些物体允许穿透，避免通过名字或全局 Layer 误伤其他障碍。
3. **隐藏墙体为什么不关闭 GameObject？** 关闭 GameObject 会连 Collider 一起失效，玩家和 Boss 可能跑出战斗区域。
4. **怎样避免每帧产生垃圾？** 使用 `RaycastNonAlloc` 固定缓冲区、缓存 Renderer，并复用 HashSet 保存当前遮挡物。
5. **切场景时如何防止墙体一直隐藏？** `OnDisable` 会恢复记录的 Renderer 原始状态并清空缓存。

### 8. 本次涉及知识点

- 第三人称摄像机球形避障
- 标记组件和职责拆分
- `Physics.RaycastNonAlloc`
- `Renderer.forceRenderingOff`
- Collider与Renderer的职责区别
- Unity脚本执行顺序与`LateUpdate`
- 缓冲区、HashSet和每帧GC控制
- 生命周期清理与状态恢复

## 功能名称：角色死亡全进度重置与自动存档

### 1. 实现目标

解决角色死亡后返回选角仍保留等级的问题。死亡事件现在会立即请求一份专用重置存档，把角色恢复为 1 级、0 经验、0 属性强化、0 Boss 轮数和 0 累计宝箱；角色槽位、名称、职业和历史最高分继续保留。正常暂停返回选角仍使用普通保存，不会误清进度。

### 2. 涉及脚本

- `CharacterProgressSaveService.cs`：定义保存模式、合并并串行执行自动存档，死亡模式优先级最高。
- `PlayerProgressSaveData.cs`：生成固定的死亡重置快照。
- `PlayerProgressionSystem.cs`、`PlayerCommands.cs`：把数据源确认后的 1 级数据同步到运行时，同时保持玩家死亡。
- `GameApiClient.cs`、`LocalGuestSaveService.cs`：向在线或游客数据源传递并校验死亡重置标记。
- 客户端与服务端 `message.cs`、`UserService.cs`：增加协议字段并只允许严格的 1 级空进度绕过防回档校验。
- `GameSessionUi.cs`、`ReStartPanel.cs`：返回和重开按钮等待存档完成，失败时停留在结算界面。

### 3. 调用流程

`PlayerCombatSystem.TakeDamage -> PlayerDiedEvent -> CharacterProgressSaveService.ResetAfterDeath -> PlayerProgressSaveData.ResetAfterDeath -> GameApiClient -> 游客JSON或服务端DB事务`。

保存成功后：`数据源返回 NCharacter -> 更新 GameApiClient/SelectedCharacterState 缓存 -> ResetPlayerProgressAfterDeathCommand -> PlayerProgressionSystem -> 重置技能、按分类同步背包、重置Boss和宝箱运行时状态`。

### 4. 核心原理

原问题不是没有死亡存档，而是旧逻辑只清除了属性强化，等级和经验仍按长期进度发送。在线服务端和游客存档还有防回档校验，因此客户端直接改成 1 级会被拒绝。

新的保存模式用优先级表达意图：普通保存、清本局强化、死亡全重置。多个请求重叠时只能提升优先级，死亡重置不会被按钮随后发起的普通保存覆盖。协议中的 `ResetAfterDeath` 不是无条件回档权限，数据源只接受“等级 1 且其余成长全部为 0”的固定数据，其他降级仍会被拒绝。

本地运行时只在数据源成功后重置，避免网络失败造成客户端与存档分叉。模型复用完整初始化公式撤销所有强化属性，再把当前生命设回 0，所以失败界面背后不会复活；下一场景才会按 1 级角色正常生成满血状态。

### 5. Unity 测试方式

1. 选择角色进入 `MainScene`，通过正常玩法或开发者模式提升等级并获得属性强化。
2. 击破宝箱、完成 Boss 后让角色死亡，观察失败界面并等待自动存档。
3. 点击“返回角色选择”，确认槽位显示 1 级、0 经验、0 Boss 和 0 宝箱累计。
4. 再次进入角色，确认基础属性和初始技能状态；药水应清空，材料与任务物品应保留，角色名称、职业和历史最高分也应保留。
5. 重复测试死亡后的“重新开始”，确认直接以 1 级进入且旧等级不会写回来。
6. 未死亡时从暂停界面返回，确认当前等级与进度正常保留。
7. 分别测试游客和在线账号，并运行 `CharacterProgressPersistenceTests`、`GuestModePersistenceTests`。

### 6. 面试表达

这个功能表面上是死亡清等级，实际涉及自动存档并发和服务端防回档。我把保存请求拆成普通、清强化和死亡重置三种模式，并设置优先级，避免死亡自动存档和结算按钮同时保存时旧数据覆盖新数据。服务端没有直接关闭防回档，而是增加一个死亡重置标记，只接受严格的 1 级空进度。数据库确认成功后，客户端再同步角色缓存和运行时模型，同时保持生命为 0。这样在线、游客和 UI 流程使用同一套规则，存档失败也不会带着错误状态切场景。

### 7. 面试追问

1. **为什么不能直接在客户端把等级设为 1？** 客户端不是最终可信数据源，现有防回档会拒绝降级，而且恶意客户端可以伪造任意进度。
2. **如何避免死亡存档被普通保存覆盖？** 保存模式按优先级合并，死亡重置最高；成功后还会先同步运行时模型，后续快照只能读到 1 级数据。
3. **为什么数据源成功后才清本地状态？** 如果先清本地但网络或磁盘写入失败，客户端和权威存档会出现分叉。
4. **为什么重置模型后角色不会复活？** 完整重算基础属性后显式把当前生命恢复为 0，只在新场景初始化时补满生命。
5. **新增协议字段会破坏旧客户端吗？** Protobuf 新字段默认为 false，旧客户端仍走普通保存；但新客户端的死亡重置能力需要与新服务端一起发布。

### 8. 本次涉及知识点

- 事件驱动自动存档
- 协程、防抖、请求串行化和优先级合并
- Protobuf 向后兼容字段
- 客户端不可信与服务端严格校验
- 防回档规则与受控降级
- 数据源确认后同步本地状态
- 运行时模型、角色缓存与场景状态清理
- 游客 JSON 和在线数据库双数据源测试

## 功能名称：登录界面完整 PC 设置面板

### 1. 实现目标

让登录界面左上角的设置按钮打开一个可复用模态面板，支持主音量、音乐、音效、鼠标灵敏度、分辨率、无边框/窗口模式、六档画质、垂直同步和帧率上限。设置保存在本机 PlayerPrefs；音量和灵敏度即时预览，显示设置需要点击应用，并在分辨率或窗口模式改变后提供 10 秒安全确认。

### 2. 涉及脚本

- `GameSettingsData.cs`：定义本机设置数据、显示模式和显示风险差异判断。
- `GameSettingsConfig.cs`：保存 AudioMixer、Music/Sounds 分组、默认值和可选帧率。
- `GameSettingsService.cs`：负责首场景前初始化、数据校验、PlayerPrefs 存储和 Unity 全局设置应用。
- `GameSettingsPanelController.cs`：维护打开快照与编辑草稿，处理即时预览、取消、应用和 10 秒回退。
- `GameSettingsAssetSetupTool.cs`：生成配置资产和公共 Prefab，并把登录按钮持久绑定到面板。
- `CameraCo.cs`：在原水平/垂直速度上乘本机灵敏度倍率。
- `GameConfig.cs`、`BossRoomSceneBootstrap.cs`：把主玩法与 Boss BGM 接入 Music 分组。
- `PlayerAudioComponent.cs`、`PlayerCo.cs`、`SlimeCo.cs`：把玩家和怪物音效接入 Sounds 分组。
- `GameSettingsTests.cs`：验证数据换算、存储、资源引用、Prefab 和场景装配。

### 3. 调用流程

启动流程：`RuntimeInitializeOnLoadMethod -> GameSettingsService.GetOrCreate -> Resources.Load<GameSettingsConfig> -> PlayerPrefs/默认数据 -> Sanitize -> AudioListener/AudioMixer/Screen/QualitySettings/Application`。

面板流程：`SettingButton -> GameSettingsPanelController.Open -> 保存 openingSnapshot -> 编辑 draft -> 滑块即时 PreviewAudioAndSensitivity -> Apply -> 普通设置直接 Save / 显示设置进入10秒确认 -> Keep 保存或 Revert 恢复快照`。

玩法接入：`CameraCo.Update -> GameSettingsService.MouseSensitivityMultiplier`；`运行时 AudioSource 初始化 -> RouteMusicSource/RouteSoundsSource -> Main.mixer 分组`。

### 4. 核心原理

设置面板没有直接把每次 UI 变化写进 PlayerPrefs，而是使用“快照 + 草稿”。打开时复制当前设置作为快照，玩家只修改草稿；音量与灵敏度虽然即时预览，但取消或切场景时会恢复快照。这样 UI 交互和正式数据不会混在一起。

分辨率和窗口模式可能导致黑屏，所以点击应用后先只改变运行时状态，不写入磁盘。玩家在 10 秒内确认才保存；取消、Esc、超时、对象禁用或切场景都会恢复整份旧快照。倒计时使用 `Time.unscaledDeltaTime`，以后复用到暂停界面时也不会被 `timeScale = 0` 冻结。

音量分成三层：主音量使用 `AudioListener.volume`，音乐和音效通过 AudioMixer 暴露参数控制。滑块的 0～1 线性值用 `20 * Log10(value)` 转为分贝，0%固定为 -80dB。音源只在没有 Inspector 自定义分组时自动路由，避免覆盖已有美术或音频配置。

全局服务在首场景加载前创建并 `DontDestroyOnLoad`，因此玩家即使直接从 MainScene 或 BossRoomScene 开始调试，也能读取同一份本机设置。分辨率列表会去重、过滤小于 1280×720 的普通选项，同时保留当前和已保存值，防止下拉框找不到正在使用的分辨率。

### 5. Unity 测试方式

1. 打开 `LoginScene`，确认 Canvas 最后一个子对象是 `GameSettingsPanel`，其根对象默认关闭，`SettingButton` 的 OnClick 指向 `Open`。
2. 运行场景并点击左上角齿轮，拖动三个音量和灵敏度滑块，确认百分比与预览立即变化；点取消后恢复。
3. 修改画质、VSync 和帧率，点击应用后关闭并重新打开，确认设置已保留；重启游戏确认 PlayerPrefs 持久化。
4. 修改分辨率或窗口模式并应用，确认出现 10 秒提示；分别测试保留、主动恢复、Esc 和等待超时。
5. 选择 MainScene 与 BossRoomScene，确认鼠标灵敏度生效，背景音乐受音乐音量控制，玩家和怪物声音受音效音量控制。
6. 在 Test Runner 运行 `GameSettingsTests` 和完整 EditMode 测试，检查 Console 没有 Mixer 参数、空引用或场景绑定错误。

### 6. 面试表达

我把 PC 设置拆成数据、全局服务和 UI 三层。数据层只描述音量、分辨率和性能选项；服务在首场景前创建，负责 PlayerPrefs、合法性校验以及 AudioMixer、Screen 和 QualitySettings 的实际应用；面板只维护打开快照和编辑草稿。音量和灵敏度支持即时预览，但取消会恢复快照。分辨率和窗口模式应用后不会立刻落盘，而是进入 10 秒确认，超时自动回退，避免错误显示设置让玩家无法操作。音乐和音效也统一路由到了 Mixer 分组，后续暂停界面可以直接复用同一服务和 Prefab。

### 7. 面试追问

1. **为什么设置数据不能直接由 UI 修改？** 草稿和正式数据分开后，取消、回退和切场景清理都有明确边界，不会产生半应用状态。
2. **为什么线性音量要转分贝？** AudioMixer 的暴露音量参数使用 dB；对数换算更符合声音强度的控制方式，0 需要使用一个足够低的有限值代替负无穷。
3. **怎样防止错误分辨率导致永久黑屏？** 先保存运行时快照，应用后启动不受暂停影响的倒计时，只有确认才写 PlayerPrefs，其他退出路径统一恢复快照。
4. **VSync 和帧率上限为什么不能同时控制？** 开启 VSync 时帧率由显示器刷新节奏控制，因此把 `targetFrameRate` 设为 -1 并禁用手动帧率选项；关闭后才应用玩家上限。
5. **后续如何接入暂停菜单？** 设置服务和 Prefab 都不依赖 LoginPanel，暂停 UI 只需实例化同一 Prefab 并调用 `Open`，倒计时也已使用 unscaled time。

### 8. 本次涉及知识点

- ScriptableObject 静态配置与 Resources 启动加载
- `RuntimeInitializeOnLoadMethod` 与 `DontDestroyOnLoad`
- PlayerPrefs、JsonUtility 和版本字段
- AudioMixer 分组、暴露参数与线性值/dB 换算
- `Screen.SetResolution`、`FullScreenMode` 和安全回退
- `QualitySettings`、VSync 与 `Application.targetFrameRate`
- UI 快照/草稿、模态遮罩和事件注册注销
- `Time.unscaledDeltaTime`
- Prefab/场景自动装配工具与 EditMode 资源回归测试

## 功能名称：角色背包分类持久化

### 1. 实现目标

解决背包只存在于运行时、返回选角或重新登录后物品全部丢失的问题。正常保存会保留角色全部背包；角色死亡时清除生命药水和魔法药水，同时保留经验结晶、Boss 核心和古代卷轴等材料或任务物品。

### 2. 涉及脚本

- `InventoryDatabase.cs`：根据稳定 `itemId` 查找物品静态配置。
- `InventorySystem.cs`、`InventoryCommands.cs`：生成背包快照、校验并恢复24格运行时背包。
- `PlayerProgressSaveData.cs`、`PlayerQueries.cs`：把背包加入角色成长快照，并在死亡模式中过滤消耗品。
- `NCharacter.cs`：保存角色背包格数据，并在跨层传递时进行深拷贝。
- `CharacterProgressSaveService.cs`、`SceneFlowService.cs`：监听背包变化、合并自动保存，并在进入角色时恢复权威背包。
- `GameApiClient.cs`、客户端与服务端 `message.cs`：传输格子下标、物品ID和数量。
- `LocalGuestSaveService.cs`：游客JSON背包保存、校验和版本1到版本2兼容。
- `TCharacter.cs`、`UserService.cs`、`DBService.cs`：服务端白名单校验、事务保存和背包子表读取。

### 3. 调用流程

普通保存：`拾取/使用物品 -> InventorySystem -> InventoryChangedEvent -> CharacterProgressSaveService防抖 -> GetPlayerProgressSaveDataQuery -> GameApiClient -> 游客JSON或服务端数据库`。

角色进入：`角色选择 -> 数据源返回NCharacter -> RestoreInventoryCommand -> InventorySystem.RestoreInventory -> InventoryChangedEvent刷新UI -> BeginSession开启后续自动存档`。

角色死亡：`PlayerDiedEvent -> ResetAfterDeath -> 删除Consumable快照 -> 数据源事务保存 -> 返回权威NCharacter -> RestoreInventoryCommand -> 运行时只保留材料和任务物品`。

### 4. 核心原理

物品的名称、图标和效果属于静态配置，玩家拥有的数量属于运行时数据，两者不能一起序列化。存档只记录 `slotIndex + itemId + count`，加载时再从 `InventoryDatabase` 找回对应 ScriptableObject。这样资源引用不会写入JSON或网络协议，资源重新加载后仍可通过稳定ID识别物品。

自动保存监听统一的 `InventoryChangedEvent`，拾取和使用物品不需要直接依赖存档服务。连续变化复用原有1秒防抖，降低磁盘和网络请求频率。进入角色时先恢复背包再开启存档会话，服务端确认后的死亡恢复也使用抑制标记，避免“加载数据”被误认为玩家再次修改。

在线数据库使用 `CharacterInventoryItems` 子表，以角色ID和格子下标作为联合主键。角色成长、属性强化和背包在同一个事务中写入，任何一步失败都会整体回滚。服务端还会检查24格边界、重复格子、物品白名单、堆叠上限以及死亡请求不能携带药水。

### 5. Unity 测试方式

1. 进入 `MainScene`，拾取药水和材料，打开背包确认格子与数量。
2. 进入 `BossRoomScene`再返回，确认运行时背包不变。
3. 从暂停界面保存并返回选角，再次进入同一角色，确认全部物品及原格子恢复。
4. 使用一瓶药水并等待约1秒，再返回选角，确认保存的是减少后的数量。
5. 让角色死亡并返回选角，确认药水清空，经验结晶、Boss核心和古代卷轴保留。
6. 分别用游客和在线账号重启客户端验证，并运行背包与角色存档专项测试。

### 6. 面试表达

我的背包持久化分成静态配置、运行时模型和存档DTO三层。ScriptableObject只描述物品是什么，InventoryModel记录当前24格状态，存档只发送格子下标、稳定物品ID和数量。背包变化通过事件触发防抖自动保存，正常退出保留全部物品，死亡时则根据物品分类移除消耗品、保留材料。在线端使用角色背包子表，并把成长、强化和背包放进同一事务，服务端还会校验物品白名单和堆叠上限。这样既避免Unity资源引用进入存档，也保证游客和在线账号使用同一套规则。

### 7. 面试追问

1. **为什么不直接序列化ScriptableObject？** 它是Unity资源引用，不适合跨客户端、服务器和JSON持久化；稳定ID更容易兼容资源重新加载和版本更新。
2. **为什么还要保存格子下标？** 只保存总数量会改变玩家的背包排列；格子下标可以精确恢复UI位置，并可检测重复或越界数据。
3. **怎样避免连续拾取造成大量网络请求？** 所有变化先发事件，存档服务把1秒内的变化合并为一次最新快照。
4. **为什么背包使用独立数据库表？** 背包是一个角色对应多条格子记录，用子表符合一对多关系，也方便事务更新和角色删除时级联清理。
5. **服务端能完全防止客户端伪造掉落吗？** 当前掉落仍由客户端结算，所以只能验证ID、数量和结构；进一步强化需要把掉落生成和拾取确认迁移到服务端权威逻辑。

### 8. 本次涉及知识点

- ScriptableObject静态配置与运行时数据分离
- DTO、稳定ID和深拷贝
- QFramework事件、Command和Query
- 防抖自动保存与权威状态恢复
- Protobuf字段兼容
- JSON存档版本迁移
- SQL一对多表、联合主键、外键和事务
- 客户端不可信、白名单和边界校验

## 功能名称：淘宝 UI 素材标准化导入

### 1. 实现目标

把 49 个淘宝 PSD 从设计源文件整理成 Unity 可直接使用的 UI Sprite 素材库。导出以实用组件为单位，移除英文示例文字和数值，保留按钮底图、图标、边框、进度条分层等可复用图形，同时保留原始 PSD/JPG 方便追溯。

### 2. 涉及脚本

- `Tools/UiAssetPipeline/export_ui_assets.py`：读取显式清单、解析 PSD 图层、过滤文字、导出 PNG，并生成中英文目录和分类预览图。
- `Tools/UiAssetPipeline/export_manifest.json`：记录来源 PSD、图层路径、输出名、中文用途、组件角色、排除规则和九宫格 Border。
- `PurchasedUiSpriteImportPostprocessor.cs`：只对淘宝 UI 专用目录应用 Unity Sprite 导入设置，并提供重导和校验菜单。
- `PurchasedUiSpriteImportRules.json`：把每张实际 PNG 和 Unity 九宫格规则对应起来。
- `PurchasedUiAssetLibraryTests.cs`：检查源文件数量、命名、功能图标数量、导入参数、九宫格和未完成下载文件。
- `Docs/UiAssetCatalog.md`、`Docs/UiAssetCatalog.csv`：按英文 Sprite 名或中文用途查询素材来源。

### 3. 调用流程

`原始 PSD -> export_manifest.json -> export_ui_assets.py -> RuntimeSprites 分类 PNG -> PurchasedUiSpriteImportPostprocessor -> Unity Sprite -> Image / Button / Slider 等 UI 组件`。

目录流程：`导出记录 -> CSV/Markdown 素材目录 -> 分类缩略预览图 -> 根据英文名或中文用途反查来源 PSD 和原图层路径`。

### 4. 核心原理

PSD 是设计源文件，运行时真正需要的是可以组合的图形组件。导出器不会把五千多个底层形状逐个输出，而是按按钮、面板、图标、背景和条形组件等语义层级合成；普通文字交给 TextMeshPro，这样文本可以本地化、动态变化，也不会因为放大而模糊。

按钮的渐变、描边和阴影合并为一张底图，血条和进度条拆成 Background、Fill、Frame，滑动条拆成 Track、Fill、Handle。可拉伸的矩形组件配置九宫格，让四角保持原尺寸、只拉伸中间区域；图标、圆形按钮、背景和 Fill 不设置 Border。

功能图标使用固定 104×104 透明画布，只做居中而不缩放图形本身，因此放到同一个 RectTransform 时更容易对齐和换色。内容像素、分类、组件角色和原图层语义完全一致时才共用一个 PNG，外观相近但用途不同的变体仍然保留。

Unity 后处理器先判断资源路径，只影响 `淘宝ui素材` 目录。运行时 PNG 统一导成 Single Sprite、Full Rect、100 PPU、Bilinear、Clamp、关闭 Mipmap并启用透明通道；PSD/JPG 保持 Default，只用于查看设计参考。

### 5. Unity 测试方式

1. 等 Unity 完成首次导入，在 Project 窗口打开 `Assets/AllResources/淘宝ui素材/RuntimeSprites`，确认十个分类目录和英文命名 Sprite。
2. 执行 `Tools/Treasure Hunter/UI Assets/Apply Import Settings`，统一重新应用导入设置。
3. 执行 `Tools/Treasure Hunter/UI Assets/Validate Library`，Console 应显示素材库校验通过且没有 Error。
4. 在 Sprite Editor 查看带 Border 的按钮或面板，确认九宫格不越界；把它放进 Image，选择 Sliced 后拉宽和拉高，确认四角不变形。
5. 分别组合血条的 Background、Fill、Frame，以及滑动条的 Track、Fill、Handle，确认层级和透明边缘正确。
6. 打开 `Docs/UiAssetCatalog.md` 和分类预览图，根据中英文名称反查来源 PSD 与图层路径。

### 6. 面试表达

我把购买的 49 个 PSD 做成了一套可复现的 UI 资源管线。离线工具用显式清单记录每个组件的来源图层、英文名、中文用途和九宫格参数，导出时按组件语义合并效果、过滤示例文字，并把进度条和滑动条拆成可动态控制的层。Unity 端用限定目录的 AssetPostprocessor 统一 Sprite 导入设置，不会影响项目其他贴图；另外提供菜单校验和 EditMode 测试，检查命名、数量、透明通道、Border 与导入参数。这样资源不仅能用，还能从运行时文件追溯回 PSD，后续换版本也可以稳定重导。

### 7. 面试追问

1. **为什么不直接把 PSD 当运行时资源？** PSD 图层多、导入开销大且包含示例界面；运行时 PNG 更可控，PSD 只保留作设计源和追溯依据。
2. **为什么文字不一起切进按钮？** TextMeshPro 才能支持动态文本、本地化、清晰缩放和无障碍字号；底图与文字分离也更容易复用。
3. **九宫格解决什么问题？** 它固定四角和边缘厚度，只拉伸中间区域，避免圆角、描边和阴影随 RectTransform 一起变形。
4. **怎样防止 AssetPostprocessor 影响其他图片？** 在预处理入口先判断规范化资源路径，只有专用素材根目录和 RuntimeSprites 子目录会进入对应设置分支。
5. **如何保证重新导出不会静默切错层？** 清单同时记录图层索引和完整路径，Dry Run 会重新解析并比较路径，还会检查 PSD 数量、缺失图层和大小写重名。

### 8. 本次涉及知识点

- PSD 图层树、合成范围和透明通道
- 数据驱动导出清单与可复现资源管线
- Unity TextureImporter 与 AssetPostprocessor
- Sprite Mesh Type、PPU、Filter、Wrap、Mipmap 和 Max Size
- UGUI Image 的 Simple、Sliced、Filled 模式
- 九宫格 Border 与可拉伸 UI
- TextMeshPro 与图文分离
- 内容哈希去重、命名规范和资源追溯
- Editor 菜单、批处理导入和 EditMode 资源测试

## 功能名称：淘宝 UI 设置界面换肤

### 1. 实现目标

把登录场景原来的居中设置弹窗替换成 1920×1080 全屏淘宝 `Setting.psd` 风格页面，同时保留主音量、音乐音量、音效音量、鼠标灵敏度、分辨率、显示模式、画质、垂直同步和帧率上限等全部 PC 设置功能。换肤只改变表现层，不修改设置数据、存档、AudioMixer 或运行时业务规则。

### 2. 涉及脚本

- `GameSettingsAssetSetupTool.cs`：集中加载淘宝 Sprite，生成全屏两栏设置 Prefab，并替换 LoginScene 的设置入口图标和点击绑定。
- `GameSettingsTests.cs`：检查设置页背景、Slider 三层、VSync 双状态、Dropdown 标准层级、确认弹窗和场景入口绑定。
- `GameSettingsPanel.prefab`：生成后的共享设置界面，继续由原 `GameSettingsPanelController` 驱动。
- `LoginScene.unity`：保存新的 Prefab 实例，并让淘宝齿轮按钮调用设置面板的 `Open`。

### 3. 调用流程

打开页面：`LoginScene SettingButton -> GameSettingsPanelController.Open -> 读取当前设置 -> 填充 Slider / Dropdown / Toggle -> 显示全屏设置页`。

应用设置：`玩家修改控件 -> Controller 更新草稿 -> 点击应用 -> GameSettingsService 应用并保存 -> 若分辨率或显示模式变化，打开淘宝确认弹窗 -> 保留或在 10 秒后恢复`。

返回页面：`顶部淘宝返回箭头或 Esc -> Controller.Cancel -> 放弃未应用草稿 -> 关闭设置页`。

### 4. 核心原理

这次修改把“界面长什么样”和“设置怎样生效”分开处理。`GameSettingsPanelController` 仍然只负责读取控件、维护草稿和调用设置服务；Editor 生成工具负责创建 RectTransform、绑定 Sprite 和组织控件层级。因此以后再次生成 Prefab 不会恢复旧皮肤，换另一套美术资源时也不必改设置业务逻辑。

Slider 仍使用 UGUI 的 `fillRect` 和 `handleRect`，但可见图片分别换成淘宝 Background、Fill、Handle。Toggle 的底图表示 Off，勾选图表示 On；Dropdown 保留 `Template/Viewport/Content/Item` 标准结构，避免换肤后破坏展开、滚动和选择逻辑。

按钮、Dropdown 和弹窗底板使用 Sliced 九宫格，只拉伸中间区域，保护圆角、描边和阴影；图标、背景、Fill 与 Handle 使用 Simple。所有中文标题和动态数值仍由 `Text` 渲染，没有烘焙进图片，因此可以继续按运行时数据更新。

### 5. Unity 测试方式

1. 执行 `Treasure Hunter/UI/Create Or Refresh Login Settings`，等待 Console 编译和资源刷新完成。
2. 打开 `LoginScene`，点击淘宝齿轮，确认出现全屏背景、顶部返回、左右两栏和底部按钮。
3. 分别拖动四个 Slider、切换三个显示 Dropdown、VSync 和帧率上限，确认文字与草稿同步变化。
4. 点击恢复默认、应用、顶部返回和 Esc，确认原有行为不变。
5. 改变分辨率或显示模式并应用，确认淘宝确认弹窗出现；验证保留设置、恢复设置和 10 秒自动回退。
6. 在 1920×1080、2560×1440 和窗口模式下观察布局与 Dropdown 展开层级，并运行 `GameSettingsTests`。

### 6. 面试表达

我给项目的 PC 设置页做了一次表现层换肤，但没有改原来的设置业务逻辑。我把淘宝 PSD 切出的背景、按钮、Slider 三层、开关和图标集中放进 Editor 生成工具，由工具稳定生成 Prefab 和场景引用；运行时仍由原 Controller 维护设置草稿、存档和显示回退。可拉伸控件使用九宫格，Slider、Toggle 和 Dropdown 保留 UGUI 规定的功能层级，并用 EditMode 测试检查 Sprite 来源和引用完整性。这样既降低了美术资源与业务逻辑的耦合，也避免后续重生成 Prefab 时丢失皮肤修改。

### 7. 面试追问

1. **为什么不直接在 Prefab 上手工换图？** 这个 Prefab 原本由 Editor 工具生成，手工修改下次刷新会丢失；把皮肤写入生成源才能保证结果可复现。
2. **Slider 为什么要拆成三张图？** Background 表示总范围，Fill 表示当前值，Handle 表示交互位置；UGUI 可以分别控制它们，动态变化时不需要重新生成图片。
3. **Dropdown 换肤最容易出什么问题？** 如果删除或改坏 `Template/Viewport/Content/Item`，下拉列表会无法展开、裁剪或选择，所以这次只替换可见 Graphic，不改变标准结构。
4. **九宫格为什么适合按钮和弹窗？** 它固定四角与边缘，只拉伸中心，按钮尺寸变化时圆角、描边和阴影不会被整体拉扁。
5. **怎样证明换肤没有影响业务？** 没有修改 Controller、Service、配置和存档文件；同时原数据规则测试与新增的 Prefab/场景资源测试全部通过。

### 8. 本次涉及知识点

- UGUI Image、Button、Slider、Toggle 和 Dropdown 层级
- RectTransform 锚点与 1920×1080 参考分辨率
- Sprite Simple 与 Sliced 九宫格
- 表现层和业务逻辑分离
- Editor 工具生成 Prefab 与场景持久绑定
- SerializedObject 自动绑定私有序列化字段
- UnityEvent 持久监听
- EditMode Prefab、Sprite 和场景回归测试
## 功能名称：Boss 装备系统与淘宝装备背包 UI

### 1. 实现目标

在原 24 格背包上增加六个穿戴槽，使 Boss 每次死亡额外掉落一件四职业通用装备。装备固定属性会实时影响玩家最终面板，并按角色保存；死亡只清理局内消耗品和强化，不清理背包装备或穿戴状态。

### 2. 涉及脚本

- `EquipmentTypes.cs`：稳定槽位、属性枚举、固定属性修改器和操作结果。
- `EquipmentModel.cs`：只保存六个槽位当前穿戴的数据。
- `EquipmentSystem.cs`：处理穿戴、原子交换、卸下、属性差值结算和存档恢复。
- `InventoryPanel.cs` / `EquipmentSlotView.cs`：显示淘宝全屏装备背包并发送装备命令。
- `BossLootDropController.cs` / `InventoryDatabase.cs`：保留三个材料球并追加一个独立装备球。
- `LocalGuestSaveService.cs`、客户端/服务端协议和 `DBService.cs`：完成角色级永久存档与服务端边界校验。

### 3. 调用流程

`B 输入 -> InventoryPanel -> EquipInventoryItemCommand -> EquipmentSystem -> InventorySystem + EquipmentModel -> PlayerRuntimeStats -> EquipmentChangedEvent / PlayerStatsChangedEvent -> UI 与自动存档`

`BossDied -> BossLootDropController -> InventoryDatabase 独立装备权重池 -> WorldLootPool -> WorldItemPickup -> AddInventoryItemCommand`

### 4. 核心原理

装备配置描述“这件物品是什么、属于哪个槽、加什么属性”，背包和装备模型描述“玩家现在拥有什么、穿着什么”，UI 只负责显示和发送命令。换装时旧装备直接写回新装备所在的来源格，所以其余格子全满也能安全交换；卸下则先确认有空格，失败时两个模型都不改变。属性不是在每次穿戴时盲目累加，而是重新汇总六个槽位，用新旧总值差更新玩家属性，因此重复穿脱不会漂移。最大血蓝变化保留当前比例，成长计算会先排除装备部分再套成长公式。

游客存档升级到 v3，旧 v1/v2 因没有装备字段会迁移为空装备栏。在线模式把角色主表、强化、背包和穿戴栏放在同一数据库事务中，服务端校验物品白名单、重复槽位和物品槽位是否匹配。

### 5. Unity 测试方式

打开 `MainScene`，选择任意职业进入游戏。击败 Boss 后应看到三个材料球和一个装备球；拾取后按 `B`，在右侧选中装备点击“装备”，左侧对应槽位出现图标且最终属性立即变化。再次拾取同槽装备可直接交换；选中左侧装备可卸下。1-9 级戒指槽显示锁定，10 级后可穿戴。切场景、死亡和重新登录后检查背包装备与穿戴状态仍存在。

### 6. 面试表达

这个装备系统我拆成配置、运行时模型、规则系统和 UI 四层。装备用 ScriptableObject 配置槽位与固定属性，背包只记录拥有状态，EquipmentModel 只记录穿戴状态，所有写操作统一经过 EquipmentSystem。换装采用来源格原子交换，所以背包满时也不会丢装备；属性用整套装备汇总后的差值结算，避免重复穿脱产生数值漂移。持久化同时支持游客 JSON 和在线数据库，在线端会在同一事务中保存背包与装备并做白名单、重复槽位和槽位匹配校验。

### 7. 面试追问

1. 为什么 UI 不直接改装备槽？答：避免绕过等级、容量和槽位校验，也便于存档、测试和以后联网复用同一规则。
2. 背包满了为什么还能换装？答：旧装备直接替换来源格的新装备，不需要寻找额外空格。
3. 如何避免属性重复叠加？答：每次汇总六槽总属性，只把新旧差值应用到玩家最终属性。
4. 为什么最大生命切装要保留比例？答：避免通过反复切装免费回血，也让战斗中的换装结果连续。
5. 后续随机词条怎么扩展？答：把固定修改器扩成装备实例数据，静态定义仍保存基础信息，实例单独保存词条和强化等级。

### 8. 本次涉及知识点

- ScriptableObject 数据驱动与静态数据/运行时数据分离
- Command、Query、Event 与 UI/业务解耦
- 原子状态交换、失败不变式和事件驱动刷新
- 属性差值结算、百分比属性上限和血蓝比例保持
- 对象池复用 Boss 世界掉落物
- JSON 版本迁移、protobuf 字段兼容、数据库事务与白名单校验
## 功能名称：背包装备 UI 手动排版入口

### 1. 实现目标

把共享的背包装备界面直接开放到 Unity Prefab Mode 中编辑，让布局问题可以通过 Scene 视图手动调整并保存，同时避免常规装备数据生成流程覆盖美术排版。

### 2. 涉及脚本

- `InventoryFeatureSetupTool.cs`：提供一键打开并定位背包装备窗口的编辑器菜单，同时让批处理入口保留现有 UI。
- `GameplayUiRoot.prefab`：背包装备布局的唯一保存位置，MainScene 与 BossRoomScene 共用这份 Prefab。

### 3. 调用流程

Unity 菜单 -> `OpenEquipmentInventoryLayoutForEditing` -> 打开 `GameplayUiRoot.prefab` -> 进入 Prefab Mode -> 选中 `InventoryOverlay/InventoryWindow` -> Scene 视图手动排版 -> `Ctrl+S` 保存 Prefab

### 4. 核心原理

Prefab 可以理解成多场景共同使用的 UI 模板。布局直接保存在共享 Prefab 上，比把 Prefab 拆开放进每个场景更安全：只需调整一次，两个玩法场景就会同步更新，也不会产生两份逐渐不一致的 UI。运行时仍由 `InventoryPanel.Start` 隐藏界面，因此 Prefab 编辑态保持可见不会导致开局自动打开背包。

常规 Setup 与批处理现在只补齐装备数据和缺失资源；已经存在 `InventoryPanel` 时不会重建 UI。只有明确点击带 `Overwrites Manual Layout` 字样并经过二次确认的菜单，才会恢复默认布局。

### 5. Unity 测试方式

1. 点击 `Tools/Treasure Hunter/UI/Edit Equipment Inventory Layout`。
2. 在打开的 Prefab Mode 中调整 `InventoryWindow` 子节点位置与尺寸。
3. 按 `Ctrl+S` 保存，然后退出 Prefab Mode。
4. 运行 MainScene，按 `B` 检查新布局；再进入 BossRoomScene 检查布局是否同步。

### 6. 面试表达

背包装备 UI 是两个玩法场景共用的，所以我没有把 Prefab Unpack 成两份场景对象，而是提供了一个编辑器入口，直接进入共享 Prefab 的排版节点。策划或美术可以在 Scene 视图里调整并保存，两个场景会自动同步。同时我把数据生成和 UI 重建拆开，普通配置刷新不会覆盖手动布局，只有显式确认的恢复菜单才会重建默认 UI。

### 7. 面试追问

- 为什么不直接 Unpack？答：Unpack 后每个场景会形成独立副本，后续容易出现布局不一致。
- 编辑态显示会不会导致运行时开局显示？答：不会，运行时由 `InventoryPanel.Start` 统一隐藏。
- 怎么避免生成工具覆盖美术调整？答：常规生成只补数据，已有 UI 不重建；破坏性重建放在单独的确认菜单中。
- 为什么选择 Prefab Mode？答：它能隔离编辑共享资源，并且修改结果会传播到所有 Prefab 实例。
- 如果节点被误删怎么办？答：可以使用带二次确认的默认 UI 重建菜单恢复结构。

### 8. 本次涉及知识点

- Unity Prefab Mode 与共享 Prefab
- `AssetDatabase.OpenAsset`
- `PrefabStageUtility`
- 编辑器菜单与 Selection 定位
- 数据生成流程和美术资源编辑解耦

## 功能名称：Fungi 商人、金币掉落与淘宝商店系统

### 1. 实现目标

把出生点附近的 Fungi 改造成可交互商人。当前角色第一次靠近按 `E` 只显示引导台词并立即记录，之后按 `E` 直接打开全屏淘宝商店。小怪、金库和 Boss 提供分层金币产出，金币、首次对话与装备限购记录都按角色永久保存，死亡不会清除。

### 2. 涉及脚本

- `EconomyModel.cs` / `EconomySystem.cs`：保存并校验当前角色金币，统一处理增加、消费、恢复和 9,999,999 上限。
- `ShopModel.cs` / `ShopSystem.cs`：维护首次对话与限购集合，并协调目录、钱包和背包完成原子购买。
- `ShopCatalog.cs` / `EconomyConfig.cs`：分别配置 16 个固定商品和怪物、金库、Boss 的金币平衡。
- `MerchantNpcController.cs`：检测三米范围内的玩家，把 `E` 输入转换成首次对话或打开商店事件。
- `MerchantShopPanel.cs` / `ShopItemCardView.cs` / `GoldHudView.cs`：显示交互提示、对话、分类商品卡、购买反馈和常驻金币。
- `MonsterGoldRewardController.cs`：监听 Slime 正式死亡事件，金币直接到账，不创建大量地面对象。
- `VaultGoldRewardController.cs` / `BossGoldRewardController.cs`：生成重要金币地面拾取物，并保留已有材料和装备掉落。
- `WorldGoldPool.cs` / `WorldGoldPickup.cs`：复用金库与 Boss 金币对象，负责悬浮、旋转、90 秒生命周期和触碰收取。
- `ShopFeatureSetupTool.cs`：可重复生成配置、六件入门装备、金币与商人 Prefab、淘宝商店 UI，并装配 MainScene。
- `LocalGuestSaveService.cs`、网络协议、`DBService.cs`、`UserService.cs`：完成游客 v4 迁移和在线同事务保存、白名单及边界校验。

### 3. 调用流程

首次交互：`InputCo(E) -> MerchantNpcController -> CompleteMerchantIntroCommand -> ShopSystem -> MerchantIntroCompletedEvent（立即存档） -> MerchantDialogueRequestedEvent -> MerchantShopPanel`。

再次交互和购买：`MerchantNpcController -> ShopOpenRequestedEvent -> MerchantShopPanel -> PurchaseShopItemCommand -> ShopSystem 只读预检 -> EconomySystem + InventorySystem + ShopModel -> GoldChangedEvent / InventoryChangedEvent / ShopPurchaseCompletedEvent -> UI 刷新与立即存档`。

金币产出：`SlimeCo.Died -> MonsterGoldRewardController -> AddGoldCommand`；`BoxCo.OnVaultDestroyed / BossDied -> 奖励控制器 -> WorldGoldPool.Get -> WorldGoldPickup.OnTriggerEnter -> AddGoldCommand -> Release`。

### 4. 核心原理

这个功能把静态配置、角色状态、业务规则和 UI 分开。`ShopCatalog` 只描述卖什么和价格，`EconomyModel` 与 `ShopModel` 只记录当前角色的金币与购买进度，所有写操作必须通过 System。UI 因此不能绕过金币、背包容量或限购校验，未来换皮肤时也不用改经济规则。

购买前先完成目录、限购、余额和背包容量四项只读检查；任何一项失败都不改变状态。通过后在 Unity 主线程内完成扣款和加物品，极端异常会退款，最后才写限购并广播完成事件。这种设计强调“失败时状态不变”的事务思路。当前服务端不宣称校验真实击杀来源，但会校验金币范围、物品白名单、限购 ID、重复记录和槽位结构。

普通怪一次有 18 只，所以采用死亡后直接到账，避免同时生成大量金币对象与物理触发器；金库和 Boss 的高价值奖励需要拾取表现，才使用独立对象池。两类奖励共享同一钱包命令，但表现和性能策略不同。

商店、对话、背包和暂停都需要控制 `Time.timeScale` 与鼠标状态。`GameSessionUi` 统一维护优先级：对话/商店在最上层，之后是背包和暂停；`Esc` 一次只关闭当前最上层，商店打开时 `B` 和重复 `E` 会被阻挡。

游客存档从 v1-v3 迁移到 v4 时补成 0 金币、未对话和空限购集合。在线存档把金币、成长、背包、装备和限购记录放在同一 SQL 事务内，任一子表写入失败都会整体回滚。死亡快照只重置成长和消耗品，经济字段原样进入同一次保存，因此不会丢失刚获得但仍在防抖窗口内的金币。

### 5. Unity 测试方式

1. 打开 `MainScene`，确认出生点附近仍是原位置、旋转和大小的 Fungi；靠近约三米后显示“按 E 与 Fungi 交谈”。
2. 第一次按 `E`，确认只出现指定台词；关闭后再次按 `E`，应直接打开 1920×1080 淘宝 ShopChest 商店。
3. 检查全部、消耗品、装备、材料四个分类；购买药水应可重复，购买装备后卡片应显示“已售罄”。
4. 用金币不足和 24 格背包全满两种情况购买，确认不扣金币、不加物品、不写限购。
5. 清理 12 只 Slime1 与 6 只 Slime2，确认金币直接到账；击破金库和 Boss 后各出现一个金色悬浮拾取物，原材料球和装备球数量不变。
6. 购买铜纹戒指，低于十级时可以买但不能穿；十级后复用装备系统正常穿戴。
7. 验证 `Esc`、顶部返回、`B` 冲突、暂停与鼠标状态；再测试切场景、死亡、退出重登和四角色存档隔离。
8. 运行 `EconomyShopSystemTests`、`ShopFeatureAssetTests`、`GuestModePersistenceTests` 与 `CharacterProgressPersistenceTests`，并执行服务端 `dotnet build`。

### 6. 面试表达

我把商店系统拆成经济模型、商店进度模型、业务系统和表现层。商品与金币产出由 ScriptableObject 配置，UI 只发送购买命令；ShopSystem 会按目录、限购、余额和背包容量顺序预检，失败时不改变任何状态，成功后统一更新钱包、背包和限购记录。普通怪金币直接到账以减少对象数量，金库和 Boss 的高价值金币使用独立对象池提供拾取表现。持久化同时支持游客 JSON 版本迁移和在线 SQL 事务，金币、装备、背包与限购会作为一个角色快照保存，并由服务端做结构和白名单校验。

### 7. 面试追问

1. **为什么钱包不直接放在商店 UI 中？** 钱包还会被怪物、任务和拾取物使用，放进独立 EconomySystem 才能保证所有来源共享上限、事件和存档规则。
2. **怎样保证购买失败不扣钱？** 先做全部只读预检，再在主线程内提交；背包写入出现理论外异常时还有退款保护，限购记录只在成功后写入。
3. **为什么普通怪和 Boss 的金币表现不同？** 18 只普通怪同时生成物理对象会增加实例、Collider 和 GC 压力；重要奖励数量少，适合用对象池保留反馈感。
4. **客户端结算金币安全吗？** 当前是作品原型，服务端只做结构、范围和白名单校验，不宣称验证击杀；商业联网项目应由权威服务端结算掉落或校验战斗事件。
5. **首次对话为什么按角色保存？** 每个角色的引导进度独立，角色切换不会串档；完成事件立即保存，避免玩家看完台词后立刻退出又重复出现。
6. **为什么要数据库事务？** 主表、背包、装备和限购子表必须同时成功，否则可能出现扣了金币却没有物品，或已有商品又能重复购买。
7. **对象池回收时最重要的是什么？** 重置金额、生命周期、Transform 和配置状态，防止上一次拾取物的数据残留到下一次复用。

### 8. 本次涉及知识点

- QFramework Command、Query、Event 与领域模型
- ScriptableObject 商品目录和数值配置
- 事务式购买、预检、退款保护和失败不变式
- UGUI ScrollRect、Mask、GridLayoutGroup 与模态优先级
- Trigger、运动学 Rigidbody 与玩家身份识别
- 对象池生命周期、状态重置和性能取舍
- 防抖保存与关键事件立即保存
- JSON v1-v3 到 v4 迁移和角色隔离
- protobuf 向后兼容字段编号
- SQL 主表、子表、事务回滚和服务端白名单校验

## 功能名称：商人对话与商品卡可读性、鼠标锁定修复

### 1. 实现目标

修复 Fungi 首次对话和商店商品卡在深色淘宝底板上看不清、文字与图标重叠，以及关闭商店后鼠标没有重新锁定的问题。修复只调整商人 UI 的指定子节点，不重建背包装备 UI，也不改变购买、金币和存档规则。

### 2. 涉及脚本

- `ShopFeatureSetupTool.cs`：更新新生成商店的默认颜色与排版，并提供只修复现有节点的非破坏性菜单。
- `MerchantShopPanel.cs`：关闭最后一个商人模态时确定性恢复玩法鼠标状态。
- `UiCursorStateUtility.cs`：新增统一的“锁定并隐藏鼠标”入口。
- `ShopFeatureAssetTests.cs` / `UiCursorStateTests.cs`：保护文字对比度、卡片分区、淘宝底板和鼠标状态。

### 3. 调用流程

视觉生成：`ShopFeatureSetupTool -> BuildMerchantUi/CreateProductCard -> 高对比颜色 + Outline + 固定 RectTransform 分区 -> GameplayUiRoot.prefab`。

退出商店：`返回按钮或 Esc -> MerchantShopPanel.CloseShop -> RestoreGameplayStateIfNoModal -> UiCursorStateUtility.EnsureHiddenAndLocked -> 镜头重新接收鼠标移动`。

### 4. 核心原理

UGUI 的 `offsetMin/offsetMax` 在锚点重合时表示矩形边界，而不是“位置和大小”。旧商品名称把 `offsetMax.y` 写成正数，导致文字区域越过卡片顶部并压到图标上。现在把卡片按从顶部向下的固定区域分成图标、名称、说明、价格和按钮，各区域之间保留间距，因此不会依赖子节点渲染顺序来掩盖重叠。

深色背景上的深棕文字即使字号足够也缺少亮度对比。修复使用暖白和金色前景，并增加深色一像素描边，让文字在不同图标颜色和分辨率下仍然清楚。

鼠标状态属于全局状态，而且会跨场景或模态界面保留。商店关闭时恢复缓存值会把打开前偶然的解锁状态继续保留下来。由于商人交互只能从正常玩法进入，关闭最后一个商人模态时直接恢复 `Locked + hidden` 更符合确定性状态切换。

### 5. Unity 测试方式

1. 运行 MainScene，首次与 Fungi 对话，检查名称为金色、正文为暖白色且没有压在固定装备图案上。
2. 再次按 E 打开商店，检查商品图标上方和内部没有文字遮挡，名称、属性说明和价格均清楚可见。
3. 切换全部、消耗品、装备和材料分类，重点检查带两条属性和等级提示的装备卡。
4. 分别通过顶部返回和 Esc 关闭商店，确认鼠标隐藏、锁定并重新控制镜头。
5. 在 1920×1080、2560×1440 和窗口模式下复核五列布局。

### 6. 面试表达

这次问题不是单纯换一个字体颜色。我先检查了淘宝底板的实际明暗和 RectTransform 数据，发现旧代码误把 offsetMax 当成高度，导致名称区域跨过卡片顶部并覆盖图标。我把商品卡划分成互不重叠的固定区域，再用高对比颜色和 Outline 保证可读性。鼠标恢复则从“恢复可能错误的缓存值”改成确定性的玩法状态切换，同时保留 Time.timeScale 的原值恢复。

### 7. 面试追问

- `offsetMin` 和 `offsetMax` 是什么？答：它们是 RectTransform 相对锚点矩形的下左、上右边界偏移，具体含义会随锚点是否拉伸而变化。
- 为什么不用调整子节点顺序解决遮挡？答：顺序只能决定谁画在上面，不能解决两个控件占用同一区域的问题。
- 为什么还要加 Outline？答：商品图标颜色不固定，描边可以在浅色和深色局部背景上同时保持文字边缘清晰。
- 为什么关闭商店不恢复 Cursor 缓存？答：商店只允许从正常玩法打开，目标状态明确；恢复偶然缓存值反而会传播错误状态。
- 怎样避免生成工具覆盖手调布局？答：默认生成值和当前 Prefab 都修正，但现有资源使用定点修复，只更新指定子节点，不删除整棵 UI。

### 8. 本次涉及知识点

- RectTransform 锚点、Pivot、offsetMin/offsetMax
- UGUI 渲染顺序与真正布局冲突的区别
- Text Outline 和颜色对比度
- 全局 Cursor 状态与确定性状态恢复
- PrefabContents 非破坏性编辑
- UI 资源生成器与当前 Prefab 同步

## 功能名称：商店金币显示、扣款反馈与鼠标滚轮浏览

### 1. 实现目标

商店每次打开时显示当前角色的真实金币余额，购买成功后立即显示扣除金额并用颜色变化强化反馈。商品列表提高鼠标滚轮灵敏度，并在重新打开商店或切换分类时回到顶部，解决默认灵敏度过低造成的“看起来不能滚动”问题。

### 2. 涉及脚本

- `MerchantShopPanel.cs`：查询并显示金币、监听购买结果、播放扣款高亮、控制商品列表滚动位置。
- `ShopFeatureSetupTool.cs`：为新生成和已有的商店 Prefab 统一配置 ScrollRect，并保存面板引用。
- `ShopFeatureAssetTests.cs`：验证金币文本、滚动方向、灵敏度、Viewport 和 Content 引用。
- `GameplayUiRoot.prefab`：保存 ProductScroll 的灵敏度以及 MerchantShopPanel 对 ScrollRect 的引用。

### 3. 调用流程

打开商店：`MerchantNpcController -> ShopOpenRequestedEvent -> MerchantShopPanel -> GetGoldQuery -> EconomySystem -> ShopGoldText`。

购买商品：`ShopItemCardView -> MerchantShopPanel -> PurchaseShopItemCommand -> ShopSystem -> EconomySystem.TrySpendGold -> GoldChangedEvent -> 刷新余额 -> ShopPurchaseCompletedEvent -> 扣款 Toast + 金币高亮`。

浏览商品：`鼠标滚轮 -> EventSystem -> ScrollRect.OnScroll -> Content 纵向移动`。

### 4. 核心原理

金币真实数据仍由 EconomySystem 管理，商店 UI 不保存自己的余额副本。打开商店时主动查询一次，余额变化时再通过事件刷新，这样既能保证首次显示正确，也不需要在 Update 中每帧查询。

购买反馈只在 ShopPurchaseCompletedEvent 后播放，因此金币不足、背包已满或退款保护触发时不会误报成功。由于商店打开后游戏时间暂停，高亮恢复使用 WaitForSecondsRealtime，不受 Time.timeScale 为 0 的影响。

ScrollRect 原本已经具备 Viewport、Mask 和自动扩展高度的 Content，但默认灵敏度 1 每格滚轮只能移动约一个像素。提高灵敏度即可恢复正常手感，不需要重建商品 UI。分类变化时只重建一次布局并把 normalizedPosition 设为顶部，不产生每帧布局开销。

### 5. Unity 测试方式

1. 打开 `MainScene`，运行游戏并获得一定金币。
2. 靠近 Fungi，完成首次对话后再次按 E 打开商店。
3. 检查右上角“当前金币”是否与 HUD 一致。
4. 购买一件商品，检查余额立即减少，Toast 显示“金币 -价格”，金币文字短暂变为橙红色后恢复。
5. 金币不足时再次购买，确认没有扣款高亮且余额不变。
6. 鼠标放在商品卡和商品区空白位置滚轮，确认只能上下滚动。
7. 滚到底部后切换分类或关闭重开商店，确认列表回到顶部。

### 6. 面试表达

商店金币显示我没有放在 Update 里轮询，而是采用“打开时主动查询、变化时事件刷新”的方式。购买命令由 ShopSystem 统一校验余额、背包容量和限购，只有收到购买成功事件后，UI 才显示扣款金额和高亮，所以失败不会产生错误反馈。商店会暂停游戏时间，因此反馈协程使用真实时间。商品列表本身已有 ScrollRect，问题是默认滚动灵敏度只有 1，我保留原布局，只提高纵向灵敏度并在切换分类时重置到顶部。

### 7. 面试追问

1. **为什么打开商店还要主动查询金币？** 事件只能通知之后发生的变化，主动查询可以保证 UI 第一次显示时就拿到当前状态。
2. **为什么不让 UI 直接扣金币？** 钱包规则属于业务层，放在 System 中才能被商店、任务和掉落共同复用，并保证失败不改变余额。
3. **为什么高亮使用 WaitForSecondsRealtime？** 商店打开时 Time.timeScale 为 0，普通 WaitForSeconds 不会继续计时。
4. **为什么提高灵敏度而不是自己读取鼠标滚轮？** ScrollRect 已统一处理拖动、惯性、边界和事件冒泡，复用标准组件更简单可靠。
5. **为什么分类切换时强制重建布局？** 商品显隐会改变 Content 高度，先更新布局再设置顶部位置，ScrollRect 才能用正确边界计算滚动位置。

### 8. 本次涉及知识点

- QFramework Command、Query、Event
- UGUI ScrollRect、Viewport、Mask 与 ContentSizeFitter
- LayoutRebuilder 与事件触发式 UI 刷新
- Coroutine 与 WaitForSecondsRealtime
- 购买事务、失败不变式和表现层解耦
- Prefab 序列化引用与编辑器资产测试

## 功能名称：F1 热键开发者模式

### 1. 实现目标

把原来只支持少量 L/P/O/N 快捷键的调试组件改成 F1-F8 开发者模式。它可以临时开启极高攻击、无敌和技能零冷却，也能通过正式业务入口增加金币、补足本轮五次宝箱、升一级和回满蓝，方便快速验证四职业、商店、Boss 入口与技能战斗。

### 2. 涉及脚本

- `DeveloperModeModel/System`：保存并处理不进入存档的高攻、无敌和零冷却状态。
- `DeveloperModeCommands/Queries`：为 MonoBehaviour 和战斗系统提供统一读写入口。
- `PlayerDeveloperModeComponent`：读取 F1-F8、调用 Command 并显示 IMGUI 状态概览。
- `PlayerCombatSystem` / `PlayerSkillSystem`：在正式伤害和技能规则入口读取临时开发者状态。
- `BoxCo`：连续执行正式击破结算，并只跳过开发测试中的重生等待表现。
- `DeveloperModeSystemTests`：验证状态可逆、正式进度入口和本轮宝箱补足。

### 3. 调用流程

临时战斗开关：`InputCo -> PlayerDeveloperModeComponent -> ToggleDeveloper...Command -> DeveloperModeSystem -> DeveloperModeModel -> PlayerCombatSystem / PlayerSkillSystem`。

进度操作：`F4/F6/F7 -> AddGoldCommand / AddPlayerLevelsForDevelopmentCommand / FullRestorePlayerManaCommand -> 原有 System -> Event -> UI 与自动存档`。

宝箱补足：`F5 -> VaultsUntilNextBoss -> BoxCo.BreakRepeatedlyForDevelopment -> HandleDestroyed -> 奖励、掉落、OnVaultDestroyed -> BossRunProgressState -> Boss 入口`。

### 4. 核心原理

开发者高攻没有直接把 `PlayerRuntimeStats.AttackPower` 改成大数，而是在每次伤害结算时读取“有效攻击力”。这相当于在账本外加一张临时测试券：装备、升级和存档看到的仍是真实攻击，普通攻击与技能伤害结算时才额外加 10,000，所以关闭功能后不会反向计算，也不会发生属性漂移。

无敌放在统一 `TakeDamage` 入口，是为了让近战、子弹和 Boss 攻击共享同一个判断。技能零冷却也放在 `PlayerSkillSystem`：开启时先清掉已有 CD，以后释放不再写入新 CD，但扣蓝仍走正式资源系统。

金币、等级和宝箱进度属于用户明确要求保留的测试进度，因此继续调用正式 Command/System 并触发存档。高攻、无敌和零冷却只保存在运行时 DeveloperModeModel，关闭 F1 或销毁玩家组件时统一清空，不扩展存档协议。

F5 使用“补足本轮”而不是固定增加五次。例如当前已经完成 2/5，只正式结算剩余 3 次。连续结算仍调用宝箱原来的奖励和事件，只跳过等待动画，既能快速测试，又不会绕开 Boss 解锁链路。

### 5. Unity 测试方式

1. 打开 `MainScene` 运行游戏，按 F1，确认左上角显示开发者状态与 F2-F8 说明。
2. 按 F2 后用普通攻击和职业技能攻击怪物，确认两者都额外获得 10,000 攻击；再次按 F2 恢复。
3. 按 F3 后让小怪、子弹和 Boss 命中，确认生命不减少；再次按 F3 恢复承伤。
4. 按 F4，确认金币增加 10,000；按 F6，确认只升一级；消耗魔法后按 F7，确认回满蓝。
5. 先释放一个技能进入 CD，再按 F8，确认现有 CD 立即清除且可连续释放，魔法仍正常减少。
6. 在宝箱进度 0/5、2/5 或 4/5 时按 F5，确认只补足到 5/5、奖励正常并生成 Boss 入口。
7. 开启 F2、F3、F8 后再次按 F1，确认三个临时效果全部关闭；重新登录时只保留金币、等级和宝箱进度。

### 6. 面试表达

我把开发者模式拆成输入表现层和运行时规则层。F1-F8 由玩家组件读取，但高攻、无敌和零冷却保存在独立 Model，并通过 Command 和 Query 修改。高攻采用伤害结算时叠加，不直接修改角色基础属性，所以装备切换、升级和存档不会出现数值漂移；无敌与零冷却分别接在统一受伤和技能释放入口。金币、等级和宝箱则复用正式系统，让开发测试覆盖真实事件和存档链路。宝箱快捷键只补足本轮剩余次数，不会提前污染下一轮进度。

### 7. 面试追问

1. **为什么高攻不直接改 AttackPower？** 直接改值需要记录并恢复旧值，而且装备、升级也会同时改它，很容易还原错误；结算时叠加天然可逆。
2. **为什么开发者状态也要 Model/System？** 多个战斗模块都需要读取同一状态，集中管理能避免 MonoBehaviour 静态变量散落，也方便自动测试。
3. **无敌为什么不把减伤设为 100%？** 正式减伤有 95% 上限且至少扣 1 点血；在受伤入口明确返回 0 更符合无敌语义。
4. **0CD 为什么还要清除旧冷却？** 只阻止新 CD 不会处理已经释放的技能，玩家开启后仍需等待；先清空才能立即反馈。
5. **连续击破宝箱会绕过奖励吗？** 不会，每次仍调用正式 HandleDestroyed，只跳过多次测试之间的重生动画等待。
6. **哪些内容会保存？** 金币、等级和宝箱进度走正式事件并保存；高攻、无敌、0CD 只存在当前运行时。
7. **正式上线如何关闭？** 当前按作品演示约定在 PC 包保留；商业发布时可将环境判断改为只允许 Editor 或 Development Build，或使用编译宏彻底裁剪。

### 8. 本次涉及知识点

- QFramework Model、System、Command 与 Query
- 临时状态和持久化状态的边界
- 伤害结算管线与统一规则入口
- 可逆属性修饰与避免数值漂移
- 技能 CD、资源消耗和运行时数据
- IMGUI 调试覆盖层
- 事件驱动的宝箱奖励和 Boss 解锁
- EditMode 单元测试与静态进度清理

## 功能名称：Mushroom NPC 持久化任务系统

### 1. 实现目标

在不影响 Fungi 商店的前提下，增加一个独立 Mushroom 任务 NPC。玩家靠近后按 E 打开“蘑菇委托”，可以同时接取“击杀 5 只红色史莱姆”和“击杀 8 只绿色史莱姆”两条一次性任务；只有接取后的正式死亡才计数，完成后必须返回 NPC 手动领取 50/80 金币。

任务状态、击杀数量和已领取状态属于角色长期数据。在线角色写入 SQL Server 的任务子表，游客角色写入 v5 JSON；角色死亡只重置原有战斗成长，不清除任务。

### 2. 涉及脚本

- `QuestCatalog / QuestTypes`：定义稳定任务 ID、目标怪物、目标数量、金币奖励和四阶段状态。
- `QuestModel / QuestSystem`：维护角色运行时任务进度，统一处理接取、击杀、完成、领奖和恢复。
- `QuestCommands / QuestQueries / QuestEvents`：为怪物、UI 和存档提供解耦的读写入口。
- `MonsterQuestProgressReporter`：监听每个 SlimeCo 实例的 Died 事件，按 MonsterKind 上报一次正式死亡。
- `QuestNpcController`：处理 Mushroom 的 3 米触发区、E 键交互和头顶问号显隐。
- `QuestPanel / QuestListItemView`：根据 QuestCatalog 生成任务卡，显示进度、奖励和状态按钮。
- `CharacterProgressSaveService / GameApiClient / LocalGuestSaveService`：处理立即保存、防抖保存、网络映射和游客 v5 迁移。
- 服务端 `QuestPersistenceRules / DBService / UserService`：校验白名单与状态不可回退，并在角色保存事务中读写 `CharacterQuestProgress`。
- `QuestFeatureSetupTool`：幂等创建配置、Prefab、淘宝 UI 视图和 MainScene 引用。
- `QuestSystemTests / QuestFeatureAssetTests`：验证领域规则、金币原子性和资源完整性。

### 3. 调用流程

接取任务：`Player Input(E) -> QuestNpcController -> QuestPanelOpenRequestedEvent -> QuestPanel -> AcceptQuestCommand -> QuestSystem -> QuestModel -> QuestAcceptedEvent -> UI 刷新 + 立即保存`。

击杀计数：`SlimeCo.DoDie -> Died -> MonsterQuestProgressReporter -> RecordMonsterDefeatedCommand -> QuestSystem -> QuestProgressChangedEvent -> UI 按需刷新 + 防抖保存`。

领取奖励：`QuestListItemView -> QuestPanel -> ClaimQuestRewardCommand -> QuestSystem 检查金币上限 -> AddGoldCommand -> EconomySystem -> GoldChangedEvent -> Gold HUD + 立即保存`。

角色恢复：`SceneFlowService -> RestoreQuestProgressCommand -> QuestSystem -> QuestModel -> QuestProgressRestoredEvent -> NPC 问号与任务面板刷新`。

在线持久化：`GetPlayerProgressSaveDataQuery -> GameApiClient -> Protobuf -> UserService 校验 -> DBService 事务 -> CharacterQuestProgress`。

### 4. 核心原理

任务目录和角色进度分开保存。QuestCatalog 像“任务策划表”，只说明任务是什么；QuestModel 像“角色任务日志”，只记录这个角色接了没有、杀了几只、领了没有。这样改奖励或目标数量不会直接修改角色运行时数据，换 UI 也不会影响任务规则。

任务只能按照 `Available -> Active -> ReadyToClaim -> Claimed` 向前推进。未接取时不会计数，达到目标后数量封顶，已领奖后不能再次接取或领取。奖励先检查能否完整放入金币上限，再通过 AddGoldCommand 发放；如果不能完整领取，任务保持 ReadyToClaim，避免“金币只加一部分但任务已经消失”。

MonsterKind 与 SlimeType 分开：MonsterKind 表示红色或绿色这种稳定玩法身份，SlimeType 继续表示近战或远程行为。任务不通过材质颜色和 Prefab 名称猜怪物类型，因此以后换模型、材质或攻击方式也不会统计错。

对象池中的史莱姆会反复启用和停用，所以死亡上报组件在 OnEnable 注册、OnDisable 注销。它只监听 SlimeCo 首次进入死亡状态时发出的 Died，不把对象池回收、场景清理或普通 Destroy 当成击杀。

存档方面，接取和领奖是关键状态切换，立即请求保存；连续击杀可能很密集，使用一秒防抖合并请求。游客旧档没有 questProgress 时解释为“全部可接取”，在线服务端则用稳定 ID 白名单、唯一记录、目标数量和不可回退规则保护数据。

### 5. Unity 测试方式

1. 打开 `Assets/Scenes/MainScene.unity` 并运行，确认右侧 Fungi 商店仍正常，左侧 x=-3.4 的 Mushroom 头顶有问号。
2. 靠近 Mushroom，确认只出现“按 E 查看蘑菇委托”，按 E 后游戏暂停、鼠标解锁并打开任务面板。
3. 两条任务都接取，分别击杀红色与绿色史莱姆；重新打开面板，确认只增加对应任务，数量显示为 `当前/目标`。
4. 红色达到 5、绿色达到 8 后回到 NPC，分别领取 50 和 80 金币，确认 Gold HUD 立即变化且按钮变为“已领取”。
5. 按 ESC 和关闭按钮检查面板关闭、时间恢复、鼠标重新锁定；同时验证背包、商店、暂停和任务面板不会一起打开。
6. 分别用游客和在线角色退出再进入，检查 Active、ReadyToClaim 和 Claimed 状态都能恢复；角色死亡后检查任务仍保留。
7. 在 Test Runner 中运行 QuestSystemTests、QuestFeatureAssetTests、CharacterProgressPersistenceTests 和 GuestModePersistenceTests。

### 6. 面试表达

这个任务系统我分成静态配置、运行时逻辑和表现三层。QuestCatalog 用 ScriptableObject 配任务 ID、目标怪物、数量和奖励；QuestModel 只保存角色进度；QuestSystem 通过 Command 处理接取、击杀和领奖，并用 Event 通知 UI 和存档。史莱姆死亡通过独立上报组件订阅 Died 事件，对象池复用时成对注册注销。领奖复用统一 AddGoldCommand，并在状态切换前检查金币能否完整放入。任务数据同时接入游客 JSON 和在线 SQL 子表，服务端会校验白名单、数量范围和状态不可回退。这样新增任务主要是加配置，怪物、UI、经济和存档之间不会直接互相修改数据。

### 7. 面试追问

1. **为什么用 ScriptableObject 配任务？** 静态任务数据可以在 Inspector 调整并被多个角色共享，运行时角色进度单独存 Model，避免修改共享资源。
2. **为什么不直接在 SlimeCo 里写任务逻辑？** SlimeCo 只负责战斗死亡，独立 Reporter 把死亡转换为任务命令，后续移除任务或增加别的统计系统都不需要改怪物核心逻辑。
3. **怎样避免对象池重复统计？** Died 只在一次生命首次死亡时触发，Reporter 在 OnEnable/OnDisable 成对注册，回收和清场不会主动发送任务命令。
4. **领奖为什么要先检查金币上限？** 奖励和任务状态应当是一个不可分割的业务结果；空间不足就不改任何数据，避免部分奖励或任务丢失。
5. **任务 UI 为什么不每帧刷新？** UI 在接取、进度变化、领奖和存档恢复事件发生时刷新，减少无效查询和布局重建。
6. **游客旧档怎样兼容？** 存档版本升到 v5，v1-v4 缺少任务字段时初始化为空列表；空列表在运行时代表所有目录任务为 Available。
7. **服务端是否权威判定击杀？** 当前项目战斗仍由客户端驱动，首版服务端只校验任务数据结构和不可回退；商业项目应由服务端战斗事件推进任务，客户端只显示结果。

### 8. 本次涉及知识点

- ScriptableObject 数据驱动配置
- QFramework Model、System、Command、Query、Event
- 有限状态机与单向状态迁移
- 事件驱动 UI 和模态窗口输入互斥
- 对象池生命周期与事件注册/注销
- 原子奖励、金币上限和失败不变式
- Protobuf 向后兼容字段扩展
- SQL 子表、事务保存和数据白名单校验
- JSON 存档版本迁移与游客旧档兼容
- EditMode 领域测试与 Prefab 资源测试

## 功能名称：商店与任务对话 UI 统一及任务排版入口

### 1. 实现目标

把“风叶长靴”原来容易误认成叶子的图标换成明确的长靴图标；让 Fungi 的首次对话复用 Mushroom 任务窗口的视觉语言，同时保留原有“知道了 -> 打开商店”交互流程。任务面板和任务条目增加专用编辑菜单，打开 Prefab 后直接显示并选中需要排版的节点，方便在 Unity 中手动调整。

### 2. 涉及脚本

- `InventoryFeatureSetupTool`：为风叶长靴保存跨目录鞋子图标路径，防止重新装配后退回旧图标。
- `ShopFeatureSetupTool`：把 Fungi 对话迁移到任务窗口背景、金色标题、暖色正文和任务绿色按钮，并升级视觉迁移版本。
- `QuestFeatureSetupTool`：提供任务面板与任务条目的 Prefab Mode 编辑入口；重建菜单增加覆盖警告。
- `GameplayUiRoot.prefab`：保存新版 Fungi 对话布局，并让 QuestModal 在编辑态可见。
- `ShopFeatureAssetTests / QuestFeatureAssetTests`：保护鞋子图标、统一背景、按钮资源、任务面板可见状态和序列化引用。

### 3. 调用流程

Fungi 对话：`MerchantNpcController -> MerchantShopPanel 显示 FirstDialogue -> Fungi 对话窗口 -> 点击“知道了” -> MerchantShopPanel 打开 ShopPanel`。

任务排版：`Unity 菜单 Tools/Treasure Hunter/Quest/Edit Quest Panel Layout -> AssetDatabase.OpenAsset -> PrefabStage -> 显示 QuestModal、隐藏 QuestPrompt -> 选中 QuestModal/Panel -> 手动拖拽并 Ctrl+S`。

运行时隐藏：`实例化 GameplayUiRoot -> QuestPanel.Start -> panelRoot.SetActive(false) -> 玩家与 Mushroom 交互后再打开`。

### 4. 核心原理

视觉统一只替换表现层资源和 RectTransform 参数，没有替换 MerchantShopPanel，也没有改商店业务事件。Fungi 仍使用自己的首次对话和“知道了”按钮，只是背景、文字层级和按钮素材与 Mushroom 任务窗口一致。因此 UI 风格统一，但两个 NPC 的功能职责不会混在一起。

QuestModal 在 Prefab 资产中保持激活，是为了让编辑者打开 Prefab 就能看到面板；进入游戏时 QuestPanel.Start 会主动关闭它。可以把这理解为“编辑态默认展开，运行态初始化收起”，既方便排版，又不改变玩家看到窗口的时机。

编辑器菜单通过 PrefabStage 定位真实的 Prefab 内容，不在场景实例上做临时修改。菜单会选中并聚焦 `QuestModal/Panel`；任务重建入口明确提示会覆盖手调布局，减少误操作。生成工具和当前资源同时更新，避免以后重跑工具后样式回退。

### 5. Unity 测试方式

1. 打开 `MainScene` 运行，查看背包或商店中的风叶长靴，确认显示为鞋子图标。
2. 第一次靠近 Fungi 按 E，确认对话使用与 Mushroom 任务面板相同的公会背景、居中金色标题和任务绿色按钮。
3. 点击“知道了”，确认仍正常进入商店；关闭和再次打开商店，确认原逻辑不变。
4. 退出 Play Mode，执行 `Tools/Treasure Hunter/Quest/Edit Quest Panel Layout`，确认进入 GameplayUiRoot 的 Prefab Mode，QuestModal 已显示且 Panel 被选中。
5. 手动移动 Title、Content 或 Feedback，按 Ctrl+S；关闭后重新打开，确认排版保留。
6. 执行 `Edit Quest List Item Layout`，确认可以单独编辑任务卡 Prefab。
7. 打开带 `Overwrites Manual Layout` 的任务重建菜单，确认 Unity 会先弹出覆盖警告，测试时选择取消。

### 6. 面试表达

这次我没有把商店对话逻辑改成任务逻辑，而是只统一表现层。Fungi 继续由 MerchantShopPanel 控制首次对话和商店开启，背景、标题、正文和按钮素材则复用任务 UI 的视觉规范。任务面板为了方便美术和策划手调，我增加了 PrefabStage 编辑入口：菜单会打开 GameplayUiRoot、显示运行时默认隐藏的 QuestModal，并自动选中核心面板；运行时再由 QuestPanel.Start 关闭。生成工具也增加覆盖警告，并用资产测试保护图标、Sprite 和节点状态，避免重新生成导致回退。

### 7. 面试追问

1. **为什么不让 Fungi 直接复用 QuestPanel？** 两者业务职责不同，复用整个面板会让商店依赖任务组件；这里只复用视觉资源，耦合更低。
2. **Prefab 里 QuestModal 激活会不会开局显示？** 不会，QuestPanel.Start 在第一帧渲染前主动把 panelRoot 关闭，交互后才重新打开。
3. **为什么需要专门的编辑菜单？** GameplayUiRoot 层级较深且窗口默认隐藏，菜单能稳定定位、显示并聚焦目标，降低手动找节点和误改场景实例的成本。
4. **为什么重建菜单要警告？** 生成工具会删除再创建 QuestFeature 和 QuestListItem，属于可能覆盖人工排版的操作，必须让编辑者明确确认。
5. **如何防止鞋子图标以后又变回叶子？** 当前 ScriptableObject 和生成配置都改成同一完整路径，并用资产测试校验最终 Sprite 路径。

### 8. 本次涉及知识点

- UGUI Image、Text、Button 与 RectTransform
- Prefab Mode、PrefabStage、Selection 和 SceneView 聚焦
- 编辑态状态与运行时初始化状态的区别
- 表现层复用与业务层职责隔离
- Editor MenuItem、Undo 和场景脏标记
- 生成工具版本迁移与人工布局保护
- ScriptableObject 资源引用与 GUID
- EditMode Prefab 资产测试

## 功能名称：统一游戏音乐与核心音效系统

### 1. 实现目标

把原先散落在场景、玩家和怪物 Prefab 中的 AudioClip 引用收口到统一配置。游戏第一次进入 LoadingScene 就播放登录主题，登录、选角、野外和 Boss 房间按场景平滑切歌；角色、技能、敌人、金库、拾取、任务、商店、传送门和 UI 使用统一语义 Cue。原有 Master、Music、Sounds 三档设置与 PlayerPrefs 数据保持兼容。

### 2. 涉及脚本

- `GameAudioTypes.cs`：定义业务层使用的 `GameSfxId`，玩法代码不直接依赖音频文件。
- `GameAudioCatalog.cs`：ScriptableObject 数据层，保存场景音乐、候选音效、基础音量、随机音高和 2D/3D 参数。
- `GameAudioService.cs`：跨场景播放服务，负责双 BGM 交叉淡化、2D 音源和 16 个 3D 音源池，并监听死亡、购买和任务事件。
- `UiAudioFeedback.cs`：只负责监听 Button 点击并播放通用点击 Cue。
- `PlayerAudioComponent.cs`：把职业、连击段数和技能 ID 转换为对应 Cue。
- `SlimeCo`、`SpiderKingBossController`、`BoxCo`、`WorldItemPickup`、`WorldGoldPickup`、`BossScenePortal`：在行为真正成功的位置触发空间音效。
- `GameAudioSetupTool.cs`：统一配置 AudioImporter、Catalog、正式场景环境音和 UI Prefab，清理旧 BGM。
- `GameAudioTests.cs`：保护资源数量、Cue 完整性、导入策略、Mixer 路由、重复 BGM 和 UI 点击组件。

### 3. 调用流程

场景加载 -> `GameAudioService.HandleSceneLoaded` -> `GameAudioCatalog.TryGetSceneMusic` -> 双 Music AudioSource 交叉淡化 -> Main.mixer/Music

玩家攻击或交互成功 -> 业务组件 -> `GameAudioService.Play2D / PlayAt / PlayOn` -> Catalog 查找 Cue -> 随机候选 AudioClip -> Main.mixer/Sounds

设置面板拖动滑杆 -> `GameSettingsService` -> Main.mixer 暴露参数 -> Music 或 Sounds 分组整体改变响度

### 4. 核心原理

可以把系统理解为“菜单、调度员和播放器”。`GameAudioCatalog` 是菜单，记录每个声音标识可以选哪些素材、响度和空间参数；`GameAudioService` 是调度员，决定用音乐音源、2D 音源还是 3D 音源池；AudioSource 和 AudioMixer 才是真正的播放器与总控台。

业务脚本只说“播放 Boss 受击”或“播放金币拾取”，不保存具体 AudioClip。以后换素材只改 Catalog，伤害、任务、背包逻辑都不用改。BGM 使用两个 AudioSource：新曲淡入的同时旧曲淡出，且用 `Time.unscaledDeltaTime`，暂停游戏时也能完成切歌。3D 短音效使用 16 个预热音源轮换，避免 `PlayClipAtPoint` 每次创建临时 GameObject 带来的实例化和 GC 波动。

LoadingScene 是短过渡场景：第一次启动且没有音乐时播放 Happy；普通场景切换经过 Loading 时保持旧曲，到目标场景激活后才交叉切换。相同 AudioClip 会直接忽略重复请求，因此不会叠播。Music 和 Sounds 分别进入现有 Mixer 分组，玩家之前保存的三档音量数据无需迁移。

### 5. Unity 测试方式

1. 从 Build Settings 的 `LoadingScene` 启动，确认立刻听到 Happy；进入 Login 不应重新起一遍同一首歌。
2. 依次进入 `CharacterSelectScene`、`MainScene`、`BossRoomScene`，确认 Mystery、Forest、Darkness 进行约 1 秒交叉切换且无叠播。
3. 在 MainScene 测试四职业移动、跳跃、翻滚和普攻，确认武器声音按职业变化；释放 1001 火球、1002 毒区、2001 旋转斩。
4. 测试史莱姆近战/远程/受击/死亡、Spider King 三类攻击/受击/死亡、金库受击/击破、金币/物品拾取和传送门。
5. 打开任务、商店及正式 UI，确认点击、购买、接受任务和领奖反馈。
6. 在设置中分别把 Master、Music、Sounds 拉到 0，再取消、应用并重启，确认预览、恢复和持久化都正确。
7. 执行 EditMode 测试 `GameAudioTests`，确认 6 项全部通过。

### 6. 面试表达

这个音频系统我分成了配置层、播放服务和业务触发三部分。配置层用 ScriptableObject 维护场景 BGM 和语义化 Cue，每个 Cue 可以配置多个随机片段、基础音量、音高和 2D/3D 参数；业务代码只传枚举，不直接引用 AudioClip。播放服务跨场景常驻，用双 AudioSource 做不受 timeScale 影响的 BGM 交叉淡化，并预热 16 个 3D AudioSource 做复用，减少临时对象和 GC。所有音乐和音效分别路由到现有 AudioMixer 的 Music、Sounds 分组，所以保留了主音量、音乐和音效设置以及原有存档格式。最后我用 EditMode 测试保护 Cue 完整性、导入策略、Mixer 路由和场景重复 BGM。

### 7. 面试追问

1. **为什么用 ScriptableObject？** 音频属于可配置数据，换素材和调基础响度时不需要修改玩法代码或多个 Prefab，且 Inspector 中能直观看到引用。
2. **为什么 BGM 要两个 AudioSource？** 单音源只能先停旧曲再播新曲；双音源可以同时控制旧曲淡出和新曲淡入，实现无缝交叉切换。
3. **为什么使用 unscaledDeltaTime？** 设置面板或暂停界面可能把 `timeScale` 设为 0，真实时间仍能保证音乐淡化完成。
4. **音源池解决什么问题？** 避免每个空间音效都临时创建和销毁 GameObject，减少 Instantiate/Destroy 开销、GC 和帧时间波动；代价是高并发超过容量时需要制定复用策略。
5. **为什么 UI 点击用组件而不是每个按钮手写监听？** `UiAudioFeedback` 只负责通用表现，可由工具批量挂载，动态条目从 Prefab 继承，业务按钮逻辑保持独立。
6. **LoadingScene 为什么不固定切音乐？** 它停留时间短，反复切歌会造成听感破碎；只有首次启动兜底，后续等目标场景真正激活再切换。
7. **如何继续扩展？** 可以增加并发上限、Cue 冷却、优先级、音频总线快照、Addressables 异步加载以及按地表材质选择脚步声。

### 8. 本次涉及知识点

- ScriptableObject 数据驱动配置
- AudioSource、AudioClip 与 AudioMixerGroup 路由
- 分贝与线性音量、PlayerPrefs 持久化
- 跨场景单例和 `RuntimeInitializeOnLoadMethod`
- 不受 `timeScale` 影响的协程与交叉淡化
- 2D/3D 空间音效、衰减距离与单声道
- AudioSource 对象池、随机候选与音高扰动
- Unity 场景加载事件和 QFramework 事件订阅/注销
- AudioImporter 的 Streaming、Decompress On Load、Compressed In Memory
- Editor 工具批量配置与 EditMode 回归测试

## 功能名称：NPC 交互提示、ESC 鼠标与商店金币可见性修复

### 1. 实现目标

统一 Fungi 首次对话与 Mushroom 任务窗口的视觉语言；修复商店和任务面板按 ESC 后鼠标没有重新锁定的问题；让玩家远离 NPC 后可靠隐藏“按 E”提示；把商店金币文本放回屏幕右上角安全区域。

### 2. 涉及脚本

- `GameSessionUi`：先立即锁定，再等待 ESC 松开后于帧末二次锁定，避免 Unity Editor 的 ESC 行为覆盖鼠标状态。
- `MerchantShopPanel / QuestPanel`：关闭最后一个模态界面时请求恢复玩法鼠标。
- `MerchantNpcController / QuestNpcController`：保存真实玩家 Collider，并主动清理失效或已经离开交互范围的引用。
- `ShopFeatureSetupTool`：统一对话色值，修复 ShopGoldText 锚点、位置、颜色和描边，并升级视觉迁移版本。
- `GameplayUiRoot.prefab`：保存最终 Fungi 对话和商店金币布局。
- `ShopFeatureAssetTests`：保护对话背景、标题正文色值和金币安全区域。

### 3. 调用流程

ESC 关闭：`Input.GetKeyDown(Escape) -> GameSessionUi -> MerchantShopPanel.TryCloseTopModal / QuestPanel.TryClose -> 恢复 timeScale -> RequestGameplayCursorRestore -> 当帧锁定 -> 等待 ESC 松开 -> 帧末确认无其它模态 -> 再次锁定`。

提示隐藏：`OnTriggerEnter/Stay -> 记录玩家 Collider -> 每帧检查 Collider 有效性与 ClosestPoint 距离 -> 清除失效引用 -> 集合为空 -> ProximityChanged(false) -> Panel.RefreshPromptVisibility -> 隐藏按 E`。

金币显示：`打开 ShopPanel -> GetGoldQuery -> MerchantShopPanel.RefreshGold -> ShopGoldText`；购买后由 `GoldChangedEvent` 再次刷新余额。

### 4. 核心原理

Unity Editor 会把 ESC 当作“释放 Game View 鼠标”的快捷操作。如果脚本只在 ESC 当帧或按键仍按住时锁定，编辑器仍可能再次解锁。因此最终采用立即锁定，再等待 ESC 松开并到达帧末后二次确认；确认前会检查是否又打开了暂停、背包或其它模态界面，避免抢走 UI 鼠标。

触发器事件不是绝对可靠的状态存储。Collider 被禁用、销毁或角色瞬移时，`OnTriggerExit` 可能没有机会执行。现在 HashSet 保存真实 Collider，每帧只遍历当前重叠项，清理空引用、禁用对象、非玩家对象和已经超过三米范围的对象。清理使用复用 List，避免每帧产生委托或临时集合 GC。

金币业务数据原本是正确的，问题来自 RectTransform：右上角锚点配正 X 偏移会把文字推向屏幕外。修复后使用右上角锚点、负向安全边距、金币色和描边，数据查询与表现布局仍保持分离。

### 5. Unity 测试方式

1. 打开 `MainScene`，分别靠近 Fungi 和 Mushroom，确认进入约三米范围才显示按 E。
2. 快速进出触发区多次，并尝试翻滚离开，确认远离后提示立即消失。
3. 打开 Fungi 首次对话，确认背景、尺寸、金色标题、暖色正文和绿色按钮与任务窗口一致。
4. 打开商店，确认右上角显示当前金币；购买后余额和扣款反馈立即更新。
5. 分别打开商店和任务面板后按下并松开 ESC，确认松开后鼠标隐藏并锁定，可以继续控制镜头。
6. 使用关闭按钮重复测试，确认鼠标状态同样恢复。
7. 在 Test Runner 运行 `UiCursorStateTests`、`ShopFeatureAssetTests` 和 `QuestFeatureAssetTests`。

### 6. 面试表达

这次主要修复了三个 UI 状态边界。模态面板关闭时我没有只依赖 ESC 当帧的 Cursor 设置，而是由 GameSessionUi 统一立即锁定，等待 ESC 松开后再在帧末确认一次，因为 Unity Editor 会用 ESC 释放 Game View 鼠标。NPC 提示也不再把 OnTriggerExit 当作唯一真相，而是保存真实 Collider，并主动清理失效或已经超过交互距离的引用。商店金币的数据查询原本正常，实际是右上角 RectTransform 正偏移导致文字在屏幕外，我修正了安全边距并用资产测试保护。整个修改只调整交互和表现层，没有改经济、任务或存档规则。

### 7. 面试追问

1. **为什么要等 ESC 松开后再锁一次？** Unity Editor 自己也会处理 ESC 并释放 Game View 鼠标；按键仍按住时重新锁定可能继续被覆盖，松开后的帧末确认更稳定。
2. **为什么不能只依赖 OnTriggerExit？** 对象禁用、销毁、传送和物理状态变化可能让退出事件丢失，提示状态就会永久残留。
3. **怎样兼容玩家有多个 Collider？** HashSet 分别记录真实 Collider，只有所有有效 Collider 都离开后才发送附近状态为 false。
4. **每帧校验会不会影响性能？** 只有两个 NPC，而且只遍历当前记录的少量 Collider；清理列表会复用，不产生持续 GC。
5. **金币不显示为什么不改 EconomySystem？** 查询和事件刷新都正常，根因是 UI 坐标在屏幕外；修改业务层反而会扩大影响范围。

### 8. 本次涉及知识点

- Unity CursorLockMode 与 Editor Game View 行为
- Coroutine、按键松开后的帧末确认与模态界面互斥
- Trigger Enter/Stay/Exit 的可靠性边界
- HashSet、Collider.ClosestPoint 与无 GC 清理
- 事件驱动交互提示刷新
- RectTransform 锚点、Pivot 和安全边距
- 表现层与经济/任务业务层解耦
- Prefab 定点迁移版本与资产回归测试

## 功能名称：商店底部提示统一与 ESC 光标二次修复

### 1. 实现目标

根据实际运行截图，把 Fungi 商店底部的黑色交互条改成与 Mushroom 任务提示完全相同的金色装饰样式；同时修复在 Unity Editor 中按 ESC 关闭商店或任务窗口后光标仍被释放的问题。

### 2. 涉及脚本

- `ShopFeatureSetupTool`：给商店提示应用任务提示的背景精灵、尺寸、位置、字号、颜色和内边距，并升级视觉迁移版本。
- `GameSessionUi`：等待 ESC 松开并完成帧末处理后，再次恢复玩法光标。
- `ShopFeatureAssetTests`：逐项比较商店提示与任务提示的 Sprite、Image、RectTransform 和 Text 参数。
- `GameplayUiRoot.prefab`：保存统一后的底部提示。

### 3. 调用流程

提示迁移：`ShopFeatureSetupTool -> InteractionPrompt -> 复用 QuestPrompt 背景 -> 同步 RectTransform/Text 参数 -> 保存 GameplayUiRoot.prefab`。

光标恢复：`ESC 关闭模态 -> 立即尝试锁定 -> Coroutine 等待 Input.GetKey(Escape) 为 false -> WaitForEndOfFrame -> 确认玩法未被其它 UI 阻挡 -> Locked + Hidden`。

### 4. 核心原理

视觉一致不等于让两个业务面板互相引用。本次只共享同一个 UI 精灵和布局规范，商店提示仍由 `MerchantShopPanel` 控制，任务提示仍由 `QuestPanel` 控制。这样修改商店业务不会影响任务状态机，但玩家看到的是统一的交互语言。

Unity Editor 会把 ESC 用作释放 Game View 光标。等待固定一帧并不保证玩家已经松开按键，所以改为等待真实按键状态结束，再在帧末恢复。相比每帧强制锁定，这个方案不会破坏大地图等需要解锁鼠标但不暂停时间的界面。

### 5. Unity 测试方式

1. 在 `MainScene` 分别靠近 Fungi 和 Mushroom，比较两套底部提示的边框、左右装饰、尺寸和高度。
2. 分别进入商店和任务窗口，按住 ESC 片刻再松开，确认窗口关闭且镜头恢复。
3. 使用窗口关闭按钮重复测试，确认不按 ESC 也能在帧末恢复鼠标。
4. 展开大地图，确认鼠标不会被本次逻辑持续抢回。
5. 在 Test Runner 运行 `ShopFeatureAssetTests` 和 `UiCursorStateTests`。

### 6. 面试表达

这次我根据运行截图发现，之前统一错了 UI 层级：实际需要统一的是 NPC 底部交互提示，不是弹窗。我让商店和任务提示共享同一张切片背景和同一套布局参数，但保留各自的面板控制逻辑。鼠标问题则来自 Unity Editor 会用 ESC 释放 Game View 光标，固定延迟一帧仍可能发生在按键松开之前，所以我改成等待 ESC 松开后在帧末恢复，并在恢复前检查其它模态界面，避免抢走其它 UI 的鼠标。

### 7. 面试追问

1. **为什么不直接让商店使用 QuestPrompt 对象？** 两个提示生命周期和业务事件不同，直接共用对象会产生跨系统引用；共享视觉资源和规范更低耦合。
2. **为什么固定等待一帧不够？** 玩家按键可能持续多帧，Editor 仍会保持释放光标的处理结果。
3. **为什么不用 Update 每帧强制 Locked？** 大地图等非暂停 UI 也需要自由鼠标，持续强制会破坏它们。
4. **为什么使用 WaitForEndOfFrame？** 确保当前帧的输入和 Editor 光标处理已经完成，再写入最终玩法状态。
5. **如何防止以后两套提示又不一致？** 资产测试直接比较 Sprite、Image、RectTransform 和 Text 参数，出现漂移会立即失败。

### 8. 本次涉及知识点

- UGUI 九宫格切片与 `Image.Type.Sliced`
- RectTransform 锚点、Pivot、Offset 和底部定位
- 输入按下、持续、松开的生命周期
- `WaitForEndOfFrame` 与 Unity 帧顺序
- Unity Editor 和 Standalone 的光标行为差异
- UI 视觉复用与业务解耦
- Prefab 定点迁移和资产一致性测试

## 功能名称：Mushroom 任务面板安全区域排版修复

### 1. 实现目标

修复任务面板标题、底部提示以及任务卡中的图标、文字、奖励和按钮互相遮挡的问题。调整只发生在任务 UI 表现层，不改变接取、击杀计数、领奖和持久化规则。

### 2. 涉及脚本

- `QuestFeatureSetupTool.cs`：按淘宝背景素材的装饰区域重新生成任务卡，并使用正确的 Anchor/Pivot 生成面板控件。
- `QuestListItem.prefab`：保存标题、描述、进度、奖励和按钮的新安全区域布局。
- `GameplayUiRoot.prefab`：保存面板标题、关闭按钮、任务列表和底部反馈文字的新锚点。
- `QuestFeatureAssetTests.cs`：检查控件边界、区域重叠和两张任务卡所需的列表高度。

### 3. 调用流程

编辑器执行 UI 布局迁移 -> `BuildQuestItemPrefab` 划分任务卡安全区域 -> `UpgradeGameplayUiPrefab` 修正弹窗锚点 -> 保存两个 Prefab -> `QuestPanel` 运行时生成任务卡 -> `QuestListItemView.Bind` 只刷新内容和状态。

### 4. 核心原理

淘宝任务条图片并不是纯背景，它已经画好了左侧徽章和右侧奖杯。如果再把敌人图标或按钮放在这些坐标上，即使 RectTransform 没有互相相交，视觉上仍会发生遮挡。因此排版时先划分“素材装饰区”和“动态内容安全区”，所有会变化的文字、进度、奖励和按钮都放在中间安全区。

Anchor 决定坐标参考点，Pivot 决定控件用自身哪个点对齐参考点。旧标题以面板中心为 Anchor，却使用了从左下角计算的 X 坐标，等于重复增加了半个面板宽度。修复后标题使用顶部中心锚点，关闭按钮使用右上角锚点，反馈文字使用底部中心锚点，因此不同分辨率缩放时仍保持正确位置。

文字使用 Best Fit 和 Truncate 作为最后保护：正常配置保持最大字号，极端长文本会先缩小，仍放不下时只在自己的矩形内截断，不会进入奖励或按钮区域。

### 5. Unity 测试方式

1. 打开 `MainScene`，靠近 Mushroom 后按 E。
2. 检查两张任务卡之间有间距，标题、描述、进度、金币和按钮互不遮挡。
3. 检查左侧徽章和右侧奖杯装饰没有被任务内容覆盖。
4. 分别查看接取、进行中、可领取和已领取状态，确认按钮文字仍完整。
5. 切换到 1920×1080、1600×900 和 1280×720，确认标题、关闭按钮和底部提示都在面板内。
6. 运行 `QuestFeatureAssetTests`，检查 Prefab 引用、边界和重叠断言。

### 6. 面试表达

这次任务 UI 的问题不只是普通 RectTransform 重叠，还包括动态控件覆盖了背景素材自带的徽章和奖杯。我先根据原图划分左右装饰区和中间安全区，再把标题、描述、进度、奖励和状态按钮分成互不相交的矩形。弹窗标题、关闭按钮和反馈文字则改成符合语义的顶部中心、右上角和底部中心锚点。最后增加资产测试，用几何边界检查保护控件不会跑出父节点，也检查两张任务卡的总高度和关键区域不重叠。

### 7. 面试追问

1. **Anchor 和 Pivot 有什么区别？** Anchor 是控件相对父节点的参考位置，Pivot 是控件自身围绕哪个点定位、缩放和旋转。
2. **为什么坐标没有相交，画面仍可能遮挡？** 背景 Sprite 本身也可能包含图标和装饰，动态控件需要避开素材的视觉安全区域。
3. **为什么不用 Update 每帧修布局？** 布局是静态规则，应该保存在 Prefab；运行时只在数据变化时刷新文字和进度，避免无意义计算。
4. **长任务名称怎么处理？** 先限制在独立 RectTransform 中并允许 Best Fit，仍放不下时截断，不能让文字溢出到奖励区域。
5. **怎样避免以后重新生成又恢复错误布局？** 同时修改幂等生成工具和生成后的 Prefab，并用 EditMode 资产测试保护关键边界。

### 8. 本次涉及知识点

- RectTransform 的 Anchor、Pivot、anchoredPosition 和 sizeDelta
- UGUI VerticalLayoutGroup 与 LayoutElement
- UI 素材装饰区和动态内容安全区
- Text Best Fit、Wrap 与 Truncate
- Prefab 编辑器生成工具的幂等性和最小影响范围
- EditMode 资产测试与 RectTransform 几何边界检查

## 功能名称：商店被任务模态层遮挡修复

### 1. 实现目标

修复加入 Mushroom 任务 UI 后，Fungi 商店虽然收到打开事件、却被任务功能根节点中的全屏 UI 遮挡，表现为按 E 后商店打不开的问题。修复同时保证商店、对话和任务窗口在运行时默认关闭，并让最后打开的功能窗口显示在 Canvas 最上层。

### 2. 涉及脚本

- `MerchantShopPanel.cs`：运行时关闭商店相关根节点；打开 Fungi 对话或商店前提升整个 `MerchantShopFeature`。
- `QuestPanel.cs`：运行时关闭任务相关根节点；打开任务窗口前提升整个 `QuestFeature`。
- `QuestFeatureSetupTool.cs`：生成 `GameplayUiRoot.prefab` 时把 `QuestModal` 保存为未激活状态。
- `GameplayUiRoot.prefab`：保存商店、对话、任务等模态根节点的正确默认状态。
- `ShopFeatureAssetTests.cs`、`QuestFeatureAssetTests.cs`：验证模态根节点默认隐藏，并验证商店与任务功能根节点处于同一个 Canvas 层级。
- `QuestFeatureTestRunner.cs`：增加不会切换或保存当前场景的共享模态资源冒烟检查。

### 3. 调用流程

打开商店：`Fungi 触发范围 -> MerchantNpcController 检测 E -> ShopOpenRequestedEvent -> MerchantShopPanel -> 检查其它模态窗口 -> MerchantShopFeature.SetAsLastSibling -> 显示 ShopPanel`。

打开任务：`Mushroom 触发范围 -> QuestNpcController 检测 E -> QuestPanelOpenRequestedEvent -> QuestPanel -> 检查其它模态窗口 -> QuestFeature.SetAsLastSibling -> 显示 QuestModal`。

### 4. 核心原理

UGUI 在同一个 Canvas 中通常按照 Hierarchy 的兄弟顺序绘制，越靠后的对象越晚绘制，也就越容易显示在上面。商店和任务不是两个普通 Panel 兄弟，而是分别放在 `MerchantShopFeature` 与 `QuestFeature` 两个功能根节点下面。因此只对内部 `ShopPanel` 调用 `SetAsLastSibling`，只能改变它在商店功能内部的顺序，无法越过排在后面的整个 `QuestFeature`。

本次把层级提升放到功能根节点：哪个模态窗口最后成功打开，就把对应 Feature 移到 Canvas 末尾。与此同时，`Awake` 会在运行时尽早关闭可能用于 Prefab 排版预览的全屏根节点，避免透明但可射线检测的 Graphic 抢走点击或遮住其它 UI。打开前仍通过 `GameSessionUi` 检查暂停、背包和另一套 NPC 模态，保证互斥规则不被绘制顺序替代。

### 5. Unity 测试方式

1. 打开 `MainScene` 并运行游戏，先不要靠近 NPC，确认任务和商店窗口都不会自动出现。
2. 靠近右侧 Fungi，按 E 进入对话，再点击进入商店，确认商品列表正常显示并可关闭。
3. 靠近左侧 Mushroom，按 E 打开任务面板，关闭后再次回到 Fungi 打开商店。
4. 反向测试：先打开商店并关闭，再打开任务面板，确认最后打开的窗口位于最上层。
5. 验证商店、任务、背包和暂停窗口不能同时打开，ESC 关闭后角色输入和鼠标状态恢复。
6. 在 EditMode Test Runner 中运行 `ShopFeatureAssetTests` 和 `QuestFeatureAssetTests`。

### 6. 面试表达

加入任务 UI 后，商店打不开并不是 NPC 触发器或 E 键失效，而是 UGUI 的层级问题。商店和任务分别在两个 Feature 根节点下，之前只把内部 ShopPanel 移到最后，它仍然无法越过整个 QuestFeature。我把显示顺序的控制提升到 Feature 根节点，最后打开哪个窗口就把哪个 Feature 放到 Canvas 最后，同时在 Awake 关闭编辑预览可能留下的全屏模态对象，并保留 GameSessionUi 的互斥检查。这样既修复遮挡，也避免两个面板同时抢输入。

### 7. 面试追问

1. **为什么 ShopPanel.SetAsLastSibling 不够？** 它只能改变同一个父节点内部的顺序；真正参与跨功能排序的是两个 Feature 根节点。
2. **为什么全屏对象透明也会有问题？** `Image` 等 Graphic 即使看起来透明，只要开启 Raycast Target，仍可能拦截指针事件；激活的全屏层也会影响绘制结果。
3. **为什么在 Awake 隐藏，而不是等 Start？** Awake 更早执行，可在首帧 UI 输入和其它组件初始化前消除错误的默认可见状态。
4. **只调整层级会不会让两个窗口同时打开？** 所以仍保留 `GameSessionUi` 的互斥状态检查；层级只负责显示，状态管理负责是否允许打开。
5. **如何防止生成工具以后把问题带回来？** 生成工具将 QuestModal 固定保存为未激活，并用资产测试断言默认状态和父级关系。

### 8. 本次涉及知识点

- UGUI Canvas 绘制顺序与 Hierarchy 兄弟索引
- `Transform.SetAsLastSibling` 的作用范围
- Feature 根节点与内部 Panel 的层级区别
- `Awake`、`OnEnable`、`Start` 的初始化时序
- Graphic Raycast Target 与透明 UI 的输入拦截
- 模态窗口互斥、暂停和输入状态管理
- Prefab 默认激活状态与 EditMode 资产测试

## 功能名称：宝箱金币安全掉落点修复

### 1. 实现目标

修复宝箱被击破后，金币生成在宝箱实体碰撞体内部，玩家被碰撞体挡住而无法进入拾取 Trigger 的问题。金币现在会生成在宝箱朝向玩家的一侧，并完整避开宝箱碰撞体。

### 2. 涉及脚本

- `VaultGoldRewardController.cs`：击破结算时读取当前玩家方向、宝箱碰撞体和金币 Trigger 半径，计算安全生成位置。
- `WorldPickupSpawnUtility.cs`：负责寻找碰撞体外表面和保留掉落物安全净空，不处理实例化或奖励业务。
- `WorldPickupSpawnUtilityTests.cs`：验证多个玩家方向、无碰撞体备用路径，以及实际 Box/金币 Prefab 配置。

### 3. 调用流程

`玩家攻击宝箱 -> BoxCo.HandleDestroyed -> OnVaultDestroyed -> VaultGoldRewardController -> 获取玩家方向与金币半径 -> WorldPickupSpawnUtility.CalculateOutsidePosition -> WorldGoldPool.Get -> WorldGoldPickup.OnTriggerEnter -> AddGoldCommand`。

### 4. 核心原理

旧逻辑使用“宝箱坐标向上 0.8 米”作为金币位置，但 `MainScene` 中宝箱碰撞体从底部一直覆盖到三米多高，因此这个位置仍在实体内部。金币虽然使用 Trigger，但玩家自己的碰撞体不能穿过宝箱实体，自然无法触发拾取。

新逻辑先根据玩家相对宝箱的水平方向，在宝箱外创建一个探测点，再使用 `Collider.ClosestPoint` 找到这一侧的真实表面。随后沿表面外法线移动“金币触发球半径 + 额外间距”，确保不是只有金币中心在外面，而是整个拾取范围都离开宝箱。玩家攻击宝箱时所在的一侧通常已经可通行，所以比固定向前或向右掉落更可靠。

位置计算被拆成无状态工具类，奖励控制器只负责决定何时掉落、掉多少金币和使用哪个方向；对象池仍只负责创建、复用与回收。这样几何计算、奖励规则和生命周期互不混杂。

### 5. Unity 测试方式

1. 打开 `MainScene`，从宝箱左、右、前、后等不同方向将其击破。
2. 确认金币出现在玩家这一侧，而不是宝箱模型或碰撞体内部。
3. 靠近金币，确认立刻拾取、播放金币音效并刷新金币 HUD。
4. 连续击破多次，确认对象池复用的金币每次都会移动到新的正确位置。
5. 在 EditMode Test Runner 中运行 `WorldPickupSpawnUtilityTests`。
6. 也可以执行菜单 `Tools/Treasure Hunter/Validate Vault Gold Spawn` 运行不切换场景的专项测试。

### 6. 面试表达

宝箱金币无法拾取的原因是生成点只做了向上偏移，但这个偏移仍位于宝箱的实体碰撞体内。我的修复不是简单写死另一个坐标，而是根据玩家攻击宝箱时所在的方向，用 Collider.ClosestPoint 找到朝向玩家的一侧表面，再把金币沿外法线推出“拾取球半径加安全间距”。这样整个 Trigger 都在碰撞体外，而且奖励会落在玩家已经能够到达的一侧。位置计算单独放在无状态工具类，奖励发放和对象池逻辑都不需要改。

### 7. 面试追问

1. **为什么只把金币中心移到碰撞体外还不够？** 金币的 SphereCollider 有半径，中心在外但球体仍可能与宝箱重叠，所以要把半径计入安全距离。
2. **为什么使用 Collider.ClosestPoint？** 它可以根据实际碰撞体形状返回最近表面点，不需要把 BoxCollider 尺寸写死进玩法代码。
3. **为什么朝玩家方向掉落？** 玩家能够攻击宝箱说明这一侧通常可通行，可以减少金币生成到墙后或其它障碍物一侧的概率。
4. **为什么不在击破时关闭宝箱碰撞体？** 宝箱会原地重生，保留碰撞体可以避免玩家和怪物在重生期间进入模型内部；调整奖励位置影响更小。
5. **对象池复用会不会保留旧位置？** `WorldGoldPool.Get` 每次取出对象都会重新设置位置和旋转，随后 `Configure` 重置金额、生命周期和悬浮状态。

### 8. 本次涉及知识点

- Collider、Trigger 与 Rigidbody 的物理交互区别
- `Collider.ClosestPoint` 和碰撞体表面点计算
- 向量投影、归一化和表面外法线
- SphereCollider 半径与安全净空
- 玩家方向驱动的可达掉落位置
- 对象池对象的位置和运行时状态重置
- 无状态工具类与奖励业务职责拆分
- EditMode 几何回归测试

## 功能名称：项目文档与求职包装统一

### 1. 实现目标

把 README、客户端架构、策划案和简历描述统一到当前真实实现，并将游戏对外名称统一为《宝藏猎手》。旧策划案继续保留方案演进价值，但必须明确标记为历史版本，避免把 MMO-Lite、多人系统或未验证指标误写成已经完成。

### 2. 涉及脚本

- `README.md`：当前玩法、系统、运行方式、边界和测试说明。
- `Docs/ClientArchitecture.md`：当前 QFramework、玩家组件、背包装备、商店任务和存档结构。
- `Docs/README.md`：区分当前文档、历史策划案、学习记录和自动生成清单。
- `Docs/ResumeProjectDescription.md`：一页简历文案、面试表达和禁止夸大的边界。
- `Assets/Docs/宝藏猎手策划案.docx`：当前实现版策划案。
- `ProjectSettings/ProjectSettings.asset`：Unity 对外产品名。

### 3. 调用流程

`当前代码/Prefab/场景/配置 -> 功能事实盘点 -> README与架构文档 -> 当前策划案 -> 简历要点 -> 面试表达`。

### 4. 核心原理

项目文档不能只复制旧策划目标，而应区分“已经实现”“当前边界”和“后续规划”。简历中的每条描述都应该能在代码、场景、配置、测试或真实测量结果中找到证据。技术类名和仓库路径属于内部标识，对外游戏名称属于产品标识；只修改产品名而不盲目重命名全部代码，可以避免无收益的资源引用和协议风险。

### 5. Unity 测试方式

1. 打开 Unity，确认应用产品名显示为“宝藏猎手”。
2. 从 `LoginScene` 走游客模式，核对 README 中的按键、场景和核心循环。
3. 检查背包、装备、商店、任务、设置、音频和 Boss 流程与文档描述一致。
4. 运行完整 EditMode 测试并单独保存结果；在没有结果前，不在简历中填写通过率。
5. 打开当前与历史 Word 策划案，确认当前入口和历史状态清楚可辨。

### 6. 面试表达

我没有直接把旧策划案里的所有目标都写进简历，而是先以当前代码、Prefab、场景和配置为事实来源，把项目分成已实现、当前边界和后续计划。对外名称统一为《宝藏猎手》，但保留已经参与代码引用的 TreasureHunter 技术标识，避免为了改名字引入大范围资源和协议风险。简历重点放在组件化玩家、QFramework 业务分层、对象池、背包装备与双数据源存档，并主动说明多人同步和性能指标还没有完成或测量。

### 7. 面试追问

1. **为什么历史策划案不直接删除？** 它能记录需求和方案演进，但必须标注状态，不能继续作为当前完成功能的证据。
2. **为什么不把所有类名一起改成中文游戏名？** 类名、命名空间、资源引用和协议属于技术标识，大范围改名需要单独迁移和回归，本轮只统一产品展示名。
3. **简历为什么不写性能提升百分比？** 没有相同环境下的前后测量就无法证明，面试追问时也无法给出可信测试条件。
4. **怎样保证 README 不会再次过期？** 新功能完成后同时追加学习记录，并检查 README、架构、存档边界和当前策划案是否受影响。
5. **项目系统很多，简历应该选哪些？** 优先选择能讲完整调用链和异常边界的模块，例如玩家架构、对象池、装备事务和双数据源存档。

### 8. 本次涉及知识点

- 技术文档的事实来源与版本管理
- 产品名称与技术标识的边界
- 简历真实性与可验证证据
- 当前功能、已知边界和后续规划的区分
- 架构图、调用链和面试表达
- 测试结果与性能指标的测量前提
