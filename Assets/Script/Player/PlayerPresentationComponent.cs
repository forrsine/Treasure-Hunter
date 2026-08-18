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
    private bool attackLayerFading;

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
        TickAttackLayerFadeOut();
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
        skillAnimationTimer = 0f;
        attackLayerFading = false;

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
            SetFloat("Speed", isWalking ? (isRunning ? 1f : 0.5f) : 0f);
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
        SetFloat("RollX", inputX);
        SetFloat("RollY", inputY);
        SetTrigger("Roll");
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
                SetTrigger("Attack");
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
        if (animationStyle == CharacterAnimationStyle.SimpleSpeedAttack)
        {
            SetTrigger(SkillTriggerName);
            return;
        }

        skillAnimationTimer = Mathf.Max(0.1f, animationDuration);
        attackLayerFading = false;
        attackLayerFadeDelayTimer = 0f;

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

    private void BeginAttackLayerFadeOut(float delay, float duration)
    {
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
            BeginAttackLayerFadeOut(0f, skillLayerFadeOutDuration);
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
