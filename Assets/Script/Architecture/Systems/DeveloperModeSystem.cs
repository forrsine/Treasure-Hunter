using QFramework;
using UnityEngine;

/// <summary>
/// 开发者模式规则系统：集中处理临时作弊状态，正式战斗系统只读取结果。
/// 这样调试入口不会直接篡改角色基础属性，也不会污染装备和成长计算。
/// </summary>
public sealed class DeveloperModeSystem : AbstractSystem
{
    public const int HighAttackBonus = 10_000;

    private DeveloperModeModel model;

    protected override void OnInit()
    {
        model = this.GetModel<DeveloperModeModel>();
    }

    public bool ToggleHighAttack()
    {
        return model.ToggleHighAttack();
    }

    public bool ToggleInvincibility()
    {
        return model.ToggleInvincibility();
    }

    public bool ToggleZeroCooldown()
    {
        bool enabled = model.ToggleZeroCooldown();
        if (enabled)
        {
            // 开启零冷却时先清掉已经存在的 CD，保证热键按下后立即生效。
            this.GetSystem<PlayerSkillSystem>().ClearAllCooldownsForDevelopment();
        }

        return enabled;
    }

    /// <summary>
    /// 计算开发者模式下的实际攻击力。只在伤害结算时叠加，不写回 PlayerRuntimeStats。
    /// </summary>
    public int GetEffectiveAttackPower(int baseAttackPower)
    {
        int safeBaseAttack = Mathf.Max(1, baseAttackPower);
        if (!model.HighAttackEnabled)
        {
            return safeBaseAttack;
        }

        long boostedAttack = (long)safeBaseAttack + HighAttackBonus;
        return boostedAttack >= int.MaxValue ? int.MaxValue : (int)boostedAttack;
    }

    public void ResetTemporaryEffects()
    {
        model.Reset();
    }
}
