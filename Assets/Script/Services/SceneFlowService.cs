using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景流程服务：统一处理登录、选角、进入游戏、重开和登出。
/// 所有切场景入口先恢复 Time.timeScale，避免从暂停/结算界面切场景后新场景仍被冻结。
/// </summary>
public static class SceneFlowService
{
    private static string pendingTargetSceneName;
    private static bool isSceneTransitionInProgress;

    /// <summary>
    /// 每次进入 Play Mode 或启动客户端时清空静态请求。
    /// 即使项目关闭了 Domain Reload，也不会把上一次运行残留的目标场景带进新会话。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSceneTransitionState()
    {
        pendingTargetSceneName = null;
        isSceneTransitionInProgress = false;
    }

    /// <summary>
    /// 获取现有的 GameApiClient；如果场景里还没有，就自动创建一个常驻对象。
    /// </summary>
    public static GameApiClient GetOrCreateApiClient()
    {
        GameApiClient existing = FindApiClient();
        if (existing != null)
        {
            return existing;
        }

        GameObject apiClientObject = new GameObject("GameApiClient");
        return apiClientObject.AddComponent<GameApiClient>();
    }

    /// <summary>
    /// 查找当前可用的 API 客户端。
    /// 先走单例，再回退到场景查找，兼容初始化先后顺序不同的情况。
    /// </summary>
    public static GameApiClient FindApiClient()
    {
        return GameApiClient.Instance != null
            ? GameApiClient.Instance
            : Object.FindObjectOfType<GameApiClient>();
    }

    /// <summary>
    /// 进入选角场景。
    /// </summary>
    public static void LoadCharacterSelectScene()
    {
        LoadSceneWithLoading(GameSceneNames.CharacterSelectScene);
    }

    /// <summary>
    /// 当前角色正常保存并离场后返回角色选择界面。
    /// 与 LogoutToLogin 不同，这里只结束角色会话，保留账号登录态和最新角色缓存。
    /// </summary>
    public static void ReturnToCharacterSelect()
    {
        // FlushAndLeave 成功时已经结束存档会话，这里再次清理是为了兼容其他安全调用入口。
        CharacterProgressSaveService.Instance?.EndSession();
        GameplayCharacterManager.Instance.Clear();
        TreasureHunterArchitecture.Interface.SendCommand(new ResetInventoryCommand());
        SelectedCharacterState.Clear();
        GameplayRuntime.Instance.ClearVaultProgressCache();
        BossRunProgressState.ResetRun();
        PlayerSceneTransferState.Clear();
        GameplayStartupGuideState.ResetSession();
        LoadSceneWithLoading(GameSceneNames.CharacterSelectScene);
    }

    /// <summary>
    /// 保存当前选中的角色，并切换到玩法场景。
    /// </summary>
    public static void StartGameplay(NCharacter selectedCharacter)
    {
        SelectedCharacterState.SetCharacter(selectedCharacter);
        CharacterProgressSaveService saveService = CharacterProgressSaveService.Instance;
        if (saveService != null)
        {
            saveService.BeginSession(selectedCharacter);
        }
        // 选择角色代表开始新的角色会话，背包从空状态重新累计。
        TreasureHunterArchitecture.Interface.SendCommand(new ResetInventoryCommand());
        GameplayRuntime.Instance.ClearVaultProgressCache();
        BossRunProgressState.RestorePersistentProgress(
            selectedCharacter != null ? selectedCharacter.vaultDestroyedCount : 0,
            selectedCharacter != null ? selectedCharacter.completedBossCount : 0);
        GameplayStartupGuideState.ResetSession();
        LoadSceneWithLoading(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// 重新进入玩法场景。
    /// </summary>
    public static void RestartGameplay()
    {
        LoadSceneWithLoading(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// Boss 胜利后返回主场景。它属于同一局流程，不执行死亡/主动重开的强化清零规则。
    /// </summary>
    public static void ReturnToGameplayFromBoss()
    {
        LoadSceneWithLoading(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// 进入 Boss 房间场景。
    /// 统一从场景流程服务切换，后续要加读条、淡入淡出或存档检查时不用到处改入口。
    /// </summary>
    public static void LoadBossRoomScene()
    {
        GameplayRuntime.Instance.CacheCurrentVaultProgress();
        LoadSceneWithLoading(GameSceneNames.BossRoomScene);
    }

    /// <summary>
    /// 退出登录并回到登录场景。
    /// </summary>
    public static void LogoutToLogin(GameApiClient apiClient = null)
    {
        ClearSession(apiClient);
        TreasureHunterArchitecture.Interface.SendCommand(new ResetInventoryCommand());
        SelectedCharacterState.Clear();
        GameplayRuntime.Instance.ClearVaultProgressCache();
        BossRunProgressState.ResetRun();
        GameplayStartupGuideState.ResetSession();
        LoadSceneWithLoading(GameSceneNames.LoginScene);
    }

    /// <summary>
    /// 统一场景跳转入口：先记录目标场景，再进入轻量 LoadingScene。
    /// LoadingScene 会异步加载真正目标，并把真实加载进度同步给 UI。
    /// </summary>
    public static void LoadSceneWithLoading(string targetSceneName)
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("场景跳转失败：目标场景名称不能为空。");
            return;
        }

        if (targetSceneName == GameSceneNames.LoadingScene)
        {
            Debug.LogError("场景跳转失败：LoadingScene 不能把自己作为目标场景。");
            return;
        }

        if (isSceneTransitionInProgress)
        {
            Debug.LogWarning($"场景跳转正在进行，已忽略重复请求：{targetSceneName}");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"场景跳转失败：目标场景 {targetSceneName} 没有加入 Build Settings。 ");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(GameSceneNames.LoadingScene))
        {
            Debug.LogError($"场景跳转失败：{GameSceneNames.LoadingScene} 没有加入 Build Settings。 ");
            return;
        }

        pendingTargetSceneName = targetSceneName;
        isSceneTransitionInProgress = true;
        PrepareForSceneLoad();

        try
        {
            // LoadingScene 本身非常轻量，先快速进入它，再由它异步加载真正的大场景。
            SceneManager.LoadScene(GameSceneNames.LoadingScene);
        }
        catch (System.Exception exception)
        {
            CompleteSceneTransition();
            Debug.LogError($"进入加载场景失败：{exception.Message}");
        }
    }

    /// <summary>
    /// LoadingScene 读取一次待加载目标。
    /// 使用“取走”语义，避免同一个请求被多个控制器重复执行。
    /// </summary>
    internal static bool TryConsumePendingTargetScene(out string targetSceneName)
    {
        targetSceneName = pendingTargetSceneName;
        pendingTargetSceneName = null;
        return !string.IsNullOrWhiteSpace(targetSceneName);
    }

    /// <summary>
    /// 目标场景即将激活或加载失败时结束本次跳转，允许后续再次发起请求。
    /// </summary>
    internal static void CompleteSceneTransition()
    {
        pendingTargetSceneName = null;
        isSceneTransitionInProgress = false;
    }

    /// <summary>
    /// 清理本地会话数据。
    /// 如果已有运行中的 GameApiClient，则优先走它的统一清理入口。
    /// </summary>
    public static void ClearSession(GameApiClient apiClient = null)
    {
        CharacterProgressSaveService.Instance?.EndSession();
        GameApiClient targetClient = apiClient != null ? apiClient : GameApiClient.Instance;
        if (targetClient != null)
        {
            targetClient.ClearSession();
            return;
        }

        PlayerPrefs.DeleteKey("AuthToken");
        PlayerPrefs.DeleteKey("AuthUsername");
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 切场景前的统一收尾。
    /// 当前至少要恢复 Time.timeScale，避免暂停状态带到新场景。
    /// </summary>
    public static void PrepareForSceneLoad()
    {
        Time.timeScale = 1f;
    }
}
