#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Mushroom 任务功能装配工具：幂等创建配置、任务 UI、NPC Prefab，并把稳定引用写入现有资源。
/// 将容易出错的 Inspector 拖拽集中成可重复执行的编辑器步骤，运行时不做资源查找或自动补组件。
/// </summary>
public static class QuestFeatureSetupTool
{
    private const string SetupRequestPath = "Temp/QuestFeatureSetup.request";
    private const string UiLayoutRequestPath = "Temp/QuestUiLayoutSetup.request";
    private const string CatalogFolder = "Assets/Resources/Data/Quest";
    private const string CatalogPath = CatalogFolder + "/QuestCatalog.asset";
    private const string QuestItemPrefabPath = "Assets/Prefabs/UI/QuestListItem.prefab";
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    private const string QuestNpcPrefabPath = "Assets/Prefabs/NPC/QuestMushroom.prefab";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string Slime1PrefabPath = "Assets/Prefabs/Slime1.prefab";
    private const string Slime2PrefabPath = "Assets/Prefabs/Slime2.prefab";
    private const string MushroomSourcePath =
        "Assets/AllResources/Monsters Ultimate Pack 01 Cute Series/Mushroom Cute Series/Prefabs/Mushroom.prefab";

    private const string TaobaoRoot = "Assets/AllResources/淘宝ui素材/RuntimeSprites";
    private const string ListBackgroundPath = TaobaoRoot + "/Progression/UI_Progression_Guild_List.png";
    private const string PanelBackgroundPath = TaobaoRoot + "/Progression/UI_Progression_Guild_Background.png";
    private const string ProgressFillPath = TaobaoRoot + "/Progression/UI_Progression_Missions_List_PrgBar_Fill.png";
    private const string GreenButtonPath = TaobaoRoot + "/Progression/UI_Progression_Missions_List_ButtonGreen_Btn_Normal.png";
    private const string CloseButtonPath = TaobaoRoot + "/Progression/UI_Progression_RewardDaily_Top_ButtonClose_Btn_Normal.png";
    private const string CloseIconPath = TaobaoRoot + "/Progression/UI_Progression_RewardDaily_Top_ButtonClose_Icon.png";
    private const string CoinIconPath = TaobaoRoot + "/Common/UI_Common_Component_Reward_Coin.png";
    private const string QuestionIconPath = TaobaoRoot + "/FunctionIcons/UI_FunctionIcon_MarkQuestion.png";
    private const string EnemyIconPath = TaobaoRoot + "/FunctionIcons/UI_FunctionIcon_Enemy.png";

    private static Font font;

    [MenuItem("Tools/Treasure Hunter/Quest/Rebuild Mushroom Quest Feature (Overwrites Manual Layout)")]
    public static void SetupFromMenu()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "重新生成 Mushroom 任务功能",
            "此操作会删除并重建 QuestFeature 和 QuestListItem，覆盖你手动调整的任务 UI 排版。\n\n确定继续吗？",
            "继续重建",
            "取消");
        if (!confirmed)
        {
            return;
        }

        SetupFeature();
        Debug.Log("Mushroom 任务系统资源装配完成。");
    }

    /// <summary>
    /// 打开玩法 UI Prefab，并把运行时默认隐藏的任务窗口临时放出来，便于直接拖拽排版。
    /// QuestPanel.Start 会在进入游戏时重新隐藏窗口，所以编辑态可见不会改变运行时开关流程。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/Quest/Edit Quest Panel Layout")]
    public static void OpenQuestPanelLayoutForEditing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        if (prefab == null || !AssetDatabase.OpenAsset(prefab))
        {
            Debug.LogError($"无法打开任务 UI Prefab：{GameplayUiPrefabPath}");
            return;
        }

        EditorApplication.delayCall += FocusQuestPanelLayout;
    }

    [MenuItem("Tools/Treasure Hunter/Quest/Edit Quest List Item Layout")]
    public static void OpenQuestListItemLayoutForEditing()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestItemPrefabPath);
        if (prefab == null || !AssetDatabase.OpenAsset(prefab))
        {
            Debug.LogError($"无法打开任务条目 Prefab：{QuestItemPrefabPath}");
            return;
        }

        EditorApplication.delayCall += () =>
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return;
            }

            Selection.activeGameObject = stage.prefabContentsRoot;
            EditorGUIUtility.PingObject(stage.prefabContentsRoot);
            SceneView.lastActiveSceneView?.FrameSelected();
        };
    }

    private static void FocusQuestPanelLayout()
    {
        PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
        Transform feature = stage != null ? stage.prefabContentsRoot.transform.Find("QuestFeature") : null;
        Transform prompt = feature != null ? feature.Find("QuestPrompt") : null;
        Transform modal = feature != null ? feature.Find("QuestModal") : null;
        Transform panel = modal != null ? modal.Find("Panel") : null;
        if (panel == null)
        {
            Debug.LogError("GameplayUiRoot.prefab 缺少 QuestFeature/QuestModal/Panel，无法进入任务 UI 排版状态。");
            return;
        }

        if (prompt != null && prompt.gameObject.activeSelf)
        {
            Undo.RecordObject(prompt.gameObject, "隐藏任务交互提示");
            prompt.gameObject.SetActive(false);
        }
        if (!modal.gameObject.activeSelf)
        {
            Undo.RecordObject(modal.gameObject, "显示任务排版窗口");
            modal.gameObject.SetActive(true);
        }

        EditorSceneManager.MarkSceneDirty(stage.scene);
        Selection.activeGameObject = panel.gameObject;
        EditorGUIUtility.PingObject(panel.gameObject);
        SceneView.lastActiveSceneView?.FrameSelected();
        Debug.Log("已打开任务面板排版模式：调整完成后按 Ctrl+S 保存；请勿执行带 Overwrites Manual Layout 的重建菜单。");
    }

    public static void SetupFromCommandLine()
    {
        SetupFeature();
        Debug.Log("QUEST_FEATURE_SETUP_SUCCEEDED");
    }

    /// <summary>
    /// 只重新生成任务 UI，不触碰任务配置、史莱姆、NPC 或场景。
    /// UI 排版迭代使用这个入口，可以把编辑器工具的影响范围控制在两个 UI Prefab 内。
    /// </summary>
    public static void ApplyUiLayoutFromCommandLine()
    {
        ApplyUiLayout();
        Debug.Log("QUEST_UI_LAYOUT_SETUP_SUCCEEDED");
    }

    /// <summary>
    /// Unity 已经打开项目时，批处理进程不能同时进入同一工程。
    /// 通过一次性 Temp 标记让当前编辑器在脚本重载后执行装配，执行完立即删除标记，不会反复改资源。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ScheduleRequestedSetup()
    {
        if (!File.Exists(SetupRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(SetupRequestPath))
            {
                return;
            }

            File.Delete(SetupRequestPath);
            SetupFeature();
            Debug.Log("QUEST_FEATURE_SETUP_SUCCEEDED_IN_OPEN_EDITOR");
        };
    }

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedUiLayout()
    {
        if (!File.Exists(UiLayoutRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(UiLayoutRequestPath))
            {
                return;
            }

            File.Delete(UiLayoutRequestPath);
            ApplyUiLayout();
            Debug.Log("QUEST_UI_LAYOUT_SETUP_SUCCEEDED_IN_OPEN_EDITOR");
        };
    }

    private static void SetupFeature()
    {
        SceneSetup[] previousScenes = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/Data");
            EnsureFolder(CatalogFolder);
            EnsureFolder("Assets/Prefabs/NPC");

            BuildQuestCatalog();
            ConfigureSlimePrefab(Slime1PrefabPath, MonsterKind.RedSlime);
            ConfigureSlimePrefab(Slime2PrefabPath, MonsterKind.GreenSlime);
            QuestListItemView itemPrefab = BuildQuestItemPrefab();
            GameObject npcPrefab = BuildQuestNpcPrefab();
            UpgradeGameplayUiPrefab(itemPrefab);
            PlaceQuestNpcInMainScene(npcPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        finally
        {
            if (previousScenes.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            }
        }
    }

    private static void ApplyUiLayout()
    {
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        QuestListItemView itemPrefab = BuildQuestItemPrefab();
        UpgradeGameplayUiPrefab(itemPrefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void BuildQuestCatalog()
    {
        QuestCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        SerializedObject serialized = new SerializedObject(catalog);
        SerializedProperty entries = RequireProperty(serialized, "entries");
        entries.arraySize = 2;
        ConfigureQuestDefinition(
            entries.GetArrayElementAtIndex(0),
            "hunt_red_slime",
            "清理红色史莱姆",
            "击杀 5 只红色史莱姆",
            MonsterKind.RedSlime,
            5,
            50L);
        ConfigureQuestDefinition(
            entries.GetArrayElementAtIndex(1),
            "hunt_green_slime",
            "清理绿色史莱姆",
            "击杀 8 只绿色史莱姆",
            MonsterKind.GreenSlime,
            8,
            80L);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void ConfigureQuestDefinition(
        SerializedProperty definition,
        string questId,
        string displayName,
        string description,
        MonsterKind monsterKind,
        int requiredCount,
        long reward)
    {
        definition.FindPropertyRelative("questId").stringValue = questId;
        definition.FindPropertyRelative("displayName").stringValue = displayName;
        definition.FindPropertyRelative("description").stringValue = description;
        definition.FindPropertyRelative("objectiveType").enumValueIndex = (int)QuestObjectiveType.KillMonster;
        definition.FindPropertyRelative("targetMonster").enumValueIndex = (int)monsterKind;
        definition.FindPropertyRelative("requiredCount").intValue = requiredCount;
        definition.FindPropertyRelative("goldReward").longValue = reward;
    }

    private static void ConfigureSlimePrefab(string prefabPath, MonsterKind monsterKind)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到史莱姆 Prefab：{prefabPath}");
        }

        try
        {
            SlimeCo slime = root.GetComponentInChildren<SlimeCo>(true);
            if (slime == null)
            {
                throw new InvalidOperationException($"{prefabPath} 缺少 SlimeCo。");
            }

            SerializedObject serialized = new SerializedObject(slime);
            RequireProperty(serialized, "monsterKind").enumValueIndex = (int)monsterKind;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            MonsterQuestProgressReporter reporter = slime.GetComponent<MonsterQuestProgressReporter>();
            if (reporter == null)
            {
                slime.gameObject.AddComponent<MonsterQuestProgressReporter>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static QuestListItemView BuildQuestItemPrefab()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(QuestItemPrefabPath) == null
            ? new GameObject("QuestListItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(QuestListItemView))
            : PrefabUtility.LoadPrefabContents(QuestItemPrefabPath);
        bool isLoadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestItemPrefabPath) != null;

        try
        {
            ClearChildren(root.transform);
            RectTransform rootRect = RequireComponent<RectTransform>(root);
            rootRect.sizeDelta = new Vector2(820f, 180f);
            Image background = RequireComponent<Image>(root);
            background.sprite = LoadSprite(ListBackgroundPath);
            background.type = Image.Type.Sliced;
            background.color = Color.white;
            LayoutElement rootLayout = RequireComponent<LayoutElement>(root);
            rootLayout.minHeight = 180f;
            rootLayout.preferredHeight = 180f;
            rootLayout.flexibleWidth = 1f;

            // 淘宝任务条背景左侧已经绘制了徽章、右侧已经绘制了奖杯。
            // 所有可变内容都放进中间安全区域，避免再次遮住素材本身的装饰。
            Image objectiveIcon = CreateImage("ObjectiveIcon", root.transform, LoadSprite(EnemyIconPath));
            SetAnchoredRect(objectiveIcon.rectTransform, new Vector2(145f, 122f), new Vector2(32f, 32f));
            objectiveIcon.preserveAspect = true;

            Text title = CreateText("Title", root.transform, 25, new Color32(255, 236, 177, 255), TextAnchor.MiddleLeft);
            SetAnchoredRect(title.rectTransform, new Vector2(185f, 117f), new Vector2(305f, 40f));
            ConfigureBoundedText(title, 19, 25);
            title.fontStyle = FontStyle.Bold;

            Text description = CreateText("Description", root.transform, 18, new Color32(239, 235, 222, 255), TextAnchor.MiddleLeft);
            SetAnchoredRect(description.rectTransform, new Vector2(145f, 76f), new Vector2(345f, 34f));
            ConfigureBoundedText(description, 15, 18);

            Image progressTrack = CreateImage("ProgressTrack", root.transform, null);
            progressTrack.color = new Color(0.10f, 0.08f, 0.07f, 0.85f);
            SetAnchoredRect(progressTrack.rectTransform, new Vector2(145f, 34f), new Vector2(335f, 24f));
            Image progressFill = CreateImage("Fill", progressTrack.transform, LoadSprite(ProgressFillPath));
            Stretch(progressFill.rectTransform, Vector2.zero, Vector2.zero);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = 0;
            progressFill.fillAmount = 0f;
            Text progressText = CreateText("ProgressText", progressTrack.transform, 16, Color.white, TextAnchor.MiddleCenter);
            Stretch(progressText.rectTransform, Vector2.zero, Vector2.zero);
            ConfigureBoundedText(progressText, 13, 16);

            Image coin = CreateImage("RewardCoin", root.transform, LoadSprite(CoinIconPath));
            SetAnchoredRect(coin.rectTransform, new Vector2(515f, 122f), new Vector2(36f, 36f));
            coin.preserveAspect = true;
            Text reward = CreateText("RewardText", root.transform, 23, new Color32(255, 224, 96, 255), TextAnchor.MiddleLeft);
            SetAnchoredRect(reward.rectTransform, new Vector2(558f, 116f), new Vector2(128f, 44f));
            ConfigureBoundedText(reward, 16, 23);
            reward.fontStyle = FontStyle.Bold;

            Button actionButton = CreateButton("ActionButton", root.transform, LoadSprite(GreenButtonPath));
            SetAnchoredRect(actionButton.GetComponent<RectTransform>(), new Vector2(515f, 31f), new Vector2(170f, 62f));
            Text actionText = CreateText("Label", actionButton.transform, 20, Color.white, TextAnchor.MiddleCenter);
            Stretch(actionText.rectTransform, Vector2.zero, Vector2.zero);
            ConfigureBoundedText(actionText, 15, 20);
            actionText.fontStyle = FontStyle.Bold;

            QuestListItemView view = RequireComponent<QuestListItemView>(root);
            SerializedObject serialized = new SerializedObject(view);
            SetReference(serialized, "titleText", title);
            SetReference(serialized, "objectiveIcon", objectiveIcon);
            SetReference(serialized, "descriptionText", description);
            SetReference(serialized, "progressText", progressText);
            SetReference(serialized, "progressFill", progressFill);
            SetReference(serialized, "rewardText", reward);
            SetReference(serialized, "actionButton", actionButton);
            SetReference(serialized, "actionButtonText", actionText);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, QuestItemPrefabPath);
            QuestListItemView savedView = saved.GetComponent<QuestListItemView>();
            if (savedView == null)
            {
                throw new InvalidOperationException("QuestListItem Prefab 保存失败。");
            }
            return savedView;
        }
        finally
        {
            if (isLoadedPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static GameObject BuildQuestNpcPrefab()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomSourcePath);
        if (source == null)
        {
            throw new InvalidOperationException($"找不到 Mushroom 模型：{MushroomSourcePath}");
        }

        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(QuestNpcPrefabPath) == null
            ? new GameObject("QuestMushroom")
            : PrefabUtility.LoadPrefabContents(QuestNpcPrefabPath);
        bool isLoadedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(QuestNpcPrefabPath) != null;
        try
        {
            ClearChildren(root.transform);
            root.name = "QuestMushroom";
            QuestNpcController controller = GetOrAddComponent<QuestNpcController>(root);
            SphereCollider trigger = GetOrAddComponent<SphereCollider>(root);
            trigger.isTrigger = true;
            trigger.radius = 3f;
            Rigidbody body = GetOrAddComponent<Rigidbody>(root);
            body.isKinematic = true;
            body.useGravity = false;

            GameObject visual = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
            if (visual == null)
            {
                throw new InvalidOperationException("Mushroom 模型实例化失败。");
            }
            visual.name = "MushroomVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            GameObject marker = new GameObject("QuestMarker", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            marker.transform.SetParent(root.transform, false);
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            markerRect.localPosition = new Vector3(0f, 2.25f, 0f);
            markerRect.localScale = Vector3.one * 0.01f;
            markerRect.sizeDelta = new Vector2(100f, 100f);
            Canvas markerCanvas = marker.GetComponent<Canvas>();
            markerCanvas.renderMode = RenderMode.WorldSpace;
            markerCanvas.sortingOrder = 20;
            Image question = CreateImage("Question", marker.transform, LoadSprite(QuestionIconPath));
            Stretch(question.rectTransform, Vector2.zero, Vector2.zero);
            question.preserveAspect = true;
            question.raycastTarget = false;

            SerializedObject serialized = new SerializedObject(controller);
            RequireProperty(serialized, "interactionRadius").floatValue = 3f;
            SetReference(serialized, "questMarkerRoot", marker);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, QuestNpcPrefabPath);
            if (saved == null)
            {
                throw new InvalidOperationException("QuestMushroom Prefab 保存失败。");
            }
            return saved;
        }
        finally
        {
            if (isLoadedPrefab)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static void UpgradeGameplayUiPrefab(QuestListItemView itemPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameplayUiPrefabPath);
        if (root == null)
        {
            throw new InvalidOperationException($"找不到玩法 UI Prefab：{GameplayUiPrefabPath}");
        }

        try
        {
            Canvas canvas = RequireComponent<Canvas>(root);
            GameplayUiRoot uiRoot = RequireComponent<GameplayUiRoot>(root);
            GameSessionUi sessionUi = RequireComponent<GameSessionUi>(root);
            InventoryPanel inventory = RequireComponent<InventoryPanel>(root);
            MerchantShopPanel merchant = RequireComponent<MerchantShopPanel>(root);
            QuestPanel questPanel = GetOrAddComponent<QuestPanel>(root);

            Transform oldFeature = root.transform.Find("QuestFeature");
            if (oldFeature != null)
            {
                UnityEngine.Object.DestroyImmediate(oldFeature.gameObject);
            }

            RectTransform feature = CreateRect("QuestFeature", root.transform);
            Stretch(feature, Vector2.zero, Vector2.zero);

            Image prompt = CreateImage("QuestPrompt", feature, LoadSprite(ListBackgroundPath));
            prompt.type = Image.Type.Sliced;
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(490f, 64f), new Vector2(0f, 90f));
            Text promptText = CreateText("Label", prompt.transform, 22, new Color32(255, 239, 193, 255), TextAnchor.MiddleCenter);
            Stretch(promptText.rectTransform, new Vector2(15f, 8f), new Vector2(-15f, -8f));
            promptText.text = "按 E 查看蘑菇委托";

            Image modal = CreateImage("QuestModal", feature, null);
            Stretch(modal.rectTransform, Vector2.zero, Vector2.zero);
            modal.color = new Color(0f, 0f, 0f, 0.68f);

            Image panel = CreateImage("Panel", modal.transform, LoadSprite(PanelBackgroundPath));
            panel.type = Image.Type.Sliced;
            SetRect(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1050f, 660f), Vector2.zero);

            Text title = CreateText("Title", panel.transform, 34, new Color32(255, 226, 151, 255), TextAnchor.MiddleCenter);
            SetAnchoredRect(
                title.rectTransform,
                new Vector2(0f, -28f),
                new Vector2(650f, 58f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f));
            ConfigureBoundedText(title, 26, 34);
            title.text = "蘑菇委托";
            title.fontStyle = FontStyle.Bold;

            Button close = CreateButton("CloseButton", panel.transform, LoadSprite(CloseButtonPath));
            SetAnchoredRect(
                close.GetComponent<RectTransform>(),
                new Vector2(-22f, -22f),
                new Vector2(62f, 62f),
                Vector2.one,
                Vector2.one);
            Image closeIcon = CreateImage("Icon", close.transform, LoadSprite(CloseIconPath));
            Stretch(closeIcon.rectTransform, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            closeIcon.preserveAspect = true;

            RectTransform content = CreateRect("Content", panel.transform);
            SetAnchoredRect(
                content,
                new Vector2(0f, 112f),
                new Vector2(840f, 400f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            Text feedback = CreateText("Feedback", panel.transform, 18, new Color32(241, 220, 169, 255), TextAnchor.MiddleCenter);
            SetAnchoredRect(
                feedback.rectTransform,
                new Vector2(0f, 54f),
                new Vector2(850f, 38f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f));
            ConfigureBoundedText(feedback, 15, 18);
            feedback.text = "选择一项委托开始冒险。";

            SerializedObject questSerialized = new SerializedObject(questPanel);
            SetReference(questSerialized, "targetCanvas", canvas);
            SetReference(questSerialized, "sessionUi", sessionUi);
            SetReference(questSerialized, "inventoryPanel", inventory);
            SetReference(questSerialized, "merchantShopPanel", merchant);
            SetReference(questSerialized, "promptRoot", prompt.gameObject);
            SetReference(questSerialized, "promptText", promptText);
            SetReference(questSerialized, "panelRoot", modal.gameObject);
            SetReference(questSerialized, "closeButton", close);
            SetReference(questSerialized, "contentRoot", content);
            SetReference(questSerialized, "itemPrefab", itemPrefab);
            SetReference(questSerialized, "feedbackText", feedback);
            questSerialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject rootSerialized = new SerializedObject(uiRoot);
            SetReference(rootSerialized, "questPanel", questPanel);
            rootSerialized.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject sessionSerialized = new SerializedObject(sessionUi);
            SetReference(sessionSerialized, "questPanel", questPanel);
            sessionSerialized.ApplyModifiedPropertiesWithoutUndo();

            prompt.gameObject.SetActive(false);
            // Prefab 默认必须关闭全屏任务层，避免它在运行时初始化前遮住商店。
            // 需要排版时使用 Edit Quest Panel Layout 菜单临时显示。
            modal.gameObject.SetActive(false);
            feature.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAsset(root, GameplayUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void PlaceQuestNpcInMainScene(GameObject npcPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        QuestNpcController[] existing = UnityEngine.Object.FindObjectsOfType<QuestNpcController>(true);
        GameObject instance = null;
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i].gameObject.scene != scene)
            {
                continue;
            }

            if (instance == null && PrefabUtility.GetCorrespondingObjectFromSource(existing[i].gameObject) == npcPrefab)
            {
                instance = existing[i].gameObject;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(existing[i].gameObject);
        }

        if (instance == null)
        {
            instance = PrefabUtility.InstantiatePrefab(npcPrefab, scene) as GameObject;
        }
        if (instance == null)
        {
            throw new InvalidOperationException("QuestMushroom 场景实例化失败。");
        }

        instance.name = "Mushroom";
        // 两个 NPC 都是 3 米交互半径；与 x=2.7 的 Fungi 保持 6 米以上距离，避免出生点同时出现两个 E 提示。
        instance.transform.position = new Vector3(-3.4f, 0.15232706f, 1.29f);
        instance.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        instance.transform.localScale = Vector3.one * 1.5f;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.transform.SetParent(parent, false);
        return target.GetComponent<RectTransform>();
    }

    private static Image CreateImage(string name, Transform parent, Sprite sprite)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.sprite = sprite;
        return image;
    }

    private static Text CreateText(string name, Transform parent, int size, Color color, TextAnchor alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Text));
        target.transform.SetParent(parent, false);
        Text text = target.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        return text;
    }

    /// <summary>
    /// 限制文字只能在自己的 RectTransform 内显示。
    /// 自动缩小只处理极端长文本，正常任务名称仍使用配置的最大字号。
    /// </summary>
    private static void ConfigureBoundedText(Text text, int minSize, int maxSize)
    {
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = minSize;
        text.resizeTextMaxSize = maxSize;
    }

    private static Button CreateButton(string name, Transform parent, Sprite sprite)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        Button button = target.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        button.colors = colors;
        return button;
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        SetAnchoredRect(rect, position, size, Vector2.zero);
    }

    private static void SetAnchoredRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 anchor)
    {
        SetAnchoredRect(rect, position, size, anchor, Vector2.zero);
    }

    private static void SetAnchoredRect(
        RectTransform rect,
        Vector2 position,
        Vector2 size,
        Vector2 anchor,
        Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            throw new InvalidOperationException($"找不到任务 UI Sprite：{path}");
        }
        return sprite;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string folder = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
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

    private static SerializedProperty RequireProperty(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException($"{serialized.targetObject.GetType().Name} 缺少字段：{propertyName}");
        }
        return property;
    }

    private static void SetReference(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        RequireProperty(serialized, propertyName).objectReferenceValue = value;
    }
}
#endif
