using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneFlowService
{
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

    public static GameApiClient FindApiClient()
    {
        return GameApiClient.Instance != null
            ? GameApiClient.Instance
            : Object.FindObjectOfType<GameApiClient>();
    }

    public static void LoadCharacterSelectScene()
    {
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.CharacterSelectScene);
    }

    public static void StartGameplay(NCharacter selectedCharacter)
    {
        SelectedCharacterState.SetCharacter(selectedCharacter);
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.GameplayScene);
    }

    public static void RestartGameplay()
    {
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.GameplayScene);
    }

    public static void LogoutToLogin(GameApiClient apiClient = null)
    {
        ClearSession(apiClient);
        SelectedCharacterState.Clear();
        PrepareForSceneLoad();
        SceneManager.LoadScene(GameSceneNames.LoginScene);
    }

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

    public static void PrepareForSceneLoad()
    {
        Time.timeScale = 1f;
    }
}
