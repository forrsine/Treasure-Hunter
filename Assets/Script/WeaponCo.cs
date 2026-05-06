using UnityEngine;

/// <summary>
/// 玩家武器碰撞盒。
/// 
/// 新手阅读顺序：
/// 1. PlayerCo 的攻击动画会在合适的帧启用/关闭武器碰撞体。
/// 2. 武器碰到任何实现 FighterInterface 的对象，就可以调用 Hit。
/// 3. 真正伤害不是直接写死在武器上，而是从 PlayerCo.RollAttackDamage 计算，
///    这样暴击、吸血、攻击力升级都能统一生效。
/// </summary>
public class WeaponCo : MonoBehaviour
{
    /// <summary>
    /// 武器碰到可受击对象时，统一走 PlayerCo 的伤害、暴击、吸血和漂浮文字流程。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // Ignore self-collisions and any hit before the player singleton is ready.
        if (other.transform.root == transform.root || PlayerCo.instance == null)
        {
            return;
        }

        FighterInterface fighterInterface =
            other.GetComponent<FighterInterface>() ??
            other.GetComponentInParent<FighterInterface>();

        // 如果碰到的东西不能被攻击，就直接忽略。
        if (fighterInterface == null)
        {
            return;
        }

        // 让 PlayerCo 统一计算本次攻击伤害，里面会处理暴击。
        // isCritical 会告诉我们这次是否暴击，后面用来决定伤害数字颜色。
        bool isCritical;
        int damage = PlayerCo.instance.RollAttackDamage(out isCritical);

        // 找到真正被打中的目标脚本，后面把漂浮文字显示在这个目标斜上方。
        Component fighterComponent = fighterInterface as Component;
        Transform feedbackTarget = fighterComponent != null ? fighterComponent.transform : other.transform;

        // appliedDamage 表示“真实扣掉了多少血”，吸血要用它计算，避免打 25 点但敌人只剩 3 血时吸太多。
        int appliedDamage = damage;
        bool shouldShowDamageText = true;

        // 记录目标受击前的生命。正式目标目前主要是史莱姆和金库。
        // 这样既能正确处理过量伤害，也能避免金库无敌时还触发吸血。
        SlimeCo slime = other.GetComponentInParent<SlimeCo>();
        int targetHpBeforeHit = 0;
        if (slime != null)
        {
            feedbackTarget = slime.transform;
            targetHpBeforeHit = Mathf.Max(0, slime.Hp);
            appliedDamage = Mathf.Min(damage, targetHpBeforeHit);
            shouldShowDamageText = targetHpBeforeHit > 0;
        }
        else
        {
            BoxCo vault = other.GetComponentInParent<BoxCo>();
            if (vault != null)
            {
                feedbackTarget = vault.transform;
                targetHpBeforeHit = Mathf.Max(0, vault.CurrentHp);

                // 金库重生/无敌时 Hit 会直接忽略，所以这里也不显示伤害、不触发吸血。
                bool vaultCanTakeDamage = !vault.IsInvincible && !vault.IsRespawning && targetHpBeforeHit > 0;
                appliedDamage = vaultCanTakeDamage ? Mathf.Min(damage, targetHpBeforeHit) : 0;
                shouldShowDamageText = vaultCanTakeDamage;
            }
        }

        // 先把伤害交给目标，让目标自己扣血、死亡、刷新血条。
        fighterInterface.Hit(damage);

        // 命中有效时显示伤害数字：普通白色，暴击深红色。
        if (shouldShowDamageText)
        {
            FloatingCombatText.ShowDamage(feedbackTarget, other, damage, isCritical);
        }

        // 再把“实际造成的伤害值”交给玩家处理吸血。
        // 回血数字现在统一显示在玩家头顶，所以这里不再把敌人位置传过去。
        PlayerCo.instance.HandleDamageDealt(appliedDamage);
    }
}
