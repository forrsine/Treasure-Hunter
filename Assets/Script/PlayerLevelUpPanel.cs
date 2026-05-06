using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 玩家升级三选一面板。
/// 
/// 新手阅读顺序：
/// 1. PlayerCo 升级后会增加 pendingUpgradeSelectionCount，并触发 PendingUpgradeSelectionsChanged 事件。
/// 2. 本脚本收到事件后暂停游戏，向 PlayerCo 要 3 个随机升级选项。
/// 3. 玩家点击按钮后，OnOptionSelected 通知 PlayerCo 真正应用升级。
/// 4. 如果还有待选升级，就继续显示下一轮；没有就隐藏面板并恢复游戏。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerLevelUpPanel : MonoBehaviour
{
    // 每次升级面板最多显示 3 个选项。
    private const int ChoiceCount = 3;

    // UI 会挂到这个 Canvas 下。为空时会自动找/创建运行时 Canvas。
    [SerializeField] private Canvas targetCanvas;

    // 面板尺寸、颜色、预览开关，主要给 Inspector 调整外观用。
    [SerializeField] private Vector2 panelSize = new Vector2(760f, 360f);
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color panelColor = new Color(0.1f, 0.12f, 0.16f, 0.96f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.2f, 0.28f, 1f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color detailColor = new Color(0.9f, 0.92f, 0.96f, 1f);
    [SerializeField] private bool editorPreviewVisible = true;
    [SerializeField] private int editorPreviewPendingCount = 2;
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text queueText;
    [SerializeField] private Button[] optionButtons = new Button[ChoiceCount];
    [SerializeField] private Text[] optionTexts = new Text[ChoiceCount];

    // 玩家组件和字体缓存。
    private PlayerCo player;
    private Font font;
    private bool isVisible;

    // 打开升级面板时要暂停游戏，所以这里缓存打开前的时间和鼠标状态，关闭时恢复。
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;

    /// <summary>
    /// 缓存玩家组件和字体资源。
    /// </summary>
    private void Awake()
    {
        // 升级面板和 PlayerCo 挂在同一个玩家物体上，所以直接 GetComponent。
        player = GetComponent<PlayerCo>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    /// <summary>
    /// 启用时订阅玩家待选升级数量变化，或生成编辑器预览。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            if (player == null)
            {
                player = GetComponent<PlayerCo>();
            }

            if (player != null)
            {
                // 订阅“待选择升级数量变化”事件。
                // 事件触发时，本脚本就知道该显示还是隐藏。
                player.PendingUpgradeSelectionsChanged += HandlePendingUpgradeSelectionsChanged;
            }
        }
        else
        {
            EnsureEditorUi();
        }
    }

    /// <summary>
    /// 禁用时取消订阅并关闭面板，避免暂停状态残留。
    /// </summary>
    private void OnDisable()
    {
        if (Application.isPlaying && player != null)
        {
            player.PendingUpgradeSelectionsChanged -= HandlePendingUpgradeSelectionsChanged;
            HidePanel();
        }
    }

    /// <summary>
    /// Inspector 参数变化时刷新编辑器预览。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorUi();
        }
    }

    /// <summary>
    /// 运行开始时创建升级面板 UI，并先保持隐藏。
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorUi();
            return;
        }

        EnsureCanvas();
        EnsureEventSystem();
        BuildPanelIfNeeded();
        SetVisible(false);
    }

    /// <summary>
    /// 根据玩家还有多少次未选择升级，决定显示下一轮还是隐藏面板。
    /// </summary>
    private void HandlePendingUpgradeSelectionsChanged(int pendingCount)
    {
        // pendingCount > 0 说明还有升级没选，打开下一次三选一。
        if (pendingCount > 0)
        {
            ShowNextSelection();
            return;
        }

        HidePanel();
    }

    /// <summary>
    /// 编辑器路径：创建面板并填入示例选项。
    /// </summary>
    private void EnsureEditorUi()
    {
        player = GetComponent<PlayerCo>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureCanvas();
        BuildPanelIfNeeded();
        ApplyEditorPreview();
    }

    /// <summary>
    /// 查找或创建升级面板要挂载的 Canvas。
    /// </summary>
    private void EnsureCanvas()
    {
        if (targetCanvas == null)
        {
            targetCanvas = RuntimeUiCanvasProvider.FindBestCanvas();
        }

        if (targetCanvas == null && Application.isPlaying)
        {
            targetCanvas = RuntimeUiCanvasProvider.GetOrCreateCanvas();
        }
    }

    /// <summary>
    /// 确保场景里有 EventSystem，按钮才能响应点击。
    /// </summary>
    private void EnsureEventSystem()
    {
        // Button 点击需要 EventSystem。场景里没有时自动创建一个。
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>
    /// 创建或复用升级面板遮罩、容器、标题和三个选项按钮。
    /// </summary>
    private void BuildPanelIfNeeded()
    {
        if (targetCanvas == null)
        {
            return;
        }

        // 如果场景里已经有同名 UI，就复用；否则运行时自动创建。
        ResolveExistingReferences();

        if (overlayRoot == null)
        {
            // overlayRoot 是全屏半透明遮罩，盖住游戏画面并承载面板。
            overlayRoot = new GameObject("PlayerLevelUpOverlay", typeof(RectTransform), typeof(Image));
            overlayRoot.transform.SetParent(targetCanvas.transform, false);
            overlayRoot.transform.SetAsLastSibling();

            RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }

        Image overlayImage = overlayRoot.GetComponent<Image>();
        overlayImage.color = overlayColor;

        if (panelRoot == null)
        {
            // panelRoot 是真正的升级面板容器。
            panelRoot = new GameObject("LevelUpPanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(overlayRoot.transform, false);
        }

        RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = panelSize;
        panelRect.anchoredPosition = Vector2.zero;

        Image panelImage = panelRoot.GetComponent<Image>();
        panelImage.color = panelColor;

        if (titleText == null)
        {
            titleText = CreateText("Title", panelRoot.transform, 30, titleColor, TextAnchor.UpperCenter);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(24f, -56f);
            titleRect.offsetMax = new Vector2(-24f, -16f);
        }

        if (subtitleText == null)
        {
            subtitleText = CreateText("Subtitle", panelRoot.transform, 18, detailColor, TextAnchor.UpperCenter);
            RectTransform subtitleRect = subtitleText.rectTransform;
            subtitleRect.anchorMin = new Vector2(0f, 1f);
            subtitleRect.anchorMax = new Vector2(1f, 1f);
            subtitleRect.offsetMin = new Vector2(24f, -92f);
            subtitleRect.offsetMax = new Vector2(-24f, -56f);
        }

        if (queueText == null)
        {
            queueText = CreateText("QueueText", panelRoot.transform, 16, detailColor, TextAnchor.UpperCenter);
            RectTransform queueRect = queueText.rectTransform;
            queueRect.anchorMin = new Vector2(0f, 1f);
            queueRect.anchorMax = new Vector2(1f, 1f);
            queueRect.offsetMin = new Vector2(24f, -120f);
            queueRect.offsetMax = new Vector2(-24f, -88f);
        }

        for (int i = 0; i < ChoiceCount; i++)
        {
            // 逐个创建/查找 3 个选项按钮。
            if (optionButtons[i] == null || optionTexts[i] == null)
            {
                CreateOrResolveOptionButton(i);
            }
        }
    }

    /// <summary>
    /// 从已有层级里找回手动调整过的升级面板 UI。
    /// </summary>
    private void ResolveExistingReferences()
    {
        // 这一段是“复用已有 UI”的逻辑。
        // 如果你在层级面板里手动摆好了 PlayerLevelUpOverlay/LevelUpPanel，
        // 脚本会优先找到它们，而不是重复创建。
        if (overlayRoot == null)
        {
            Transform overlayTransform = targetCanvas.transform.Find("PlayerLevelUpOverlay");
            if (overlayTransform != null)
            {
                overlayRoot = overlayTransform.gameObject;
            }
        }

        if (overlayRoot == null)
        {
            return;
        }

        if (panelRoot == null)
        {
            Transform panelTransform = overlayRoot.transform.Find("LevelUpPanel");
            if (panelTransform != null)
            {
                panelRoot = panelTransform.gameObject;
            }
        }

        if (panelRoot == null)
        {
            return;
        }

        if (titleText == null)
        {
            Transform child = panelRoot.transform.Find("Title");
            if (child != null)
            {
                titleText = child.GetComponent<Text>();
            }
        }

        if (subtitleText == null)
        {
            Transform child = panelRoot.transform.Find("Subtitle");
            if (child != null)
            {
                subtitleText = child.GetComponent<Text>();
            }
        }

        if (queueText == null)
        {
            Transform child = panelRoot.transform.Find("QueueText");
            if (child != null)
            {
                queueText = child.GetComponent<Text>();
            }
        }

        for (int i = 0; i < ChoiceCount; i++)
        {
            if (optionButtons[i] == null)
            {
                Transform child = panelRoot.transform.Find($"OptionButton{i + 1}");
                if (child != null)
                {
                    optionButtons[i] = child.GetComponent<Button>();
                }
            }

            if (optionTexts[i] == null && optionButtons[i] != null)
            {
                optionTexts[i] = optionButtons[i].GetComponentInChildren<Text>(true);
            }
        }
    }

    /// <summary>
    /// 创建或复用指定序号的升级选项按钮。
    /// </summary>
    private void CreateOrResolveOptionButton(int index)
    {
        // index 从 0 开始，但按钮名字从 1 开始：OptionButton1、OptionButton2、OptionButton3。
        Transform existing = panelRoot.transform.Find($"OptionButton{index + 1}");
        if (existing != null)
        {
            optionButtons[index] = existing.GetComponent<Button>();
            optionTexts[index] = existing.GetComponentInChildren<Text>(true);
            return;
        }

        GameObject buttonObject = new GameObject($"OptionButton{index + 1}", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(panelRoot.transform, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        float width = 220f;
        float height = 180f;
        float spacing = 16f;

        // 三个按钮横向排列：第一个在左，中间一个居中，第三个在右。
        float startX = -width - spacing;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(width, height);
        buttonRect.anchoredPosition = new Vector2(startX + (width + spacing) * index, -22f);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = buttonColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;

        // Button.colors 控制普通、悬停、按下、选中时的颜色。
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(buttonColor.r + 0.06f, buttonColor.g + 0.06f, buttonColor.b + 0.06f, 1f);
        colors.pressedColor = new Color(buttonColor.r + 0.1f, buttonColor.g + 0.1f, buttonColor.b + 0.1f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text optionText = CreateText("Label", buttonObject.transform, 18, detailColor, TextAnchor.MiddleCenter);
        RectTransform optionRect = optionText.rectTransform;
        optionRect.anchorMin = Vector2.zero;
        optionRect.anchorMax = Vector2.one;
        optionRect.offsetMin = new Vector2(12f, 12f);
        optionRect.offsetMax = new Vector2(-12f, -12f);
        optionText.alignment = TextAnchor.MiddleCenter;
        optionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        optionText.verticalOverflow = VerticalWrapMode.Overflow;
        optionText.lineSpacing = 1.1f;

        optionButtons[index] = button;
        optionTexts[index] = optionText;
    }

    /// <summary>
    /// 创建升级面板使用的通用 Text。
    /// </summary>
    private Text CreateText(string objectName, Transform parent, int fontSize, Color color, TextAnchor alignment)
    {
        // 运行时创建 Text 的小工具，避免每次都重复写一长串 AddComponent 代码。
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        return text;
    }

    /// <summary>
    /// 编辑器里填入示例文字并按预览开关显示面板。
    /// </summary>
    private void ApplyEditorPreview()
    {
        // ExecuteAlways 让这个脚本在编辑器非运行状态也能生成预览 UI。
        // 这样你不用按 Play，也能看到面板大概长什么样。
        if (overlayRoot == null)
        {
            return;
        }

        overlayRoot.SetActive(editorPreviewVisible);
        if (!editorPreviewVisible)
        {
            return;
        }

        if (titleText != null)
        {
            titleText.text = "升级！";
        }

        if (subtitleText != null)
        {
            subtitleText.text = "选择 1 项强化";
        }

        if (queueText != null)
        {
            queueText.text = $"待选择：{editorPreviewPendingCount}";
        }

        for (int i = 0; i < ChoiceCount; i++)
        {
            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(true);
                optionButtons[i].onClick.RemoveAllListeners();
            }

            if (optionTexts[i] != null)
            {
                optionTexts[i].text = $"示例强化 {i + 1}\n这里可以直接调位置、尺寸、图片和按钮样式";
            }
        }
    }

    /// <summary>
    /// 从玩家处抽取三项升级候选，填入按钮并暂停游戏。
    /// </summary>
    private void ShowNextSelection()
    {
        if (player == null)
        {
            return;
        }

        // 打开面板前确保基础 UI 环境都存在。
        EnsureCanvas();
        EnsureEventSystem();
        BuildPanelIfNeeded();

        // 从 PlayerCo 获取本轮随机升级选项。
        var choices = player.GetRandomUpgradeChoices(ChoiceCount);
        if (choices.Count == 0)
        {
            HidePanel();
            return;
        }

        CaptureGameplayStateIfNeeded();
        player.SetUpgradeSelectionState(true);
        queueText.text = $"待选择：{player.PendingUpgradeSelectionCount}";

        for (int i = 0; i < ChoiceCount; i++)
        {
            bool hasChoice = i < choices.Count;
            optionButtons[i].gameObject.SetActive(hasChoice);

            if (!hasChoice)
            {
                continue;
            }

            PlayerAttributeType selectedType = choices[i];
            optionTexts[i].text = player.GetUpgradeOptionText(selectedType);
            optionButtons[i].onClick.RemoveAllListeners();

            // 注意这里用 selectedType 局部变量，而不是直接用 i。
            // 这样每个按钮点击时都会记住自己对应的属性。
            optionButtons[i].onClick.AddListener(() => OnOptionSelected(selectedType));
        }

        SetVisible(true);
    }

    /// <summary>
    /// 玩家点击某个升级按钮后，把选择交给 PlayerCo 应用。
    /// </summary>
    private void OnOptionSelected(PlayerAttributeType attributeType)
    {
        if (player == null)
        {
            return;
        }

        // 真正应用升级的逻辑在 PlayerCo。
        // 这里负责把玩家点了哪个选项传过去。
        player.ResolvePendingUpgradeSelection(attributeType);
    }

    /// <summary>
    /// 隐藏升级面板并恢复玩法输入/时间。
    /// </summary>
    private void HidePanel()
    {
        // 关闭升级选择状态，玩家移动/攻击输入才会恢复。
        if (player != null)
        {
            player.SetUpgradeSelectionState(false);
        }

        RestoreGameplayStateIfNeeded();
        SetVisible(false);
    }

    /// <summary>
    /// 控制升级遮罩根物体的显隐。
    /// </summary>
    private void SetVisible(bool visible)
    {
        isVisible = visible;

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// 打开升级面板前缓存并暂停玩法状态。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        // 打开升级面板时暂停游戏，并释放鼠标让玩家能点按钮。
        if (hasCapturedGameplayState)
        {
            Time.timeScale = 0f;

            // 升级面板打开时需要点选项，所以显示鼠标，并把鼠标放到左上角避开按钮。
            CursorPopupUtility.ShowAtTopLeft();
            return;
        }

        cachedTimeScale = Time.timeScale;
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        hasCapturedGameplayState = true;

        Time.timeScale = 0f;

        // 第一次打开升级面板时，也统一把鼠标弹到左上角。
        CursorPopupUtility.ShowAtTopLeft();
    }

    /// <summary>
    /// 关闭升级面板后恢复暂停前的玩法状态。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
        // 关闭面板时恢复打开前的暂停状态和鼠标状态。
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        hasCapturedGameplayState = false;
    }
}
