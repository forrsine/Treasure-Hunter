using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家单个技能的跨场景快照。
/// 只保存运行时需要延续的数据：技能 ID、等级和剩余冷却。
/// </summary>
[Serializable]
public sealed class PlayerSkillTransferData
{
    public int skillId;
    public int level;
    public float cooldownRemaining;

    public PlayerSkillTransferData(int skillId, int level, float cooldownRemaining)
    {
        this.skillId = skillId;
        this.level = Mathf.Max(1, level);
        this.cooldownRemaining = Mathf.Max(0f, cooldownRemaining);
    }
}

/// <summary>
/// 玩家进入新战斗场景时使用的运行时快照。
/// 它保存“数据状态”，不直接保存玩家 GameObject，避免跨场景携带旧摄像机、UI、碰撞父节点等引用。
/// </summary>
public sealed class PlayerSceneTransferSnapshot
{
    public PlayerSceneTransferSnapshot(
        NCharacter characterSave,
        CharacterDefine characterDefine,
        PlayerStatsSnapshot stats,
        List<PlayerSkillTransferData> learnedSkills,
        int pendingSkillSelectionCount)
    {
        CharacterSave = CloneCharacterSave(characterSave);
        CharacterDefine = characterDefine;
        Stats = stats;
        LearnedSkills = CloneSkillList(learnedSkills);
        PendingSkillSelectionCount = Mathf.Max(0, pendingSkillSelectionCount);
    }

    public NCharacter CharacterSave { get; }
    public CharacterDefine CharacterDefine { get; }
    public PlayerStatsSnapshot Stats { get; }
    public List<PlayerSkillTransferData> LearnedSkills { get; }
    public int PendingSkillSelectionCount { get; }

    public int ClassId
    {
        get
        {
            if (CharacterSave != null && CharacterSave.classId > 0)
            {
                return CharacterSave.classId;
            }

            return CharacterDefine != null ? CharacterDefine.classId : 0;
        }
    }

    public bool IsValid => ClassId > 0;

    /// <summary>
    /// 给角色生成器使用的存档副本。
    /// 这里会把等级和经验同步成快照值，避免 Boss 房间又拿旧存档等级初始化。
    /// </summary>
    public NCharacter CreateCharacterSaveCopy()
    {
        NCharacter save = CloneCharacterSave(CharacterSave);
        if (save == null)
        {
            save = new NCharacter
            {
                id = 0,
                slotIndex = -1,
                name = CharacterDefine != null ? CharacterDefine.name : "Player",
                classId = ClassId
            };
        }

        save.classId = save.classId > 0 ? save.classId : ClassId;
        save.level = Mathf.Max(1, Stats.Level);
        save.exp = Mathf.Max(0, Stats.CurrentExp);
        return save;
    }

    private static NCharacter CloneCharacterSave(NCharacter source)
    {
        if (source == null)
        {
            return null;
        }

        return new NCharacter
        {
            id = source.id,
            slotIndex = source.slotIndex,
            name = source.name,
            classId = source.classId,
            level = source.level,
            exp = source.exp
        };
    }

    private static List<PlayerSkillTransferData> CloneSkillList(List<PlayerSkillTransferData> source)
    {
        List<PlayerSkillTransferData> result = new List<PlayerSkillTransferData>();
        if (source == null)
        {
            return result;
        }

        for (int i = 0; i < source.Count; i++)
        {
            PlayerSkillTransferData skill = source[i];
            if (skill == null || skill.skillId <= 0)
            {
                continue;
            }

            result.Add(new PlayerSkillTransferData(
                skill.skillId,
                skill.level,
                skill.cooldownRemaining));
        }

        return result;
    }
}

/// <summary>
/// 玩家跨场景传递状态。
/// Boss 传送门在切场景前写入快照，Boss 房间角色生成器读取并恢复。
/// </summary>
public static class PlayerSceneTransferState
{
    private static PlayerSceneTransferSnapshot pendingSnapshot;

    public static bool HasPendingSnapshot => pendingSnapshot != null;

    /// <summary>
    /// 从当前玩家身上捕获角色、属性和技能状态。
    /// </summary>
    public static bool TryCaptureFrom(PlayerRuntimeController player)
    {
        if (player == null)
        {
            return false;
        }

        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        PlayerModel playerModel = architecture.GetModel<PlayerModel>();
        PlayerSkillModel skillModel = architecture.GetModel<PlayerSkillModel>();
        if (playerModel == null)
        {
            return false;
        }

        PlayerStatsSnapshot stats = playerModel.CreateSnapshot();
        CharacterDefine define = player.EntryDefine ?? playerModel.CharacterDefine;
        NCharacter save = CloneCharacterSave(player.EntrySave ?? playerModel.CharacterSave);

        if (save == null && define != null)
        {
            save = new NCharacter
            {
                id = 0,
                slotIndex = -1,
                name = define.name,
                classId = define.classId
            };
        }

        if (save == null)
        {
            return false;
        }

        if (save.classId <= 0 && define != null)
        {
            save.classId = define.classId;
        }

        save.level = Mathf.Max(1, stats.Level);
        save.exp = Mathf.Max(0, stats.CurrentExp);

        pendingSnapshot = new PlayerSceneTransferSnapshot(
            save,
            define,
            stats,
            skillModel != null ? CreateSkillSnapshot(skillModel) : new List<PlayerSkillTransferData>(),
            skillModel != null ? skillModel.PendingSkillSelectionCount : 0);

        // 同步一份轻量角色状态，作为快照恢复失败时的兜底，至少保证职业不会退回 fallbackClassId。
        SelectedCharacterState.SetCharacter(pendingSnapshot.CreateCharacterSaveCopy());
        return pendingSnapshot.IsValid;
    }

    public static bool TryConsume(out PlayerSceneTransferSnapshot snapshot)
    {
        snapshot = pendingSnapshot;
        pendingSnapshot = null;
        return snapshot != null && snapshot.IsValid;
    }

    public static void Clear()
    {
        pendingSnapshot = null;
    }

    private static NCharacter CloneCharacterSave(NCharacter source)
    {
        if (source == null)
        {
            return null;
        }

        return new NCharacter
        {
            id = source.id,
            slotIndex = source.slotIndex,
            name = source.name,
            classId = source.classId,
            level = source.level,
            exp = source.exp
        };
    }

    private static List<PlayerSkillTransferData> CreateSkillSnapshot(PlayerSkillModel skillModel)
    {
        List<PlayerSkillTransferData> result = new List<PlayerSkillTransferData>();
        if (skillModel == null)
        {
            return result;
        }

        List<PlayerSkillRuntimeData> learnedSkills = skillModel.GetLearnedSkills();
        for (int i = 0; i < learnedSkills.Count; i++)
        {
            PlayerSkillRuntimeData skill = learnedSkills[i];
            if (skill == null || skill.skillId <= 0)
            {
                continue;
            }

            result.Add(new PlayerSkillTransferData(
                skill.skillId,
                skill.level,
                skill.cooldownRemaining));
        }

        return result;
    }
}
