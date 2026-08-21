using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// 本地游客存档服务：负责把单一游客账号的四个角色槽保存为 JSON。
/// 它只处理游客数据的读取与写入，不负责 UI、场景跳转或玩家运行时数值计算。
/// </summary>
public sealed class LocalGuestSaveService
{
    public const int CurrentSaveVersion = 1;
    public const int CharacterSlotCount = 4;

    private const string SaveDirectoryName = "Saves";
    private const string SaveFileName = "guest-save.json";

    private readonly string saveFilePath;
    private readonly string backupFilePath;
    private readonly string temporaryFilePath;

    private GuestSaveFile loadedSave;
    private long activeCharacterId;

    public string SaveFilePath => saveFilePath;
    public string BackupFilePath => backupFilePath;

    public LocalGuestSaveService(string customSaveFilePath = null)
    {
        saveFilePath = string.IsNullOrWhiteSpace(customSaveFilePath)
            ? Path.Combine(Application.persistentDataPath, SaveDirectoryName, SaveFileName)
            : customSaveFilePath;
        backupFilePath = saveFilePath + ".bak";
        temporaryFilePath = saveFilePath + ".tmp";
    }

    /// <summary>
    /// 加载游客档案。主文件损坏时尝试从备份恢复；两份文件都不可用时不会覆盖原文件。
    /// </summary>
    public bool TryLoad(out NCharacter[] characters, out string message)
    {
        activeCharacterId = 0;

        if (!File.Exists(saveFilePath) && !File.Exists(backupFilePath))
        {
            loadedSave = new GuestSaveFile();
            characters = Array.Empty<NCharacter>();
            message = "游客存档已加载。";
            return true;
        }

        if (TryReadAndValidate(saveFilePath, out GuestSaveFile primarySave, out string primaryError))
        {
            loadedSave = primarySave;
            characters = CloneCharacters(loadedSave.characters);
            message = "游客存档已加载。";
            return true;
        }

        if (TryReadAndValidate(backupFilePath, out GuestSaveFile backupSave, out string backupError))
        {
            if (!TryRestorePrimaryFromBackup(out string restoreError))
            {
                loadedSave = null;
                characters = Array.Empty<NCharacter>();
                message = $"游客备份存档有效，但恢复主文件失败：{restoreError}";
                return false;
            }

            loadedSave = backupSave;
            characters = CloneCharacters(loadedSave.characters);
            message = "游客主存档损坏，已从备份恢复。";
            return true;
        }

        loadedSave = null;
        characters = Array.Empty<NCharacter>();
        message = $"游客存档读取失败。主文件：{primaryError}；备份：{backupError}";
        return false;
    }

    /// <summary>
    /// 在指定槽位创建游客角色。与在线模式一致，已有槽位会被新角色覆盖并重置成长数据。
    /// </summary>
    public bool TryCreateCharacter(
        int slotIndex,
        string characterName,
        int classId,
        out NCharacter createdCharacter,
        out NCharacter[] characters,
        out string message)
    {
        createdCharacter = null;
        characters = CloneCharacters(loadedSave?.characters);

        if (!TryEnsureLoaded(out message))
        {
            return false;
        }

        string sanitizedName = characterName?.Trim() ?? "";
        if (slotIndex < 0 || slotIndex >= CharacterSlotCount)
        {
            message = "角色槽位必须是 0-3。";
            return false;
        }

        if (classId < 1 || classId > 4)
        {
            message = "职业不存在。";
            return false;
        }

        if (sanitizedName.Length < 1 || sanitizedName.Length > 32)
        {
            message = "角色名长度必须是 1-32 个字符。";
            return false;
        }

        GuestSaveFile candidate = CloneSave(loadedSave);
        candidate.characters.RemoveAll(character => character != null && character.slotIndex == slotIndex);

        var newCharacter = new NCharacter
        {
            // 游客只有四个固定槽位，使用槽位下标生成稳定正数 ID 即可满足现有会话校验。
            id = slotIndex + 1L,
            slotIndex = slotIndex,
            name = sanitizedName,
            classId = classId,
            level = 1,
            exp = 0,
            pendingAttributeUpgradeCount = 0,
            vaultDestroyedCount = 0,
            completedBossCount = 0
        };
        candidate.characters.Add(newCharacter);
        candidate.characters.Sort((left, right) => left.slotIndex.CompareTo(right.slotIndex));

        if (!TryWrite(candidate, out message))
        {
            return false;
        }

        loadedSave = candidate;
        activeCharacterId = 0;
        createdCharacter = newCharacter.Clone();
        characters = CloneCharacters(loadedSave.characters);
        message = "游客角色创建成功。";
        return true;
    }

    /// <summary>
    /// 从已经加载的游客档案中确认角色归属，并建立本次本地角色会话。
    /// </summary>
    public bool TryEnterCharacter(NCharacter selectedCharacter, out NCharacter enteredCharacter, out string message)
    {
        enteredCharacter = null;
        if (!TryEnsureLoaded(out message))
        {
            return false;
        }

        if (selectedCharacter == null || selectedCharacter.id <= 0)
        {
            message = "请选择有效角色。";
            return false;
        }

        NCharacter storedCharacter = loadedSave.characters.FirstOrDefault(character =>
            character != null &&
            character.id == selectedCharacter.id &&
            character.slotIndex == selectedCharacter.slotIndex);
        if (storedCharacter == null)
        {
            message = "游客角色不存在。";
            return false;
        }

        activeCharacterId = storedCharacter.id;
        enteredCharacter = storedCharacter.Clone();
        message = "游客角色进入成功。";
        return true;
    }

    /// <summary>
    /// 保存当前游客角色的长期成长数据。字段与在线角色存档保持一致。
    /// </summary>
    public bool TrySaveCharacterProgress(
        PlayerProgressSaveData progress,
        int vaultDestroyedCount,
        int completedBossCount,
        out NCharacter savedCharacter,
        out string message)
    {
        savedCharacter = null;
        if (!TryEnsureLoaded(out message))
        {
            return false;
        }

        NCharacter currentCharacter = loadedSave.characters.FirstOrDefault(character =>
            character != null && character.id == activeCharacterId);
        if (currentCharacter == null)
        {
            message = "请先进入游客角色。";
            return false;
        }

        if (!TryValidateProgress(
                progress,
                currentCharacter,
                vaultDestroyedCount,
                completedBossCount,
                out List<NAttributeUpgradeSave> normalizedUpgrades,
                out message))
        {
            return false;
        }

        GuestSaveFile candidate = CloneSave(loadedSave);
        int characterIndex = candidate.characters.FindIndex(character =>
            character != null && character.id == activeCharacterId);
        NCharacter updatedCharacter = candidate.characters[characterIndex];
        updatedCharacter.level = progress.Level;
        updatedCharacter.exp = progress.Exp;
        updatedCharacter.pendingAttributeUpgradeCount = progress.PendingAttributeUpgradeCount;
        updatedCharacter.vaultDestroyedCount = vaultDestroyedCount;
        updatedCharacter.completedBossCount = completedBossCount;
        updatedCharacter.attributeUpgrades = normalizedUpgrades;

        if (!TryWrite(candidate, out message))
        {
            return false;
        }

        loadedSave = candidate;
        savedCharacter = updatedCharacter.Clone();
        message = "游客角色进度已保存。";
        return true;
    }

    public void LeaveCharacter()
    {
        activeCharacterId = 0;
    }

    /// <summary>
    /// 退出登录时只清理内存会话，不删除电脑上的游客存档。
    /// </summary>
    public void ResetRuntimeSession()
    {
        activeCharacterId = 0;
        loadedSave = null;
    }

    private bool TryEnsureLoaded(out string message)
    {
        if (loadedSave != null)
        {
            message = "";
            return true;
        }

        message = "游客存档尚未加载。";
        return false;
    }

    private bool TryReadAndValidate(string path, out GuestSaveFile save, out string error)
    {
        save = null;
        if (!File.Exists(path))
        {
            error = "文件不存在";
            return false;
        }

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "文件内容为空";
                return false;
            }

            GuestSaveFile parsed = JsonUtility.FromJson<GuestSaveFile>(json);
            if (!TryValidateSaveFile(parsed, out error))
            {
                return false;
            }

            save = CloneSave(parsed);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryValidateSaveFile(GuestSaveFile save, out string error)
    {
        if (save == null)
        {
            error = "JSON 结构无效";
            return false;
        }

        if (save.version != CurrentSaveVersion)
        {
            error = $"不支持的存档版本 {save.version}";
            return false;
        }

        if (save.characters == null || save.characters.Count > CharacterSlotCount)
        {
            error = "角色槽位数量无效";
            return false;
        }

        var usedSlots = new HashSet<int>();
        foreach (NCharacter character in save.characters)
        {
            if (character == null ||
                character.slotIndex < 0 || character.slotIndex >= CharacterSlotCount ||
                character.id != character.slotIndex + 1L ||
                !usedSlots.Add(character.slotIndex))
            {
                error = "角色槽位或本地角色 ID 无效";
                return false;
            }

            if (string.IsNullOrWhiteSpace(character.name) || character.name.Trim().Length > 32 ||
                character.classId < 1 || character.classId > 4 ||
                character.level < 1 || character.level > 999 || character.exp < 0 ||
                character.pendingAttributeUpgradeCount < 0 ||
                character.vaultDestroyedCount < 0 || character.completedBossCount < 0 ||
                character.completedBossCount > character.vaultDestroyedCount)
            {
                error = $"槽位 {character.slotIndex} 的角色数据无效";
                return false;
            }

            character.attributeUpgrades = character.attributeUpgrades ?? new List<NAttributeUpgradeSave>();
            if (!TryValidateUpgradeList(
                    character.attributeUpgrades,
                    character.level,
                    character.pendingAttributeUpgradeCount,
                    out _,
                    out error))
            {
                error = $"槽位 {character.slotIndex}：{error}";
                return false;
            }
        }

        error = "";
        return true;
    }

    private static bool TryValidateProgress(
        PlayerProgressSaveData progress,
        NCharacter current,
        int vaultDestroyedCount,
        int completedBossCount,
        out List<NAttributeUpgradeSave> normalizedUpgrades,
        out string error)
    {
        normalizedUpgrades = new List<NAttributeUpgradeSave>();
        if (progress == null || progress.Level < 1 || progress.Level > 999 || progress.Exp < 0)
        {
            error = "等级或经验数据非法。";
            return false;
        }

        if (progress.PendingAttributeUpgradeCount < 0 ||
            vaultDestroyedCount < 0 || completedBossCount < 0 ||
            completedBossCount > vaultDestroyedCount)
        {
            error = "角色进度数据非法。";
            return false;
        }

        if (progress.Level < current.level ||
            (progress.Level == current.level && progress.Exp < current.exp) ||
            vaultDestroyedCount < current.vaultDestroyedCount ||
            completedBossCount < current.completedBossCount)
        {
            error = "不能用旧进度覆盖游客存档。";
            return false;
        }

        if (!TryValidateUpgradeList(
                progress.AttributeUpgrades,
                progress.Level,
                progress.PendingAttributeUpgradeCount,
                out normalizedUpgrades,
                out error))
        {
            return false;
        }

        error = "";
        return true;
    }

    private static bool TryValidateUpgradeList(
        IEnumerable<NAttributeUpgradeSave> upgrades,
        int level,
        int pendingUpgradeCount,
        out List<NAttributeUpgradeSave> normalizedUpgrades,
        out string error)
    {
        normalizedUpgrades = new List<NAttributeUpgradeSave>();
        var usedTypes = new HashSet<int>();
        long totalUpgradeCount = pendingUpgradeCount;

        if (upgrades != null)
        {
            foreach (NAttributeUpgradeSave upgrade in upgrades)
            {
                if (upgrade == null)
                {
                    continue;
                }

                if (upgrade.attributeType < 1 || upgrade.attributeType > 8 ||
                    upgrade.upgradeCount < 0 || !usedTypes.Add(upgrade.attributeType))
                {
                    error = "属性强化数据非法或存在重复类型。";
                    return false;
                }

                totalUpgradeCount += upgrade.upgradeCount;
                if (upgrade.upgradeCount > 0)
                {
                    normalizedUpgrades.Add(new NAttributeUpgradeSave
                    {
                        attributeType = upgrade.attributeType,
                        upgradeCount = upgrade.upgradeCount
                    });
                }
            }
        }

        if (totalUpgradeCount > Math.Max(0, level - 1))
        {
            error = "属性强化次数超过当前等级可获得数量。";
            return false;
        }

        normalizedUpgrades.Sort((left, right) => left.attributeType.CompareTo(right.attributeType));
        error = "";
        return true;
    }

    private bool TryWrite(GuestSaveFile candidate, out string message)
    {
        try
        {
            string directory = Path.GetDirectoryName(saveFilePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                message = "游客存档路径无效。";
                return false;
            }

            Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(candidate, true);
            File.WriteAllText(temporaryFilePath, json, new UTF8Encoding(false));

            if (!File.Exists(saveFilePath))
            {
                File.Move(temporaryFilePath, saveFilePath);
                message = "";
                return true;
            }

            try
            {
                File.Replace(temporaryFilePath, saveFilePath, backupFilePath, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceWithPortableFallback();
            }

            message = "";
            return true;
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile();
            message = $"游客存档写入失败：{exception.Message}";
            return false;
        }
    }

    private void ReplaceWithPortableFallback()
    {
        File.Copy(saveFilePath, backupFilePath, true);
        File.Delete(saveFilePath);
        File.Move(temporaryFilePath, saveFilePath);
    }

    private bool TryRestorePrimaryFromBackup(out string error)
    {
        try
        {
            string directory = Path.GetDirectoryName(saveFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(backupFilePath, temporaryFilePath, true);
            File.Copy(temporaryFilePath, saveFilePath, true);
            File.Delete(temporaryFilePath);
            error = "";
            return true;
        }
        catch (Exception exception)
        {
            TryDeleteTemporaryFile();
            error = exception.Message;
            return false;
        }
    }

    private void TryDeleteTemporaryFile()
    {
        try
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }
        }
        catch
        {
            // 临时文件清理失败不能覆盖真正的存档错误，下一次写入时会直接覆盖该临时文件。
        }
    }

    private static GuestSaveFile CloneSave(GuestSaveFile source)
    {
        var copy = new GuestSaveFile
        {
            version = source != null ? source.version : CurrentSaveVersion,
            characters = new List<NCharacter>()
        };

        if (source?.characters != null)
        {
            foreach (NCharacter character in source.characters)
            {
                if (character != null)
                {
                    copy.characters.Add(character.Clone());
                }
            }
        }

        return copy;
    }

    private static NCharacter[] CloneCharacters(IEnumerable<NCharacter> characters)
    {
        return characters == null
            ? Array.Empty<NCharacter>()
            : characters.Where(character => character != null).Select(character => character.Clone()).ToArray();
    }

    [Serializable]
    private sealed class GuestSaveFile
    {
        public int version = CurrentSaveVersion;
        public List<NCharacter> characters = new List<NCharacter>();
    }
}
