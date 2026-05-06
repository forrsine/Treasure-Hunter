using UnityEngine;

/// <summary>
/// 游戏数值配置中心。
/// 
/// 新手阅读顺序：
/// 1. 这个脚本一般挂在场景里的一个配置物体上。
/// 2. PlayerCo、SlimeCo、BoxCo 等脚本会从 GameConfig.instance 读取统一数值。
/// 3. 想调平衡性时，优先改这里的 Inspector 数值，而不是到处改代码。
/// 4. EnsureConfig 会给空数组和非法数值兜底，防止运行时出现 0 血量、负经验等问题。
/// </summary>
public class GameConfig : MonoBehaviour
{
    // 策划文档里的每级升级所需经验表。
    // 如果 Inspector 里的 Lv_NextExp 没填够，会用这份默认表补上。
    private static readonly int[] DocumentLevelExpTable =
    {
        50, 60, 75, 95, 110,
        125, 140, 155, 170, 185,
        200, 215, 230, 245, 260,
        275, 290, 305, 330, 320
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
    [Tooltip("Experience required per level. Levels above 20 reuse the final value 320.")]
    public int[] Lv_NextExp;

    [Tooltip("Legacy HP table kept for backwards-compatible APIs.")]
    public int[] Lv_Hpmax;

    [Tooltip("Soft level cap used by the current prototype.")]
    public int defaultLevelCap = 999;

    [Header("Player Base Stats")]
    // 玩家初始属性。PlayerCo.Start 时会读取这些值初始化自己。
    public int playerBaseMaxHp = 150;
    public int playerBaseAttack = 25;
    public float playerBaseMoveSpeed = 3f;
    [Range(0f, 1f)] public float playerBaseCritChance = 0f;
    public float playerCritDamageMultiplier = 1.5f;
    [Range(0f, 1f)] public float playerBaseDodgeChance = 0f;
    public float playerBaseHpRegenPerSecond = 0f;
    [Range(0f, 0.95f)] public float playerBaseDamageReduction = 0f;
    [Range(0f, 1f)] public float playerBaseLifeSteal = 0f;
    public float playerRunSpeedMultiplier = 1.6666667f;

    [Header("Player Upgrade Values")]
    // 每次升级选项带来的成长幅度。
    // Percent 表示百分比成长，Flat 表示固定数值成长，Cap 表示上限。
    [Range(0f, 1f)] public float playerAttackUpgradePercent = 0.3f;
    public int playerMaxHpUpgradeFlat = 50;
    [Range(0f, 1f)] public float playerMoveSpeedUpgradePercent = 0.15f;
    [Range(0f, 1f)] public float playerMoveSpeedUpgradeCapPercent = 0.6f;
    [Range(0f, 1f)] public float playerCritChanceUpgrade = 0.1f;
    [Range(0f, 1f)] public float playerCritChanceCap = 0.8f;
    [Range(0f, 1f)] public float playerDodgeChanceUpgrade = 0.1f;
    [Range(0f, 1f)] public float playerDodgeChanceCap = 0.5f;
    public float playerHpRegenUpgrade = 1f;
    public float playerHpRegenCap = 32f;
    [Range(0f, 1f)] public float playerDamageReductionUpgrade = 0.1f;
    [Range(0f, 1f)] public float playerDamageReductionCap = 0.7f;
    [Range(0f, 1f)] public float playerLifeStealUpgrade = 0.05f;
    [Range(0f, 1f)] public float playerLifeStealCap = 0.5f;

    [Header("Player Upgrade Weights")]
    // 随机升级选项的权重。数值越大，越容易出现在三选一面板里。
    public float attackUpgradeWeight = 1f;
    public float maxHpUpgradeWeight = 1f;
    public float moveSpeedUpgradeWeight = 0.8f;
    public float critUpgradeWeight = 0.7f;
    public float dodgeUpgradeWeight = 0.7f;
    public float hpRegenUpgradeWeight = 0.6f;
    public float damageReductionUpgradeWeight = 0.6f;
    public float lifeStealUpgradeWeight = 0.4f;

    [Header("Player Reward Rules")]
    // 升级和击破金库后给玩家的奖励规则。
    [Range(0f, 1f)] public float levelUpHealPercent = 0.3f;
    public int minimumLevelUpHeal = 30;
    public bool fullHealPlayerOnVaultDestroy = true;

    [Header("Background Music")]
    [Tooltip("Background music clip played during gameplay. Leave empty to disable BGM.")]
    public AudioClip backgroundMusic;
    [Range(0f, 1f)] public float backgroundMusicVolume = 0.6f;
    public bool loopBackgroundMusic = true;
    [Tooltip("Optional AudioSource override. If left empty, one is created automatically at runtime.")]
    [SerializeField] private AudioSource backgroundMusicSource;

    [Header("Monster Growth")]
    // 金库每被击破一次，怪物会按这些倍率继续成长。
    public float monsterHpGrowthPerVaultDestroy = 1.1f;
    public float monsterAtkGrowthPerVaultDestroy = 1.1f;
    public float monsterExpGrowthPerVaultDestroy = 0.05f;

    /// <summary>
    /// 注册全局配置实例，并在场景启动时修正配置、播放背景音乐。
    /// </summary>
    private void Awake()
    {
        // 单例赋值：其他脚本通过 GameConfig.instance 找到本配置。
        instance = this;
        EnsureConfig();
        EnsureBackgroundMusicPlayback();
    }

    /// <summary>
    /// Inspector 数值变化时自动修正非法配置，编辑器里也同步音乐源设置。
    /// </summary>
    private void OnValidate()
    {
        // OnValidate 在 Inspector 里改值时触发，能让错误数值立刻被修正。
        EnsureConfig();

        if (Application.isPlaying)
        {
            EnsureBackgroundMusicPlayback();
            return;
        }

        ApplyBackgroundMusicSourceSettings();
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
    /// Player-specific previews are composed inside PlayerCo.
    /// </summary>
    public string GetAttributeUpgradeEffectText(PlayerAttributeType attributeType)
    {
        // 这里描述“升级会增加什么”，比如 +30% 当前攻击力。
        // PlayerCo 会再补上“当前值 -> 升级后值”的预览。
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
                return $"+{playerHpRegenUpgrade:0.##}/+{playerHpRegenUpgrade * 2f:0.##}/+{playerHpRegenUpgrade * 4f:0.##}.../s \u751f\u547d\u6062\u590d";
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
        // 有些属性可以无限成长，有些属性必须限制上限，否则会破坏平衡。
        switch (attributeType)
        {
            case PlayerAttributeType.MoveSpeed:
                return $"\u4e0a\u9650 +{Mathf.RoundToInt(playerMoveSpeedUpgradeCapPercent * 100f)}%";
            case PlayerAttributeType.CritChance:
                return $"\u4e0a\u9650 {Mathf.RoundToInt(playerCritChanceCap * 100f)}%";
            case PlayerAttributeType.DodgeChance:
                return $"\u4e0a\u9650 {Mathf.RoundToInt(playerDodgeChanceCap * 100f)}%";
            case PlayerAttributeType.HealthRegen:
                return $"\u4e0a\u9650 {playerHpRegenCap:0.##}/s";
            case PlayerAttributeType.DamageReduction:
                return $"\u4e0a\u9650 {Mathf.RoundToInt(playerDamageReductionCap * 100f)}%";
            case PlayerAttributeType.LifeSteal:
                return $"\u4e0a\u9650 {Mathf.RoundToInt(playerLifeStealCap * 100f)}%";
            case PlayerAttributeType.AttackPower:
            case PlayerAttributeType.MaxHp:
                return "\u65e0\u4e0a\u9650";
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
    /// 根据金库击破次数计算怪物生命倍率。
    /// </summary>
    public float GetMonsterHpMultiplier(int destroyedVaultCount)
    {
        // Mathf.Pow(a, n) 表示 a 的 n 次方。
        // 例如 1.1 的 3 次方，表示连续成长 3 次。
        return Mathf.Pow(monsterHpGrowthPerVaultDestroy, Mathf.Max(0, destroyedVaultCount));
    }

    /// <summary>
    /// 根据金库击破次数计算怪物攻击倍率。
    /// </summary>
    public float GetMonsterAtkMultiplier(int destroyedVaultCount)
    {
        return Mathf.Pow(monsterAtkGrowthPerVaultDestroy, Mathf.Max(0, destroyedVaultCount));
    }

    /// <summary>
    /// 根据金库击破次数计算怪物经验倍率。
    /// </summary>
    public float GetMonsterExpMultiplier(int destroyedVaultCount)
    {
        return 1f + monsterExpGrowthPerVaultDestroy * Mathf.Max(0, destroyedVaultCount);
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
        playerBaseAttack = Mathf.Max(1, playerBaseAttack);
        playerBaseMoveSpeed = Mathf.Max(0.01f, playerBaseMoveSpeed);
        playerCritDamageMultiplier = Mathf.Max(1f, playerCritDamageMultiplier);
        playerRunSpeedMultiplier = Mathf.Max(1f, playerRunSpeedMultiplier);
        playerMaxHpUpgradeFlat = Mathf.Max(1, playerMaxHpUpgradeFlat);
        playerHpRegenUpgrade = Mathf.Max(0f, playerHpRegenUpgrade);
        playerHpRegenCap = Mathf.Max(0f, playerHpRegenCap);
        levelUpHealPercent = Mathf.Clamp01(levelUpHealPercent);
        minimumLevelUpHeal = Mathf.Max(0, minimumLevelUpHeal);
        monsterHpGrowthPerVaultDestroy = Mathf.Max(1f, monsterHpGrowthPerVaultDestroy);
        monsterAtkGrowthPerVaultDestroy = Mathf.Max(1f, monsterAtkGrowthPerVaultDestroy);
        monsterExpGrowthPerVaultDestroy = Mathf.Max(0f, monsterExpGrowthPerVaultDestroy);
        backgroundMusicVolume = Mathf.Clamp01(backgroundMusicVolume);
    }

    /// <summary>
    /// 确保背景音乐 AudioSource 存在，并按当前配置播放或停止音乐。
    /// </summary>
    private void EnsureBackgroundMusicPlayback()
    {
        // 背景音乐也从配置中心管理，场景启动时自动确保 AudioSource 存在并开始播放。
        EnsureBackgroundMusicSource();
        ApplyBackgroundMusicSourceSettings();

        if (backgroundMusicSource == null)
        {
            return;
        }

        if (backgroundMusic == null)
        {
            // 没有配置音乐时，停止旧音乐并清空 clip。
            if (backgroundMusicSource.isPlaying)
            {
                backgroundMusicSource.Stop();
            }

            backgroundMusicSource.clip = null;
            return;
        }

        if (backgroundMusicSource.clip != backgroundMusic)
        {
            // 如果 Inspector 换了音乐，先停掉旧的，再切换新 clip。
            backgroundMusicSource.Stop();
            backgroundMusicSource.clip = backgroundMusic;
        }

        if (!backgroundMusicSource.isPlaying)
        {
            backgroundMusicSource.Play();
        }
    }

    /// <summary>
    /// 查找或创建用于播放背景音乐的 AudioSource。
    /// </summary>
    private void EnsureBackgroundMusicSource()
    {
        // 先找当前物体上已有的 AudioSource，找不到就自动添加一个。
        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = GetComponent<AudioSource>();
        }

        if (backgroundMusicSource == null)
        {
            backgroundMusicSource = gameObject.AddComponent<AudioSource>();
        }
    }

    /// <summary>
    /// 把循环、音量、2D 声音等设置写入背景音乐 AudioSource。
    /// </summary>
    private void ApplyBackgroundMusicSourceSettings()
    {
        if (backgroundMusicSource == null)
        {
            return;
        }

        backgroundMusicSource.playOnAwake = false;
        backgroundMusicSource.loop = loopBackgroundMusic;
        backgroundMusicSource.volume = backgroundMusicVolume;

        // spatialBlend = 0 表示 2D 声音，不会因为玩家离物体远近而改变音量。
        backgroundMusicSource.spatialBlend = 0f;
    }
}
