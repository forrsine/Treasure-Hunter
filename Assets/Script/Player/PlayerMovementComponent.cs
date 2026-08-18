using UnityEngine;

/// <summary>
/// 玩家移动组件：只处理位移、跳跃、翻滚和体力，不关心当前使用哪一个职业模型。
/// 动画表现统一交给 PlayerPresentationComponent，从而让四个职业复用本组件。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMovementComponent : MonoBehaviour
{
    private const float GroundedVerticalVelocity = -2f;

    private IPlayerStatsReadOnly stats;
    private PlayerAudioComponent audioComponent;
    private CharacterController controller;
    private Animator animator;
    private PlayerPresentationComponent presentation;

    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;
    [SerializeField] private float attackMoveSpeedLimit = 3f;
    [SerializeField] private float rollSpeed = 12f;
    [SerializeField] private float rollDuration = 0.5f;
    [SerializeField] private float jumpHeight = 2f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;

    [SerializeField] private float maxStamina = 120f;
    [SerializeField] private float currentStamina;
    [SerializeField] private float jumpStaminaCost = 60f;
    [SerializeField] private float rollStaminaCost = 40f;
    [SerializeField] private float runStaminaCostPerSecond = 18f;
    [SerializeField] private float minimumStaminaToStartRun = 5f;
    [SerializeField] private float staminaRecoverPerSecond = 15f;

    private bool isJumping;
    private bool isRunning;
    private bool isRolling;
    private bool isWalk;
    private bool staminaConsumedThisFrame;
    private bool footstepLoopActive;
    private float footstepTimer;
    private float rollTimer;
    private float coyoteTimer;
    private float jumpBufferTimer;
    private Vector3 rollDirection;
    private Vector3 verticalVelocity;

    public bool IsRunning => isRunning;
    public bool IsRolling => isRolling;
    public bool IsWalk => isWalk;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;
    public float AttackMoveSpeedLimit => Mathf.Max(0.01f, attackMoveSpeedLimit);
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaPercent => maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    /// <summary>
    /// 旧脚本兼容入口。
    /// 现在真正的初始化仍然统一转给 PlayerRuntimeController，避免重新依赖旧的大脚本。
    /// </summary>
    public void Initialize(MonoBehaviour obsoleteOwner)
    {
        Initialize(GetComponent<PlayerRuntimeController>());
    }

    /// <summary>
    /// 新架构初始化入口。依赖来自明确的装配控制器，不再反向读取巨型玩家脚本。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        stats = player != null ? player.Stats : null;
        controller = player != null ? player.CharacterController : GetComponent<CharacterController>();
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        animator = presentation != null ? presentation.Animator : null;
        audioComponent = player != null ? player.Audio : GetComponent<PlayerAudioComponent>();
        ImportRuntimeSettings();
    }

    /// <summary>
    /// 供外部在运行时修改走路/跑步速度。
    /// 做 Clamp 是为了防止配置错误把速度设成 0 或负数，导致 CharacterController 行为异常。
    /// </summary>
    public void SetSpeeds(float newWalkSpeed, float newRunSpeed)
    {
        walkSpeed = Mathf.Max(0.01f, newWalkSpeed);
        runSpeed = Mathf.Max(walkSpeed, newRunSpeed);
    }

    /// <summary>
    /// 初始化体力参数。
    /// 体力系统和血量系统分开维护，这样以后扩闪避、冲刺或技能耗蓝时更容易独立调整。
    /// </summary>
    public void InitializeStamina()
    {
        maxStamina = Mathf.Max(1f, maxStamina);
        jumpStaminaCost = Mathf.Clamp(jumpStaminaCost, 0f, maxStamina);
        rollStaminaCost = Mathf.Clamp(rollStaminaCost, 0f, maxStamina);
        runStaminaCostPerSecond = Mathf.Max(0f, runStaminaCostPerSecond);
        minimumStaminaToStartRun = Mathf.Clamp(minimumStaminaToStartRun, 0f, maxStamina);
        staminaRecoverPerSecond = Mathf.Max(0f, staminaRecoverPerSecond);

        currentStamina = maxStamina;
        staminaConsumedThisFrame = false;
    }

    /// <summary>
    /// 每帧开始时同步最终移动速度结果。
    /// 移速升级发生在成长系统里，而移动组件每帧只读取最终结果，不参与升级公式。
    /// </summary>
    public void BeginFrame()
    {
        // 移速升级发生在 System 中，移动组件每帧只同步最终结果，不参与升级公式。
        if (stats != null && stats.CurrentMoveSpeed > 0f)
        {
            walkSpeed = stats.CurrentMoveSpeed;
            runSpeed = walkSpeed * Mathf.Max(1f, stats.RunSpeedMultiplier);
        }

        staminaConsumedThisFrame = false;
    }

    /// <summary>
    /// 如果当前处于翻滚状态，则继续推进翻滚过程。
    /// 返回 true 表示这一帧已经由翻滚接管了位移，外层不应再处理普通移动。
    /// </summary>
    public bool TickRolling()
    {
        if (!isRolling)
        {
            return false;
        }

        HandleRoll();
        return true;
    }

    /// <summary>
    /// 尝试进入翻滚。
    /// 这里会同时判断输入、攻击占用、当前状态和体力是否足够，
    /// 保证翻滚是一个完整的状态切换，而不是单纯播一个动作。
    /// </summary>
    public bool TryStartRoll(bool isAttacking)
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || isAttacking || isRolling || !input.RollDown)
        {
            return false;
        }

        if (!HasEnoughStamina(rollStaminaCost))
        {
            return false;
        }

        StartRoll(input);
        return true;
    }

    /// <summary>
    /// 处理普通状态下的移动。
    /// movementBlocked 控制是否完全禁止水平移动；jumpBlocked 只限制起跳，用于“攻击时可移动但不可跳跃”的动作优先级。
    /// horizontalSpeedLimit 用于攻击中限速，避免玩家一边播放攻击动作一边高速奔跑。
    /// </summary>
    public void TickNormalMovement(bool movementBlocked, bool jumpBlocked = false, float horizontalSpeedLimit = -1f)
    {
        UpdateJumpTimers();
        UpdateGroundedState();

        Vector3 horizontalVelocity = Vector3.zero;
        if (!movementBlocked)
        {
            if (!jumpBlocked)
            {
                TryJump();
            }
            horizontalVelocity = Move(horizontalSpeedLimit);
        }
        else
        {
            isWalk = false;
            isRunning = false;
            ResetMoveAnimationParams();
        }

        if (controller != null)
        {
            verticalVelocity.y += gravity * Time.deltaTime;
            controller.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);
        }

        UpdateGroundedAnimationState();
        UpdateMovementAudio(movementBlocked);
    }

    /// <summary>
    /// 恢复体力。
    /// 只有这一帧没有消耗体力且不在翻滚状态时，才允许自动回复。
    /// </summary>
    public void ApplyStaminaRecovery()
    {
        if (staminaConsumedThisFrame || isRolling)
        {
            return;
        }

        if (currentStamina >= maxStamina)
        {
            currentStamina = maxStamina;
            return;
        }

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRecoverPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// 从玩家运行时数据中导入基础移动参数。
    /// 这样职业基础速度和升级后的移速都能反映到当前组件。
    /// </summary>
    private void ImportRuntimeSettings()
    {
        if (stats != null)
        {
            walkSpeed = Mathf.Max(0.01f, stats.CurrentMoveSpeed > 0f ? stats.CurrentMoveSpeed : stats.BaseMoveSpeed);
            runSpeed = Mathf.Max(walkSpeed, walkSpeed * Mathf.Max(1f, stats.RunSpeedMultiplier));
        }

        maxStamina = Mathf.Max(1f, maxStamina);
        currentStamina = maxStamina;
    }

    /// <summary>
    /// 处理跳跃缓冲。
    /// 玩家就算提前一小段时间按下空格，也能在落地瞬间起跳，手感会更好。
    /// </summary>
    private void UpdateJumpTimers()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 同步 CharacterController 的落地状态，并维护土狼时间。
    /// 土狼时间可以理解成“离地后一小段时间内仍允许起跳”，常用于提升平台动作手感。
    /// </summary>
    private void UpdateGroundedState()
    {
        if (controller == null)
        {
            return;
        }

        if (controller.isGrounded)
        {
            coyoteTimer = coyoteTime;
            if (verticalVelocity.y < 0f)
            {
                verticalVelocity.y = GroundedVerticalVelocity;
            }
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// 尝试起跳。
    /// 只有同时满足跳跃缓冲、土狼时间和体力条件时，才真正进入跳跃。
    /// </summary>
    private void TryJump()
    {
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f)
        {
            return;
        }

        if (!HasEnoughStamina(jumpStaminaCost))
        {
            jumpBufferTimer = 0f;
            return;
        }

        ConsumeStamina(jumpStaminaCost);

        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isJumping = true;

        if (presentation != null)
        {
            presentation.PlayJump();
        }
        else if (animator != null)
        {
            animator.SetBool("IsGrounded", false);
            animator.SetTrigger("Jump");
        }

        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (ShouldAutoPlayActions())
        {
            PlayJumpSfx();
        }
    }

    private Vector3 Move(float horizontalSpeedLimit)
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null)
        {
            isWalk = false;
            isRunning = false;
            ResetMoveAnimationParams();
            return Vector3.zero;
        }

        float inputX = input.XInput;
        float inputY = input.YInput;
        Vector3 direction = Vector3.ClampMagnitude(transform.TransformDirection(inputX, 0f, inputY), 1f);
        bool hasSpeedLimit = horizontalSpeedLimit > 0f;

        bool wantsRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isWalk = direction.magnitude > 0.01f;

        if (!isWalk)
        {
            isRunning = false;
            if (presentation == null && animator != null)
            {
                animator.SetBool("IsRunning", false);
            }

            ResetMoveAnimationParams();
            return Vector3.zero;
        }

        bool wasRunning = isRunning;
        bool hasEnoughStaminaToStartRun = wasRunning
            ? currentStamina > 0f
            : currentStamina >= minimumStaminaToStartRun;
        // 跑步属于地面移动状态，空中只能保留普通水平位移，避免跳跃时切进跑步动画。
        bool canRunOnGround = controller == null || controller.isGrounded;
        bool canRunUnderLimit = !hasSpeedLimit || runSpeed <= horizontalSpeedLimit;
        isRunning = canRunOnGround && wantsRun && hasEnoughStaminaToStartRun && canRunUnderLimit;
        if (isRunning)
        {
            ConsumeStaminaAllowPartial(runStaminaCostPerSecond * Time.deltaTime);
        }

        if (presentation != null)
        {
            presentation.SetMovement(inputX, inputY, isRunning, isWalk);
        }
        else if (animator != null)
        {
            animator.SetBool("IsRunning", isRunning);
        }

        if (presentation == null)
        {
            UpdateMoveAnimationParams(inputX, inputY);
        }

        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        if (hasSpeedLimit)
        {
            targetSpeed = Mathf.Min(targetSpeed, horizontalSpeedLimit);
        }

        return direction * targetSpeed;
    }

    private void UpdateMoveAnimationParams(float inputX, float inputY)
    {
        if (animator == null)
        {
            return;
        }

        if (isRunning)
        {
            animator.SetFloat("SpeedX_Run", inputX);
            animator.SetFloat("SpeedY_Run", inputY);
            return;
        }

        animator.SetFloat("SpeedX", inputX);
        animator.SetFloat("SpeedY", inputY);
    }

    private void ResetMoveAnimationParams()
    {
        if (presentation != null)
        {
            presentation.ResetMovement();
            return;
        }

        if (animator == null)
        {
            return;
        }

        animator.SetFloat("SpeedX", 0f);
        animator.SetFloat("SpeedY", 0f);
        animator.SetFloat("SpeedX_Run", 0f);
        animator.SetFloat("SpeedY_Run", 0f);
    }

    private void StartRoll(IGameplayInput input)
    {
        ConsumeStamina(rollStaminaCost);

        isRolling = true;
        rollTimer = rollDuration;
        ResetFootstepLoop();

        float inputX = input != null ? input.XInput : 0f;
        float inputY = input != null ? input.YInput : 0f;
        Vector3 localDirection = new Vector3(inputX, 0f, inputY);
        if (localDirection.sqrMagnitude < 0.01f)
        {
            // 没有方向输入时默认向角色面朝方向翻滚，避免玩家按右键却完全没有反馈。
            inputX = 0f;
            inputY = 1f;
            localDirection = Vector3.forward;
        }

        rollDirection = transform.TransformDirection(localDirection).normalized;

        if (presentation != null)
        {
            presentation.PlayRoll(inputX, inputY);
        }
        else if (animator != null)
        {
            animator.SetFloat("RollX", inputX);
            animator.SetFloat("RollY", inputY);
            animator.SetTrigger("Roll");
        }

        isRunning = false;
        isWalk = false;

        if (ShouldAutoPlayActions())
        {
            PlayRollSfx();
        }
    }

    private void HandleRoll()
    {
        rollTimer -= Time.deltaTime;

        if (controller != null && controller.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = GroundedVerticalVelocity;
        }

        if (rollTimer > 0f)
        {
            if (controller != null)
            {
                controller.Move(rollDirection * rollSpeed * Time.deltaTime);
                verticalVelocity.y += gravity * Time.deltaTime;
                controller.Move(verticalVelocity * Time.deltaTime);
            }

            return;
        }

        isRolling = false;
        rollTimer = 0f;
    }

    private bool HasEnoughStamina(float cost)
    {
        return cost <= 0f || currentStamina >= cost;
    }

    private bool ConsumeStamina(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (currentStamina < amount)
        {
            return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        staminaConsumedThisFrame = true;
        return true;
    }

    private void ConsumeStaminaAllowPartial(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        staminaConsumedThisFrame = true;
    }

    private void UpdateGroundedAnimationState()
    {
        if (controller != null && presentation != null)
        {
            presentation.SetGrounded(controller.isGrounded);
        }
        else if (controller != null && animator != null)
        {
            animator.SetBool("IsGrounded", controller.isGrounded);
        }

        isJumping = controller == null || !controller.isGrounded;
    }

    private void UpdateMovementAudio(bool movementBlocked)
    {
        if (!ShouldAutoPlayFootsteps() || movementBlocked || controller == null)
        {
            ResetFootstepLoop();
            return;
        }

        bool canPlayFootstep = controller.isGrounded && isWalk && !isRolling;
        if (!canPlayFootstep)
        {
            ResetFootstepLoop();
            return;
        }

        float interval = Mathf.Max(
            0.05f,
            isRunning
                ? audioComponent != null ? audioComponent.RunFootstepInterval : 0.3f
                : audioComponent != null ? audioComponent.WalkFootstepInterval : 0.7f);
        if (!footstepLoopActive)
        {
            footstepLoopActive = true;
            footstepTimer = 0f;
        }
        else
        {
            footstepTimer -= Time.deltaTime;
        }

        if (footstepTimer > 0f)
        {
            return;
        }

        if (isRunning)
        {
            PlayRunFootstepSfx();
        }
        else
        {
            PlayWalkFootstepSfx();
        }

        footstepTimer = interval;
    }

    private void ResetFootstepLoop()
    {
        footstepLoopActive = false;
        footstepTimer = 0f;
    }


    private bool ShouldAutoPlayActions()
    {
        return audioComponent != null && audioComponent.AutoPlayActions;
    }

    private bool ShouldAutoPlayFootsteps()
    {
        return audioComponent != null && audioComponent.AutoPlayFootsteps;
    }

    private void PlayJumpSfx()
    {
        if (audioComponent != null) audioComponent.PlayJump();
    }

    private void PlayRollSfx()
    {
        if (audioComponent != null) audioComponent.PlayRoll();
    }

    private void PlayRunFootstepSfx()
    {
        if (audioComponent != null) audioComponent.PlayRunFootstep();
    }

    private void PlayWalkFootstepSfx()
    {
        if (audioComponent != null) audioComponent.PlayWalkFootstep();
    }
}
