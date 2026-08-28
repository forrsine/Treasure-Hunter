using QFramework;
using UnityEngine;

/// <summary>
/// 玩家开发者模式表现与输入入口：F1 开关模式，F2-F8 执行调试操作。
/// 核心规则仍由 Model/System/Command 负责，这个组件只读取输入、显示状态和调用正式业务入口。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDeveloperModeComponent : MonoBehaviour, IController
{
    private const long QuickGoldAmount = 10_000L;
    private const int QuickLevelCount = 1;

    private PlayerRuntimeController runtimeController;
    private bool isDeveloperModeEnabled;
    private GUIStyle hintStyle;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 由玩家运行时控制器完成依赖注入，避免该组件主动查找玩家对象。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        runtimeController = player;
    }

    /// <summary>
    /// 每帧处理一次开发者快捷键。关闭 F1 后所有临时作弊状态都会恢复默认值，
    /// 但已经通过正式系统增加的金币、等级和宝箱进度仍按正常规则保存。
    /// </summary>
    public void Tick()
    {
        if (!IsDevelopmentEnvironment())
        {
            DisableDeveloperMode();
            return;
        }

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || runtimeController == null)
        {
            return;
        }

        if (input.DeveloperModeToggleDown)
        {
            if (isDeveloperModeEnabled)
            {
                DisableDeveloperMode();
                Debug.Log("[开发者模式] 已关闭，临时战斗效果已清除。");
            }
            else
            {
                this.SendCommand(new ResetDeveloperModeCommand());
                isDeveloperModeEnabled = true;
                Debug.Log("[开发者模式] 已开启：F2 高攻，F3 无敌，F4 金币，F5 宝箱，F6 升级，F7 满蓝，F8 零冷却。");
            }
        }

        if (!isDeveloperModeEnabled)
        {
            return;
        }

        if (input.DebugHighAttackToggleDown)
        {
            bool enabled = this.SendCommand(new ToggleDeveloperHighAttackCommand());
            Debug.Log($"[开发者模式] 极高攻击：{FormatLogState(enabled)}，额外攻击 +{DeveloperModeSystem.HighAttackBonus:N0}。");
        }

        if (input.DebugInvincibilityToggleDown)
        {
            bool enabled = this.SendCommand(new ToggleDeveloperInvincibilityCommand());
            Debug.Log($"[开发者模式] 无敌：{FormatLogState(enabled)}。");
        }

        if (input.DebugAddGoldDown)
        {
            long addedGold = this.SendCommand(new AddGoldCommand(QuickGoldAmount));
            Debug.Log($"[开发者模式] 实际增加金币 {addedGold:N0}，当前金币 {this.SendQuery(new GetGoldQuery()):N0}。");
        }

        if (input.DebugCompleteVaultCycleDown)
        {
            CompleteCurrentVaultCycle();
        }

        if (input.DebugAddLevelDown)
        {
            int actualLevelCount = this.SendCommand(
                new AddPlayerLevelsForDevelopmentCommand(QuickLevelCount));
            Debug.Log($"[开发者模式] 实际增加 {actualLevelCount} 级，属性选择已跳过。");
        }

        if (input.DebugRestoreManaDown)
        {
            int restoredMana = runtimeController.FullRestoreMana();
            Debug.Log($"[开发者模式] 已回满蓝，实际恢复 {restoredMana} 点。");
        }

        if (input.DebugZeroCooldownToggleDown)
        {
            bool enabled = this.SendCommand(new ToggleDeveloperZeroCooldownCommand());
            Debug.Log($"[开发者模式] 技能零冷却：{FormatLogState(enabled)}，魔法消耗保持正常。");
        }
    }

    /// <summary>
    /// 开发者提示继续使用 IMGUI，因为它不依赖 GameplayUiRoot，不会覆盖背包和商店的手调布局。
    /// </summary>
    private void OnGUI()
    {
        if (!isDeveloperModeEnabled || !IsDevelopmentEnvironment() || runtimeController == null)
        {
            return;
        }

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                richText = true,
                wordWrap = false,
                padding = new RectOffset(12, 12, 10, 10),
                normal = { textColor = new Color(1f, 0.9f, 0.25f, 1f) }
            };
        }

        DeveloperModeStateSnapshot state = this.SendQuery(new GetDeveloperModeStateQuery());
        IPlayerStatsReadOnly stats = runtimeController.Stats;
        int vaultsPerBoss = Mathf.Max(1, BossRunProgressState.VaultsPerBoss);
        int vaultsRemaining = Mathf.Clamp(BossRunProgressState.VaultsUntilNextBoss, 0, vaultsPerBoss);
        int currentCycleProgress = vaultsPerBoss - vaultsRemaining;
        int effectiveAttack = this.SendQuery(new GetEffectivePlayerAttackPowerQuery());
        long gold = this.SendQuery(new GetGoldQuery());

        string text =
            "<b>开发者模式已开启（F1 关闭）</b>\n" +
            $"F2  极高攻击：{FormatGuiState(state.HighAttackEnabled)}  " +
            $"F3  无敌：{FormatGuiState(state.InvincibilityEnabled)}\n" +
            $"F4  +{QuickGoldAmount:N0} 金币  F5  补足本轮宝箱  F6  +1 级\n" +
            $"F7  回满蓝  F8  技能 0CD：{FormatGuiState(state.ZeroCooldownEnabled)}\n" +
            $"等级 {stats.Level}  攻击 {effectiveAttack:N0}  蓝量 {stats.CurrentMp}/{stats.MaxMp}\n" +
            $"金币 {gold:N0}  本轮宝箱 {currentCycleProgress}/{vaultsPerBoss}";

        GUI.Label(new Rect(12f, 12f, 570f, 158f), text, hintStyle);
    }

    /// <summary>
    /// 只补足当前 Boss 轮剩余的宝箱次数，不固定额外增加五次，避免把下一轮进度提前写入存档。
    /// </summary>
    private void CompleteCurrentVaultCycle()
    {
        int remainingBreakCount = BossRunProgressState.VaultsUntilNextBoss;
        if (remainingBreakCount <= 0)
        {
            Debug.Log("[开发者模式] 当前轮宝箱进度已经满足，Boss 入口正在或已经解锁。");
            return;
        }

        BoxCo vault = BoxCo.instance != null && BoxCo.instance.gameObject.activeInHierarchy
            ? BoxCo.instance
            : FindObjectOfType<BoxCo>();

        if (vault == null)
        {
            Debug.LogWarning("[开发者模式] 当前场景没有找到可击破的宝箱。");
            return;
        }

        int completedBreakCount = vault.BreakRepeatedlyForDevelopment(remainingBreakCount);
        if (completedBreakCount <= 0)
        {
            Debug.LogWarning("[开发者模式] 宝箱当前不可击破，请确认宝箱对象已启用。");
            return;
        }

        Debug.Log(
            $"[开发者模式] 已正式结算 {completedBreakCount} 次宝箱击破，" +
            $"本轮剩余 {BossRunProgressState.VaultsUntilNextBoss} 次。");
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            DisableDeveloperMode();
        }
    }

    private void DisableDeveloperMode()
    {
        if (!isDeveloperModeEnabled)
        {
            return;
        }

        this.SendCommand(new ResetDeveloperModeCommand());
        isDeveloperModeEnabled = false;
    }

    private static string FormatGuiState(bool enabled)
    {
        return enabled
            ? "<color=#7CFC72>开启</color>"
            : "<color=#FF8A80>关闭</color>";
    }

    private static string FormatLogState(bool enabled) => enabled ? "开启" : "关闭";

    private static bool IsDevelopmentEnvironment()
    {
#if UNITY_STANDALONE
        // 求职演示使用的 PC 包保留 F1 调试入口，延续项目原有约定。
        return true;
#else
        return Application.isEditor || Debug.isDebugBuild;
#endif
    }
}
