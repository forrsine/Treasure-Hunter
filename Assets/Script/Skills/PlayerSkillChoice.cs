using System;

/// <summary>
/// 技能选择类型。
/// Learn 表示学习新技能；Upgrade 表示升级已拥有技能。
/// </summary>
public enum PlayerSkillChoiceType
{
    Learn = 0,
    Upgrade = 1
}

/// <summary>
/// 技能三选一面板中的一个候选项。
/// 注意：它不是技能配置本身，而是“这次面板准备给玩家的选择结果”。
/// </summary>
[Serializable]
public class PlayerSkillChoice
{
    public int skillId;
    public PlayerSkillChoiceType choiceType;
    public int currentLevel;
    public int nextLevel;

    public PlayerSkillChoice(int skillId, PlayerSkillChoiceType choiceType, int currentLevel, int nextLevel)
    {
        this.skillId = skillId;
        this.choiceType = choiceType;
        this.currentLevel = currentLevel;
        this.nextLevel = nextLevel;
    }

    /// <summary>
    /// 是否是学习新技能选项。
    /// UI 后面可以根据它显示“学习”或“升级”。
    /// </summary>
    public bool IsLearnChoice()
    {
        return choiceType == PlayerSkillChoiceType.Learn;
    }
}