using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家表现组件：负责把“移动、攻击、跳跃”等通用动作翻译成当前职业 Animator 能理解的参数。
///
/// 四个职业使用的 Animator Controller 参数并不一致。移动和战斗组件只表达动作意图，
/// 具体应该设置 Speed 还是 SpeedX、应该触发 Attack 还是修改 ComboIndex，都由本组件处理。
/// 这样核心玩法代码不需要认识某一个角色模型，也不会因为更换美术资源而跟着修改。
/// </summary>
[DisallowMultipleComponent]
public class PlayerPresentationComponent : MonoBehaviour
{
    private const string DirectionalAttackLayerName = "Attack Layer";
    private const string SimpleAttackStateName = "Attack";
    private const string SimpleSkillStateName = "Skill";
    private const string SimpleEmptyStateName = "Empty";
    private const string SkillTriggerName = "Skill";
    private const string ComboAttack1StateName = "Atk4";
    private const string ComboAttack2StateName = "Atk1";
    private const string ComboAttack3StateName = "Atk2";

    private readonly HashSet<int> animatorParameterHashes = new HashSet<int>();

    [SerializeField] private float attackLayerFadeOutDelay = 0.55f;
    [SerializeField] private float attackLayerFadeOutDuration = 0.18f;
    [SerializeField] private float attackLayerRunCancelFadeOutDuration = 0.06f;
    [SerializeField] private float scriptedAttackLayerFadeOutDuration = 0.12f;
    [SerializeField] private float skillLayerFadeOutDuration = 0.18f;
    [SerializeField] private float comboAttackCrossFadeDuration = 0.04f;
    [SerializeField, Min(0f)] private float simpleMovementDampTime = 0.1f;
    [SerializeField, Min(0f)] private float simpleActionTransitionDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float simpleActionLayerBlendDuration = 0.1f;
    [SerializeField] private bool logComboAnimationDebug = true;

    private Animator animator;
    private SkinnedMeshRenderer primaryRenderer;
    private CharacterAnimationStyle animationStyle;
    private float basicAttackDuration = 0.7f;
    private float attackLayerWeight;
    private float attackLayerFadeDelayTimer;
    private float attackLayerCurrentFadeOutDuration;
    private float skillAnimationTimer;
    private float lastMoveInputX;
    private float lastMoveInputY = 1f;
    private bool attackLayerFadingIn;
    private bool attackLayerFading;
    private float attackLayerCurrentFadeInDuration;
    private bool simpleAttackStateMissingLogged;
    private bool simpleSkillStateMissingLogged;
    private bool simpleAttackPoseHeld;
    private float simpleAttackHeldNormalizedTime;

    /// <summary>
    /// 当前职业模型真正使用的 Animator。优先选择带有 Controller 的 Animator，
    /// 避免复杂模型中辅助 Animator 抢先被绑定。
    /// </summary>
    public Animator Animator => animator;

    /// <summary>
    /// 受击变色使用的主要模型 Renderer。
    /// </summary>
    public SkinnedMeshRenderer PrimaryRenderer => primaryRenderer;

    /// <summary>
    /// 刺客动画包含连击窗口和武器开关事件；简单职业控制器暂时使用代码计时兜底。
    /// </summary>
    public bool UsesCombatAnimationEvents => animationStyle == CharacterAnimationStyle.DirectionalCombo;

    public float BasicAttackDuration => Mathf.Max(0.1f, basicAttackDuration);
    public bool IsSkillAnimationPlaying => skillAnimationTimer > 0f;

    private void Update()
    {
        TickSkillAnimationTimer();
        TickAttackLayerFadeIn();
        TickAttackLayerFadeOut();
    }

    private void LateUpdate()
    {
        // Animator 在 Update 后完成采样，LateUpdate 再固定攻击层，避免蓄力姿势在帧间继续向前漂移。
        PinSimpleAttackPoseIfNeeded();
    }

    /// <summary>
    /// 绑定运行时装入的职业模型，并缓存动画参数。
    /// 视觉 Prefab 自带的碰撞体会被关闭，角色移动和受击统一交给外层 PlayerRuntime。
    /// </summary>
    public void BindVisual(GameObject visualObject, CharacterDefine define)
    {
        animator = FindGameplayAnimator(visualObject);
        primaryRenderer = visualObject != null
            ? visualObject.GetComponentInChildren<SkinnedMeshRenderer>(true)
            : null;

        animationStyle = define != null
            ? define.animationStyle
            : CharacterAnimationStyle.DirectionalCombo;
        basicAttackDuration = define != null && define.basicAttackDuration > 0f
            ? define.basicAttackDuration
            : 0.7f;

        DisableVisualPhysics(visualObject);
        CacheAnimatorParameters();
        EnsureAnimationEventRelay();
        SetDirectionalAttackLayerWeight(0f);
        attackLayerFadeDelayTimer = 0f;
        attackLayerCurrentFadeOutDuration = attackLayerFadeOutDuration;
        attackLayerCurrentFadeInDuration = simpleActionLayerBlendDuration;
        skillAnimationTimer = 0f;
        attackLayerFadingIn = false;
        attackLayerFading = false;
        simpleAttackStateMissingLogged = false;
        simpleSkillStateMissingLogged = false;
        simpleAttackPoseHeld = false;
        simpleAttackHeldNormalizedTime = 0f;

        if (animator == null)
        {
            Debug.LogError("职业模型缺少可用的 Animator，角色逻辑可以运行，但不会播放动画。", visualObject);
            return;
        }

        LogComboAnimationDebug(
            $"玩家表现绑定：Animator={animator.name}，Controller={GetAnimatorControllerName()}，AnimationStyle={animationStyle}");
    }

    /// <summary>
    /// 同步移动表现。DirectionalCombo 使用四方向参数，SimpleSpeedAttack 使用单一 Speed 参数。
    /// </summary>
    public void SetMovement(float inputX, float inputY, bool isRunning, bool isWalking)
    {
        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            SetFloatDamped(
                "Speed",
                isWalking ? (isRunning ? 1f : 0.5f) : 0f,
                simpleMovementDampTime);
            return;
        }

        SetBool("IsWalk", isWalking);
        SetBool("IsRunning", isRunning);

        if (isRunning)
        {
            SpeedUpAttackLayerFadeOutForRun();
        }

        if (isWalking)
        {
            Vector2 inputDirection = Vector2.ClampMagnitude(new Vector2(inputX, inputY), 1f);
            lastMoveInputX = inputDirection.x;
            lastMoveInputY = inputDirection.y;

            // WalkBlend 和 RunBlend 在过渡时会同时采样。
            // 两套方向参数保持一致，可以避免其中一套被写成 (0,0) 后采样到错误方向。
            SetFloat("SpeedX", lastMoveInputX);
            SetFloat("SpeedY", lastMoveInputY);
            SetFloat("SpeedX_Run", lastMoveInputX);
            SetFloat("SpeedY_Run", lastMoveInputY);
        }
        else
        {
            SetFloat("SpeedX", 0f);
            SetFloat("SpeedY", 0f);
            // RunBlend 没有 (0,0) 中心动画，退出跑步的过渡帧继续保留最后方向，避免短暂左前方偏移。
            SetFloat("SpeedX_Run", lastMoveInputX);
            SetFloat("SpeedY_Run", lastMoveInputY);
        }
    }

    public void ResetMovement()
    {
        SetMovement(0f, 0f, false, false);
    }

    /// <summary>
    /// 同步落地状态，供跳跃/落地状态机切换。
    /// </summary>
    public void SetGrounded(bool isGrounded)
    {
        SetBool("IsGrounded", isGrounded);
    }

    /// <summary>
    /// 播放跳跃动作。
    /// </summary>
    public void PlayJump()
    {
        SetBool("IsGrounded", false);
        SetTrigger("Jump");
    }

    /// <summary>
    /// 播放翻滚动作。
    /// 方向参数由移动组件提前算好，表现层只负责喂给 Animator。
    /// </summary>
    public void PlayRoll(float inputX, float inputY)
    {
        if (HasParameter("Roll"))
        {
            SetFloat("RollX", inputX);
            SetFloat("RollY", inputY);
            SetTrigger("Roll");
            return;
        }

        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            // 简单动画职业没有翻滚动作。冲刺位移仍由 MovementComponent 负责，
            // 表现层把 Speed 切到奔跑档，让战士、弓箭手和法师用两倍速移动动画快速冲向锁定方向。
            SetFloat("Speed", 1f);
        }
    }

    /// <summary>
    /// 更新攻击表现。刺客通过 ComboIndex 进入不同连击状态，其他职业暂时触发基础 Attack。
    /// </summary>
    public void SetCombo(int comboIndex)
    {
        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            SetBool("isAttacking", comboIndex > 0);
            if (comboIndex > 0)
            {
                // 简单职业的攻击也使用独立上半身层。绑定模型时该层会先归零，
                // 每次攻击必须显式恢复权重，否则 Trigger 已触发但玩家看不到动作。
                BeginAttackLayerFadeIn(simpleActionLayerBlendDuration);
                ResetTrigger("Attack");
                if (!TryCrossFadeSimpleState(SimpleAttackStateName))
                {
                    // 兼容以后接入但没有项目标准 Attack 状态的第三方简单控制器。
                    if (!simpleAttackStateMissingLogged)
                    {
                        simpleAttackStateMissingLogged = true;
                        Debug.LogError(
                            $"简单职业 Animator 缺少 {DirectionalAttackLayerName}.{SimpleAttackStateName}，Controller={GetAnimatorControllerName()}，已回退到 Attack Trigger。",
                            this);
                    }
                    SetTrigger("Attack");
                }
            }
            else
            {
                // 显式回到空状态再淡出层权重，避免非循环攻击停在最后一帧后影响下一次射击。
                TryCrossFadeSimpleState(SimpleEmptyStateName);
                BeginAttackLayerFadeOut(0f, simpleActionLayerBlendDuration);
            }

            return;
        }

        if (comboIndex > 0)
        {
            attackLayerFading = false;
            attackLayerFadeDelayTimer = 0f;
            SetDirectionalAttackLayerWeight(1f);
            SetInteger("ComboIndex", comboIndex);
            CrossFadeComboAttackState(comboIndex);
            return;
        }

        // 先让状态机根据 ComboIndex=0 走完攻击收尾，再延迟淡出攻击层。
        // 如果这里立刻把层权重设为 0，收刀动作会被脚本直接切掉。
        SetInteger("ComboIndex", 0);
        BeginAttackLayerFadeOut(attackLayerFadeOutDelay, attackLayerFadeOutDuration);
    }

    /// <summary>
    /// 当简单职业攻击动画到达指定归一化时间后，只固定 Attack Layer 的 Attack 状态。
    /// 不修改 Animator.speed，因此基础移动层仍能以受限速度继续播放和移动。
    /// </summary>
    public bool TryHoldSimpleAttackPose(float normalizedTime)
    {
        if (animationStyle != CharacterAnimationStyle.SimpleSpeedAttack || animator == null)
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(DirectionalAttackLayerName);
        int attackStateHash = GetSimpleStateHash(layerIndex, SimpleAttackStateName);
        if (layerIndex < 0 || attackStateHash == 0)
        {
            return false;
        }

        float targetTime = Mathf.Clamp01(normalizedTime);
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
        if (animator.IsInTransition(layerIndex))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(layerIndex);
            if (IsSimpleState(nextState, SimpleAttackStateName))
            {
                stateInfo = nextState;
            }
        }

        if (!IsSimpleState(stateInfo, SimpleAttackStateName) || stateInfo.normalizedTime < targetTime)
        {
            return false;
        }

        simpleAttackPoseHeld = true;
        simpleAttackHeldNormalizedTime = targetTime;
        attackLayerFadingIn = false;
        attackLayerFading = false;
        SetDirectionalAttackLayerWeight(1f);
        PinSimpleAttackPose(layerIndex, attackStateHash);
        return true;
    }

    /// <summary>
    /// 松手时停止固定状态，让 Animator 从职业配置的固定点继续播放剩余挥剑动作。
    /// </summary>
    public void ReleaseSimpleAttackPose()
    {
        simpleAttackPoseHeld = false;
    }

    /// <summary>
    /// 非正常结束时只解除姿势固定；回到 Empty 和淡出攻击层由战斗组件统一重置。
    /// </summary>
    public void CancelSimpleAttackPose()
    {
        simpleAttackPoseHeld = false;
        simpleAttackHeldNormalizedTime = 0f;
    }

    /// <summary>
    /// 脚本计时攻击使用的提前清参入口。
    /// ATK4 没有动画事件，如果动画先回到 Empty 但 ComboIndex 仍为 1，Any State 会再次拉回 Atk4，画面就会抽搐。
    /// </summary>
    public void ClearComboIndexForScriptedAttack()
    {
        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            return;
        }

        SetInteger("ComboIndex", 0);
    }

    /// <summary>
    /// 脚本计时攻击结束后立即淡出攻击层。
    /// 这样 Empty 状态不会长时间以 1 的权重覆盖 Base Layer，避免回到待机/移动时抖一下。
    /// </summary>
    public void FadeOutAttackLayerAfterScriptedAttack()
    {
        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            return;
        }

        SetInteger("ComboIndex", 0);
        BeginAttackLayerFadeOut(0f, scriptedAttackLayerFadeOutDuration);
    }

    /// <summary>
    /// 播放技能动作。
    /// 技能和普攻共用 Attack Layer，但使用独立 Trigger，避免把技能塞进三连击 ComboIndex。
    /// </summary>
    public void PlaySkill(float animationDuration)
    {
        skillAnimationTimer = Mathf.Max(0.1f, animationDuration);
        attackLayerFading = false;
        attackLayerFadeDelayTimer = 0f;

        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            // 远程职业的 Skill 占位动作也包含 shoot 事件。
            // 技能计时期间阻止普通攻击，避免该事件被误判为一次普攻投射物释放。
            BeginAttackLayerFadeIn(simpleActionLayerBlendDuration);
            ResetTrigger(SkillTriggerName);
            if (!TryCrossFadeSimpleState(SimpleSkillStateName))
            {
                if (!simpleSkillStateMissingLogged)
                {
                    simpleSkillStateMissingLogged = true;
                    Debug.LogError(
                        $"简单职业 Animator 缺少 {DirectionalAttackLayerName}.{SimpleSkillStateName}，Controller={GetAnimatorControllerName()}，已回退到 Skill Trigger。",
                        this);
                }
                SetTrigger(SkillTriggerName);
            }
            return;
        }

        // 技能触发前先清掉普攻连击参数，防止当前卡在 Atk1/Atk2 时抢不过 Skill Trigger。
        SetDirectionalAttackLayerWeight(1f);
        SetInteger("ComboIndex", 0);
        SetTrigger(SkillTriggerName);
        BeginAttackLayerFadeOut(skillAnimationTimer, skillLayerFadeOutDuration);
    }

    private Animator FindGameplayAnimator(GameObject visualObject)
    {
        if (visualObject == null)
        {
            return null;
        }

        Animator[] animators = visualObject.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i].runtimeAnimatorController != null)
            {
                return animators[i];
            }
        }

        return animators.Length > 0 ? animators[0] : null;
    }

    /// <summary>
    /// 关闭职业模型自带的物理与演示脚本。
    /// 运行时真正的碰撞与战斗流程由外层 PlayerRuntime 统一接管，避免资源包自带逻辑干扰项目规则。
    /// </summary>
    private void DisableVisualPhysics(GameObject visualObject)
    {
        if (visualObject == null)
        {
            return;
        }

        // 外层 PlayerRuntime 已经统一提供 CharacterController 和攻击判定，
        // 模型自身的 Collider/Rigidbody 如果继续启用，会造成重复碰撞或移动抖动。
        Collider[] colliders = visualObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = visualObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }

        // 模型资源中可能残留演示用的武器或投射物脚本。继续启用会绕过本项目战斗流程，
        // 例如 Human Pack 的 triggerProjectile 会自行 Instantiate 演示子弹。
        MonoBehaviour[] behaviours = visualObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour is WeaponCo ||
                (behaviour != null && behaviour.GetType().Name == "triggerProjectile"))
            {
                behaviour.enabled = false;
            }
        }
    }

    /// <summary>
    /// 缓存当前 Animator 真正存在的参数哈希。
    /// 后续写参数前先检查存在性，可以避免切换职业后因为参数名不同而刷报错。
    /// </summary>
    private void CacheAnimatorParameters()
    {
        animatorParameterHashes.Clear();
        if (animator == null)
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            animatorParameterHashes.Add(parameters[i].nameHash);
        }
    }

    /// <summary>
    /// 确保职业模型上的动画事件可以转发回玩家根对象。
    /// 因为 Animator 在子物体上，而真正的战斗逻辑组件在 PlayerRuntime 根对象上。
    /// </summary>
    private void EnsureAnimationEventRelay()
    {
        if (animator == null)
        {
            return;
        }

        PlayerAnimationEventRelay relay = animator.GetComponent<PlayerAnimationEventRelay>();
        if (relay == null)
        {
            relay = animator.gameObject.AddComponent<PlayerAnimationEventRelay>();
        }

        relay.Initialize(GetComponent<PlayerRuntimeController>());
    }

    private bool HasParameter(string parameterName)
    {
        return animator != null && animatorParameterHashes.Contains(Animator.StringToHash(parameterName));
    }

    private void SetFloat(string parameterName, float value)
    {
        if (HasParameter(parameterName))
        {
            animator.SetFloat(parameterName, value);
        }
    }

    private void SetFloatDamped(string parameterName, float value, float dampTime)
    {
        if (HasParameter(parameterName))
        {
            // 简单职业使用同一 BlendTree 表现待机、走路和奔跑。
            // 参数阻尼能保留输入响应，同时避免松开方向键时动作权重瞬间跳变。
            animator.SetFloat(
                parameterName,
                value,
                Mathf.Max(0f, dampTime),
                Time.deltaTime);
        }
    }

    private void SetBool(string parameterName, bool value)
    {
        if (HasParameter(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private void SetInteger(string parameterName, int value)
    {
        if (HasParameter(parameterName))
        {
            animator.SetInteger(parameterName, value);
        }
    }

    private void SetTrigger(string parameterName)
    {
        if (HasParameter(parameterName))
        {
            animator.SetTrigger(parameterName);
        }
    }

    private void ResetTrigger(string parameterName)
    {
        if (HasParameter(parameterName))
        {
            animator.ResetTrigger(parameterName);
        }
    }

    /// <summary>
    /// 刺客三连击的运行时保险：状态机仍保留 ComboIndex 过渡，但这里直接指定本段要播放的状态。
    /// 这样即使 Any State 或过渡优先级发生变化，也能保证顺序是 ATK4 -> Atk1 -> Atk2。
    /// </summary>
    private void CrossFadeComboAttackState(int comboIndex)
    {
        if (animator == null)
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(DirectionalAttackLayerName);
        if (layerIndex < 0)
        {
            return;
        }

        string stateName = GetComboAttackStateName(comboIndex);
        if (string.IsNullOrEmpty(stateName))
        {
            return;
        }

        string playableStateName = GetPlayableStateName(layerIndex, stateName);
        if (string.IsNullOrEmpty(playableStateName))
        {
            Debug.LogWarning(
                $"攻击层找不到连击状态：Combo {comboIndex} -> {stateName}，Controller={GetAnimatorControllerName()}，将退回 ComboIndex 过渡。",
                this);
            return;
        }

        // 连击顺序由表现层显式映射，避免状态机当前停留位置影响下一段攻击播放。
        animator.CrossFadeInFixedTime(
            playableStateName,
            Mathf.Max(0f, comboAttackCrossFadeDuration),
            layerIndex,
            0f);

        LogComboAnimationDebug(
            $"刺客连击动画：Combo {comboIndex} -> {stateName}，播放状态={playableStateName}，Controller={GetAnimatorControllerName()}，Layer={layerIndex}");
    }

    private string GetPlayableStateName(int layerIndex, string stateName)
    {
        string fullStateName = DirectionalAttackLayerName + "." + stateName;
        if (animator.HasState(layerIndex, Animator.StringToHash(fullStateName)))
        {
            return fullStateName;
        }

        if (animator.HasState(layerIndex, Animator.StringToHash(stateName)))
        {
            return stateName;
        }

        return string.Empty;
    }

    private string GetAnimatorControllerName()
    {
        return animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : "None";
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void LogComboAnimationDebug(string message)
    {
        if (logComboAnimationDebug)
        {
            Debug.Log(message, this);
        }
    }

    private string GetComboAttackStateName(int comboIndex)
    {
        switch (comboIndex)
        {
            case 1:
                return ComboAttack1StateName;
            case 2:
                return ComboAttack2StateName;
            case 3:
                return ComboAttack3StateName;
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 刺客攻击使用独立攻击层叠加到移动层上。
    /// 平时关闭攻击层，避免 Empty 状态影响移动；普攻开始时再打开，实现“边移动边攻击”。
    /// </summary>
    private void SetDirectionalAttackLayerWeight(float weight)
    {
        attackLayerWeight = Mathf.Clamp01(weight);
        if (animator == null)
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(DirectionalAttackLayerName);
        if (layerIndex >= 0)
        {
            animator.SetLayerWeight(layerIndex, attackLayerWeight);
        }
    }

    /// <summary>
    /// 简单职业从起点淡入项目标准状态。
    /// 固定时间 CrossFade 在保留连续重播能力的同时，避免弩射击、法杖和剑击动作瞬间硬切。
    /// </summary>
    private bool TryCrossFadeSimpleState(string stateName)
    {
        if (animator == null)
        {
            return false;
        }

        int layerIndex = animator.GetLayerIndex(DirectionalAttackLayerName);
        if (layerIndex < 0)
        {
            return false;
        }

        int fullPathHash = Animator.StringToHash(
            $"{DirectionalAttackLayerName}.{stateName}");
        int shortNameHash = Animator.StringToHash(stateName);
        int playableStateHash = animator.HasState(layerIndex, fullPathHash)
            ? fullPathHash
            : shortNameHash;
        if (!animator.HasState(layerIndex, playableStateHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(
            playableStateHash,
            Mathf.Max(0f, simpleActionTransitionDuration),
            layerIndex,
            0f);
        return true;
    }

    private int GetSimpleStateHash(int layerIndex, string stateName)
    {
        if (animator == null || layerIndex < 0)
        {
            return 0;
        }

        int fullPathHash = Animator.StringToHash($"{DirectionalAttackLayerName}.{stateName}");
        if (animator.HasState(layerIndex, fullPathHash))
        {
            return fullPathHash;
        }

        int shortNameHash = Animator.StringToHash(stateName);
        return animator.HasState(layerIndex, shortNameHash) ? shortNameHash : 0;
    }

    private static bool IsSimpleState(AnimatorStateInfo stateInfo, string stateName)
    {
        return stateInfo.IsName($"{DirectionalAttackLayerName}.{stateName}") ||
               stateInfo.IsName(stateName);
    }

    private void PinSimpleAttackPoseIfNeeded()
    {
        if (!simpleAttackPoseHeld || animator == null)
        {
            return;
        }

        int layerIndex = animator.GetLayerIndex(DirectionalAttackLayerName);
        int attackStateHash = GetSimpleStateHash(layerIndex, SimpleAttackStateName);
        if (layerIndex >= 0 && attackStateHash != 0)
        {
            PinSimpleAttackPose(layerIndex, attackStateHash);
        }
    }

    private void PinSimpleAttackPose(int layerIndex, int attackStateHash)
    {
        animator.Play(attackStateHash, layerIndex, simpleAttackHeldNormalizedTime);
        animator.Update(0f);
    }

    private void BeginAttackLayerFadeIn(float duration)
    {
        attackLayerFading = false;
        attackLayerFadeDelayTimer = 0f;
        attackLayerCurrentFadeInDuration = Mathf.Max(0.01f, duration);
        attackLayerFadingIn = attackLayerWeight < 1f;

        if (!attackLayerFadingIn)
        {
            SetDirectionalAttackLayerWeight(1f);
        }
    }

    private void BeginAttackLayerFadeOut(float delay, float duration)
    {
        attackLayerFadingIn = false;
        attackLayerFadeDelayTimer = Mathf.Max(0f, delay);
        attackLayerCurrentFadeOutDuration = Mathf.Max(0.01f, duration);
        attackLayerFading = attackLayerWeight > 0f;
    }

    private void SpeedUpAttackLayerFadeOutForRun()
    {
        if (IsSkillAnimationPlaying)
        {
            return;
        }

        if (!attackLayerFading || attackLayerWeight <= 0f)
        {
            return;
        }

        // 攻击结束后如果玩家立刻跑步，优先让移动表现接管。
        // 不跑步时仍然保留普通收刀延迟，避免站立结束动作被硬切。
        attackLayerFadeDelayTimer = 0f;
        attackLayerCurrentFadeOutDuration = Mathf.Max(0.01f, attackLayerRunCancelFadeOutDuration);
    }

    private void TickSkillAnimationTimer()
    {
        if (skillAnimationTimer <= 0f)
        {
            return;
        }

        skillAnimationTimer -= Time.deltaTime;
        if (skillAnimationTimer <= 0f)
        {
            skillAnimationTimer = 0f;
            SetInteger("ComboIndex", 0);
            BeginAttackLayerFadeOut(
                0f,
                animationStyle == CharacterAnimationStyle.SimpleSpeedAttack
                    ? simpleActionLayerBlendDuration
                    : skillLayerFadeOutDuration);
        }
    }

    private void TickAttackLayerFadeIn()
    {
        AdvanceAttackLayerFadeIn(Time.deltaTime);
    }

    /// <summary>
    /// 单独接收帧间隔，既便于 EditMode 回归测试精确推进，也避免把表现混合绑定到固定帧率。
    /// </summary>
    private void AdvanceAttackLayerFadeIn(float deltaTime)
    {
        if (!attackLayerFadingIn)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, attackLayerCurrentFadeInDuration);
        float nextWeight = Mathf.MoveTowards(attackLayerWeight, 1f, Mathf.Max(0f, deltaTime) / duration);
        SetDirectionalAttackLayerWeight(nextWeight);

        if (attackLayerWeight >= 1f)
        {
            attackLayerFadingIn = false;
        }
    }

    private void TickAttackLayerFadeOut()
    {
        if (!attackLayerFading)
        {
            return;
        }

        if (attackLayerFadeDelayTimer > 0f)
        {
            attackLayerFadeDelayTimer -= Time.deltaTime;
            return;
        }

        float duration = Mathf.Max(0.01f, attackLayerCurrentFadeOutDuration);
        float nextWeight = Mathf.MoveTowards(attackLayerWeight, 0f, Time.deltaTime / duration);
        SetDirectionalAttackLayerWeight(nextWeight);

        if (attackLayerWeight <= 0f)
        {
            attackLayerFading = false;
        }
    }
}
