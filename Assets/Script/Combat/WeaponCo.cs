using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家武器碰撞盒。
/// 
/// 新手阅读顺序：
/// 1. PlayerCombatComponent 会在合适的动画帧启用/关闭武器碰撞体。
/// 2. 武器碰到任何实现 FighterInterface 的对象，就可以调用 Hit。
/// 3. 真正伤害不是直接写死在武器上，而是交给 PlayerCombatSystem 计算，
///    这样暴击、吸血、攻击力升级都能统一生效。
/// </summary>
public class WeaponCo : MonoBehaviour
{
    private readonly HashSet<FighterInterface> damagedTargetsInCurrentWindow = new HashSet<FighterInterface>();
    private int currentHitWindowId = -1;

    /// <summary>
    /// 进入攻击盒时尝试造成伤害。
    /// 注意：连击时敌人可能一直待在攻击盒里，单靠 OnTriggerEnter 不一定会再次触发。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    /// <summary>
    /// 敌人持续停留在攻击盒中时也要尝试命中。
    /// 同一次攻击窗口内会做目标去重，所以不会因为 OnTriggerStay 每个物理帧都扣血。
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    /// <summary>
    /// 近战命中的统一入口：先确认攻击窗口，再确认目标是否本窗口已经受击，最后才结算伤害。
    /// </summary>
    private void TryHit(Collider other)
    {
        // 优先从武器所属层级寻找玩家，避免场景中存在多个角色时拿到错误的全局玩家。
        PlayerCombatComponent combat = GetOwnerCombat();

        // Ignore self-collisions and any hit before the current player is ready.
        if (other.transform.root == transform.root || combat == null)
        {
            return;
        }

        // 近战和远程统一使用同一个目标识别规则，避免某类敌人只会被其中一种攻击命中。
        if (!PlayerBasicAttackDamageResolver.TryGetTarget(other, out FighterInterface fighterInterface))
        {
            return;
        }

        RefreshHitWindow(combat.AttackHitWindowId);
        if (damagedTargetsInCurrentWindow.Contains(fighterInterface))
        {
            return;
        }

        damagedTargetsInCurrentWindow.Add(fighterInterface);
        PlayerBasicAttackDamageResolver.Apply(combat, other, fighterInterface);
    }

    private PlayerCombatComponent GetOwnerCombat()
    {
        PlayerCombatComponent combat = GetComponentInParent<PlayerCombatComponent>();
        if (combat == null && GameplayRuntime.Instance.CurrentPlayer != null)
        {
            combat = GameplayRuntime.Instance.CurrentPlayer.GetComponent<PlayerCombatComponent>();
        }

        return combat;
    }

    private void RefreshHitWindow(int hitWindowId)
    {
        if (currentHitWindowId == hitWindowId)
        {
            return;
        }

        currentHitWindowId = hitWindowId;
        damagedTargetsInCurrentWindow.Clear();
    }

}
