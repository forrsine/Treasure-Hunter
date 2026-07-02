using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMovementComponent : MonoBehaviour
{
    private const float GroundedVerticalVelocity = -2f;

    private PlayerCo owner;
    private CharacterController controller;
    private Animator animator;

    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 5f;
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
    public float StaminaPercent => maxStamina > 0f ? Mathf.Clamp01(currentStamina / maxStamina) : 0f;

    public void Initialize(PlayerCo player)
    {
        owner = player;
        controller = owner.CharacterController;
        animator = owner.PlayerAnimator;
        ImportLegacySettings();
        InitializeStamina();
        SyncOwnerState();
    }

    public void SetSpeeds(float newWalkSpeed, float newRunSpeed)
    {
        walkSpeed = Mathf.Max(0.01f, newWalkSpeed);
        runSpeed = Mathf.Max(walkSpeed, newRunSpeed);
        owner.LegacyWalkSpeed = walkSpeed;
        owner.LegacyRunSpeed = runSpeed;
    }

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
        owner.LegacyCurrentStamina = currentStamina;
        owner.LegacyMaxStamina = maxStamina;
    }

    public void BeginFrame()
    {
        staminaConsumedThisFrame = false;
        SyncOwnerState();
    }

    public bool TickRolling()
    {
        if (!isRolling)
        {
            return false;
        }

        HandleRoll();
        SyncOwnerState();
        return true;
    }

    public bool TryStartRoll(bool isAttacking)
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || isAttacking || isRolling || !Input.GetKeyDown(KeyCode.Mouse1))
        {
            return false;
        }

        bool hasMoveInput =
            Mathf.Abs(input.XInput) > 0.1f ||
            Mathf.Abs(input.YInput) > 0.1f;

        if (!hasMoveInput || !HasEnoughStamina(rollStaminaCost))
        {
            return false;
        }

        StartRoll(input);
        SyncOwnerState();
        return true;
    }

    public void TickNormalMovement(bool movementBlocked)
    {
        UpdateJumpTimers();
        UpdateGroundedState();

        Vector3 horizontalVelocity = Vector3.zero;
        if (!movementBlocked)
        {
            TryJump();
            horizontalVelocity = Move();
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
        SyncOwnerState();
    }

    public void ApplyStaminaRecovery()
    {
        if (staminaConsumedThisFrame || isRolling)
        {
            return;
        }

        if (currentStamina >= maxStamina)
        {
            currentStamina = maxStamina;
            owner.LegacyCurrentStamina = currentStamina;
            return;
        }

        currentStamina = Mathf.Min(
            maxStamina,
            currentStamina + staminaRecoverPerSecond * Time.deltaTime);
        owner.LegacyCurrentStamina = currentStamina;
    }

    private void ImportLegacySettings()
    {
        walkSpeed = Mathf.Max(0.01f, owner.LegacyWalkSpeed);
        runSpeed = Mathf.Max(walkSpeed, owner.LegacyRunSpeed);
        rollSpeed = Mathf.Max(0f, owner.LegacyRollSpeed);
        rollDuration = Mathf.Max(0.01f, owner.LegacyRollDuration);
        jumpHeight = Mathf.Max(0f, owner.LegacyJumpHeight);
        gravity = owner.LegacyGravity;
        coyoteTime = Mathf.Max(0f, owner.LegacyCoyoteTime);
        jumpBufferTime = Mathf.Max(0f, owner.LegacyJumpBufferTime);
        maxStamina = Mathf.Max(1f, owner.LegacyMaxStamina);
        currentStamina = Mathf.Clamp(owner.LegacyCurrentStamina, 0f, maxStamina);
        jumpStaminaCost = owner.LegacyJumpStaminaCost;
        rollStaminaCost = owner.LegacyRollStaminaCost;
        runStaminaCostPerSecond = owner.LegacyRunStaminaCostPerSecond;
        minimumStaminaToStartRun = owner.LegacyMinimumStaminaToStartRun;
        staminaRecoverPerSecond = owner.LegacyStaminaRecoverPerSecond;
    }

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

        if (animator != null)
        {
            animator.SetBool("IsGrounded", false);
            animator.SetTrigger("Jump");
        }

        verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        if (owner.AutoPlayActionSfx)
        {
            owner.PlayJumpSfxEvent();
        }
    }

    private Vector3 Move()
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
        Vector3 direction = transform.TransformDirection(inputX, 0f, inputY);

        bool wantsRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isWalk = direction.magnitude > 0.01f;

        if (!isWalk)
        {
            isRunning = false;
            if (animator != null)
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
        isRunning = wantsRun && hasEnoughStaminaToStartRun;
        if (isRunning)
        {
            ConsumeStaminaAllowPartial(runStaminaCostPerSecond * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetBool("IsRunning", isRunning);
        }

        UpdateMoveAnimationParams(inputX, inputY);
        return direction * (isRunning ? runSpeed : walkSpeed);
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
        rollDirection = transform.TransformDirection(localDirection).normalized;

        if (animator != null)
        {
            animator.SetFloat("RollX", inputX);
            animator.SetFloat("RollY", inputY);
            animator.SetTrigger("Roll");
        }

        isRunning = false;
        isWalk = false;

        if (owner.AutoPlayActionSfx)
        {
            owner.PlayRollSfxEvent();
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
        owner.LegacyCurrentStamina = currentStamina;
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
        owner.LegacyCurrentStamina = currentStamina;
        staminaConsumedThisFrame = true;
    }

    private void UpdateGroundedAnimationState()
    {
        if (controller != null && animator != null)
        {
            animator.SetBool("IsGrounded", controller.isGrounded);
        }

        isJumping = controller == null || !controller.isGrounded;
    }

    private void UpdateMovementAudio(bool movementBlocked)
    {
        if (!owner.AutoPlayFootstepSfx || movementBlocked || controller == null)
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

        float interval = Mathf.Max(0.05f, isRunning ? owner.RunFootstepInterval : owner.WalkFootstepInterval);
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
            owner.PlayRunFootstepSfxEvent();
        }
        else
        {
            owner.PlayWalkFootstepSfxEvent();
        }

        footstepTimer = interval;
    }

    private void ResetFootstepLoop()
    {
        footstepLoopActive = false;
        footstepTimer = 0f;
    }

    private void SyncOwnerState()
    {
        owner.SetMovementRuntimeState(isRunning, isRolling, isWalk);
        owner.LegacyCurrentStamina = currentStamina;
        owner.LegacyMaxStamina = maxStamina;
        owner.LegacyWalkSpeed = walkSpeed;
        owner.LegacyRunSpeed = runSpeed;
    }
}
