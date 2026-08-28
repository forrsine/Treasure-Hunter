#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using QFramework;
using UnityEngine;

/// <summary>
/// 任务领域规则测试：重点验证“先接取再计数、目标隔离、手动领奖、一次性和金币原子性”。
/// </summary>
public sealed class QuestSystemTests
{
    private const string RedQuestId = "hunt_red_slime";
    private const string GreenQuestId = "hunt_green_slime";

    private IArchitecture architecture;
    private QuestSystem questSystem;
    private EconomySystem economySystem;

    [SetUp]
    public void SetUp()
    {
        architecture = TreasureHunterArchitecture.Interface;
        questSystem = architecture.GetSystem<QuestSystem>();
        economySystem = architecture.GetSystem<EconomySystem>();
        QuestCatalog catalog = Resources.Load<QuestCatalog>(QuestCatalog.ResourcesPath);
        Assert.That(catalog, Is.Not.Null);
        questSystem.ConfigureCatalog(catalog);
        economySystem.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.Deinit();
        architecture = null;
    }

    [Test]
    public void NewCharacter_HasTwoAvailableQuests()
    {
        IReadOnlyList<QuestSnapshot> snapshots = architecture.SendQuery(new GetQuestSnapshotsQuery());

        Assert.That(snapshots, Has.Count.EqualTo(2));
        Assert.That(Find(snapshots, RedQuestId).State, Is.EqualTo(QuestState.Available));
        Assert.That(Find(snapshots, GreenQuestId).State, Is.EqualTo(QuestState.Available));
    }

    [Test]
    public void MonsterDeath_BeforeAccept_DoesNotCount()
    {
        int changed = architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.RedSlime));

        Assert.That(changed, Is.Zero);
        Assert.That(Get(RedQuestId).CurrentCount, Is.Zero);
    }

    [Test]
    public void AcceptedQuest_OnlyCountsMatchingMonster_AndCapsAtTarget()
    {
        Assert.That(architecture.SendCommand(new AcceptQuestCommand(RedQuestId)).Success, Is.True);
        architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.GreenSlime));
        Assert.That(Get(RedQuestId).CurrentCount, Is.Zero);

        for (int i = 0; i < 8; i++)
        {
            architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.RedSlime));
        }

        QuestSnapshot completed = Get(RedQuestId);
        Assert.That(completed.CurrentCount, Is.EqualTo(5));
        Assert.That(completed.State, Is.EqualTo(QuestState.ReadyToClaim));
    }

    [Test]
    public void BothQuests_CanBeActiveAndProgressIndependently()
    {
        architecture.SendCommand(new AcceptQuestCommand(RedQuestId));
        architecture.SendCommand(new AcceptQuestCommand(GreenQuestId));
        architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.RedSlime));
        architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.GreenSlime));
        architecture.SendCommand(new RecordMonsterDefeatedCommand(MonsterKind.GreenSlime));

        Assert.That(Get(RedQuestId).CurrentCount, Is.EqualTo(1));
        Assert.That(Get(GreenQuestId).CurrentCount, Is.EqualTo(2));
    }

    [Test]
    public void ClaimReward_AddsExactGold_AndCannotRepeat()
    {
        Complete(RedQuestId, MonsterKind.RedSlime, 5);

        QuestActionResult first = architecture.SendCommand(new ClaimQuestRewardCommand(RedQuestId));
        QuestActionResult second = architecture.SendCommand(new ClaimQuestRewardCommand(RedQuestId));

        Assert.That(first.Success, Is.True);
        Assert.That(second.Success, Is.False);
        Assert.That(second.Failure, Is.EqualTo(QuestActionFailure.InvalidState));
        Assert.That(economySystem.CurrentGold, Is.EqualTo(50L));
        Assert.That(Get(RedQuestId).State, Is.EqualTo(QuestState.Claimed));
    }

    [Test]
    public void ClaimReward_WhenGoldCannotFit_KeepsReadyStateAndGold()
    {
        Complete(RedQuestId, MonsterKind.RedSlime, 5);
        economySystem.Restore(EconomySystem.MaxGold - 49L);

        QuestActionResult result = architecture.SendCommand(new ClaimQuestRewardCommand(RedQuestId));

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(QuestActionFailure.GoldLimitExceeded));
        Assert.That(economySystem.CurrentGold, Is.EqualTo(EconomySystem.MaxGold - 49L));
        Assert.That(Get(RedQuestId).State, Is.EqualTo(QuestState.ReadyToClaim));
    }

    [Test]
    public void Restore_NormalizesCountsAndIgnoresUnknownQuest()
    {
        architecture.SendCommand(new RestoreQuestProgressCommand(new List<NQuestProgressSave>
        {
            new NQuestProgressSave { questId = RedQuestId, state = (int)QuestState.Active, currentCount = 99 },
            new NQuestProgressSave { questId = "not_in_catalog", state = (int)QuestState.Claimed, currentCount = 1 }
        }));

        Assert.That(Get(RedQuestId).State, Is.EqualTo(QuestState.ReadyToClaim));
        Assert.That(Get(RedQuestId).CurrentCount, Is.EqualTo(5));
        Assert.That(Get(GreenQuestId).State, Is.EqualTo(QuestState.Available));
    }

    private void Complete(string questId, MonsterKind monsterKind, int count)
    {
        architecture.SendCommand(new AcceptQuestCommand(questId));
        for (int i = 0; i < count; i++)
        {
            architecture.SendCommand(new RecordMonsterDefeatedCommand(monsterKind));
        }
    }

    private QuestSnapshot Get(string questId)
    {
        return Find(architecture.SendQuery(new GetQuestSnapshotsQuery()), questId);
    }

    private static QuestSnapshot Find(IReadOnlyList<QuestSnapshot> snapshots, string questId)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].IsValid && snapshots[i].Definition.QuestId == questId)
            {
                return snapshots[i];
            }
        }
        Assert.Fail($"找不到任务：{questId}");
        return default;
    }
}
#endif
