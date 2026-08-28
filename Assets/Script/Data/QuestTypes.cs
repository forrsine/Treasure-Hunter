using System;
using UnityEngine;

/// <summary>
/// 怪物的稳定玩法身份：任务系统按这个枚举统计目标，不依赖 Prefab 名称或材质颜色。
/// SlimeType 继续只负责近战/远程攻击差异，避免“外观身份”和“战斗行为”混在一起。
/// </summary>
public enum MonsterKind
{
    RedSlime = 0,
    GreenSlime = 1
}

public enum QuestObjectiveType
{
    KillMonster = 0
}

/// <summary>一次性任务的完整生命周期，只允许从左向右推进。</summary>
public enum QuestState
{
    Available = 0,
    Active = 1,
    ReadyToClaim = 2,
    Claimed = 3
}

public enum QuestActionFailure
{
    None = 0,
    UnknownQuest = 1,
    InvalidState = 2,
    GoldLimitExceeded = 3,
    InternalError = 4
}

/// <summary>
/// 单条任务的静态配置：只描述任务是什么，不保存任何角色进度。
/// 后续增加收集或交互任务时，可以扩展 ObjectiveType，而不需要改 UI 数据结构。
/// </summary>
[Serializable]
public sealed class QuestDefinition
{
    [SerializeField] private string questId;
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.KillMonster;
    [SerializeField] private MonsterKind targetMonster;
    [SerializeField, Min(1)] private int requiredCount = 1;
    [SerializeField, Min(1)] private long goldReward = 1L;

    public string QuestId => questId ?? string.Empty;
    public string DisplayName => displayName ?? string.Empty;
    public string Description => description ?? string.Empty;
    public QuestObjectiveType ObjectiveType => objectiveType;
    public MonsterKind TargetMonster => targetMonster;
    public int RequiredCount => Mathf.Max(1, requiredCount);
    public long GoldReward => Math.Max(1L, goldReward);
}

/// <summary>UI 和外部模块使用的只读任务快照，避免直接拿到可写 Model。</summary>
public readonly struct QuestSnapshot
{
    public QuestSnapshot(QuestDefinition definition, QuestState state, int currentCount)
    {
        Definition = definition;
        State = state;
        CurrentCount = definition != null
            ? Mathf.Clamp(currentCount, 0, definition.RequiredCount)
            : Mathf.Max(0, currentCount);
    }

    public QuestDefinition Definition { get; }
    public QuestState State { get; }
    public int CurrentCount { get; }
    public bool IsValid => Definition != null;
}

/// <summary>任务操作结果：System 返回规则结果，UI 只负责翻译失败原因。</summary>
public readonly struct QuestActionResult
{
    public QuestActionResult(bool success, QuestActionFailure failure, QuestSnapshot snapshot)
    {
        Success = success;
        Failure = failure;
        Snapshot = snapshot;
    }

    public bool Success { get; }
    public QuestActionFailure Failure { get; }
    public QuestSnapshot Snapshot { get; }
}
