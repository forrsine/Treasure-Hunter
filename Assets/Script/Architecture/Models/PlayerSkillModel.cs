using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家技能模型：保存当前玩家这一局已经学习的技能、技能等级、冷却状态和待处理技能选择次数。
/// 注意：Model 只负责保存和维护数据，不读取输入、不播放特效、不操作 UI。
/// </summary>
public sealed class PlayerSkillModel : AbstractModel
{
    /// <summary>
    /// 已学习技能字典。
    /// key 是 skillId，value 是该技能在玩家身上的运行时数据。
    /// 用字典是为了快速判断“玩家是否已经学会某个技能”。
    /// </summary>
    private readonly Dictionary<int, PlayerSkillRuntimeData> learnedSkillMap =
        new Dictionary<int, PlayerSkillRuntimeData>();

    /// <summary>
    /// 待处理技能选择次数。
    /// 玩家每到 5 的倍数等级时，后续会给这里 +1。
    /// UI 看到它大于 0，就弹出技能三选一面板。
    /// </summary>
    public int PendingSkillSelectionCount { get; private set; }

    /// <summary>
    /// QFramework Model 初始化入口。
    /// 这里暂时不需要加载数据，因为当前第一版技能是局内运行时数据。
    /// </summary>
    protected override void OnInit()
    {
    }

    /// <summary>
    /// 新开一局或切换角色时重置技能数据。
    /// 第一版可以先清空；后续如果要做存档，可以在这里从服务器或本地存档恢复已学技能。
    /// </summary>
    public void Reset()
    {
        learnedSkillMap.Clear();
        PendingSkillSelectionCount = 0;
    }

    /// <summary>
    /// 判断玩家是否已经学会指定技能。
    /// 技能选择面板和释放技能前都会用到。
    /// </summary>
    public bool HasSkill(int skillId)
    {
        return learnedSkillMap.ContainsKey(skillId);
    }

    /// <summary>
    /// 学习一个新技能。
    /// 成功返回 true；如果已经学过或配置不存在，返回 false。
    /// </summary>
    public bool LearnSkill(int skillId)
    {
        if (HasSkill(skillId))
        {
            Debug.LogWarning($"玩家已经学习过技能：skillId = {skillId}");
            return false;
        }

        if (SkillDataManager.Instance == null || SkillDataManager.Instance.GetSkill(skillId) == null)
        {
            Debug.LogError($"学习技能失败，找不到技能配置：skillId = {skillId}");
            return false;
        }

        learnedSkillMap.Add(skillId, new PlayerSkillRuntimeData(skillId, 1));
        Debug.Log($"学习技能成功：skillId = {skillId}");
        return true;
    }

    /// <summary>
    /// 升级一个已学习技能。
    /// 成功返回 true；如果没学过或已经满级，返回 false。
    /// </summary>
    public bool UpgradeSkill(int skillId)
    {
        if (!learnedSkillMap.TryGetValue(skillId, out PlayerSkillRuntimeData runtimeData))
        {
            Debug.LogWarning($"升级技能失败，玩家还没有学习该技能：skillId = {skillId}");
            return false;
        }

        SkillDefine skillDefine = SkillDataManager.Instance != null
            ? SkillDataManager.Instance.GetSkill(skillId)
            : null;

        if (skillDefine == null)
        {
            Debug.LogError($"升级技能失败，找不到技能配置：skillId = {skillId}");
            return false;
        }

        if (runtimeData.level >= skillDefine.maxLevel)
        {
            Debug.LogWarning($"技能已经满级，无法继续升级：skillId = {skillId}");
            return false;
        }

        runtimeData.SetLevel(runtimeData.level + 1);
        Debug.Log($"升级技能成功：skillId = {skillId}, 当前等级 = {runtimeData.level}");
        return true;
    }

    /// <summary>
    /// 获取玩家某个技能的当前等级。
    /// 没学过返回 0，方便外部判断。
    /// </summary>
    public int GetSkillLevel(int skillId)
    {
        if (learnedSkillMap.TryGetValue(skillId, out PlayerSkillRuntimeData runtimeData))
        {
            return runtimeData.level;
        }

        return 0;
    }

    /// <summary>
    /// 获取某个已学习技能的运行时数据。
    /// 后续释放技能时会用它读取等级和冷却。
    /// </summary>
    public PlayerSkillRuntimeData GetSkillRuntimeData(int skillId)
    {
        learnedSkillMap.TryGetValue(skillId, out PlayerSkillRuntimeData runtimeData);
        return runtimeData;
    }

    /// <summary>
    /// 获取所有已学习技能。
    /// 返回新列表，避免外部直接修改 learnedSkillMap。
    /// </summary>
    public List<PlayerSkillRuntimeData> GetLearnedSkills()
    {
        return new List<PlayerSkillRuntimeData>(learnedSkillMap.Values);
    }

    /// <summary>
    /// 判断某个技能是否可以继续升级。
    /// 技能选择面板会用它筛选“升级已有技能”的选项。
    /// </summary>
    public bool CanUpgradeSkill(int skillId)
    {
        PlayerSkillRuntimeData runtimeData = GetSkillRuntimeData(skillId);
        if (runtimeData == null)
        {
            return false;
        }

        SkillDefine skillDefine = SkillDataManager.Instance != null
            ? SkillDataManager.Instance.GetSkill(skillId)
            : null;

        return skillDefine != null && runtimeData.level < skillDefine.maxLevel;
    }

    /// <summary>
    /// 增加一次待处理技能选择次数。
    /// 后续玩家升到 5、10、15 级时会调用这里。
    /// </summary>
    public void AddPendingSkillSelection()
    {
        PendingSkillSelectionCount++;
    }

    /// <summary>
    /// 消耗一次技能选择次数。
    /// 玩家在技能三选一面板里选择“学习”或“升级”成功后调用。
    /// </summary>
    public bool ConsumePendingSkillSelection()
    {
        if (PendingSkillSelectionCount <= 0)
        {
            return false;
        }

        PendingSkillSelectionCount--;
        return true;
    }

    /// <summary>
    /// 推进所有已学习技能的冷却时间。
    /// 后续会由 PlayerSkillSystem 或 PlayerSkillComponent 每帧调用。
    /// </summary>
    public void TickCooldowns(float deltaTime)
    {
        foreach (PlayerSkillRuntimeData runtimeData in learnedSkillMap.Values)
        {
            runtimeData.TickCooldown(deltaTime);
        }
    }
}