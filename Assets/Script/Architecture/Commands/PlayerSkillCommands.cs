using QFramework;

/// <summary>
/// 增加一次技能选择机会。
/// 后续玩家每到 5 的倍数等级时调用。
/// </summary>
public sealed class AddPendingSkillSelectionCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        this.GetSystem<PlayerSkillSystem>().AddPendingSkillSelection();
    }
}

/// <summary>
/// 处理玩家选择的技能学习/升级项。
/// UI 点击按钮后会发送这个命令。
/// </summary>
public sealed class ResolvePlayerSkillChoiceCommand : AbstractCommand<bool>
{
    private readonly PlayerSkillChoice choice;

    public ResolvePlayerSkillChoiceCommand(PlayerSkillChoice choice)
    {
        this.choice = choice;
    }

    protected override bool OnExecute()
    {
        return this.GetSystem<PlayerSkillSystem>().ResolvePendingSkillChoice(choice);
    }
}

/// <summary>
/// 调试用：直接学习技能。
/// 正常游戏流程建议优先走 ResolvePlayerSkillChoiceCommand。
/// </summary>
public sealed class LearnPlayerSkillCommand : AbstractCommand<bool>
{
    private readonly int skillId;

    public LearnPlayerSkillCommand(int skillId)
    {
        this.skillId = skillId;
    }

    protected override bool OnExecute()
    {
        return this.GetSystem<PlayerSkillSystem>().TryLearnSkill(skillId);
    }
}

/// <summary>
/// 调试用：直接升级技能。
/// 正常游戏流程建议优先走 ResolvePlayerSkillChoiceCommand。
/// </summary>
public sealed class UpgradePlayerSkillCommand : AbstractCommand<bool>
{
    private readonly int skillId;

    public UpgradePlayerSkillCommand(int skillId)
    {
        this.skillId = skillId;
    }

    protected override bool OnExecute()
    {
        return this.GetSystem<PlayerSkillSystem>().TryUpgradeSkill(skillId);
    }
}

/// <summary>
/// 尝试释放技能。
/// 玩家输入层或技能释放组件不直接操作 PlayerSkillSystem，统一通过 Command 进入规则层。
/// </summary>
public sealed class TryCastPlayerSkillCommand : AbstractCommand<bool>
{
    private readonly int skillId;

    public TryCastPlayerSkillCommand(int skillId)
    {
        this.skillId = skillId;
    }

    protected override bool OnExecute()
    {
        return this.GetSystem<PlayerSkillSystem>().TryCastSkill(skillId);
    }
}