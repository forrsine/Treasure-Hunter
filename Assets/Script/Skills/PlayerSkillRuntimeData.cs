using System;

/// <summary>
/// 玩家已学习技能的运行时数据。
/// 注意：SkillDefine 保存“技能静态配置”，例如蓝耗、冷却、伤害倍率；
/// 这个类保存“玩家当前状态”，例如这个玩家有没有学会、当前几级、冷却剩多久。
/// </summary>
[Serializable]
public class PlayerSkillRuntimeData
{
    /// <summary>
    /// 技能 ID，对应 SkillDefine.json 里的 skillId。
    /// 后续客户端和服务器同步技能时，也应该用 skillId，而不是用技能名。
    /// </summary>
    public int skillId;

    /// <summary>
    /// 当前技能等级。
    /// 新学习技能时从 1 级开始，升级时递增。
    /// </summary>
    public int level;

    /// <summary>
    /// 当前冷却剩余时间，单位是秒。
    /// 释放技能后设置为配置表里的 cooldown，之后每帧递减。
    /// </summary>
    public float cooldownRemaining;

    /// <summary>
    /// 构造一个已学习技能数据。
    /// </summary>
    public PlayerSkillRuntimeData(int skillId, int level)
    {
        this.skillId = skillId;
        this.level = Math.Max(1, level);
        cooldownRemaining = 0f;
    }

    /// <summary>
    /// 技能是否正在冷却中。
    /// 后续释放技能前会先判断它，冷却中就不允许释放。
    /// </summary>
    public bool IsCoolingDown()
    {
        return cooldownRemaining > 0f;
    }

    /// <summary>
    /// 设置技能冷却。
    /// 释放技能成功后调用，避免玩家连续无间隔释放。
    /// </summary>
    public void StartCooldown(float cooldown)
    {
        cooldownRemaining = Math.Max(0f, cooldown);
    }

    /// <summary>
    /// 推进冷却计时。
    /// 后续 PlayerSkillSystem 会在每帧或固定入口调用它。
    /// </summary>
    public void TickCooldown(float deltaTime)
    {
        if (cooldownRemaining <= 0f)
        {
            cooldownRemaining = 0f;
            return;
        }

        cooldownRemaining = Math.Max(0f, cooldownRemaining - Math.Max(0f, deltaTime));
    }

    /// <summary>
    /// 升级技能等级。
    /// 这里只修改运行时等级，不判断是否超过最大等级；
    /// 最大等级判断会放到 PlayerSkillModel 或 PlayerSkillSystem，避免数据类承担太多规则。
    /// </summary>
    public void SetLevel(int newLevel)
    {
        level = Math.Max(1, newLevel);
    }
}