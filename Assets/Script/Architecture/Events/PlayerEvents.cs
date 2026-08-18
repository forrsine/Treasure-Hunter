/// <summary>玩家核心属性发生变化，UI 收到后再刷新，避免每帧轮询。</summary>
public readonly struct PlayerStatsChangedEvent { }

/// <summary>待处理升级选择数量发生变化。</summary>
public readonly struct PlayerUpgradeQueueChangedEvent
{
    public PlayerUpgradeQueueChangedEvent(int count) => Count = count;
    public int Count { get; }
}

/// <summary>玩家获得经验，用于播放漂浮文字等表现。</summary>
public readonly struct PlayerExperienceGainedEvent
{
    public PlayerExperienceGainedEvent(int amount) => Amount = amount;
    public int Amount { get; }
}

/// <summary>玩家实际受到伤害后的领域事件。</summary>
public readonly struct PlayerDamagedEvent
{
    public PlayerDamagedEvent(int amount, int currentHp)
    {
        Amount = amount;
        CurrentHp = currentHp;
    }

    public int Amount { get; }
    public int CurrentHp { get; }
}

/// <summary>玩家实际恢复生命后的领域事件。</summary>
public readonly struct PlayerHealedEvent
{
    public PlayerHealedEvent(int amount, bool showFloatingText)
    {
        Amount = amount;
        ShowFloatingText = showFloatingText;
    }

    public int Amount { get; }
    public bool ShowFloatingText { get; }
}

/// <summary>
/// 玩家魔法值发生变化。
/// Delta 小于 0 表示消耗魔法，大于 0 表示恢复魔法，方便后续技能表现或飘字系统订阅。
/// </summary>
public readonly struct PlayerManaChangedEvent
{
    public PlayerManaChangedEvent(int delta, int currentMp, int maxMp)
    {
        Delta = delta;
        CurrentMp = currentMp;
        MaxMp = maxMp;
    }

    public int Delta { get; }
    public int CurrentMp { get; }
    public int MaxMp { get; }
}

/// <summary>本次伤害被闪避，表现层负责显示 miss 和闪烁。</summary>
public readonly struct PlayerDodgedEvent { }

/// <summary>玩家生命归零。死亡菜单只监听该事件，不参与伤害计算。</summary>
public readonly struct PlayerDiedEvent { }
