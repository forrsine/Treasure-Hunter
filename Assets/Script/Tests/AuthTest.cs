using UnityEngine;

public class AuthTest : MonoBehaviour
{
    [SerializeField] private GameApiClient apiClient;
    [SerializeField] private string username = "test001";
    [SerializeField] private string password = "123456";

    private void Start()
    {
        if (apiClient == null)
        {
            apiClient = FindObjectOfType<GameApiClient>();
        }

        StartCoroutine(apiClient.Login(username, password, (success, message) =>
        {
            Debug.Log($"登录结果：{success}, 消息：{message}");

            if (!success)
            {
                return;
            }

            StartCoroutine(apiClient.SubmitHighScore(999, (uploadOk, uploadMessage, serverHighScore) =>
            {
                Debug.Log($"上传结果：{uploadOk}, 消息：{uploadMessage}");
                Debug.Log($"服务器最高分：{serverHighScore}");
            }));
        }));
    }
}