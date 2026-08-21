using QFramework;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 游戏会话 UI：负责分数 HUD、暂停菜单和游戏结束菜单。
/// 所有控件都必须来自 GameplayUiRoot Prefab；运行时只更新内容和显隐，不查找或创建 UI 对象。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GameSessionUi : MonoBehaviour, IController
{
    private enum OverlayMode
    {
        None,
        Pause,
        GameOver
    }

    public static GameSessionUi Instance { get; private set; }

    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
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
    [SerializeField] private InventoryPanel inventoryPanel;

    [Header("Editor Preview")]
    [SerializeField] private bool editorPreviewOverlay = true;
    [SerializeField] private OverlayMode editorPreviewMode = OverlayMode.Pause;
    [SerializeField] private int editorPreviewScore = 123;

    private OverlayMode overlayMode;
    private int displayedScore = int.MinValue;
    private int displayedChestBreakCount = int.MinValue;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;
    private bool hasSubmittedScoreToServer;

    public bool IsGameplayInputBlocked =>
        overlayMode != OverlayMode.None || (inventoryPanel != null && inventoryPanel.IsOpen);

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 运行时确保场景里只有一个 GameSessionUi。
    /// 它负责暂停和结算控制，如果有多个实例，按钮和时间缩放都会互相冲突。
    /// </summary>
    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Instance != null && Instance != this)
        {
            Debug.LogError("场景中存在多个 GameSessionUi，请只保留 GameplayUiRoot Prefab 中的实例。", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    /// <summary>
    /// 运行时注册死亡和金库状态事件；编辑器下则刷新预览。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            this.RegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
            BoxCo.OnVaultStatsChanged += HandleVaultChanged;
            BoxCo.OnVaultDestroyed += HandleVaultChanged;
            return;
        }

        ApplyEditorPreviewIfReady();
    }

    /// <summary>
    /// 停用时解除事件，并恢复暂停期间缓存的全局状态。
    /// </summary>
    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            this.UnRegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
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
    /// 编辑器里修改引用或预览参数时，立即刷新预览效果。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
        }
    }

    /// <summary>
    /// Start 时校验 Prefab 引用，并初始化 HUD 与遮罩状态。
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
            return;
        }

        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        SetOverlayMode(OverlayMode.None);
        RefreshScoreDisplay(true);
    }

    /// <summary>
    /// 每帧刷新分数显示，并处理 ESC 暂停切换。
    /// 游戏结束或升级面板激活时，不再响应暂停输入。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshScoreDisplay(false);
        if (overlayMode == OverlayMode.GameOver || this.GetModel<PlayerModel>().Stats.IsUpgradeSelectionActive)
        {
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
        {
            return;
        }

        // 背包属于比暂停菜单更靠前的模态层，ESC 先关闭背包，避免同一帧又打开暂停菜单。
        if (inventoryPanel != null && inventoryPanel.IsOpen)
        {
            inventoryPanel.Close();
            return;
        }

        if (overlayMode == OverlayMode.Pause)
        {
            ResumeGame();
        }
        else if (overlayMode == OverlayMode.None && Time.timeScale > 0f)
        {
            ShowPauseMenu();
        }
    }

    /// <summary>
    /// 打开暂停界面。
    /// </summary>
    public void ShowPauseMenu()
    {
        if (overlayMode == OverlayMode.GameOver)
        {
            return;
        }

        CaptureGameplayStateIfNeeded();
        SetOverlayMode(OverlayMode.Pause);
    }

    /// <summary>
    /// 打开游戏结束界面，并先落地本局分数。
    /// </summary>
    public void ShowGameOver()
    {
        CaptureGameplayStateIfNeeded();
        PersistCurrentScore();
        SetOverlayMode(OverlayMode.GameOver);
    }

    /// <summary>
    /// 从暂停界面恢复游戏。
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
    /// 重新开始当前玩法场景。
    /// </summary>
    public void RestartGame()
    {
        PersistCurrentScore();
        StartCoroutine(ResetProgressAndRestart());
    }

    /// <summary>
    /// 退出游戏，或在编辑器里停止播放。
    /// </summary>
    public void QuitGame()
    {
        PersistCurrentScore();
        StartCoroutine(SaveAndQuit());
    }

    private IEnumerator ResetProgressAndRestart()
    {
        CharacterProgressSaveService saveService = CharacterProgressSaveService.Instance;
        if (saveService == null || !saveService.IsSessionActive)
        {
            PrepareForSceneTransition();
            SceneFlowService.RestartGameplay();
            yield break;
        }

        bool success = false;
        string message = "";
        SetTransitionPending("正在清空本局强化并保存...");
        yield return saveService.FlushNow(true, (result, resultMessage, _) =>
        {
            success = result;
            message = resultMessage;
        });

        if (!success)
        {
            ShowTransitionError(message);
            yield break;
        }

        PrepareForSceneTransition();
        SceneFlowService.RestartGameplay();
    }

    private IEnumerator SaveAndQuit()
    {
        CharacterProgressSaveService saveService = CharacterProgressSaveService.Instance;
        if (saveService != null && saveService.IsSessionActive)
        {
            bool success = false;
            string message = "";
            SetTransitionPending("正在保存并退出...");
            yield return saveService.FlushAndLeave(false, (result, resultMessage) =>
            {
                success = result;
                message = resultMessage;
            });

            if (!success)
            {
                ShowTransitionError(message);
                yield break;
            }
        }

        PrepareForSceneTransition();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 检查 Prefab 是否完整装配。纯 Prefab 模式下缺少引用时直接报错，不在运行时偷偷创建替代对象。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (hudRoot == null) missing = nameof(hudRoot);
        else if (scoreText == null) missing = nameof(scoreText);
        else if (chestBreakText == null) missing = nameof(chestBreakText);
        else if (pauseHintText == null) missing = nameof(pauseHintText);
        else if (overlayRoot == null) missing = nameof(overlayRoot);
        else if (overlayTitleText == null) missing = nameof(overlayTitleText);
        else if (overlayBodyText == null) missing = nameof(overlayBodyText);
        else if (primaryButton == null) missing = nameof(primaryButton);
        else if (primaryButtonText == null) missing = nameof(primaryButtonText);
        else if (secondaryButton == null) missing = nameof(secondaryButton);
        else if (secondaryButtonText == null) missing = nameof(secondaryButtonText);
        else if (inventoryPanel == null) missing = nameof(inventoryPanel);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"GameSessionUi 的 Prefab 引用未配置：{missing}。请修复 GameplayUiRoot.prefab。", this);
        }

        return false;
    }

    /// <summary>
    /// 切换当前遮罩模式。
    /// 这里只负责显隐与调度，具体文案和按钮逻辑在 ApplyOverlayContent 里填充。
    /// </summary>
    private void SetOverlayMode(OverlayMode mode)
    {
        overlayMode = mode;
        if (overlayRoot == null)
        {
            return;
        }

        if (mode == OverlayMode.None)
        {
            overlayRoot.SetActive(false);
            return;
        }

        overlayRoot.SetActive(true);
        overlayRoot.transform.SetAsLastSibling();
        ApplyOverlayContent(mode, GetCurrentScore(), false);
    }

    /// <summary>
    /// 根据模式填充遮罩文案和按钮事件。
    /// 预览模式下只更新文案，不绑定真实点击行为。
    /// </summary>
    private void ApplyOverlayContent(OverlayMode mode, int score, bool preview)
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        primaryButton.onClick.RemoveAllListeners();
        secondaryButton.onClick.RemoveAllListeners();

        if (mode == OverlayMode.Pause)
        {
            overlayTitleText.text = "游戏暂停";
            overlayBodyText.text = $"当前分数：{score}";
            primaryButtonText.text = "回到游戏";
            secondaryButtonText.text = "保存并退出";
            if (!preview)
            {
                primaryButton.onClick.AddListener(ResumeGame);
                secondaryButton.onClick.AddListener(SaveAndReturnToCharacterSelect);
            }

            return;
        }

        overlayTitleText.text = "游戏结束";
        overlayBodyText.text = $"当前分数：{score}\n历史最高分：{GameHighScore.GetHighScore()}";
        primaryButtonText.text = "重新开始";
        secondaryButtonText.text = "退出游戏";
        if (!preview)
        {
            primaryButton.onClick.AddListener(RestartGame);
            secondaryButton.onClick.AddListener(QuitGame);
        }
    }

    /// <summary>
    /// 编辑器预览逻辑。
    /// 这样你在 Prefab 模式下也能直接检查 HUD 和弹窗的排版是否正确。
    /// </summary>
    private void ApplyEditorPreviewIfReady()
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        hudRoot.SetActive(true);
        scoreText.text = $"当前分数：{editorPreviewScore}";
        chestBreakText.text = "已打破宝箱：0 次";
        pauseHintText.text = "ESC 暂停";

        bool showOverlay = editorPreviewOverlay && editorPreviewMode != OverlayMode.None;
        overlayRoot.SetActive(showOverlay);
        if (showOverlay)
        {
            ApplyOverlayContent(editorPreviewMode, editorPreviewScore, true);
        }
    }

    private void HandleVaultChanged(BoxCo _)
    {
        RefreshScoreDisplay(true);
    }

    private void RefreshScoreDisplay(bool force)
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        int score = GetCurrentScore();
        int chestBreakCount = GetCurrentChestBreakCount();
        if (!force && score == displayedScore && chestBreakCount == displayedChestBreakCount)
        {
            return;
        }

        displayedScore = score;
        displayedChestBreakCount = chestBreakCount;
        scoreText.text = $"当前分数：{score}";
        chestBreakText.text = $"已打破宝箱：{chestBreakCount} 次";

        if (overlayMode != OverlayMode.None)
        {
            ApplyOverlayContent(overlayMode, score, false);
        }
    }

    private int GetCurrentScore()
    {
        return Application.isPlaying
            ? GameplayRuntime.Instance.CurrentScore
            : editorPreviewScore;
    }

    private int GetCurrentChestBreakCount()
    {
        return GameplayRuntime.Instance.CurrentVaultDestroyedCount;
    }

    private void HandlePlayerDied(PlayerDiedEvent _)
    {
        ShowGameOver();
    }

    private void PersistCurrentScore()
    {
        int score = GetCurrentScore();
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
    /// 进入暂停或结算前，缓存当前全局状态并切到 UI 可交互模式。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        if (!hasCapturedGameplayState)
        {
            cachedTimeScale = Time.timeScale;
            cachedCursorLockMode = Cursor.lockState;
            cachedCursorVisible = Cursor.visible;
            hasCapturedGameplayState = true;
        }

        Time.timeScale = 0f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
    }

    /// <summary>
    /// 从暂停/结算状态恢复回正常玩法状态。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
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
    /// 切场景前的统一 UI 收尾，避免把旧场景的暂停状态带到新场景。
    /// </summary>
    private void PrepareForSceneTransition()
    {
        overlayMode = OverlayMode.None;
        hasCapturedGameplayState = false;
        Time.timeScale = 1f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }
    }

    /// <summary>
    /// 从暂停界面保存当前角色并返回角色选择界面。
    /// 这里只结束角色会话，账号登录态由常驻 GameApiClient 继续保留。
    /// </summary>
    private void SaveAndReturnToCharacterSelect()
    {
        PersistCurrentScore();
        StartCoroutine(SaveAndReturnToCharacterSelectRoutine());
    }

    private IEnumerator SaveAndReturnToCharacterSelectRoutine()
    {
        CharacterProgressSaveService saveService = CharacterProgressSaveService.Instance;
        if (saveService == null || !saveService.IsSessionActive)
        {
            ShowTransitionError("当前角色会话不存在，无法保存并退出。");
            yield break;
        }

        bool success = false;
        string message = "";
        SetTransitionPending("正在保存并返回角色选择...");
        yield return saveService.FlushAndLeave(false, (result, resultMessage) =>
        {
            success = result;
            message = resultMessage;
        });

        if (!success)
        {
            ShowTransitionError(message);
            yield break;
        }

        PrepareForSceneTransition();
        SceneFlowService.ReturnToCharacterSelect();
    }

    private void SetTransitionPending(string message)
    {
        primaryButton.interactable = false;
        secondaryButton.interactable = false;
        overlayBodyText.text = message;
    }

    private void ShowTransitionError(string message)
    {
        primaryButton.interactable = true;
        secondaryButton.interactable = true;
        overlayBodyText.text = string.IsNullOrEmpty(message)
            ? "存档失败，请检查服务端后重试。"
            : message;
    }
}
