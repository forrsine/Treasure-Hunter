# Treasure Hunter 客户端架构

## 1. 总体结构

客户端采用“QFramework 核心架构 + Unity 组件”的混合方案：

```text
Unity 输入/碰撞/UI
        ↓ Command / Query
TreasureHunterArchitecture
        ↓
Model / System
        ↓ Event
Unity 动画/音效/UI
```

## 2. 依赖规则

1. `Model` 只保存权威运行时数据，不引用 GameObject、Animator 或 UI；外部只能拿到 `IPlayerStatsReadOnly`。
2. `System` 负责可复用业务规则，例如伤害、升级和属性计算。
3. `Command` 是修改数据的唯一上层入口。
4. `Query` 只返回不可变快照或 UI DTO，不把 Model 内部可变对象泄漏出去。
5. `Event` 从数据层通知表现层，不携带 Unity 对象。
6. MonoBehaviour 负责输入、碰撞和表现，不保存另一套角色属性。
7. 四个职业的差异来自 `CharacterDefine`，通用逻辑只存在于 `PlayerRuntime.prefab`。
8. 网络消息总线与玩法领域事件分离，避免协议 DTO 直接进入 UI。
9. 跑步、翻滚、攻击中、体力等瞬时表现状态留在对应组件，不放入全局 Model。
10. 玩法 UI 由场景级 `GameplayUiRoot.prefab` 持有，玩家 Prefab 不挂 UI，也不负责自动添加 UI 组件。
11. 运行时 UI 只消费 Prefab 序列化引用；缺失引用时明确报错，不查找、不创建、不删除 UI 节点。

## 3. 玩家模块

| 模块 | 职责 |
|---|---|
| `PlayerModel` | 血量、等级、经验、攻击、移动速度等权威数据 |
| `PlayerCombatSystem` | 暴击、减伤、闪避、回血、吸血 |
| `PlayerProgressionSystem` | 经验、升级、属性三选一 |
| `PlayerRuntimeController` | Unity 组件装配和执行顺序 |
| `PlayerMovementComponent` | 输入、移动、跳跃、翻滚、体力 |
| `PlayerCombatComponent` | 攻击输入、连击、攻击碰撞盒 |
| `PlayerHealthComponent` | 受击入口和受击表现 |
| `PlayerPresentationComponent` | 四职业 Animator 参数适配 |
| `PlayerAudioComponent` | 玩家动作音效 |
| `IPlayerStatsReadOnly` | 向表现层暴露 getter-only 玩家属性契约 |
| `PlayerStatsSnapshot` | Query 返回的不可变值副本，隔离 Model 内部对象 |
| `GameplayUiRoot` | 场景级 UI 组合根，显式装配会话、属性和升级界面 |
| `PlayerAttributeRowView` | Prefab 中固定属性行，按 key 接收 Query 数据并播放数值高亮 |

## 4. 扩展约定

- 新增属性：先扩展 Model，再由 System 修改，最后通过 Query/Event 接入 UI。
- 新增职业：新增职业配置和表现 Prefab，不复制玩家逻辑脚本。
- 新增攻击类型：攻击源调用 `FighterInterface`，最终通过 Command 进入战斗系统。
- 新增 UI：放入场景级 UI Prefab，通过 Query 读取快照、通过 Event 刷新，不挂玩家、不缓存可写数据。
- 新增属性：除扩展 Query 外，还要在 `GameplayUiRoot.prefab` 中增加相同 key 的静态属性行。
- Prefab 引用缺失：修复资产本身，不允许在运行时用 `Find` 或 `new GameObject` 掩盖装配错误。

## 5. 自动验证

Unity 菜单执行：

`Tools/Treasure Hunter/Validate Player Architecture`

验证器会检查通用玩家组件、攻击盒、四职业表现 Prefab、独立 GameplayUiRoot、Missing Script，并阻止旧巨型玩家控制脚本或 UI 重新挂回玩家 Prefab。

EditMode 测试位置：`Assets/Editor/Tests/PlayerModelReadOnlyTests.cs`。测试覆盖只读契约、快照隔离、伤害、回血、死亡、暴击、升级、UI 装配边界和禁止运行时 UI 构建规则。
