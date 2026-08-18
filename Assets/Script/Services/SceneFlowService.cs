using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景流程服务：统一处理登录、选角、进入游戏、重开和登出。
/// 所有切场景入口先恢复 Time.timeScale，避免从暂停/结算界面切场景后新场景仍被冻结。
/// </summary>
public static class SceneFlowService
{
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
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.CharacterSelectScene);
    }

    /// <summary>
    /// 保存当前选中的角色，并切换到玩法场景。
    /// </summary>
    public static void StartGameplay(NCharacter selectedCharacter)
    {
        SelectedCharacterState.SetCharacter(selectedCharacter);
        // 选择角色代表开始新的角色会话，背包从空状态重新累计。
        TreasureHunterArchitecture.Interface.SendCommand(new ResetInventoryCommand());
        GameplayRuntime.Instance.ClearVaultProgressCache();
        BossRunProgressState.ResetRun();
        GameplayStartupGuideState.ResetSession();
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// 重新进入玩法场景。
    /// </summary>
    public static void RestartGameplay()
    {
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// 进入 Boss 房间场景。
    /// 统一从场景流程服务切换，后续要加读条、淡入淡出或存档检查时不用到处改入口。
    /// </summary>
    public static void LoadBossRoomScene()
    {
        GameplayRuntime.Instance.CacheCurrentVaultProgress();
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.BossRoomScene);
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
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.LoginScene);
    }

    /// <summary>
    /// 清理本地会话数据。
    /// 如果已有运行中的 GameApiClient，则优先走它的统一清理入口。
    /// </summary>
    public static void ClearSession(GameApiClient apiClient = null)
    {
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
