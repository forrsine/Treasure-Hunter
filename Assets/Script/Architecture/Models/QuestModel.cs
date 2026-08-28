using System;
using System.Collections.Generic;
using QFramework;

/// <summary>
/// 当前角色的任务运行时数据。这里只保存稳定任务 ID、状态和数量，不保存 UI 或场景 NPC 引用。
/// </summary>
public sealed class QuestModel : AbstractModel
{
    private readonly Dictionary<string, QuestRuntimeProgress> progressByQuestId =
        new Dictionary<string, QuestRuntimeProgress>(StringComparer.Ordinal);

    protected override void OnInit()
    {
        Reset();
    }

    internal QuestRuntimeProgress GetProgress(string questId)
    {
        return !string.IsNullOrWhiteSpace(questId) && progressByQuestId.TryGetValue(questId, out QuestRuntimeProgress progress)
            ? progress
            : new QuestRuntimeProgress(QuestState.Available, 0);
    }

    internal void SetProgress(string questId, QuestState state, int currentCount)
    {
        if (string.IsNullOrWhiteSpace(questId))
        {
            return;
        }

        if (state == QuestState.Available && currentCount <= 0)
        {
            progressByQuestId.Remove(questId);
            return;
        }

        progressByQuestId[questId] = new QuestRuntimeProgress(state, currentCount);
    }

    internal void Reset()
    {
        progressByQuestId.Clear();
    }
}

/// <summary>QuestModel 内部使用的轻量值对象。</summary>
public readonly struct QuestRuntimeProgress
{
    public QuestRuntimeProgress(QuestState state, int currentCount)
    {
        State = state;
        CurrentCount = currentCount;
    }

    public QuestState State { get; }
    public int CurrentCount { get; }
}
