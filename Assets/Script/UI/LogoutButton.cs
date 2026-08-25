using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 登出按钮：清理客户端登录状态、当前选角数据并返回登录场景。
/// </summary>
public class LogoutButton : MonoBehaviour
{
    [SerializeField] private Button logoutButton;
    [SerializeField] private GameApiClient apiClient;

    private void Awake()
    {
        if (logoutButton == null)
        {
            logoutButton = GetComponent<Button>();
        }

        if (apiClient == null)
        {
            apiClient = SceneFlowService.GetOrCreateApiClient();
        }

        if (logoutButton != null)
        {
            logoutButton.onClick.AddListener(Logout);
        }
    }

    private void Logout()
    {
        StartCoroutine(SaveAndLogout());
    }

    private IEnumerator SaveAndLogout()
    {
        CharacterProgressSaveService saveService = CharacterProgressSaveService.Instance;
        if (saveService != null && saveService.IsSessionActive)
        {
            bool success = false;
            string message = "";
            yield return saveService.FlushAndLeave(
                CharacterProgressSaveMode.Normal,
                (result, resultMessage) =>
                {
                    success = result;
                    message = resultMessage;
                });

            if (!success)
            {
                Debug.LogWarning(string.IsNullOrEmpty(message) ? "角色存档保存失败，已取消退出登录。" : message);
                yield break;
            }
        }

        SceneFlowService.LogoutToLogin(apiClient);
    }
}
