namespace GameServer;

using SkillBridge.Message;

/// <summary>
/// 任务存档白名单与结构校验。当前战斗由客户端驱动，服务端负责防止未知任务、重复记录和进度回滚，
/// 后续接入服务端权威战斗时可以继续在这里校验击杀来源。
/// </summary>
public static class QuestPersistenceRules
{
    private const int ActiveState = 1;
    private const int ReadyToClaimState = 2;
    private const int ClaimedState = 3;

    private static readonly IReadOnlyDictionary<string, int> RequiredCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["hunt_red_slime"] = 5,
            ["hunt_green_slime"] = 8
        };

    public static bool TryValidate(
        IEnumerable<NQuestProgressInfo> requestedProgress,
        IReadOnlyList<TQuestProgress> currentProgress,
        out List<TQuestProgress> normalizedProgress,
        out string error)
    {
        normalizedProgress = new List<TQuestProgress>();
        var usedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (NQuestProgressInfo progress in requestedProgress ?? Array.Empty<NQuestProgressInfo>())
        {
            if (progress == null || string.IsNullOrWhiteSpace(progress.QuestId) || !usedIds.Add(progress.QuestId))
            {
                error = "任务进度包含空 ID 或重复记录";
                return false;
            }

            if (!RequiredCounts.TryGetValue(progress.QuestId, out int requiredCount))
            {
                error = $"任务不在白名单中：{progress.QuestId}";
                return false;
            }

            // Available 状态不需要落库；若客户端显式提交，则必须是 0 进度。
            if (progress.State == 0 && progress.CurrentCount == 0)
            {
                continue;
            }

            bool validActive = progress.State == ActiveState &&
                               progress.CurrentCount >= 0 &&
                               progress.CurrentCount < requiredCount;
            bool validCompleted = (progress.State == ReadyToClaimState || progress.State == ClaimedState) &&
                                  progress.CurrentCount == requiredCount;
            if (!validActive && !validCompleted)
            {
                error = $"任务状态与完成数量不一致：{progress.QuestId}";
                return false;
            }

            normalizedProgress.Add(new TQuestProgress
            {
                QuestId = progress.QuestId,
                State = progress.State,
                CurrentCount = progress.CurrentCount
            });
        }

        if (currentProgress != null)
        {
            foreach (TQuestProgress existing in currentProgress)
            {
                TQuestProgress? requested = normalizedProgress.Find(progress =>
                    string.Equals(progress.QuestId, existing.QuestId, StringComparison.Ordinal));
                if (requested == null || requested.State < existing.State || requested.CurrentCount < existing.CurrentCount)
                {
                    error = $"任务进度不能回滚：{existing.QuestId}";
                    return false;
                }
            }
        }

        normalizedProgress.Sort((left, right) => string.CompareOrdinal(left.QuestId, right.QuestId));
        error = "";
        return true;
    }
}
