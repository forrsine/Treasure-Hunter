using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家技能系统：负责技能学习、升级、三选一候选生成。
/// 注意：这里仍然不负责播放特效、不负责读输入、不负责 UI。
/// </summary>
public sealed class PlayerSkillSystem : AbstractSystem
{
    private PlayerModel playerModel;
    private PlayerSkillModel skillModel;
    private DeveloperModeModel developerModeModel;

    protected override void OnInit()
    {
        playerModel = this.GetModel<PlayerModel>();
        skillModel = this.GetModel<PlayerSkillModel>();
        developerModeModel = this.GetModel<DeveloperModeModel>();
    }

    /// <summary>
    /// 重置玩家技能运行时数据。
    /// 后续切换角色或重新开始游戏时会调用，避免上一个角色的技能残留。
    /// </summary>
    public void ResetRuntimeSkills()
    {
        skillModel.Reset();
        this.SendEvent(new PlayerSkillSelectionQueueChangedEvent(skillModel.PendingSkillSelectionCount));
    }

    /// <summary>
    /// 增加一次技能选择机会。
    /// 后续玩家每升到 5 的倍数等级时，会走这里。
    /// </summary>
    public void AddPendingSkillSelection()
    {
        if (!HasAvailableSkillChoice())
        {
            // 技能全部满级或当前没有合法技能时，不再累计一个永远无法处理的选择次数。
            // 同时清理旧版本可能已经留下的无效次数，避免队列永久残留。
            bool clearedInvalidSelection = ClearPendingSkillSelections();

            if (clearedInvalidSelection)
            {
                this.SendEvent(new PlayerSkillSelectionQueueChangedEvent(0));
            }

            Debug.Log("当前没有可学习或可升级的技能，本次不增加技能选择次数。");
            return;
        }

        skillModel.AddPendingSkillSelection();
        this.SendEvent(new PlayerSkillSelectionQueueChangedEvent(skillModel.PendingSkillSelectionCount));
    }

    /// <summary>
    /// 判断当前角色是否至少存在一个可学习或可升级的技能。
    /// 这个检查用于阻止“候选已经耗尽，但待选择次数仍不断累积”的死队列。
    /// </summary>
    private bool HasAvailableSkillChoice()
    {
        if (SkillDataManager.Instance == null)
        {
            Debug.LogError("检查技能候选失败：场景中没有 SkillDataManager。");
            return false;
        }

        List<SkillDefine> allSkills = SkillDataManager.Instance.GetAllSkills();
        for (int i = 0; i < allSkills.Count; i++)
        {
            SkillDefine skill = allSkills[i];
            if (skill == null)
            {
                continue;
            }

            int currentLevel = skillModel.GetSkillLevel(skill.skillId);
            if (currentLevel <= 0)
            {
                if (CanLearnSkill(skill.skillId))
                {
                    return true;
                }
            }
            else if (CanUpgradeSkill(skill.skillId))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清空已经无法处理的技能选择次数。
    /// 使用 Model 的正式消费入口，而不是直接修改计数，继续保持数据写入边界。
    /// </summary>
    private bool ClearPendingSkillSelections()
    {
        bool clearedAny = false;
        while (skillModel.ConsumePendingSkillSelection())
        {
            clearedAny = true;
        }

        return clearedAny;
    }

    /// <summary>
    /// 判断当前角色能不能学习某个技能。
    /// 这里统一检查：配置是否存在、是否已经学过、等级是否足够、职业是否允许。
    /// </summary>
    public bool CanLearnSkill(int skillId)
    {
        SkillDefine skill = GetSkill(skillId);
        if (skill == null)
        {
            return false;
        }

        if (skillModel.HasSkill(skillId))
        {
            return false;
        }

        if (GetCurrentPlayerLevel() < skill.unlockLevel)
        {
            return false;
        }

        return skill.CanLearnByClass(GetCurrentClassId());
    }

    /// <summary>
    /// 判断当前角色能不能升级某个技能。
    /// 技能必须已经学会，并且当前等级不能超过配置表里的最大等级。
    /// </summary>
    public bool CanUpgradeSkill(int skillId)
    {
        SkillDefine skill = GetSkill(skillId);
        if (skill == null)
        {
            return false;
        }

        return skillModel.CanUpgradeSkill(skillId);
    }

    /// <summary>
    /// 学习一个新技能。
    /// 只用于正式规则入口，不建议外部直接改 PlayerSkillModel。
    /// </summary>
    public bool TryLearnSkill(int skillId)
    {
        if (!CanLearnSkill(skillId))
        {
            return false;
        }

        bool success = skillModel.LearnSkill(skillId);
        if (success)
        {
            this.SendEvent(new PlayerSkillChangedEvent(skillId, skillModel.GetSkillLevel(skillId)));
        }

        return success;
    }

    /// <summary>
    /// 升级一个已学习技能。
    /// </summary>
    public bool TryUpgradeSkill(int skillId)
    {
        if (!CanUpgradeSkill(skillId))
        {
            return false;
        }

        bool success = skillModel.UpgradeSkill(skillId);
        if (success)
        {
            this.SendEvent(new PlayerSkillChangedEvent(skillId, skillModel.GetSkillLevel(skillId)));
        }

        return success;
    }

    /// <summary>
    /// 处理玩家在技能三选一面板里的选择。
    /// 成功学习或升级后，才会消耗一次待选择次数。
    /// </summary>
    public bool ResolvePendingSkillChoice(PlayerSkillChoice choice)
    {
        if (choice == null || skillModel.PendingSkillSelectionCount <= 0)
        {
            return false;
        }

        bool success = choice.choiceType == PlayerSkillChoiceType.Learn
            ? TryLearnSkill(choice.skillId)
            : TryUpgradeSkill(choice.skillId);

        if (!success)
        {
            return false;
        }

        skillModel.ConsumePendingSkillSelection();
        if (!HasAvailableSkillChoice())
        {
            // 快速升级可能一次积累多次选择。最后一个可升级技能满级后，
            // 把剩余但已经没有候选项的次数一起清掉，避免面板下一轮空转。
            ClearPendingSkillSelections();
        }

        this.SendEvent(new PlayerSkillSelectionQueueChangedEvent(skillModel.PendingSkillSelectionCount));
        return true;
    }

    /// <summary>
    /// 生成本次技能三选一候选。
    /// 未学习的技能生成“学习”选项；已学习但未满级的技能生成“升级”选项。
    /// </summary>
    public List<PlayerSkillChoice> GetRandomSkillChoices(int choiceCount)
    {
        List<PlayerSkillChoice> candidates = new List<PlayerSkillChoice>();
        List<PlayerSkillChoice> result = new List<PlayerSkillChoice>();

        if (SkillDataManager.Instance == null)
        {
            Debug.LogError("生成技能选项失败：场景中没有 SkillDataManager。");
            return result;
        }

        List<SkillDefine> allSkills = SkillDataManager.Instance.GetAllSkills();

        for (int i = 0; i < allSkills.Count; i++)
        {
            SkillDefine skill = allSkills[i];
            if (skill == null)
            {
                continue;
            }

            int currentLevel = skillModel.GetSkillLevel(skill.skillId);

            if (currentLevel <= 0)
            {
                if (CanLearnSkill(skill.skillId))
                {
                    candidates.Add(new PlayerSkillChoice(skill.skillId, PlayerSkillChoiceType.Learn, 0, 1));
                }
            }
            else if (CanUpgradeSkill(skill.skillId))
            {
                candidates.Add(new PlayerSkillChoice(skill.skillId, PlayerSkillChoiceType.Upgrade, currentLevel, currentLevel + 1));
            }
        }

        int finalCount = Mathf.Min(Mathf.Max(0, choiceCount), candidates.Count);
        for (int i = 0; i < finalCount; i++)
        {
            int index = Random.Range(0, candidates.Count);
            result.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return result;
    }

    /// <summary>
    /// 把一个技能候选项转换成 UI 可显示文本。
    /// 后续技能选择面板可以直接调用它。
    /// </summary>
    public string GetSkillChoiceText(PlayerSkillChoice choice)
    {
        if (choice == null)
        {
            return string.Empty;
        }

        SkillDefine skill = GetSkill(choice.skillId);
        if (skill == null)
        {
            return string.Empty;
        }

        SkillLevelDefine nextLevelData = skill.GetLevelData(choice.nextLevel);
        if (nextLevelData == null)
        {
            return skill.name;
        }

        if (choice.choiceType == PlayerSkillChoiceType.Learn)
        {
            return $"学习 {skill.name}\n{BuildLevelText(skill, nextLevelData)}";
        }

        return $"升级 {skill.name}\nLv.{choice.currentLevel} -> Lv.{choice.nextLevel}\n{BuildLevelText(skill, nextLevelData)}";
    }

    private SkillDefine GetSkill(int skillId)
    {
        if (SkillDataManager.Instance == null)
        {
            Debug.LogError("没有找到 SkillDataManager，无法读取技能配置。");
            return null;
        }

        return SkillDataManager.Instance.GetSkill(skillId);
    }

    private int GetCurrentPlayerLevel()
    {
        return playerModel != null && playerModel.Stats != null
            ? Mathf.Max(1, playerModel.Stats.Level)
            : 1;
    }

    private int GetCurrentClassId()
    {
        if (playerModel == null)
        {
            return 0;
        }

        if (playerModel.CharacterSave != null)
        {
            return playerModel.CharacterSave.classId;
        }

        return playerModel.CharacterDefine != null ? playerModel.CharacterDefine.classId : 0;
    }

    private string BuildLevelText(SkillDefine skill, SkillLevelDefine levelData)
    {
        if (levelData == null)
        {
            return "缺少等级配置";
        }

        // 按钮宽度有限，主动把技能数值拆成短行，避免 Unity Text 自动把单个数字挤到下一行。
        string text =
            $"蓝耗{levelData.mpCost}  冷却{levelData.cooldown:0.#}s\n" +
            $"伤害{levelData.damageRate:0.##}x  范围{levelData.radius:0.#}";

        if (skill != null && skill.GetSkillType() == SkillType.AreaDot)
        {
            text += $"\n持续{levelData.duration:0.#}s  减速{Mathf.RoundToInt(levelData.slowRate * 100f)}%";
        }

        return text;
    }

    /// <summary>
    /// 尝试释放一个已经学习的技能。
    /// 这一层只处理规则：是否学会、冷却、蓝耗。
    /// 真正的火球、毒雾、旋转伤害，交给 PlayerSkillCastComponent 执行。
    /// </summary>
    public bool TryCastSkill(int skillId)
    {
        SkillDefine skill = GetSkill(skillId);
        if (skill == null)
        {
            NotifyCastFailed(skillId, "技能配置不存在");
            return false;
        }

        if (!skill.TryGetSkillType(out _))
        {
            NotifyCastFailed(skillId, "技能类型配置错误");
            Debug.LogError($"释放技能失败：skillType 非法，skillId = {skillId}, skillType = {skill.skillType}");
            return false;
        }

        PlayerSkillRuntimeData runtimeData = skillModel.GetSkillRuntimeData(skillId);
        if (runtimeData == null)
        {
            NotifyCastFailed(skillId, $"还没有学习{skill.name}");
            return false;
        }

        bool zeroCooldownEnabled = developerModeModel != null && developerModeModel.ZeroCooldownEnabled;
        if (!zeroCooldownEnabled && runtimeData.IsCoolingDown())
        {
            NotifyCastFailed(skillId, $"技能冷却中：{runtimeData.cooldownRemaining:0.0} 秒");
            return false;
        }

        SkillLevelDefine levelData = skill.GetLevelData(runtimeData.level);
        if (levelData == null)
        {
            NotifyCastFailed(skillId, "技能等级配置不存在");
            Debug.LogError($"释放技能失败：找不到技能等级配置 skillId = {skillId}, level = {runtimeData.level}");
            return false;
        }

        // 扣蓝必须走 PlayerResourceSystem，不能直接改 PlayerModel。
        // 这样后续回蓝、扣蓝、UI 刷新都可以统一维护。
        bool spentMana = this.GetSystem<PlayerResourceSystem>().TrySpendMana(levelData.mpCost);
        if (!spentMana)
        {
            NotifyCastFailed(skillId, $"蓝量不足，需要 {levelData.mpCost} MP");
            return false;
        }

        if (!zeroCooldownEnabled)
        {
            runtimeData.StartCooldown(levelData.cooldown);
        }
        Debug.Log($"释放技能成功：{skill.name} Lv.{runtimeData.level}，消耗 MP {levelData.mpCost}");

        return true;
    }

    /// <summary>
    /// 统一发送技能释放失败事件。
    /// 这里用事件通知 UI，而不是让技能系统直接引用 UI，目的是降低耦合。
    /// </summary>
    private void NotifyCastFailed(int skillId, string message)
    {
        Debug.Log($"释放技能失败：{message}");
        this.SendEvent(new PlayerSkillCastFailedEvent(skillId, message));
    }

    /// <summary>
    /// 每帧推进技能冷却。
    /// 注意：Time.deltaTime 从外部传入，方便以后做暂停、加速、服务器同步等扩展。
    /// </summary>
    public void TickSkillCooldowns(float deltaTime)
    {
        if (developerModeModel != null && developerModeModel.ZeroCooldownEnabled)
        {
            return;
        }

        skillModel.TickCooldowns(deltaTime);
    }

    /// <summary>
    /// 开启开发者零冷却时立即清掉已经存在的 CD。
    /// 写操作仍由技能 System 完成，DeveloperModeSystem 不直接改技能 Model。
    /// </summary>
    public void ClearAllCooldownsForDevelopment()
    {
        skillModel.ClearAllCooldowns();
    }

}
