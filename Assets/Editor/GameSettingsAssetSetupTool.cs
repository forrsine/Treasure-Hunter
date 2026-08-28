#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PC 设置资源装配工具：生成配置资产、可复用设置面板 Prefab，并绑定登录场景现有设置按钮。
/// 工具可以重复执行，便于 UI 引用丢失时恢复；不会重建或覆盖整个 LoginScene。
/// </summary>
public static class GameSettingsAssetSetupTool
{
    public const string ConfigAssetPath = "Assets/Resources/Data/GameSettingsConfig.asset";
    public const string PanelPrefabPath = "Assets/Prefabs/UI/GameSettingsPanel.prefab";
    public const string LoginScenePath = "Assets/Scenes/LoginScene.unity";

    private const string MainMixerPath = "Assets/AllResources/Audio/Main.mixer";
    private const string PurchasedUiSpriteRoot = "Assets/AllResources/淘宝ui素材/RuntimeSprites/";

    /// <summary>
    /// 首次加入设置系统时自动生成缺失资源。
    /// 如果当前场景存在未保存修改则停止自动装配，避免覆盖用户正在编辑的场景。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ScheduleMissingAssetSetup()
    {
        EditorApplication.delayCall += TryCreateMissingAssets;
    }

    [MenuItem("Treasure Hunter/UI/Create Or Refresh Login Settings")]
    public static void CreateOrRefreshFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        CreateOrRefresh();
    }

    /// <summary>
    /// 命令行入口：供自动化装配资源，不弹出确认框。
    /// </summary>
    public static void CreateOrRefreshFromCommandLine()
    {
        CreateOrRefresh();
        Debug.Log("GAME_SETTINGS_ASSET_SETUP_SUCCEEDED");
    }

    private static void TryCreateMissingAssets()
    {
        if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }

        bool configMissing = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(ConfigAssetPath) == null;
        bool prefabMissing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) == null;
        if (!configMissing && !prefabMissing)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.isDirty)
        {
            Debug.LogWarning(
                "检测到当前场景有未保存修改，已暂停自动生成设置面板。保存场景后可从 Treasure Hunter/UI 菜单重新执行。 ");
            return;
        }

        CreateOrRefresh();
    }

    private static void CreateOrRefresh()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        bool shouldRestoreSceneSetup = !Application.isBatchMode && previousSceneSetup.Length > 0;

        try
        {
            CreateOrRefreshConfigAsset();
            CreateOrRefreshPanelPrefab();
            ConfigureLoginScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("登录界面 PC 设置面板、配置资产和按钮绑定已完成。 ");
        }
        finally
        {
            if (shouldRestoreSceneSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    private static void CreateOrRefreshConfigAsset()
    {
        GameSettingsConfig config = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(ConfigAssetPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GameSettingsConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
        }

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MainMixerPath);
        AudioMixerGroup musicGroup = FindMixerGroup(mixer, "Music");
        AudioMixerGroup soundsGroup = FindMixerGroup(mixer, "Sounds");

        SerializedObject serializedConfig = new SerializedObject(config);
        serializedConfig.FindProperty("audioMixer").objectReferenceValue = mixer;
        serializedConfig.FindProperty("musicMixerGroup").objectReferenceValue = musicGroup;
        serializedConfig.FindProperty("soundsMixerGroup").objectReferenceValue = soundsGroup;
        serializedConfig.FindProperty("musicVolumeParameter").stringValue = "musicVolume";
        serializedConfig.FindProperty("soundsVolumeParameter").stringValue = "soundsVolume";
        serializedConfig.FindProperty("defaultMasterVolume").floatValue = 1f;
        serializedConfig.FindProperty("defaultMusicVolume").floatValue = 1f;
        serializedConfig.FindProperty("defaultSoundEffectsVolume").floatValue = 1f;
        serializedConfig.FindProperty("defaultMouseSensitivity").floatValue = 1f;
        serializedConfig.FindProperty("defaultDisplayMode").enumValueIndex =
            (int)GameDisplayMode.BorderlessFullscreen;
        serializedConfig.FindProperty("defaultQualityLevel").intValue = 5;
        serializedConfig.FindProperty("defaultVerticalSync").boolValue = true;
        serializedConfig.FindProperty("defaultFrameRateLimit").intValue = -1;
        serializedConfig.FindProperty("minimumResolutionWidth").intValue = 1280;
        serializedConfig.FindProperty("minimumResolutionHeight").intValue = 720;
        SetIntegerArray(
            serializedConfig.FindProperty("supportedFrameRateLimits"),
            new[] { 30, 60, 90, 120, 144, 165, 240, -1 });
        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);

        if (mixer == null || musicGroup == null || soundsGroup == null)
        {
            Debug.LogError("GameSettingsConfig 无法找到 Main.mixer 或 Music/Sounds 分组。 ");
        }
    }

    private static void CreateOrRefreshPanelPrefab()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        DefaultControls.Resources uiResources = CreateDefaultControlResources();
        SettingsUiSkin skin = LoadSettingsUiSkin();

        GameObject root = new GameObject(
            "GameSettingsPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(GameSettingsPanelController));

        try
        {
            RectTransform rootRect = root.GetComponent<RectTransform>();
            StretchToParent(rootRect);
            Image background = root.GetComponent<Image>();
            ConfigureSpriteImage(background, skin.background, Image.Type.Simple, true);
            background.preserveAspect = false;

            Image window = CreateImage(
                "SettingsWindow",
                root.transform,
                Color.clear,
                false);
            StretchToParent(window.rectTransform);

            Text title = CreateText(
                "Title",
                window.transform,
                font,
                "游戏设置",
                46,
                Color.white,
                new Vector2(-600f, 465f),
                new Vector2(420f, 70f),
                TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;

            Button cancelButton = CreateIconButton(
                window.transform,
                skin.back,
                "CancelButton",
                new Vector2(-870f, 465f),
                new Vector2(84f, 84f));

            CreateDecorativeImage(
                "HeaderDivider",
                window.transform,
                skin.divider,
                new Vector2(0f, 390f),
                new Vector2(1802f, 10f));
            CreateDecorativeImage(
                "FooterDivider",
                window.transform,
                skin.divider,
                new Vector2(0f, -340f),
                new Vector2(1802f, 10f));

            Slider masterSlider = CreateSliderRow(
                window.transform, uiResources, font, skin, skin.volumeUpIcon,
                "MasterVolume", "主音量", 250f, 0f, 1f,
                out Text masterValue);
            Slider musicSlider = CreateSliderRow(
                window.transform, uiResources, font, skin, skin.musicIcon,
                "MusicVolume", "音乐音量", 90f, 0f, 1f,
                out Text musicValue);
            Slider soundsSlider = CreateSliderRow(
                window.transform, uiResources, font, skin, skin.soundIcon,
                "SoundEffectsVolume", "音效音量", -70f, 0f, 1f,
                out Text soundsValue);
            Slider sensitivitySlider = CreateSliderRow(
                window.transform, uiResources, font, skin, skin.setting2Icon,
                "MouseSensitivity", "鼠标灵敏度", -230f, 0.5f, 2f,
                out Text sensitivityValue);

            Dropdown resolutionDropdown = CreateDropdownRow(
                window.transform, uiResources, font, skin, skin.displayIcon,
                "Resolution", "分辨率", 270f);
            Dropdown displayModeDropdown = CreateDropdownRow(
                window.transform, uiResources, font, skin, skin.setting1Icon,
                "DisplayMode", "显示模式", 145f);
            Dropdown qualityDropdown = CreateDropdownRow(
                window.transform, uiResources, font, skin, skin.gameIcon,
                "Quality", "画质", 20f);
            Toggle vSyncToggle = CreateToggleRow(
                window.transform, uiResources, font, skin, skin.refreshIcon,
                "VerticalSync", "垂直同步", -105f);
            Dropdown frameRateDropdown = CreateDropdownRow(
                window.transform, uiResources, font, skin, skin.timerIcon,
                "FrameRateLimit", "帧率上限", -230f);

            Button defaultsButton = CreateButton(
                window.transform, uiResources, font, skin.grayButton,
                "RestoreDefaultsButton", "恢复默认",
                new Vector2(-190f, -445f), new Vector2(320f, 82f),
                new Color32(62, 46, 83, 255));
            Button applyButton = CreateButton(
                window.transform, uiResources, font, skin.greenButton,
                "ApplyButton", "应用",
                new Vector2(190f, -445f), new Vector2(320f, 82f),
                Color.white);

            GameObject confirmationPanel = CreateConfirmationPanel(
                root.transform,
                uiResources,
                font,
                skin,
                out Text confirmationText,
                out Button keepButton,
                out Button revertButton);

            GameSettingsPanelController controller = root.GetComponent<GameSettingsPanelController>();
            SerializedObject serializedController = new SerializedObject(controller);
            SetReference(serializedController, "masterVolumeSlider", masterSlider);
            SetReference(serializedController, "masterVolumeValueText", masterValue);
            SetReference(serializedController, "musicVolumeSlider", musicSlider);
            SetReference(serializedController, "musicVolumeValueText", musicValue);
            SetReference(serializedController, "soundEffectsVolumeSlider", soundsSlider);
            SetReference(serializedController, "soundEffectsVolumeValueText", soundsValue);
            SetReference(serializedController, "mouseSensitivitySlider", sensitivitySlider);
            SetReference(serializedController, "mouseSensitivityValueText", sensitivityValue);
            SetReference(serializedController, "resolutionDropdown", resolutionDropdown);
            SetReference(serializedController, "displayModeDropdown", displayModeDropdown);
            SetReference(serializedController, "qualityDropdown", qualityDropdown);
            SetReference(serializedController, "verticalSyncToggle", vSyncToggle);
            SetReference(serializedController, "frameRateDropdown", frameRateDropdown);
            SetReference(serializedController, "restoreDefaultsButton", defaultsButton);
            SetReference(serializedController, "cancelButton", cancelButton);
            SetReference(serializedController, "applyButton", applyButton);
            SetReference(serializedController, "displayConfirmationPanel", confirmationPanel);
            SetReference(serializedController, "displayConfirmationText", confirmationText);
            SetReference(serializedController, "keepDisplaySettingsButton", keepButton);
            SetReference(serializedController, "revertDisplaySettingsButton", revertButton);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            SetLayerRecursively(root, LayerMask.NameToLayer("UI"));
            confirmationPanel.SetActive(false);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PanelPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureLoginScene()
    {
        Scene scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Single);
        GameObject canvasObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
        if (canvasObject == null)
        {
            throw new MissingReferenceException("LoginScene 缺少根 Canvas，无法装配设置面板。 ");
        }

        Transform oldPanel = canvasObject.transform.Find("GameSettingsPanel");
        if (oldPanel != null)
        {
            Object.DestroyImmediate(oldPanel.gameObject);
        }

        GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
        GameObject panelInstance = PrefabUtility.InstantiatePrefab(panelPrefab, scene) as GameObject;
        if (panelInstance == null)
        {
            throw new MissingReferenceException("GameSettingsPanel Prefab 实例化失败。 ");
        }

        panelInstance.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelInstance.GetComponent<RectTransform>();
        StretchToParent(panelRect);
        panelRect.SetAsLastSibling();
        panelInstance.SetActive(false);

        Transform settingButtonTransform = canvasObject.transform.Find("SettingButton");
        Button settingButton = settingButtonTransform != null
            ? settingButtonTransform.GetComponent<Button>()
            : null;
        GameSettingsPanelController panelController =
            panelInstance.GetComponent<GameSettingsPanelController>();
        if (settingButton == null || panelController == null)
        {
            throw new MissingReferenceException("LoginScene 的 SettingButton 或设置面板控制器缺失。 ");
        }

        // 入口只替换表现资源，保留原按钮尺寸、位置和业务点击事件。
        Image settingButtonImage = settingButton.GetComponent<Image>();
        if (settingButtonImage == null)
        {
            throw new MissingReferenceException("LoginScene 的 SettingButton 缺少 Image 组件。 ");
        }

        ConfigureSpriteImage(
            settingButtonImage,
            LoadPurchasedSprite("Home/UI_Home_Top_ButtonSetting_Icon"),
            Image.Type.Simple,
            true);
        settingButtonImage.preserveAspect = true;

        RemoveOldSettingsPanelListeners(settingButton);
        UnityEventTools.AddPersistentListener(settingButton.onClick, panelController.Open);
        EditorUtility.SetDirty(settingButton);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static AudioMixerGroup FindMixerGroup(AudioMixer mixer, string groupName)
    {
        if (mixer == null)
        {
            return null;
        }

        return mixer.FindMatchingGroups(groupName)
            .FirstOrDefault(group => group != null && group.name == groupName);
    }

    private static DefaultControls.Resources CreateDefaultControlResources()
    {
        return new DefaultControls.Resources
        {
            standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
            background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
            knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
            checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
            dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
            mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd")
        };
    }

    private static Slider CreateSliderRow(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        SettingsUiSkin skin,
        Sprite iconSprite,
        string objectName,
        string label,
        float y,
        float minimum,
        float maximum,
        out Text valueText)
    {
        CreateDecorativeImage(
            objectName + "Icon",
            parent,
            iconSprite,
            new Vector2(-865f, y),
            new Vector2(64f, 64f));
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(-690f, y), new Vector2(230f, 54f), TextAnchor.MiddleLeft);

        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = objectName + "Slider";
        sliderObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            sliderObject.GetComponent<RectTransform>(),
            new Vector2(-300f, y),
            new Vector2(470f, 54f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = maximum;
        slider.wholeNumbers = false;

        Transform backgroundTransform = sliderObject.transform.Find("Background");
        Image sliderBackground = backgroundTransform != null
            ? backgroundTransform.GetComponent<Image>()
            : null;
        if (sliderBackground != null)
        {
            ConfigureCenteredRect(
                sliderBackground.rectTransform,
                new Vector2(-13f, 0f),
                new Vector2(390f, 34f));
            ConfigureSpriteImage(
                sliderBackground,
                skin.sliderBackground,
                Image.Type.Simple,
                true);
        }

        Transform fillAreaTransform = sliderObject.transform.Find("Fill Area");
        RectTransform fillArea = fillAreaTransform as RectTransform;
        if (fillArea != null)
        {
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.pivot = new Vector2(0.5f, 0.5f);
            fillArea.anchoredPosition = new Vector2(-13f, 0f);
            fillArea.sizeDelta = new Vector2(-80f, 23f);
        }

        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            ConfigureSpriteImage(fill, skin.sliderFill, Image.Type.Simple, true);
            fill.preserveAspect = false;
        }

        Transform handleAreaTransform = sliderObject.transform.Find("Handle Slide Area");
        RectTransform handleArea = handleAreaTransform as RectTransform;
        if (handleArea != null)
        {
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = new Vector2(27f, 0f);
            handleArea.offsetMax = new Vector2(-53f, 0f);
        }

        Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        if (handle != null)
        {
            ConfigureSpriteImage(handle, skin.sliderHandle, Image.Type.Simple, true);
            handle.preserveAspect = true;
            slider.handleRect.sizeDelta = new Vector2(54f, 54f);
        }

        valueText = CreateText(
            objectName + "Value", parent, font, "100%", 24,
            Color.white,
            new Vector2(0f, y), new Vector2(100f, 54f), TextAnchor.MiddleCenter);
        valueText.fontStyle = FontStyle.Bold;
        return slider;
    }

    private static Dropdown CreateDropdownRow(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        SettingsUiSkin skin,
        Sprite iconSprite,
        string objectName,
        string label,
        float y)
    {
        CreateDecorativeImage(
            objectName + "Icon",
            parent,
            iconSprite,
            new Vector2(105f, y),
            new Vector2(64f, 64f));
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(280f, y), new Vector2(230f, 54f), TextAnchor.MiddleLeft);

        GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
        dropdownObject.name = objectName + "Dropdown";
        dropdownObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            dropdownObject.GetComponent<RectTransform>(),
            new Vector2(680f, y),
            new Vector2(400f, 76f));

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.captionText.font = font;
        dropdown.captionText.fontSize = 24;
        dropdown.captionText.fontStyle = FontStyle.Bold;
        dropdown.captionText.color = new Color32(62, 46, 83, 255);
        dropdown.itemText.font = font;
        dropdown.itemText.fontSize = 22;
        dropdown.itemText.color = new Color32(62, 46, 83, 255);

        Image dropdownImage = dropdown.targetGraphic as Image;
        if (dropdownImage != null)
        {
            ConfigureSpriteImage(dropdownImage, skin.grayButton, Image.Type.Sliced, true);
        }

        Transform arrowTransform = dropdownObject.transform.Find("Arrow");
        Image arrow = arrowTransform != null ? arrowTransform.GetComponent<Image>() : null;
        if (arrow != null)
        {
            ConfigureSpriteImage(arrow, skin.arrowDown, Image.Type.Simple, false);
            arrow.preserveAspect = true;
            ConfigureCenteredRect(
                arrow.rectTransform,
                new Vector2(160f, 0f),
                new Vector2(36f, 36f));
        }

        ConfigureDropdownTemplate(dropdown, skin);
        return dropdown;
    }

    private static Toggle CreateToggleRow(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        SettingsUiSkin skin,
        Sprite iconSprite,
        string objectName,
        string label,
        float y)
    {
        CreateDecorativeImage(
            objectName + "Icon",
            parent,
            iconSprite,
            new Vector2(105f, y),
            new Vector2(64f, 64f));
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(280f, y), new Vector2(230f, 54f), TextAnchor.MiddleLeft);

        GameObject toggleObject = DefaultControls.CreateToggle(resources);
        toggleObject.name = objectName + "Toggle";
        toggleObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            toggleObject.GetComponent<RectTransform>(),
            new Vector2(680f, y),
            new Vector2(179f, 83f));

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.isOn = true;

        Transform offTransform = toggleObject.transform.Find("Background");
        Image offImage = offTransform != null ? offTransform.GetComponent<Image>() : null;
        if (offImage != null)
        {
            StretchToParent(offImage.rectTransform);
            ConfigureSpriteImage(offImage, skin.switchOff, Image.Type.Simple, true);
            offImage.preserveAspect = true;
            toggle.targetGraphic = offImage;
        }

        Transform onTransform = toggleObject.transform.Find("Background/Checkmark");
        Image onImage = onTransform != null ? onTransform.GetComponent<Image>() : null;
        if (onImage != null)
        {
            StretchToParent(onImage.rectTransform);
            ConfigureSpriteImage(onImage, skin.switchOn, Image.Type.Simple, false);
            onImage.preserveAspect = true;
            toggle.graphic = onImage;
        }

        Text toggleText = toggleObject.GetComponentInChildren<Text>(true);
        if (toggleText != null)
        {
            toggleText.text = string.Empty;
            toggleText.gameObject.SetActive(false);
        }

        return toggle;
    }

    private static Button CreateButton(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        Sprite buttonSprite,
        string objectName,
        string label,
        Vector2 position,
        Vector2 size,
        Color textColor)
    {
        GameObject buttonObject = DefaultControls.CreateButton(resources);
        buttonObject.name = objectName;
        buttonObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        ConfigureSpriteImage(image, buttonSprite, Image.Type.Sliced, true);

        Text text = buttonObject.GetComponentInChildren<Text>(true);
        text.font = font;
        text.fontSize = 27;
        text.fontStyle = FontStyle.Bold;
        text.color = textColor;
        text.text = label;
        return button;
    }

    private static GameObject CreateConfirmationPanel(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        SettingsUiSkin skin,
        out Text confirmationText,
        out Button keepButton,
        out Button revertButton)
    {
        Image overlay = CreateImage(
            "DisplayConfirmationPanel",
            parent,
            new Color32(3, 5, 10, 225),
            true);
        StretchToParent(overlay.rectTransform);

        Image dialog = CreateImage(
            "ConfirmationWindow",
            overlay.transform,
            Color.white,
            true);
        ConfigureCenteredRect(dialog.rectTransform, Vector2.zero, new Vector2(880f, 510f));
        ConfigureSpriteImage(dialog, skin.popup, Image.Type.Sliced, true);

        Text title = CreateText(
            "ConfirmationTitle", dialog.transform, font, "确认显示设置", 36,
            new Color32(62, 46, 83, 255),
            new Vector2(0f, 145f), new Vector2(660f, 60f), TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;

        confirmationText = CreateText(
            "ConfirmationText", dialog.transform, font, "是否保留新的显示设置？", 27,
            new Color32(62, 46, 83, 255),
            new Vector2(0f, 45f), new Vector2(700f, 100f), TextAnchor.MiddleCenter);

        keepButton = CreateButton(
            dialog.transform, resources, font, skin.greenButton, "KeepButton", "保留设置",
            new Vector2(-175f, -145f), new Vector2(300f, 82f), Color.white);
        revertButton = CreateButton(
            dialog.transform, resources, font, skin.grayButton, "RevertButton", "恢复设置",
            new Vector2(175f, -145f), new Vector2(300f, 82f),
            new Color32(62, 46, 83, 255));
        return overlay.gameObject;
    }

    /// <summary>
    /// 保留 Unity Dropdown 标准层级，只替换可见图片，避免换肤影响展开、滚动和选项选择逻辑。
    /// </summary>
    private static void ConfigureDropdownTemplate(Dropdown dropdown, SettingsUiSkin skin)
    {
        RectTransform template = dropdown.template;
        if (template == null)
        {
            return;
        }

        template.sizeDelta = new Vector2(0f, 300f);
        Image templateImage = template.GetComponent<Image>();
        if (templateImage != null)
        {
            ConfigureSpriteImage(templateImage, skin.grayButton, Image.Type.Sliced, true);
        }

        Transform itemTransform = template.Find("Viewport/Content/Item");
        if (itemTransform == null)
        {
            return;
        }

        Toggle itemToggle = itemTransform.GetComponent<Toggle>();
        Transform itemBackgroundTransform = itemTransform.Find("Item Background");
        Image itemBackground = itemBackgroundTransform != null
            ? itemBackgroundTransform.GetComponent<Image>()
            : null;
        if (itemBackground != null)
        {
            ConfigureSpriteImage(itemBackground, skin.whiteButton, Image.Type.Sliced, true);
            itemBackground.color = new Color32(255, 255, 255, 230);
            if (itemToggle != null)
            {
                itemToggle.targetGraphic = itemBackground;
            }
        }
    }

    private static Button CreateIconButton(
        Transform parent,
        Sprite sprite,
        string objectName,
        Vector2 position,
        Vector2 size)
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Image image = buttonObject.GetComponent<Image>();
        ConfigureSpriteImage(image, sprite, Image.Type.Simple, true);
        image.preserveAspect = true;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static Image CreateDecorativeImage(
        string name,
        Transform parent,
        Sprite sprite,
        Vector2 position,
        Vector2 size)
    {
        Image image = CreateImage(name, parent, Color.white, false);
        ConfigureCenteredRect(image.rectTransform, position, size);
        ConfigureSpriteImage(image, sprite, Image.Type.Simple, false);
        image.preserveAspect = true;
        return image;
    }

    private static Image CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject imageObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    private static Text CreateText(
        string name,
        Transform parent,
        Font font,
        string content,
        int fontSize,
        Color color,
        Vector2 position,
        Vector2 size,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(textObject.GetComponent<RectTransform>(), position, size);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void ConfigureSpriteImage(
        Image image,
        Sprite sprite,
        Image.Type imageType,
        bool raycastTarget)
    {
        image.sprite = sprite;
        image.type = imageType;
        image.color = Color.white;
        image.raycastTarget = raycastTarget;
    }

    private static Sprite LoadPurchasedSprite(string relativePathWithoutExtension)
    {
        string assetPath = PurchasedUiSpriteRoot + relativePathWithoutExtension + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite == null)
        {
            throw new MissingReferenceException($"找不到淘宝 UI Sprite：{assetPath}");
        }

        return sprite;
    }

    private static SettingsUiSkin LoadSettingsUiSkin()
    {
        return new SettingsUiSkin
        {
            background = LoadPurchasedSprite("Home/UI_Home_Setting_Background"),
            grayButton = LoadPurchasedSprite("Home/UI_Home_Setting_Buttons_ButtonGray_Btn_Normal"),
            whiteButton = LoadPurchasedSprite("Home/UI_Home_Setting_ButtonWhite_Btn_Normal"),
            sliderBackground = LoadPurchasedSprite(
                "Home/UI_Home_Setting_ControlBar_Control_Prg_Bg_Background"),
            sliderFill = LoadPurchasedSprite(
                "Home/UI_Home_Setting_ControlBar_Control_Prg_Bar_Fill"),
            sliderHandle = LoadPurchasedSprite(
                "Home/UI_Home_Setting_Control_ControlBar_Control_Pointer_Handle"),
            switchOff = LoadPurchasedSprite("Home/UI_Home_Setting_Control_SwitchOff_Handle"),
            switchOn = LoadPurchasedSprite("Home/UI_Home_Setting_Control_SwitchOn_Handle"),
            divider = LoadPurchasedSprite("Home/UI_Home_Setting_Line"),
            back = LoadPurchasedSprite("Home/UI_Home_Setting_Top_Back"),
            popup = LoadPurchasedSprite("Popups/UI_Popups_PopupChecking_Popup"),
            greenButton = LoadPurchasedSprite("Common/UI_Common_Button_Rect_Green_Normal"),
            arrowDown = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_ArrowDown"),
            volumeUpIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_VolumeUp"),
            musicIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Music"),
            soundIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Sound"),
            setting2Icon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Setting2"),
            displayIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Display"),
            setting1Icon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Setting1"),
            gameIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Game"),
            refreshIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Refresh"),
            timerIcon = LoadPurchasedSprite("FunctionIcons/UI_FunctionIcon_Timer")
        };
    }

    /// <summary>
    /// 设置页换肤资源集合：生成阶段集中加载，运行时控制器不感知具体美术资源。
    /// </summary>
    private sealed class SettingsUiSkin
    {
        public Sprite background;
        public Sprite grayButton;
        public Sprite whiteButton;
        public Sprite sliderBackground;
        public Sprite sliderFill;
        public Sprite sliderHandle;
        public Sprite switchOff;
        public Sprite switchOn;
        public Sprite divider;
        public Sprite back;
        public Sprite popup;
        public Sprite greenButton;
        public Sprite arrowDown;
        public Sprite volumeUpIcon;
        public Sprite musicIcon;
        public Sprite soundIcon;
        public Sprite setting2Icon;
        public Sprite displayIcon;
        public Sprite setting1Icon;
        public Sprite gameIcon;
        public Sprite refreshIcon;
        public Sprite timerIcon;
    }

    private static void ConfigureCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void SetReference(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property == null)
        {
            throw new MissingReferenceException($"找不到设置面板字段：{propertyName}");
        }

        property.objectReferenceValue = value;
    }

    private static void SetIntegerArray(SerializedProperty property, int[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }

    private static void RemoveOldSettingsPanelListeners(Button button)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            Object target = button.onClick.GetPersistentTarget(i);
            if (target == null || target is GameSettingsPanelController)
            {
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (layer < 0)
        {
            layer = 5;
        }

        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
        {
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
        }
    }
}
#endif
