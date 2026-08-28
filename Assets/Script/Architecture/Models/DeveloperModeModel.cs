using QFramework;

/// <summary>
/// 开发者模式只读快照：给调试表现层读取当前临时开关，不暴露 Model 的写入口。
/// </summary>
public readonly struct DeveloperModeStateSnapshot
{
    public DeveloperModeStateSnapshot(
        bool highAttackEnabled,
        bool invincibilityEnabled,
        bool zeroCooldownEnabled)
    {
        HighAttackEnabled = highAttackEnabled;
        InvincibilityEnabled = invincibilityEnabled;
        ZeroCooldownEnabled = zeroCooldownEnabled;
    }

    public bool HighAttackEnabled { get; }
    public bool InvincibilityEnabled { get; }
    public bool ZeroCooldownEnabled { get; }
}

/// <summary>
/// 开发者模式运行时数据：只保存高攻、无敌和零冷却三个临时状态。
/// 这些字段不会进入角色存档，关闭开发者模式或玩家对象销毁时会统一清空。
/// </summary>
public sealed class DeveloperModeModel : AbstractModel
{
    public bool HighAttackEnabled { get; private set; }
    public bool InvincibilityEnabled { get; private set; }
    public bool ZeroCooldownEnabled { get; private set; }

    protected override void OnInit()
    {
        Reset();
    }

    internal bool ToggleHighAttack()
    {
        HighAttackEnabled = !HighAttackEnabled;
        return HighAttackEnabled;
    }

    internal bool ToggleInvincibility()
    {
        InvincibilityEnabled = !InvincibilityEnabled;
        return InvincibilityEnabled;
    }

    internal bool ToggleZeroCooldown()
    {
        ZeroCooldownEnabled = !ZeroCooldownEnabled;
        return ZeroCooldownEnabled;
    }

    internal void Reset()
    {
        HighAttackEnabled = false;
        InvincibilityEnabled = false;
        ZeroCooldownEnabled = false;
    }

    public DeveloperModeStateSnapshot CreateSnapshot()
    {
        return new DeveloperModeStateSnapshot(
            HighAttackEnabled,
            InvincibilityEnabled,
            ZeroCooldownEnabled);
    }
}
