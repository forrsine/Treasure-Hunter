#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LoadingScene 生成工具：创建轻量加载界面、绑定序列化引用并加入 Build Settings。
/// 菜单刷新会覆盖现有 LoadingScene，因此交互模式下必须经过二次确认。
/// </summary>
public static class LoadingSceneSetupTool
{
    public const string ScenePath = "Assets/Scenes/LoadingScene.unity";

    [MenuItem("Treasure Hunter/Scene/Create Or Refresh Loading Scene")]
    public static void CreateOrRefreshFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        SceneAsset existingScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        if (existingScene != null &&
            !EditorUtility.DisplayDialog(
                "刷新 LoadingScene",
                "此操作会重新生成 LoadingScene，并覆盖该场景中的手动布局修改。是否继续？",
                "继续刷新",
                "取消"))
        {
            return;
        }

        CreateOrRefresh();
    }

    /// <summary>
    /// 命令行入口用于自动生成和验证资源，不显示编辑器确认框。
    /// </summary>
    public static void CreateOrRefreshFromCommandLine()
    {
        CreateOrRefresh();
        Debug.Log("LOADING_SCENE_SETUP_SUCCEEDED");
    }

    private static void CreateOrRefresh()
    {
        SceneSetup[] previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
        bool shouldRestoreSceneSetup = !Application.isBatchMode && previousSceneSetup.Length > 0;

        try
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildLoadingUi();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Loading 场景已创建并加入 Build Settings：{ScenePath}");
        }
        finally
        {
            if (shouldRestoreSceneSetup)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }
    }

    private static void BuildLoadingUi()
    {
        Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        GameObject canvasObject = new GameObject(
            "LoadingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(LoadingSceneController));

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        Image background = CreateImage(
            "Background",
            canvasObject.transform,
            new Color32(11, 16, 32, 255));
        StretchToParent(background.rectTransform);

        Text loadingText = CreateText(
            "LoadingText",
            canvasObject.transform,
            font,
            32,
            new Color32(243, 232, 200, 255),
            new Vector2(0f, 90f),
            new Vector2(900f, 60f));
        loadingText.text = "正在加载，请稍候……";
        loadingText.fontStyle = FontStyle.Bold;

        Slider progressSlider = CreateProgressSlider(canvasObject.transform);

        Text progressText = CreateText(
            "ProgressText",
            canvasObject.transform,
            font,
            24,
            Color.white,
            new Vector2(0f, -58f),
            new Vector2(300f, 42f));
        progressText.text = "0%";

        LoadingSceneController controller = canvasObject.GetComponent<LoadingSceneController>();
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("progressSlider").objectReferenceValue = progressSlider;
        serializedController.FindProperty("progressText").objectReferenceValue = progressText;
        serializedController.FindProperty("loadingText").objectReferenceValue = loadingText;
        serializedController.FindProperty("progressSmoothSpeed").floatValue = 1.5f;
        serializedController.FindProperty("minimumVisibleDuration").floatValue = 0.8f;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Slider CreateProgressSlider(Transform parent)
    {
        GameObject sliderObject = new GameObject(
            "ProgressSlider",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Slider));
        sliderObject.transform.SetParent(parent, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        ConfigureCenteredRect(sliderRect, Vector2.zero, new Vector2(760f, 30f));

        Image backgroundImage = sliderObject.GetComponent<Image>();
        backgroundImage.color = new Color32(255, 255, 255, 51);
        backgroundImage.raycastTarget = false;

        GameObject fillAreaObject = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaObject.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillAreaObject.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(5f, 5f);
        fillAreaRect.offsetMax = new Vector2(-5f, -5f);

        Image fillImage = CreateImage(
            "Fill",
            fillAreaObject.transform,
            new Color32(217, 164, 65, 255));
        StretchToParent(fillImage.rectTransform);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillImage.rectTransform;
        slider.targetGraphic = backgroundImage;

        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
        return slider;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text CreateText(
        string objectName,
        Transform parent,
        Font font,
        int fontSize,
        Color color,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        GameObject textObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        ConfigureCenteredRect(rectTransform, anchoredPosition, size);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.supportRichText = false;
        return text;
    }

    private static void ConfigureCenteredRect(
        RectTransform rectTransform,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void AddSceneToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        scenes.RemoveAll(scene => scene.path == ScenePath);

        int characterSelectIndex = scenes.FindIndex(
            scene => scene.path == "Assets/Scenes/CharacterSelectScene.unity");
        int insertionIndex = characterSelectIndex >= 0
            ? characterSelectIndex + 1
            : scenes.Count;

        scenes.Insert(insertionIndex, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
#endif
