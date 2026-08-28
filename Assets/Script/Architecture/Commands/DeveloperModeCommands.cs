using QFramework;

/// <summary>切换开发者高攻击状态，返回切换后的值。</summary>
public sealed class ToggleDeveloperHighAttackCommand : AbstractCommand<bool>
{
    protected override bool OnExecute() => this.GetSystem<DeveloperModeSystem>().ToggleHighAttack();
}

/// <summary>切换开发者无敌状态，返回切换后的值。</summary>
public sealed class ToggleDeveloperInvincibilityCommand : AbstractCommand<bool>
{
    protected override bool OnExecute() => this.GetSystem<DeveloperModeSystem>().ToggleInvincibility();
}

/// <summary>切换开发者技能零冷却状态，返回切换后的值。</summary>
public sealed class ToggleDeveloperZeroCooldownCommand : AbstractCommand<bool>
{
    protected override bool OnExecute() => this.GetSystem<DeveloperModeSystem>().ToggleZeroCooldown();
}

/// <summary>关闭开发者模式或销毁玩家时，统一清除所有临时作弊效果。</summary>
public sealed class ResetDeveloperModeCommand : AbstractCommand
{
    protected override void OnExecute() => this.GetSystem<DeveloperModeSystem>().ResetTemporaryEffects();
}
