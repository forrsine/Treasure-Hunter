using System.Collections.Generic;
using QFramework;

/// <summary>
/// 获取本次技能三选一候选。
/// UI 面板打开时调用。
/// </summary>
public sealed class GetPlayerSkillChoicesQuery : AbstractQuery<List<PlayerSkillChoice>>
{
    private readonly int count;

    public GetPlayerSkillChoicesQuery(int count = 3)
    {
        this.count = count;
    }

    protected override List<PlayerSkillChoice> OnDo()
    {
        return this.GetSystem<PlayerSkillSystem>().GetRandomSkillChoices(count);
    }
}

/// <summary>
/// 获取某个技能候选项的显示文本。
/// UI 不自己拼规则文本，避免 UI 和技能规则耦合。
/// </summary>
public sealed class GetPlayerSkillChoiceTextQuery : AbstractQuery<string>
{
    private readonly PlayerSkillChoice choice;

    public GetPlayerSkillChoiceTextQuery(PlayerSkillChoice choice)
    {
        this.choice = choice;
    }

    protected override string OnDo()
    {
        return this.GetSystem<PlayerSkillSystem>().GetSkillChoiceText(choice);
    }
}