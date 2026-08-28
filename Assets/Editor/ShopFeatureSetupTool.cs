#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Fungi 商店功能装配工具：生成经济配置、商品目录、金币对象池 Prefab、商人 Prefab 和淘宝商店 UI。
/// 普通刷新只补缺失资源；已有商店 UI 会保留手调布局，只有带确认的重建菜单会覆盖商店子树。
/// </summary>
public static class ShopFeatureSetupTool
{
    private const int CurrentVisualLayoutVersion = 5;
    private const float ProductScrollSensitivity = 60f;
    private const string ShopFolder = "Assets/Resources/Data/Shop";
    private const string InventoryFolder = "Assets/Resources/Data/Inventory";
    private const string CatalogPath = ShopFolder + "/ShopCatalog.asset";
    private const string EconomyConfigPath = ShopFolder + "/EconomyConfig.asset";
    private const string GoldMaterialPath = ShopFolder + "/WorldGold.mat";
    private const string GoldPrefabPath = "Assets/Prefabs/World/WorldGoldPickup.prefab";
    private const string MerchantPrefabPath = "Assets/Prefabs/NPC/MerchantFungi.prefab";
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    private const string InventoryDatabasePath = InventoryFolder + "/InventoryDatabase.asset";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string SlimeOnePrefabPath = "Assets/Prefabs/Slime1.prefab";
    private const string SlimeTwoPrefabPath = "Assets/Prefabs/Slime2.prefab";
    private const string BoxPrefabPath = "Assets/Prefabs/Box.prefab";
    private const string OriginalFungiPrefabPath = "Assets/AllResources/Monsters Ultimate Pack 01 Cute Series/Fungi Cute Series/Prefabs/Fungi.prefab";
    private const string EquipmentSpriteFolder = "Assets/AllResources/淘宝ui素材/RuntimeSprites/Equipment/";
    private const string ProgressionSpriteFolder = "Assets/AllResources/淘宝ui素材/RuntimeSprites/Progression/";
    private const string ShopSpriteFolder = "Assets/AllResources/淘宝ui素材/RuntimeSprites/Shop/";
    private const string QuestListBackgroundPath = ProgressionSpriteFolder + "UI_Progression_Guild_List.png";
    private const string QuestPanelBackgroundPath = ProgressionSpriteFolder + "UI_Progression_Guild_Background.png";
    private const string QuestActionButtonPath = ProgressionSpriteFolder + "UI_Progression_Missions_List_ButtonGreen_Btn_Normal.png";

    private static readonly Color DialogueSpeakerColor = new Color32(255, 226, 151, 255);
    private static readonly Color DialogueBodyColor = new Color32(241, 220, 169, 255);
    private static readonly Color InteractionPromptTextColor = new Color32(255, 239, 193, 255);
    private static readonly Color ShopGoldColor = new Color32(255, 181, 46, 255);
    private static readonly Color ProductNameColor = new Color32(255, 214, 107, 255);
    private static readonly Color ProductDescriptionColor = new Color32(238, 233, 223, 255);
    private static readonly Color ProductPriceColor = new Color32(255, 181, 46, 255);
    private static readonly Color ReadabilityOutlineColor = new Color32(18, 14, 22, 217);

    private static readonly StarterEquipmentData[] StarterEquipment =
    {
        new StarterEquipmentData("MerchantTrainingHammer.asset", "merchant_training_hammer", "练习战锤", EquipmentSlotType.Weapon,
            "UI_Equipment_Item_Slot04_Slot02.png", "商人准备的入门战锤。", new EquipmentStatModifier(EquipmentStatType.Attack, 6f)),
        new StarterEquipmentData("MerchantTravelerArmor.asset", "merchant_traveler_armor", "旅人护甲", EquipmentSlotType.Armor,
            "UI_Equipment_Item_Slot04_Slot05.png", "适合长途冒险的轻便护甲。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 45f)),
        new StarterEquipmentData("MerchantOakShield.asset", "merchant_oak_shield", "橡木圆盾", EquipmentSlotType.Shield,
            "UI_Equipment_Item_Slot04_Slot01.png", "结实耐用的橡木圆盾。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 30f), new EquipmentStatModifier(EquipmentStatType.DamageReduction, 0.01f)),
        new StarterEquipmentData("MerchantHunterGloves.asset", "merchant_hunter_gloves", "猎手手套", EquipmentSlotType.Gloves,
            "UI_Equipment_Item_Slot05_Slot03.png", "帮助冒险者抓住致命时机。", new EquipmentStatModifier(EquipmentStatType.CritChance, 0.02f)),
        new StarterEquipmentData("MerchantLightstepBoots.asset", "merchant_lightstep_boots", "轻步靴", EquipmentSlotType.Boots,
            "UI_Equipment_Item_Slot05_Slot06.png", "轻巧而灵活的旅行靴。", new EquipmentStatModifier(EquipmentStatType.MoveSpeed, 0.15f), new EquipmentStatModifier(EquipmentStatType.DodgeChance, 0.01f)),
        new StarterEquipmentData("MerchantCopperRing.asset", "merchant_copper_ring", "铜纹戒指", EquipmentSlotType.Ring,
            "UI_Equipment_Item_Slot03.png", "蕴含微弱魔力的戒指，十级后可装备。", new EquipmentStatModifier(EquipmentStatType.MaxMp, 25f), new EquipmentStatModifier(EquipmentStatType.LifeSteal, 0.005f))
    };

    private static readonly CatalogData[] CatalogDefinitions =
    {
        new CatalogData("HealingPotion.asset", 25, ShopCategory.Consumable, false),
        new CatalogData("ManaPotion.asset", 25, ShopCategory.Consumable, false),
        new CatalogData("ExperienceCrystal.asset", 50, ShopCategory.Material, false),
        new CatalogData("AncientScroll.asset", 100, ShopCategory.Material, false),
        new CatalogData("MerchantTrainingHammer.asset", 120, ShopCategory.Equipment, true),
        new CatalogData("MerchantTravelerArmor.asset", 150, ShopCategory.Equipment, true),
        new CatalogData("MerchantOakShield.asset", 150, ShopCategory.Equipment, true),
        new CatalogData("MerchantHunterGloves.asset", 120, ShopCategory.Equipment, true),
        new CatalogData("MerchantLightstepBoots.asset", 130, ShopCategory.Equipment, true),
        new CatalogData("MerchantCopperRing.asset", 220, ShopCategory.Equipment, true),
        new CatalogData("BossIronWarAxe.asset", 360, ShopCategory.Equipment, true),
        new CatalogData("BossStoneplateArmor.asset", 420, ShopCategory.Equipment, true),
        new CatalogData("BossWoodguardShield.asset", 400, ShopCategory.Equipment, true),
        new CatalogData("BossFangGloves.asset", 340, ShopCategory.Equipment, true),
        new CatalogData("BossWindleafBoots.asset", 350, ShopCategory.Equipment, true),
        new CatalogData("BossRubyRing.asset", 480, ShopCategory.Equipment, true)
    };

    [InitializeOnLoadMethod]
    private static void SetupOnceAfterScriptReload()
    {
        bool needsSetup = NeedsSetup();
        bool needsReadabilityFix = NeedsReadabilityFix();
        if (!needsSetup && !needsReadabilityFix)
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            try
            {
                if (needsSetup)
                {
                    Setup(false);
                    Debug.Log("SHOP_FEATURE_AUTO_SETUP_SUCCEEDED");
                }

                if (NeedsReadabilityFix())
                {
                    var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                    if (prefabStage != null && prefabStage.assetPath == GameplayUiPrefabPath)
                    {
                        Debug.LogWarning("GameplayUiRoot 正在 Prefab Mode 中编辑，已跳过自动可读性迁移；请保存退出后执行 Apply Shop Readability Fixes。");
                        return;
                    }

                    ApplyReadabilityFixesToGameplayUiPrefab();
                    Debug.Log("SHOP_READABILITY_AUTO_MIGRATION_SUCCEEDED");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Treasure Hunter/Shop/Apply Data and World Setup")]
    private static void ApplySetupMenu()
    {
        Setup(false);
        Debug.Log("商店数据、掉落和场景装配已刷新；已有商店布局保持不变。");
    }

    /// <summary>CI/批处理入口：与安全刷新菜单一致，不覆盖已有手调商店布局。</summary>
    public static void SetupFromCommandLine()
    {
        Setup(false);
        // 批处理不能把 MainScene 留成下一轮 EditMode 测试的活动场景，否则场景里的物理对象会污染独立单元测试。
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("SHOP_FEATURE_BATCH_SETUP_SUCCEEDED");
    }

    [MenuItem("Tools/Treasure Hunter/Shop/Apply Shop Readability Fixes")]
    private static void ApplyReadabilityFixesMenu()
    {
        ApplyReadabilityFixesToGameplayUiPrefab();
        Debug.Log("商人对话和商品卡可读性修复已应用；商店外层与背包装备布局保持不变。");
    }

    /// <summary>批处理入口：只修复现有商店的指定视觉节点，不重建 MerchantShopFeature。</summary>
    public static void ApplyReadabilityFixesFromCommandLine()
    {
        ApplyReadabilityFixesToGameplayUiPrefab();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Debug.Log("SHOP_READABILITY_FIX_SUCCEEDED");
    }

    [MenuItem("Tools/Treasure Hunter/Shop/Open Shop Layout For Editing")]
    private static void OpenShopLayoutForEditing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("无法打开商店 UI", $"找不到 {GameplayUiPrefabPath}", "确定");
            return;
        }

        AssetDatabase.OpenAsset(prefab);
        Selection.activeObject = prefab;
    }

    [MenuItem("Tools/Treasure Hunter/Shop/Regenerate Shop UI (Confirm)")]
    private static void RegenerateShopUiMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "重建商店 UI",
                "这会覆盖 GameplayUiRoot 中 MerchantShopFeature 子树的手动布局。背包装备 UI 不受影响。是否继续？",
                "重建",
                "取消"))
        {
            return;
        }

        Setup(true);
    }

    private static bool NeedsSetup()
    {
        if (AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath) == null ||
            AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyConfigPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(GoldPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath) == null)
        {
            return true;
        }

        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        if (gameplayUi == null || gameplayUi.GetComponent<MerchantShopPanel>() == null ||
            gameplayUi.GetComponent<GoldHudView>() == null)
        {
            return true;
        }

        for (int i = 0; i < StarterEquipment.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(InventoryFolder + "/" + StarterEquipment[i].AssetName) == null)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NeedsReadabilityFix()
    {
        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        MerchantShopPanel panel = gameplayUi != null ? gameplayUi.GetComponent<MerchantShopPanel>() : null;
        if (panel == null)
        {
            return false;
        }

        SerializedObject serialized = new SerializedObject(panel);
        SerializedProperty version = serialized.FindProperty("visualLayoutVersion");
        return version == null || version.intValue < CurrentVisualLayoutVersion;
    }

    private static void Setup(bool rebuildShopUi)
    {
        EnsureFolder("Assets/Prefabs", "NPC");
        EnsureFolder("Assets/Resources/Data", "Shop");

        InventoryItemDefinition[] starterItems = CreateStarterEquipment();
        AddItemsToInventoryDatabase(starterItems);
        GameObject goldPrefab = CreateOrUpgradeGoldPrefab();
        EconomyConfig economyConfig = CreateOrUpgradeEconomyConfig(goldPrefab);
        CreateOrUpgradeCatalog();
        CreateOrUpgradeMerchantPrefab();
        UpgradeRewardPrefabs(economyConfig);
        UpgradeGameplayUiPrefab(rebuildShopUi);
        UpgradeMainSceneMerchant();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static InventoryItemDefinition[] CreateStarterEquipment()
    {
        var result = new InventoryItemDefinition[StarterEquipment.Length];
        for (int i = 0; i < StarterEquipment.Length; i++)
        {
            StarterEquipmentData data = StarterEquipment[i];
            string path = InventoryFolder + "/" + data.AssetName;
            InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<InventoryItemDefinition>();
                item.name = System.IO.Path.GetFileNameWithoutExtension(data.AssetName);
                AssetDatabase.CreateAsset(item, path);
            }

            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = data.ItemId;
            serialized.FindProperty("displayName").stringValue = data.DisplayName;
            serialized.FindProperty("category").enumValueIndex = (int)InventoryItemCategory.Equipment;
            serialized.FindProperty("rarity").enumValueIndex = (int)InventoryItemRarity.Uncommon;
            serialized.FindProperty("icon").objectReferenceValue = LoadSprite(EquipmentSpriteFolder + data.IconName);
            serialized.FindProperty("description").stringValue = data.Description;
            serialized.FindProperty("maxStack").intValue = 1;
            serialized.FindProperty("useEffect").enumValueIndex = (int)InventoryItemUseEffect.None;
            serialized.FindProperty("restorePercent").floatValue = 0f;
            serialized.FindProperty("displayTint").colorValue = Color.white;
            serialized.FindProperty("equipmentSlot").enumValueIndex = (int)data.Slot;
            SerializedProperty modifiers = serialized.FindProperty("equipmentStatModifiers");
            modifiers.arraySize = data.Modifiers.Length;
            for (int modifierIndex = 0; modifierIndex < data.Modifiers.Length; modifierIndex++)
            {
                SerializedProperty modifier = modifiers.GetArrayElementAtIndex(modifierIndex);
                modifier.FindPropertyRelative("statType").enumValueIndex = (int)data.Modifiers[modifierIndex].StatType;
                modifier.FindPropertyRelative("value").floatValue = data.Modifiers[modifierIndex].Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            result[i] = item;
        }

        return result;
    }

    private static void AddItemsToInventoryDatabase(IEnumerable<InventoryItemDefinition> starterItems)
    {
        InventoryDatabase database = AssetDatabase.LoadAssetAtPath<InventoryDatabase>(InventoryDatabasePath);
        if (database == null)
        {
            throw new InvalidOperationException("请先运行背包装备生成工具，InventoryDatabase 不存在。");
        }

        SerializedObject serialized = new SerializedObject(database);
        SerializedProperty items = serialized.FindProperty("items");
        foreach (InventoryItemDefinition item in starterItems)
        {
            bool exists = false;
            for (int i = 0; i < items.arraySize; i++)
            {
                if (items.GetArrayElementAtIndex(i).objectReferenceValue == item)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                int index = items.arraySize;
                items.InsertArrayElementAtIndex(index);
                items.GetArrayElementAtIndex(index).objectReferenceValue = item;
            }
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
    }

    private static ShopCatalog CreateOrUpgradeCatalog()
    {
        ShopCatalog catalog = AssetDatabase.LoadAssetAtPath<ShopCatalog>(CatalogPath);
        if (catalog == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(CatalogPath) != null)
            {
                AssetDatabase.DeleteAsset(CatalogPath);
            }
            catalog = ScriptableObject.CreateInstance<ShopCatalog>();
            catalog.name = "ShopCatalog";
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty entries = serialized.FindProperty("entries");
        entries.arraySize = CatalogDefinitions.Length;
        for (int i = 0; i < CatalogDefinitions.Length; i++)
        {
            CatalogData data = CatalogDefinitions[i];
            InventoryItemDefinition item = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(InventoryFolder + "/" + data.AssetName);
            if (item == null)
            {
                throw new InvalidOperationException($"商店商品资源不存在：{data.AssetName}");
            }

            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("entryId").stringValue = item.ItemId;
            entry.FindPropertyRelative("item").objectReferenceValue = item;
            entry.FindPropertyRelative("price").longValue = data.Price;
            entry.FindPropertyRelative("category").enumValueIndex = (int)data.Category;
            entry.FindPropertyRelative("limitedOncePerCharacter").boolValue = data.Limited;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
        return catalog;
    }

    private static EconomyConfig CreateOrUpgradeEconomyConfig(GameObject goldPrefab)
    {
        EconomyConfig config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(EconomyConfigPath);
        if (config == null)
        {
            if (AssetDatabase.LoadMainAssetAtPath(EconomyConfigPath) != null)
            {
                AssetDatabase.DeleteAsset(EconomyConfigPath);
            }
            config = ScriptableObject.CreateInstance<EconomyConfig>();
            config.name = "EconomyConfig";
            AssetDatabase.CreateAsset(config, EconomyConfigPath);
        }

        SerializedObject serialized = new SerializedObject(config);
        serialized.FindProperty("slimeOneMinGold").intValue = 1;
        serialized.FindProperty("slimeOneMaxGold").intValue = 2;
        serialized.FindProperty("slimeTwoMinGold").intValue = 2;
        serialized.FindProperty("slimeTwoMaxGold").intValue = 3;
        serialized.FindProperty("vaultBaseGold").intValue = 30;
        serialized.FindProperty("vaultStepGold").intValue = 5;
        serialized.FindProperty("vaultStepCap").intValue = 4;
        serialized.FindProperty("bossBaseGold").intValue = 150;
        serialized.FindProperty("bossStepGold").intValue = 25;
        serialized.FindProperty("bossMaxGold").intValue = 300;
        serialized.FindProperty("worldGoldPickupPrefab").objectReferenceValue = goldPrefab;
        serialized.FindProperty("importantPickupLifetimeSeconds").floatValue = 90f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        return config;
    }

    private static GameObject CreateOrUpgradeGoldPrefab()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(GoldMaterialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard")) { name = "WorldGold" };
            material.color = new Color(1f, 0.62f, 0.05f, 1f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1f, 0.28f, 0.02f, 1f) * 1.5f);
            AssetDatabase.CreateAsset(material, GoldMaterialPath);
        }

        GameObject root = new GameObject("WorldGoldPickup");
        try
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            WorldGoldPickup pickup = root.AddComponent<WorldGoldPickup>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "GoldCoinVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            visual.transform.localScale = new Vector3(0.42f, 0.08f, 0.42f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = material;

            Light glow = visual.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.55f, 0.08f, 1f);
            glow.range = 2.8f;
            glow.intensity = 1.6f;

            SerializedObject serialized = new SerializedObject(pickup);
            serialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
            serialized.FindProperty("rotationSpeed").floatValue = 110f;
            serialized.FindProperty("bobAmplitude").floatValue = 0.15f;
            serialized.FindProperty("bobFrequency").floatValue = 2.5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GoldPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(GoldPrefabPath);
    }

    private static GameObject CreateOrUpgradeMerchantPrefab()
    {
        GameObject original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalFungiPrefabPath);
        if (original == null)
        {
            throw new InvalidOperationException($"找不到原始 Fungi 模型：{OriginalFungiPrefabPath}");
        }

        GameObject root = new GameObject("MerchantFungi");
        try
        {
            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 3f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            root.AddComponent<MerchantNpcController>();

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(original);
            model.name = "FungiModel";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(root, MerchantPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
    }

    private static void UpgradeRewardPrefabs(EconomyConfig config)
    {
        UpgradeMonsterRewardPrefab(SlimeOnePrefabPath, config);
        UpgradeMonsterRewardPrefab(SlimeTwoPrefabPath, config);

        GameObject root = PrefabUtility.LoadPrefabContents(BoxPrefabPath);
        try
        {
            BoxCo vault = root.GetComponentInChildren<BoxCo>(true);
            if (vault == null)
            {
                throw new InvalidOperationException("Box.prefab 中找不到 BoxCo。");
            }

            VaultGoldRewardController reward = GetOrAddComponent<VaultGoldRewardController>(vault.gameObject);
            SerializedObject serialized = new SerializedObject(reward);
            serialized.FindProperty("economyConfig").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BoxPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeMonsterRewardPrefab(string path, EconomyConfig config)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SlimeCo slime = root.GetComponentInChildren<SlimeCo>(true);
            if (slime == null)
            {
                throw new InvalidOperationException($"{path} 中找不到 SlimeCo。");
            }

            MonsterGoldRewardController reward = GetOrAddComponent<MonsterGoldRewardController>(slime.gameObject);
            SerializedObject serialized = new SerializedObject(reward);
            serialized.FindProperty("economyConfig").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradeGameplayUiPrefab(bool rebuildShopUi)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
        try
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            GameSessionUi sessionUi = root.GetComponent<GameSessionUi>();
            InventoryPanel inventoryPanel = root.GetComponent<InventoryPanel>();
            GameplayUiRoot gameplayUiRoot = root.GetComponent<GameplayUiRoot>();
            if (canvas == null || sessionUi == null || inventoryPanel == null || gameplayUiRoot == null)
            {
                throw new InvalidOperationException("GameplayUiRoot.prefab 缺少既有 Canvas、Session、Inventory 或 Root 组件。");
            }

            Transform existing = canvas.transform.Find("MerchantShopFeature");
            bool needsBuild = existing == null || root.GetComponent<MerchantShopPanel>() == null || root.GetComponent<GoldHudView>() == null;
            if (!rebuildShopUi && !needsBuild)
            {
                return;
            }

            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            MerchantShopPanel oldPanel = root.GetComponent<MerchantShopPanel>();
            if (oldPanel != null) UnityEngine.Object.DestroyImmediate(oldPanel);
            GoldHudView oldHud = root.GetComponent<GoldHudView>();
            if (oldHud != null) UnityEngine.Object.DestroyImmediate(oldHud);

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            MerchantUiBuildResult ui = BuildMerchantUi(canvas.transform, font);
            MerchantShopPanel panel = root.AddComponent<MerchantShopPanel>();
            GoldHudView goldHud = root.AddComponent<GoldHudView>();
            ConfigureMerchantPanel(panel, canvas, sessionUi, inventoryPanel, ui);

            SerializedObject hudSerialized = new SerializedObject(goldHud);
            hudSerialized.FindProperty("goldText").objectReferenceValue = ui.HudGoldText;
            hudSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject sessionSerialized = new SerializedObject(sessionUi);
            sessionSerialized.FindProperty("merchantShopPanel").objectReferenceValue = panel;
            sessionSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject rootSerialized = new SerializedObject(gameplayUiRoot);
            rootSerialized.FindProperty("merchantShopPanel").objectReferenceValue = panel;
            rootSerialized.FindProperty("goldHudView").objectReferenceValue = goldHud;
            rootSerialized.ApplyModifiedPropertiesWithoutUndo();

            ui.PromptRoot.SetActive(false);
            ui.DialogueRoot.SetActive(false);
            ui.ShopRoot.SetActive(false);
            ui.ToastRoot.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static MerchantUiBuildResult BuildMerchantUi(Transform canvas, Font font)
    {
        GameObject feature = CreateRectObject("MerchantShopFeature", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject hud = CreateImage("GoldHud", feature.transform, LoadSprite(EquipmentSpriteFolder + "UI_Equipment_Top_Resource_Coin.png"), Color.white,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-185f, -56f), new Vector2(330f, 82f));
        Text hudGold = CreateText("GoldText", hud.transform, font, "金币  0", 25, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(34f, 0f), new Vector2(-20f, 0f));

        GameObject prompt = CreateImage("InteractionPrompt", feature.transform, LoadSprite(QuestListBackgroundPath), Color.white,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(490f, 64f));
        Text promptText = CreateText("PromptText", prompt.transform, font, "按 E 与 Fungi 交谈", 22,
            InteractionPromptTextColor, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        ConfigureMerchantInteractionPrompt(prompt.transform);

        GameObject dialogue = CreateImage("FirstDialogue", feature.transform, null, new Color(0f, 0f, 0f, 0.68f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        GameObject dialogueWindow = CreateImage("Popup", dialogue.transform,
            LoadSprite(QuestPanelBackgroundPath), Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1050f, 660f));
        Text speaker = CreateText("SpeakerText", dialogueWindow.transform, font, "Fungi", 34, DialogueSpeakerColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(200f, -88f), new Vector2(-200f, -30f));
        Text body = CreateText("BodyText", dialogueWindow.transform, font, "去右边战斗赚些金币，再来找我买东西吧。", 28,
            DialogueBodyColor, TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
            new Vector2(120f, -440f), new Vector2(-120f, -140f));
        Button dialogueClose = CreateButton("ContinueButton", dialogueWindow.transform, font, "知道了", 24,
            LoadSprite(QuestActionButtonPath), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(220f, 68f));
        ConfigureFungiDialogueStyle(dialogueWindow.transform);

        GameObject shop = CreateImage("ShopPanel", feature.transform, LoadSprite(ShopSpriteFolder + "UI_Shop_ShopChest_Background.png"), Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Button back = CreateButton("BackButton", shop.transform, font, string.Empty, 20,
            LoadSprite(ShopSpriteFolder + "UI_Shop_ShopChest_Top_Back.png"), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(64f, -54f), new Vector2(78f, 78f));
        CreateText("Title", shop.transform, font, "Fungi 商店", 42, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(420f, 72f));
        Text shopGold = CreateText("ShopGoldText", shop.transform, font, "当前金币：0", 28, Color.white, TextAnchor.MiddleRight,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -55f), new Vector2(420f, 72f));
        ConfigureShopGoldDisplay(shopGold);

        Sprite tabSprite = LoadSprite(ShopSpriteFolder + "UI_Shop_ShopChest_Tab.png");
        Button all = CreateButton("AllCategory", shop.transform, font, "全部", 24, tabSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(145f, -155f), new Vector2(220f, 64f));
        Button consumable = CreateButton("ConsumableCategory", shop.transform, font, "消耗品", 24, tabSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(385f, -155f), new Vector2(220f, 64f));
        Button equipment = CreateButton("EquipmentCategory", shop.transform, font, "装备", 24, tabSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(625f, -155f), new Vector2(220f, 64f));
        Button material = CreateButton("MaterialCategory", shop.transform, font, "材料", 24, tabSprite,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(865f, -155f), new Vector2(220f, 64f));

        GameObject scroll = CreateRectObject("ProductScroll", shop.transform, new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(65f, 55f), new Vector2(-65f, -220f));
        Image scrollBackground = scroll.AddComponent<Image>();
        scrollBackground.color = new Color(0.12f, 0.07f, 0.03f, 0.36f);
        ScrollRect scrollRect = scroll.AddComponent<ScrollRect>();
        ConfigureProductScroll(scrollRect);
        GameObject viewport = CreateRectObject("Viewport", scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        GameObject content = CreateRectObject("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(320f, 285f);
        grid.spacing = new Vector2(22f, 22f);
        grid.padding = new RectOffset(28, 28, 25, 25);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.childAlignment = TextAnchor.UpperCenter;
        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = content.GetComponent<RectTransform>();

        var cards = new ShopItemCardView[20];
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i] = CreateProductCard(content.transform, font, i + 1);
        }

        GameObject toast = CreateImage("ShopToast", feature.transform, null, new Color(0.03f, 0.02f, 0.01f, 0.9f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f), new Vector2(520f, 62f));
        Text toastText = CreateText("ToastText", toast.transform, font, "购买成功", 24, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        return new MerchantUiBuildResult(feature, hudGold, prompt, promptText, dialogue, speaker, body, dialogueClose,
            shop, back, shopGold, scrollRect, all, consumable, equipment, material, cards, toast, toastText);
    }

    private static ShopItemCardView CreateProductCard(Transform parent, Font font, int index)
    {
        GameObject card = CreateImage($"ProductCard{index:00}", parent,
            LoadSprite(ShopSpriteFolder + "UI_Shop_ShopChest_Slot01_ShopFrame_Frame.png"), Color.white,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Button button = card.AddComponent<Button>();
        button.targetGraphic = card.GetComponent<Image>();
        Image icon = CreateImage("Icon", card.transform, null, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(100f, 100f)).GetComponent<Image>();
        icon.preserveAspect = true;
        Text name = CreateText("NameText", card.transform, font, "商品名称", 21, ProductNameColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -152f), new Vector2(-14f, -122f));
        Text description = CreateText("DescriptionText", card.transform, font, "商品说明", 15, ProductDescriptionColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -208f), new Vector2(-14f, -158f));
        Text price = CreateText("PriceText", card.transform, font, "0 金币", 18, ProductPriceColor, TextAnchor.MiddleCenter,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(14f, -236f), new Vector2(-14f, -210f));
        AddReadabilityOutline(name);
        AddReadabilityOutline(description);
        AddReadabilityOutline(price);
        GameObject stateBackground = CreateImage("StateBackground", card.transform,
            LoadSprite(ShopSpriteFolder + "UI_Shop_ShopChest_Slot01_ButtonBlue_Btn_Normal.png"), Color.white,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(130f, 36f));
        Text state = CreateText("StateText", stateBackground.transform, font, "购买", 18, Color.white, TextAnchor.MiddleCenter,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

        ShopItemCardView view = card.AddComponent<ShopItemCardView>();
        SerializedObject serialized = new SerializedObject(view);
        serialized.FindProperty("selectButton").objectReferenceValue = button;
        serialized.FindProperty("iconImage").objectReferenceValue = icon;
        serialized.FindProperty("nameText").objectReferenceValue = name;
        serialized.FindProperty("descriptionText").objectReferenceValue = description;
        serialized.FindProperty("priceText").objectReferenceValue = price;
        serialized.FindProperty("stateText").objectReferenceValue = state;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    /// <summary>
    /// 定点修复当前 GameplayUiRoot 中已有的商人 UI。
    /// 这里只改指定文字、图标和按钮子节点，不删除 MerchantShopFeature，避免覆盖其它手调布局和序列化引用。
    /// </summary>
    private static void ApplyReadabilityFixesToGameplayUiPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
        try
        {
            Canvas canvas = root.GetComponentInChildren<Canvas>(true);
            Transform feature = canvas != null ? canvas.transform.Find("MerchantShopFeature") : null;
            if (feature == null)
            {
                throw new InvalidOperationException("GameplayUiRoot.prefab 缺少 MerchantShopFeature，无法定点修复商店 UI。");
            }

            ScrollRect productScrollRect = RequireComponent<ScrollRect>(RequireChild(feature, "ShopPanel/ProductScroll"));
            ConfigureProductScroll(productScrollRect);
            ConfigureShopGoldDisplay(RequireComponent<Text>(RequireChild(feature, "ShopPanel/ShopGoldText")));
            ConfigureMerchantInteractionPrompt(RequireChild(feature, "InteractionPrompt"));

            Transform dialogue = RequireChild(feature, "FirstDialogue");
            RequireComponent<Image>(dialogue).color = new Color(0f, 0f, 0f, 0.68f);
            ConfigureFungiDialogueStyle(RequireChild(dialogue, "Popup"));

            ShopItemCardView[] cards = feature.GetComponentsInChildren<ShopItemCardView>(true);
            if (cards.Length != 20)
            {
                throw new InvalidOperationException($"商店商品卡数量应为 20，当前为 {cards.Length}；为避免误改，已停止定点修复。");
            }

            for (int i = 0; i < cards.Length; i++)
            {
                ConfigureProductCardReadability(cards[i].transform);
            }

            MerchantShopPanel panel = root.GetComponent<MerchantShopPanel>();
            if (panel == null)
            {
                throw new InvalidOperationException("GameplayUiRoot.prefab 缺少 MerchantShopPanel，无法记录视觉迁移版本。");
            }

            SerializedObject panelSerialized = new SerializedObject(panel);
            panelSerialized.FindProperty("visualLayoutVersion").intValue = CurrentVisualLayoutVersion;
            panelSerialized.FindProperty("productScrollRect").objectReferenceValue = productScrollRect;
            panelSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 让 Fungi 首次对话复用 Mushroom 任务窗口的背景、标题配色和操作按钮语言。
    /// 这里只迁移对话子节点，不替换 MerchantShopPanel，商店开启流程和“知道了”按钮逻辑保持不变。
    /// </summary>
    private static void ConfigureFungiDialogueStyle(Transform popup)
    {
        Image popupImage = RequireComponent<Image>(popup);
        popupImage.sprite = LoadSprite(QuestPanelBackgroundPath);
        popupImage.type = popupImage.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        SetCenteredRect(RequireComponent<RectTransform>(popup), new Vector2(1050f, 660f));

        Text speaker = RequireComponent<Text>(RequireChild(popup, "SpeakerText"));
        speaker.color = DialogueSpeakerColor;
        speaker.fontSize = 34;
        speaker.fontStyle = FontStyle.Bold;
        speaker.alignment = TextAnchor.MiddleCenter;
        SetTopStretchRect(speaker.rectTransform, 200f, 30f, 58f);
        AddReadabilityOutline(speaker);

        Text body = RequireComponent<Text>(RequireChild(popup, "BodyText"));
        body.color = DialogueBodyColor;
        body.fontSize = 28;
        body.fontStyle = FontStyle.Normal;
        body.alignment = TextAnchor.MiddleCenter;
        SetTopStretchRect(body.rectTransform, 120f, 140f, 300f);
        AddReadabilityOutline(body);

        Transform continueButton = RequireChild(popup, "ContinueButton");
        Image buttonImage = RequireComponent<Image>(continueButton);
        buttonImage.sprite = LoadSprite(QuestActionButtonPath);
        buttonImage.type = buttonImage.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        SetBottomCenteredRect(RequireComponent<RectTransform>(continueButton), 48f, new Vector2(220f, 68f));
    }

    /// <summary>
    /// 商店和任务属于不同业务系统，但底部交互提示应共享同一套视觉语言。
    /// 这里复制 QuestPrompt 的背景、尺寸和文字排版，不让 MerchantShopPanel 直接依赖 QuestPanel。
    /// </summary>
    private static void ConfigureMerchantInteractionPrompt(Transform prompt)
    {
        Image background = RequireComponent<Image>(prompt);
        background.sprite = LoadSprite(QuestListBackgroundPath);
        background.type = Image.Type.Sliced;
        background.color = Color.white;
        RectTransform promptRect = RequireComponent<RectTransform>(prompt);
        promptRect.anchorMin = new Vector2(0.5f, 0f);
        promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, 90f);
        promptRect.sizeDelta = new Vector2(490f, 64f);
        promptRect.localScale = Vector3.one;

        Text label = RequireComponent<Text>(RequireChild(prompt, "PromptText"));
        label.color = InteractionPromptTextColor;
        label.fontSize = 22;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.offsetMin = new Vector2(15f, 8f);
        labelRect.offsetMax = new Vector2(-15f, -8f);
        labelRect.localScale = Vector3.one;
    }

    /// <summary>
    /// 金币文本使用右上角锚点和负向安全边距，避免正向偏移把文字推到屏幕外。
    /// 描边与金币色保证它在明暗不同的商店背景上都能读清。
    /// </summary>
    private static void ConfigureShopGoldDisplay(Text shopGold)
    {
        shopGold.color = ShopGoldColor;
        shopGold.fontSize = 28;
        shopGold.fontStyle = FontStyle.Bold;
        shopGold.alignment = TextAnchor.MiddleRight;
        SetTopRightRect(shopGold.rectTransform, 70f, 28f, new Vector2(420f, 72f));
        AddReadabilityOutline(shopGold);
    }

    private static void ConfigureProductCardReadability(Transform card)
    {
        Image icon = RequireComponent<Image>(RequireChild(card, "Icon"));
        Text name = RequireComponent<Text>(RequireChild(card, "NameText"));
        Text description = RequireComponent<Text>(RequireChild(card, "DescriptionText"));
        Text price = RequireComponent<Text>(RequireChild(card, "PriceText"));
        RectTransform stateBackground = RequireChild(card, "StateBackground") as RectTransform;

        icon.preserveAspect = true;
        SetTopCenteredRect(icon.rectTransform, 16f, new Vector2(100f, 100f));

        name.color = ProductNameColor;
        SetTopStretchRect(name.rectTransform, 14f, 122f, 30f);
        AddReadabilityOutline(name);

        description.color = ProductDescriptionColor;
        description.alignment = TextAnchor.MiddleCenter;
        SetTopStretchRect(description.rectTransform, 14f, 158f, 50f);
        AddReadabilityOutline(description);

        price.color = ProductPriceColor;
        SetTopStretchRect(price.rectTransform, 14f, 210f, 26f);
        AddReadabilityOutline(price);

        SetBottomCenteredRect(stateBackground, 12f, new Vector2(130f, 36f));
    }

    private static void AddReadabilityOutline(Text text)
    {
        Outline outline = text.GetComponent<Outline>();
        if (outline == null)
        {
            outline = text.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = ReadabilityOutlineColor;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    /// <summary>
    /// 商品卡单格高度较大，UGUI 默认灵敏度 1 每次滚轮只移动约一个像素，玩家会误以为无法滚动。
    /// 这里统一新生成和已存在 Prefab 的纵向滚动参数。
    /// </summary>
    private static void ConfigureProductScroll(ScrollRect scrollRect)
    {
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = ProductScrollSensitivity;
    }

    private static void SetTopStretchRect(RectTransform rect, float horizontalPadding, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(horizontalPadding, -top - height);
        rect.offsetMax = new Vector2(-horizontalPadding, -top);
        rect.localScale = Vector3.one;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetTopRightRect(RectTransform rect, float right, float top, Vector2 size)
    {
        rect.anchorMin = Vector2.one;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-right, -top);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetTopCenteredRect(RectTransform rect, float top, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -top);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void SetBottomCenteredRect(RectTransform rect, float bottom, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottom);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Transform RequireChild(Transform parent, string path)
    {
        Transform child = parent != null ? parent.Find(path) : null;
        if (child == null)
        {
            throw new InvalidOperationException($"商店 UI 缺少节点：{path}");
        }

        return child;
    }

    private static T RequireComponent<T>(Transform target) where T : Component
    {
        T component = target != null ? target.GetComponent<T>() : null;
        if (component == null)
        {
            throw new InvalidOperationException($"商店 UI 节点 {target?.name ?? "<null>"} 缺少组件 {typeof(T).Name}。");
        }

        return component;
    }

    private static void ConfigureMerchantPanel(
        MerchantShopPanel panel,
        Canvas canvas,
        GameSessionUi sessionUi,
        InventoryPanel inventoryPanel,
        MerchantUiBuildResult ui)
    {
        SerializedObject serialized = new SerializedObject(panel);
        serialized.FindProperty("visualLayoutVersion").intValue = CurrentVisualLayoutVersion;
        serialized.FindProperty("targetCanvas").objectReferenceValue = canvas;
        serialized.FindProperty("sessionUi").objectReferenceValue = sessionUi;
        serialized.FindProperty("inventoryPanel").objectReferenceValue = inventoryPanel;
        serialized.FindProperty("promptRoot").objectReferenceValue = ui.PromptRoot;
        serialized.FindProperty("promptText").objectReferenceValue = ui.PromptText;
        serialized.FindProperty("dialogueRoot").objectReferenceValue = ui.DialogueRoot;
        serialized.FindProperty("dialogueSpeakerText").objectReferenceValue = ui.DialogueSpeaker;
        serialized.FindProperty("dialogueBodyText").objectReferenceValue = ui.DialogueBody;
        serialized.FindProperty("dialogueCloseButton").objectReferenceValue = ui.DialogueClose;
        serialized.FindProperty("shopRoot").objectReferenceValue = ui.ShopRoot;
        serialized.FindProperty("shopCloseButton").objectReferenceValue = ui.ShopClose;
        serialized.FindProperty("shopGoldText").objectReferenceValue = ui.ShopGoldText;
        serialized.FindProperty("productScrollRect").objectReferenceValue = ui.ProductScrollRect;
        serialized.FindProperty("allCategoryButton").objectReferenceValue = ui.AllCategory;
        serialized.FindProperty("consumableCategoryButton").objectReferenceValue = ui.ConsumableCategory;
        serialized.FindProperty("equipmentCategoryButton").objectReferenceValue = ui.EquipmentCategory;
        serialized.FindProperty("materialCategoryButton").objectReferenceValue = ui.MaterialCategory;
        SerializedProperty cards = serialized.FindProperty("itemCards");
        cards.arraySize = ui.Cards.Length;
        for (int i = 0; i < ui.Cards.Length; i++)
        {
            cards.GetArrayElementAtIndex(i).objectReferenceValue = ui.Cards[i];
        }
        serialized.FindProperty("toastRoot").objectReferenceValue = ui.ToastRoot;
        serialized.FindProperty("toastText").objectReferenceValue = ui.ToastText;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void UpgradeMainSceneMerchant()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string previousScenePath = activeScene.path;
        bool reopenPrevious = activeScene.IsValid() && !string.IsNullOrWhiteSpace(previousScenePath) && previousScenePath != MainScenePath;
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        try
        {
            MerchantNpcController existingMerchant = UnityEngine.Object.FindObjectOfType<MerchantNpcController>();
            if (existingMerchant != null)
            {
                return;
            }

            GameObject oldFungi = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].name == "Fungi")
                    {
                        oldFungi = transforms[i].gameObject;
                        break;
                    }
                }
                if (oldFungi != null) break;
            }

            Vector3 position = oldFungi != null ? oldFungi.transform.position : new Vector3(2.7f, 0.15232706f, 1.29f);
            Quaternion rotation = oldFungi != null ? oldFungi.transform.rotation : Quaternion.Euler(0f, 180f, 0f);
            Vector3 scale = oldFungi != null ? oldFungi.transform.localScale : Vector3.one * 1.5f;
            Transform parent = oldFungi != null ? oldFungi.transform.parent : null;
            if (oldFungi != null)
            {
                UnityEngine.Object.DestroyImmediate(oldFungi);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
            GameObject merchant = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            merchant.name = "Fungi";
            merchant.transform.SetParent(parent, true);
            merchant.transform.SetPositionAndRotation(position, rotation);
            merchant.transform.localScale = scale;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (reopenPrevious && System.IO.File.Exists(previousScenePath))
            {
                EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
            }
        }
    }

    private static GameObject CreateRectObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        return gameObject;
    }

    private static GameObject CreateRectObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject gameObject = CreateRectObject(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        return gameObject;
    }

    private static GameObject CreateImage(string name, Transform parent, Sprite sprite, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject gameObject = CreateRectObject(name, parent, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Image image = gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        return gameObject;
    }

    private static Text CreateText(string name, Transform parent, Font font, string value, int fontSize, Color color,
        TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject gameObject = CreateRectObject(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.pivot = pivot;
        Text text = gameObject.AddComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, Font font, string label, int fontSize, Sprite sprite,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject gameObject = CreateImage(name, parent, sprite, Color.white, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
        Button button = gameObject.AddComponent<Button>();
        button.targetGraphic = gameObject.GetComponent<Image>();
        if (!string.IsNullOrEmpty(label))
        {
            CreateText("Label", gameObject.transform, font, label, fontSize, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }
        return button;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new InvalidOperationException($"找不到淘宝 UI Sprite：{path}");
        }
        return sprite;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private readonly struct CatalogData
    {
        public CatalogData(string assetName, long price, ShopCategory category, bool limited)
        {
            AssetName = assetName;
            Price = price;
            Category = category;
            Limited = limited;
        }

        public string AssetName { get; }
        public long Price { get; }
        public ShopCategory Category { get; }
        public bool Limited { get; }
    }

    private readonly struct StarterEquipmentData
    {
        public StarterEquipmentData(string assetName, string itemId, string displayName, EquipmentSlotType slot,
            string iconName, string description, params EquipmentStatModifier[] modifiers)
        {
            AssetName = assetName;
            ItemId = itemId;
            DisplayName = displayName;
            Slot = slot;
            IconName = iconName;
            Description = description;
            Modifiers = modifiers;
        }

        public string AssetName { get; }
        public string ItemId { get; }
        public string DisplayName { get; }
        public EquipmentSlotType Slot { get; }
        public string IconName { get; }
        public string Description { get; }
        public EquipmentStatModifier[] Modifiers { get; }
    }

    private readonly struct MerchantUiBuildResult
    {
        public MerchantUiBuildResult(GameObject featureRoot, Text hudGoldText, GameObject promptRoot, Text promptText,
            GameObject dialogueRoot, Text dialogueSpeaker, Text dialogueBody, Button dialogueClose, GameObject shopRoot,
            Button shopClose, Text shopGoldText, ScrollRect productScrollRect, Button allCategory,
            Button consumableCategory, Button equipmentCategory, Button materialCategory, ShopItemCardView[] cards,
            GameObject toastRoot, Text toastText)
        {
            FeatureRoot = featureRoot;
            HudGoldText = hudGoldText;
            PromptRoot = promptRoot;
            PromptText = promptText;
            DialogueRoot = dialogueRoot;
            DialogueSpeaker = dialogueSpeaker;
            DialogueBody = dialogueBody;
            DialogueClose = dialogueClose;
            ShopRoot = shopRoot;
            ShopClose = shopClose;
            ShopGoldText = shopGoldText;
            ProductScrollRect = productScrollRect;
            AllCategory = allCategory;
            ConsumableCategory = consumableCategory;
            EquipmentCategory = equipmentCategory;
            MaterialCategory = materialCategory;
            Cards = cards;
            ToastRoot = toastRoot;
            ToastText = toastText;
        }

        public GameObject FeatureRoot { get; }
        public Text HudGoldText { get; }
        public GameObject PromptRoot { get; }
        public Text PromptText { get; }
        public GameObject DialogueRoot { get; }
        public Text DialogueSpeaker { get; }
        public Text DialogueBody { get; }
        public Button DialogueClose { get; }
        public GameObject ShopRoot { get; }
        public Button ShopClose { get; }
        public Text ShopGoldText { get; }
        public ScrollRect ProductScrollRect { get; }
        public Button AllCategory { get; }
        public Button ConsumableCategory { get; }
        public Button EquipmentCategory { get; }
        public Button MaterialCategory { get; }
        public ShopItemCardView[] Cards { get; }
        public GameObject ToastRoot { get; }
        public Text ToastText { get; }
    }
}
#endif
