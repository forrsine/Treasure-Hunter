using System.Collections.Generic;
using QFramework;

public sealed class GetQuestSnapshotsQuery : AbstractQuery<IReadOnlyList<QuestSnapshot>>
{
    protected override IReadOnlyList<QuestSnapshot> OnDo() => this.GetSystem<QuestSystem>().CreateQuestSnapshots();
}

public sealed class GetQuestProgressSaveDataQuery : AbstractQuery<IReadOnlyList<NQuestProgressSave>>
{
    protected override IReadOnlyList<NQuestProgressSave> OnDo() => this.GetSystem<QuestSystem>().CreateSaveSnapshot();
}

public sealed class AreAllQuestsClaimedQuery : AbstractQuery<bool>
{
    protected override bool OnDo() => this.GetSystem<QuestSystem>().AreAllQuestsClaimed();
}
