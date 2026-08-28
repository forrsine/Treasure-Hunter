#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
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
    private const string EquipmentSpriteFolder = "Assets/AllResources/淘宝ui素材/RuntimeSprites/Equipment/";
    private const string WindleafBootsIconPath =
        "Assets/AllResources/淘宝ui素材/RuntimeSprites/FunctionIcons/UI_FunctionIcon_CustumeBoots.png";

    private static readonly EquipmentSetupData[] EquipmentDefinitions =
    {
        new EquipmentSetupData("BossIronWarAxe.asset", "boss_iron_war_axe", "铁铸战斧", EquipmentSlotType.Weapon, InventoryItemRarity.Rare, "UI_Equipment_Item_Slot01.png", "Boss 掉落的沉重战斧。", new EquipmentStatModifier(EquipmentStatType.Attack, 12f)),
        new EquipmentSetupData("BossMoonReaper.asset", "boss_moon_reaper", "月蚀战斧", EquipmentSlotType.Weapon, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot03_Slot06.png", "月光淬炼的史诗战斧。", new EquipmentStatModifier(EquipmentStatType.Attack, 20f), new EquipmentStatModifier(EquipmentStatType.CritChance, 0.03f)),
        new EquipmentSetupData("BossStoneplateArmor.asset", "boss_stoneplate_armor", "岩铸护甲", EquipmentSlotType.Armor, InventoryItemRarity.Rare, "UI_Equipment_Item_Slot01_Slot03.png", "岩石般可靠的重甲。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 80f), new EquipmentStatModifier(EquipmentStatType.MaxMp, 30f)),
        new EquipmentSetupData("BossCrystalplateArmor.asset", "boss_crystalplate_armor", "晶簇护甲", EquipmentSlotType.Armor, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot01_Slot06.png", "晶簇保护的史诗护甲。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 130f), new EquipmentStatModifier(EquipmentStatType.DamageReduction, 0.03f)),
        new EquipmentSetupData("BossWoodguardShield.asset", "boss_woodguard_shield", "古木盾", EquipmentSlotType.Shield, InventoryItemRarity.Rare, "UI_Equipment_Item_Slot04_Slot01.png", "古木打造的坚韧盾牌。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 60f), new EquipmentStatModifier(EquipmentStatType.DamageReduction, 0.02f)),
        new EquipmentSetupData("BossRoyalShield.asset", "boss_royal_shield", "王庭壁垒", EquipmentSlotType.Shield, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot03_Slot04.png", "王庭守卫使用的史诗盾牌。", new EquipmentStatModifier(EquipmentStatType.MaxHp, 90f), new EquipmentStatModifier(EquipmentStatType.DamageReduction, 0.04f)),
        new EquipmentSetupData("BossFangGloves.asset", "boss_fang_gloves", "兽牙手套", EquipmentSlotType.Gloves, InventoryItemRarity.Rare, "UI_Equipment_Item_Slot01_Slot05.png", "提高致命一击机会的手套。", new EquipmentStatModifier(EquipmentStatType.CritChance, 0.04f)),
        new EquipmentSetupData("BossBloodclawGloves.asset", "boss_bloodclaw_gloves", "血爪手套", EquipmentSlotType.Gloves, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot02_Slot05.png", "能从伤口汲取生命的史诗手套。", new EquipmentStatModifier(EquipmentStatType.CritChance, 0.05f), new EquipmentStatModifier(EquipmentStatType.LifeSteal, 0.02f)),
        new EquipmentSetupData("BossWindleafBoots.asset", "boss_windleaf_boots", "风叶长靴", EquipmentSlotType.Boots, InventoryItemRarity.Rare, WindleafBootsIconPath, "轻盈如风的长靴。", new EquipmentStatModifier(EquipmentStatType.MoveSpeed, 0.25f), new EquipmentStatModifier(EquipmentStatType.DodgeChance, 0.02f)),
        new EquipmentSetupData("BossPredatorBoots.asset", "boss_predator_boots", "掠影长靴", EquipmentSlotType.Boots, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot02.png", "追猎者留下的史诗长靴。", new EquipmentStatModifier(EquipmentStatType.MoveSpeed, 0.45f), new EquipmentStatModifier(EquipmentStatType.DodgeChance, 0.04f)),
        new EquipmentSetupData("BossRubyRing.asset", "boss_ruby_ring", "红玉戒指", EquipmentSlotType.Ring, InventoryItemRarity.Rare, "UI_Equipment_Item_Slot01_Slot02.png", "十级后可以驾驭的红玉戒指。", new EquipmentStatModifier(EquipmentStatType.MaxMp, 50f), new EquipmentStatModifier(EquipmentStatType.LifeSteal, 0.015f)),
        new EquipmentSetupData("BossTideRing.asset", "boss_tide_ring", "潮汐戒指", EquipmentSlotType.Ring, InventoryItemRarity.Epic, "UI_Equipment_Item_Slot05_Slot06.png", "蕴含潮汐魔力的史诗戒指。", new EquipmentStatModifier(EquipmentStatType.MaxMp, 80f), new EquipmentStatModifier(EquipmentStatType.LifeSteal, 0.025f))
    };

    private const string BackgroundSpritePath =
        "Assets/AllResources/淘宝ui素材/RuntimeSprites/Equipment/UI_Equipment_Background.png";
    private const string SlotSpritePath =
        "Assets/AllResources/淘宝ui素材/RuntimeSprites/Common/UI_Common_Component_Stage_Slot01_Frame.png";
    private const string SelectedSlotSpritePath =
        "Assets/AllResources/淘宝ui素材/RuntimeSprites/Progression/UI_Progression_StageSelect_Stage_Slot06_Frame.png";
    private const string BagIconPath =
        "Assets/AllResources/淘宝ui素材/RuntimeSprites/Equipment/UI_Equipment_Tab_Menu_Icon.png";
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
            database.BossLootEntries.Length != 3 || database.BossEquipmentLootEntries == null ||
            database.BossEquipmentLootEntries.Length != EquipmentDefinitions.Length;
    }

    [MenuItem("Tools/Treasure Hunter/Setup Inventory Feature")]
    public static void SetupFromMenu()
    {
        Setup();
        Debug.Log("背包配置、药水拾取物、Boss 光球和普通怪 Prefab 装配完成；已有背包 UI 已保留。");
    }

    /// <summary>
    /// 进入共享 GameplayUiRoot 的 Prefab Mode，并定位到背包装备窗口。
    /// 手动排版直接保存在 Prefab 上，因此 MainScene 与 BossRoomScene 会同时获得修改结果。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/UI/Edit Equipment Inventory Layout")]
    private static void OpenEquipmentInventoryLayoutForEditing()
    {
        GameObject gameplayUi = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        if (gameplayUi == null)
        {
            EditorUtility.DisplayDialog(
                "无法编辑背包装备 UI",
                $"找不到 Prefab：{GameplayUiPrefabPath}",
                "确定");
            return;
        }

        if (!AssetDatabase.OpenAsset(gameplayUi))
        {
            Debug.LogError($"无法打开 GameplayUiRoot Prefab：{GameplayUiPrefabPath}");
            return;
        }

        // Prefab Mode 切换完成后再定位节点，避免刚打开资源时 PrefabStage 尚未初始化。
        EditorApplication.delayCall += FocusEquipmentInventoryLayout;
    }

    private static void FocusEquipmentInventoryLayout()
    {
        PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage == null || prefabStage.prefabAssetPath != GameplayUiPrefabPath)
        {
            Debug.LogError("背包装备 UI 编辑入口未能进入 GameplayUiRoot 的 Prefab Mode，请重新执行菜单。");
            return;
        }

        Transform overlay = prefabStage.prefabContentsRoot.transform.Find("InventoryOverlay");
        Transform window = overlay != null ? overlay.Find("InventoryWindow") : null;
        if (overlay == null || window == null)
        {
            Debug.LogError("GameplayUiRoot 中缺少 InventoryOverlay/InventoryWindow，请先运行背包 UI 重建菜单。", prefabStage.prefabContentsRoot);
            return;
        }

        if (!overlay.gameObject.activeSelf)
        {
            // 这里只恢复编辑态可见；运行时仍由 InventoryPanel.Start 统一隐藏。
            Undo.RecordObject(overlay.gameObject, "Show Inventory Layout For Editing");
            overlay.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }

        Selection.activeGameObject = window.gameObject;
        EditorGUIUtility.PingObject(window.gameObject);
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView.lastActiveSceneView.FrameSelected();
        }

        Debug.Log("已打开背包装备 UI 排版：调整 InventoryWindow 子节点后按 Ctrl+S 保存。请勿执行带 Overwrites Manual Layout 字样的重建菜单。");
    }

    /// <summary>
    /// 批处理验收入口：补齐装备配置与缺失 UI，但保留已经存在的手动排版。
    /// 需要恢复默认布局时，只能通过带二次确认的重建菜单显式执行。
    /// </summary>
    public static void GenerateEquipmentFeatureBatch()
    {
        Setup();
        AssetDatabase.SaveAssets();
        Debug.Log("EQUIPMENT_FEATURE_GENERATION_SUCCEEDED");
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
        var equipmentItems = new InventoryItemDefinition[EquipmentDefinitions.Length];
        for (int i = 0; i < EquipmentDefinitions.Length; i++)
        {
            EquipmentSetupData data = EquipmentDefinitions[i];
            equipmentItems[i] = CreateOrLoadItem(
                InventoryFolder + "/" + data.AssetName,
                data.ItemId,
                data.DisplayName,
                InventoryItemCategory.Equipment,
                data.Rarity,
                data.IconPath,
                data.Description,
                1);
            ConfigureEquipmentData(equipmentItems[i], data);
        }

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
            equipmentItems,
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

    private static void ConfigureEquipmentData(InventoryItemDefinition item, EquipmentSetupData data)
    {
        SerializedObject serialized = new SerializedObject(item);
        serialized.FindProperty("displayName").stringValue = data.DisplayName;
        serialized.FindProperty("category").enumValueIndex = (int)InventoryItemCategory.Equipment;
        serialized.FindProperty("rarity").enumValueIndex = (int)data.Rarity;
        serialized.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(data.IconPath);
        serialized.FindProperty("description").stringValue = data.Description;
        serialized.FindProperty("maxStack").intValue = 1;
        serialized.FindProperty("useEffect").enumValueIndex = (int)InventoryItemUseEffect.None;
        serialized.FindProperty("restorePercent").floatValue = 0f;
        serialized.FindProperty("equipmentSlot").enumValueIndex = (int)data.Slot;
        SerializedProperty modifiers = serialized.FindProperty("equipmentStatModifiers");
        modifiers.arraySize = data.Modifiers.Length;
        for (int i = 0; i < data.Modifiers.Length; i++)
        {
            SerializedProperty modifier = modifiers.GetArrayElementAtIndex(i);
            modifier.FindPropertyRelative("statType").enumValueIndex = (int)data.Modifiers[i].StatType;
            modifier.FindPropertyRelative("value").floatValue = data.Modifiers[i].Value;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
    }

    private static InventoryDatabase CreateOrLoadDatabase(
        InventoryItemDefinition potion,
        InventoryItemDefinition manaPotion,
        InventoryItemDefinition crystal,
        InventoryItemDefinition scroll,
        InventoryItemDefinition spiderKingCore,
        InventoryItemDefinition[] equipmentItems,
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
        for (int i = 0; i < equipmentItems.Length; i++)
        {
            EnsureObjectReferenceInArray(items, equipmentItems[i]);
        }

        serialized.FindProperty("capacity").intValue = InventoryModel.DefaultCapacity;
        SerializedProperty lootEntries = serialized.FindProperty("vaultLootEntries");
        lootEntries.arraySize = 0;

        serialized.FindProperty("monsterDropChance").floatValue = 0.12f;
        SerializedProperty monsterEntries = serialized.FindProperty("monsterLootEntries");
        monsterEntries.arraySize = 2;
        ConfigureMonsterLootEntry(monsterEntries.GetArrayElementAtIndex(0), potion, 55f);
        ConfigureMonsterLootEntry(monsterEntries.GetArrayElementAtIndex(1), manaPotion, 45f);
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

        SerializedProperty equipmentEntries = serialized.FindProperty("bossEquipmentLootEntries");
        if (equipmentEntries != null)
        {
            equipmentEntries.arraySize = equipmentItems.Length;
            for (int i = 0; i < equipmentItems.Length; i++)
            {
                float weight = equipmentItems[i].Rarity == InventoryItemRarity.Epic ? 1f : 3f;
                ConfigureLootEntry(equipmentEntries.GetArrayElementAtIndex(i), equipmentItems[i], weight, 1, 1);
            }
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

    private readonly struct EquipmentSetupData
    {
        public EquipmentSetupData(string assetName, string itemId, string displayName, EquipmentSlotType slot,
            InventoryItemRarity rarity, string iconPathOrName, string description, params EquipmentStatModifier[] modifiers)
        {
            AssetName = assetName;
            ItemId = itemId;
            DisplayName = displayName;
            Slot = slot;
            Rarity = rarity;
            // 普通装备只写文件名；个别需要跨目录取图标的装备可以直接传完整资源路径。
            IconPath = iconPathOrName.StartsWith("Assets/", StringComparison.Ordinal)
                ? iconPathOrName
                : EquipmentSpriteFolder + iconPathOrName;
            Description = description;
            Modifiers = modifiers ?? Array.Empty<EquipmentStatModifier>();
        }

        public string AssetName { get; }
        public string ItemId { get; }
        public string DisplayName { get; }
        public EquipmentSlotType Slot { get; }
        public InventoryItemRarity Rarity { get; }
        public string IconPath { get; }
        public string Description { get; }
        public EquipmentStatModifier[] Modifiers { get; }
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
            backgroundSprite,
            Color.white,
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
            null,
            new Color(0f, 0f, 0f, 0f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(1920f, 1080f));

        Text title = CreateText(
            "Title",
            window.transform,
            font,
            "装 备 背 包",
            30,
            new Color(0.95f, 0.81f, 0.55f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -52f),
            new Vector2(500f, 52f));
        AddTextOutline(title, new Color(0.12f, 0.06f, 0.03f, 1f));

        Button closeButton = CreateButton(
            "CloseButton",
            window.transform,
            font,
            "",
            30,
            Color.clear,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(70f, -54f),
            new Vector2(82f, 82f));
        closeButton.targetGraphic.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentSpriteFolder + "UI_Equipment_Top_Back.png");
        closeButton.targetGraphic.GetComponent<Image>().color = Color.white;

        CreateImageObject(
            "BagIcon",
            window.transform,
            bagIcon,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(320f, 370f),
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
            new Vector2(470f, 370f),
            new Vector2(210f, 38f));

        Text helpText = CreateText(
            "HelpText",
            window.transform,
            font,
            "Boss 必掉一件装备\n点击背包或装备槽查看详情\n\nB  打开 / 关闭\nESC  返回游戏",
            17,
            new Color(0.82f, 0.74f, 0.62f, 1f),
            TextAnchor.UpperCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(730f, 362f),
            new Vector2(220f, 170f));
        helpText.lineSpacing = 1.2f;

        GameObject gridRoot = CreateRectObject(
            "ItemGrid",
            window.transform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(430f, 90f),
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
            AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentSpriteFolder + "UI_Equipment_EquipmentDetail1_Popup.png"),
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(790f, 120f),
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
            new Vector2(790f, 32f),
            new Vector2(220f, 42f));
        Text detailMeta = CreateText(
            "DetailMetaText",
            window.transform,
            font,
            "点击背包或装备槽查看详情",
            16,
            new Color(0.76f, 0.69f, 0.61f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(790f, -4f),
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
            new Vector2(790f, -38f),
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
            new Vector2(790f, -170f),
            new Vector2(260f, 210f));

        Button useButton = CreateButton(
            "UseButton",
            window.transform,
            font,
            "使  用",
            20,
            Color.white,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(790f, -330f),
            new Vector2(210f, 64f));
        useButton.targetGraphic.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentSpriteFolder + "UI_Equipment_ButtonGreen.png");

        Text footer = CreateText(
            "FooterText",
            window.transform,
            font,
            "B / ESC / 返回按钮关闭 · 装备属性自动结算",
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

        Image classIcon;
        Text characterName;
        Text characterLevel;
        Text finalStats;
        EquipmentSlotView[] equipmentSlots = BuildEquipmentArea(window.transform, font, slotSprite, selectedSlotSprite,
            out classIcon, out characterName, out characterLevel, out finalStats);

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
            equipmentSlots,
            classIcon,
            characterName,
            characterLevel,
            finalStats,
            toast,
            toastText);
    }

    private static EquipmentSlotView[] BuildEquipmentArea(
        Transform window, Font font, Sprite slotSprite, Sprite selectedSlotSprite,
        out Image classIcon, out Text characterName, out Text characterLevel, out Text finalStats)
    {
        Sprite statPanel = AssetDatabase.LoadAssetAtPath<Sprite>(EquipmentSpriteFolder + "UI_Equipment_Stat.png");
        CreateImageObject("CharacterStatPanel", window, statPanel, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-590f, -40f), new Vector2(520f, 720f));
        classIcon = CreateImageObject("ClassIcon", window, null, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-590f, 300f), new Vector2(130f, 130f)).GetComponent<Image>();
        classIcon.preserveAspect = true;
        characterName = CreateText("CharacterNameText", window, font, "冒险者", 28, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-590f, 214f), new Vector2(260f, 42f));
        characterLevel = CreateText("CharacterLevelText", window, font, "Lv.1", 20, new Color(0.57f, 1f, 0.55f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-590f, 174f), new Vector2(200f, 32f));
        finalStats = CreateText("FinalStatsText", window, font, "攻击\n生命\n魔法\n移速\n暴击\n闪避\n减伤\n吸血", 19, Color.white, TextAnchor.UpperLeft,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-590f, -195f), new Vector2(270f, 260f));
        finalStats.lineSpacing = 1.25f;

        EquipmentSlotType[] types = { EquipmentSlotType.Weapon, EquipmentSlotType.Armor, EquipmentSlotType.Shield, EquipmentSlotType.Gloves, EquipmentSlotType.Boots, EquipmentSlotType.Ring };
        string[] placeholderNames = { "Sword1", "CustumeTop", "Shield", "Glove", "BootSpeed", "Ring" };
        string functionFolder = "Assets/AllResources/淘宝ui素材/RuntimeSprites/FunctionIcons/UI_FunctionIcon_";
        var views = new EquipmentSlotView[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            Vector2 position = new Vector2(-790f + column * 400f, 130f - row * 170f);
            GameObject slot = CreateImageObject($"EquipmentSlot_{types[i]}", window, slotSprite, Color.white,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(112f, 112f));
            Image frame = slot.GetComponent<Image>();
            Button button = slot.AddComponent<Button>();
            button.targetGraphic = frame;
            Image placeholder = CreateImageObject("Placeholder", slot.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(functionFolder + placeholderNames[i] + ".png"),
                new Color(1f, 1f, 1f, 0.42f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).GetComponent<Image>();
            placeholder.rectTransform.offsetMin = new Vector2(18f, 18f);
            placeholder.rectTransform.offsetMax = new Vector2(-18f, -18f);
            placeholder.preserveAspect = true;
            Image icon = CreateImageObject("Icon", slot.transform, null, Color.white, Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).GetComponent<Image>();
            icon.rectTransform.offsetMin = new Vector2(10f, 10f);
            icon.rectTransform.offsetMax = new Vector2(-10f, -10f);
            icon.preserveAspect = true;
            GameObject selected = CreateImageObject("SelectedFrame", slot.transform, selectedSlotSprite, Color.white,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            selected.GetComponent<RectTransform>().offsetMin = new Vector2(-4f, -4f);
            selected.GetComponent<RectTransform>().offsetMax = new Vector2(4f, 4f);
            selected.SetActive(false);
            GameObject locked = CreateImageObject("LockedState", slot.transform, null, new Color(0f, 0f, 0f, 0.72f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Text lockText = CreateText("LockText", locked.transform, font, "Lv.10\n解锁", 17, Color.white, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            lockText.rectTransform.offsetMin = Vector2.zero;
            lockText.rectTransform.offsetMax = Vector2.zero;
            locked.SetActive(types[i] == EquipmentSlotType.Ring);
            CreateText("SlotName", window, font, GetEquipmentSlotDisplayName(types[i]), 17, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position + new Vector2(0f, -72f), new Vector2(120f, 26f));

            EquipmentSlotView view = slot.AddComponent<EquipmentSlotView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("slotType").enumValueIndex = (int)types[i];
            SetReference(serialized, "button", button);
            SetReference(serialized, "iconImage", icon);
            SetReference(serialized, "placeholderImage", placeholder);
            SetReference(serialized, "selectedFrame", selected);
            SetReference(serialized, "lockedState", locked);
            views[i] = view;
        }
        return views;
    }

    private static string GetEquipmentSlotDisplayName(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon: return "武器";
            case EquipmentSlotType.Armor: return "护甲";
            case EquipmentSlotType.Shield: return "盾牌";
            case EquipmentSlotType.Gloves: return "手套";
            case EquipmentSlotType.Boots: return "鞋子";
            default: return "戒指";
        }
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
        SetReference(serialized, "classIcon", ui.ClassIcon);
        SetReference(serialized, "characterNameText", ui.CharacterNameText);
        SetReference(serialized, "characterLevelText", ui.CharacterLevelText);
        SetReference(serialized, "finalStatsText", ui.FinalStatsText);

        string[] classIconPaths =
        {
            "Assets/AllResources/淘宝ui素材/RuntimeSprites/Character/UI_Character_Role_WarriorSelect.png",
            "Assets/AllResources/淘宝ui素材/RuntimeSprites/Character/UI_Character_Role_Wizard.png",
            "Assets/AllResources/淘宝ui素材/RuntimeSprites/Character/UI_Character_Role_Archer.png",
            "Assets/AllResources/淘宝ui素材/RuntimeSprites/Character/UI_Character_Role_Assassin.png"
        };
        SerializedProperty classIcons = serialized.FindProperty("classIcons");
        classIcons.arraySize = classIconPaths.Length;
        for (int i = 0; i < classIconPaths.Length; i++)
        {
            classIcons.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(classIconPaths[i]);
        }

        SerializedProperty slots = serialized.FindProperty("slotViews");
        slots.arraySize = ui.Slots.Length;
        for (int i = 0; i < ui.Slots.Length; i++)
        {
            slots.GetArrayElementAtIndex(i).objectReferenceValue = ui.Slots[i];
        }

        SerializedProperty equipmentSlots = serialized.FindProperty("equipmentSlotViews");
        equipmentSlots.arraySize = ui.EquipmentSlots.Length;
        for (int i = 0; i < ui.EquipmentSlots.Length; i++)
        {
            equipmentSlots.GetArrayElementAtIndex(i).objectReferenceValue = ui.EquipmentSlots[i];
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
            EquipmentSlotView[] equipmentSlots,
            Image classIcon,
            Text characterNameText,
            Text characterLevelText,
            Text finalStatsText,
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
            EquipmentSlots = equipmentSlots;
            ClassIcon = classIcon;
            CharacterNameText = characterNameText;
            CharacterLevelText = characterLevelText;
            FinalStatsText = finalStatsText;
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
        public EquipmentSlotView[] EquipmentSlots { get; }
        public Image ClassIcon { get; }
        public Text CharacterNameText { get; }
        public Text CharacterLevelText { get; }
        public Text FinalStatsText { get; }
        public GameObject ToastRoot { get; }
        public Text ToastText { get; }
    }
}
#endif
