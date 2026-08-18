using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 新手说明弹窗的当前角色会话状态。
/// 这里不使用 PlayerPrefs 持久化，因为需求是“每次重新选角色可以重新显示一次”，不是永久只显示一次。
/// </summary>
public static class GameplayStartupGuideState
{
    public static bool HasShownInCurrentSession { get; private set; }

    public static void MarkShown()
    {
        HasShownInCurrentSession = true;
    }

    public static void ResetSession()
    {
        HasShownInCurrentSession = false;
    }
}

/// <summary>
/// 游戏开始时的新手说明弹窗。
/// 弹窗层级和控件必须在场景/Prefab 中提前配置；运行时只绑定关闭按钮、写文案和切换显隐。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GameplayStartupGuidePopup : MonoBehaviour
{
    public static bool IsRuntimePopupVisible { get; private set; }

    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Image backdropImage;
    [SerializeField] private Image panelImage;
    [SerializeField] private Text titleText;
    [SerializeField] private Text bodyText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text closeButtonText;

    [Header("Behaviour")]
    [SerializeField] private bool editorPreviewVisible = true;
    [SerializeField] private bool showOnPlayStart = true;
    [SerializeField] private float startupDelaySeconds;
    [SerializeField] private bool lockCursorWhenClosed = true;
    [SerializeField] private bool useTextOverrides;
    [SerializeField] private string popupTitle = string.Empty;
    [SerializeField, TextArea(8, 14)] private string popupBody = string.Empty;

    private Coroutine startupCoroutine;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;

    /// <summary>
    /// 启用时校验引用、绑定关闭按钮，并根据运行模式决定显示编辑器预览还是启动时弹窗逻辑。
    /// </summary>
    private void OnEnable()
    {
        IsRuntimePopupVisible = false;
        if (!ValidatePrefabReferences(Application.isPlaying))
        {
            if (Application.isPlaying)
            {
                enabled = false;
            }

            return;
        }

        ApplyContent();
        closeButtonText.text = "X";
        closeButton.onClick.RemoveListener(ClosePopup);

        if (!Application.isPlaying)
        {
            popupRoot.SetActive(editorPreviewVisible);
            return;
        }

        closeButton.onClick.AddListener(ClosePopup);
        HidePopupImmediate();
        if (showOnPlayStart && ShouldShowAtRuntimeStartup())
        {
            startupCoroutine = StartCoroutine(ShowPopupAtStartup());
        }
    }

    /// <summary>
    /// 停用时解除按钮监听、停止协程并恢复全局状态。
    /// </summary>
    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePopup);
        }

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
    /// 编辑器下修改引用或文案覆盖时，实时刷新预览内容。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying && ValidatePrefabReferences(false))
        {
            ApplyContent();
            closeButtonText.text = "X";
            popupRoot.SetActive(editorPreviewVisible);
        }
    }

    /// <summary>
    /// 弹窗显示期间持续确保鼠标保持可见且未锁定。
    /// </summary>
    private void Update()
    {
        if (Application.isPlaying && IsRuntimePopupVisible &&
            (Cursor.lockState != CursorLockMode.None || !Cursor.visible))
        {
            CursorPopupUtility.ShowAtUpperCenterQuarter();
        }
    }

    /// <summary>
    /// 主动显示新手说明弹窗，并暂停当前玩法时间。
    /// </summary>
    public void ShowPopup()
    {
        if (!ValidatePrefabReferences(true))
        {
            return;
        }

        ApplyContent();
        CaptureGameplayStateIfNeeded();
        popupRoot.SetActive(true);
        popupRoot.transform.SetAsLastSibling();
        IsRuntimePopupVisible = true;
    }

    /// <summary>
    /// 关闭弹窗并恢复进入弹窗前的游戏状态。
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
    /// 纯引用模式的完整性检查。缺少引用时只报错，不再查找或创建替代节点。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (popupRoot == null) missing = nameof(popupRoot);
        else if (backdropImage == null) missing = nameof(backdropImage);
        else if (panelImage == null) missing = nameof(panelImage);
        else if (titleText == null) missing = nameof(titleText);
        else if (bodyText == null) missing = nameof(bodyText);
        else if (closeButton == null) missing = nameof(closeButton);
        else if (closeButtonText == null) missing = nameof(closeButtonText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"GameplayStartupGuidePopup 的引用未配置：{missing}。", this);
        }

        return false;
    }

    /// <summary>
    /// 启动时延迟显示弹窗。
    /// 这样可以避免和场景开场的其他初始化逻辑抢同一帧。
    /// </summary>
    private IEnumerator ShowPopupAtStartup()
    {
        if (startupDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(startupDelaySeconds);
        }

        if (!ShouldShowAtRuntimeStartup())
        {
            startupCoroutine = null;
            yield break;
        }

        GameplayStartupGuideState.MarkShown();
        ShowPopup();
        startupCoroutine = null;
    }

    /// <summary>
    /// 游戏说明只在当前角色会话第一次进入主场景时弹出。
    /// Boss 房和从 Boss 房返回主场景都复用 GameplayUiRoot，但不能再重复打断玩家。
    /// </summary>
    private bool ShouldShowAtRuntimeStartup()
    {
        return Application.isPlaying &&
               SceneManager.GetActiveScene().name == GameSceneNames.GameplayScene &&
               !GameplayStartupGuideState.HasShownInCurrentSession;
    }

    /// <summary>
    /// 把 Inspector 里配置的标题和正文写到文本控件上。
    /// </summary>
    private void ApplyContent()
    {
        if (!useTextOverrides)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(popupTitle))
        {
            titleText.text = popupTitle;
        }

        if (!string.IsNullOrWhiteSpace(popupBody))
        {
            bodyText.text = popupBody;
        }
    }

    /// <summary>
    /// 在不触发完整恢复流程的前提下，立刻把弹窗隐藏并清空内部状态。
    /// </summary>
    private void HidePopupImmediate()
    {
        popupRoot.SetActive(false);
        hasCapturedGameplayState = false;
        IsRuntimePopupVisible = false;
    }

    /// <summary>
    /// 首次打开弹窗时缓存当前 TimeScale 和鼠标状态，然后切到 UI 可交互模式。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        if (hasCapturedGameplayState)
        {
            return;
        }

        cachedTimeScale = Time.timeScale;
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        Time.timeScale = 0f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
        hasCapturedGameplayState = true;
    }

    /// <summary>
    /// 按进入弹窗前缓存的状态恢复游戏。
    /// 如果勾选了 lockCursorWhenClosed，则无论之前状态如何，都会回到玩法常用的锁鼠标模式。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        if (lockCursorWhenClosed)
        {
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
}
