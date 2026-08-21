using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 加载场景控制器：读取 SceneFlowService 保存的目标场景，异步加载并刷新进度条。
/// 这里仅负责场景加载表现，不处理选角、背包或 Boss 等玩法数据。
/// </summary>
[DisallowMultipleComponent]
public sealed class LoadingSceneController : MonoBehaviour
{
    private const float SceneReadyProgress = 0.9f;
    private const string DefaultLoadingMessage = "正在加载，请稍候……";

    [Header("UI References")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Text progressText;
    [SerializeField] private Text loadingText;

    [Header("Progress Settings")]
    [Tooltip("显示进度每秒最多前进多少。显示值只会追赶真实进度，不会提前虚报。")]
    [SerializeField, Min(0.1f)] private float progressSmoothSpeed = 1.5f;

    [Tooltip("加载界面最少显示时间，避免小场景加载过快时只闪一下。")]
    [SerializeField, Min(0f)] private float minimumVisibleDuration = 0.8f;

    /// <summary>
    /// Awake 只初始化自身 UI 引用，不在这里启动异步加载。
    /// 加载协程放在 Start 中，确保场景内所有 UI 已完成初始化。
    /// </summary>
    private void Awake()
    {
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.wholeNumbers = false;
            progressSlider.interactable = false;
        }

        if (loadingText != null)
        {
            loadingText.text = DefaultLoadingMessage;
        }

        UpdateProgressUi(0f);
        ValidateReferences();
    }

    private IEnumerator Start()
    {
        yield return LoadTargetSceneAsync();
    }

    /// <summary>
    /// 异步加载目标场景，并把 Unity 的 0-0.9 加载区间换算为 UI 的 0%-100%。
    /// allowSceneActivation 为 false 时先停在加载场景，等进度动画和最低展示时间都完成后再激活目标场景。
    /// </summary>
    private IEnumerator LoadTargetSceneAsync()
    {
        string targetSceneName;
        if (!SceneFlowService.TryConsumePendingTargetScene(out targetSceneName))
        {
            // 直接从编辑器运行 LoadingScene 时没有上一个场景请求，安全回到登录场景。
            targetSceneName = GameSceneNames.LoginScene;
            Debug.LogWarning("LoadingScene 没有收到目标场景，已回退到 LoginScene。", this);
        }

        if (targetSceneName == GameSceneNames.LoadingScene ||
            !Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            HandleLoadFailure($"加载失败：目标场景 {targetSceneName} 无效或没有加入 Build Settings。 ");
            yield break;
        }

        AsyncOperation loadOperation;
        try
        {
            loadOperation = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }
        catch (System.Exception exception)
        {
            HandleLoadFailure($"加载场景 {targetSceneName} 时发生异常：{exception.Message}");
            yield break;
        }

        if (loadOperation == null)
        {
            HandleLoadFailure($"加载场景 {targetSceneName} 失败：Unity 没有返回异步加载任务。 ");
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        float elapsedTime = 0f;
        float displayedProgress = 0f;
        float highestRealProgress = 0f;
        float smoothSpeed = Mathf.Max(0.1f, progressSmoothSpeed);
        float minimumDuration = Mathf.Max(0f, minimumVisibleDuration);

        while (highestRealProgress < 1f || displayedProgress < 1f || elapsedTime < minimumDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            // 禁止激活时 AsyncOperation.progress 最大为 0.9，除以 0.9 后才对应 UI 的 100%。
            float normalizedRealProgress = Mathf.Clamp01(loadOperation.progress / SceneReadyProgress);
            highestRealProgress = Mathf.Max(highestRealProgress, normalizedRealProgress);

            // 只平滑追赶已经发生的真实进度，绝不让显示值跑到真实加载进度前面。
            displayedProgress = Mathf.MoveTowards(
                displayedProgress,
                highestRealProgress,
                smoothSpeed * Time.unscaledDeltaTime);

            UpdateProgressUi(displayedProgress);
            yield return null;
        }

        UpdateProgressUi(1f);
        yield return null;

        // 先解除重复跳转锁，再允许 Unity 激活已经准备好的目标场景。
        SceneFlowService.CompleteSceneTransition();
        loadOperation.allowSceneActivation = true;
    }

    private void UpdateProgressUi(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (progressSlider != null)
        {
            progressSlider.SetValueWithoutNotify(clampedProgress);
        }

        if (progressText != null)
        {
            progressText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
        }
    }

    private void HandleLoadFailure(string errorMessage)
    {
        SceneFlowService.CompleteSceneTransition();
        Debug.LogError(errorMessage, this);

        if (loadingText != null)
        {
            loadingText.text = "加载失败，请查看 Console。";
        }
    }

    private void ValidateReferences()
    {
        if (progressSlider == null || progressText == null || loadingText == null)
        {
            Debug.LogError("LoadingSceneController 的 UI 引用未配置完整，请重新生成或修复 LoadingScene。", this);
        }
    }
}
