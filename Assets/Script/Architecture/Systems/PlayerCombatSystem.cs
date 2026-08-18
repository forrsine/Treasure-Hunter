using QFramework;
using UnityEngine;

/// <summary>
/// 一次攻击伤害计算结果。表现层只关心数值和是否暴击，不需要知道公式。
/// </summary>
public readonly struct PlayerAttackRoll
{
    public PlayerAttackRoll(int damage, bool isCritical)
    {
        Damage = damage;
        IsCritical = isCritical;
    }

    public int Damage { get; }
    public bool IsCritical { get; }
}

/// <summary>
/// 一次玩家受伤结算结果，用于让 Unity 表现组件决定闪红、闪避闪烁或死亡表现。
/// </summary>
public readonly struct PlayerDamageResult
{
    public PlayerDamageResult(bool dodged, int actualDamage, bool died)
    {
        Dodged = dodged;
        ActualDamage = actualDamage;
        Died = died;
    }

    public bool Dodged { get; }
    public int ActualDamage { get; }
    public bool Died { get; }
}

/// <summary>
/// 玩家战斗系统：集中管理暴击、减伤、闪避、回血和吸血公式。
/// 它不持有 Collider、Animator 等 Unity 表现对象，因此可以被不同职业共同复用。
/// </summary>
public sealed class PlayerCombatSystem : AbstractSystem
{
    private PlayerModel model;
    private float lifeStealBuffer;

    protected override void OnInit()
    {
        model = this.GetModel<PlayerModel>();
    }

    /// <summary>
    /// 清理跨局或跨角色残留的临时缓冲。
    /// 当前主要是吸血的小数缓存，避免上一局的数据泄漏到下一局。
    /// </summary>
    public void ResetRuntimeBuffers()
    {
        lifeStealBuffer = 0f;
    }

    /// <summary>
    /// 计算一次普通攻击的最终伤害。
    /// 这里统一处理暴击概率和暴击倍率，外部脚本不需要自己再写一套公式。
    /// </summary>
    public PlayerAttackRoll RollAttackDamage()
    {
        PlayerRuntimeStats stats = model.MutableStats;
        int damage = Mathf.Max(1, stats.AttackPower);
        bool isCritical = Random.value < stats.CritChance;
        if (isCritical)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * stats.CritDamageMultiplier));
        }

        return new PlayerAttackRoll(damage, isCritical);
    }

    /// <summary>
    /// 处理玩家受伤结算。
    /// 顺序是：先判断是否还能受伤，再判断闪避，再计算减伤，最后广播血量变化事件。
    /// 这样近战、子弹、陷阱都能复用同一套规则。
    /// </summary>
    public PlayerDamageResult TakeDamage(int incomingAttackPower, bool allowDodge)
    {
        PlayerRuntimeStats stats = model.MutableStats;
        if (incomingAttackPower <= 0 || stats.CurrentHp <= 0)
        {
            return new PlayerDamageResult(false, 0, stats.CurrentHp <= 0);
        }

        if (allowDodge && stats.DodgeChance > 0f && Random.value < stats.DodgeChance)
        {
            this.SendEvent(new PlayerDodgedEvent());
            return new PlayerDamageResult(true, 0, false);
        }

        int finalDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(incomingAttackPower * (1f - Mathf.Clamp01(stats.DamageReduction))));
        int hpBeforeHit = stats.CurrentHp;
        stats.CurrentHp = Mathf.Max(0, stats.CurrentHp - finalDamage);
        int actualDamage = hpBeforeHit - stats.CurrentHp;

        this.SendEvent(new PlayerDamagedEvent(actualDamage, stats.CurrentHp));
        this.SendEvent(new PlayerStatsChangedEvent());

        bool died = stats.CurrentHp <= 0;
        if (died)
        {
            this.SendEvent(new PlayerDiedEvent());
        }

        return new PlayerDamageResult(false, actualDamage, died);
    }

    /// <summary>
    /// 处理玩家回血。
    /// 无论回血来自升级、吸血还是持续恢复，最终都走这里，
    /// 这样 UI 只需要监听统一事件。
    /// </summary>
    public int Heal(int amount, bool showFloatingText)
    {
        PlayerRuntimeStats stats = model.MutableStats;
        if (amount <= 0 || stats.CurrentHp <= 0 || stats.CurrentHp >= stats.MaxHp)
        {
            return 0;
        }

        int before = stats.CurrentHp;
        stats.CurrentHp = Mathf.Min(stats.MaxHp, stats.CurrentHp + amount);
        int actual = stats.CurrentHp - before;
        if (actual > 0)
        {
            this.SendEvent(new PlayerHealedEvent(actual, showFloatingText));
            this.SendEvent(new PlayerStatsChangedEvent());
        }

        return actual;
    }

    /// <summary>
    /// 直接把当前生命补到上限。
    /// </summary>
    public int FullHeal()
    {
        return Heal(Mathf.Max(0, model.Stats.MaxHp - model.Stats.CurrentHp), false);
    }

    /// <summary>
    /// 记录玩家本次造成的真实伤害，并把吸血折算为治疗。
    /// 单独拆这个入口，是为了确保吸血基于“命中后实际生效的伤害”。
    /// </summary>
    public int HandleDamageDealt(int appliedDamage)
    {
        PlayerRuntimeStats stats = model.MutableStats;
        if (stats.LifeSteal <= 0f || appliedDamage <= 0)
        {
            return 0;
        }

        lifeStealBuffer += appliedDamage * stats.LifeSteal;
        int requestedHeal = Mathf.FloorToInt(lifeStealBuffer);
        if (requestedHeal <= 0)
        {
            return 0;
        }

        lifeStealBuffer -= requestedHeal;
        return Heal(requestedHeal, true);
    }
}
