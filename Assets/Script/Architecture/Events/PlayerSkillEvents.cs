/// <summary>
/// 玩家技能发生变化。
/// 例如学习新技能或升级技能后，UI 可以监听它刷新技能栏。
/// </summary>
public readonly struct PlayerSkillChangedEvent
{
    public PlayerSkillChangedEvent(int skillId, int level)
    {
        SkillId = skillId;
        Level = level;
    }

    public int SkillId { get; }
    public int Level { get; }
}

/// <summary>
/// 待处理技能选择次数变化。
/// 例如玩家到达 5、10、15 级时次数增加，技能选择面板可以监听它决定是否弹出。
/// </summary>
public readonly struct PlayerSkillSelectionQueueChangedEvent
{
    public PlayerSkillSelectionQueueChangedEvent(int count)
    {
        Count = count;
    }

    public int Count { get; }
}


/// <summary>
/// 玩家释放技能失败事件。
/// 例如：没学技能、技能冷却中、蓝量不足。
/// 注意：技能系统只负责发出失败原因，不直接控制 UI 显示。
/// </summary>
public readonly struct PlayerSkillCastFailedEvent
{
    public PlayerSkillCastFailedEvent(int skillId, string message)
    {
        SkillId = skillId;
        Message = message;
    }

    public int SkillId { get; }
    public string Message { get; }
}