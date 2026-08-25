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
    private const string RpgUiSpritePath = "Assets/AllResources/2D Casual UI/Sprite/GUI.png";

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
        Sprite panelSprite = FindSprite("GUI_46");
        Sprite buttonSprite = FindSprite("GUI_19");

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
            Image overlay = root.GetComponent<Image>();
            overlay.color = new Color32(5, 8, 16, 205);
            overlay.raycastTarget = true;

            Image window = CreateImage(
                "SettingsWindow",
                root.transform,
                new Color32(39, 31, 43, 250),
                true);
            ConfigureCenteredRect(window.rectTransform, Vector2.zero, new Vector2(980f, 940f));
            if (panelSprite != null)
            {
                window.sprite = panelSprite;
                window.type = Image.Type.Sliced;
            }

            Text title = CreateText(
                "Title",
                window.transform,
                font,
                "游戏设置",
                38,
                new Color32(255, 216, 105, 255),
                new Vector2(0f, 405f),
                new Vector2(600f, 60f),
                TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;

            Slider masterSlider = CreateSliderRow(
                window.transform, uiResources, font, "MasterVolume", "主音量", 315f, 0f, 1f,
                out Text masterValue);
            Slider musicSlider = CreateSliderRow(
                window.transform, uiResources, font, "MusicVolume", "音乐音量", 245f, 0f, 1f,
                out Text musicValue);
            Slider soundsSlider = CreateSliderRow(
                window.transform, uiResources, font, "SoundEffectsVolume", "音效音量", 175f, 0f, 1f,
                out Text soundsValue);
            Slider sensitivitySlider = CreateSliderRow(
                window.transform, uiResources, font, "MouseSensitivity", "鼠标灵敏度", 105f, 0.5f, 2f,
                out Text sensitivityValue);

            Dropdown resolutionDropdown = CreateDropdownRow(
                window.transform, uiResources, font, "Resolution", "分辨率", 35f);
            Dropdown displayModeDropdown = CreateDropdownRow(
                window.transform, uiResources, font, "DisplayMode", "显示模式", -35f);
            Dropdown qualityDropdown = CreateDropdownRow(
                window.transform, uiResources, font, "Quality", "画质", -105f);
            Toggle vSyncToggle = CreateToggleRow(
                window.transform, uiResources, font, "VerticalSync", "垂直同步", -175f);
            Dropdown frameRateDropdown = CreateDropdownRow(
                window.transform, uiResources, font, "FrameRateLimit", "帧率上限", -245f);

            Button defaultsButton = CreateButton(
                window.transform, uiResources, font, buttonSprite, "RestoreDefaultsButton", "恢复默认",
                new Vector2(-280f, -385f), new Vector2(220f, 58f), new Color32(84, 72, 62, 255));
            Button cancelButton = CreateButton(
                window.transform, uiResources, font, buttonSprite, "CancelButton", "取消",
                new Vector2(0f, -385f), new Vector2(220f, 58f), new Color32(101, 66, 68, 255));
            Button applyButton = CreateButton(
                window.transform, uiResources, font, buttonSprite, "ApplyButton", "应用",
                new Vector2(280f, -385f), new Vector2(220f, 58f), new Color32(158, 111, 42, 255));

            GameObject confirmationPanel = CreateConfirmationPanel(
                root.transform,
                uiResources,
                font,
                panelSprite,
                buttonSprite,
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
        string objectName,
        string label,
        float y,
        float minimum,
        float maximum,
        out Text valueText)
    {
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(-350f, y), new Vector2(220f, 50f), TextAnchor.MiddleLeft);

        GameObject sliderObject = DefaultControls.CreateSlider(resources);
        sliderObject.name = objectName + "Slider";
        sliderObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            sliderObject.GetComponent<RectTransform>(),
            new Vector2(60f, y),
            new Vector2(500f, 34f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.value = maximum;
        slider.wholeNumbers = false;

        Image fill = slider.fillRect != null ? slider.fillRect.GetComponent<Image>() : null;
        if (fill != null)
        {
            fill.color = new Color32(224, 166, 60, 255);
        }

        Image handle = slider.handleRect != null ? slider.handleRect.GetComponent<Image>() : null;
        if (handle != null)
        {
            handle.color = new Color32(255, 222, 126, 255);
        }

        valueText = CreateText(
            objectName + "Value", parent, font, "100%", 24,
            new Color32(255, 221, 130, 255),
            new Vector2(385f, y), new Vector2(110f, 50f), TextAnchor.MiddleCenter);
        return slider;
    }

    private static Dropdown CreateDropdownRow(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        string objectName,
        string label,
        float y)
    {
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(-350f, y), new Vector2(220f, 50f), TextAnchor.MiddleLeft);

        GameObject dropdownObject = DefaultControls.CreateDropdown(resources);
        dropdownObject.name = objectName + "Dropdown";
        dropdownObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            dropdownObject.GetComponent<RectTransform>(),
            new Vector2(135f, y),
            new Vector2(500f, 48f));

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.captionText.font = font;
        dropdown.captionText.fontSize = 23;
        dropdown.captionText.color = Color.white;
        dropdown.itemText.font = font;
        dropdown.itemText.fontSize = 22;
        dropdown.targetGraphic.color = new Color32(66, 56, 69, 255);
        return dropdown;
    }

    private static Toggle CreateToggleRow(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        string objectName,
        string label,
        float y)
    {
        CreateText(
            objectName + "Label", parent, font, label, 25, Color.white,
            new Vector2(-350f, y), new Vector2(220f, 50f), TextAnchor.MiddleLeft);

        GameObject toggleObject = DefaultControls.CreateToggle(resources);
        toggleObject.name = objectName + "Toggle";
        toggleObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(
            toggleObject.GetComponent<RectTransform>(),
            new Vector2(-5f, y),
            new Vector2(220f, 48f));

        Toggle toggle = toggleObject.GetComponent<Toggle>();
        toggle.isOn = true;
        Text toggleText = toggleObject.GetComponentInChildren<Text>(true);
        if (toggleText != null)
        {
            toggleText.font = font;
            toggleText.fontSize = 23;
            toggleText.color = Color.white;
            toggleText.text = "开启";
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
        Color color)
    {
        GameObject buttonObject = DefaultControls.CreateButton(resources);
        buttonObject.name = objectName;
        buttonObject.transform.SetParent(parent, false);
        ConfigureCenteredRect(buttonObject.GetComponent<RectTransform>(), position, size);

        Button button = buttonObject.GetComponent<Button>();
        Image image = buttonObject.GetComponent<Image>();
        image.color = color;
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
        }

        Text text = buttonObject.GetComponentInChildren<Text>(true);
        text.font = font;
        text.fontSize = 25;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.text = label;
        return button;
    }

    private static GameObject CreateConfirmationPanel(
        Transform parent,
        DefaultControls.Resources resources,
        Font font,
        Sprite panelSprite,
        Sprite buttonSprite,
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
            new Color32(45, 36, 48, 255),
            true);
        ConfigureCenteredRect(dialog.rectTransform, Vector2.zero, new Vector2(680f, 330f));
        if (panelSprite != null)
        {
            dialog.sprite = panelSprite;
            dialog.type = Image.Type.Sliced;
        }

        Text title = CreateText(
            "ConfirmationTitle", dialog.transform, font, "确认显示设置", 31,
            new Color32(255, 216, 105, 255),
            new Vector2(0f, 105f), new Vector2(560f, 55f), TextAnchor.MiddleCenter);
        title.fontStyle = FontStyle.Bold;

        confirmationText = CreateText(
            "ConfirmationText", dialog.transform, font, "是否保留新的显示设置？", 25,
            Color.white, new Vector2(0f, 25f), new Vector2(580f, 90f), TextAnchor.MiddleCenter);

        keepButton = CreateButton(
            dialog.transform, resources, font, buttonSprite, "KeepButton", "保留设置",
            new Vector2(-150f, -105f), new Vector2(220f, 58f), new Color32(158, 111, 42, 255));
        revertButton = CreateButton(
            dialog.transform, resources, font, buttonSprite, "RevertButton", "恢复设置",
            new Vector2(150f, -105f), new Vector2(220f, 58f), new Color32(101, 66, 68, 255));
        return overlay.gameObject;
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

    private static Sprite FindSprite(string spriteName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(RpgUiSpritePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == spriteName);
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
