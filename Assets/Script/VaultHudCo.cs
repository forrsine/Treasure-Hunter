using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 金库常驻 HUD。
/// 把这个脚本挂到 Canvas 或任意 UI 管理物体上，再在 Inspector 里拖入积分和轮次 Text。
/// 
/// 新手阅读顺序：
/// 1. OnEnable 订阅 BoxCo 的事件，金库数值变化时自动刷新。
/// 2. ResolveVault 找到场景里的金库对象。
/// 3. Refresh 根据金库的分数/轮次更新 Text。
/// 4. FormatValue 负责把数字塞进类似 "Score: {0}" 的格式字符串里。
/// </summary>
[DisallowMultipleComponent]
public class VaultHudCo : MonoBehaviour
{
    /// <summary>
    /// 控制积分文本显示哪一种数值。
    /// </summary>
    public enum ScoreDisplayMode
    {
        /// <summary>
        /// 显示 BoxCo.Score，包含当前金库受伤进度换算出的积分。
        /// </summary>
        VaultScore,

        /// <summary>
        /// 显示金库被击破次数。按策划案“击破一次得 1 分”时用这个模式。
        /// </summary>
        DestroyedCount
    }

    [Header("References")]
    [Tooltip("可不填。为空时会自动使用场景中的 BoxCo.instance。")]
    [SerializeField] private BoxCo vault;

    // 显示分数的 Text，比如屏幕右上角的 "Score: 120"。
    [Tooltip("显示积分的 Text 组件。")]
    [SerializeField] private Text scoreText;

    // 显示当前轮次的 Text，比如 "Round: 3"。
    [Tooltip("显示轮次的 Text 组件。")]
    [SerializeField] private Text roundText;

    [Header("Display")]
    [Tooltip("选择积分文本显示累计积分，还是显示金库击破次数。")]
    [SerializeField] private ScoreDisplayMode scoreDisplayMode = ScoreDisplayMode.VaultScore;

    [Tooltip("轮次偏移。填 1 时，0 次击破显示第 1 轮。")]
    [SerializeField] private int roundOffset = 1;

    [Tooltip("积分显示格式，{0} 会被替换成实际数值。例：积分：{0}")]
    [SerializeField] private string scoreFormat = "Score: {0}";

    [Tooltip("轮次显示格式，{0} 会被替换成实际数值。例：第 {0} 轮")]
    [SerializeField] private string roundFormat = "Round: {0}";

    [Tooltip("一般不需要勾选。只有外部脚本没有触发金库事件时，才用每帧刷新兜底。")]
    [SerializeField] private bool refreshEveryFrame;

    // 记录上一次显示的值，避免每帧重复写 Text。
    private int lastScore = int.MinValue;
    private int lastRound = int.MinValue;

    /// <summary>
    /// 启用时订阅金库事件，并立即刷新一次 HUD。
    /// </summary>
    private void OnEnable()
    {
        // BoxCo 在受伤、击破、重生、重置时会广播事件，HUD 只在数值变化时刷新。
        BoxCo.OnVaultStatsChanged += HandleVaultChanged;
        BoxCo.OnVaultDestroyed += HandleVaultChanged;

        // 启用时立刻找一次金库并刷新，避免等到下一次事件才显示。
        ResolveVault();
        Refresh(true);
    }

    /// <summary>
    /// 禁用时取消事件订阅，避免对象销毁后还被回调。
    /// </summary>
    private void OnDisable()
    {
        BoxCo.OnVaultStatsChanged -= HandleVaultChanged;
        BoxCo.OnVaultDestroyed -= HandleVaultChanged;
    }

    /// <summary>
    /// 可选兜底刷新入口；默认依赖事件刷新。
    /// </summary>
    private void Update()
    {
        // 正常情况下事件刷新就够了；这个开关只是兜底。
        if (refreshEveryFrame)
        {
            Refresh(false);
        }
    }

    /// <summary>
    /// 手动指定这个 HUD 要监听哪一个金库。
    /// 如果场景以后有多个金库，可以用这个方法切换目标。
    /// </summary>
    public void SetVault(BoxCo newVault)
    {
        vault = newVault;
        Refresh(true);
    }

    /// <summary>
    /// 给按钮、调试脚本或外部流程手动刷新 HUD 用。
    /// </summary>
    public void RefreshNow()
    {
        ResolveVault();
        Refresh(true);
    }

    /// <summary>
    /// 金库广播数值变化时刷新本 HUD。
    /// </summary>
    private void HandleVaultChanged(BoxCo changedVault)
    {
        // 如果当前还没有绑定金库，或者事件来自当前金库，就刷新。
        // 如果以后有多个金库，这里可以避免别的金库事件影响本 HUD。
        if (vault == null || vault == changedVault)
        {
            vault = changedVault;
            Refresh(true);
        }
    }

    /// <summary>
    /// 自动寻找当前场景里的金库对象。
    /// </summary>
    private void ResolveVault()
    {
        if (vault != null)
        {
            return;
        }

        // BoxCo.instance 是最快路径；如果它还没准备好，再用 FindObjectOfType 兜底查找。
        if (BoxCo.instance != null)
        {
            vault = BoxCo.instance;
            return;
        }

        vault = FindObjectOfType<BoxCo>();
    }

    /// <summary>
    /// 根据金库分数和击破次数更新显示文本。
    /// </summary>
    private void Refresh(bool force)
    {
        if (vault == null)
        {
            ResolveVault();
        }

        if (vault == null)
        {
            // 场景里暂时没有金库时，也给 UI 一个稳定的默认值，避免空引用。
            SetText(scoreText, FormatValue(scoreFormat, 0));
            SetText(roundText, FormatValue(roundFormat, Mathf.Max(0, roundOffset)));
            return;
        }

        int scoreValue = scoreDisplayMode == ScoreDisplayMode.DestroyedCount
            ? vault.DestroyedCount
            : vault.Score;
        int roundValue = vault.DestroyedCount + roundOffset;

        // force 为 true 时强制写入；否则只有数字变化才改 Text，减少不必要 UI 刷新。
        if (force || scoreValue != lastScore)
        {
            SetText(scoreText, FormatValue(scoreFormat, scoreValue));
            lastScore = scoreValue;
        }

        if (force || roundValue != lastRound)
        {
            SetText(roundText, FormatValue(roundFormat, roundValue));
            lastRound = roundValue;
        }
    }

    /// <summary>
    /// 安全写入 Text，允许引用为空。
    /// </summary>
    private void SetText(Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }

    /// <summary>
    /// 把数值套进格式字符串，格式错误时退回纯数字。
    /// </summary>
    private string FormatValue(string format, int value)
    {
        if (string.IsNullOrEmpty(format))
        {
            return value.ToString();
        }

        try
        {
            // 例如 format = "积分：{0}"，value = 100，结果就是 "积分：100"。
            return string.Format(format, value);
        }
        catch (FormatException)
        {
            // 如果格式字符串写错，比如少了大括号，就退回只显示数字，避免游戏报错中断。
            return value.ToString();
        }
    }
}
