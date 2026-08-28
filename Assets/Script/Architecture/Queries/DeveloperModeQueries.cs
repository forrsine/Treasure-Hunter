using QFramework;

/// <summary>读取开发者模式临时状态。</summary>
public sealed class GetDeveloperModeStateQuery : AbstractQuery<DeveloperModeStateSnapshot>
{
    protected override DeveloperModeStateSnapshot OnDo()
    {
        return this.GetModel<DeveloperModeModel>().CreateSnapshot();
    }
}

/// <summary>
/// 获取当前攻击结算应使用的攻击力，供普通攻击和技能共享同一套开发者加成。
/// </summary>
public sealed class GetEffectivePlayerAttackPowerQuery : AbstractQuery<int>
{
    protected override int OnDo()
    {
        int baseAttackPower = this.GetModel<PlayerModel>().Stats.AttackPower;
        return this.GetSystem<DeveloperModeSystem>().GetEffectiveAttackPower(baseAttackPower);
    }
}
