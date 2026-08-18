using System;
using System.Collections.Generic;

/// <summary>
/// 技能类型。
/// 注意：JSON 里用字符串保存技能类型，例如 "ProjectileAoe"。
/// 这样配置表更容易看懂，也方便以后给策划维护。
/// </summary>
public enum SkillType
{
    /// <summary>
    /// 配置无效。非法类型不能进入技能池，也不能扣蓝或开始冷却。
    /// </summary>
    Invalid = -1,

    /// <summary>
    /// 投射物范围技能，例如大火球。
    /// </summary>
    ProjectileAoe,

    /// <summary>
    /// 持续范围技能，例如毒雾领域。
    /// </summary>
    AreaDot,

    /// <summary>
    /// 以玩家自身为中心的范围技能，例如镰刀大旋转。
    /// </summary>
    SelfAoe
}

/// <summary>
/// 技能配置表根节点。
/// JsonUtility 读取 JSON 时，需要一个外层对象包住数组，不能直接读取裸数组。
/// </summary>
[Serializable]
public class SkillDefineTable
{
    /// <summary>
    /// 技能列表，对应 SkillDefine.json 里的 skills 字段。
    /// </summary>
    public List<SkillDefine> skills;
}

/// <summary>
/// 单个技能的静态配置。
/// 这里只保存不会在战斗中频繁变化的数据，例如技能名字、职业限制、最高等级和每级数值。
/// 玩家是否已经学会、当前冷却剩多久，不放在这里，后面会放到 PlayerSkillModel。
/// </summary>
[Serializable]
public class SkillDefine
{
    /// <summary>
    /// 技能唯一 ID。
    /// 后期如果接服务器，客户端和服务器都应该通过 skillId 识别技能，不要靠中文名判断。
    /// </summary>
    public int skillId;

    /// <summary>
    /// 技能英文标识，方便程序调试和日志输出。
    /// </summary>
    public string skillKey;

    /// <summary>
    /// 技能中文名，用于 UI 显示。
    /// </summary>
    public string name;

    /// <summary>
    /// 技能描述，用于技能选择面板和技能详情。
    /// </summary>
    public string description;

    /// <summary>
    /// 技能类型字符串。
    /// 这里不用 enum 直接接 JSON，是为了避免 JsonUtility 对 enum 字符串解析不稳定。
    /// </summary>
    public string skillType;

    /// <summary>
    /// 是否是通用技能。
    /// true 表示所有职业都可以学习，例如大火球、毒雾领域。
    /// </summary>
    public bool isCommon;

    /// <summary>
    /// 允许学习该技能的职业 ID。
    /// 通用技能可以留空；刺客专属技能填 4。
    /// </summary>
    public List<int> allowedClassIds;

    /// <summary>
    /// 技能最高等级。
    /// 后续技能升级时，会用它判断是否已经满级。
    /// </summary>
    public int maxLevel;

    /// <summary>
    /// 技能进入技能池的等级。
    /// 第一版统一写 5，表示 5 级后才可能学到。
    /// </summary>
    public int unlockLevel;

    /// <summary>
    /// 每一级的技能数值配置。
    /// 例如 Lv.1、Lv.2、Lv.3 的蓝耗、冷却、伤害倍率不同。
    /// </summary>
    public List<SkillLevelDefine> levels;

    /// <summary>
    /// 尝试把 JSON 里的 skillType 字符串转换成 SkillType 枚举。
    /// 非法类型返回 false，让加载和释放流程能够阻止错误配置继续执行。
    /// </summary>
    public bool TryGetSkillType(out SkillType result)
    {
        if (!string.IsNullOrWhiteSpace(skillType) &&
            Enum.TryParse(skillType, false, out result) &&
            result != SkillType.Invalid &&
            Enum.IsDefined(typeof(SkillType), result) &&
            string.Equals(skillType, result.ToString(), StringComparison.Ordinal))
        {
            return true;
        }

        result = SkillType.Invalid;
        return false;
    }

    /// <summary>
    /// 获取技能类型。非法配置会返回 Invalid，不再静默回退成 SelfAoe。
    /// 调用方必须显式处理 Invalid，避免错误技能扣蓝并执行错误效果。
    /// </summary>
    public SkillType GetSkillType()
    {
        return TryGetSkillType(out SkillType result) ? result : SkillType.Invalid;
    }

    /// <summary>
    /// 判断某个职业是否允许学习这个技能。
    /// 通用技能直接允许；非通用技能需要检查 allowedClassIds。
    /// </summary>
    public bool CanLearnByClass(int classId)
    {
        if (isCommon)
        {
            return true;
        }

        if (allowedClassIds == null)
        {
            return false;
        }

        return allowedClassIds.Contains(classId);
    }

    /// <summary>
    /// 根据技能等级获取对应数值。
    /// 例如传入 1，就返回 Lv.1 的蓝耗、冷却、伤害倍率。
    /// </summary>
    public SkillLevelDefine GetLevelData(int level)
    {
        if (levels == null)
        {
            return null;
        }

        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null && levels[i].level == level)
            {
                return levels[i];
            }
        }

        return null;
    }
}

/// <summary>
/// 单个技能某一级的数值配置。
/// 这些数值以后主要给 PlayerSkillSystem 使用，例如扣蓝、判断冷却、计算伤害。
/// </summary>
[Serializable]
public class SkillLevelDefine
{
    /// <summary>
    /// 技能等级，从 1 开始。
    /// </summary>
    public int level;

    /// <summary>
    /// 释放技能消耗的 MP。
    /// </summary>
    public int mpCost;

    /// <summary>
    /// 技能冷却时间，单位是秒。
    /// </summary>
    public float cooldown;

    /// <summary>
    /// 伤害倍率。
    /// 例如玩家攻击力是 50，damageRate 是 1.5，则技能基础伤害约等于 75。
    /// </summary>
    public float damageRate;

    /// <summary>
    /// 技能范围。
    /// 火球表示爆炸半径，毒雾表示毒雾半径，镰刀旋转表示自身攻击半径。
    /// </summary>
    public float radius;

    /// <summary>
    /// 持续时间。
    /// 毒雾会用到；火球和镰刀旋转暂时填 0。
    /// </summary>
    public float duration;

    /// <summary>
    /// 持续技能的间隔时间。
    /// 例如毒雾每 1 秒造成一次伤害。
    /// </summary>
    public float tickInterval;

    /// <summary>
    /// 减速比例。
    /// 例如 0.4 表示降低 40% 移动速度。
    /// </summary>
    public float slowRate;
}
