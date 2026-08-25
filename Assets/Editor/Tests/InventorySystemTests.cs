#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包规则 EditMode 测试：直接验证 InventorySystem 的堆叠、占格、满包和重置边界。
/// 测试不依赖场景与 UI，因此规则层出错时可以更快定位问题。
/// </summary>
public sealed class InventorySystemTests
{
    private readonly List<InventoryItemDefinition> temporaryItems =
        new List<InventoryItemDefinition>();

    private IArchitecture architecture;
    private InventorySystem inventorySystem;
    private InventoryModel inventoryModel;
    private GameObject configObject;

    [SetUp]
    public void SetUp()
    {
        configObject = new GameObject("InventoryTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60, 75 };
        config.Lv_Hpmax = new[] { 100, 120, 140 };
        GameConfig.instance = config;

        architecture = TreasureHunterArchitecture.Interface;
        inventorySystem = architecture.GetSystem<InventorySystem>();
        inventoryModel = architecture.GetModel<InventoryModel>();

        inventorySystem.ConfigureDatabase(null);
        inventorySystem.ResetInventory();
        InitializePlayer();
    }

    [TearDown]
    public void TearDown()
    {
        inventorySystem.ResetInventory();
        inventorySystem.ConfigureDatabase(Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath));

        for (int i = 0; i < temporaryItems.Count; i++)
        {
            Object.DestroyImmediate(temporaryItems[i]);
        }

        temporaryItems.Clear();
        GameConfig.instance = null;
        Object.DestroyImmediate(configObject);
    }

    [Test]
    public void TryAddItem_StacksIntoExistingSlotFirst()
    {
        InventoryItemDefinition potion = CreateItem("test_potion", 20);

        inventorySystem.TryAddItem(potion, 12);
        InventoryAddResult result = inventorySystem.TryAddItem(potion, 5);

        Assert.That(result.AddedAmount, Is.EqualTo(5));
        Assert.That(result.RemainingAmount, Is.Zero);
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(17));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.EqualTo(1));
    }

    [Test]
    public void TryAddItem_CrossesStackLimitAndUsesNextEmptySlot()
    {
        InventoryItemDefinition crystal = CreateItem("test_crystal", 20);

        InventoryAddResult result = inventorySystem.TryAddItem(crystal, 27);

        Assert.That(result.AddedAll, Is.True);
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(20));
        Assert.That(inventoryModel.Slots[1].Count, Is.EqualTo(7));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.EqualTo(2));
    }

    [Test]
    public void TryAddItem_DifferentItemUsesEmptySlot()
    {
        InventoryItemDefinition first = CreateItem("test_first", 20);
        InventoryItemDefinition second = CreateItem("test_second", 20);

        inventorySystem.TryAddItem(first, 1);
        inventorySystem.TryAddItem(second, 1);

        Assert.That(inventoryModel.Slots[0].Item, Is.SameAs(first));
        Assert.That(inventoryModel.Slots[1].Item, Is.SameAs(second));
    }

    [Test]
    public void TryAddItem_WhenInventoryIsFullReportsRemainingAmount()
    {
        for (int i = 0; i < InventoryModel.DefaultCapacity; i++)
        {
            inventorySystem.TryAddItem(CreateItem($"full_slot_{i}", 1), 1);
        }

        InventoryItemDefinition overflow = CreateItem("overflow", 1);
        InventoryAddResult result = inventorySystem.TryAddItem(overflow, 3);

        Assert.That(result.AddedAmount, Is.Zero);
        Assert.That(result.RemainingAmount, Is.EqualTo(3));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.EqualTo(InventoryModel.DefaultCapacity));
        Assert.That(inventorySystem.GetAddableAmount(overflow, 1), Is.Zero);
    }

    [Test]
    public void ResetInventory_ClearsEverySlotWithoutChangingCapacity()
    {
        inventorySystem.TryAddItem(CreateItem("reset_item", 5), 8);

        inventorySystem.ResetInventory();

        Assert.That(inventoryModel.Capacity, Is.EqualTo(InventoryModel.DefaultCapacity));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.Zero);
        Assert.That(inventoryModel.Slots, Has.All.Matches<InventorySlotData>(slot => slot.IsEmpty));
    }

    [Test]
    public void SaveSnapshotAndRestore_PreserveStableIdCountAndSlotIndex()
    {
        InventoryDatabase database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);
        Assert.That(database, Is.Not.Null);
        Assert.That(database.TryGetItemById("spider_king_core", out InventoryItemDefinition item), Is.True);
        inventorySystem.ConfigureDatabase(database);
        inventorySystem.TryAddItem(item, 2);

        List<NInventoryItemSave> snapshot = inventorySystem.CreateSaveSnapshot();
        inventorySystem.ResetInventory();
        inventorySystem.RestoreInventory(snapshot);

        Assert.That(snapshot, Has.Count.EqualTo(1));
        Assert.That(snapshot[0].slotIndex, Is.Zero);
        Assert.That(snapshot[0].itemId, Is.EqualTo("spider_king_core"));
        Assert.That(snapshot[0].count, Is.EqualTo(2));
        Assert.That(inventoryModel.Slots[0].Item, Is.SameAs(item));
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(2));
    }

    [Test]
    public void UseHealthPotion_RestoresThirtyPercentAndConsumesOne()
    {
        InventoryItemDefinition potion = CreateItem(
            "health_potion",
            20,
            InventoryItemUseEffect.RestoreHealth,
            0.3f);
        inventorySystem.TryAddItem(potion, 2);
        architecture.SendCommand(new TakePlayerDamageCommand(70, false));
        PlayerStatsSnapshot damagedStats = architecture.SendQuery(new GetPlayerStatsQuery());
        int expectedRestore = Mathf.Min(
            Mathf.CeilToInt(damagedStats.MaxHp * 0.3f),
            damagedStats.MaxHp - damagedStats.CurrentHp);

        InventoryUseResult result = architecture.SendCommand(new UseInventoryItemCommand(0));
        PlayerStatsSnapshot stats = architecture.SendQuery(new GetPlayerStatsQuery());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.ActualRestoredAmount, Is.EqualTo(expectedRestore));
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(1));
        Assert.That(stats.CurrentHp, Is.EqualTo(damagedStats.CurrentHp + result.ActualRestoredAmount));
    }

    [Test]
    public void UseHealthPotion_WhenHealthIsFullDoesNotConsumeItem()
    {
        InventoryItemDefinition potion = CreateItem(
            "full_health_potion",
            20,
            InventoryItemUseEffect.RestoreHealth,
            0.3f);
        inventorySystem.TryAddItem(potion, 1);

        InventoryUseResult result = architecture.SendCommand(new UseInventoryItemCommand(0));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.FailureReason, Is.EqualTo(InventoryUseFailureReason.ResourceAlreadyFull));
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(1));
    }

    [Test]
    public void UseManaPotion_RestoresThirtyPercentAndClearsLastItem()
    {
        InventoryItemDefinition potion = CreateItem(
            "mana_potion",
            20,
            InventoryItemUseEffect.RestoreMana,
            0.3f);
        inventorySystem.TryAddItem(potion, 1);
        architecture.SendCommand(new TrySpendPlayerManaCommand(80));
        int maxMana = architecture.SendQuery(new GetPlayerStatsQuery()).MaxMp;

        InventoryUseResult result = architecture.SendCommand(new UseInventoryItemCommand(0));
        PlayerStatsSnapshot stats = architecture.SendQuery(new GetPlayerStatsQuery());

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.ActualRestoredAmount, Is.EqualTo(Mathf.CeilToInt(maxMana * 0.3f)));
        Assert.That(stats.CurrentMp, Is.EqualTo(maxMana - 80 + result.ActualRestoredAmount));
        Assert.That(inventoryModel.Slots[0].IsEmpty, Is.True);
    }

    private InventoryItemDefinition CreateItem(
        string itemId,
        int maxStack,
        InventoryItemUseEffect useEffect = InventoryItemUseEffect.None,
        float restorePercent = 0f)
    {
        InventoryItemDefinition item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        item.name = itemId;

        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = itemId;
        serialized.FindProperty("maxStack").intValue = maxStack;
        serialized.FindProperty("useEffect").enumValueIndex = (int)useEffect;
        serialized.FindProperty("restorePercent").floatValue = restorePercent;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        temporaryItems.Add(item);
        return item;
    }

    private void InitializePlayer()
    {
        architecture.SendCommand(new InitializePlayerCommand(
            new NCharacter
            {
                id = 1,
                slotIndex = 0,
                name = "InventoryTestPlayer",
                classId = 1,
                level = 1,
                exp = 0
            },
            new CharacterDefine
            {
                classId = 1,
                initLevel = 1,
                hp = 100f,
                mp = 100f,
                attack = 10f,
                defense = 0f,
                moveSpeed = 3f
            }));
    }
}

/// <summary>
/// 背包资源结构测试：保护静态 24 格、面板引用、小怪掉落和 Boss 掉落的装配结果。
/// </summary>
public sealed class InventoryPrefabStructureTests
{
    [Test]
    public void GameplayUiPrefab_HasValidInventoryPanelAndTwentyFourStaticSlots()
    {
        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameplayUiRoot.prefab");

        Assert.That(gameplayUi, Is.Not.Null);
        InventoryPanel panel = gameplayUi.GetComponent<InventoryPanel>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.ValidatePrefabReferences(false), Is.True);
        InventorySlotView[] slots = gameplayUi.GetComponentsInChildren<InventorySlotView>(true);
        Assert.That(slots, Has.Length.EqualTo(InventoryModel.DefaultCapacity));

        for (int i = 0; i < slots.Length; i++)
        {
            Transform selectedFrame = slots[i].transform.Find("SelectedFrame");
            Assert.That(selectedFrame, Is.Not.Null, $"Slot {i} 缺少 SelectedFrame。 ");

            Image selectedFrameImage = selectedFrame.GetComponent<Image>();
            Assert.That(selectedFrameImage, Is.Not.Null, $"Slot {i} 的 SelectedFrame 缺少 Image。 ");
        }
    }

    [Test]
    public void DefaultDatabaseAndBoxPrefab_HaveExpectedLootConfiguration()
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(
            "Assets/Resources/Data/Inventory/InventoryDatabase.asset");
        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Box.prefab");

        Assert.That(database, Is.Not.Null);
        Assert.That(database.Capacity, Is.EqualTo(24));
        Assert.That(database.Items, Has.Length.EqualTo(5));
        Assert.That(database.VaultLootEntries, Is.Empty);
        Assert.That(database.MonsterDropChance, Is.EqualTo(0.12f));
        Assert.That(database.MonsterLootEntries, Has.Length.EqualTo(2));
        Assert.That(database.MonsterLootEntries[0].Weight, Is.EqualTo(55f));
        Assert.That(database.MonsterLootEntries[1].Weight, Is.EqualTo(45f));
        Assert.That(database.WorldPickupPrefab, Is.Not.Null);
        Assert.That(database.BossDropOrbCount, Is.EqualTo(2));
        Assert.That(database.BossLootEntries, Has.Length.EqualTo(3));
        Assert.That(database.BossLootOrbPrefab, Is.Not.Null);

        Assert.That(boxPrefab, Is.Not.Null);
        VaultLootRewardController rewardController = boxPrefab.GetComponent<VaultLootRewardController>();
        Assert.That(rewardController, Is.Null);
    }

    [Test]
    public void MonsterLootRoll_UsesTwelvePercentGateAndApprovedPotionWeights()
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(
            "Assets/Resources/Data/Inventory/InventoryDatabase.asset");

        Assert.That(database.TryRollMonsterLoot(0.12f, 0f, out _), Is.False);
        Assert.That(database.TryRollMonsterLoot(0.1199f, 0.5499f, out InventoryItemDefinition health), Is.True);
        Assert.That(database.TryRollMonsterLoot(0.1199f, 0.55f, out InventoryItemDefinition mana), Is.True);
        Assert.That(health.UseEffect, Is.EqualTo(InventoryItemUseEffect.RestoreHealth));
        Assert.That(mana.UseEffect, Is.EqualTo(InventoryItemUseEffect.RestoreMana));
    }

    [Test]
    public void BossLootRoll_UsesBossTableWithoutPotionItems()
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(
            "Assets/Resources/Data/Inventory/InventoryDatabase.asset");

        Assert.That(database.TryRollBossLoot(0f, out InventoryItemDefinition first, out int firstAmount), Is.True);
        Assert.That(database.TryRollBossLoot(0.99f, out InventoryItemDefinition last, out int lastAmount), Is.True);
        Assert.That(first.UseEffect, Is.EqualTo(InventoryItemUseEffect.None));
        Assert.That(last.UseEffect, Is.EqualTo(InventoryItemUseEffect.None));
        Assert.That(firstAmount, Is.GreaterThanOrEqualTo(1));
        Assert.That(lastAmount, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void PickupAndSlimePrefabs_HaveRequiredComponents()
    {
        GameObject pickup = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/World/WorldItemPickup.prefab");
        GameObject bossPickup = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/World/BossLootOrbPickup.prefab");
        GameObject slimeOne = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slime1.prefab");
        GameObject slimeTwo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slime2.prefab");

        Assert.That(pickup, Is.Not.Null);
        Assert.That(pickup.GetComponent<WorldItemPickup>(), Is.Not.Null);
        Assert.That(pickup.GetComponent<SphereCollider>().isTrigger, Is.True);
        Assert.That(pickup.GetComponent<Rigidbody>().isKinematic, Is.True);
        Assert.That(pickup.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
        Assert.That(bossPickup, Is.Not.Null);
        Assert.That(bossPickup.GetComponent<WorldItemPickup>(), Is.Not.Null);
        Assert.That(bossPickup.GetComponent<SphereCollider>().isTrigger, Is.True);
        Assert.That(bossPickup.GetComponent<Rigidbody>().isKinematic, Is.True);
        Assert.That(slimeOne.GetComponent<MonsterLootDropController>(), Is.Not.Null);
        Assert.That(slimeTwo.GetComponent<MonsterLootDropController>(), Is.Not.Null);
        Assert.That(slimeOne.GetComponent<CharacterController>().stepOffset, Is.LessThanOrEqualTo(0.05f));
        Assert.That(slimeTwo.GetComponent<CharacterController>().stepOffset, Is.LessThanOrEqualTo(0.05f));
    }
}
#endif
