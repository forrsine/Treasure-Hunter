/// <summary>
/// 玩家业务属性只读契约。
/// 表现层、UI 和 Query 只能通过 getter 观察数据，不能绕过 Command/System 修改权威状态。
/// </summary>
public interface IPlayerStatsReadOnly
{
    int CurrentHp { get; }
    int MaxHp { get; }
    int CurrentMp { get; }
    int MaxMp { get; }
    int Level { get; }
    int LevelCap { get; }
    int CurrentExp { get; }
    int ExpToNextLevel { get; }
    int AttackPower { get; }
    int BaseMaxHp { get; }
    int BonusMaxHp { get; }
    int BaseMaxMp { get; }
    int BonusMaxMp { get; }
    int BaseAttackPower { get; }
    float BaseMoveSpeed { get; }
    float CurrentMoveSpeed { get; }
    float RunSpeedMultiplier { get; }
    float CritChance { get; }
    float CritDamageMultiplier { get; }
    float DodgeChance { get; }
    float HealthRegenPerSecond { get; }
    float DamageReduction { get; }
    float LifeSteal { get; }
    int HealthRegenUpgradeCount { get; }
    int PendingUpgradeSelectionCount { get; }
    bool IsUpgradeSelectionActive { get; }
}

/// <summary>
/// 玩家属性不可变快照。Query 返回值类型副本，调用者既不能修改，也不会持有 Model 内部对象。
/// </summary>
public readonly struct PlayerStatsSnapshot : IPlayerStatsReadOnly
{
    public PlayerStatsSnapshot(IPlayerStatsReadOnly source)
    {
        CurrentHp = source.CurrentHp;
        MaxHp = source.MaxHp;
        CurrentMp = source.CurrentMp;
        MaxMp = source.MaxMp;
        Level = source.Level;
        LevelCap = source.LevelCap;
        CurrentExp = source.CurrentExp;
        ExpToNextLevel = source.ExpToNextLevel;
        AttackPower = source.AttackPower;
        BaseMaxHp = source.BaseMaxHp;
        BonusMaxHp = source.BonusMaxHp;
        BaseMaxMp = source.BaseMaxMp;
        BonusMaxMp = source.BonusMaxMp;
        BaseAttackPower = source.BaseAttackPower;
        BaseMoveSpeed = source.BaseMoveSpeed;
        CurrentMoveSpeed = source.CurrentMoveSpeed;
        RunSpeedMultiplier = source.RunSpeedMultiplier;
        CritChance = source.CritChance;
        CritDamageMultiplier = source.CritDamageMultiplier;
        DodgeChance = source.DodgeChance;
        HealthRegenPerSecond = source.HealthRegenPerSecond;
        DamageReduction = source.DamageReduction;
        LifeSteal = source.LifeSteal;
        HealthRegenUpgradeCount = source.HealthRegenUpgradeCount;
        PendingUpgradeSelectionCount = source.PendingUpgradeSelectionCount;
        IsUpgradeSelectionActive = source.IsUpgradeSelectionActive;
    }

    public int CurrentHp { get; }
    public int MaxHp { get; }
    public int CurrentMp { get; }
    public int MaxMp { get; }
    public int Level { get; }
    public int LevelCap { get; }
    public int CurrentExp { get; }
    public int ExpToNextLevel { get; }
    public int AttackPower { get; }
    public int BaseMaxHp { get; }
    public int BonusMaxHp { get; }
    public int BaseMaxMp { get; }
    public int BonusMaxMp { get; }
    public int BaseAttackPower { get; }
    public float BaseMoveSpeed { get; }
    public float CurrentMoveSpeed { get; }
    public float RunSpeedMultiplier { get; }
    public float CritChance { get; }
    public float CritDamageMultiplier { get; }
    public float DodgeChance { get; }
    public float HealthRegenPerSecond { get; }
    public float DamageReduction { get; }
    public float LifeSteal { get; }
    public int HealthRegenUpgradeCount { get; }
    public int PendingUpgradeSelectionCount { get; }
    public bool IsUpgradeSelectionActive { get; }
}
