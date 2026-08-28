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
    public const int CurrentSaveVersion = 5;
    private const int MinimumSupportedSaveVersion = 1;
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
        bool resetAfterDeath,
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
                resetAfterDeath,
                out List<NAttributeUpgradeSave> normalizedUpgrades,
                out List<NInventoryItemSave> normalizedInventory,
                out List<NEquippedItemSave> normalizedEquipment,
                out List<string> normalizedPurchases,
                out List<NQuestProgressSave> normalizedQuestProgress,
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
        updatedCharacter.inventoryItems = normalizedInventory;
        updatedCharacter.equippedItems = normalizedEquipment;
        updatedCharacter.gold = progress.Gold;
        updatedCharacter.merchantIntroCompleted = progress.MerchantIntroCompleted;
        updatedCharacter.purchasedLimitedShopItemIds = normalizedPurchases;
        updatedCharacter.questProgress = normalizedQuestProgress;

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

        if (save.version < MinimumSupportedSaveVersion || save.version > CurrentSaveVersion)
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

            character.inventoryItems = character.inventoryItems ?? new List<NInventoryItemSave>();
            if (!TryValidateInventoryList(
                    character.inventoryItems,
                    false,
                    true,
                    out List<NInventoryItemSave> normalizedInventory,
                    out error))
            {
                error = $"槽位 {character.slotIndex}：{error}";
                return false;
            }

            character.inventoryItems = normalizedInventory;
            // v1/v2 没有装备字段，JsonUtility 会给出 null；迁移为空装备栏。
            character.equippedItems = character.equippedItems ?? new List<NEquippedItemSave>();
            if (!TryValidateEquipmentList(character.equippedItems, true, out List<NEquippedItemSave> normalizedEquipment, out error))
            {
                error = $"槽位 {character.slotIndex}：{error}";
                return false;
            }
            character.equippedItems = normalizedEquipment;

            // v1-v3 没有经济与商店字段，默认迁移为 0 金币、未对话、空限购记录。
            character.purchasedLimitedShopItemIds = character.purchasedLimitedShopItemIds ?? new List<string>();
            if (character.gold < 0L || character.gold > EconomySystem.MaxGold)
            {
                error = $"槽位 {character.slotIndex}：金币超出允许范围。";
                return false;
            }

            if (!TryValidateShopProgress(
                    character.merchantIntroCompleted,
                    character.purchasedLimitedShopItemIds,
                    null,
                    true,
                    out List<string> normalizedPurchases,
                    out error))
            {
                error = $"槽位 {character.slotIndex}：{error}";
                return false;
            }
            character.purchasedLimitedShopItemIds = normalizedPurchases;

            // v1-v4 没有任务字段，空集合代表所有配置任务都处于可接取状态。
            character.questProgress = character.questProgress ?? new List<NQuestProgressSave>();
            if (!TryValidateQuestProgress(
                    character.questProgress,
                    null,
                    true,
                    out List<NQuestProgressSave> normalizedQuestProgress,
                    out error))
            {
                error = $"槽位 {character.slotIndex}：{error}";
                return false;
            }
            character.questProgress = normalizedQuestProgress;
        }

        // 旧版本缺失字段按空集合迁移；下一次成功保存时写回当前版本。
        save.version = CurrentSaveVersion;
        error = "";
        return true;
    }

    private static bool TryValidateProgress(
        PlayerProgressSaveData progress,
        NCharacter current,
        int vaultDestroyedCount,
        int completedBossCount,
        bool resetAfterDeath,
        out List<NAttributeUpgradeSave> normalizedUpgrades,
        out List<NInventoryItemSave> normalizedInventory,
        out List<NEquippedItemSave> normalizedEquipment,
        out List<string> normalizedPurchases,
        out List<NQuestProgressSave> normalizedQuestProgress,
        out string error)
    {
        normalizedUpgrades = new List<NAttributeUpgradeSave>();
        normalizedInventory = new List<NInventoryItemSave>();
        normalizedEquipment = new List<NEquippedItemSave>();
        normalizedPurchases = new List<string>();
        normalizedQuestProgress = new List<NQuestProgressSave>();

        if (progress == null)
        {
            error = "角色进度不能为空。";
            return false;
        }

        if (!TryValidateInventoryList(
                progress.InventoryItems,
                resetAfterDeath,
                false,
                out normalizedInventory,
                out error))
        {
            return false;
        }

        if (!TryValidateEquipmentList(progress.EquippedItems, false, out normalizedEquipment, out error))
        {
            return false;
        }

        if (progress.Gold < 0L || progress.Gold > EconomySystem.MaxGold)
        {
            error = "金币超出允许范围。";
            return false;
        }

        if (!TryValidateShopProgress(
                progress.MerchantIntroCompleted,
                progress.PurchasedLimitedShopItemIds,
                current,
                false,
                out normalizedPurchases,
                out error))
        {
            return false;
        }

        if (!TryValidateQuestProgress(
                progress.QuestProgress,
                current,
                false,
                out normalizedQuestProgress,
                out error))
        {
            return false;
        }

        if (resetAfterDeath)
        {
            bool isExactDeathReset = progress.Level == 1 &&
                progress.Exp == 0 &&
                progress.PendingAttributeUpgradeCount == 0 &&
                vaultDestroyedCount == 0 &&
                completedBossCount == 0 &&
                progress.AttributeUpgrades.Count == 0;
            if (!isExactDeathReset)
            {
                error = "死亡重置数据必须全部归零。";
                return false;
            }

            error = "";
            return true;
        }

        if (progress.Level < 1 || progress.Level > 999 || progress.Exp < 0)
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

    /// <summary>
    /// 校验限购集合只包含当前目录里的限购装备，并保证客户端不能把已经购买的记录回滚。
    /// 加载旧存档时允许跳过已经从配置中删除的商品，避免整个角色无法进入。
    /// </summary>
    private static bool TryValidateShopProgress(
        bool introCompleted,
        IEnumerable<string> purchasedItemIds,
        NCharacter current,
        bool skipUnknownItems,
        out List<string> normalizedPurchases,
        out string error)
    {
        normalizedPurchases = new List<string>();
        ShopCatalog catalog = Resources.Load<ShopCatalog>(ShopCatalog.ResourcesPath);
        if (catalog == null)
        {
            error = "商店目录未加载。";
            return false;
        }

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        if (purchasedItemIds != null)
        {
            foreach (string itemId in purchasedItemIds)
            {
                if (string.IsNullOrWhiteSpace(itemId) || !usedIds.Add(itemId))
                {
                    error = "限购商品记录为空或重复。";
                    return false;
                }

                if (!catalog.TryGetEntry(itemId, out ShopCatalogEntry entry) || !entry.LimitedOncePerCharacter)
                {
                    if (skipUnknownItems)
                    {
                        Debug.LogWarning($"游客存档中的限购商品已不存在，加载时跳过：{itemId}");
                        continue;
                    }

                    error = $"限购商品不在白名单中：{itemId}";
                    return false;
                }

                normalizedPurchases.Add(itemId);
            }
        }

        if (current != null)
        {
            if (current.merchantIntroCompleted && !introCompleted)
            {
                error = "商人首次对话状态不能回滚。";
                return false;
            }

            if (current.purchasedLimitedShopItemIds != null)
            {
                for (int i = 0; i < current.purchasedLimitedShopItemIds.Count; i++)
                {
                    if (!usedIds.Contains(current.purchasedLimitedShopItemIds[i]))
                    {
                        error = "已购买的限购商品不能回滚。";
                        return false;
                    }
                }
            }
        }

        normalizedPurchases.Sort(StringComparer.Ordinal);
        error = "";
        return true;
    }

    /// <summary>
    /// 校验任务白名单、完成条件和单向进度。加载旧档时可跳过已从目录删除的任务，
    /// 正常保存则禁止未知 ID、重复记录、数量倒退或状态回滚。
    /// </summary>
    private static bool TryValidateQuestProgress(
        IEnumerable<NQuestProgressSave> requestedProgress,
        NCharacter current,
        bool skipUnknownQuests,
        out List<NQuestProgressSave> normalizedProgress,
        out string error)
    {
        normalizedProgress = new List<NQuestProgressSave>();
        QuestCatalog catalog = Resources.Load<QuestCatalog>(QuestCatalog.ResourcesPath);
        if (catalog == null)
        {
            error = "任务目录未加载。";
            return false;
        }

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        if (requestedProgress != null)
        {
            foreach (NQuestProgressSave saved in requestedProgress)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.questId) || !usedIds.Add(saved.questId))
                {
                    error = "任务进度包含空 ID 或重复记录。";
                    return false;
                }

                if (!catalog.TryGetQuest(saved.questId, out QuestDefinition definition))
                {
                    if (skipUnknownQuests)
                    {
                        Debug.LogWarning($"游客存档中的任务已不存在，加载时跳过：{saved.questId}");
                        continue;
                    }

                    error = $"任务不在白名单中：{saved.questId}";
                    return false;
                }

                if (!TryNormalizeQuestProgress(saved, definition, out NQuestProgressSave normalized, out error))
                {
                    return false;
                }

                if (normalized != null)
                {
                    normalizedProgress.Add(normalized);
                }
            }
        }

        if (current != null && current.questProgress != null)
        {
            for (int i = 0; i < current.questProgress.Count; i++)
            {
                NQuestProgressSave existing = current.questProgress[i];
                if (existing == null || !catalog.TryGetQuest(existing.questId, out _))
                {
                    continue;
                }

                NQuestProgressSave requested = normalizedProgress.Find(item =>
                    string.Equals(item.questId, existing.questId, StringComparison.Ordinal));
                if (requested == null || requested.state < existing.state || requested.currentCount < existing.currentCount)
                {
                    error = $"任务进度不能回滚：{existing.questId}";
                    return false;
                }
            }
        }

        normalizedProgress.Sort((left, right) => string.CompareOrdinal(left.questId, right.questId));
        error = "";
        return true;
    }

    private static bool TryNormalizeQuestProgress(
        NQuestProgressSave saved,
        QuestDefinition definition,
        out NQuestProgressSave normalized,
        out string error)
    {
        normalized = null;
        if (!Enum.IsDefined(typeof(QuestState), saved.state) || saved.currentCount < 0)
        {
            error = $"任务状态或数量非法：{saved.questId}";
            return false;
        }

        QuestState state = (QuestState)saved.state;
        if (state == QuestState.Available)
        {
            if (saved.currentCount != 0)
            {
                error = $"未接取任务不能拥有进度：{saved.questId}";
                return false;
            }

            error = "";
            return true;
        }

        bool validActive = state == QuestState.Active && saved.currentCount < definition.RequiredCount;
        bool validCompleted = (state == QuestState.ReadyToClaim || state == QuestState.Claimed) &&
                              saved.currentCount == definition.RequiredCount;
        if (!validActive && !validCompleted)
        {
            error = $"任务状态与完成数量不一致：{saved.questId}";
            return false;
        }

        normalized = saved.Clone();
        error = "";
        return true;
    }

    /// <summary>
    /// 校验并规范化游客背包。游客与在线模式使用相同的格子、物品白名单和死亡保留规则。
    /// </summary>
    private static bool TryValidateInventoryList(
        IEnumerable<NInventoryItemSave> inventoryItems,
        bool resetAfterDeath,
        bool skipUnknownItems,
        out List<NInventoryItemSave> normalizedInventory,
        out string error)
    {
        normalizedInventory = new List<NInventoryItemSave>();
        InventoryDatabase database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);
        if (database == null)
        {
            error = "背包数据库未加载。";
            return false;
        }

        var usedSlots = new HashSet<int>();
        if (inventoryItems != null)
        {
            foreach (NInventoryItemSave savedItem in inventoryItems)
            {
                if (savedItem == null ||
                    savedItem.slotIndex < 0 || savedItem.slotIndex >= database.Capacity ||
                    !usedSlots.Add(savedItem.slotIndex) ||
                    savedItem.count <= 0)
                {
                    error = "背包格子、物品ID或数量非法。";
                    return false;
                }

                if (!database.TryGetItemById(savedItem.itemId, out InventoryItemDefinition item))
                {
                    if (skipUnknownItems)
                    {
                        Debug.LogWarning($"游客存档中的物品配置已不存在，加载时跳过：{savedItem.itemId}");
                        continue;
                    }

                    error = "背包格子、物品ID或数量非法。";
                    return false;
                }

                if (savedItem.count > item.MaxStack)
                {
                    error = "背包格子、物品ID或数量非法。";
                    return false;
                }

                if (resetAfterDeath && item.Category == InventoryItemCategory.Consumable)
                {
                    error = "死亡重置存档不能保留消耗品。";
                    return false;
                }

                normalizedInventory.Add(savedItem.Clone());
            }
        }

        normalizedInventory.Sort((left, right) => left.slotIndex.CompareTo(right.slotIndex));
        error = "";
        return true;
    }

    private static bool TryValidateEquipmentList(
        IEnumerable<NEquippedItemSave> equippedItems,
        bool skipUnknownItems,
        out List<NEquippedItemSave> normalizedEquipment,
        out string error)
    {
        normalizedEquipment = new List<NEquippedItemSave>();
        InventoryDatabase database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);
        if (database == null)
        {
            error = "背包数据库未加载。";
            return false;
        }

        var usedSlots = new HashSet<int>();
        if (equippedItems != null)
        {
            foreach (NEquippedItemSave savedItem in equippedItems)
            {
                if (savedItem == null || savedItem.equipmentSlot < 1 || savedItem.equipmentSlot > 6 ||
                    !usedSlots.Add(savedItem.equipmentSlot))
                {
                    error = "装备槽位非法或重复。";
                    return false;
                }

                if (!database.TryGetItemById(savedItem.itemId, out InventoryItemDefinition item))
                {
                    if (skipUnknownItems)
                    {
                        Debug.LogWarning($"游客存档中的装备配置已不存在，加载时跳过：{savedItem.itemId}");
                        continue;
                    }
                    error = "装备物品ID非法。";
                    return false;
                }

                if (!item.IsEquipment || (int)item.EquipmentSlot != savedItem.equipmentSlot)
                {
                    error = "装备物品与槽位不匹配。";
                    return false;
                }

                normalizedEquipment.Add(savedItem.Clone());
            }
        }

        normalizedEquipment.Sort((left, right) => left.equipmentSlot.CompareTo(right.equipmentSlot));
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
