using System;
using UnityEngine;

/// <summary>
/// 任务静态目录：集中保存任务策划数据，角色运行时进度由 QuestModel 单独维护。
/// 这种拆分让修改击杀数量或奖励时不需要改代码，也不会污染共享 ScriptableObject。
/// </summary>
[CreateAssetMenu(fileName = "QuestCatalog", menuName = "Treasure Hunter/Quest/Catalog")]
public sealed class QuestCatalog : ScriptableObject
{
    public const string ResourcesPath = "Data/Quest/QuestCatalog";

    [SerializeField] private QuestDefinition[] entries = Array.Empty<QuestDefinition>();

    public QuestDefinition[] Entries => entries;

    public bool TryGetQuest(string questId, out QuestDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(questId) || entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            QuestDefinition candidate = entries[i];
            if (candidate != null && string.Equals(candidate.QuestId, questId, StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }
}
