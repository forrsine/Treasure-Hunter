using System;

/// <summary>
/// 玩家运行时属性模型。
///
/// 这个类只保存“一局游戏中会变化的数据”，例如当前生命、等级、攻击力和升级次数，
/// 不继承 MonoBehaviour，也不直接刷新 UI、播放动画或读取输入。
/// PlayerModel 持有这一份数据，各模块通过架构访问，避免把 MonoBehaviour 当成公共变量仓库。
/// </summary>
public sealed class PlayerRuntimeStats : IPlayerStatsReadOnly
{
    // 生存与成长状态：会在一局游戏中持续变化，并由 UI 事件驱动显示。
    public int CurrentHp { get; internal set; }
    public int MaxHp { get; internal set; }
    public int CurrentMp { get; internal set; }
    public int MaxMp { get; internal set; }
    public int Level { get; internal set; }
    public int LevelCap { get; internal set; }
    public int CurrentExp { get; internal set; }
    public int ExpToNextLevel { get; internal set; }

    // 战斗与职业基础属性：成长组件修改，战斗/生命组件只读取自己需要的部分。
    public int AttackPower { get; internal set; }
    public int BaseMaxHp { get; internal set; }
    public int BonusMaxHp { get; internal set; }
    public int BaseMaxMp { get; internal set; }
    public int BonusMaxMp { get; internal set; }
    public int BaseAttackPower { get; internal set; }
    public float BaseMoveSpeed { get; internal set; }
    public float CurrentMoveSpeed { get; internal set; }
    public float RunSpeedMultiplier { get; internal set; }
    public float CritChance { get; internal set; }
    public float CritDamageMultiplier { get; internal set; }
    public float DodgeChance { get; internal set; }
    public float HealthRegenPerSecond { get; internal set; }
    public float DamageReduction { get; internal set; }
    public float LifeSteal { get; internal set; }

    // 升级流程状态：记录递增回血次数和仍未处理的三选一队列。
    public int HealthRegenUpgradeCount { get; internal set; }
    public int PendingUpgradeSelectionCount { get; internal set; }
    public bool IsUpgradeSelectionActive { get; internal set; }

    /// <summary>
    /// 核心属性变化事件。UI 只在收到事件时刷新，避免每帧主动查询全部数据。
    /// </summary>
    public event Action StatsChanged;

    /// <summary>
    /// 待处理升级次数变化事件，专门驱动升级三选一面板。
    /// </summary>
    public event Action<int> PendingUpgradeSelectionsChanged;

    public void NotifyStatsChanged()
    {
        StatsChanged?.Invoke();
    }

    public void NotifyPendingUpgradeSelectionsChanged()
    {
        PendingUpgradeSelectionsChanged?.Invoke(PendingUpgradeSelectionCount);
    }
}
