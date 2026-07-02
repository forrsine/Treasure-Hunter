using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 游戏开始时的新手说明弹窗。
/// 
/// 新手阅读顺序：
/// 1. OnEnable 里创建/查找弹窗 UI。
/// 2. 如果 showOnPlayStart 为 true，就在开局显示弹窗。
/// 3. 弹窗打开时暂停游戏、显示鼠标；关闭时恢复游戏状态。
/// 4. useTextOverrides 为 true 时，Inspector 里填写的标题/正文会覆盖默认文本。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class GameplayStartupGuidePopup : MonoBehaviour
{
    // 自动创建 UI 时用到的对象名字。统一成常量可以避免拼写错误。
    private const string PopupRootName = "StartupGuidePopup";
    private const string PanelName = "Panel";
    private const string TitleName = "Title";
    private const string BodyName = "Body";
    private const string CloseButtonName = "CloseButton";

    public static bool IsRuntimePopupVisible { get; private set; }

    // 目标 Canvas 和弹窗行为设置。
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool editorPreviewVisible = true;
    [SerializeField] private bool showOnPlayStart = true;
    [SerializeField] private float startupDelaySeconds = 0f;
    [SerializeField] private bool lockCursorWhenClosed = true;
    [SerializeField] private bool useTextOverrides = false;
    [SerializeField] private string popupTitle = "";
    [SerializeField] [TextArea(8, 14)] private string popupBody = "";
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image backdropImage;
    [SerializeField] private Image panelImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text closeButtonText;

    // 运行时字体和开局延迟协程缓存。
    private Font font;
    private Coroutine startupCoroutine;

    // 弹窗打开时会改 Time.timeScale 和鼠标状态，所以需要缓存打开前的状态。
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;

    /// <summary>
    /// 缓存字体，并在编辑器下生成可视化预览。
    /// </summary>
    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // ExecuteAlways 下，编辑器非运行状态也可以生成预览 UI。
        if (!Application.isPlaying)
        {
            EnsureEditorUi();
        }
    }

    /// <summary>
    /// 启用时创建弹窗 UI，并按配置决定是否开局弹出。
    /// </summary>
    private void OnEnable()
    {
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        IsRuntimePopupVisible = false;

        if (!Application.isPlaying)
        {
            EnsureEditorUi();
            return;
        }

        EnsureCanvas();
        EnsureUi();
        HidePopupImmediate();

        if (showOnPlayStart)
        {
            // 开局延迟显示，避免和场景初始化抢同一帧。
            startupCoroutine = StartCoroutine(ShowPopupAtStartup());
        }
    }

    /// <summary>
    /// 禁用时停止开局协程并恢复可能被弹窗暂停的游戏状态。
    /// </summary>
    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (startupCoroutine != null)
        {
            StopCoroutine(startupCoroutine);
            startupCoroutine = null;
        }

        RestoreGameplayStateIfNeeded();
        IsRuntimePopupVisible = false;
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
    /// 弹窗可见期间持续确保鼠标可见，防止其他脚本重新锁定。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying || !IsRuntimePopupVisible)
        {
            return;
        }

        // 弹窗可见期间持续释放鼠标，防止其他脚本又把鼠标锁回去。
        if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
        {
            // 如果鼠标被别的脚本重新锁住，就再次显示，并移动到左上角。
            CursorPopupUtility.ShowAtTopLeft();
        }
    }

    /// <summary>
    /// 打开弹窗。
    /// 可以由开局协程调用，也可以由按钮或调试脚本手动调用。
    /// </summary>
    public void ShowPopup()
    {
        EnsureCanvas();
        EnsureUi();
        ApplyContent();

        if (popupRoot == null)
        {
            return;
        }

        // 显示 UI 前暂停游戏并释放鼠标。
        CaptureGameplayStateIfNeeded();
        popupRoot.SetActive(true);
        popupRoot.transform.SetAsLastSibling();
        IsRuntimePopupVisible = true;
    }

    /// <summary>
    /// 关闭弹窗并恢复游戏。
    /// </summary>
    public void ClosePopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }

        IsRuntimePopupVisible = false;
        RestoreGameplayStateIfNeeded();
    }

    /// <summary>
    /// 按配置延迟若干真实秒后显示开局说明。
    /// </summary>
    private IEnumerator ShowPopupAtStartup()
    {
        // WaitForSecondsRealtime 不受 Time.timeScale 影响，适合 UI 延迟。
        if (startupDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(startupDelaySeconds);
        }

        ShowPopup();
        startupCoroutine = null;
    }

    /// <summary>
    /// 编辑器路径：保证弹窗层级存在并应用预览状态。
    /// </summary>
    private void EnsureEditorUi()
    {
        // 编辑器预览路径：保证 UI 存在、内容已写入、场景标记为已修改。
        EnsureCanvas();
        bool changed = EnsureUi();
        ApplyEditorPreview();
#if UNITY_EDITOR
        MarkSceneDirtyIfNeeded(changed);
#endif
    }

    /// <summary>
    /// 查找弹窗挂载的 Canvas。
    /// </summary>
    private void EnsureCanvas()
    {
        // 优先使用本物体上的 Canvas，其次找场景里已有的运行时 Canvas。
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = RuntimeUiCanvasProvider.FindBestCanvas();
        }
    }

    /// <summary>
    /// 创建或复用弹窗根节点、面板、文字和按钮。
    /// </summary>
    private bool EnsureUi()
    {
        if (targetCanvas == null)
        {
            return false;
        }

        // changed 用来告诉编辑器：这次是否真的创建/修改了层级对象。
        bool changed = false;

        if (popupRoot == null)
        {
            Transform existingRoot = targetCanvas.transform.Find(PopupRootName);
            if (existingRoot != null)
            {
                popupRoot = existingRoot.gameObject;
            }
        }

        if (popupRoot == null)
        {
            // popupRoot 是全屏半透明背景。
            popupRoot = new GameObject(PopupRootName, typeof(RectTransform), typeof(Image));
            popupRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = popupRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            backdropImage = popupRoot.GetComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.6f);
            changed = true;
        }

        if (backdropImage == null && popupRoot != null)
        {
            backdropImage = popupRoot.GetComponent<Image>();
            if (backdropImage == null)
            {
                backdropImage = popupRoot.AddComponent<Image>();
                backdropImage.color = new Color(0f, 0f, 0f, 0.6f);
                changed = true;
            }
        }

        RectTransform panelRect = EnsurePanel(ref changed);
        EnsureTexts(panelRect, ref changed);
        EnsureCloseButton(panelRect, ref changed);

        ApplyContent();
        return changed;
    }

    /// <summary>
    /// 创建或复用弹窗中间的面板容器。
    /// </summary>
    private RectTransform EnsurePanel(ref bool changed)
    {
        // Panel 是居中的深色内容框。
        if (popupRoot == null)
        {
            return null;
        }

        Transform panelTransform = popupRoot.transform.Find(PanelName);
        if (panelTransform == null)
        {
            GameObject panelObject = new GameObject(PanelName, typeof(RectTransform), typeof(Image), typeof(Outline));
            panelObject.transform.SetParent(popupRoot.transform, false);

            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, 460f);

            panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.98f);

            Outline outline = panelObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.58f, 0.84f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            changed = true;
            return rect;
        }

        RectTransform existingRect = panelTransform as RectTransform;
        if (panelImage == null)
        {
            panelImage = panelTransform.GetComponent<Image>();
        }

        if (panelImage == null)
        {
            panelImage = panelTransform.gameObject.AddComponent<Image>();
            panelImage.color = new Color(0.08f, 0.12f, 0.16f, 0.98f);
            changed = true;
        }

        if (panelTransform.GetComponent<Outline>() == null)
        {
            Outline outline = panelTransform.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.36f, 0.58f, 0.84f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            changed = true;
        }

        return existingRect;
    }

    /// <summary>
    /// 创建或复用标题和正文文本。
    /// </summary>
    private void EnsureTexts(RectTransform panelRect, ref bool changed)
    {
        // 标题和正文不存在时自动创建。
        if (panelRect == null)
        {
            return;
        }

        titleText = FindText(panelRect, TitleName);
        if (titleText == null)
        {
            titleText = CreateText(
                TitleName,
                panelRect,
                34,
                Color.white,
                TextAnchor.UpperCenter,
                new Vector2(0f, -36f),
                new Vector2(600f, 50f));
            titleText.fontStyle = FontStyle.Bold;
            changed = true;
        }

        bodyText = FindText(panelRect, BodyName);
        if (bodyText == null)
        {
            bodyText = CreateText(
                BodyName,
                panelRect,
                22,
                new Color(0.9f, 0.95f, 1f, 1f),
                TextAnchor.UpperLeft,
                new Vector2(0f, -116f),
                new Vector2(640f, 320f));
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            changed = true;
        }
    }

    /// <summary>
    /// 创建或复用关闭按钮，并绑定关闭事件。
    /// </summary>
    private void EnsureCloseButton(RectTransform panelRect, ref bool changed)
    {
        // 右上角 X 按钮，运行时绑定 ClosePopup。
        if (panelRect == null)
        {
            return;
        }

        closeButton = FindButton(panelRect, CloseButtonName);
        if (closeButton == null)
        {
            GameObject buttonObject = new GameObject(
                CloseButtonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(panelRect, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 1f);
            buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-24f, -24f);
            buttonRect.sizeDelta = new Vector2(52f, 52f);

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.78f, 0.24f, 0.24f, 0.96f);

            closeButton = buttonObject.GetComponent<Button>();
            closeButton.targetGraphic = buttonImage;
            changed = true;
        }

        closeButtonText = closeButton.GetComponentInChildren<Text>(true);
        if (closeButtonText == null)
        {
            closeButtonText = CreateText(
                "Label",
                closeButton.transform,
                26,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.zero);
            RectTransform labelRect = closeButtonText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            closeButtonText.fontStyle = FontStyle.Bold;
            changed = true;
        }

        closeButtonText.text = "X";
        closeButton.onClick.RemoveAllListeners();
        if (Application.isPlaying)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    /// <summary>
    /// 把默认或 Inspector 覆盖文案写入弹窗文本。
    /// </summary>
    private void ApplyContent()
    {
        // useTextOverrides 关闭时，不强制改现有文本，方便你在层级里手动写文案。
        if (!useTextOverrides)
        {
            return;
        }

        if (titleText != null && !string.IsNullOrWhiteSpace(popupTitle))
        {
            titleText.text = popupTitle;
        }

        if (bodyText != null && !string.IsNullOrWhiteSpace(popupBody))
        {
            bodyText.text = popupBody;
        }
    }

    /// <summary>
    /// 在编辑器里按预览开关显示/隐藏弹窗。
    /// </summary>
    private void ApplyEditorPreview()
    {
        // 编辑器里显示/隐藏预览，不影响运行时逻辑。
        ApplyContent();

        if (closeButtonText != null)
        {
            closeButtonText.text = "X";
        }

        if (popupRoot != null)
        {
            popupRoot.SetActive(editorPreviewVisible);
            popupRoot.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 立即隐藏弹窗，不修改已缓存的游戏状态。
    /// </summary>
    private void HidePopupImmediate()
    {
        // 开局先隐藏，等 ShowPopupAtStartup 到时间后再显示。
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }

        hasCapturedGameplayState = false;
        IsRuntimePopupVisible = false;
    }

    /// <summary>
    /// 打开弹窗前缓存时间和鼠标状态，并暂停游戏。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        // 只缓存一次，避免重复打开时把已经暂停的状态当成“原始状态”。
        if (hasCapturedGameplayState)
        {
            return;
        }

        cachedTimeScale = Time.timeScale;
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        // 暂停游戏并释放鼠标，玩家才能点击关闭按钮。
        Time.timeScale = 0f;

        // 新手说明弹窗打开时也会弹出鼠标；这里统一移动到左上角。
        CursorPopupUtility.ShowAtTopLeft();
        hasCapturedGameplayState = true;
    }

    /// <summary>
    /// 关闭弹窗后恢复时间和鼠标状态。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
        // 关闭弹窗时恢复打开前的 timeScale。
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        if (lockCursorWhenClosed)
        {
            // 游戏继续后通常希望鼠标重新被镜头控制。
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = cachedCursorLockMode;
            Cursor.visible = cachedCursorVisible;
        }

        hasCapturedGameplayState = false;
    }

    /// <summary>
    /// 在指定父节点下按名字查找 Text。
    /// </summary>
    private Text FindText(RectTransform parent, string childName)
    {
        // Transform.Find 只找直接子物体。
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    /// <summary>
    /// 在指定父节点下按名字查找 Button。
    /// </summary>
    private Button FindButton(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        int fontSize,
        Color color,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        // 统一创建 Text，减少重复设置字体、颜色、对齐等代码。
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.text = string.Empty;
        text.supportRichText = false;
        text.raycastTarget = false;
        return text;
    }

#if UNITY_EDITOR
    private void MarkSceneDirtyIfNeeded(bool changed)
    {
        if (!changed || !gameObject.scene.IsValid())
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
