using UnityEngine;

/// <summary>
/// 战士蓄力普攻状态机：只负责蓄力流程和数值进度，不直接处理碰撞、伤害或 UI。
/// 状态拆成前摇、保持和释放，能让战斗判定与动画表现通过清晰入口协作。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerChargedAttackComponent : MonoBehaviour
{
    private enum ChargeAttackState
    {
        Inactive,
        Windup,
        Holding,
        Releasing
    }

    private PlayerCombatComponent combat;
    private PlayerPresentationComponent presentation;
    private CharacterChargedAttackDefine chargeDefine;
    private ChargeAttackState state;
    private float chargeTimer;
    private bool isFullChargeSpinActive;
    private float fullChargeSpinTimer;
    private Quaternion fullChargeSpinStartRotation;

    public bool IsChargeAttackActive => state != ChargeAttackState.Inactive;
    public bool IsHoldingCharge => state == ChargeAttackState.Holding;
    public float ChargeProgress => chargeDefine != null
        ? Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, chargeDefine.maxChargeDuration))
        : 0f;
    public float CurrentDamageMultiplier => chargeDefine != null
        ? Mathf.Lerp(1f, Mathf.Max(1f, chargeDefine.maxDamageMultiplier), ChargeProgress)
        : 1f;
    public float MovementSpeedLimit => chargeDefine != null
        ? Mathf.Max(0f, chargeDefine.movementSpeedLimit)
        : -1f;
    /// <summary>
    /// 满蓄力减伤从 Holding 达到 100% 时开启，并覆盖随后整段 Releasing 动画。
    /// 短按或未蓄满释放不会获得该效果。
    /// </summary>
    public bool IsFullChargeGuardActive =>
        (state == ChargeAttackState.Holding || state == ChargeAttackState.Releasing) &&
        ChargeProgress >= 1f;
    public float FullChargeDamageReduction => chargeDefine != null
        ? Mathf.Clamp01(chargeDefine.fullChargeDamageReduction)
        : 0f;
    /// <summary>
    /// 满蓄力释放后的旋转阶段会锁定水平移动，但移动组件仍继续处理重力和落地。
    /// </summary>
    public bool IsFullChargeSpinActive => isFullChargeSpinActive;

    /// <summary>
    /// 角色模型或职业数据变化后重新绑定配置。
    /// 没有 chargeAttack 节点的职业会保持禁用，不改变原本的攻击方式。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        if (IsChargeAttackActive)
        {
            CancelCharge();
        }

        combat = player != null ? player.GetComponent<PlayerCombatComponent>() : GetComponent<PlayerCombatComponent>();
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        CharacterDefine define = player != null ? player.EntryDefine : null;
        chargeDefine = define != null && define.chargeAttack != null && define.chargeAttack.enabled
            ? define.chargeAttack
            : null;
        state = ChargeAttackState.Inactive;
        chargeTimer = 0f;
        RestoreFullChargeSpinRotation();
    }

    /// <summary>
    /// 由战斗组件优先处理左键输入。
    /// 返回 true 表示这一帧的普攻输入已被蓄力状态机接管，普通连击流程不应再处理。
    /// </summary>
    public bool TryHandleBasicAttackInput(IGameplayInput input)
    {
        if (chargeDefine == null || input == null)
        {
            return false;
        }

        if (state == ChargeAttackState.Inactive)
        {
            if (!input.LeftMouseDown || combat == null || !combat.BeginControlledBasicAttack())
            {
                return false;
            }

            state = ChargeAttackState.Windup;
            chargeTimer = 0f;
            return true;
        }

        if (state == ChargeAttackState.Releasing)
        {
            if (combat == null || !combat.IsAttacking)
            {
                ResetState();
            }

            return true;
        }

        // 鼠标失焦时可能收不到 Up 帧；Held 变为 false 也要安全释放，避免永久卡在蓄力姿势。
        if (input.LeftMouseUp || !input.LeftMouseHeld)
        {
            ReleaseAttack();
            return true;
        }

        if (state == ChargeAttackState.Windup)
        {
            float holdTime = Mathf.Clamp01(chargeDefine.holdNormalizedTime);
            if (presentation == null || presentation.TryHoldSimpleAttackPose(holdTime))
            {
                state = ChargeAttackState.Holding;
                chargeTimer = 0f;
            }

            return true;
        }

        AdvanceCharge(Time.deltaTime);
        return true;
    }

    /// <summary>
    /// 死亡、暂停、升级选择、切场景或对象禁用时统一取消。
    /// 取消入口同时恢复动画和关闭攻击判定，避免状态残留到下一次启用。
    /// </summary>
    public void CancelCharge()
    {
        if (!IsChargeAttackActive && !isFullChargeSpinActive)
        {
            return;
        }

        presentation?.CancelSimpleAttackPose();
        combat?.CancelControlledBasicAttack();
        ResetState();
    }

    private void OnDisable()
    {
        CancelCharge();
    }

    private void AdvanceCharge(float deltaTime)
    {
        if (state != ChargeAttackState.Holding || chargeDefine == null)
        {
            return;
        }

        // 到达满蓄力后只封顶计时，不自动释放，保留玩家选择出手时机的空间。
        chargeTimer = Mathf.Min(
            Mathf.Max(0.01f, chargeDefine.maxChargeDuration),
            chargeTimer + Mathf.Max(0f, deltaTime));
    }

    private void ReleaseAttack()
    {
        bool isFullChargeRelease =
            state == ChargeAttackState.Holding &&
            ChargeProgress >= 1f;
        float multiplier = state == ChargeAttackState.Holding
            ? CurrentDamageMultiplier
            : 1f;
        float areaRadius = isFullChargeRelease && chargeDefine != null
            ? Mathf.Max(0f, chargeDefine.fullChargeAreaRadius)
            : 0f;

        presentation?.ReleaseSimpleAttackPose();
        if (combat != null && combat.ReleaseControlledBasicAttack(
                multiplier,
                chargeDefine.releaseHitDelay,
                areaRadius))
        {
            state = ChargeAttackState.Releasing;
            if (isFullChargeRelease)
            {
                StartFullChargeSpin();
            }
            return;
        }

        ResetState();
    }

    /// <summary>
    /// 由 PlayerRuntimeController 在战斗输入处理后推进旋转。
    /// 始终使用“初始朝向 + 绝对进度角度”，避免逐帧累加造成不同帧率下的方向漂移。
    /// </summary>
    internal void TickFullChargeSpin(float deltaTime)
    {
        if (!isFullChargeSpinActive || chargeDefine == null)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, chargeDefine.fullChargeSpinDuration);
        fullChargeSpinTimer = Mathf.Min(
            duration,
            fullChargeSpinTimer + Mathf.Max(0f, deltaTime));
        float progress = Mathf.Clamp01(fullChargeSpinTimer / duration);
        float angle = chargeDefine.fullChargeSpinDegrees * progress;
        transform.rotation = fullChargeSpinStartRotation * Quaternion.Euler(0f, angle, 0f);

        if (progress >= 1f)
        {
            RestoreFullChargeSpinRotation();
        }
    }

    private void StartFullChargeSpin()
    {
        RestoreFullChargeSpinRotation();
        fullChargeSpinStartRotation = transform.rotation;
        fullChargeSpinTimer = 0f;
        isFullChargeSpinActive =
            chargeDefine != null &&
            chargeDefine.fullChargeSpinDuration > 0f &&
            Mathf.Abs(chargeDefine.fullChargeSpinDegrees) > 0.01f;
    }

    private void RestoreFullChargeSpinRotation()
    {
        if (isFullChargeSpinActive)
        {
            transform.rotation = fullChargeSpinStartRotation;
        }

        isFullChargeSpinActive = false;
        fullChargeSpinTimer = 0f;
    }

    private void ResetState()
    {
        RestoreFullChargeSpinRotation();
        state = ChargeAttackState.Inactive;
        chargeTimer = 0f;
    }
}
