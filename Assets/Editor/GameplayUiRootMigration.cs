#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameplayUiRoot Prefab 装配工具。
/// 所有创建行为只发生在编辑器：生成静态属性行、写入序列化引用，再把完整 Prefab 放入 MainScene。
/// </summary>
public static class GameplayUiRootMigration
{
    private const string PrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    private const string StartupGuidePrefabPath = "Assets/Prefabs/UI/StartupGuidePopup.prefab";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    private readonly struct AttributeDefinition
    {
        public AttributeDefinition(string group, string key, string label, string previewValue)
        {
            Group = group;
            Key = key;
            Label = label;
            PreviewValue = previewValue;
        }

        public string Group { get; }
        public string Key { get; }
        public string Label { get; }
        public string PreviewValue { get; }
    }

    private static readonly AttributeDefinition[] AttributeDefinitions =
    {
        new AttributeDefinition("概览", "level", "等级", "1"),
        new AttributeDefinition("概览", "exp", "经验", "0/50"),
        new AttributeDefinition("概览", "current_hp", "当前生命", "150/150"),
        new AttributeDefinition("概览", "max_hp", "最大生命", "150"),
        new AttributeDefinition("概览", "move_speed", "移动速度", "3.00"),
        new AttributeDefinition("战斗", "attack_power", "攻击力", "25"),
        new AttributeDefinition("战斗", "crit_chance", "暴击率", "0%"),
        new AttributeDefinition("战斗", "crit_damage", "暴击伤害", "1.50x"),
        new AttributeDefinition("生存", "dodge_chance", "闪避率", "0%"),
        new AttributeDefinition("生存", "health_regen", "生命恢复", "0/s"),
        new AttributeDefinition("生存", "damage_reduction", "伤害减免", "0%"),
        new AttributeDefinition("生存", "life_steal", "吸血", "0%")
    };

    [MenuItem("Tools/Treasure Hunter/Rewire Gameplay UI Prefab")]
    public static void RebuildFromMenu()
    {
        Rebuild();
        Debug.Log("GameplayUiRoot Prefab 静态引用装配完成。");
    }

    public static void RebuildFromCommandLine()
    {
        Rebuild();
        Debug.Log("GAMEPLAY_UI_PREFAB_REWIRE_SUCCEEDED");
    }

    /// <summary>
    /// 只更新 Prefab，不重新放置 MainScene 中已经存在的实例。
    /// 适合场景还有其他未提交修改时使用，避免装配 UI 时覆盖场景编辑内容。
    /// </summary>
    public static void UpgradePrefabsFromCommandLine()
    {
        UpgradeStartupGuidePrefab();
        UpgradeStartupGuideInGameplayPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GAMEPLAY_UI_PREFABS_UPGRADE_SUCCEEDED");
    }

    private static void Rebuild()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            UpgradeStartupGuidePrefab();
            UpgradePrefab();
            PlacePrefabInMainScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            if (previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    /// <summary>
    /// 清理独立新手引导视图中覆盖在关闭图标上的旧文字。
    /// 关闭按钮已经使用“×”图片作为视觉表现，不再额外保留 Text 子对象。
    /// </summary>
    private static void UpgradeStartupGuidePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(StartupGuidePrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到新手引导 Prefab：{StartupGuidePrefabPath}");
        }

        try
        {
            Transform panel = RequireTransform(root.transform, "Panel");
            Transform closeButton = RequireTransform(panel, "CloseButton");
            Text[] legacyLabels = closeButton.GetComponentsInChildren<Text>(true);
            foreach (Text legacyLabel in legacyLabels)
            {
                // 关闭按钮本身已经有图标，删除旧文字可以避免两个“×”叠在一起。
                UnityEngine.Object.DestroyImmediate(legacyLabel.gameObject);
            }

            PrefabUtility.SaveAsPrefabAsset(root, StartupGuidePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 只给现有 GameplayUiRoot 补上新手引导，不触碰其他已经装配完成的玩法 UI。
    /// 当前项目还保留了一套旧层级迁移逻辑，这个窄入口可以避免旧迁移规则覆盖新版 Prefab。
    /// </summary>
    private static void UpgradeStartupGuideInGameplayPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到玩法 UI Prefab：{PrefabPath}");
        }

        try
        {
            Canvas canvas = RequireComponent<Canvas>(root);
            GameplayStartupGuidePopup startupGuide = GetOrAddComponent<GameplayStartupGuidePopup>(root);
            WireStartupGuide(root, canvas, startupGuide);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void UpgradePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到玩法 UI Prefab：{PrefabPath}");
        }

        try
        {
            Canvas canvas = RequireComponent<Canvas>(root);
            GameplayUiRoot uiRoot = RequireComponent<GameplayUiRoot>(root);
            PlayerHudUi playerHudUi = RequireComponent<PlayerHudUi>(root);
            GameSessionUi sessionUi = RequireComponent<GameSessionUi>(root);
            PlayerAttributePanel attributePanel = RequireComponent<PlayerAttributePanel>(root);
            PlayerLevelUpPanel levelUpPanel = RequireComponent<PlayerLevelUpPanel>(root);
            InventoryPanel inventoryPanel = RequireComponent<InventoryPanel>(root);
            GameplayStartupGuidePopup startupGuide = GetOrAddComponent<GameplayStartupGuidePopup>(root);

            WirePlayerHudUi(root, canvas, playerHudUi);
            WireSessionUi(root, canvas, sessionUi, inventoryPanel);
            PlayerAttributeRowView[] rows = RebuildAttributeRows(root);
            WireAttributePanel(root, canvas, sessionUi, attributePanel, rows);
            WireLevelUpPanel(root, canvas, levelUpPanel);
            WireStartupGuide(root, canvas, startupGuide);
            WireUiRoot(uiRoot, canvas, playerHudUi, sessionUi, attributePanel, levelUpPanel, inventoryPanel);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 把新手引导视图作为 GameplayUiRoot 的最后一个子节点，并写入全部序列化引用。
    /// 控制脚本放在 UI 根上，视图仍保留为独立 Prefab，方便后续单独调整样式。
    /// </summary>
    private static void WireStartupGuide(
        GameObject root,
        Canvas canvas,
        GameplayStartupGuidePopup view)
    {
        Transform popup = root.transform.Find("StartupGuidePopup");
        if (popup == null)
        {
            GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StartupGuidePrefabPath);
            if (popupPrefab == null)
            {
                throw new InvalidOperationException($"找不到新手引导 Prefab：{StartupGuidePrefabPath}");
            }

            GameObject popupInstance = PrefabUtility.InstantiatePrefab(popupPrefab, root.transform) as GameObject;
            if (popupInstance == null)
            {
                throw new InvalidOperationException("StartupGuidePopup 实例化失败。");
            }

            popupInstance.name = "StartupGuidePopup";
            popup = popupInstance.transform;
        }

        popup.SetAsLastSibling();
        Transform panel = RequireTransform(popup, "Panel");
        Transform closeButtonTransform = RequireTransform(panel, "CloseButton");

        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "popupRoot", popup.gameObject);
        SetReference(serialized, "backdropImage", RequireComponent<Image>(popup.gameObject));
        SetReference(serialized, "panelImage", RequireComponent<Image>(panel.gameObject));
        SetReference(serialized, "titleText", RequireComponent<Text>(RequireTransform(panel, "Title").gameObject));
        SetReference(serialized, "bodyText", RequireComponent<Text>(RequireTransform(panel, "Body").gameObject));
        SetReference(serialized, "closeButton", RequireComponent<Button>(closeButtonTransform.gameObject));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireSessionUi(
        GameObject root,
        Canvas canvas,
        GameSessionUi view,
        InventoryPanel inventoryPanel)
    {
        Transform hud = RequireTransform(root.transform, "GameHud");
        Transform overlay = RequireTransform(root.transform, "SessionOverlay");
        Transform panel = RequireTransform(overlay, "Panel");
        Button primaryButton = RequireComponent<Button>(RequireTransform(panel, "PrimaryButton").gameObject);
        Button secondaryButton = RequireComponent<Button>(RequireTransform(panel, "SecondaryButton").gameObject);

        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "hudRoot", hud.gameObject);
        SetReference(serialized, "scoreText", RequireComponent<Text>(RequireTransform(hud, "ScoreText").gameObject));
        SetReference(serialized, "chestBreakText", RequireComponent<Text>(RequireTransform(hud, "ChestBreakText").gameObject));
        SetReference(serialized, "pauseHintText", RequireComponent<Text>(RequireTransform(hud, "PauseHintText").gameObject));
        SetReference(serialized, "overlayRoot", overlay.gameObject);
        SetReference(serialized, "overlayTitleText", RequireComponent<Text>(RequireTransform(panel, "Title").gameObject));
        SetReference(serialized, "overlayBodyText", RequireComponent<Text>(RequireTransform(panel, "Body").gameObject));
        SetReference(serialized, "primaryButton", primaryButton);
        SetReference(serialized, "primaryButtonText", RequireComponent<Text>(RequireTransform(primaryButton.transform, "Label").gameObject));
        SetReference(serialized, "secondaryButton", secondaryButton);
        SetReference(serialized, "secondaryButtonText", RequireComponent<Text>(RequireTransform(secondaryButton.transform, "Label").gameObject));
        SetReference(serialized, "inventoryPanel", inventoryPanel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WirePlayerHudUi(GameObject root, Canvas canvas, PlayerHudUi view)
    {
        Transform hud = RequireTransform(root.transform, "GameHud");
        Transform panel = RequireTransform(hud, "PlayerHudPanel");
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "hudRoot", panel.gameObject);
        SetReference(serialized, "levelText", RequireComponent<Text>(RequireTransform(panel, "LevelText").gameObject));
        SetReference(serialized, "hpText", RequireComponent<Text>(RequireTransform(panel, "HpText").gameObject));
        SetReference(serialized, "mpText", RequireComponent<Text>(RequireTransform(panel, "MpText").gameObject));
        SetReference(serialized, "staminaText", RequireComponent<Text>(RequireTransform(panel, "StaminaText").gameObject));
        SetReference(serialized, "expText", RequireComponent<Text>(RequireTransform(panel, "ExpText").gameObject));
        SetReference(serialized, "hpBar", PrepareHudScrollbar(panel, "HpBar"));
        SetReference(serialized, "mpBar", PrepareHudScrollbar(panel, "MpBar"));
        SetReference(serialized, "staminaBar", PrepareHudScrollbar(panel, "StaminaBar"));
        SetReference(serialized, "expBar", PrepareHudScrollbar(panel, "ExpBar"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Scrollbar PrepareHudScrollbar(Transform panel, string barName)
    {
        Transform bar = RequireTransform(panel, barName);
        Transform fill = RequireTransform(bar, "Fill");
        Image fillImage = RequireComponent<Image>(fill.gameObject);
        fillImage.type = Image.Type.Simple;
        fillImage.fillAmount = 1f;

        Scrollbar scrollbar = GetOrAddComponent<Scrollbar>(bar.gameObject);
        scrollbar.handleRect = RequireComponent<RectTransform>(fill.gameObject);
        scrollbar.targetGraphic = RequireComponent<Image>(bar.gameObject);
        scrollbar.direction = Scrollbar.Direction.LeftToRight;
        scrollbar.interactable = false;
        scrollbar.numberOfSteps = 0;
        scrollbar.SetValueWithoutNotify(0f);
        return scrollbar;
    }

    private static PlayerAttributeRowView[] RebuildAttributeRows(GameObject root)
    {
        RectTransform content = RequireComponent<RectTransform>(
            RequireTransform(RequireTransform(root.transform, "PlayerAttributePanel"), "Content").gameObject);
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(content.GetChild(i).gameObject);
        }

        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        List<PlayerAttributeRowView> result = new List<PlayerAttributeRowView>(AttributeDefinitions.Length);
        string currentGroup = null;
        RectTransform rowsRoot = null;

        for (int i = 0; i < AttributeDefinitions.Length; i++)
        {
            AttributeDefinition definition = AttributeDefinitions[i];
            if (definition.Group != currentGroup)
            {
                currentGroup = definition.Group;
                rowsRoot = CreateSection(content, definition.Group, font);
            }

            result.Add(CreateAttributeRow(rowsRoot, definition, font));
        }

        return result.ToArray();
    }

    private static RectTransform CreateSection(Transform parent, string groupName, Font font)
    {
        GameObject section = new GameObject(
            $"Section_{groupName}",
            typeof(RectTransform),
            typeof(Image),
            typeof(Outline),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter),
            typeof(LayoutElement));
        section.transform.SetParent(parent, false);
        section.GetComponent<Image>().color = new Color(0.12f, 0.15f, 0.21f, 0.85f);
        Outline outline = section.GetComponent<Outline>();
        outline.effectColor = new Color(0.43f, 0.54f, 0.72f, 0.45f);
        outline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        section.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        section.GetComponent<LayoutElement>().flexibleWidth = 1f;

        Text title = CreateText("SectionTitle", section.transform, font, 15, new Color(0.83f, 0.89f, 0.97f, 1f), TextAnchor.MiddleLeft);
        title.text = groupName;
        title.gameObject.AddComponent<LayoutElement>().minHeight = 16f;

        GameObject rows = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rows.transform.SetParent(section.transform, false);
        VerticalLayoutGroup rowsLayout = rows.GetComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 4f;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = false;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        rows.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rows.GetComponent<RectTransform>();
    }

    private static PlayerAttributeRowView CreateAttributeRow(
        Transform parent,
        AttributeDefinition definition,
        Font font)
    {
        GameObject row = new GameObject(
            $"Row_{definition.Key}",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement),
            typeof(PlayerAttributeRowView));
        row.transform.SetParent(parent, false);
        Image background = row.GetComponent<Image>();
        background.color = new Color(1f, 1f, 1f, 0.06f);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 12f;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.minHeight = 28f;
        rowLayout.flexibleWidth = 1f;

        Text label = CreateText("Label", row.transform, font, 14, new Color(0.72f, 0.79f, 0.88f, 1f), TextAnchor.MiddleLeft);
        label.text = definition.Label;
        LayoutElement labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minWidth = 88f;

        Text value = CreateText("Value", row.transform, font, 14, new Color(0.96f, 0.98f, 1f, 1f), TextAnchor.MiddleRight);
        value.text = definition.PreviewValue;
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.minWidth = 84f;
        valueLayout.preferredWidth = 104f;

        PlayerAttributeRowView rowView = row.GetComponent<PlayerAttributeRowView>();
        SerializedObject serialized = new SerializedObject(rowView);
        serialized.FindProperty("key").stringValue = definition.Key;
        SetReference(serialized, "background", background);
        SetReference(serialized, "labelText", label);
        SetReference(serialized, "valueText", value);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return rowView;
    }

    private static void WireAttributePanel(
        GameObject root,
        Canvas canvas,
        GameSessionUi sessionUi,
        PlayerAttributePanel view,
        PlayerAttributeRowView[] rows)
    {
        Transform panel = RequireTransform(root.transform, "PlayerAttributePanel");
        Transform header = RequireTransform(panel, "Header");
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "sessionUi", sessionUi);
        SetReference(serialized, "panelRoot", panel.gameObject);
        SetReference(serialized, "contentRoot", RequireComponent<RectTransform>(RequireTransform(panel, "Content").gameObject));
        SetReference(serialized, "titleText", RequireComponent<Text>(RequireTransform(header, "Title").gameObject));
        SetReference(serialized, "summaryText", RequireComponent<Text>(RequireTransform(header, "Summary").gameObject));
        SerializedProperty rowProperty = serialized.FindProperty("rowViews");
        rowProperty.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            rowProperty.GetArrayElementAtIndex(i).objectReferenceValue = rows[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireLevelUpPanel(GameObject root, Canvas canvas, PlayerLevelUpPanel view)
    {
        Transform overlay = RequireTransform(root.transform, "PlayerLevelUpOverlay");
        Transform panel = RequireTransform(overlay, "LevelUpPanel");
        SerializedObject serialized = new SerializedObject(view);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "overlayRoot", overlay.gameObject);
        SetReference(serialized, "panelRoot", panel.gameObject);
        SetReference(serialized, "titleText", RequireComponent<Text>(RequireTransform(panel, "Title").gameObject));
        SetReference(serialized, "subtitleText", RequireComponent<Text>(RequireTransform(panel, "Subtitle").gameObject));
        SetReference(serialized, "queueText", RequireComponent<Text>(RequireTransform(panel, "QueueText").gameObject));

        SerializedProperty buttons = serialized.FindProperty("optionButtons");
        SerializedProperty texts = serialized.FindProperty("optionTexts");
        buttons.arraySize = 3;
        texts.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            Button button = RequireComponent<Button>(RequireTransform(panel, $"OptionButton{i + 1}").gameObject);
            buttons.GetArrayElementAtIndex(i).objectReferenceValue = button;
            texts.GetArrayElementAtIndex(i).objectReferenceValue =
                RequireComponent<Text>(RequireTransform(button.transform, "Label").gameObject);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireUiRoot(
        GameplayUiRoot root,
        Canvas canvas,
        PlayerHudUi playerHudUi,
        GameSessionUi sessionUi,
        PlayerAttributePanel attributePanel,
        PlayerLevelUpPanel levelUpPanel,
        InventoryPanel inventoryPanel)
    {
        SerializedObject serialized = new SerializedObject(root);
        SetReference(serialized, "targetCanvas", canvas);
        SetReference(serialized, "playerHudUi", playerHudUi);
        SetReference(serialized, "sessionUi", sessionUi);
        SetReference(serialized, "attributePanel", attributePanel);
        SetReference(serialized, "levelUpPanel", levelUpPanel);
        SetReference(serialized, "inventoryPanel", inventoryPanel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void PlacePrefabInMainScene()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameplayUiRoot[] existing = UnityEngine.Object.FindObjectsOfType<GameplayUiRoot>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].gameObject.scene == scene)
            {
                UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
            }
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("GameplayUiRoot 实例化失败。");
        }

        instance.name = "GameplayUiRoot";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        int fontSize,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        return text;
    }

    private static Transform RequireTransform(Transform parent, string path)
    {
        Transform result = parent.Find(path);
        if (result == null)
        {
            throw new InvalidOperationException($"GameplayUiRoot.prefab 缺少节点：{parent.name}/{path}");
        }

        return result;
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
            throw new InvalidOperationException($"{target.name} 缺少组件：{typeof(T).Name}");
        }

        return component;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} 缺少序列化字段：{propertyName}");
        }

        property.objectReferenceValue = value;
    }
}
#endif
