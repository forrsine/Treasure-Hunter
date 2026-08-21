using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家普通攻击伤害结算器。
/// 近战攻击盒和远程投射物都通过这里处理攻击力、暴击、飘字和吸血，
/// 避免不同命中入口各自维护一套容易产生差异的伤害公式。
/// </summary>
public static class PlayerBasicAttackDamageResolver
{
    /// <summary>
    /// 尝试对碰撞目标结算一次普通攻击。
    /// 返回 true 表示碰到了可受击目标，单体投射物可以结束飞行并回到对象池。
    /// </summary>
    public static bool TryApply(PlayerCombatComponent combat, Collider other)
    {
        if (!TryGetTarget(other, out FighterInterface fighterInterface))
        {
            return false;
        }

        Apply(combat, other, fighterInterface);
        return combat != null;
    }

    /// <summary>
    /// 范围普通攻击入口：先处理直接碰撞目标，再扫描爆炸范围，并按 FighterInterface 去重。
    /// 同一个敌人即使拥有多个 Collider，一次爆炸也只会进入一次公共伤害结算。
    /// </summary>
    internal static int ApplyInRadius(
        PlayerCombatComponent combat,
        Vector3 center,
        float radius,
        Transform ownerTransform,
        Collider directHit,
        Collider[] overlapBuffer,
        HashSet<FighterInterface> hitTargets)
    {
        if (combat == null || radius <= 0f || overlapBuffer == null || hitTargets == null)
        {
            return 0;
        }

        hitTargets.Clear();
        TryApplyUnique(combat, directHit, ownerTransform, hitTargets);

        int overlapCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            overlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlapCount; i++)
        {
            TryApplyUnique(combat, overlapBuffer[i], ownerTransform, hitTargets);
            overlapBuffer[i] = null;
        }

        return hitTargets.Count;
    }

    internal static bool TryGetTarget(Collider other, out FighterInterface fighterInterface)
    {
        fighterInterface = null;
        if (other == null)
        {
            return false;
        }

        fighterInterface =
            other.GetComponent<FighterInterface>() ??
            other.GetComponentInParent<FighterInterface>();
        return fighterInterface != null;
    }

    private static void TryApplyUnique(
        PlayerCombatComponent combat,
        Collider other,
        Transform ownerTransform,
        HashSet<FighterInterface> hitTargets)
    {
        if (other == null ||
            (ownerTransform != null &&
             (other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform))) ||
            !TryGetTarget(other, out FighterInterface fighterInterface) ||
            !hitTargets.Add(fighterInterface))
        {
            return;
        }

        Apply(combat, other, fighterInterface);
    }

    /// <summary>
    /// 使用公共战斗系统生成本次伤害，并把实际伤害交给吸血等后结算逻辑。
    /// </summary>
    internal static void Apply(
        PlayerCombatComponent combat,
        Collider other,
        FighterInterface fighterInterface)
    {
        if (combat == null || other == null || fighterInterface == null)
        {
            return;
        }

        bool isCritical;
        int damage = combat.RollAttackDamage(out isCritical);

        Component fighterComponent = fighterInterface as Component;
        Transform feedbackTarget = fighterComponent != null ? fighterComponent.transform : other.transform;
        int appliedDamage = damage;
        bool shouldShowDamageText = true;

        // 正式目标记录受击前生命，用于限制过量伤害和吸血数值。
        SlimeCo slime = other.GetComponentInParent<SlimeCo>();
        if (slime != null)
        {
            feedbackTarget = slime.transform;
            int targetHpBeforeHit = Mathf.Max(0, slime.Hp);
            appliedDamage = Mathf.Min(damage, targetHpBeforeHit);
            shouldShowDamageText = targetHpBeforeHit > 0;
        }
        else
        {
            BoxCo vault = other.GetComponentInParent<BoxCo>();
            if (vault != null)
            {
                feedbackTarget = vault.transform;
                int targetHpBeforeHit = Mathf.Max(0, vault.CurrentHp);
                bool canTakeDamage =
                    !vault.IsInvincible &&
                    !vault.IsRespawning &&
                    targetHpBeforeHit > 0;
                appliedDamage = canTakeDamage ? Mathf.Min(damage, targetHpBeforeHit) : 0;
                shouldShowDamageText = canTakeDamage;
            }
            else
            {
                SpiderKingBossController boss = other.GetComponentInParent<SpiderKingBossController>();
                if (boss != null)
                {
                    feedbackTarget = boss.transform;
                    int targetHpBeforeHit = Mathf.Max(0, boss.CurrentHp);
                    shouldShowDamageText = !boss.IsDead && targetHpBeforeHit > 0;
                    appliedDamage = shouldShowDamageText ? Mathf.Min(damage, targetHpBeforeHit) : 0;
                }
            }
        }

        fighterInterface.Hit(damage);

        if (shouldShowDamageText)
        {
            FloatingCombatText.ShowDamage(feedbackTarget, other, appliedDamage, isCritical);
        }

        combat.HandleDamageDealt(appliedDamage);
    }
}
