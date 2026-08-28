/// <summary>任务被玩家主动接取；存档服务据此立即保存。</summary>
public readonly struct QuestAcceptedEvent
{
    public QuestAcceptedEvent(QuestSnapshot snapshot) => Snapshot = snapshot;
    public QuestSnapshot Snapshot { get; }
}

/// <summary>击杀目标后任务数量变化；UI 刷新，存档服务使用防抖保存。</summary>
public readonly struct QuestProgressChangedEvent
{
    public QuestProgressChangedEvent(QuestSnapshot snapshot) => Snapshot = snapshot;
    public QuestSnapshot Snapshot { get; }
}

/// <summary>任务奖励成功领取；金币与任务状态需要作为同一角色快照立即保存。</summary>
public readonly struct QuestRewardClaimedEvent
{
    public QuestRewardClaimedEvent(QuestSnapshot snapshot, long goldReward)
    {
        Snapshot = snapshot;
        GoldReward = goldReward;
    }

    public QuestSnapshot Snapshot { get; }
    public long GoldReward { get; }
}

public readonly struct QuestProgressRestoredEvent { }

/// <summary>任务 NPC 接近状态变化，任务 UI 通过事件刷新交互提示。</summary>
public readonly struct QuestNpcProximityChangedEvent
{
    public QuestNpcProximityChangedEvent(bool isNearby) => IsNearby = isNearby;
    public bool IsNearby { get; }
}

public readonly struct QuestPanelOpenRequestedEvent { }
