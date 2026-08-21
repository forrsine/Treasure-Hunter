using UnityEngine;

/// <summary>
/// 动画事件转发器：Animator 在职业模型子物体上，本组件把事件转发给父物体的独立功能组件。
/// 它不保存战斗规则，也不依赖某个巨型玩家控制脚本。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAnimationEventRelay : MonoBehaviour
{
    private PlayerCombatComponent combat;
    private PlayerAudioComponent audioComponent;

    private void Awake()
    {
        CacheTargets();
    }

    public void Initialize(PlayerRuntimeController runtimeController)
    {
        combat = runtimeController != null
            ? runtimeController.GetComponent<PlayerCombatComponent>()
            : GetComponentInParent<PlayerCombatComponent>();
        audioComponent = runtimeController != null
            ? runtimeController.Audio
            : GetComponentInParent<PlayerAudioComponent>();
    }

    private void CacheTargets()
    {
        combat = GetComponentInParent<PlayerCombatComponent>();
        audioComponent = GetComponentInParent<PlayerAudioComponent>();
    }

    public void OpenComboWindow() => combat?.OpenComboWindow();
    public void ResetCombo() => combat?.ResetCombo();
    public void WeaponEnable() => combat?.WeaponEnable();
    public void WeaponDisable() => combat?.WeaponDisable();
    public void EnableAtk() => WeaponEnable();
    public void DisableAtk() => WeaponDisable();

    /// <summary>
    /// 弓箭手和法师的攻击动画释放点。
    /// 事件只通知战斗组件，由战斗组件负责去重、校验攻击状态并从对象池发射投射物。
    /// </summary>
    public void shoot() => combat?.TryReleaseRangedBasicAttack();
    public void Dead() { }

    public void PlayWalkFootstepSfxEvent() => audioComponent?.PlayWalkFootstep();
    public void PlayRunFootstepSfxEvent() => audioComponent?.PlayRunFootstep();
    public void PlayJumpSfxEvent() => audioComponent?.PlayJump();
    public void PlayRollSfxEvent() => audioComponent?.PlayRoll();
    public void PlayAttack1SfxEvent() => audioComponent?.PlayAttack(1);
    public void PlayAttack2SfxEvent() => audioComponent?.PlayAttack(2);
    public void PlayAttack3SfxEvent() => audioComponent?.PlayAttack(3);
    public void PlayHitSfxEvent() => audioComponent?.PlayHit();
}
