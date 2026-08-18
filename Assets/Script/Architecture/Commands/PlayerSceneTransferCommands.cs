using QFramework;
using UnityEngine;

/// <summary>
/// 从跨场景快照恢复玩家运行时数据。
/// 入口统一做成 Command，避免场景脚本直接修改 PlayerModel / PlayerSkillModel 的内部数据。
/// </summary>
public sealed class RestorePlayerSceneTransferSnapshotCommand : AbstractCommand
{
    private readonly PlayerSceneTransferSnapshot snapshot;

    public RestorePlayerSceneTransferSnapshotCommand(PlayerSceneTransferSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    protected override void OnExecute()
    {
        if (snapshot == null || !snapshot.IsValid)
        {
            return;
        }

        PlayerModel playerModel = this.GetModel<PlayerModel>();
        if (playerModel == null)
        {
            return;
        }

        CharacterDefine define = ResolveCharacterDefine();
        NCharacter save = snapshot.CreateCharacterSaveCopy();

        playerModel.RestoreFromSceneTransferSnapshot(save, define, snapshot.Stats);
        this.GetSystem<PlayerCombatSystem>().ResetRuntimeBuffers();
        RestoreSkillSnapshot();

        PlayerRuntimeStats stats = playerModel.MutableStats;
        stats.NotifyStatsChanged();
        stats.NotifyPendingUpgradeSelectionsChanged();

        this.SendEvent(new PlayerStatsChangedEvent());
        this.SendEvent(new PlayerUpgradeQueueChangedEvent(stats.PendingUpgradeSelectionCount));
    }

    private CharacterDefine ResolveCharacterDefine()
    {
        if (snapshot.CharacterDefine != null)
        {
            return snapshot.CharacterDefine;
        }

        if (CharacterDataManager.Instance == null)
        {
            return null;
        }

        return CharacterDataManager.Instance.GetCharacter(snapshot.ClassId);
    }

    /// <summary>
    /// 恢复技能数据。
    /// PlayerSkillModel 已经提供学习、升级、查询运行时数据等 public 入口，这里复用它们而不是绕过边界改私有字典。
    /// </summary>
    private void RestoreSkillSnapshot()
    {
        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();
        if (skillModel == null)
        {
            return;
        }

        skillModel.Reset();

        for (int i = 0; i < snapshot.LearnedSkills.Count; i++)
        {
            PlayerSkillTransferData skill = snapshot.LearnedSkills[i];
            if (skill == null || skill.skillId <= 0)
            {
                continue;
            }

            if (!skillModel.HasSkill(skill.skillId) && !skillModel.LearnSkill(skill.skillId))
            {
                continue;
            }

            while (skillModel.GetSkillLevel(skill.skillId) < Mathf.Max(1, skill.level))
            {
                if (!skillModel.UpgradeSkill(skill.skillId))
                {
                    break;
                }
            }

            PlayerSkillRuntimeData runtimeData = skillModel.GetSkillRuntimeData(skill.skillId);
            if (runtimeData != null)
            {
                runtimeData.cooldownRemaining = Mathf.Max(0f, skill.cooldownRemaining);
                this.SendEvent(new PlayerSkillChangedEvent(runtimeData.skillId, runtimeData.level));
            }
        }

        for (int i = 0; i < snapshot.PendingSkillSelectionCount; i++)
        {
            skillModel.AddPendingSkillSelection();
        }

        this.SendEvent(new PlayerSkillSelectionQueueChangedEvent(skillModel.PendingSkillSelectionCount));
    }
}
