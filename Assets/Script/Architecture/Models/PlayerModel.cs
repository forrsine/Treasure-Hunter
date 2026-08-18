using QFramework;
using UnityEngine;

/// <summary>
/// 玩家运行时数据模型：只保存一局游戏中的权威数据，不读取输入、不播放动画、不操作 UI。
/// 你可以把它理解成“这一局玩家状态的总账本”：
/// 角色多少血、多少经验、暴击率是多少，都统一存这里。
/// 四个职业共享同一模型结构，职业差异来自 CharacterDefine 配置，而不是写四套状态类。
/// </summary>
public sealed class PlayerModel : AbstractModel
{
    private readonly PlayerRuntimeStats mutableStats = new PlayerRuntimeStats();

    public IPlayerStatsReadOnly Stats => mutableStats;
    internal PlayerRuntimeStats MutableStats => mutableStats;
    public NCharacter CharacterSave { get; private set; }
    public CharacterDefine CharacterDefine { get; private set; }

    protected override void OnInit()
    {
    }

    /// <summary>
    /// 创建一份只读快照，供 UI、查询或调试读取。
    /// 使用快照而不是直接把可写对象交出去，可以减少外部误改核心数据的风险。
    /// </summary>
    public PlayerStatsSnapshot CreateSnapshot()
    {
        return new PlayerStatsSnapshot(mutableStats);
    }

    /// <summary>
    /// 从跨场景快照恢复玩家权威属性。
    /// Boss 房间会先按职业重新生成 GameObject，再用这里把主场景中的血量、蓝量、等级和战斗属性恢复回来。
    /// </summary>
    public void RestoreFromSceneTransferSnapshot(
        NCharacter save,
        CharacterDefine define,
        PlayerStatsSnapshot snapshot)
    {
        CharacterSave = save;
        CharacterDefine = define;

        mutableStats.Level = Mathf.Max(1, snapshot.Level);
        mutableStats.LevelCap = Mathf.Max(mutableStats.Level, snapshot.LevelCap);
        mutableStats.CurrentExp = Mathf.Max(0, snapshot.CurrentExp);
        mutableStats.ExpToNextLevel = snapshot.ExpToNextLevel > 0
            ? snapshot.ExpToNextLevel
            : GetNextExpForLevel(mutableStats.Level);

        mutableStats.BaseMaxHp = Mathf.Max(1, snapshot.BaseMaxHp);
        mutableStats.BonusMaxHp = Mathf.Max(0, snapshot.BonusMaxHp);
        mutableStats.MaxHp = Mathf.Max(1, snapshot.MaxHp);
        mutableStats.CurrentHp = Mathf.Clamp(snapshot.CurrentHp, 0, mutableStats.MaxHp);

        mutableStats.BaseMaxMp = Mathf.Max(1, snapshot.BaseMaxMp);
        mutableStats.BonusMaxMp = Mathf.Max(0, snapshot.BonusMaxMp);
        mutableStats.MaxMp = Mathf.Max(1, snapshot.MaxMp);
        mutableStats.CurrentMp = Mathf.Clamp(snapshot.CurrentMp, 0, mutableStats.MaxMp);

        mutableStats.BaseAttackPower = Mathf.Max(1, snapshot.BaseAttackPower);
        mutableStats.AttackPower = Mathf.Max(1, snapshot.AttackPower);
        mutableStats.BaseMoveSpeed = Mathf.Max(0.01f, snapshot.BaseMoveSpeed);
        mutableStats.CurrentMoveSpeed = Mathf.Max(0.01f, snapshot.CurrentMoveSpeed);
        mutableStats.RunSpeedMultiplier = Mathf.Max(1f, snapshot.RunSpeedMultiplier);

        mutableStats.CritChance = Mathf.Clamp01(snapshot.CritChance);
        mutableStats.CritDamageMultiplier = Mathf.Max(1f, snapshot.CritDamageMultiplier);
        mutableStats.DodgeChance = Mathf.Clamp01(snapshot.DodgeChance);
        mutableStats.HealthRegenPerSecond = Mathf.Max(0f, snapshot.HealthRegenPerSecond);
        mutableStats.DamageReduction = Mathf.Clamp(snapshot.DamageReduction, 0f, 0.95f);
        mutableStats.LifeSteal = Mathf.Clamp01(snapshot.LifeSteal);

        mutableStats.HealthRegenUpgradeCount = Mathf.Max(0, snapshot.HealthRegenUpgradeCount);
        mutableStats.PendingUpgradeSelectionCount = Mathf.Max(0, snapshot.PendingUpgradeSelectionCount);

        // 进入 Boss 房间后应恢复正常操控；待升级次数保留，但不把旧场景的弹窗激活状态带过来。
        mutableStats.IsUpgradeSelectionActive = false;
    }

    /// <summary>
    /// 开始新一局时一次性重置权威数据，避免跨场景或换角色后残留上一局状态。
    /// 这里会把三类数据合并成最终初始面板：
    /// 1. 存档决定等级和经验；
    /// 2. 职业配置决定基础职业差异；
    /// 3. 通用配置决定默认值与成长规则。
    /// </summary>
    public void Reset(NCharacter save, CharacterDefine define)
    {
        CharacterSave = save;
        CharacterDefine = define;

        GameConfig config = GameConfig.instance;
        mutableStats.Level = save != null ? Mathf.Max(1, save.level) : define != null ? Mathf.Max(1, define.initLevel) : 1;
        mutableStats.LevelCap = config != null ? Mathf.Max(mutableStats.Level, config.GetDefaultLevelCap()) : 999;
        mutableStats.CurrentExp = save != null ? Mathf.Max(0, save.exp) : 0;
        mutableStats.ExpToNextLevel = GetNextExpForLevel(mutableStats.Level);

        mutableStats.BaseMaxHp = define != null && define.hp > 0f
            ? Mathf.Max(1, Mathf.RoundToInt(define.hp))
            : config != null ? config.GetPlayerBaseMaxHp() : 150;
        mutableStats.BonusMaxHp = 0;
        mutableStats.BaseMaxMp = define != null && define.mp > 0f
            ? Mathf.Max(1, Mathf.RoundToInt(define.mp))
            : config != null ? config.GetPlayerBaseMaxMp() : 120;
        mutableStats.BonusMaxMp = 0;
        mutableStats.BaseAttackPower = define != null && define.attack > 0f
            ? Mathf.Max(1, Mathf.RoundToInt(define.attack))
            : config != null ? config.GetPlayerBaseAttack() : 25;
        mutableStats.AttackPower = mutableStats.BaseAttackPower;
        mutableStats.BaseMoveSpeed = define != null && define.moveSpeed > 0f
            ? Mathf.Max(0.01f, define.moveSpeed)
            : config != null ? config.GetPlayerBaseMoveSpeed() : 3f;
        mutableStats.CurrentMoveSpeed = mutableStats.BaseMoveSpeed;
        mutableStats.RunSpeedMultiplier = config != null ? config.GetPlayerRunSpeedMultiplier() : 5f / 3f;

        mutableStats.CritChance = config != null ? config.playerBaseCritChance : 0f;
        mutableStats.CritDamageMultiplier = config != null ? Mathf.Max(1f, config.playerCritDamageMultiplier) : 1.5f;
        mutableStats.DodgeChance = config != null ? config.playerBaseDodgeChance : 0f;
        mutableStats.HealthRegenPerSecond = config != null ? Mathf.Max(0f, config.playerBaseHpRegenPerSecond) : 0f;
        mutableStats.DamageReduction = config != null ? config.playerBaseDamageReduction : 0f;
        mutableStats.LifeSteal = config != null ? config.playerBaseLifeSteal : 0f;
        mutableStats.HealthRegenUpgradeCount = 0;
        mutableStats.PendingUpgradeSelectionCount = 0;
        mutableStats.IsUpgradeSelectionActive = false;

        RecalculateMaxHp(true);
        mutableStats.CurrentHp = mutableStats.MaxHp;
        RecalculateMaxMp(true);
        mutableStats.CurrentMp = mutableStats.MaxMp;
    }

    /// <summary>
    /// 查询某个等级升到下一次所需经验。
    /// 即使配置表暂时缺失，也会使用保底值，避免模型初始化失败。
    /// </summary>
    public int GetNextExpForLevel(int level)
    {
        return GameConfig.instance != null
            ? Mathf.Max(1, GameConfig.instance.getNextExp(level))
            : Mathf.Max(1, mutableStats.ExpToNextLevel > 0 ? mutableStats.ExpToNextLevel : 50);
    }

    /// <summary>
    /// 在最大生命值变化后，重新计算 MaxHp，并决定是否补满当前生命。
    /// 这样既能支持“开局直接满血”，也能支持“升级后按当前血量比例调整”。
    /// </summary>
    public void RecalculateMaxHp(bool fillCurrentHp)
    {
        int previousMaxHp = Mathf.Max(1, mutableStats.MaxHp);
        float hpPercent = previousMaxHp > 0 ? (float)mutableStats.CurrentHp / previousMaxHp : 1f;
        mutableStats.MaxHp = Mathf.Max(1, GetLevelBaseMaxHp(mutableStats.Level) + mutableStats.BonusMaxHp);
        mutableStats.CurrentHp = fillCurrentHp
            ? mutableStats.MaxHp
            : Mathf.Clamp(Mathf.CeilToInt(mutableStats.MaxHp * hpPercent), 0, mutableStats.MaxHp);
    }

    /// <summary>
    /// 重新计算最大魔法值。
    /// 技能系统还没接入前先把资源规则放在模型层，后续新增职业、装备或 Buff 改蓝量时都能复用这里。
    /// </summary>
    public void RecalculateMaxMp(bool fillCurrentMp)
    {
        int previousMaxMp = Mathf.Max(1, mutableStats.MaxMp);
        float mpPercent = previousMaxMp > 0 ? (float)mutableStats.CurrentMp / previousMaxMp : 1f;
        mutableStats.MaxMp = Mathf.Max(1, mutableStats.BaseMaxMp + mutableStats.BonusMaxMp);
        mutableStats.CurrentMp = fillCurrentMp
            ? mutableStats.MaxMp
            : Mathf.Clamp(Mathf.CeilToInt(mutableStats.MaxMp * mpPercent), 0, mutableStats.MaxMp);
    }

    /// <summary>
    /// 计算指定等级下的基础生命值。
    /// 如果职业配置里提供了职业基础 HP，就在职业基础上叠等级成长；
    /// 否则退回 GameConfig 的通用曲线。
    /// </summary>
    private int GetLevelBaseMaxHp(int level)
    {
        if (CharacterDefine != null && CharacterDefine.hp > 0f)
        {
            int characterBaseHp = Mathf.Max(1, Mathf.RoundToInt(CharacterDefine.hp));
            if (GameConfig.instance == null)
            {
                return characterBaseHp;
            }

            int growthHp = Mathf.Max(0, GameConfig.instance.getMaxHp(level) - GameConfig.instance.getMaxHp(1));
            return characterBaseHp + growthHp;
        }

        return GameConfig.instance != null
            ? Mathf.Max(1, GameConfig.instance.getMaxHp(level))
            : Mathf.Max(1, mutableStats.BaseMaxHp);
    }
}
