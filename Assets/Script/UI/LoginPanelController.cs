using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 登录面板控制器：负责输入校验、按钮状态和结果提示，
/// 具体网络请求交给 GameApiClient，成功后的场景跳转交给 SceneFlowService。
/// </summary>
public class LoginPanelController : MonoBehaviour
{
    [SerializeField] private GameApiClient apiClient;
    [SerializeField] private InputField usernameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button registerButton;
    [SerializeField] private Text messageText;

    /// <summary>
    /// Awake 只做本地初始化与按钮绑定。
    /// 真正的网络请求发生在点击按钮之后，避免一进场景就自动发请求。
    /// </summary>
    private void Awake()
    {
        if (apiClient == null)
        {
            apiClient = SceneFlowService.GetOrCreateApiClient();
        }

        // Awake 时绑定一次按钮事件；面板不会跨场景保留，销毁时 Unity 会一起清理按钮监听。
        loginButton.onClick.AddListener(Login);
        registerButton.onClick.AddListener(Register);
        StartCoroutine(TryAutoLogin());
    }

    /// <summary>
    /// 点击登录按钮后的入口。
    /// 先做本地输入校验，再异步请求服务端。
    /// </summary>
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

    /// <summary>
    /// 点击注册按钮后的入口。
    /// 本地先做基础长度校验，减少无效请求打到服务端。
    /// </summary>
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

    /// <summary>
    /// 统一读取并校验输入框内容。
    /// </summary>
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
        // 当前服务端没有可验证的 Token 时 IsLoggedIn 为 false，本协程会直接结束并等待手动登录。
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

    /// <summary>
    /// 关闭登录面板。
    /// </summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }
}
