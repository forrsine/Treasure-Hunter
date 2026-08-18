using QFramework;
using UnityEngine;

/// <summary>
/// 玩家资源系统：负责魔法值这类“技能资源”的消耗和恢复。
/// 注意：技能系统后续只需要调用 TrySpendPlayerManaCommand，不应该直接修改 PlayerModel，
/// 这样蓝量不足、UI 刷新、联网同步或日志记录都能集中在一个入口处理。
/// </summary>
public sealed class PlayerResourceSystem : AbstractSystem
{
    private PlayerModel model;

    private PlayerRuntimeStats Stats => model.MutableStats;

    protected override void OnInit()
    {
        model = this.GetModel<PlayerModel>();
    }

    /// <summary>
    /// 只判断魔法是否足够，不修改数据。
    /// 技能按钮置灰、技能释放前预检查，都可以使用这个方法。
    /// </summary>
    public bool CanSpendMana(int amount)
    {
        if (Stats.CurrentHp <= 0)
        {
            return false;
        }

        if (amount <= 0)
        {
            return true;
        }

        return Stats.CurrentMp >= amount;
    }

    /// <summary>
    /// 尝试消耗魔法值。
    /// 返回 false 表示蓝量不足或玩家已经死亡，调用方应该取消技能释放。
    /// </summary>
    public bool TrySpendMana(int amount)
    {
        if (!CanSpendMana(amount))
        {
            return false;
        }

        if (amount <= 0)
        {
            return true;
        }

        Stats.CurrentMp = Mathf.Max(0, Stats.CurrentMp - amount);
        this.SendEvent(new PlayerManaChangedEvent(-amount, Stats.CurrentMp, Stats.MaxMp));
        this.SendEvent(new PlayerStatsChangedEvent());
        return true;
    }

    /// <summary>
    /// 恢复魔法值，返回实际恢复量。
    /// 药水、升级奖励、装备效果以后都可以共用这个入口。
    /// </summary>
    public int RestoreMana(int amount)
    {
        if (amount <= 0 || Stats.CurrentHp <= 0 || Stats.CurrentMp >= Stats.MaxMp)
        {
            return 0;
        }

        int before = Stats.CurrentMp;
        Stats.CurrentMp = Mathf.Min(Stats.MaxMp, Stats.CurrentMp + amount);
        int actual = Stats.CurrentMp - before;
        if (actual > 0)
        {
            this.SendEvent(new PlayerManaChangedEvent(actual, Stats.CurrentMp, Stats.MaxMp));
            this.SendEvent(new PlayerStatsChangedEvent());
        }

        return actual;
    }

    /// <summary>
    /// 直接回满魔法，常用于补给或调试。
    /// </summary>
    public int FullRestoreMana()
    {
        return RestoreMana(Mathf.Max(0, Stats.MaxMp - Stats.CurrentMp));
    }
}
