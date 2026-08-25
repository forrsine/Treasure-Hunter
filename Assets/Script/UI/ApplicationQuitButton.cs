using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 应用退出按钮：用于登录等没有存档写入任务的界面，点击后直接退出到桌面。
/// 编辑器中不会关闭 Unity，而是停止播放，方便重复测试按钮行为。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class ApplicationQuitButton : MonoBehaviour
{
    [SerializeField] private Button exitButton;

    private void Awake()
    {
        CacheButton();
    }

    /// <summary>
    /// 按钮启用时注册点击事件，确保界面重复显示时不会累积监听。
    /// </summary>
    private void OnEnable()
    {
        CacheButton();
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(QuitToDesktop);
        }
    }

    private void OnDisable()
    {
        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(QuitToDesktop);
        }
    }

    /// <summary>
    /// 登录阶段没有进行中的角色存档，因此可以直接结束应用。
    /// </summary>
    public void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CacheButton()
    {
        if (exitButton == null)
        {
            exitButton = GetComponent<Button>();
        }
    }
}
