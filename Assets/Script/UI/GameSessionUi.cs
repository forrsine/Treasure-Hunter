using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 游戏会话 UI：负责右上角分数、暂停菜单、游戏结束菜单。
/// 
/// 新手阅读顺序：
/// 1. Start 里确保 Canvas/EventSystem 存在，然后自动创建 HUD 和遮罩菜单。
/// 2. Update 监听 ESC，切换暂停菜单。
/// 3. ShowGameOver 会保存最高分并显示结束菜单。
/// 4. CaptureGameplayStateIfNeeded / RestoreGameplayStateIfNeeded 负责暂停和恢复时间、鼠标状态。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class GameSessionUi : MonoBehaviour
{
    /// <summary>
    /// 当前覆盖层模式。
    /// None 表示没有菜单，Pause 表示暂停菜单，GameOver 表示游戏结束菜单。
    /// </summary>
    private enum OverlayMode
    {
        None,
        Pause,
        GameOver
    }

    public static GameSessionUi Instance { get; private set; }

    // 下面这些字段主要保存运行时自动创建出来的 UI 引用。
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private bool editorPreviewOverlay = true;
    [SerializeField] private OverlayMode editorPreviewMode = OverlayMode.Pause;
    [SerializeField] private int editorPreviewScore = 123;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text chestBreakText;
    [SerializeField] private Text pauseHintText;
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Text overlayTitleText;
    [SerializeField] private Text overlayBodyText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private Text primaryButtonText;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private Text secondaryButtonText;

    // 依赖组件和字体缓存，避免每帧查找。
    private PlayerCo player;
    private Font font;
    private OverlayMode overlayMode;

    // displayedScore / displayedChestBreakCount 用来避免数值没变化时反复写 Text。
    private int displayedScore = int.MinValue;
    private int displayedChestBreakCount = int.MinValue;

    // 暂停/结束时会改 Time.timeScale 和鼠标状态，关闭菜单时需要恢复。
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;

    public bool IsGameplayInputBlocked => overlayMode != OverlayMode.None;
    private bool hasSubmittedScoreToServer;

    /// <summary>
    /// 缓存玩家和字体，并在运行时注册唯一会话 UI 实例。
    /// </summary>
    private void Awake()
    {
        // 这个脚本通常挂在玩家身上，所以直接拿 PlayerCo。
        player = GetComponent<PlayerCo>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (!Application.isPlaying)
        {
            return;
        }

        if (Instance != null && Instance != this)
        {
            // 场景中只允许一个会话 UI 实例。
            Destroy(this);
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 启用时订阅金库事件，或在编辑器里生成预览 UI。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            // 金库受伤/击破都会影响分数，所以订阅金库事件刷新 HUD。
            BoxCo.OnVaultStatsChanged += HandleVaultChanged;
            BoxCo.OnVaultDestroyed += HandleVaultChanged;
        }
        else
        {
            EnsureEditorUi();
        }
    }

    /// <summary>
    /// 禁用时取消订阅、恢复玩法状态并清理单例引用。
    /// </summary>
    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            BoxCo.OnVaultStatsChanged -= HandleVaultChanged;
            BoxCo.OnVaultDestroyed -= HandleVaultChanged;
            RestoreGameplayStateIfNeeded();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 编辑器参数变化时刷新预览。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorUi();
        }
    }

    /// <summary>
    /// 运行开始时创建 HUD/菜单结构，并进入正常游戏显示状态。
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
        BuildUiIfNeeded();

        // 开局没有暂停/结束菜单，只显示 HUD。
        SetOverlayMode(OverlayMode.None);
        RefreshScoreDisplay(true);
    }

    /// <summary>
    /// 刷新 HUD，并处理 ESC 暂停/继续输入。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshScoreDisplay(false);

        if (overlayMode == OverlayMode.GameOver)
        {
            return;
        }

        if (player != null && player.IsUpgradeSelectionActive)
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // ESC 在暂停菜单打开时是“继续游戏”，没打开时是“暂停”。
        if (overlayMode == OverlayMode.Pause)
        {
            ResumeGame();
            return;
        }

        if (overlayMode == OverlayMode.None && Time.timeScale > 0f)
        {
            ShowPauseMenu();
        }
    }

    /// <summary>
    /// 打开暂停菜单并冻结游戏时间。
    /// </summary>
    public void ShowPauseMenu()
    {
        if (overlayMode == OverlayMode.GameOver)
        {
            return;
        }

        // 保存暂停前的状态，然后把游戏停住。
        CaptureGameplayStateIfNeeded();
        SetOverlayMode(OverlayMode.Pause);
    }

    /// <summary>
    /// 打开游戏结束菜单，保存分数并冻结游戏。
    /// </summary>
    public void ShowGameOver()
    {
        // 游戏结束也要暂停，并保存本局分数。
        CaptureGameplayStateIfNeeded();
        PersistCurrentScore();
        SetOverlayMode(OverlayMode.GameOver);
    }

    /// <summary>
    /// 从暂停菜单返回游戏。
    /// </summary>
    public void ResumeGame()
    {
        if (overlayMode != OverlayMode.Pause)
        {
            return;
        }

        SetOverlayMode(OverlayMode.None);
        RestoreGameplayStateIfNeeded();
    }

    /// <summary>
    /// 重新加载主玩法场景。
    /// </summary>
    public void RestartGame()
    {
        // 重开前保存分数、恢复时间，避免新场景继承 timeScale = 0。
        PersistCurrentScore();
        PrepareForSceneTransition();
        SceneFlowService.RestartGameplay();
    }

    /// <summary>
    /// 退出游戏或停止编辑器播放模式。
    /// </summary>
    public void QuitGame()
    {
        // 编辑器里退出 Play 模式，打包后退出程序。
        PersistCurrentScore();
        PrepareForSceneTransition();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 编辑器预览路径：创建 UI 并套用预览文案。
    /// </summary>
    private void EnsureEditorUi()
    {
        player = GetComponent<PlayerCo>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureCanvas();
        BuildUiIfNeeded();
        ApplyEditorPreview();
    }

    /// <summary>
    /// 查找或创建会话 UI 使用的 Canvas。
    /// </summary>
    private void EnsureCanvas()
    {
        // 自动 UI 必须挂在 Canvas 下；这里复用或创建运行时 Canvas。
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
    /// 确保场景里有 EventSystem，否则按钮无法接收点击。
    /// </summary>
    private void EnsureEventSystem()
    {
        // Button 点击需要 EventSystem，场景没放时自动补一个。
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>
    /// 总入口：创建或复用 HUD 和覆盖菜单。
    /// </summary>
    private void BuildUiIfNeeded()
    {
        if (targetCanvas == null)
        {
            return;
        }

        // 优先查找已有同名 UI，方便你手动调整过后继续复用。
        if (hudRoot == null)
        {
            Transform existingHud = targetCanvas.transform.Find("GameHud");
            if (existingHud != null)
            {
                hudRoot = existingHud.gameObject;
            }
        }

        if (overlayRoot == null)
        {
            Transform existingOverlay = targetCanvas.transform.Find("SessionOverlay");
            if (existingOverlay != null)
            {
                overlayRoot = existingOverlay.gameObject;
            }
        }

        if (hudRoot == null)
        {
            BuildHud();
        }
        else
        {
            ResolveHudReferences();
        }

        if (overlayRoot == null)
        {
            BuildOverlay();
        }
        else
        {
            ResolveOverlayReferences();
        }
    }

    /// <summary>
    /// 创建右上角分数、击破次数和暂停提示 HUD。
    /// </summary>
    private void BuildHud()
    {
        // HUD 是游戏中常驻的轻量信息层，比如分数和 ESC 提示。
        hudRoot = new GameObject("GameHud", typeof(RectTransform));
        hudRoot.transform.SetParent(targetCanvas.transform, false);

        RectTransform hudRect = hudRoot.GetComponent<RectTransform>();
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.offsetMin = Vector2.zero;
        hudRect.offsetMax = Vector2.zero;

        scoreText = CreateText(
            "ScoreText",
            hudRoot.transform,
            32,
            Color.white,
            TextAnchor.MiddleRight,
            Vector2.zero,
            new Vector2(260f, 50f));
        scoreText.fontStyle = FontStyle.Bold;

        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(1f, 1f);
        scoreRect.anchorMax = new Vector2(1f, 1f);
        scoreRect.pivot = new Vector2(1f, 1f);
        scoreRect.anchoredPosition = new Vector2(-30f, -30f);

        chestBreakText = CreateText(
            "ChestBreakText",
            hudRoot.transform,
            22,
            new Color(0.9f, 0.95f, 1f, 1f),
            TextAnchor.MiddleRight,
            Vector2.zero,
            new Vector2(260f, 36f));

        RectTransform chestBreakRect = chestBreakText.rectTransform;
        chestBreakRect.anchorMin = new Vector2(1f, 1f);
        chestBreakRect.anchorMax = new Vector2(1f, 1f);
        chestBreakRect.pivot = new Vector2(1f, 1f);
        chestBreakRect.anchoredPosition = new Vector2(-30f, -74f);

        pauseHintText = CreateText(
            "PauseHintText",
            hudRoot.transform,
            20,
            new Color(0.85f, 0.92f, 1f, 1f),
            TextAnchor.MiddleRight,
            Vector2.zero,
            new Vector2(180f, 36f));
        pauseHintText.text = "ESC 暂停";

        RectTransform pauseRect = pauseHintText.rectTransform;
        pauseRect.anchorMin = new Vector2(1f, 1f);
        pauseRect.anchorMax = new Vector2(1f, 1f);
        pauseRect.pivot = new Vector2(1f, 1f);
        pauseRect.anchoredPosition = new Vector2(-30f, -112f);
    }

    /// <summary>
    /// 创建暂停/结束菜单的遮罩、文本和按钮。
    /// </summary>
    private void BuildOverlay()
    {
        // Overlay 是暂停/结束菜单的全屏遮罩。
        overlayRoot = new GameObject("SessionOverlay", typeof(RectTransform), typeof(Image));
        overlayRoot.transform.SetParent(targetCanvas.transform, false);
        overlayRoot.transform.SetAsLastSibling();

        RectTransform overlayRect = overlayRoot.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        Image overlayImage = overlayRoot.GetComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.72f);

        GameObject overlayPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(Outline));
        overlayPanel.transform.SetParent(overlayRoot.transform, false);

        RectTransform panelRect = overlayPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(620f, 360f);

        Image panelImage = overlayPanel.GetComponent<Image>();
        panelImage.color = new Color(0.08f, 0.11f, 0.16f, 0.98f);

        Outline outline = overlayPanel.GetComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.56f, 0.82f, 0.8f);
        outline.effectDistance = new Vector2(1f, -1f);

        overlayTitleText = CreateText(
            "Title",
            overlayPanel.transform,
            34,
            Color.white,
            TextAnchor.UpperCenter,
            new Vector2(0f, -36f),
            new Vector2(520f, 46f));
        overlayTitleText.fontStyle = FontStyle.Bold;

        overlayBodyText = CreateText(
            "Body",
            overlayPanel.transform,
            24,
            new Color(0.88f, 0.94f, 1f, 1f),
            TextAnchor.MiddleCenter,
            new Vector2(0f, -118f),
            new Vector2(520f, 96f));

        primaryButton = CreateButton(
            "PrimaryButton",
            overlayPanel.transform,
            new Vector2(0f, -206f),
            new Vector2(240f, 58f),
            out primaryButtonText);

        secondaryButton = CreateButton(
            "SecondaryButton",
            overlayPanel.transform,
            new Vector2(0f, -278f),
            new Vector2(240f, 58f),
            out secondaryButtonText);
    }

    /// <summary>
    /// 尝试复用层级中已有的 HUD 子节点引用。
    /// </summary>
    private void ResolveHudReferences()
    {
        // 如果 HUD 已存在，只重新抓取其中的 Text 引用。
        if (hudRoot == null)
        {
            return;
        }

        if (scoreText == null)
        {
            Transform textTransform = hudRoot.transform.Find("ScoreText");
            if (textTransform != null)
            {
                scoreText = textTransform.GetComponent<Text>();
            }
        }

        if (chestBreakText == null)
        {
            Transform chestBreakTransform = hudRoot.transform.Find("ChestBreakText");
            if (chestBreakTransform != null)
            {
                chestBreakText = chestBreakTransform.GetComponent<Text>();
            }
        }

        if (chestBreakText == null)
        {
            // 旧场景里已经有 GameHud 时，运行到这里会自动补上新的“宝箱击破次数”文本。
            chestBreakText = CreateText(
                "ChestBreakText",
                hudRoot.transform,
                22,
                new Color(0.9f, 0.95f, 1f, 1f),
                TextAnchor.MiddleRight,
                Vector2.zero,
                new Vector2(260f, 36f));
        }

        if (pauseHintText == null)
        {
            Transform hintTransform = hudRoot.transform.Find("PauseHintText");
            if (hintTransform != null)
            {
                pauseHintText = hintTransform.GetComponent<Text>();
            }
        }

        ApplyHudLayout();
    }

    /// <summary>
    /// 固定 HUD 的锚点、尺寸和相对位置。
    /// </summary>
    private void ApplyHudLayout()
    {
        // 统一摆放右上角 HUD：
        // 第一行是积分，第二行是已打破宝箱次数，第三行是 ESC 提示。
        // 旧场景自动补 Text 后也走这里，避免新旧 HUD 位置不一致。
        if (scoreText != null)
        {
            RectTransform scoreRect = scoreText.rectTransform;
            scoreRect.anchorMin = new Vector2(1f, 1f);
            scoreRect.anchorMax = new Vector2(1f, 1f);
            scoreRect.pivot = new Vector2(1f, 1f);
            scoreRect.sizeDelta = new Vector2(260f, 50f);
            scoreRect.anchoredPosition = new Vector2(-30f, -30f);
        }

        if (chestBreakText != null)
        {
            RectTransform chestBreakRect = chestBreakText.rectTransform;
            chestBreakRect.anchorMin = new Vector2(1f, 1f);
            chestBreakRect.anchorMax = new Vector2(1f, 1f);
            chestBreakRect.pivot = new Vector2(1f, 1f);
            chestBreakRect.sizeDelta = new Vector2(260f, 36f);
            chestBreakRect.anchoredPosition = new Vector2(-30f, -74f);
        }

        if (pauseHintText != null)
        {
            RectTransform pauseRect = pauseHintText.rectTransform;
            pauseRect.anchorMin = new Vector2(1f, 1f);
            pauseRect.anchorMax = new Vector2(1f, 1f);
            pauseRect.pivot = new Vector2(1f, 1f);
            pauseRect.sizeDelta = new Vector2(180f, 36f);
            pauseRect.anchoredPosition = new Vector2(-30f, -112f);
        }
    }

    /// <summary>
    /// 尝试复用层级中已有的覆盖菜单子节点引用。
    /// </summary>
    private void ResolveOverlayReferences()
    {
        // 如果菜单已存在，只重新抓取标题、正文、按钮引用。
        if (overlayRoot == null)
        {
            return;
        }

        Transform panelTransform = overlayRoot.transform.Find("Panel");
        if (panelTransform == null)
        {
            return;
        }

        if (overlayTitleText == null)
        {
            Transform child = panelTransform.Find("Title");
            if (child != null)
            {
                overlayTitleText = child.GetComponent<Text>();
            }
        }

        if (overlayBodyText == null)
        {
            Transform child = panelTransform.Find("Body");
            if (child != null)
            {
                overlayBodyText = child.GetComponent<Text>();
            }
        }

        if (primaryButton == null)
        {
            Transform child = panelTransform.Find("PrimaryButton");
            if (child != null)
            {
                primaryButton = child.GetComponent<Button>();
            }
        }

        if (secondaryButton == null)
        {
            Transform child = panelTransform.Find("SecondaryButton");
            if (child != null)
            {
                secondaryButton = child.GetComponent<Button>();
            }
        }

        if (primaryButtonText == null && primaryButton != null)
        {
            primaryButtonText = primaryButton.GetComponentInChildren<Text>(true);
        }

        if (secondaryButtonText == null && secondaryButton != null)
        {
            secondaryButtonText = secondaryButton.GetComponentInChildren<Text>(true);
        }
    }

    /// <summary>
    /// 切换覆盖菜单模式，并同步 UI 显隐和内容。
    /// </summary>
    private void SetOverlayMode(OverlayMode mode)
    {
        overlayMode = mode;

        if (overlayRoot == null)
        {
            return;
        }

        if (overlayMode == OverlayMode.None)
        {
            // None 表示没有遮罩菜单，恢复到纯游戏 HUD。
            overlayRoot.SetActive(false);
            return;
        }

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        ApplyOverlayContent(overlayMode, GetCurrentScore(), false);
    }

    /// <summary>
    /// 根据暂停/结束/预览模式填入菜单标题、正文和按钮。
    /// </summary>
    private void ApplyOverlayContent(OverlayMode mode, int score, bool preview)
    {
        if (overlayTitleText == null || overlayBodyText == null || primaryButtonText == null || secondaryButtonText == null)
        {
            return;
        }

        if (mode == OverlayMode.Pause)
        {
            // 暂停菜单：主按钮继续游戏，副按钮退出。
            overlayTitleText.text = "游戏暂停";
            overlayBodyText.text = $"当前分数：{score}";
            primaryButtonText.text = "回到游戏";
            secondaryButtonText.text = "退出登录";

            if (primaryButton != null)
            {
                primaryButton.onClick.RemoveAllListeners();
                if (!preview)
                {
                    primaryButton.onClick.AddListener(ResumeGame);
                }
            }

            if (secondaryButton != null)
            {
                secondaryButton.onClick.RemoveAllListeners();
                if (!preview)
                {
                    secondaryButton.onClick.AddListener(LogoutToLogin);
                }
            }

            return;
        }

        overlayTitleText.text = "游戏结束";
        overlayBodyText.text = $"当前分数：{score}\n历史最高分：{GameHighScore.GetHighScore()}";
        primaryButtonText.text = "重新开始";
        secondaryButtonText.text = "退出游戏";

        if (primaryButton != null)
        {
            primaryButton.onClick.RemoveAllListeners();
            if (!preview)
            {
                primaryButton.onClick.AddListener(RestartGame);
            }
        }

        if (secondaryButton != null)
        {
            secondaryButton.onClick.RemoveAllListeners();
            if (!preview)
            {
                secondaryButton.onClick.AddListener(QuitGame);
            }
        }
    }

    /// <summary>
    /// 编辑器里使用假数据展示 HUD 和菜单布局。
    /// </summary>
    private void ApplyEditorPreview()
    {
        // 编辑器预览：不按 Play 也能看到 UI 大概样子。
        if (hudRoot == null || overlayRoot == null)
        {
            return;
        }

        hudRoot.SetActive(true);

        if (scoreText != null)
        {
            scoreText.text = $"当前分数：{editorPreviewScore}";
        }

        if (chestBreakText != null)
        {
            chestBreakText.text = "已打破宝箱：0 次";
        }

        if (pauseHintText != null)
        {
            pauseHintText.text = "ESC 暂停";
        }

        if (editorPreviewOverlay && editorPreviewMode != OverlayMode.None)
        {
            overlayRoot.SetActive(true);
            ApplyOverlayContent(editorPreviewMode, editorPreviewScore, true);
            return;
        }

        overlayRoot.SetActive(false);
    }

    /// <summary>
    /// 金库数值变化时刷新分数 HUD。
    /// </summary>
    private void HandleVaultChanged(BoxCo _)
    {
        RefreshScoreDisplay(true);
    }

    /// <summary>
    /// 刷新分数和金库击破次数，数值没变时跳过写 Text。
    /// </summary>
    private void RefreshScoreDisplay(bool force)
    {
        int score = GetCurrentScore();
        int chestBreakCount = GetCurrentChestBreakCount();
        if (!force && score == displayedScore && chestBreakCount == displayedChestBreakCount)
        {
            return;
        }

        displayedScore = score;
        displayedChestBreakCount = chestBreakCount;

        if (scoreText != null)
        {
            scoreText.text = $"当前分数：{score}";
        }

        if (chestBreakText != null)
        {
            chestBreakText.text = $"已打破宝箱：{chestBreakCount} 次";
        }

        // 如果菜单开着，菜单里的分数也要同步刷新。
        if (overlayMode != OverlayMode.None)
        {
            ApplyOverlayContent(overlayMode, score, false);
        }
    }

    /// <summary>
    /// 从金库实例读取当前累计分数。
    /// </summary>
    private int GetCurrentScore()
    {
        BoxCo vault = GameplayRuntime.Instance.CurrentVault;
        return vault != null ? GameplayRuntime.Instance.CurrentScore : editorPreviewScore;
    }

    /// <summary>
    /// 从金库实例读取当前击破次数。
    /// </summary>
    private int GetCurrentChestBreakCount()
    {
        // BoxCo.DestroyedCount 保存当前已经完整击破过多少次宝箱。
        return GameplayRuntime.Instance.CurrentVaultDestroyedCount;
    }

    /// <summary>
    /// 将本局分数尝试写入历史最高分。
    /// </summary>
    private void PersistCurrentScore()
    {
        int score = GetCurrentScore();

        // 先保留原来的本地最高分逻辑。
        GameHighScore.UpdateHighScore(score);

        if (!Application.isPlaying || hasSubmittedScoreToServer)
        {
            return;
        }

        GameApiClient apiClient = SceneFlowService.FindApiClient();

        if (apiClient == null || !apiClient.IsLoggedIn)
        {
            return;
        }

        hasSubmittedScoreToServer = true;

        StartCoroutine(apiClient.SubmitHighScore(score, (success, message, serverHighScore) =>
        {
            Debug.Log($"同步最高分：{success}, {message}, 服务器最高分：{serverHighScore}");
        }));
    }

    /// <summary>
    /// 缓存当前时间和鼠标状态，然后进入菜单暂停状态。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        // 第一次打开菜单时缓存当前时间和鼠标状态。
        if (!hasCapturedGameplayState)
        {
            cachedTimeScale = Time.timeScale;
            cachedCursorLockMode = Cursor.lockState;
            cachedCursorVisible = Cursor.visible;
            hasCapturedGameplayState = true;
        }

        // timeScale = 0 会暂停大多数基于 Time.deltaTime 的玩法逻辑。
        Time.timeScale = 0f;

        // 暂停菜单弹出时需要显示鼠标；统一放到左上角，避免误点中间按钮。
        CursorPopupUtility.ShowAtTopLeft();
    }

    /// <summary>
    /// 关闭菜单时恢复之前缓存的时间和鼠标状态。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
        // 只有确实缓存过状态，才需要恢复。
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        hasCapturedGameplayState = false;
    }

    /// <summary>
    /// 切场景或退出前恢复全局状态，避免暂停状态泄漏到下一场景。
    /// </summary>
    private void PrepareForSceneTransition()
    {
        // 切换场景前强制回到正常时间，避免新场景加载后仍然暂停。
        overlayMode = OverlayMode.None;
        hasCapturedGameplayState = false;
        Time.timeScale = 1f;

        // 切换场景前也会显示鼠标，同样放到左上角，保持所有弹鼠标行为一致。
        CursorPopupUtility.ShowAtTopLeft();

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchoredPosition,
        Vector2 size,
        out Text labelText)
    {
        // 运行时创建按钮的工具方法：创建 GameObject、Image、Button，再创建 Label。
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        Color normalColor = new Color(0.18f, 0.34f, 0.56f, 1f);
        image.color = normalColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = new Color(0.24f, 0.42f, 0.68f, 1f);
        colors.pressedColor = new Color(0.14f, 0.26f, 0.44f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        labelText = CreateText(
            "Label",
            buttonObject.transform,
            24,
            Color.white,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            size);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.fontStyle = FontStyle.Bold;

        return button;
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
        // 运行时创建 Text 的工具方法，统一字体、换行、对齐等基础设置。
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        return text;
    }

    private void LogoutToLogin()
    {
        PrepareForSceneTransition();
        SceneFlowService.LogoutToLogin();
    }
}
