using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家成长系统：负责经验、等级、属性升级和升级候选生成。
/// 规则从 MonoBehaviour 中移到 System 后，UI、动画和四个职业都只消费结果。
/// </summary>
public sealed class PlayerProgressionSystem : AbstractSystem
{
    private const float MinUpgradeableThreshold = 0.0001f;
    private readonly List<PlayerAttributeType> choiceBuffer = new List<PlayerAttributeType>(8);
    private PlayerModel model;

    private PlayerRuntimeStats Stats => model.MutableStats;

    protected override void OnInit()
    {
        model = this.GetModel<PlayerModel>();
    }

    /// <summary>
    /// 初始化玩家成长数据，并把首帧需要刷新的事件一次性发出去。
    /// </summary>
    public void InitializePlayer(NCharacter save, CharacterDefine define)
    {
        model.Reset(save, define);
        this.GetSystem<PlayerCombatSystem>().ResetRuntimeBuffers();
        // 新开一局或切换角色时，重置玩家技能运行时数据。
        // 这样可以避免上一个角色学过的技能、冷却、待选择次数残留到新角色身上。
        this.GetSystem<PlayerSkillSystem>().ResetRuntimeSkills();
        this.SendEvent(new PlayerStatsChangedEvent());
        this.SendEvent(new PlayerUpgradeQueueChangedEvent(0));
    }

    /// <summary>
    /// 增加经验并尝试升级。
    /// 外部只关心“经验加了多少”，升级判断和升级奖励都集中在系统内部完成。
    /// </summary>
    public void AddExp(int exp)
    {
        AddExpInternal(exp, true);
    }

    /// <summary>
    /// 开发者专用快速升级入口。
    /// 通过经验换算复用正式升级流程，但不生成逐级属性选择，避免一次测试要连续选择十五次。
    /// 技能选择仍按 5 级一次的正式规则发放，所以正常增加 15 级时会得到 3 次技能选择。
    /// </summary>
    public int AddLevelsForDevelopment(int levelCount)
    {
        if (levelCount <= 0 || Stats.Level >= Stats.LevelCap)
        {
            return 0;
        }

        int previousLevel = Stats.Level;
        int targetLevel = Mathf.Min(Stats.LevelCap, Stats.Level + levelCount);
        long requiredExp = -Stats.CurrentExp;

        for (int level = Stats.Level; level < targetLevel; level++)
        {
            requiredExp += model.GetNextExpForLevel(level);
        }

        int expToAdd = requiredExp >= int.MaxValue
            ? int.MaxValue
            : Mathf.Max(1, (int)requiredExp);
        AddExpInternal(expToAdd, false);
        return Stats.Level - previousLevel;
    }

    /// <summary>
    /// 统一增加经验，并决定本次升级是否生成属性选择。
    /// 正常玩法传 true，开发者快速升级传 false，其他升级规则仍然完全共用。
    /// </summary>
    private void AddExpInternal(int exp, bool grantAttributeSelections)
    {
        if (exp <= 0)
        {
            return;
        }

        Stats.CurrentExp += exp;
        this.SendEvent(new PlayerExperienceGainedEvent(exp));
        DoLevelUp(grantAttributeSelections);
        this.SendEvent(new PlayerStatsChangedEvent());
    }

    /// <summary>
    /// 控制升级三选一面板是否激活。
    /// 运行时控制器会根据这个标记暂停玩家正常操作。
    /// </summary>
    public void SetUpgradeSelectionState(bool active)
    {
        Stats.IsUpgradeSelectionActive = active;
        this.SendEvent(new PlayerStatsChangedEvent());
    }

    /// <summary>
    /// 处理一次玩家升级选择。
    /// 成功时既会应用属性，也会减少待选择次数。
    /// </summary>
    public bool ResolvePendingUpgradeSelection(PlayerAttributeType attributeType)
    {
        if (Stats.PendingUpgradeSelectionCount <= 0 || !TryApplyAttributeUpgrade(attributeType))
        {
            return false;
        }

        Stats.PendingUpgradeSelectionCount = Mathf.Max(0, Stats.PendingUpgradeSelectionCount - 1);
        this.SendEvent(new PlayerUpgradeQueueChangedEvent(Stats.PendingUpgradeSelectionCount));
        return true;
    }

    /// <summary>
    /// 判断某个属性当前还能不能继续升级。
    /// 这里统一处理各种上限规则，避免 UI 和逻辑各写一份判断。
    /// </summary>
    public bool CanApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
            case PlayerAttributeType.MaxHp:
                return true;
            case PlayerAttributeType.HealthRegen:
                return GetNextHealthRegenUpgradeAmount() > MinUpgradeableThreshold;
            case PlayerAttributeType.MoveSpeed:
                float moveCap = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                return GetMoveSpeedBonusPercent() + MinUpgradeableThreshold < moveCap;
            case PlayerAttributeType.CritChance:
                return Stats.CritChance + MinUpgradeableThreshold < (config != null ? config.playerCritChanceCap : 0.8f);
            case PlayerAttributeType.DodgeChance:
                return Stats.DodgeChance + MinUpgradeableThreshold < (config != null ? config.playerDodgeChanceCap : 0.5f);
            case PlayerAttributeType.DamageReduction:
                return Stats.DamageReduction + MinUpgradeableThreshold < (config != null ? config.playerDamageReductionCap : 0.7f);
            case PlayerAttributeType.LifeSteal:
                return Stats.LifeSteal + MinUpgradeableThreshold < (config != null ? config.playerLifeStealCap : 0.5f);
            default:
                return false;
        }
    }

    /// <summary>
    /// 真正应用一次属性升级。
    /// 不同属性的增长方式不同，但都在这里集中维护，方便后续平衡数值。
    /// </summary>
    public bool TryApplyAttributeUpgrade(PlayerAttributeType type)
    {
        if (!CanApplyAttributeUpgrade(type))
        {
            return false;
        }

        GameConfig config = GameConfig.instance;
        switch (type)
        {
            case PlayerAttributeType.AttackPower:
                float attackPercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                Stats.AttackPower = Mathf.Max(1, Mathf.CeilToInt(Stats.AttackPower * (1f + attackPercent)));
                break;
            case PlayerAttributeType.MaxHp:
                int hpBonus = config != null ? config.playerMaxHpUpgradeFlat : 50;
                Stats.BonusMaxHp += hpBonus;
                model.RecalculateMaxHp(false);
                this.GetSystem<PlayerCombatSystem>().Heal(hpBonus, false);
                break;
            case PlayerAttributeType.MoveSpeed:
                float speedPercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float speedCap = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                Stats.CurrentMoveSpeed = Mathf.Min(
                    Stats.BaseMoveSpeed * (1f + speedCap),
                    Stats.CurrentMoveSpeed * (1f + speedPercent));
                break;
            case PlayerAttributeType.CritChance:
                Stats.CritChance = Mathf.Min(
                    config != null ? config.playerCritChanceCap : 0.8f,
                    Stats.CritChance + (config != null ? config.playerCritChanceUpgrade : 0.1f));
                break;
            case PlayerAttributeType.DodgeChance:
                Stats.DodgeChance = Mathf.Min(
                    config != null ? config.playerDodgeChanceCap : 0.5f,
                    Stats.DodgeChance + (config != null ? config.playerDodgeChanceUpgrade : 0.1f));
                break;
            case PlayerAttributeType.HealthRegen:
                Stats.HealthRegenPerSecond = Mathf.Min(
                    GetHealthRegenCap(),
                    Stats.HealthRegenPerSecond + GetNextHealthRegenUpgradeAmount());
                Stats.HealthRegenUpgradeCount++;
                break;
            case PlayerAttributeType.DamageReduction:
                Stats.DamageReduction = Mathf.Min(
                    config != null ? config.playerDamageReductionCap : 0.7f,
                    Stats.DamageReduction + (config != null ? config.playerDamageReductionUpgrade : 0.1f));
                break;
            case PlayerAttributeType.LifeSteal:
                Stats.LifeSteal = Mathf.Min(
                    config != null ? config.playerLifeStealCap : 0.5f,
                    Stats.LifeSteal + (config != null ? config.playerLifeStealUpgrade : 0.05f));
                break;
            default:
                return false;
        }

        this.SendEvent(new PlayerStatsChangedEvent());
        return true;
    }

    /// <summary>
    /// 生成本次升级给玩家展示的候选项列表。
    /// 当前采用带权随机且不重复抽取，既能保持随机性，也能控制不同属性的出现频率。
    /// </summary>
    public List<PlayerAttributeType> GetRandomUpgradeChoices(int choiceCount)
    {
        choiceBuffer.Clear();
        List<PlayerAttributeType> available = new List<PlayerAttributeType>
        {
            PlayerAttributeType.AttackPower,
            PlayerAttributeType.MaxHp,
            PlayerAttributeType.MoveSpeed,
            PlayerAttributeType.CritChance,
            PlayerAttributeType.DodgeChance,
            PlayerAttributeType.HealthRegen,
            PlayerAttributeType.DamageReduction,
            PlayerAttributeType.LifeSteal
        };

        available.RemoveAll(type => !CanApplyAttributeUpgrade(type));
        int resultCount = Mathf.Min(Mathf.Max(0, choiceCount), available.Count);
        for (int i = 0; i < resultCount; i++)
        {
            float totalWeight = 0f;
            for (int j = 0; j < available.Count; j++)
            {
                totalWeight += GetUpgradeWeight(available[j]);
            }

            if (totalWeight <= 0f)
            {
                break;
            }

            float roll = Random.Range(0f, totalWeight);
            float accumulated = 0f;
            int selectedIndex = 0;
            for (int j = 0; j < available.Count; j++)
            {
                accumulated += GetUpgradeWeight(available[j]);
                if (roll <= accumulated)
                {
                    selectedIndex = j;
                    break;
                }
            }

            choiceBuffer.Add(available[selectedIndex]);
            available.RemoveAt(selectedIndex);
        }

        return new List<PlayerAttributeType>(choiceBuffer);
    }

    /// <summary>
    /// 把某个升级项转换成 UI 可以直接展示的说明文本。
    /// </summary>
    public string GetUpgradeOptionText(PlayerAttributeType type)
    {
        GameConfig config = GameConfig.instance;
        string title = config != null ? config.GetAttributeDisplayName(type) : type.ToString();
        string capText = config != null ? config.GetAttributeUpgradeCapText(type) : string.Empty;
        return $"{title}\n{GetUpgradeEffectText(type)}\n{GetUpgradePreviewValueText(type)}\n{capText}";
    }

    /// <summary>
    /// 循环处理升级，直到当前经验不足为止。
    /// 使用 while 而不是 if，是为了支持一次获得大量经验后连续升多级。
    /// </summary>
    private void DoLevelUp(bool grantAttributeSelections)
    {
        while (Stats.Level < Stats.LevelCap && Stats.CurrentExp >= model.GetNextExpForLevel(Stats.Level))
        {
            Stats.CurrentExp -= model.GetNextExpForLevel(Stats.Level);
            Stats.Level++;
            // 每升到 5 的倍数等级，额外给玩家一次技能学习/升级选择机会。
            // 注意：属性升级和技能升级分开处理，避免两个成长系统强耦合在同一个 UI 规则里。
            model.RecalculateMaxHp(false);
            int minimumHeal = GameConfig.instance != null ? GameConfig.instance.minimumLevelUpHeal : 30;
            float healPercent = GameConfig.instance != null ? GameConfig.instance.levelUpHealPercent : 0.3f;
            this.GetSystem<PlayerCombatSystem>().Heal(
                Mathf.Max(minimumHeal, Mathf.CeilToInt(Stats.MaxHp * healPercent)),
                false);
            // 升级奖励统一通过资源系统回满蓝，确保蓝量事件和 HUD 刷新仍走正式流程。
            this.GetSystem<PlayerResourceSystem>().FullRestoreMana();

            if (grantAttributeSelections)
            {
                Stats.PendingUpgradeSelectionCount++;
                this.SendEvent(new PlayerUpgradeQueueChangedEvent(Stats.PendingUpgradeSelectionCount));
            }

            // 每升到 5 的倍数等级，额外给玩家一次技能学习/升级选择机会。
            // 放在属性选择事件之后触发，让共用面板优先显示属性，再显示技能。
            TryAddSkillSelectionOnLevelUp(Stats.Level);
        }

        if (Stats.Level >= Stats.LevelCap)
        {
            Stats.CurrentExp = Mathf.Min(Stats.CurrentExp, model.GetNextExpForLevel(Stats.Level));
        }

        Stats.ExpToNextLevel = model.GetNextExpForLevel(Stats.Level);
    }

    /// <summary>
    /// 升级时检查是否需要发放技能选择机会。
    /// 第一版规则：玩家每到 5 的倍数等级，可以学习一个新技能，或者升级一个已有技能。
    /// 
    /// 例如：
    /// Lv.5  第一次学习技能。
    /// Lv.10 可以升级已有技能，或者学习还没学过的技能。
    /// Lv.15、Lv.20 继续按同样规则发放。
    /// </summary>
    private void TryAddSkillSelectionOnLevelUp(int newLevel)
    {
        if (newLevel <= 0)
        {
            return;
        }

        // 只有 5、10、15、20... 这些等级触发技能三选一。
        if (newLevel % 5 != 0)
        {
            return;
        }

        this.GetSystem<PlayerSkillSystem>().AddPendingSkillSelection();
    }

    private string GetUpgradeEffectText(PlayerAttributeType type)
    {
        if (type == PlayerAttributeType.HealthRegen)
        {
            return $"+{GetNextHealthRegenUpgradeAmount():0.##}/s 生命恢复";
        }

        return GameConfig.instance != null ? GameConfig.instance.GetAttributeUpgradeEffectText(type) : string.Empty;
    }

    private string GetUpgradePreviewValueText(PlayerAttributeType type)
    {
        GameConfig config = GameConfig.instance;
        switch (type)
        {
            case PlayerAttributeType.AttackPower:
                float attackPercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                return $"当前 {Stats.AttackPower} -> {Mathf.Max(1, Mathf.CeilToInt(Stats.AttackPower * (1f + attackPercent)))}";
            case PlayerAttributeType.MaxHp:
                return $"当前 {Stats.MaxHp} -> {Stats.MaxHp + (config != null ? config.playerMaxHpUpgradeFlat : 50)}";
            case PlayerAttributeType.MoveSpeed:
                float speedPercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float speedCap = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                float next = Mathf.Min(Stats.BaseMoveSpeed * (1f + speedCap), Stats.CurrentMoveSpeed * (1f + speedPercent));
                return $"当前 {Stats.CurrentMoveSpeed:0.00} -> {next:0.00}";
            case PlayerAttributeType.CritChance:
                return PreviewPercent(Stats.CritChance, config != null ? config.playerCritChanceUpgrade : 0.1f, config != null ? config.playerCritChanceCap : 0.8f);
            case PlayerAttributeType.DodgeChance:
                return PreviewPercent(Stats.DodgeChance, config != null ? config.playerDodgeChanceUpgrade : 0.1f, config != null ? config.playerDodgeChanceCap : 0.5f);
            case PlayerAttributeType.HealthRegen:
                return $"当前 {Stats.HealthRegenPerSecond:0.##}/s -> {Mathf.Min(GetHealthRegenCap(), Stats.HealthRegenPerSecond + GetNextHealthRegenUpgradeAmount()):0.##}/s";
            case PlayerAttributeType.DamageReduction:
                return PreviewPercent(Stats.DamageReduction, config != null ? config.playerDamageReductionUpgrade : 0.1f, config != null ? config.playerDamageReductionCap : 0.7f);
            case PlayerAttributeType.LifeSteal:
                return PreviewPercent(Stats.LifeSteal, config != null ? config.playerLifeStealUpgrade : 0.05f, config != null ? config.playerLifeStealCap : 0.5f);
            default:
                return string.Empty;
        }
    }

    private string PreviewPercent(float current, float addition, float cap)
    {
        return $"当前 {Mathf.RoundToInt(current * 100f)}% -> {Mathf.RoundToInt(Mathf.Min(cap, current + addition) * 100f)}%";
    }

    private float GetUpgradeWeight(PlayerAttributeType type)
    {
        return Mathf.Max(0f, GameConfig.instance != null ? GameConfig.instance.GetUpgradeBaseWeight(type) : 1f);
    }

    private float GetNextHealthRegenUpgradeAmount()
    {
        float baseUpgrade = GameConfig.instance != null ? GameConfig.instance.playerHpRegenUpgrade : 1f;
        return Mathf.Max(
            0f,
            Mathf.Min(
                baseUpgrade * Mathf.Pow(2f, Mathf.Max(0, Stats.HealthRegenUpgradeCount)),
                GetHealthRegenCap() - Stats.HealthRegenPerSecond));
    }

    private float GetHealthRegenCap()
    {
        return GameConfig.instance != null ? Mathf.Max(0f, GameConfig.instance.playerHpRegenCap) : 32f;
    }

    private float GetMoveSpeedBonusPercent()
    {
        return Stats.BaseMoveSpeed > 0f
            ? Mathf.Max(0f, Stats.CurrentMoveSpeed / Stats.BaseMoveSpeed - 1f)
            : 0f;
    }
}
