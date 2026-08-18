using UnityEngine;
using UnityEngine.UI;

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
        SceneFlowService.LogoutToLogin(apiClient);
    }
}
