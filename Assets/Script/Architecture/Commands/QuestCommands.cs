using System.Collections.Generic;
using QFramework;

public sealed class AcceptQuestCommand : AbstractCommand<QuestActionResult>
{
    private readonly string questId;
    public AcceptQuestCommand(string questId) => this.questId = questId;
    protected override QuestActionResult OnExecute() => this.GetSystem<QuestSystem>().TryAccept(questId);
}

public sealed class RecordMonsterDefeatedCommand : AbstractCommand<int>
{
    private readonly MonsterKind monsterKind;
    public RecordMonsterDefeatedCommand(MonsterKind monsterKind) => this.monsterKind = monsterKind;
    protected override int OnExecute() => this.GetSystem<QuestSystem>().RecordMonsterDefeated(monsterKind);
}

public sealed class ClaimQuestRewardCommand : AbstractCommand<QuestActionResult>
{
    private readonly string questId;
    public ClaimQuestRewardCommand(string questId) => this.questId = questId;
    protected override QuestActionResult OnExecute() => this.GetSystem<QuestSystem>().TryClaimReward(questId);
}

public sealed class RestoreQuestProgressCommand : AbstractCommand
{
    private readonly IReadOnlyList<NQuestProgressSave> savedProgress;
    public RestoreQuestProgressCommand(IReadOnlyList<NQuestProgressSave> savedProgress) => this.savedProgress = savedProgress;
    protected override void OnExecute() => this.GetSystem<QuestSystem>().Restore(savedProgress);
}

public sealed class ResetQuestProgressCommand : AbstractCommand
{
    protected override void OnExecute() => this.GetSystem<QuestSystem>().Reset();
}
