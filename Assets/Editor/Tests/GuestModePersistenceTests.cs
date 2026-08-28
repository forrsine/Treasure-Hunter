#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游客模式 EditMode 测试：保护本地 JSON 往返、槽位覆盖、备份恢复和登录场景按钮绑定。
/// 每个测试使用独立临时目录，不会读写玩家电脑上的真实游客存档。
/// </summary>
public sealed class GuestModePersistenceTests
{
    private string temporaryDirectory;
    private string saveFilePath;

    [SetUp]
    public void SetUp()
    {
        temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "TreasureHunterGuestModeTests",
            Guid.NewGuid().ToString("N"));
        saveFilePath = Path.Combine(temporaryDirectory, "guest-save.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(temporaryDirectory) && Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, true);
        }
    }

    [Test]
    public void FirstLoad_ReturnsEmptyGuestAccountWithoutCreatingNetworkData()
    {
        var service = new LocalGuestSaveService(saveFilePath);

        bool success = service.TryLoad(out NCharacter[] characters, out string message);

        Assert.That(success, Is.True, message);
        Assert.That(characters, Is.Empty);
        Assert.That(File.Exists(saveFilePath), Is.False, "空游客档会延迟到首次实际写入时创建文件。");
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void LegacySaveWithoutQuestProgress_MigratesToVersionFiveDefaults(int legacyVersion)
    {
        Directory.CreateDirectory(temporaryDirectory);
        File.WriteAllText(
            saveFilePath,
            $"{{\"version\":{legacyVersion},\"characters\":[{{\"id\":1,\"slotIndex\":0," +
            "\"name\":\"旧游客\",\"classId\":1,\"level\":1,\"exp\":0," +
            "\"pendingAttributeUpgradeCount\":0,\"vaultDestroyedCount\":0," +
            "\"completedBossCount\":0,\"attributeUpgrades\":[]}]}" );

        var service = new LocalGuestSaveService(saveFilePath);
        bool success = service.TryLoad(out NCharacter[] characters, out string message);

        Assert.That(success, Is.True, message);
        Assert.That(characters, Has.Length.EqualTo(1));
        Assert.That(characters[0].inventoryItems, Is.Not.Null.And.Empty);
        Assert.That(characters[0].equippedItems, Is.Not.Null.And.Empty);
        Assert.That(characters[0].gold, Is.Zero);
        Assert.That(characters[0].merchantIntroCompleted, Is.False);
        Assert.That(characters[0].purchasedLimitedShopItemIds, Is.Not.Null.And.Empty);
        Assert.That(characters[0].questProgress, Is.Not.Null.And.Empty);
    }

    [Test]
    public void CreateCharacter_ReloadPreservesSlotAndIdentity()
    {
        var service = CreateLoadedService();

        bool created = service.TryCreateCharacter(
            2,
            "游客弓手",
            3,
            out NCharacter character,
            out NCharacter[] characters,
            out string message);

        Assert.That(created, Is.True, message);
        Assert.That(character.id, Is.EqualTo(3));
        Assert.That(character.slotIndex, Is.EqualTo(2));
        Assert.That(characters, Has.Length.EqualTo(1));
        Assert.That(File.Exists(saveFilePath), Is.True);

        var reloadedService = new LocalGuestSaveService(saveFilePath);
        Assert.That(reloadedService.TryLoad(out NCharacter[] reloaded, out message), Is.True, message);
        Assert.That(reloaded, Has.Length.EqualTo(1));
        Assert.That(reloaded[0].name, Is.EqualTo("游客弓手"));
        Assert.That(reloaded[0].classId, Is.EqualTo(3));
    }

    [Test]
    public void SaveProgress_ReloadPreservesEveryPersistedCharacterField()
    {
        var service = CreateLoadedService();
        CreateCharacter(service, 0, "成长测试", 1);
        Assert.That(
            service.TryEnterCharacter(
                new NCharacter { id = 1, slotIndex = 0 },
                out _,
                out string enterMessage),
            Is.True,
            enterMessage);

        var progress = new PlayerProgressSaveData
        {
            Level = 5,
            Exp = 37,
            PendingAttributeUpgradeCount = 1,
            Gold = 345,
            MerchantIntroCompleted = true
        };
        progress.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.AttackPower,
            upgradeCount = 2
        });
        progress.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.MaxHp,
            upgradeCount = 1
        });
        progress.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 2,
            itemId = "healing_potion",
            count = 3
        });
        progress.PurchasedLimitedShopItemIds.Add("boss_iron_war_axe");
        progress.QuestProgress.Add(new NQuestProgressSave
        {
            questId = "hunt_red_slime",
            state = (int)QuestState.Active,
            currentCount = 3
        });
        progress.EquippedItems.Add(new NEquippedItemSave
        {
            equipmentSlot = (int)EquipmentSlotType.Weapon,
            itemId = "boss_iron_war_axe"
        });
        progress.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 7,
            itemId = "spider_king_core",
            count = 2
        });

        bool saved = service.TrySaveCharacterProgress(
            progress,
            8,
            2,
            false,
            out NCharacter savedCharacter,
            out string saveMessage);

        Assert.That(saved, Is.True, saveMessage);
        Assert.That(savedCharacter.level, Is.EqualTo(5));
        Assert.That(savedCharacter.exp, Is.EqualTo(37));
        Assert.That(savedCharacter.pendingAttributeUpgradeCount, Is.EqualTo(1));
        Assert.That(savedCharacter.vaultDestroyedCount, Is.EqualTo(8));
        Assert.That(savedCharacter.completedBossCount, Is.EqualTo(2));
        Assert.That(savedCharacter.attributeUpgrades, Has.Count.EqualTo(2));
        Assert.That(savedCharacter.inventoryItems, Has.Count.EqualTo(2));
        Assert.That(savedCharacter.equippedItems, Has.Count.EqualTo(1));
        Assert.That(savedCharacter.gold, Is.EqualTo(345));
        Assert.That(savedCharacter.merchantIntroCompleted, Is.True);
        Assert.That(savedCharacter.purchasedLimitedShopItemIds, Is.EquivalentTo(new[] { "boss_iron_war_axe" }));
        Assert.That(savedCharacter.questProgress, Has.Count.EqualTo(1));
        Assert.That(savedCharacter.questProgress[0].currentCount, Is.EqualTo(3));

        var reloadedService = new LocalGuestSaveService(saveFilePath);
        Assert.That(reloadedService.TryLoad(out NCharacter[] reloaded, out string reloadMessage), Is.True, reloadMessage);
        NCharacter restored = reloaded.Single();
        Assert.That(restored.name, Is.EqualTo("成长测试"));
        Assert.That(restored.classId, Is.EqualTo(1));
        Assert.That(restored.level, Is.EqualTo(5));
        Assert.That(restored.exp, Is.EqualTo(37));
        Assert.That(restored.pendingAttributeUpgradeCount, Is.EqualTo(1));
        Assert.That(restored.vaultDestroyedCount, Is.EqualTo(8));
        Assert.That(restored.completedBossCount, Is.EqualTo(2));
        Assert.That(restored.GetAttributeUpgradeCount(PlayerAttributeType.AttackPower), Is.EqualTo(2));
        Assert.That(restored.GetAttributeUpgradeCount(PlayerAttributeType.MaxHp), Is.EqualTo(1));
        Assert.That(restored.inventoryItems, Has.Count.EqualTo(2));
        Assert.That(restored.equippedItems, Has.Count.EqualTo(1));
        Assert.That(restored.equippedItems[0].itemId, Is.EqualTo("boss_iron_war_axe"));
        Assert.That(restored.gold, Is.EqualTo(345));
        Assert.That(restored.merchantIntroCompleted, Is.True);
        Assert.That(restored.purchasedLimitedShopItemIds, Is.EquivalentTo(new[] { "boss_iron_war_axe" }));
        Assert.That(restored.questProgress, Has.Count.EqualTo(1));
        Assert.That(restored.questProgress[0].questId, Is.EqualTo("hunt_red_slime"));
        Assert.That(restored.questProgress[0].state, Is.EqualTo((int)QuestState.Active));
        Assert.That(restored.questProgress[0].currentCount, Is.EqualTo(3));
        Assert.That(
            restored.inventoryItems.Single(item => item.itemId == "healing_potion").count,
            Is.EqualTo(3));
        Assert.That(
            restored.inventoryItems.Single(item => item.itemId == "spider_king_core").slotIndex,
            Is.EqualTo(7));
    }

    [Test]
    public void RecreateOccupiedSlot_ResetsProgressAndKeepsStableLocalId()
    {
        var service = CreateLoadedService();
        NCharacter original = CreateCharacter(service, 1, "旧角色", 1);
        Assert.That(service.TryEnterCharacter(original, out _, out string enterMessage), Is.True, enterMessage);

        var progress = new PlayerProgressSaveData { Level = 2, Exp = 5 };
        progress.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.AttackPower,
            upgradeCount = 1
        });
        Assert.That(
            service.TrySaveCharacterProgress(progress, 1, 0, false, out _, out string saveMessage),
            Is.True,
            saveMessage);

        NCharacter replacement = CreateCharacter(service, 1, "新角色", 4);

        Assert.That(replacement.id, Is.EqualTo(2));
        Assert.That(replacement.name, Is.EqualTo("新角色"));
        Assert.That(replacement.classId, Is.EqualTo(4));
        Assert.That(replacement.level, Is.EqualTo(1));
        Assert.That(replacement.exp, Is.Zero);
        Assert.That(replacement.pendingAttributeUpgradeCount, Is.Zero);
        Assert.That(replacement.vaultDestroyedCount, Is.Zero);
        Assert.That(replacement.completedBossCount, Is.Zero);
        Assert.That(replacement.attributeUpgrades, Is.Empty);
        Assert.That(replacement.inventoryItems, Is.Empty);
    }

    [Test]
    public void DeathReset_AllowsOnlyExactLevelOneEmptyProgress()
    {
        var service = CreateLoadedService();
        NCharacter character = CreateCharacter(service, 0, "死亡重置测试", 2);
        Assert.That(service.TryEnterCharacter(character, out _, out string enterMessage), Is.True, enterMessage);

        var progressed = new PlayerProgressSaveData { Level = 5, Exp = 37 };
        progressed.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 0,
            itemId = "mana_potion",
            count = 4
        });
        progressed.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 1,
            itemId = "experience_crystal",
            count = 12
        });
        progressed.QuestProgress.Add(new NQuestProgressSave
        {
            questId = "hunt_green_slime",
            state = (int)QuestState.Active,
            currentCount = 4
        });
        Assert.That(
            service.TrySaveCharacterProgress(progressed, 8, 2, false, out _, out string progressMessage),
            Is.True,
            progressMessage);

        var invalidReset = new PlayerProgressSaveData { Level = 2, Exp = 0 };
        invalidReset.QuestProgress.AddRange(progressed.QuestProgress.Select(item => item.Clone()));
        Assert.That(
            service.TrySaveCharacterProgress(invalidReset, 0, 0, true, out _, out string invalidMessage),
            Is.False);
        Assert.That(invalidMessage, Does.Contain("必须全部归零"));

        var exactReset = new PlayerProgressSaveData { Level = 5, Exp = 99 };
        exactReset.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.AttackPower,
            upgradeCount = 1
        });
        exactReset.InventoryItems.AddRange(progressed.InventoryItems.Select(item => item.Clone()));
        exactReset.QuestProgress.AddRange(progressed.QuestProgress.Select(item => item.Clone()));
        exactReset.ResetAfterDeath(Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath));

        Assert.That(
            service.TrySaveCharacterProgress(
                exactReset,
                0,
                0,
                true,
                out NCharacter resetCharacter,
                out string resetMessage),
            Is.True,
            resetMessage);
        Assert.That(resetCharacter.level, Is.EqualTo(1));
        Assert.That(resetCharacter.exp, Is.Zero);
        Assert.That(resetCharacter.pendingAttributeUpgradeCount, Is.Zero);
        Assert.That(resetCharacter.vaultDestroyedCount, Is.Zero);
        Assert.That(resetCharacter.completedBossCount, Is.Zero);
        Assert.That(resetCharacter.attributeUpgrades, Is.Empty);
        Assert.That(resetCharacter.inventoryItems, Has.Count.EqualTo(1));
        Assert.That(resetCharacter.inventoryItems[0].itemId, Is.EqualTo("experience_crystal"));
        Assert.That(resetCharacter.inventoryItems[0].count, Is.EqualTo(12));
        Assert.That(resetCharacter.questProgress, Has.Count.EqualTo(1));
        Assert.That(resetCharacter.questProgress[0].currentCount, Is.EqualTo(4));
        Assert.That(resetCharacter.name, Is.EqualTo("死亡重置测试"));
        Assert.That(resetCharacter.classId, Is.EqualTo(2));
    }

    [Test]
    public void NormalSave_StillRejectsProgressRollback()
    {
        var service = CreateLoadedService();
        NCharacter character = CreateCharacter(service, 0, "防回档测试", 1);
        Assert.That(service.TryEnterCharacter(character, out _, out string enterMessage), Is.True, enterMessage);

        Assert.That(
            service.TrySaveCharacterProgress(
                new PlayerProgressSaveData { Level = 5, Exp = 10 },
                4,
                1,
                false,
                out _,
                out string firstMessage),
            Is.True,
            firstMessage);

        Assert.That(
            service.TrySaveCharacterProgress(
                new PlayerProgressSaveData { Level = 1, Exp = 0 },
                0,
                0,
                false,
                out _,
                out string rollbackMessage),
            Is.False);
        Assert.That(rollbackMessage, Does.Contain("不能用旧进度覆盖"));
    }

    [Test]
    public void CorruptedPrimaryFile_RestoresLastValidBackup()
    {
        var service = CreateLoadedService();
        CreateCharacter(service, 0, "备份角色", 2);
        CreateCharacter(service, 1, "最新角色", 3);
        Assert.That(File.Exists(service.BackupFilePath), Is.True, "第二次写盘后应生成上一版本备份。");

        File.WriteAllText(saveFilePath, "{ invalid json");
        var reloadedService = new LocalGuestSaveService(saveFilePath);

        bool success = reloadedService.TryLoad(out NCharacter[] characters, out string message);

        Assert.That(success, Is.True, message);
        Assert.That(message, Does.Contain("备份恢复"));
        Assert.That(characters, Has.Length.EqualTo(1));
        Assert.That(characters[0].name, Is.EqualTo("备份角色"));
    }

    [Test]
    public void CorruptedPrimaryAndBackup_ReturnsFailureWithoutOverwritingFiles()
    {
        var service = CreateLoadedService();
        CreateCharacter(service, 0, "备份角色", 2);
        CreateCharacter(service, 1, "最新角色", 3);
        const string corruptPrimary = "broken-primary";
        const string corruptBackup = "broken-backup";
        File.WriteAllText(saveFilePath, corruptPrimary);
        File.WriteAllText(service.BackupFilePath, corruptBackup);

        var reloadedService = new LocalGuestSaveService(saveFilePath);
        bool success = reloadedService.TryLoad(out NCharacter[] characters, out string message);

        Assert.That(success, Is.False);
        Assert.That(characters, Is.Empty);
        Assert.That(message, Does.Contain("读取失败"));
        Assert.That(File.ReadAllText(saveFilePath), Is.EqualTo(corruptPrimary));
        Assert.That(File.ReadAllText(service.BackupFilePath), Is.EqualTo(corruptBackup));
    }

    [Test]
    public void WriteFailure_DoesNotCommitCharacterToMemory()
    {
        string blockingFile = Path.Combine(temporaryDirectory, "not-a-directory");
        Directory.CreateDirectory(temporaryDirectory);
        File.WriteAllText(blockingFile, "block directory creation");
        var service = new LocalGuestSaveService(Path.Combine(blockingFile, "guest-save.json"));
        Assert.That(service.TryLoad(out _, out string loadMessage), Is.True, loadMessage);

        bool success = service.TryCreateCharacter(
            0,
            "无法保存",
            1,
            out NCharacter created,
            out NCharacter[] characters,
            out string message);

        Assert.That(success, Is.False);
        Assert.That(created, Is.Null);
        Assert.That(characters, Is.Empty);
        Assert.That(message, Does.Contain("写入失败"));
    }

    [Test]
    public void ResetRuntimeSession_KeepsGuestFileForNextLogin()
    {
        var service = CreateLoadedService();
        CreateCharacter(service, 3, "保留角色", 4);

        service.ResetRuntimeSession();

        Assert.That(File.Exists(saveFilePath), Is.True);
        var nextLoginService = new LocalGuestSaveService(saveFilePath);
        Assert.That(nextLoginService.TryLoad(out NCharacter[] characters, out string message), Is.True, message);
        Assert.That(characters.Single().name, Is.EqualTo("保留角色"));
    }

    [Test]
    public void LoginScene_GuestButtonIsBoundAndPlacedBelowAccountButtons()
    {
        const string scenePath = "Assets/Scenes/LoginScene.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        try
        {
            LoginPanelController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LoginPanelController>(true))
                .Single();

            FieldInfo guestField = typeof(LoginPanelController).GetField(
                "guestButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo loginField = typeof(LoginPanelController).GetField(
                "loginButton",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo registerField = typeof(LoginPanelController).GetField(
                "registerButton",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(guestField, Is.Not.Null);
            Button guestButton = guestField.GetValue(controller) as Button;
            Button loginButton = loginField?.GetValue(controller) as Button;
            Button registerButton = registerField?.GetValue(controller) as Button;
            Assert.That(guestButton, Is.Not.Null);
            Assert.That(loginButton, Is.Not.Null);
            Assert.That(registerButton, Is.Not.Null);
            Assert.That(guestButton.name, Is.EqualTo("GuestButton"));
            Assert.That(guestButton.GetComponentInChildren<Text>(true)?.text, Is.EqualTo("游客模式"));
            Assert.That(
                guestButton.GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(loginButton.GetComponent<RectTransform>().anchoredPosition.y));
            Assert.That(
                guestButton.GetComponent<RectTransform>().anchoredPosition.y,
                Is.LessThan(registerButton.GetComponent<RectTransform>().anchoredPosition.y));
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private LocalGuestSaveService CreateLoadedService()
    {
        var service = new LocalGuestSaveService(saveFilePath);
        Assert.That(service.TryLoad(out _, out string message), Is.True, message);
        return service;
    }

    private static NCharacter CreateCharacter(
        LocalGuestSaveService service,
        int slotIndex,
        string characterName,
        int classId)
    {
        Assert.That(
            service.TryCreateCharacter(
                slotIndex,
                characterName,
                classId,
                out NCharacter character,
                out _,
                out string message),
            Is.True,
            message);
        return character;
    }
}
#endif
