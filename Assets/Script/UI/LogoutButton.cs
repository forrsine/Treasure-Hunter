using UnityEngine;
using UnityEngine.UI;

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
