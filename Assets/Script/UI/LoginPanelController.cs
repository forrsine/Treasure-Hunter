using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LoginPanelController : MonoBehaviour
{
    [SerializeField] private GameApiClient apiClient;
    [SerializeField] private InputField usernameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Text messageText;

    private void Awake()
    {
        if (apiClient == null)
        {
            apiClient = SceneFlowService.GetOrCreateApiClient();
        }

        loginButton.onClick.AddListener(Login);
        registerButton.onClick.AddListener(Register);
        StartCoroutine(TryAutoLogin());
    }

    private void Login()
    {
        if (apiClient == null)
        {
            SetMessage("网络客户端未初始化。");
            return;
        }

        if (!TryReadInputs(out string username, out string password))
        {
            return;
        }

        SetMessage("正在登录...");
        SetButtonsInteractable(false);

        StartCoroutine(apiClient.Login(username, password, (success, message) =>
        {
            SetButtonsInteractable(true);
            SetMessage(string.IsNullOrEmpty(message) ? "登录失败。" : message);

            if (success)
            {
                SceneFlowService.LoadCharacterSelectScene();
            }
        }));
    }

    private void Register()
    {
        if (apiClient == null)
        {
            SetMessage("网络客户端未初始化。");
            return;
        }

        if (!TryReadInputs(out string username, out string password))
        {
            return;
        }

        if (username.Length < 3 || username.Length > 32)
        {
            SetMessage("账号长度必须是 3-32 个字符。");
            return;
        }

        if (password.Length < 6 || password.Length > 64)
        {
            SetMessage("密码长度必须是 6-64 个字符。");
            return;
        }

        SetMessage("正在注册...");
        SetButtonsInteractable(false);

        StartCoroutine(apiClient.Register(username, password, (success, message) =>
        {
            SetButtonsInteractable(true);
            SetMessage(string.IsNullOrEmpty(message) ? "注册失败。" : message);
        }));
    }

    private bool TryReadInputs(out string username, out string password)
    {
        username = usernameInput != null ? usernameInput.text.Trim() : "";
        password = passwordInput != null ? passwordInput.text : "";

        if (string.IsNullOrWhiteSpace(username))
        {
            SetMessage("请输入账号。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetMessage("请输入密码。");
            return false;
        }

        return true;
    }

    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null)
        {
            loginButton.interactable = interactable;
        }

        if (registerButton != null)
        {
            registerButton.interactable = interactable;
        }
    }

    private IEnumerator TryAutoLogin()
    {
        if (apiClient == null || !apiClient.IsLoggedIn)
        {
            yield break;
        }

        SetMessage("正在自动登录...");
        SetButtonsInteractable(false);

        yield return apiClient.ValidateSession((success, message) =>
        {
            if (success)
            {
                SetMessage(message);
                SceneFlowService.LoadCharacterSelectScene();
                return;
            }

            SetMessage(string.IsNullOrEmpty(message) ? "请先登录。" : message);
            SetButtonsInteractable(true);
        });
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
