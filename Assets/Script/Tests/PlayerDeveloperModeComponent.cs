using QFramework;
using UnityEngine;

/// <summary>
/// 玩家开发者模式：集中管理只用于本地验证的快捷键，不把测试逻辑混入正式玩法规则。
/// 正式发布包会拒绝开启；Unity 编辑器和 Development Build 中才可以使用。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerDeveloperModeComponent : MonoBehaviour, IController
{
    private const int QuickLevelCount = 15;
    private const int QuickExpAmount = 100;

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
    /// 每帧处理一次开发者快捷键。
    /// F1 负责开关；关闭时 L、P、O 都不会修改玩家数据。
    /// </summary>
    public void Tick()
    {
        if (!IsDevelopmentEnvironment())
        {
            isDeveloperModeEnabled = false;
            return;
        }

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || runtimeController == null)
        {
            return;
        }

        if (input.DeveloperModeToggleDown)
        {
            isDeveloperModeEnabled = !isDeveloperModeEnabled;
            Debug.Log(isDeveloperModeEnabled
                ? "[开发者模式] 已开启：L 增加 15 级，P 增加 100 经验，O 回满蓝，N 击破一次宝箱。"
                : "[开发者模式] 已关闭。");
        }

        if (!isDeveloperModeEnabled)
        {
            return;
        }

        if (input.DebugAddLevelsDown)
        {
            int actualLevelCount = this.SendCommand(
                new AddPlayerLevelsForDevelopmentCommand(QuickLevelCount));
            Debug.Log($"[开发者模式] 实际增加 {actualLevelCount} 级，属性选择已跳过。");
        }

        if (input.DebugAddExpDown)
        {
            runtimeController.AddExp(QuickExpAmount);
        }

        if (input.DebugRestoreManaDown)
        {
            int restoredMana = runtimeController.FullRestoreMana();
            Debug.Log($"[开发者模式] 已回满蓝，实际恢复 {restoredMana} 点。");
        }

        if (input.DebugBreakVaultDown)
        {
            BreakVaultOnce();
        }
    }

    /// <summary>
    /// 开发者提示使用 IMGUI，是因为它只服务调试，不需要依赖正式 HUD Prefab。
    /// </summary>
    private void OnGUI()
    {
        if (!isDeveloperModeEnabled || !IsDevelopmentEnvironment())
        {
            return;
        }

        if (hintStyle == null)
        {
            hintStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 16,
                normal = { textColor = Color.yellow }
            };
        }

        GUI.Label(
            new Rect(12f, 12f, 420f, 58f),
            "开发者模式已开启\nL：增加15级  P：+100经验  O：回满蓝  N：击破一次宝箱",
            hintStyle);
    }

    /// <summary>
    /// 开发者快速击破宝箱。
    /// 这里仍然调用 BoxCo 的正式击破流程，不直接改 Boss 入口次数，避免测试逻辑绕过真实玩法事件。
    /// </summary>
    private void BreakVaultOnce()
    {
        BoxCo vault = BoxCo.instance != null && BoxCo.instance.gameObject.activeInHierarchy
            ? BoxCo.instance
            : FindObjectOfType<BoxCo>();

        if (vault == null)
        {
            Debug.LogWarning("[开发者模式] 场景中没有找到可击破的宝箱。");
            return;
        }

        if (!vault.BreakOnceForDevelopment())
        {
            Debug.LogWarning("[开发者模式] 宝箱当前正在重生或不可击破，请稍后再试。");
            return;
        }

        Debug.Log($"[开发者模式] 已快速击破一次宝箱，当前累计击破次数：{vault.DestroyedCount}。");
    }

    private static bool IsDevelopmentEnvironment()
    {
        return Application.isEditor || Debug.isDebugBuild;
    }
}
