#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包功能装配工具：创建默认物品配置、静态 24 格 UI、地面拾取物，并装配普通怪与 Boss 掉落。
/// 工具可重复执行；只迁移本功能明确要求的物品、掉落表、拾取物和 UI 文案。
/// </summary>
public static class InventoryFeatureSetupTool
{
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    private const string BoxPrefabPath = "Assets/Prefabs/Box.prefab";
    private const string SlimeOnePrefabPath = "Assets/Prefabs/Slime1.prefab";
    private const string SlimeTwoPrefabPath = "Assets/Prefabs/Slime2.prefab";
    private const string WorldPickupPrefabPath = "Assets/Prefabs/World/WorldItemPickup.prefab";
    private const string BossLootOrbPrefabPath = "Assets/Prefabs/World/BossLootOrbPickup.prefab";
    private const string InventoryFolder = "Assets/Resources/Data/Inventory";
    private const string DatabasePath = InventoryFolder + "/InventoryDatabase.asset";
    private const string PotionPath = InventoryFolder + "/HealingPotion.asset";
    private const string ManaPotionPath = InventoryFolder + "/ManaPotion.asset";
    private const string CrystalPath = InventoryFolder + "/ExperienceCrystal.asset";
    private const string ScrollPath = InventoryFolder + "/AncientScroll.asset";
    private const string SpiderKingCorePath = InventoryFolder + "/SpiderKingCore.asset";

    private const string BackgroundSpritePath =
        "Assets/AllResources/Classic_RPG_GUI/Parts/Inventory_bar.png";
    private const string SlotSpritePath =
        "Assets/AllResources/Classic_RPG_GUI/Parts/inventory_frame.png";
    private const string SelectedSlotSpritePath =
        "Assets/AllResources/Classic_RPG_GUI/Parts/inventory_frame_little_ready.png";
    private const string BagIconPath =
        "Assets/AllResources/Classic_RPG_GUI/Icons/Inventory.png";
    private const string PotionIconPath =
        "Assets/AllResources/游戏原美术素材/Suriyun/UI/UI/Icons/item_icon_posion.png";
    private const string CrystalIconPath =
        "Assets/AllResources/游戏原美术素材/Suriyun/UI/UI/Icons/item_icon_exp2.png";
    private const string ScrollIconPath =
        "Assets/AllResources/游戏原美术素材/Suriyun/UI/UI/Icons/item_icon_scroll.png";
    private const string PotionModelPrefabPath =
        "Assets/AllResources/游戏原美术素材/PolygonDungeon/Prefabs/Items/SM_Item_Potion_01.prefab";
    private static readonly Color SelectedFrameColor = new Color32(255, 255, 255, 200);

    /// <summary>
    /// 首次导入本功能脚本后自动补齐背包数据与掉落资源。
    /// 已经存在 InventoryPanel 时，Setup 会保留手动编辑的 UI，不再因脚本重载覆盖布局和颜色。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void SetupOnceAfterScriptReload()
    {
        if (!NeedsSetup())
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                Setup();
                Debug.Log("INVENTORY_FEATURE_AUTO_SETUP_SUCCEEDED");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    private static bool IsSelectedFrameColor(Color color)
    {
        return Mathf.Approximately(color.r, SelectedFrameColor.r) &&
            Mathf.Approximately(color.g, SelectedFrameColor.g) &&
            Mathf.Approximately(color.b, SelectedFrameColor.b) &&
            Mathf.Approximately(color.a, SelectedFrameColor.a);
    }

    [MenuItem("Tools/Treasure Hunter/Apply Default Inventory Selection Color")]
    private static void UpgradeSelectedFrameColorsInPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
        try
        {
            InventorySlotView[] slots = root.GetComponentsInChildren<InventorySlotView>(true);
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                Transform selectedFrame = slots[i].transform.Find("SelectedFrame");
                Image image = selectedFrame != null ? selectedFrame.GetComponent<Image>() : null;
                if (image == null || IsSelectedFrameColor(image.color))
                {
                    continue;
                }

                image.color = SelectedFrameColor;
                EditorUtility.SetDirty(image);
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("已手动应用背包格子默认选中框颜色。");
    }

    private static bool NeedsSetup()
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(DatabasePath);
        if (database == null ||
            AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(ManaPotionPath) == null ||
            AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(SpiderKingCorePath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(WorldPickupPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(BossLootOrbPrefabPath) == null)
        {
            return true;
        }

        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        InventoryPanel panel = gameplayUi != null ? gameplayUi.GetComponent<InventoryPanel>() : null;
        if (gameplayUi == null || panel == null)
        {
            return true;
        }

        GameObject box = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
        GameObject slimeOne = AssetDatabase.LoadAssetAtPath<GameObject>(SlimeOnePrefabPath);
        GameObject slimeTwo = AssetDatabase.LoadAssetAtPath<GameObject>(SlimeTwoPrefabPath);
        return box == null || box.GetComponent<VaultLootRewardController>() != null ||
            slimeOne == null || slimeOne.GetComponent<MonsterLootDropController>() == null ||
            slimeTwo == null || slimeTwo.GetComponent<MonsterLootDropController>() == null ||
            database.WorldPickupPrefab == null || database.MonsterLootEntries == null ||
            database.MonsterLootEntries.Length != 2 ||
            database.BossLootOrbPrefab == null || database.BossLootEntries == null ||
            database.BossLootEntries.Length != 3;
    }

    [MenuItem("Tools/Treasure Hunter/Setup Inventory Feature")]
    public static void SetupFromMenu()
    {
        Setup();
        Debug.Log("背包配置、药水拾取物、Boss 光球和普通怪 Prefab 装配完成；已有背包 UI 已保留。");
    }

    /// <summary>
    /// 只有用户明确确认时才删除并重建背包视图。
    /// 该入口用于 UI 结构严重损坏后的兜底恢复，正常手动调整布局时不要执行。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/Regenerate Inventory UI (Overwrites Manual Layout)")]
    private static void RegenerateInventoryUiFromMenu()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "重新生成背包 UI",
            "该操作会删除当前 InventoryOverlay 和 InventoryToast，手动调整的布局、颜色和图片都会丢失。是否继续？",
            "继续重建",
            "取消");
        if (!confirmed)
        {
            return;
        }

        UpgradeGameplayUiPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("背包 UI 已按默认模板重新生成。");
    }

    public static void SetupFromCommandLine()
    {
        Setup();
        Debug.Log("INVENTORY_FEATURE_SETUP_SUCCEEDED");
    }

    private static void Setup()
    {
        // UI 是否已经存在必须在装配开始前记录；后续只补数据和掉落资源，不覆盖手动美术调整。
        bool shouldCreateInventoryUi = !HasExistingInventoryUi();

        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/Data");
        EnsureFolder(InventoryFolder);
        EnsureFolder("Assets/Prefabs/World");

        InventoryItemDefinition potion = CreateOrLoadItem(
            PotionPath,
            "healing_potion",
            "生命药水",
            InventoryItemCategory.Consumable,
            InventoryItemRarity.Common,
            PotionIconPath,
            "使用后恢复最大生命值的 30%。生命值已满时不会消耗。",
            20);
        InventoryItemDefinition manaPotion = CreateOrLoadItem(
            ManaPotionPath,
            "mana_potion",
            "魔法药水",
            InventoryItemCategory.Consumable,
            InventoryItemRarity.Common,
            PotionIconPath,
            "使用后恢复最大魔法值的 30%。魔法值已满时不会消耗。",
            20);
        InventoryItemDefinition crystal = CreateOrLoadItem(
            CrystalPath,
            "experience_crystal",
            "经验结晶",
            InventoryItemCategory.Material,
            InventoryItemRarity.Uncommon,
            CrystalIconPath,
            "怪物和 Boss 掉落的能量碎片，可作为后续经验兑换、强化或合成系统的材料。",
            99);
        InventoryItemDefinition scroll = CreateOrLoadItem(
            ScrollPath,
            "ancient_scroll",
            "古代卷轴",
            InventoryItemCategory.Quest,
            InventoryItemRarity.Rare,
            ScrollIconPath,
            "记录着遗迹文字的稀有卷轴。后续可用于任务提交或解锁特殊技能。",
            10);
        InventoryItemDefinition spiderKingCore = CreateOrLoadItem(
            SpiderKingCorePath,
            "spider_king_core",
            "蜘蛛王核心",
            InventoryItemCategory.Material,
            InventoryItemRarity.Epic,
            CrystalIconPath,
            "击败 Spider King 后可能获得的核心材料。后续可扩展为装备打造、技能解锁或任务提交道具。",
            10);

        ConfigurePotionUseData(
            potion,
            "生命药水",
            "使用后恢复最大生命值的 30%。生命值已满时不会消耗。",
            InventoryItemUseEffect.RestoreHealth,
            new Color(1f, 0.36f, 0.36f, 1f));
        ConfigurePotionUseData(
            manaPotion,
            "魔法药水",
            "使用后恢复最大魔法值的 30%。魔法值已满时不会消耗。",
            InventoryItemUseEffect.RestoreMana,
            new Color(0.35f, 0.65f, 1f, 1f));
        ConfigureItemDisplayData(
            crystal,
            "经验结晶",
            "怪物和 Boss 掉落的能量碎片，可作为后续经验兑换、强化或合成系统的材料。",
            InventoryItemCategory.Material,
            InventoryItemRarity.Uncommon,
            99,
            new Color(0.48f, 0.95f, 1f, 1f));
        ConfigureItemDisplayData(
            scroll,
            "古代卷轴",
            "记录着遗迹文字的稀有卷轴。后续可用于任务提交或解锁特殊技能。",
            InventoryItemCategory.Quest,
            InventoryItemRarity.Rare,
            10,
            new Color(1f, 0.72f, 0.25f, 1f));
        ConfigureItemDisplayData(
            spiderKingCore,
            "蜘蛛王核心",
            "击败 Spider King 后可能获得的核心材料。后续可扩展为装备打造、技能解锁或任务提交道具。",
            InventoryItemCategory.Material,
            InventoryItemRarity.Epic,
            10,
            new Color(0.88f, 0.22f, 1f, 1f));

        GameObject worldPickupPrefab = CreateOrUpgradeWorldPickupPrefab();
        GameObject bossLootOrbPrefab = CreateOrUpgradeBossLootOrbPrefab();
        InventoryDatabase database = CreateOrLoadDatabase(
            potion,
            manaPotion,
            crystal,
            scroll,
            spiderKingCore,
            worldPickupPrefab,
            bossLootOrbPrefab);
        if (shouldCreateInventoryUi)
        {
            UpgradeGameplayUiPrefab();
        }
        RemoveVaultLootFromBoxPrefab();
        UpgradeMonsterPrefab(SlimeOnePrefabPath, database);
        UpgradeMonsterPrefab(SlimeTwoPrefabPath, database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// InventoryPanel 组件代表背包视图已经正式装配。
    /// 即使用户改了节点名称、文案、颜色或部分布局，也应该把现有 Prefab 当作唯一美术来源保留下来。
    /// </summary>
    private static bool HasExistingInventoryUi()
    {
        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        return gameplayUi != null && gameplayUi.GetComponent<InventoryPanel>() != null;
    }

    private static InventoryItemDefinition CreateOrLoadItem(
        string assetPath,
        string itemId,
        string displayName,
        InventoryItemCategory category,
        InventoryItemRarity rarity,
        string iconPath,
        string description,
        int maxStack)
    {
        InventoryItemDefinition existing = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        InventoryItemDefinition item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        item.name = displayName;
        AssetDatabase.CreateAsset(item, assetPath);

        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("itemId").stringValue = itemId;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
        serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("maxStack").intValue = maxStack;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void ConfigurePotionUseData(
        InventoryItemDefinition item,
        string displayName,
        string description,
        InventoryItemUseEffect useEffect,
        Color displayTint)
    {
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("category").enumValueIndex = (int)InventoryItemCategory.Consumable;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("maxStack").intValue = 20;
        serialized.FindProperty("useEffect").enumValueIndex = (int)useEffect;
        serialized.FindProperty("restorePercent").floatValue = 0.3f;
        serialized.FindProperty("displayTint").colorValue = displayTint;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static void ConfigureItemDisplayData(
        InventoryItemDefinition item,
        string displayName,
        string description,
        InventoryItemCategory category,
        InventoryItemRarity rarity,
        int maxStack,
        Color displayTint)
    {
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.FindProperty("rarity").enumValueIndex = (int)rarity;
        serialized.FindProperty("description").stringValue = description;
        serialized.FindProperty("maxStack").intValue = Mathf.Max(1, maxStack);
        serialized.FindProperty("useEffect").enumValueIndex = (int)InventoryItemUseEffect.None;
        serialized.FindProperty("restorePercent").floatValue = 0f;
        serialized.FindProperty("displayTint").colorValue = displayTint;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static InventoryDatabase CreateOrLoadDatabase(
        InventoryItemDefinition potion,
        InventoryItemDefinition manaPotion,
        InventoryItemDefinition crystal,
        InventoryItemDefinition scroll,
        InventoryItemDefinition spiderKingCore,
        GameObject worldPickupPrefab,
        GameObject bossLootOrbPrefab)
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(DatabasePath);
        bool isNew = database == null;
        if (isNew)
        {
            database = ScriptableObject.CreateInstance<InventoryDatabase>();
            database.name = "InventoryDatabase";
            AssetDatabase.CreateAsset(database, DatabasePath);
        }

        SerializedObject serialized = new SerializedObject(database);
        SerializedProperty items = serialized.FindProperty("items");
        EnsureObjectReferenceInArray(items, potion);
        EnsureObjectReferenceInArray(items, manaPotion);
        EnsureObjectReferenceInArray(items, crystal);
        EnsureObjectReferenceInArray(items, scroll);
        EnsureObjectReferenceInArray(items, spiderKingCore);

        serialized.FindProperty("capacity").intValue = InventoryModel.DefaultCapacity;
        SerializedProperty lootEntries = serialized.FindProperty("vaultLootEntries");
        lootEntries.arraySize = 0;

        serialized.FindProperty("monsterDropChance").floatValue = 0.1f;
        SerializedProperty monsterEntries = serialized.FindProperty("monsterLootEntries");
        monsterEntries.arraySize = 2;
        ConfigureMonsterLootEntry(monsterEntries.GetArrayElementAtIndex(0), potion, 1f);
        ConfigureMonsterLootEntry(monsterEntries.GetArrayElementAtIndex(1), manaPotion, 1f);
        serialized.FindProperty("worldPickupPrefab").objectReferenceValue = worldPickupPrefab;

        SerializedProperty bossDropOrbCount = serialized.FindProperty("bossDropOrbCount");
        if (bossDropOrbCount != null)
        {
            bossDropOrbCount.intValue = 3;
        }

        SerializedProperty bossEntries = serialized.FindProperty("bossLootEntries");
        if (bossEntries != null)
        {
            bossEntries.arraySize = 3;
            ConfigureLootEntry(bossEntries.GetArrayElementAtIndex(0), crystal, 50f, 2, 4);
            ConfigureLootEntry(bossEntries.GetArrayElementAtIndex(1), scroll, 35f, 1, 1);
            ConfigureLootEntry(bossEntries.GetArrayElementAtIndex(2), spiderKingCore, 15f, 1, 1);
        }

        SerializedProperty bossOrbPrefab = serialized.FindProperty("bossLootOrbPrefab");
        if (bossOrbPrefab != null)
        {
            bossOrbPrefab.objectReferenceValue = bossLootOrbPrefab;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);

        return database;
    }

    private static void EnsureObjectReferenceInArray(
        SerializedProperty array,
        UnityEngine.Object target)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).objectReferenceValue == target)
            {
                return;
            }
        }

        int index = array.arraySize;
        array.InsertArrayElementAtIndex(index);
        array.GetArrayElementAtIndex(index).objectReferenceValue = target;
    }

    private static void ConfigureMonsterLootEntry(
        SerializedProperty entry,
        InventoryItemDefinition item,
        float weight)
    {
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("weight").floatValue = weight;
    }

    private static void ConfigureLootEntry(
        SerializedProperty entry,
        InventoryItemDefinition item,
        float weight,
        int minAmount,
        int maxAmount)
    {
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("minAmount").intValue = minAmount;
        entry.FindPropertyRelative("maxAmount").intValue = maxAmount;
    }

    private static GameObject CreateOrUpgradeWorldPickupPrefab()
    {
        GameObject potionModel = AssetDatabase.LoadAssetAtPath<GameObject>(PotionModelPrefabPath);
        if (potionModel == null)
        {
            throw new InvalidOperationException($"找不到地面药水模型：{PotionModelPrefabPath}");
        }

        GameObject root = new GameObject("WorldItemPickup");
        try
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.65f, 0f);
            trigger.radius = 0.8f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            GameObject visualRootObject = new GameObject("VisualRoot");
            visualRootObject.transform.SetParent(root.transform, false);
            visualRootObject.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(potionModel);
            visual.name = "PotionVisual";
            visual.transform.SetParent(visualRootObject.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one * 0.75f;

            WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
            SerializedObject serialized = new SerializedObject(pickup);
            serialized.FindProperty("visualRoot").objectReferenceValue = visualRootObject.transform;
            Renderer[] renderers = visualRootObject.GetComponentsInChildren<Renderer>(true);
            SerializedProperty tintedRenderers = serialized.FindProperty("tintedRenderers");
            tintedRenderers.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                tintedRenderers.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, WorldPickupPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(WorldPickupPrefabPath);
    }

    private static GameObject CreateOrUpgradeBossLootOrbPrefab()
    {
        GameObject root = new GameObject("BossLootOrbPickup");
        try
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.75f, 0f);
            trigger.radius = 0.95f;

            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;

            WorldItemPickup pickup = root.AddComponent<WorldItemPickup>();
            SerializedObject serialized = new SerializedObject(pickup);
            serialized.FindProperty("createFallbackSphereVisual").boolValue = true;
            serialized.FindProperty("fallbackVisualLocalOffset").vector3Value = new Vector3(0f, 0.75f, 0f);
            serialized.FindProperty("fallbackSphereScale").floatValue = 0.6f;
            serialized.FindProperty("emissionIntensity").floatValue = 2.6f;
            serialized.FindProperty("pointLightIntensity").floatValue = 3.2f;
            serialized.FindProperty("pointLightRange").floatValue = 4.8f;
            serialized.FindProperty("rotationSpeed").floatValue = 115f;
            serialized.FindProperty("bobAmplitude").floatValue = 0.16f;
            serialized.FindProperty("bobFrequency").floatValue = 2.7f;
            serialized.FindProperty("retryInterval").floatValue = 0.25f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BossLootOrbPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(BossLootOrbPrefabPath);
    }

    private static void UpgradeGameplayUiPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到 GameplayUiRoot Prefab：{GameplayUiPrefabPath}");
        }

        try
        {
            Canvas canvas = RequireComponent<Canvas>(root);
            GameSessionUi sessionUi = RequireComponent<GameSessionUi>(root);
            GameplayUiRoot uiRoot = RequireComponent<GameplayUiRoot>(root);
            MiniMapPanelController miniMapPanel = root.GetComponentInChildren<MiniMapPanelController>(true);
            InventoryPanel inventoryPanel = GetOrAddComponent<InventoryPanel>(root);

            DestroyChildIfExists(root.transform, "InventoryOverlay");
            DestroyChildIfExists(root.transform, "InventoryToast");

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            Sprite backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            Sprite slotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SlotSpritePath);
            Sprite selectedSlotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SelectedSlotSpritePath);
            Sprite bagIcon = AssetDatabase.LoadAssetAtPath<Sprite>(BagIconPath);

            InventoryUiBuildResult ui = BuildInventoryUi(
                root.transform,
                font,
                backgroundSprite,
                slotSprite,
                selectedSlotSprite,
                bagIcon);
            WireInventoryPanel(inventoryPanel, canvas, sessionUi, miniMapPanel, ui);
            SetReference(new SerializedObject(sessionUi), "inventoryPanel", inventoryPanel);
            SetReference(new SerializedObject(uiRoot), "inventoryPanel", inventoryPanel);

            if (!inventoryPanel.ValidatePrefabReferences(true))
            {
                throw new InvalidOperationException("InventoryPanel 静态引用校验失败。");
            }

            // Prefab 编辑状态保持可见，方便在 Scene 视图中手动调整；运行时由 InventoryPanel.Start 统一隐藏。
            ui.OverlayRoot.SetActive(true);
            ui.ToastRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static InventoryUiBuildResult BuildInventoryUi(
        Transform root,
        Font font,
        Sprite backgroundSprite,
        Sprite slotSprite,
        Sprite selectedSlotSprite,
        Sprite bagIcon)
    {
        GameObject overlay = CreateImageObject(
            "InventoryOverlay",
            root,
            null,
            new Color(0f, 0f, 0f, 0.72f),
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        GameObject window = CreateImageObject(
            "InventoryWindow",
            overlay.transform,
            backgroundSprite,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(940f, 840f));
        AddOutline(window, new Color(0.12f, 0.07f, 0.04f, 0.9f), new Vector2(3f, -3f));

        Text title = CreateText(
            "Title",
            window.transform,
            font,
            "冒 险 者 背 包",
            30,
            new Color(0.95f, 0.81f, 0.55f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -42f),
            new Vector2(500f, 52f));
        AddTextOutline(title, new Color(0.12f, 0.06f, 0.03f, 1f));

        Button closeButton = CreateButton(
            "CloseButton",
            window.transform,
            font,
            "×",
            30,
            new Color(0.24f, 0.12f, 0.08f, 0.96f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-45f, -42f),
            new Vector2(48f, 48f));

        CreateImageObject(
            "BagIcon",
            window.transform,
            bagIcon,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-365f, 205f),
            new Vector2(136f, 136f));

        Text capacityText = CreateText(
            "CapacityText",
            window.transform,
            font,
            "容量  0 / 24",
            20,
            new Color(0.93f, 0.84f, 0.68f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-365f, 103f),
            new Vector2(210f, 38f));

        Text helpText = CreateText(
            "HelpText",
            window.transform,
            font,
            "击败小怪或 Boss 后拾取掉落\n点击格子查看详情\n\nB  打开 / 关闭\nESC  返回游戏",
            17,
            new Color(0.82f, 0.74f, 0.62f, 1f),
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-365f, -35f),
            new Vector2(220f, 170f));
        helpText.lineSpacing = 1.2f;

        GameObject gridRoot = CreateRectObject(
            "ItemGrid",
            window.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-62f, -12f),
            new Vector2(470f, 320f));
        InventorySlotView[] slots = BuildSlots(gridRoot.transform, font, slotSprite, selectedSlotSprite);

        Text emptyStateText = CreateText(
            "EmptyStateText",
            gridRoot.transform,
            font,
            "背包还是空的\n击败小怪或 Boss 后拾取战利品吧",
            21,
            new Color(0.78f, 0.7f, 0.6f, 0.9f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(360f, 100f));

        GameObject detailFrame = CreateImageObject(
            "DetailFrame",
            window.transform,
            slotSprite,
            new Color(0.86f, 0.75f, 0.62f, 0.88f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, 175f),
            new Vector2(118f, 118f));
        Image detailIcon = CreateImageObject(
            "DetailIcon",
            detailFrame.transform,
            null,
            Color.white,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero).GetComponent<Image>();
        RectTransform detailIconRect = detailIcon.rectTransform;
        detailIconRect.offsetMin = new Vector2(10f, 10f);
        detailIconRect.offsetMax = new Vector2(-10f, -10f);
        detailIcon.preserveAspect = true;

        Text detailName = CreateText(
            "DetailNameText",
            window.transform,
            font,
            "未选择物品",
            23,
            new Color(0.9f, 0.82f, 0.7f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, 88f),
            new Vector2(220f, 42f));
        Text detailMeta = CreateText(
            "DetailMetaText",
            window.transform,
            font,
            "点击左侧格子查看详情",
            16,
            new Color(0.76f, 0.69f, 0.61f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, 52f),
            new Vector2(220f, 32f));
        Text detailCount = CreateText(
            "DetailCountText",
            window.transform,
            font,
            string.Empty,
            16,
            new Color(0.91f, 0.83f, 0.7f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, 18f),
            new Vector2(220f, 30f));
        Text detailDescription = CreateText(
            "DetailDescriptionText",
            window.transform,
            font,
            "击败小怪或 Boss 后拾取掉落物，可以获得物品。",
            17,
            new Color(0.84f, 0.78f, 0.69f, 1f),
            TextAnchor.UpperLeft,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, -105f),
            new Vector2(210f, 180f));

        Button useButton = CreateButton(
            "UseButton",
            window.transform,
            font,
            "使  用",
            20,
            new Color(0.3f, 0.16f, 0.08f, 0.96f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(350f, -235f),
            new Vector2(150f, 44f));
        AddOutline(useButton.gameObject, new Color(0.72f, 0.49f, 0.22f, 0.95f), new Vector2(2f, -2f));

        Text footer = CreateText(
            "FooterText",
            window.transform,
            font,
            "战利品会自动整理并优先堆叠",
            17,
            new Color(0.72f, 0.64f, 0.54f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 45f),
            new Vector2(500f, 32f));
        AddTextOutline(footer, new Color(0.08f, 0.04f, 0.02f, 0.85f));

        GameObject toast = CreateImageObject(
            "InventoryToast",
            root,
            null,
            new Color(0.08f, 0.06f, 0.045f, 0.94f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -110f),
            new Vector2(540f, 58f));
        AddOutline(toast, new Color(0.76f, 0.57f, 0.3f, 0.9f), new Vector2(2f, -2f));
        Text toastText = CreateText(
            "ToastText",
            toast.transform,
            font,
            "获得物品",
            20,
            new Color(0.95f, 0.82f, 0.55f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        RectTransform toastTextRect = toastText.rectTransform;
        toastTextRect.offsetMin = new Vector2(14f, 6f);
        toastTextRect.offsetMax = new Vector2(-14f, -6f);

        return new InventoryUiBuildResult(
            overlay,
            closeButton,
            capacityText,
            emptyStateText,
            detailIcon,
            detailName,
            detailMeta,
            detailCount,
            detailDescription,
            useButton,
            slots,
            toast,
            toastText);
    }

    private static InventorySlotView[] BuildSlots(
        Transform gridRoot,
        Font font,
        Sprite slotSprite,
        Sprite selectedSlotSprite)
    {
        const int columns = 6;
        const int rows = 4;
        const float slotSize = 68f;
        const float spacing = 8f;
        float totalWidth = columns * slotSize + (columns - 1) * spacing;
        float totalHeight = rows * slotSize + (rows - 1) * spacing;
        InventorySlotView[] result = new InventorySlotView[columns * rows];

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int index = row * columns + column;
                float x = -totalWidth * 0.5f + slotSize * 0.5f + column * (slotSize + spacing);
                float y = totalHeight * 0.5f - slotSize * 0.5f - row * (slotSize + spacing);
                GameObject slot = CreateImageObject(
                    $"Slot_{index + 1:00}",
                    gridRoot,
                    slotSprite,
                    new Color(0.65f, 0.58f, 0.52f, 0.78f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(x, y),
                    new Vector2(slotSize, slotSize));
                Image frame = slot.GetComponent<Image>();
                Button button = slot.AddComponent<Button>();
                button.targetGraphic = frame;

                Image icon = CreateImageObject(
                    "Icon",
                    slot.transform,
                    null,
                    Color.white,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero).GetComponent<Image>();
                icon.rectTransform.offsetMin = new Vector2(8f, 8f);
                icon.rectTransform.offsetMax = new Vector2(-8f, -8f);
                icon.preserveAspect = true;

                Text count = CreateText(
                    "CountText",
                    slot.transform,
                    font,
                    string.Empty,
                    17,
                    Color.white,
                    TextAnchor.LowerRight,
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(-6f, 4f),
                    new Vector2(42f, 24f));
                count.fontStyle = FontStyle.Bold;
                AddTextOutline(count, new Color(0f, 0f, 0f, 0.95f));

                GameObject selected = CreateImageObject(
                    "SelectedFrame",
                    slot.transform,
                    selectedSlotSprite != null ? selectedSlotSprite : slotSprite,
                    SelectedFrameColor,
                    new Vector2(0f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0.5f, 0.5f),
                    Vector2.zero,
                    Vector2.zero);
                RectTransform selectedRect = selected.GetComponent<RectTransform>();
                selectedRect.offsetMin = new Vector2(-3f, -3f);
                selectedRect.offsetMax = new Vector2(3f, 3f);
                selected.SetActive(false);

                InventorySlotView view = slot.AddComponent<InventorySlotView>();
                SerializedObject serialized = new SerializedObject(view);
                SetReference(serialized, "button", button);
                SetReference(serialized, "frameImage", frame);
                SetReference(serialized, "iconImage", icon);
                SetReference(serialized, "countText", count);
                SetReference(serialized, "selectedFrame", selected);
                result[index] = view;
            }
        }

        return result;
    }

    private static void WireInventoryPanel(
        InventoryPanel panel,
        Canvas canvas,
        GameSessionUi sessionUi,
        MiniMapPanelController miniMapPanel,
        InventoryUiBuildResult ui)
    {
        SerializedObject serialized = new SerializedObject(panel);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "sessionUi", sessionUi);
        SetReference(serialized, "miniMapPanel", miniMapPanel);
        SetReference(serialized, "panelRoot", ui.OverlayRoot);
        SetReference(serialized, "closeButton", ui.CloseButton);
        SetReference(serialized, "capacityText", ui.CapacityText);
        SetReference(serialized, "emptyStateText", ui.EmptyStateText);
        SetReference(serialized, "detailIcon", ui.DetailIcon);
        SetReference(serialized, "detailNameText", ui.DetailNameText);
        SetReference(serialized, "detailMetaText", ui.DetailMetaText);
        SetReference(serialized, "detailCountText", ui.DetailCountText);
        SetReference(serialized, "detailDescriptionText", ui.DetailDescriptionText);
        SetReference(serialized, "useButton", ui.UseButton);
        SetReference(serialized, "toastRoot", ui.ToastRoot);
        SetReference(serialized, "toastText", ui.ToastText);

        SerializedProperty slots = serialized.FindProperty("slotViews");
        slots.arraySize = ui.Slots.Length;
        for (int i = 0; i < ui.Slots.Length; i++)
        {
            slots.GetArrayElementAtIndex(i).objectReferenceValue = ui.Slots[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(panel);
    }

    private static void RemoveVaultLootFromBoxPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BoxPrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到宝箱 Prefab：{BoxPrefabPath}");
        }

        try
        {
            BoxCo box = root.GetComponentInChildren<BoxCo>(true);
            if (box == null)
            {
                throw new InvalidOperationException("Box.prefab 中没有 BoxCo。 ");
            }

            VaultLootRewardController reward = box.GetComponent<VaultLootRewardController>();
            if (reward != null)
            {
                UnityEngine.Object.DestroyImmediate(reward, true);
                PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeMonsterPrefab(string prefabPath, InventoryDatabase database)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到普通怪 Prefab：{prefabPath}");
        }

        try
        {
            SlimeCo monster = root.GetComponentInChildren<SlimeCo>(true);
            if (monster == null)
            {
                throw new InvalidOperationException($"{prefabPath} 中没有 SlimeCo。");
            }

            MonsterLootDropController controller =
                GetOrAddComponent<MonsterLootDropController>(monster.gameObject);
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("inventoryDatabase").objectReferenceValue = database;
            serialized.FindProperty("pickupLifetimeSeconds").floatValue = 45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject CreateRectObject(
        string name,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return gameObject;
    }

    private static GameObject CreateImageObject(
        string name,
        Transform parent,
        Sprite sprite,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = CreateRectObject(
            name,
            parent,
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            sizeDelta);
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = Image.Type.Simple;
        return gameObject;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        string value,
        int fontSize,
        Color color,
        TextAnchor alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = CreateRectObject(
            name,
            parent,
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            sizeDelta);
        Text text = gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        Font font,
        string label,
        int fontSize,
        Color backgroundColor,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        GameObject gameObject = CreateImageObject(
            name,
            parent,
            null,
            backgroundColor,
            anchorMin,
            anchorMax,
            pivot,
            anchoredPosition,
            sizeDelta);
        Image image = gameObject.GetComponent<Image>();
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.82f, 0.55f, 1f);
        colors.pressedColor = new Color(0.72f, 0.5f, 0.3f, 1f);
        button.colors = colors;
        Text text = CreateText(
            "Label",
            gameObject.transform,
            font,
            label,
            fontSize,
            new Color(0.95f, 0.85f, 0.68f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void AddTextOutline(Text text, Color color)
    {
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    private static void DestroyChildIfExists(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        int separator = folderPath.LastIndexOf('/');
        if (separator <= 0)
        {
            return;
        }

        string parent = folderPath.Substring(0, separator);
        string name = folderPath.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static T RequireComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            throw new InvalidOperationException($"{target.name} 缺少组件 {typeof(T).Name}。");
        }

        return component;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} 缺少序列化字段 {propertyName}。");
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private sealed class InventoryUiBuildResult
    {
        public InventoryUiBuildResult(
            GameObject overlayRoot,
            Button closeButton,
            Text capacityText,
            Text emptyStateText,
            Image detailIcon,
            Text detailNameText,
            Text detailMetaText,
            Text detailCountText,
            Text detailDescriptionText,
            Button useButton,
            InventorySlotView[] slots,
            GameObject toastRoot,
            Text toastText)
        {
            OverlayRoot = overlayRoot;
            CloseButton = closeButton;
            CapacityText = capacityText;
            EmptyStateText = emptyStateText;
            DetailIcon = detailIcon;
            DetailNameText = detailNameText;
            DetailMetaText = detailMetaText;
            DetailCountText = detailCountText;
            DetailDescriptionText = detailDescriptionText;
            UseButton = useButton;
            Slots = slots;
            ToastRoot = toastRoot;
            ToastText = toastText;
        }

        public GameObject OverlayRoot { get; }
        public Button CloseButton { get; }
        public Text CapacityText { get; }
        public Text EmptyStateText { get; }
        public Image DetailIcon { get; }
        public Text DetailNameText { get; }
        public Text DetailMetaText { get; }
        public Text DetailCountText { get; }
        public Text DetailDescriptionText { get; }
        public Button UseButton { get; }
        public InventorySlotView[] Slots { get; }
        public GameObject ToastRoot { get; }
        public Text ToastText { get; }
    }
}
#endif
