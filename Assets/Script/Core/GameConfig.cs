using UnityEngine;

/// <summary>
/// 游戏数值配置中心。
/// 
/// 新手阅读顺序：
/// 1. 这个脚本一般挂在场景里的一个配置物体上。
/// 2. 玩家业务 System、SlimeCo、BoxCo 等模块会从 GameConfig.instance 读取统一数值。
/// 3. 想调平衡性时，优先改这里的 Inspector 数值，而不是到处改代码。
/// 4. EnsureConfig 会给空数组和非法数值兜底，防止运行时出现 0 血量、负经验等问题。
/// </summary>
public class GameConfig : MonoBehaviour
{
    // 策划文档里的每级升级所需经验表。
    // 如果 Inspector 里的 Lv_NextExp 没填够，会用这份默认表补上。
    private static readonly int[] DocumentLevelExpTable =
    {
        60, 80, 105, 135, 170,
        210, 255, 305, 360, 420,
        485, 555, 630, 710, 795,
        885, 980, 1080, 1185
    };

    // Kept only as a compatibility table for legacy callers that still ask for level HP.
    // 旧版按等级给玩家最大生命，现在主要保留给老接口 getMaxHp 使用。
    private static readonly int[] LegacyLevelHpTable =
    {
        150, 180, 200, 220, 245,
        270, 295, 320, 350, 380,
        415, 450, 490, 530, 575,
        620, 670, 720, 775, 830
    };

    public static GameConfig instance;

    [Header("Level Progression")]
    [Tooltip("Lv.1 升到 Lv.20 的逐级经验需求。达到等级上限后不再继续升级。")]
    public int[] Lv_NextExp;

    [Tooltip("Legacy HP table kept for backwards-compatible APIs.")]
    public int[] Lv_Hpmax;

    [Tooltip("当前单局等级上限。")]
    public int defaultLevelCap = 20;

    [Header("Player Base Stats")]
    // 玩家初始属性。PlayerModel 初始化时会读取这些值。
    public int playerBaseMaxHp = 300;
    public int playerBaseMaxMp = 180;
    public int playerBaseAttack = 38;
    public float playerBaseMoveSpeed = 5f;
    [Range(0f, 1f)] public float playerBaseCritChance = 0f;
    public float playerCritDamageMultiplier = 1.75f;
    [Range(0f, 1f)] public float playerBaseDodgeChance = 0f;
    public float playerBaseHpRegenPerSecond = 0f;
    [Range(0f, 0.95f)] public float playerBaseDamageReduction = 0.1f;
    [Range(0f, 1f)] public float playerBaseLifeSteal = 0f;
    public float playerRunSpeedMultiplier = 1.6666667f;

    [Header("Player Upgrade Values")]
    // 每次升级选项带来的成长幅度。
    // Percent 表示百分比成长，Flat 表示固定数值成长，Cap 表示上限。
    [Range(0f, 1f)] public float playerAttackUpgradePercent = 0.12f;
    public int playerMaxHpUpgradeFlat = 30;
    [Range(0f, 1f)] public float playerMoveSpeedUpgradePercent = 0.05f;
    [Range(0f, 1f)] public float playerMoveSpeedUpgradeCapPercent = 0.2f;
    [Range(0f, 1f)] public float playerCritChanceUpgrade = 0.05f;
    [Range(0f, 1f)] public float playerCritChanceCap = 0.35f;
    [Range(0f, 1f)] public float playerDodgeChanceUpgrade = 0.04f;
    [Range(0f, 1f)] public float playerDodgeChanceCap = 0.2f;
    public float playerHpRegenUpgrade = 1.5f;
    public float playerHpRegenCap = 6f;
    [Range(0f, 1f)] public float playerDamageReductionUpgrade = 0.04f;
    [Range(0f, 1f)] public float playerDamageReductionCap = 0.4f;
    [Range(0f, 1f)] public float playerLifeStealUpgrade = 0.025f;
    [Range(0f, 1f)] public float playerLifeStealCap = 0.1f;

    [Header("Player Upgrade Pick Limits")]
    // 次数上限与数值上限同时生效：次数控制构筑长度，数值上限负责处理职业初始属性差异。
    public int attackUpgradeMaxCount = 6;
    public int maxHpUpgradeMaxCount = 6;
    public int moveSpeedUpgradeMaxCount = 4;
    public int critUpgradeMaxCount = 7;
    public int dodgeUpgradeMaxCount = 5;
    public int hpRegenUpgradeMaxCount = 4;
    public int damageReductionUpgradeMaxCount = 5;
    public int lifeStealUpgradeMaxCount = 4;

    [Header("Player Upgrade Weights")]
    // 随机升级选项的权重。数值越大，越容易出现在三选一面板里。
    public float attackUpgradeWeight = 1f;
    public float maxHpUpgradeWeight = 1f;
    public float moveSpeedUpgradeWeight = 0.65f;
    public float critUpgradeWeight = 0.8f;
    public float dodgeUpgradeWeight = 0.65f;
    public float hpRegenUpgradeWeight = 0.55f;
    public float damageReductionUpgradeWeight = 0.65f;
    public float lifeStealUpgradeWeight = 0.45f;

    [Header("Player Reward Rules")]
    // 升级和击破金库后给玩家的奖励规则。
    [Range(0f, 1f)] public float levelUpHealPercent = 0.15f;
    public int minimumLevelUpHeal = 20;
    public bool fullHealPlayerOnVaultDestroy = false;
    [Range(0f, 1f)] public float vaultDestroyHealPercent = 0.2f;

    [Header("Monster Growth")]
    // 普通怪使用线性双维度成长：V 是已击破金库数，B 是已击败 Boss 数。
    public float monsterHpGrowthPerVaultDestroy = 0.08f;
    public float monsterHpGrowthPerBossDefeat = 0.2f;
    public float monsterAtkGrowthPerVaultDestroy = 0.035f;
    public float monsterAtkGrowthPerBossDefeat = 0.1f;
    public float monsterExpGrowthPerVaultDestroy = 0.06f;
    public float monsterExpGrowthPerBossDefeat = 0.15f;

    /// <summary>
    /// 注册全局配置实例，并在场景启动时修正配置。
    /// </summary>
    private void Awake()
    {
        // 单例赋值：其他脚本通过 GameConfig.instance 找到本配置。
        instance = this;
        EnsureConfig();
    }

    /// <summary>
    /// Inspector 数值变化时自动修正非法配置。
    /// </summary>
    private void OnValidate()
    {
        // OnValidate 在 Inspector 里改值时触发，能让错误数值立刻被修正。
        EnsureConfig();
    }

    /// <summary>
    /// 获取玩家软等级上限，并保证至少为 1。
    /// </summary>
    public int GetDefaultLevelCap()
    {
        // 所有对外读取方法都再做一次保护，保证拿到的数值可用。
        return Mathf.Max(1, defaultLevelCap);
    }

    /// <summary>
    /// 获取玩家基础最大生命，避免返回 0 或负数。
    /// </summary>
    public int GetPlayerBaseMaxHp()
    {
        return Mathf.Max(1, playerBaseMaxHp);
    }

    /// <summary>
    /// 获取玩家基础最大魔法值，作为职业配置缺失时的兜底。
    /// 技能系统后续消耗蓝量时，会以 PlayerModel 中的运行时魔法值为准。
    /// </summary>
    public int GetPlayerBaseMaxMp()
    {
        return Mathf.Max(1, playerBaseMaxMp);
    }

    /// <summary>
    /// 获取玩家基础攻击力，避免返回 0 或负数。
    /// </summary>
    public int GetPlayerBaseAttack()
    {
        return Mathf.Max(1, playerBaseAttack);
    }

    /// <summary>
    /// 获取玩家基础移动速度，避免速度为 0 导致无法移动。
    /// </summary>
    public float GetPlayerBaseMoveSpeed()
    {
        return Mathf.Max(0.01f, playerBaseMoveSpeed);
    }

    /// <summary>
    /// 获取跑步速度倍率，保证跑步不低于走路速度。
    /// </summary>
    public float GetPlayerRunSpeedMultiplier()
    {
        return Mathf.Max(1f, playerRunSpeedMultiplier);
    }

    /// <summary>
    /// Returns the base weight used by the random three-choice upgrade panel.
    /// </summary>
    public float GetUpgradeBaseWeight(PlayerAttributeType attributeType)
    {
        // switch 根据传入的属性类型返回对应权重。
        // Mathf.Max(0f, ...) 防止负权重把随机逻辑搞坏。
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                return Mathf.Max(0f, attackUpgradeWeight);
            case PlayerAttributeType.MaxHp:
                return Mathf.Max(0f, maxHpUpgradeWeight);
            case PlayerAttributeType.MoveSpeed:
                return Mathf.Max(0f, moveSpeedUpgradeWeight);
            case PlayerAttributeType.CritChance:
                return Mathf.Max(0f, critUpgradeWeight);
            case PlayerAttributeType.DodgeChance:
                return Mathf.Max(0f, dodgeUpgradeWeight);
            case PlayerAttributeType.HealthRegen:
                return Mathf.Max(0f, hpRegenUpgradeWeight);
            case PlayerAttributeType.DamageReduction:
                return Mathf.Max(0f, damageReductionUpgradeWeight);
            case PlayerAttributeType.LifeSteal:
                return Mathf.Max(0f, lifeStealUpgradeWeight);
            default:
                return 0f;
        }
    }

    /// <summary>
    /// 返回单局内某种属性最多可选择的次数。
    /// 统一放在配置中心后，候选生成、存档恢复和 UI 都能使用同一条规则。
    /// </summary>
    public int GetUpgradeMaxCount(PlayerAttributeType attributeType)
    {
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                return Mathf.Max(0, attackUpgradeMaxCount);
            case PlayerAttributeType.MaxHp:
                return Mathf.Max(0, maxHpUpgradeMaxCount);
            case PlayerAttributeType.MoveSpeed:
                return Mathf.Max(0, moveSpeedUpgradeMaxCount);
            case PlayerAttributeType.CritChance:
                return Mathf.Max(0, critUpgradeMaxCount);
            case PlayerAttributeType.DodgeChance:
                return Mathf.Max(0, dodgeUpgradeMaxCount);
            case PlayerAttributeType.HealthRegen:
                return Mathf.Max(0, hpRegenUpgradeMaxCount);
            case PlayerAttributeType.DamageReduction:
                return Mathf.Max(0, damageReductionUpgradeMaxCount);
            case PlayerAttributeType.LifeSteal:
                return Mathf.Max(0, lifeStealUpgradeMaxCount);
            default:
                return 0;
        }
    }

    /// <summary>
    /// Human-readable attribute name used by the runtime UI.
    /// Unicode escapes keep the source ASCII-safe while still displaying Chinese at runtime.
    /// </summary>
    public string GetAttributeDisplayName(PlayerAttributeType attributeType)
    {
        // 这里返回 UI 上显示的中文属性名。
        // 字符串用 \u 写法，是为了让源码在不同编码环境下更稳定。
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                return "\u653b\u51fb\u529b";
            case PlayerAttributeType.MaxHp:
                return "\u6700\u5927\u751f\u547d";
            case PlayerAttributeType.MoveSpeed:
                return "\u79fb\u52a8\u901f\u5ea6";
            case PlayerAttributeType.CritChance:
                return "\u66b4\u51fb\u7387";
            case PlayerAttributeType.DodgeChance:
                return "\u95ea\u907f\u7387";
            case PlayerAttributeType.HealthRegen:
                return "\u751f\u547d\u6062\u590d";
            case PlayerAttributeType.DamageReduction:
                return "\u4f24\u5bb3\u51cf\u514d";
            case PlayerAttributeType.LifeSteal:
                return "\u5438\u8840";
            default:
                return "\u672a\u77e5\u5c5e\u6027";
        }
    }

    /// <summary>
    /// Text that describes the raw effect of one upgrade pick.
    /// Player-specific previews are composed inside PlayerProgressionSystem.
    /// </summary>
    public string GetAttributeUpgradeEffectText(PlayerAttributeType attributeType)
    {
        // 这里描述“升级会增加什么”，比如 +30% 当前攻击力。
        // PlayerProgressionSystem 会再补上“当前值 -> 升级后值”的预览。
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                return $"+{Mathf.RoundToInt(playerAttackUpgradePercent * 100f)}% \u5f53\u524d\u653b\u51fb\u529b";
            case PlayerAttributeType.MaxHp:
                return $"+{playerMaxHpUpgradeFlat} \u6700\u5927\u751f\u547d";
            case PlayerAttributeType.MoveSpeed:
                return $"+{Mathf.RoundToInt(playerMoveSpeedUpgradePercent * 100f)}% \u5f53\u524d\u79fb\u901f";
            case PlayerAttributeType.CritChance:
                return $"+{Mathf.RoundToInt(playerCritChanceUpgrade * 100f)}% \u66b4\u51fb\u7387";
            case PlayerAttributeType.DodgeChance:
                return $"+{Mathf.RoundToInt(playerDodgeChanceUpgrade * 100f)}% \u95ea\u907f\u7387";
            case PlayerAttributeType.HealthRegen:
                return $"+{playerHpRegenUpgrade:0.##}/s \u751f\u547d\u6062\u590d";
            case PlayerAttributeType.DamageReduction:
                return $"+{Mathf.RoundToInt(playerDamageReductionUpgrade * 100f)}% \u4f24\u5bb3\u51cf\u514d";
            case PlayerAttributeType.LifeSteal:
                return $"+{Mathf.RoundToInt(playerLifeStealUpgrade * 100f)}% \u5438\u8840";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 返回升级面板上显示的属性上限说明。
    /// </summary>
    public string GetAttributeUpgradeCapText(PlayerAttributeType attributeType)
    {
        // 同时显示次数或数值上限，让玩家知道本局构筑还剩多少成长空间。
        switch (attributeType)
        {
            case PlayerAttributeType.MoveSpeed:
                return $"\u6700\u591a {moveSpeedUpgradeMaxCount} \u6b21 / \u4e0a\u9650 +{Mathf.RoundToInt(playerMoveSpeedUpgradeCapPercent * 100f)}%";
            case PlayerAttributeType.CritChance:
                return $"\u6700\u591a {critUpgradeMaxCount} \u6b21 / \u4e0a\u9650 {Mathf.RoundToInt(playerCritChanceCap * 100f)}%";
            case PlayerAttributeType.DodgeChance:
                return $"\u6700\u591a {dodgeUpgradeMaxCount} \u6b21 / \u4e0a\u9650 {Mathf.RoundToInt(playerDodgeChanceCap * 100f)}%";
            case PlayerAttributeType.HealthRegen:
                return $"\u6700\u591a {hpRegenUpgradeMaxCount} \u6b21 / \u4e0a\u9650 {playerHpRegenCap:0.##}/s";
            case PlayerAttributeType.DamageReduction:
                return $"\u6700\u591a {damageReductionUpgradeMaxCount} \u6b21 / \u603b\u4e0a\u9650 {Mathf.RoundToInt(playerDamageReductionCap * 100f)}%";
            case PlayerAttributeType.LifeSteal:
                return $"\u6700\u591a {lifeStealUpgradeMaxCount} \u6b21 / \u4e0a\u9650 {Mathf.RoundToInt(playerLifeStealCap * 100f)}%";
            case PlayerAttributeType.AttackPower:
                return $"\u6700\u591a {attackUpgradeMaxCount} \u6b21";
            case PlayerAttributeType.MaxHp:
                return $"\u6700\u591a {maxHpUpgradeMaxCount} \u6b21";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 根据当前等级读取下一次升级所需经验。
    /// </summary>
    public int getNextExp(int level)
    {
        // level 从 1 开始，而数组下标从 0 开始，所以要减 1。
        int levelIndex = Mathf.Max(1, level) - 1;
        if (levelIndex >= Lv_NextExp.Length)
        {
            levelIndex = Lv_NextExp.Length - 1;
        }

        return Lv_NextExp[levelIndex];
    }

    /// <summary>
    /// 角色选择场景没有挂载 GameConfig 时，仍按项目默认经验表显示准确的升级需求。
    /// 主场景存在实例时优先读取 Inspector 配置，避免显示规则与实际成长不一致。
    /// </summary>
    public static int GetNextExpForDisplay(int level)
    {
        if (instance != null && instance.Lv_NextExp != null && instance.Lv_NextExp.Length > 0)
        {
            return instance.getNextExp(level);
        }

        int levelIndex = Mathf.Clamp(Mathf.Max(1, level) - 1, 0, DocumentLevelExpTable.Length - 1);
        return DocumentLevelExpTable[levelIndex];
    }

    /// <summary>
    /// 兼容旧接口：根据等级读取基础生命值。
    /// </summary>
    public int getMaxHp(int level)
    {
        // 旧接口：读取某等级对应的基础生命。
        // 如果超过表格长度，就按最后两级的差值继续线性增长。
        int levelIndex = Mathf.Max(1, level) - 1;
        if (levelIndex < Lv_Hpmax.Length)
        {
            return Lv_Hpmax[levelIndex];
        }

        int lastIndex = Lv_Hpmax.Length - 1;
        int lastHp = Lv_Hpmax[lastIndex];
        int growthPerLevel = Lv_Hpmax.Length > 1
            ? Mathf.Max(1, Lv_Hpmax[lastIndex] - Lv_Hpmax[lastIndex - 1])
            : 25;

        int overflowLevels = levelIndex - lastIndex;
        return lastHp + growthPerLevel * overflowLevels;
    }

    /// <summary>
    /// 根据金库击破次数和 Boss 击败次数计算怪物生命倍率。
    /// </summary>
    public float GetMonsterHpMultiplier(int destroyedVaultCount, int defeatedBossCount)
    {
        return 1f +
               monsterHpGrowthPerVaultDestroy * Mathf.Max(0, destroyedVaultCount) +
               monsterHpGrowthPerBossDefeat * Mathf.Max(0, defeatedBossCount);
    }

    public float GetMonsterHpMultiplier(int destroyedVaultCount)
    {
        return GetMonsterHpMultiplier(destroyedVaultCount, BossRunProgressState.CompletedBossCount);
    }

    /// <summary>
    /// 根据金库击破次数和 Boss 击败次数计算怪物攻击倍率。
    /// </summary>
    public float GetMonsterAtkMultiplier(int destroyedVaultCount, int defeatedBossCount)
    {
        return 1f +
               monsterAtkGrowthPerVaultDestroy * Mathf.Max(0, destroyedVaultCount) +
               monsterAtkGrowthPerBossDefeat * Mathf.Max(0, defeatedBossCount);
    }

    public float GetMonsterAtkMultiplier(int destroyedVaultCount)
    {
        return GetMonsterAtkMultiplier(destroyedVaultCount, BossRunProgressState.CompletedBossCount);
    }

    /// <summary>
    /// 根据金库击破次数和 Boss 击败次数计算怪物经验倍率。
    /// </summary>
    public float GetMonsterExpMultiplier(int destroyedVaultCount, int defeatedBossCount)
    {
        return 1f +
               monsterExpGrowthPerVaultDestroy * Mathf.Max(0, destroyedVaultCount) +
               monsterExpGrowthPerBossDefeat * Mathf.Max(0, defeatedBossCount);
    }

    public float GetMonsterExpMultiplier(int destroyedVaultCount)
    {
        return GetMonsterExpMultiplier(destroyedVaultCount, BossRunProgressState.CompletedBossCount);
    }

    /// <summary>
    /// Fills missing arrays and clamps values so runtime code never receives invalid data.
    /// </summary>
    private void EnsureConfig()
    {
        // 如果经验表为空或太短，就用默认表补齐，避免 getNextExp 访问空数组。
        if (Lv_NextExp == null || Lv_NextExp.Length < DocumentLevelExpTable.Length)
        {
            Lv_NextExp = (int[])DocumentLevelExpTable.Clone();
        }

        if (Lv_Hpmax == null || Lv_Hpmax.Length == 0)
        {
            Lv_Hpmax = (int[])LegacyLevelHpTable.Clone();
        }
        else if (Lv_Hpmax.Length < LegacyLevelHpTable.Length)
        {
            int[] mergedHpTable = (int[])LegacyLevelHpTable.Clone();
            System.Array.Copy(Lv_Hpmax, mergedHpTable, Lv_Hpmax.Length);
            Lv_Hpmax = mergedHpTable;
        }

        // 下面是统一的数值夹取：把容易出错的负数、0、超过范围的值修回合理范围。
        defaultLevelCap = Mathf.Max(1, defaultLevelCap);
        playerBaseMaxHp = Mathf.Max(1, playerBaseMaxHp);
        playerBaseMaxMp = Mathf.Max(1, playerBaseMaxMp);
        playerBaseAttack = Mathf.Max(1, playerBaseAttack);
        playerBaseMoveSpeed = Mathf.Max(0.01f, playerBaseMoveSpeed);
        playerCritDamageMultiplier = Mathf.Max(1f, playerCritDamageMultiplier);
        playerRunSpeedMultiplier = Mathf.Max(1f, playerRunSpeedMultiplier);
        playerMaxHpUpgradeFlat = Mathf.Max(1, playerMaxHpUpgradeFlat);
        playerHpRegenUpgrade = Mathf.Max(0f, playerHpRegenUpgrade);
        playerHpRegenCap = Mathf.Max(0f, playerHpRegenCap);
        attackUpgradeMaxCount = Mathf.Max(0, attackUpgradeMaxCount);
        maxHpUpgradeMaxCount = Mathf.Max(0, maxHpUpgradeMaxCount);
        moveSpeedUpgradeMaxCount = Mathf.Max(0, moveSpeedUpgradeMaxCount);
        critUpgradeMaxCount = Mathf.Max(0, critUpgradeMaxCount);
        dodgeUpgradeMaxCount = Mathf.Max(0, dodgeUpgradeMaxCount);
        hpRegenUpgradeMaxCount = Mathf.Max(0, hpRegenUpgradeMaxCount);
        damageReductionUpgradeMaxCount = Mathf.Max(0, damageReductionUpgradeMaxCount);
        lifeStealUpgradeMaxCount = Mathf.Max(0, lifeStealUpgradeMaxCount);
        levelUpHealPercent = Mathf.Clamp01(levelUpHealPercent);
        minimumLevelUpHeal = Mathf.Max(0, minimumLevelUpHeal);
        vaultDestroyHealPercent = Mathf.Clamp01(vaultDestroyHealPercent);
        monsterHpGrowthPerVaultDestroy = Mathf.Max(0f, monsterHpGrowthPerVaultDestroy);
        monsterHpGrowthPerBossDefeat = Mathf.Max(0f, monsterHpGrowthPerBossDefeat);
        monsterAtkGrowthPerVaultDestroy = Mathf.Max(0f, monsterAtkGrowthPerVaultDestroy);
        monsterAtkGrowthPerBossDefeat = Mathf.Max(0f, monsterAtkGrowthPerBossDefeat);
        monsterExpGrowthPerVaultDestroy = Mathf.Max(0f, monsterExpGrowthPerVaultDestroy);
        monsterExpGrowthPerBossDefeat = Mathf.Max(0f, monsterExpGrowthPerBossDefeat);
    }
}
