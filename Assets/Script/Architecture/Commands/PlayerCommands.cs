using QFramework;

/// <summary>
/// 进入玩法场景时初始化玩家模型。
/// 场景对象不直接改 Model，而是统一发 Command 给 System，
/// 这样规则入口更集中，后面扩日志、回放或联网同步都会更好接。
/// </summary>
public sealed class InitializePlayerCommand : AbstractCommand
{
    private readonly NCharacter save;
    private readonly CharacterDefine define;

    public InitializePlayerCommand(NCharacter save, CharacterDefine define)
    {
        this.save = save;
        this.define = define;
    }

    protected override void OnExecute()
    {
        this.GetSystem<PlayerProgressionSystem>().InitializePlayer(save, define);
    }
}

/// <summary>
/// 统一经验入口，掉落物和金库不直接修改等级数据。
/// 外部世界只负责表达“给玩家多少经验”，
/// 升级、经验溢出和升级奖励一律交给成长系统。
/// </summary>
public sealed class AddPlayerExpCommand : AbstractCommand
{
    private readonly int amount;
    public AddPlayerExpCommand(int amount) => this.amount = amount;
    protected override void OnExecute() => this.GetSystem<PlayerProgressionSystem>().AddExp(amount);
}

/// <summary>
/// 开发者快速升级命令：增加指定等级，但不生成属性三选一。
/// 它仍然进入正式升级结算，因此技能选择、升级回血和回蓝等规则都会正常触发。
/// </summary>
public sealed class AddPlayerLevelsForDevelopmentCommand : AbstractCommand<int>
{
    private readonly int levelCount;

    public AddPlayerLevelsForDevelopmentCommand(int levelCount)
    {
        this.levelCount = levelCount;
    }

    protected override int OnExecute()
    {
        return this.GetSystem<PlayerProgressionSystem>().AddLevelsForDevelopment(levelCount);
    }
}

/// <summary>
/// 玩家受伤命令，返回结算结果给表现层。
/// 表现层只决定要不要飘字、闪红或播放死亡动画，
/// 不会自己参与伤害公式计算。
/// </summary>
public sealed class TakePlayerDamageCommand : AbstractCommand<PlayerDamageResult>
{
    private readonly int incomingAttackPower;
    private readonly bool allowDodge;

    public TakePlayerDamageCommand(int incomingAttackPower, bool allowDodge)
    {
        this.incomingAttackPower = incomingAttackPower;
        this.allowDodge = allowDodge;
    }

    protected override PlayerDamageResult OnExecute()
    {
        return this.GetSystem<PlayerCombatSystem>().TakeDamage(incomingAttackPower, allowDodge);
    }
}

/// <summary>
/// 玩家治疗命令。
/// 不管回血来自升级、吸血还是持续恢复，都走统一入口，方便 UI 和事件系统一起监听。
/// </summary>
public sealed class HealPlayerCommand : AbstractCommand<int>
{
    private readonly int amount;
    private readonly bool showFloatingText;

    public HealPlayerCommand(int amount, bool showFloatingText)
    {
        this.amount = amount;
        this.showFloatingText = showFloatingText;
    }

    protected override int OnExecute()
    {
        return this.GetSystem<PlayerCombatSystem>().Heal(amount, showFloatingText);
    }
}

/// <summary>
/// 直接回满血量，常用于补给或调试入口。
/// </summary>
public sealed class FullHealPlayerCommand : AbstractCommand<int>
{
    protected override int OnExecute() => this.GetSystem<PlayerCombatSystem>().FullHeal();
}

/// <summary>
/// 尝试消耗玩家魔法值。
/// 技能系统后续释放技能前先调用它，返回 false 就取消释放，避免技能表现和资源扣除不同步。
/// </summary>
public sealed class TrySpendPlayerManaCommand : AbstractCommand<bool>
{
    private readonly int amount;
    public TrySpendPlayerManaCommand(int amount) => this.amount = amount;
    protected override bool OnExecute() => this.GetSystem<PlayerResourceSystem>().TrySpendMana(amount);
}

/// <summary>
/// 恢复玩家魔法值，返回实际恢复量。
/// </summary>
public sealed class RestorePlayerManaCommand : AbstractCommand<int>
{
    private readonly int amount;
    public RestorePlayerManaCommand(int amount) => this.amount = amount;
    protected override int OnExecute() => this.GetSystem<PlayerResourceSystem>().RestoreMana(amount);
}

/// <summary>
/// 直接回满魔法值，常用于补给或调试。
/// </summary>
public sealed class FullRestorePlayerManaCommand : AbstractCommand<int>
{
    protected override int OnExecute() => this.GetSystem<PlayerResourceSystem>().FullRestoreMana();
}

/// <summary>
/// 请求一次攻击掷骰，得到最终伤害和是否暴击。
/// </summary>
public sealed class RollPlayerAttackCommand : AbstractCommand<PlayerAttackRoll>
{
    protected override PlayerAttackRoll OnExecute() => this.GetSystem<PlayerCombatSystem>().RollAttackDamage();
}

/// <summary>
/// 记录一次真实生效的伤害。
/// 当前主要用于吸血结算，因为吸血应该基于“实际打出去的伤害”而不是理论伤害。
/// </summary>
public sealed class RecordPlayerDamageDealtCommand : AbstractCommand<int>
{
    private readonly int appliedDamage;
    public RecordPlayerDamageDealtCommand(int appliedDamage) => this.appliedDamage = appliedDamage;
    protected override int OnExecute() => this.GetSystem<PlayerCombatSystem>().HandleDamageDealt(appliedDamage);
}

/// <summary>
/// 控制升级选择面板是否处于激活状态。
/// 激活后，玩家移动和战斗逻辑会主动暂停。
/// </summary>
public sealed class SetPlayerUpgradeSelectionStateCommand : AbstractCommand
{
    private readonly bool active;
    public SetPlayerUpgradeSelectionStateCommand(bool active) => this.active = active;
    protected override void OnExecute() => this.GetSystem<PlayerProgressionSystem>().SetUpgradeSelectionState(active);
}

/// <summary>
/// 消耗一个待选升级次数，并结算玩家本次选择的属性。
/// </summary>
public sealed class ResolvePlayerUpgradeCommand : AbstractCommand<bool>
{
    private readonly PlayerAttributeType attributeType;
    public ResolvePlayerUpgradeCommand(PlayerAttributeType attributeType) => this.attributeType = attributeType;
    protected override bool OnExecute() => this.GetSystem<PlayerProgressionSystem>().ResolvePendingUpgradeSelection(attributeType);
}

/// <summary>
/// 仅应用属性升级，不消费升级队列；保留给调试工具使用。
/// 正常游戏流程应优先通过 ResolvePlayerUpgradeCommand 进入。
/// </summary>
public sealed class ApplyPlayerUpgradeCommand : AbstractCommand<bool>
{
    private readonly PlayerAttributeType attributeType;
    public ApplyPlayerUpgradeCommand(PlayerAttributeType attributeType) => this.attributeType = attributeType;
    protected override bool OnExecute() => this.GetSystem<PlayerProgressionSystem>().TryApplyAttributeUpgrade(attributeType);
}
