#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ProtoBuf;
using QFramework;
using SkillBridge.Message;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色存档 EditMode 测试：保护属性公式恢复、重开清理边界、Boss 进度和协议字段。
/// 测试不依赖数据库连接，数据库事务与账号归属校验由服务端编译和联调步骤验证。
/// </summary>
public sealed class CharacterProgressPersistenceTests
{
    private GameObject configObject;
    private GameConfig config;
    private IArchitecture architecture;

    [SetUp]
    public void SetUp()
    {
        configObject = new GameObject("CharacterProgressTestConfig");
        config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60, 75, 90, 110, 135, 160, 190, 225, 260 };
        config.Lv_Hpmax = new[] { 150, 180, 200, 230, 260, 290, 320, 350, 380, 410 };
        config.playerAttackUpgradePercent = 0.3f;
        config.playerMaxHpUpgradeFlat = 50;
        config.playerMoveSpeedUpgradePercent = 0.15f;
        config.playerCritChanceUpgrade = 0.1f;
        config.playerDodgeChanceUpgrade = 0.1f;
        config.playerHpRegenUpgrade = 1f;
        config.playerHpRegenCap = 32f;
        config.playerDamageReductionUpgrade = 0.1f;
        config.playerLifeStealUpgrade = 0.05f;
        GameConfig.instance = config;

        architecture = TreasureHunterArchitecture.Interface;
        BossRunProgressState.ResetRun();
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.Deinit();
        architecture = null;
        BossRunProgressState.ResetRun();
        GameConfig.instance = null;
        Object.DestroyImmediate(configObject);
    }

    [Test]
    public void InitializePlayer_RestoresAllEightUpgradeTypesAndFillsResources()
    {
        NCharacter save = CreateCharacterWithAllUpgrades();
        CharacterDefine define = CreateCharacterDefine();

        architecture.SendCommand(new InitializePlayerCommand(save, define));

        PlayerStatsSnapshot stats = architecture.SendQuery(new GetPlayerStatsQuery());
        PlayerProgressSaveData progress = architecture.SendQuery(new GetPlayerProgressSaveDataQuery());

        Assert.That(stats.Level, Is.EqualTo(10));
        Assert.That(stats.CurrentExp, Is.EqualTo(25));
        Assert.That(stats.PendingUpgradeSelectionCount, Is.EqualTo(1));
        Assert.That(stats.AttackPower, Is.EqualTo(52));
        Assert.That(stats.MaxHp, Is.EqualTo(510));
        Assert.That(stats.CurrentHp, Is.EqualTo(stats.MaxHp));
        Assert.That(stats.CurrentMp, Is.EqualTo(stats.MaxMp));
        Assert.That(stats.CurrentMoveSpeed, Is.EqualTo(3.45f).Within(0.001f));
        Assert.That(stats.CritChance, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(stats.DodgeChance, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(stats.HealthRegenPerSecond, Is.EqualTo(1f).Within(0.001f));
        Assert.That(stats.DamageReduction, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(stats.LifeSteal, Is.EqualTo(0.05f).Within(0.001f));
        Assert.That(progress.AttributeUpgrades, Has.Count.EqualTo(8));
        Assert.That(progress.PendingAttributeUpgradeCount, Is.EqualTo(1));
    }

    [Test]
    public void SceneTransferSnapshot_PreservesAllUpgradeCounts()
    {
        NCharacter save = CreateCharacterWithAllUpgrades();
        CharacterDefine define = CreateCharacterDefine();
        architecture.SendCommand(new InitializePlayerCommand(save, define));

        PlayerModel model = architecture.GetModel<PlayerModel>();
        PlayerStatsSnapshot snapshot = model.CreateSnapshot();
        model.RestoreFromSceneTransferSnapshot(save.Clone(), define, snapshot);
        PlayerProgressSaveData restored = architecture.SendQuery(new GetPlayerProgressSaveDataQuery());

        Assert.That(restored.AttributeUpgrades, Has.Count.EqualTo(8));
        for (int typeValue = (int)PlayerAttributeType.AttackPower;
             typeValue <= (int)PlayerAttributeType.LifeSteal;
             typeValue++)
        {
            Assert.That(snapshot.GetAttributeUpgradeCount((PlayerAttributeType)typeValue), Is.EqualTo(1));
            Assert.That(
                restored.AttributeUpgrades.Exists(item =>
                    item.attributeType == typeValue && item.upgradeCount == 1),
                Is.True);
        }
    }

    [Test]
    public void ClearRunUpgrades_OnlyClearsPendingAndUpgradeCounts()
    {
        var progress = new PlayerProgressSaveData
        {
            Level = 10,
            Exp = 25,
            PendingAttributeUpgradeCount = 1
        };
        progress.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.AttackPower,
            upgradeCount = 2
        });

        progress.ClearRunUpgrades();

        Assert.That(progress.Level, Is.EqualTo(10));
        Assert.That(progress.Exp, Is.EqualTo(25));
        Assert.That(progress.PendingAttributeUpgradeCount, Is.Zero);
        Assert.That(progress.AttributeUpgrades, Is.Empty);
    }

    [Test]
    public void ResetAfterDeath_ResetsLevelExperienceAndAllUpgradeData()
    {
        var progress = new PlayerProgressSaveData
        {
            Level = 10,
            Exp = 25,
            PendingAttributeUpgradeCount = 1
        };
        progress.AttributeUpgrades.Add(new NAttributeUpgradeSave
        {
            attributeType = (int)PlayerAttributeType.AttackPower,
            upgradeCount = 2
        });

        progress.ResetAfterDeath(Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath));

        Assert.That(progress.Level, Is.EqualTo(1));
        Assert.That(progress.Exp, Is.Zero);
        Assert.That(progress.PendingAttributeUpgradeCount, Is.Zero);
        Assert.That(progress.AttributeUpgrades, Is.Empty);
    }

    [Test]
    public void ResetAfterDeath_RemovesConsumablesButKeepsMaterialAndQuestItems()
    {
        InventoryDatabase database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);
        Assert.That(database, Is.Not.Null);

        var progress = new PlayerProgressSaveData { Level = 5, Exp = 20 };
        progress.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 0,
            itemId = "healing_potion",
            count = 2
        });
        progress.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 1,
            itemId = "experience_crystal",
            count = 8
        });
        progress.InventoryItems.Add(new NInventoryItemSave
        {
            slotIndex = 2,
            itemId = "ancient_scroll",
            count = 1
        });

        progress.ResetAfterDeath(database);

        Assert.That(progress.InventoryItems, Has.Count.EqualTo(2));
        Assert.That(progress.InventoryItems.Exists(item => item.itemId == "healing_potion"), Is.False);
        Assert.That(progress.InventoryItems.Exists(item => item.itemId == "experience_crystal"), Is.True);
        Assert.That(progress.InventoryItems.Exists(item => item.itemId == "ancient_scroll"), Is.True);
    }

    [Test]
    public void SaveModePriority_DeathResetCannotBeDowngradedByNormalSave()
    {
        Assert.That(
            CharacterProgressSaveMode.ResetAfterDeath,
            Is.GreaterThan(CharacterProgressSaveMode.ClearRunUpgrades));
        Assert.That(
            CharacterProgressSaveMode.ClearRunUpgrades,
            Is.GreaterThan(CharacterProgressSaveMode.Normal));

        string source = File.ReadAllText("Assets/Script/Services/CharacterProgressSaveService.cs");
        Assert.That(source, Does.Contain("if (saveMode > requestedSaveMode)"));
        Assert.That(source, Does.Contain("RequestSave(true, CharacterProgressSaveMode.ResetAfterDeath)"));
    }

    [Test]
    public void InventoryPersistence_RestoresBeforeSessionAndAutoSavesRuntimeChanges()
    {
        string sceneFlowSource = File.ReadAllText("Assets/Script/Services/SceneFlowService.cs");
        int startGameplay = sceneFlowSource.IndexOf(
            "public static void StartGameplay(NCharacter selectedCharacter)",
            System.StringComparison.Ordinal);
        int restoreInventory = sceneFlowSource.IndexOf(
            "new RestoreInventoryCommand",
            startGameplay,
            System.StringComparison.Ordinal);
        int beginSession = sceneFlowSource.IndexOf(
            "saveService.BeginSession(selectedCharacter)",
            startGameplay,
            System.StringComparison.Ordinal);

        Assert.That(startGameplay, Is.GreaterThanOrEqualTo(0));
        Assert.That(restoreInventory, Is.GreaterThan(startGameplay));
        Assert.That(beginSession, Is.GreaterThan(restoreInventory));

        string saveServiceSource = File.ReadAllText(
            "Assets/Script/Services/CharacterProgressSaveService.cs");
        Assert.That(saveServiceSource, Does.Contain("RegisterEvent<InventoryChangedEvent>"));
        Assert.That(saveServiceSource, Does.Contain("HandleInventoryChanged"));
        Assert.That(saveServiceSource, Does.Contain("new RestoreInventoryCommand(savedCharacter.inventoryItems)"));
        Assert.That(saveServiceSource, Does.Not.Contain("new ResetInventoryCommand()"));
    }

    [Test]
    public void ClearRunUpgradeCommand_PreservesLevelAndExperience()
    {
        NCharacter save = CreateCharacterWithAllUpgrades();
        architecture.SendCommand(new InitializePlayerCommand(save, CreateCharacterDefine()));

        architecture.SendCommand(new ClearPlayerRunUpgradeProgressCommand());
        PlayerProgressSaveData cleared = architecture.SendQuery(new GetPlayerProgressSaveDataQuery());

        Assert.That(cleared.Level, Is.EqualTo(10));
        Assert.That(cleared.Exp, Is.EqualTo(25));
        Assert.That(cleared.PendingAttributeUpgradeCount, Is.Zero);
        Assert.That(cleared.AttributeUpgrades, Is.Empty);
    }

    [Test]
    public void ResetAfterDeathCommand_RestoresBaseProgressWithoutRevivingPlayer()
    {
        NCharacter original = CreateCharacterWithAllUpgrades();
        architecture.SendCommand(new InitializePlayerCommand(original, CreateCharacterDefine()));
        PlayerDamageResult deathResult = architecture.SendCommand(
            new TakePlayerDamageCommand(99999, false));
        Assert.That(deathResult.Died, Is.True);

        var confirmedReset = new NCharacter
        {
            id = original.id,
            slotIndex = original.slotIndex,
            name = original.name,
            classId = original.classId,
            level = 1,
            exp = 0
        };
        architecture.SendCommand(new ResetPlayerProgressAfterDeathCommand(confirmedReset));

        PlayerStatsSnapshot stats = architecture.SendQuery(new GetPlayerStatsQuery());
        PlayerProgressSaveData progress = architecture.SendQuery(new GetPlayerProgressSaveDataQuery());
        Assert.That(stats.Level, Is.EqualTo(1));
        Assert.That(stats.CurrentExp, Is.Zero);
        Assert.That(stats.CurrentHp, Is.Zero, "死亡重置只能更新成长数据，不能在结算界面复活角色。");
        Assert.That(stats.AttackPower, Is.EqualTo(40));
        Assert.That(stats.MaxHp, Is.EqualTo(200));
        Assert.That(stats.CurrentMoveSpeed, Is.EqualTo(3f).Within(0.001f));
        Assert.That(progress.PendingAttributeUpgradeCount, Is.Zero);
        Assert.That(progress.AttributeUpgrades, Is.Empty);
    }

    [Test]
    public void RestoreBossProgress_DoesNotEmitRewardOrProgressEvents()
    {
        int eventCount = 0;
        BossRunProgressState.PersistentProgressChanged += CountEvent;
        try
        {
            BossRunProgressState.RestorePersistentProgress(12, 2);
            BossRunProgressState.ConfigureVaultsPerBoss(5);

            Assert.That(BossRunProgressState.TotalVaultDestroyedCount, Is.EqualTo(12));
            Assert.That(BossRunProgressState.CompletedBossCount, Is.EqualTo(2));
            Assert.That(BossRunProgressState.VaultsUntilNextBoss, Is.EqualTo(3));
            Assert.That(BossRunProgressState.IsBossEntranceReady, Is.False);
            Assert.That(eventCount, Is.Zero, "读取数据库不应被当成一次新击破或新胜利。");
        }
        finally
        {
            BossRunProgressState.PersistentProgressChanged -= CountEvent;
        }

        void CountEvent() => eventCount++;
    }

    [Test]
    public void CharacterSlotSummary_ContainsLevelExperienceBossAndVaultProgress()
    {
        string summary = CharacterSaveSlot.BuildProgressSummary(new NCharacter
        {
            level = 3,
            exp = 20,
            completedBossCount = 2,
            vaultDestroyedCount = 13
        });

        Assert.That(summary, Is.EqualTo("Lv.3  经验 20/75\nBoss 2轮 · 宝箱 13次"));
    }

    [Test]
    public void SaveProgressProtocol_RoundTripPreservesNewFields()
    {
        var source = new NetMessage
        {
            Request = new NetMessageRequest
            {
                saveCharacterProgress = new UserSaveCharacterProgressRequest
                {
                    Level = 10,
                    Exp = 25,
                    PendingAttributeUpgradeCount = 1,
                    VaultDestroyedCount = 12,
                    CompletedBossCount = 2,
                    ResetAfterDeath = true
                }
            }
        };
        source.Request.saveCharacterProgress.AttributeUpgrades.Add(new NAttributeUpgradeInfo
        {
            AttributeType = (int)PlayerAttributeType.AttackPower,
            UpgradeCount = 3
        });
        source.Request.saveCharacterProgress.InventoryItems.Add(new NInventoryItemInfo
        {
            SlotIndex = 3,
            ItemId = "spider_king_core",
            Count = 2
        });

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, source);
        stream.Position = 0;
        NetMessage restored = Serializer.Deserialize<NetMessage>(stream);
        UserSaveCharacterProgressRequest request = restored.Request.saveCharacterProgress;

        Assert.That(request.Level, Is.EqualTo(10));
        Assert.That(request.Exp, Is.EqualTo(25));
        Assert.That(request.PendingAttributeUpgradeCount, Is.EqualTo(1));
        Assert.That(request.VaultDestroyedCount, Is.EqualTo(12));
        Assert.That(request.CompletedBossCount, Is.EqualTo(2));
        Assert.That(request.ResetAfterDeath, Is.True);
        Assert.That(request.AttributeUpgrades, Has.Count.EqualTo(1));
        Assert.That(request.AttributeUpgrades[0].UpgradeCount, Is.EqualTo(3));
        Assert.That(request.InventoryItems, Has.Count.EqualTo(1));
        Assert.That(request.InventoryItems[0].SlotIndex, Is.EqualTo(3));
        Assert.That(request.InventoryItems[0].ItemId, Is.EqualTo("spider_king_core"));
        Assert.That(request.InventoryItems[0].Count, Is.EqualTo(2));
    }

    [Test]
    public void PauseAndGameOverOverlays_ReturnToCharacterSelectWithoutQuittingApplication()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Assert.That(prefab, Is.Not.Null);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.That(instance, Is.Not.Null);

        try
        {
            GameSessionUi sessionUi = instance.GetComponent<GameSessionUi>();
            Assert.That(sessionUi, Is.Not.Null);

            System.Type overlayModeType = typeof(GameSessionUi).GetNestedType(
                "OverlayMode",
                BindingFlags.NonPublic);
            MethodInfo applyOverlayContent = typeof(GameSessionUi).GetMethod(
                "ApplyOverlayContent",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo secondaryButtonTextField = typeof(GameSessionUi).GetField(
                "secondaryButtonText",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(overlayModeType, Is.Not.Null);
            Assert.That(applyOverlayContent, Is.Not.Null);
            Assert.That(secondaryButtonTextField, Is.Not.Null);

            Text secondaryButtonText = secondaryButtonTextField.GetValue(sessionUi) as Text;
            Assert.That(secondaryButtonText, Is.Not.Null);

            object pauseMode = System.Enum.Parse(overlayModeType, "Pause");
            applyOverlayContent.Invoke(sessionUi, new[] { pauseMode, (object)123, true });
            Assert.That(secondaryButtonText.text, Is.EqualTo("保存并退出"));

            object gameOverMode = System.Enum.Parse(overlayModeType, "GameOver");
            applyOverlayContent.Invoke(sessionUi, new[] { gameOverMode, (object)123, true });
            Assert.That(secondaryButtonText.text, Is.EqualTo("返回角色选择"));

            string sessionUiSource = File.ReadAllText("Assets/Script/UI/GameSessionUi.cs");
            Assert.That(
                sessionUiSource,
                Does.Contain("secondaryButton.onClick.AddListener(SaveAndReturnToCharacterSelect);"));
            Assert.That(
                sessionUiSource,
                Does.Contain("overlayMode == OverlayMode.GameOver")
                    .And.Contain("CharacterProgressSaveMode.ResetAfterDeath")
                    .And.Contain("CharacterProgressSaveMode.Normal"));
            Assert.That(sessionUiSource, Does.Not.Contain("Application.Quit()"));

            string fallbackPanelSource = File.ReadAllText("Assets/Script/UI/ReStartPanel.cs");
            Assert.That(
                fallbackPanelSource,
                Does.Contain("ConfigureButton(quitButton, \"返回角色选择\", ReturnToCharacterSelect);"));
            Assert.That(fallbackPanelSource, Does.Contain("SceneFlowService.ReturnToCharacterSelect();"));
            Assert.That(
                fallbackPanelSource,
                Does.Contain("CharacterProgressSaveMode.ResetAfterDeath"));
            Assert.That(fallbackPanelSource, Does.Not.Contain("Application.Quit()"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void ReturnToCharacterSelect_ClearsGameplayStateWithoutClearingLoginSession()
    {
        string source = File.ReadAllText("Assets/Script/Services/SceneFlowService.cs");
        const string signature = "public static void ReturnToCharacterSelect()";
        int methodStart = source.IndexOf(signature, System.StringComparison.Ordinal);
        int methodEnd = source.IndexOf(
            "\n    /// <summary>",
            methodStart + signature.Length,
            System.StringComparison.Ordinal);

        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0));
        Assert.That(methodEnd, Is.GreaterThan(methodStart));

        string methodSource = source.Substring(methodStart, methodEnd - methodStart);
        string[] requiredCleanupCalls =
        {
            "GameplayCharacterManager.Instance.Clear()",
            "ResetInventoryCommand",
            "SelectedCharacterState.Clear()",
            "ClearVaultProgressCache()",
            "BossRunProgressState.ResetRun()",
            "PlayerSceneTransferState.Clear()",
            "GameplayStartupGuideState.ResetSession()",
            "LoadSceneWithLoading(GameSceneNames.CharacterSelectScene)"
        };

        foreach (string requiredCall in requiredCleanupCalls)
        {
            Assert.That(methodSource, Does.Contain(requiredCall));
        }

        Assert.That(
            methodSource,
            Does.Not.Contain("ClearSession("),
            "返回选角必须保留 GameApiClient 的账号登录态和角色缓存。");
    }

    private static NCharacter CreateCharacterWithAllUpgrades()
    {
        var save = new NCharacter
        {
            id = 1,
            slotIndex = 0,
            name = "SaveTestPlayer",
            classId = 1,
            level = 10,
            exp = 25,
            pendingAttributeUpgradeCount = 1
        };

        for (int typeValue = (int)PlayerAttributeType.AttackPower;
             typeValue <= (int)PlayerAttributeType.LifeSteal;
             typeValue++)
        {
            save.attributeUpgrades.Add(new NAttributeUpgradeSave
            {
                attributeType = typeValue,
                upgradeCount = 1
            });
        }

        return save;
    }

    private static CharacterDefine CreateCharacterDefine()
    {
        return new CharacterDefine
        {
            classId = 1,
            initLevel = 1,
            hp = 200f,
            mp = 120f,
            attack = 40f,
            moveSpeed = 3f
        };
    }
}
#endif
