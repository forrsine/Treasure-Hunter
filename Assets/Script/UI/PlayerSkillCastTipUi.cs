using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 技能释放提示 UI：监听技能释放失败事件，并在屏幕上显示短提示。
/// 注意：这个脚本只负责 UI 显示，不判断技能能不能释放。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillCastTipUi : MonoBehaviour, IController
{
    [Header("UI References")]
    [SerializeField] private Text tipText;

    [Header("Display Settings")]
    [SerializeField] private float showDuration = 1.2f;
    [SerializeField] private Color tipColor = new Color(1f, 0.9f, 0.25f, 1f);

    private float hideTimer;

    public IArchitecture GetArchitecture()
    {
        return TreasureHunterArchitecture.Interface;
    }

    /// <summary>
    /// UI 启用时注册事件。
    /// QFramework 事件适合用来做“系统通知 UI 刷新”这种解耦通信。
    /// </summary>
    private void OnEnable()
    {
        this.RegisterEvent<PlayerSkillCastFailedEvent>(OnSkillCastFailed);
    }

    /// <summary>
    /// UI 禁用时注销事件，避免对象销毁后还收到事件导致报错。
    /// </summary>
    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerSkillCastFailedEvent>(OnSkillCastFailed);
    }

    private void Start()
    {
        HideTip();
    }

    private void Update()
    {
        if (tipText == null || !tipText.enabled)
        {
            return;
        }

        hideTimer -= Time.unscaledDeltaTime;
        if (hideTimer <= 0f)
        {
            HideTip();
        }
    }

    /// <summary>
    /// 收到技能释放失败事件后显示提示。
    /// 这里不关心失败原因怎么来的，只负责把 message 显示出来。
    /// </summary>
    private void OnSkillCastFailed(PlayerSkillCastFailedEvent e)
    {
        ShowTip(e.Message);
    }

    private void ShowTip(string message)
    {
        if (tipText == null)
        {
            Debug.LogWarning("PlayerSkillCastTipUi 没有绑定 tipText。", this);
            return;
        }

        tipText.text = message;
        tipText.color = tipColor;
        tipText.enabled = true;
        // 使用 unscaledDeltaTime 计时，这样以后游戏暂停时提示也能正常消失。
        hideTimer = showDuration;
    }

    private void HideTip()
    {
        if (tipText != null)
        {
            tipText.enabled = false;
        }
    }
}