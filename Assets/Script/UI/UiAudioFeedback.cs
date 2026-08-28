using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用按钮点击音效组件。
/// 监听 Button.onClick 能同时覆盖鼠标、键盘和手柄提交，不把音频调用散落到每个面板脚本。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class UiAudioFeedback : MonoBehaviour
{
    [SerializeField] private GameSfxId clickCue = GameSfxId.UiClick;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.RemoveListener(PlayClick);
        button.onClick.AddListener(PlayClick);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClick);
        }
    }

    private void PlayClick()
    {
        GameAudioService.Play2D(clickCue);
    }
}
