using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 任务业务系统：统一处理接取、目标计数、完成、领奖和存档恢复。
/// UI 与怪物都只能通过 Command 调用这里，避免任何表现层直接修改任务数据。
/// </summary>
public sealed class QuestSystem : AbstractSystem
{
    private QuestModel model;
    private EconomySystem economySystem;

    public QuestCatalog Catalog { get; private set; }

    protected override void OnInit()
    {
        model = this.GetModel<QuestModel>();
        economySystem = this.GetSystem<EconomySystem>();
        Catalog = Resources.Load<QuestCatalog>(QuestCatalog.ResourcesPath);
    }

    public void ConfigureCatalog(QuestCatalog catalog)
    {
        Catalog = catalog;
        model.Reset();
        this.SendEvent(new QuestProgressRestoredEvent());
    }

    public QuestActionResult TryAccept(string questId)
    {
        if (!TryGetDefinition(questId, out QuestDefinition definition))
        {
            return Failure(QuestActionFailure.UnknownQuest);
        }

        QuestRuntimeProgress progress = model.GetProgress(questId);
        if (progress.State != QuestState.Available)
        {
            return Failure(QuestActionFailure.InvalidState, CreateSnapshot(definition, progress));
        }

        model.SetProgress(questId, QuestState.Active, 0);
        QuestSnapshot snapshot = new QuestSnapshot(definition, QuestState.Active, 0);
        this.SendEvent(new QuestAcceptedEvent(snapshot));
        return Success(snapshot);
    }

    /// <summary>
    /// 记录一次正式怪物死亡。只推进已接取且目标匹配的任务，数量达到目标后自动进入可领奖状态。
    /// </summary>
    public int RecordMonsterDefeated(MonsterKind monsterKind)
    {
        if (Catalog == null || Catalog.Entries == null)
        {
            return 0;
        }

        int changedCount = 0;
        for (int i = 0; i < Catalog.Entries.Length; i++)
        {
            QuestDefinition definition = Catalog.Entries[i];
            if (definition == null ||
                definition.ObjectiveType != QuestObjectiveType.KillMonster ||
                definition.TargetMonster != monsterKind)
            {
                continue;
            }

            QuestRuntimeProgress progress = model.GetProgress(definition.QuestId);
            if (progress.State != QuestState.Active)
            {
                continue;
            }

            int nextCount = Mathf.Min(definition.RequiredCount, progress.CurrentCount + 1);
            QuestState nextState = nextCount >= definition.RequiredCount
                ? QuestState.ReadyToClaim
                : QuestState.Active;
            model.SetProgress(definition.QuestId, nextState, nextCount);
            QuestSnapshot snapshot = new QuestSnapshot(definition, nextState, nextCount);
            this.SendEvent(new QuestProgressChangedEvent(snapshot));
            changedCount++;
        }

        return changedCount;
    }

    public QuestActionResult TryClaimReward(string questId)
    {
        if (!TryGetDefinition(questId, out QuestDefinition definition))
        {
            return Failure(QuestActionFailure.UnknownQuest);
        }

        QuestRuntimeProgress progress = model.GetProgress(questId);
        QuestSnapshot beforeClaim = CreateSnapshot(definition, progress);
        if (progress.State != QuestState.ReadyToClaim)
        {
            return Failure(QuestActionFailure.InvalidState, beforeClaim);
        }

        if (economySystem == null || economySystem.CurrentGold > EconomySystem.MaxGold - definition.GoldReward)
        {
            return Failure(QuestActionFailure.GoldLimitExceeded, beforeClaim);
        }

        // 先把任务置为已领取，再发金币事件，使自动存档即使立刻响应 GoldChanged 也能读到同一份完整快照。
        model.SetProgress(questId, QuestState.Claimed, definition.RequiredCount);
        // System 本身不暴露 ICanSendCommand；通过所属架构派发命令，仍然复用项目唯一的金币入口。
        long addedGold = ((IBelongToArchitecture)this).GetArchitecture()
            .SendCommand(new AddGoldCommand(definition.GoldReward));
        if (addedGold != definition.GoldReward)
        {
            model.SetProgress(questId, QuestState.ReadyToClaim, definition.RequiredCount);
            return Failure(QuestActionFailure.InternalError, beforeClaim);
        }

        QuestSnapshot claimed = new QuestSnapshot(definition, QuestState.Claimed, definition.RequiredCount);
        this.SendEvent(new QuestRewardClaimedEvent(claimed, addedGold));
        return Success(claimed);
    }

    /// <summary>
    /// 从存档恢复时只接受目录中的任务，并把数量和状态归一化。
    /// 旧存档没有记录的任务自然保持 Available，保证向后兼容。
    /// </summary>
    public void Restore(IReadOnlyList<NQuestProgressSave> savedProgress)
    {
        model.Reset();
        if (savedProgress != null)
        {
            var restoredIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < savedProgress.Count; i++)
            {
                NQuestProgressSave saved = savedProgress[i];
                if (saved == null || !restoredIds.Add(saved.questId) ||
                    !TryGetDefinition(saved.questId, out QuestDefinition definition))
                {
                    continue;
                }

                QuestState state = Enum.IsDefined(typeof(QuestState), saved.state)
                    ? (QuestState)saved.state
                    : QuestState.Available;
                int count = Mathf.Clamp(saved.currentCount, 0, definition.RequiredCount);

                if (state == QuestState.Active && count >= definition.RequiredCount)
                {
                    state = QuestState.ReadyToClaim;
                }
                else if (state == QuestState.ReadyToClaim || state == QuestState.Claimed)
                {
                    count = definition.RequiredCount;
                }
                else if (state == QuestState.Available)
                {
                    count = 0;
                }

                model.SetProgress(definition.QuestId, state, count);
            }
        }

        this.SendEvent(new QuestProgressRestoredEvent());
    }

    public void Reset()
    {
        model.Reset();
        this.SendEvent(new QuestProgressRestoredEvent());
    }

    public List<QuestSnapshot> CreateQuestSnapshots()
    {
        var snapshots = new List<QuestSnapshot>();
        if (Catalog == null || Catalog.Entries == null)
        {
            return snapshots;
        }

        for (int i = 0; i < Catalog.Entries.Length; i++)
        {
            QuestDefinition definition = Catalog.Entries[i];
            if (definition != null && !string.IsNullOrWhiteSpace(definition.QuestId))
            {
                snapshots.Add(CreateSnapshot(definition, model.GetProgress(definition.QuestId)));
            }
        }

        return snapshots;
    }

    public List<NQuestProgressSave> CreateSaveSnapshot()
    {
        List<QuestSnapshot> snapshots = CreateQuestSnapshots();
        var result = new List<NQuestProgressSave>();
        for (int i = 0; i < snapshots.Count; i++)
        {
            QuestSnapshot snapshot = snapshots[i];
            if (snapshot.State == QuestState.Available)
            {
                continue;
            }

            result.Add(new NQuestProgressSave
            {
                questId = snapshot.Definition.QuestId,
                state = (int)snapshot.State,
                currentCount = snapshot.CurrentCount
            });
        }

        return result;
    }

    public bool AreAllQuestsClaimed()
    {
        List<QuestSnapshot> snapshots = CreateQuestSnapshots();
        if (snapshots.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].State != QuestState.Claimed)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetDefinition(string questId, out QuestDefinition definition)
    {
        definition = null;
        return Catalog != null && Catalog.TryGetQuest(questId, out definition);
    }

    private static QuestSnapshot CreateSnapshot(QuestDefinition definition, QuestRuntimeProgress progress)
    {
        return new QuestSnapshot(definition, progress.State, progress.CurrentCount);
    }

    private static QuestActionResult Success(QuestSnapshot snapshot)
    {
        return new QuestActionResult(true, QuestActionFailure.None, snapshot);
    }

    private static QuestActionResult Failure(QuestActionFailure failure, QuestSnapshot snapshot = default)
    {
        return new QuestActionResult(false, failure, snapshot);
    }
}
