using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家战斗表现组件：负责攻击输入、连击时序和攻击碰撞盒。
/// 伤害、暴击与吸血公式全部通过 Command 交给 PlayerCombatSystem，四个职业只需适配动画。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatComponent : MonoBehaviour, IController
{
    private const int MaxCombo = 3;
    private const int ChargedAreaOverlapCapacity = 64;
    private const float ProjectileReleaseRetryDelay = 0.05f;
    private const string ArcherClassKey = "Archer";

    [SerializeField] private SphereCollider weaponCollider;
    [SerializeField] private float comboWindowTime = 0.8f;
    [SerializeField] private float secondToThirdComboReleaseDelayAfterResetEvent = 0.04f;
    [SerializeField] private float fullAttackTimeout = 4f;
    [SerializeField] private float animationEventFallbackTimeout = 1.25f;
    [SerializeField, Range(0f, 1f)] private float eventlessAttackHitboxDelayRatio = 0.25f;
    [SerializeField, Range(0.05f, 1f)] private float eventlessAttackHitboxDurationRatio = 0.35f;
    [SerializeField] private float eventlessFirstAttackHitboxDelay = 0.24f;
    [SerializeField] private float eventlessFirstAttackHitboxDuration = 0.22f;
    [SerializeField] private float eventlessFirstAttackSecondHitboxDelayAfterClose = 0.1f;
    [SerializeField] private float eventlessFirstAttackSecondHitboxDuration = 0.22f;
    [SerializeField] private float eventlessFirstAttackComboWindowDelay = 0.72f;
    [SerializeField] private float eventlessFirstAttackResetDelay = 1.05f;

    private PlayerPresentationComponent presentation;
    private PlayerAudioComponent audioComponent;
    private PlayerRangedAttackComponent rangedAttack;
    private PlayerChargedAttackComponent chargedAttack;
    private PlayerSkillCastComponent skillCaster;
    private CharacterAnimationStyle animationStyle;
    private CharacterBasicAttackType basicAttackType;
    private bool isArcherBasicAttack;
    private float basicAttackDuration = 0.7f;
    private float projectileReleaseRatio = 0.5f;
    private int currentCombo;
    private int attackHitWindowId;
    private float currentTimer;
    private float currentComboTimer;
    private float fallbackHitboxTimer;
    private float eventlessAttackReleaseDelayTimer = -1f;
    private int scriptedFallbackCombo;
    private float scriptedHitboxDelayTimer = -1f;
    private float scriptedSecondHitboxDelayTimer = -1f;
    private float scriptedComboWindowTimer = -1f;
    private float scriptedResetTimer = -1f;
    private float queuedThirdComboReleaseTimer = -1f;
    private int activeScriptedHitboxWindow;
    private bool shouldOpenSecondScriptedHitboxAfterFirstClose;
    private bool isAttacking;
    private bool canComboNext;
    private bool queuedThirdComboAfterSecondAttack;
    private float archerBasicAttackCooldownRemaining;
    private int nextAttackToken;
    private int activeAttackToken;
    private int releasedProjectileAttackToken = -1;
    private int projectileReleaseFailureLoggedToken = -1;
    private bool isControlledBasicAttack;
    private float activeBasicAttackDamageMultiplier = 1f;
    private float activeControlledAttackAreaRadius;
    // 满蓄力范围扫描复用固定缓冲和目标集合，避免每次重斩产生 GC，同时按 FighterInterface 去重。
    private readonly Collider[] chargedAreaOverlapBuffer = new Collider[ChargedAreaOverlapCapacity];
    private readonly HashSet<FighterInterface> chargedAreaHitTargets = new HashSet<FighterInterface>();

    public bool IsAttacking => isAttacking;
    public int CurrentCombo => currentCombo;
    public int AttackHitWindowId => attackHitWindowId;
    public int AttackCollisionLayer => weaponCollider != null
        ? weaponCollider.gameObject.layer
        : gameObject.layer;
    public bool IsProjectileBasicAttack => basicAttackType == CharacterBasicAttackType.Projectile;
    public float ActiveBasicAttackDamageMultiplier => activeBasicAttackDamageMultiplier;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 绑定表现层、音效和武器碰撞盒。
    /// 组件本身不管职业差异，只要能拿到当前职业对应的 Animator / 音效适配即可。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        audioComponent = player != null ? player.Audio : GetComponent<PlayerAudioComponent>();
        rangedAttack = player != null ? player.RangedAttack : GetComponent<PlayerRangedAttackComponent>();
        chargedAttack = player != null ? player.ChargedAttack : GetComponent<PlayerChargedAttackComponent>();
        skillCaster = player != null ? player.GetComponent<PlayerSkillCastComponent>() : GetComponent<PlayerSkillCastComponent>();

        CharacterDefine define = player != null ? player.EntryDefine : null;
        animationStyle = define != null
            ? define.animationStyle
            : CharacterAnimationStyle.DirectionalCombo;
        basicAttackType = define != null
            ? define.basicAttackType
            : CharacterBasicAttackType.Melee;
        basicAttackDuration = define != null && define.basicAttackDuration > 0f
            ? define.basicAttackDuration
            : 0.7f;
        projectileReleaseRatio = define != null
            ? Mathf.Clamp01(define.projectileReleaseRatio)
            : 0.5f;
        isArcherBasicAttack =
            define != null &&
            (define.classId == 3 ||
             string.Equals(define.classKey, ArcherClassKey, System.StringComparison.OrdinalIgnoreCase));
        archerBasicAttackCooldownRemaining = 0f;
        isControlledBasicAttack = false;
        activeBasicAttackDamageMultiplier = 1f;
        activeControlledAttackAreaRadius = 0f;

        if (weaponCollider == null)
        {
            WeaponCo weapon = GetComponentInChildren<WeaponCo>(true);
            weaponCollider = weapon != null ? weapon.GetComponent<SphereCollider>() : null;
        }

        WeaponDisable();
    }

    /// <summary>
    /// 旧脚本兼容入口。
    /// 运行时真正使用的仍然是 PlayerRuntimeController 初始化链路。
    /// </summary>
    public void Initialize(MonoBehaviour obsoleteOwner)
    {
        Initialize(GetComponent<PlayerRuntimeController>());
    }

    /// <summary>
    /// 战斗组件的每帧入口。
    /// 先推进兜底碰撞盒计时，再处理攻击超时与连击窗口，最后才读取新的攻击输入。
    /// </summary>
    public void Tick()
    {
        TickScriptedFallbackAnimationEvents();
        TickQueuedThirdComboRelease();
        TickEventlessAttackReleaseDelay(Time.deltaTime);
        TickFallbackHitbox();
        if (isAttacking)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
            {
                if (isControlledBasicAttack)
                {
                    isControlledBasicAttack = false;
                }

                ResetCombo();
            }
        }

        UpdateComboTimer();
        TickArcherBasicAttackCooldown(Time.deltaTime);
        CheckAttackInput();
    }

    /// <summary>
    /// 动画事件调用：开放下一段连击输入窗口。
    /// 只有窗口打开时再次点击，才能续上下一段连击。
    /// </summary>
    public void OpenComboWindow()
    {
        if (ShouldIgnoreCombatAnimationEvent())
        {
            return;
        }

        canComboNext = true;
        currentComboTimer = comboWindowTime;
    }

    /// <summary>
    /// 重置连击状态。
    /// 当攻击超时、连击窗口结束或动画主动收尾时，都会回到这个统一入口。
    /// </summary>
    public void ResetCombo()
    {
        if (ShouldIgnoreCombatAnimationEvent())
        {
            return;
        }

        if (HasPendingProjectileRelease())
        {
            // 第三方远程动画可能在 shoot 事件之前提前发送 ResetCombo。
            // 此时保留攻击序号和计时任务，箭矢释放后仍会由攻击超时正常收尾。
            return;
        }

        if (TryScheduleQueuedThirdComboAfterSecondAttack())
        {
            return;
        }

        currentCombo = 0;
        isAttacking = false;
        canComboNext = false;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = 0f;
        fallbackHitboxTimer = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        activeAttackToken = 0;
        isControlledBasicAttack = false;
        activeBasicAttackDamageMultiplier = 1f;
        activeControlledAttackAreaRadius = 0f;
        ClearScriptedFallbackAnimationEvents();
        WeaponDisable();

        if (presentation != null)
        {
            presentation.SetCombo(0);
        }
    }

    /// <summary>
    /// 开启武器碰撞盒。
    /// 正常情况下由动画事件控制，少数职业资源没有事件时则由代码兜底开启。
    /// 每开启一次都视为新的攻击判定窗口，WeaponCo 会据此允许同一敌人在下一段攻击重新受伤。
    /// </summary>
    public void WeaponEnable()
    {
        if (ShouldIgnoreCombatAnimationEvent())
        {
            return;
        }

        // 远程职业的旧动画资源可能把释放点命名为 WeaponEnable。
        // 统一转成投射物释放，避免意外打开玩家身上的近战碰撞盒。
        if (IsProjectileBasicAttack)
        {
            TryReleaseRangedBasicAttack();
            return;
        }

        OpenWeaponHitWindow();
    }

    /// <summary>
    /// 关闭武器碰撞盒，避免待机状态也持续造成伤害。
    /// </summary>
    public void WeaponDisable()
    {
        if (ShouldIgnoreCombatAnimationEvent())
        {
            return;
        }

        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }

    /// <summary>
    /// 技能动画会复用 Atk3 动作，但技能伤害由 PlayerSkillCastComponent 的范围检测负责。
    /// 因此技能开始前要取消普通攻击状态，避免 Atk3 动画事件又打开普通攻击碰撞盒。
    /// </summary>
    public void CancelAttackForSkill()
    {
        currentCombo = 0;
        isAttacking = false;
        canComboNext = false;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = 0f;
        currentComboTimer = 0f;
        fallbackHitboxTimer = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        activeAttackToken = 0;
        isControlledBasicAttack = false;
        activeBasicAttackDamageMultiplier = 1f;
        activeControlledAttackAreaRadius = 0f;
        ClearScriptedFallbackAnimationEvents();
        ForceWeaponDisable();
    }

    /// <summary>
    /// 向战斗系统请求一次攻击伤害掷骰。
    /// 组件只拿结果，不在这里写公式。
    /// </summary>
    public int RollAttackDamage(out bool isCritical)
    {
        PlayerAttackRoll roll = this.SendCommand(new RollPlayerAttackCommand());
        isCritical = roll.IsCritical;
        // 蓄力倍率在暴击掷骰完成后统一应用，所以满蓄力暴击能同时获得两种加成。
        return Mathf.Max(1, Mathf.RoundToInt(roll.Damage * activeBasicAttackDamageMultiplier));
    }

    /// <summary>
    /// 蓄力组件开始受控普攻：立即播放前摇，但暂不安排攻击盒。
    /// currentTimer 使用正无穷，确保玩家满蓄力后继续保持姿势时不会被普通攻击超时打断。
    /// </summary>
    public bool BeginControlledBasicAttack()
    {
        if (isAttacking || IsProjectileBasicAttack)
        {
            return false;
        }

        ClearScriptedFallbackAnimationEvents();
        ForceWeaponDisable();
        fallbackHitboxTimer = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        canComboNext = false;

        isControlledBasicAttack = true;
        activeBasicAttackDamageMultiplier = 1f;
        activeControlledAttackAreaRadius = 0f;
        isAttacking = true;
        currentCombo = 1;
        activeAttackToken = ++nextAttackToken;
        currentTimer = float.PositiveInfinity;
        PlayAttackPresentation();
        return true;
    }

    /// <summary>
    /// 松开蓄力后恢复普通攻击超时，并按配置延迟结算一次攻击。
    /// areaRadius 大于 0 时改用圆形扫描并保持武器盒关闭；否则沿用原近战攻击盒。
    /// 倍率会保持到本次攻击结束，保证范围内每个目标使用同一份蓄力规则。
    /// </summary>
    public bool ReleaseControlledBasicAttack(
        float damageMultiplier,
        float hitDelay,
        float areaRadius = 0f)
    {
        if (!isControlledBasicAttack || !isAttacking || currentCombo != 1)
        {
            return false;
        }

        activeBasicAttackDamageMultiplier = Mathf.Max(1f, damageMultiplier);
        activeControlledAttackAreaRadius = Mathf.Max(0f, areaRadius);
        ForceWeaponDisable();
        currentTimer = GetAttackTimeout();
        eventlessAttackReleaseDelayTimer = Mathf.Max(0f, hitDelay);
        if (eventlessAttackReleaseDelayTimer <= 0f)
        {
            ResolveEventlessAttackRelease();
        }

        return true;
    }

    /// <summary>
    /// 取消尚未结算的受控普攻，供死亡、暂停、升级和对象禁用统一清理状态。
    /// </summary>
    public void CancelControlledBasicAttack()
    {
        if (!isControlledBasicAttack && !isAttacking)
        {
            return;
        }

        isControlledBasicAttack = false;
        ResetCombo();
    }

    /// <summary>
    /// 上报本次已经真实命中的伤害，主要给吸血等后结算逻辑使用。
    /// </summary>
    public int HandleDamageDealt(int appliedDamage)
    {
        return this.SendCommand(new RecordPlayerDamageDealtCommand(appliedDamage));
    }

    /// <summary>
    /// 远程普攻的统一释放入口。动画事件和代码计时都会调用这里，
    /// 但同一个攻击序号只允许成功生成一个投射物，避免同一帧重复发射。
    /// </summary>
    public bool TryReleaseRangedBasicAttack()
    {
        if (!IsProjectileBasicAttack ||
            !isAttacking ||
            currentCombo != 1 ||
            activeAttackToken <= 0 ||
            releasedProjectileAttackToken == activeAttackToken ||
            rangedAttack == null)
        {
            return false;
        }

        PlayerBasicAttackProjectile projectile = rangedAttack.Fire();
        if (projectile == null)
        {
            if (projectileReleaseFailureLoggedToken != activeAttackToken)
            {
                projectileReleaseFailureLoggedToken = activeAttackToken;
                Debug.LogWarning("远程普通攻击发射失败：投射物组件尚未完成职业配置，将在本次攻击结束前继续重试。", this);
            }

            eventlessAttackReleaseDelayTimer = ProjectileReleaseRetryDelay;
            return false;
        }

        // 只有真正从对象池取得投射物后才消费攻击序号。
        // 如果动画事件发生得过早而初始化尚未完成，代码计时仍有机会再次兜底。
        releasedProjectileAttackToken = activeAttackToken;
        eventlessAttackReleaseDelayTimer = -1f;
        attackHitWindowId++;

        if (audioComponent != null && audioComponent.AutoPlayActions)
        {
            audioComponent.PlayAttack(currentCombo);
        }

        return true;
    }

    public void ResetRuntimeBuffers()
    {
        this.GetSystem<PlayerCombatSystem>().ResetRuntimeBuffers();
    }

    private void CheckAttackInput()
    {
        if (skillCaster != null && skillCaster.IsCommittedCastActive)
        {
            return;
        }

        if (presentation != null && presentation.IsSkillAnimationPlaying)
        {
            return;
        }

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null)
        {
            return;
        }

        // 配置了蓄力机制的职业优先接管左键；未配置职业返回 false 后继续走原有流程。
        if (chargedAttack != null && chargedAttack.TryHandleBasicAttackInput(input))
        {
            return;
        }

        // 弓箭手允许按住左键持续攻击，但每一箭都必须经过职业攻击时长限制。
        // 冷却不会因为快速连点而重置，因此玩家无法通过高频点击绕过攻速配置。
        if (isArcherBasicAttack)
        {
            if (!input.LeftMouseHeld || archerBasicAttackCooldownRemaining > 0f)
            {
                return;
            }

            StartImmediateArcherAttack();
            archerBasicAttackCooldownRemaining = Mathf.Max(0.01f, basicAttackDuration);
            return;
        }

        if (!input.LeftMouseDown)
        {
            return;
        }

        if (currentCombo == 0 && !isAttacking)
        {
            StartFirstAttack();
        }
        else if (canComboNext && currentCombo < MaxCombo)
        {
            if (currentCombo == 2)
            {
                QueueThirdComboAfterSecondAttack();
            }
            else
            {
                TriggerNextCombo();
            }
        }
    }

    /// <summary>
    /// 开始第一段普通攻击。
    /// </summary>
    private void StartFirstAttack()
    {
        isAttacking = true;
        currentCombo = 1;
        activeAttackToken = ++nextAttackToken;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = GetAttackTimeout();

        // 先建立伤害/投射物释放任务，再请求 Animator 播放表现。
        // 这样即使第三方动画状态或 Animation Event 异常，普通攻击仍能按配置结算。
        StartFallbackHitboxIfNeeded();
        PlayAttackPresentation();
    }

    /// <summary>
    /// 弓箭手即时普攻入口：每次通过攻速检查后都重新播放动作并在同一帧发射一支箭。
    /// 技能动画已在 CheckAttackInput 前置过滤，因此这里不会打断技能释放。
    /// </summary>
    private void StartImmediateArcherAttack()
    {
        ClearScriptedFallbackAnimationEvents();
        ForceWeaponDisable();
        fallbackHitboxTimer = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        canComboNext = false;

        isAttacking = true;
        currentCombo = 1;
        activeAttackToken = ++nextAttackToken;
        currentTimer = GetAttackTimeout();
        PlayAttackPresentation();
        TryReleaseRangedBasicAttack();
    }

    /// <summary>
    /// 推进弓箭手普通攻击间隔。
    /// 使用独立计时而不是依赖动画是否播放完成，保证长按和快速点击遵守同一套攻速规则。
    /// </summary>
    private void TickArcherBasicAttackCooldown(float deltaTime)
    {
        if (archerBasicAttackCooldownRemaining <= 0f)
        {
            return;
        }

        archerBasicAttackCooldownRemaining = Mathf.Max(
            0f,
            archerBasicAttackCooldownRemaining - Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// 进入下一段连击。
    /// </summary>
    private void TriggerNextCombo()
    {
        ClearScriptedFallbackAnimationEvents();
        fallbackHitboxTimer = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        ForceWeaponDisable();

        currentCombo++;
        activeAttackToken = ++nextAttackToken;
        canComboNext = false;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = GetAttackTimeout();
        StartFallbackHitboxIfNeeded();
        PlayAttackPresentation();
    }

    /// <summary>
    /// 第二段攻击 Atk1 的连击输入只做缓存，不立刻切到 Atk2。
    /// 这样玩家可以提前按第三下，但动作会等 Atk1 末尾的 ResetCombo 事件到来后再进入第三段。
    /// </summary>
    private void QueueThirdComboAfterSecondAttack()
    {
        queuedThirdComboAfterSecondAttack = true;
        canComboNext = false;
        currentComboTimer = 0f;
        queuedThirdComboReleaseTimer = -1f;
    }

    private bool TryScheduleQueuedThirdComboAfterSecondAttack()
    {
        if (!queuedThirdComboAfterSecondAttack || currentCombo != 2 || !isAttacking)
        {
            return false;
        }

        queuedThirdComboReleaseTimer = Mathf.Max(0f, secondToThirdComboReleaseDelayAfterResetEvent);
        if (queuedThirdComboReleaseTimer <= 0f)
        {
            ConsumeQueuedThirdComboAfterSecondAttack();
        }

        return true;
    }

    private void TickQueuedThirdComboRelease()
    {
        if (queuedThirdComboReleaseTimer < 0f)
        {
            return;
        }

        if (!queuedThirdComboAfterSecondAttack || currentCombo != 2 || !isAttacking)
        {
            queuedThirdComboAfterSecondAttack = false;
            queuedThirdComboReleaseTimer = -1f;
            return;
        }

        queuedThirdComboReleaseTimer -= Time.deltaTime;
        if (queuedThirdComboReleaseTimer <= 0f)
        {
            ConsumeQueuedThirdComboAfterSecondAttack();
        }
    }

    private void ConsumeQueuedThirdComboAfterSecondAttack()
    {
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        TriggerNextCombo();
    }

    /// <summary>
    /// 将当前连击阶段翻译成具体表现层动作。
    /// 不同职业的 Animator 参数不同，所以统一交给 Presentation 适配。
    /// </summary>
    private void PlayAttackPresentation()
    {
        if (presentation == null)
        {
            Debug.LogWarning("PlayerCombatComponent 缺少 PlayerPresentationComponent，攻击逻辑已触发但无法播放攻击动画。", this);
            return;
        }

        presentation.SetCombo(currentCombo);
    }

    /// <summary>
    /// 推进连击窗口倒计时。
    /// 玩家错过窗口后会自动重置，避免无限续连。
    /// </summary>
    private void UpdateComboTimer()
    {
        if (!canComboNext)
        {
            return;
        }

        currentComboTimer -= Time.deltaTime;
        if (currentComboTimer <= 0f)
        {
            ResetCombo();
        }
    }

    /// <summary>
    /// 没有武器开关动画事件的职业，使用短计时窗口启用公共攻击盒。
    /// </summary>
    private void StartFallbackHitboxIfNeeded()
    {
        bool usesCombatAnimationEvents = presentation != null
            ? presentation.UsesCombatAnimationEvents
            : animationStyle == CharacterAnimationStyle.DirectionalCombo;
        if (!usesCombatAnimationEvents)
        {
            // 简单职业统一使用代码兜底：近战在动作中段开攻击盒，
            // 远程的动画 shoot 事件与代码计时使用同一个配置释放点，攻击序号负责重复保护。
            ForceWeaponDisable();
            fallbackHitboxTimer = 0f;
            float releaseRatio = IsProjectileBasicAttack
                ? projectileReleaseRatio
                : eventlessAttackHitboxDelayRatio;
            float attackDuration = presentation != null
                ? presentation.BasicAttackDuration
                : Mathf.Max(0.1f, basicAttackDuration);
            eventlessAttackReleaseDelayTimer = Mathf.Max(
                0f,
                attackDuration * releaseRatio);

            if (eventlessAttackReleaseDelayTimer <= 0f)
            {
                ResolveEventlessAttackRelease();
            }
            return;
        }

        // ATK4 目前没有动画事件，第一段攻击由脚本模拟“开碰撞、开连击窗口、收尾”。
        // ATK4 动作里有两次镰刀挥舞，所以这里会开启两个独立攻击窗口，保证同一敌人能吃到两次伤害。
        // 后两段 Atk1/Atk2 已经有动画事件，仍然交给动画资源驱动，打击点会更贴合动作。
        if (currentCombo == 1)
        {
            ScheduleScriptedFirstAttackEvents();
        }
    }

    private void ScheduleScriptedFirstAttackEvents()
    {
        scriptedFallbackCombo = currentCombo;
        scriptedHitboxDelayTimer = Mathf.Max(0f, eventlessFirstAttackHitboxDelay);
        scriptedSecondHitboxDelayTimer = -1f;
        scriptedComboWindowTimer = Mathf.Max(0f, eventlessFirstAttackComboWindowDelay);
        scriptedResetTimer = Mathf.Max(GetEventlessFirstAttackMinimumResetDelay(), eventlessFirstAttackResetDelay);
        activeScriptedHitboxWindow = 0;
        shouldOpenSecondScriptedHitboxAfterFirstClose = false;

        if (scriptedHitboxDelayTimer <= 0f)
        {
            TriggerFirstScriptedHitbox();
        }

        if (scriptedComboWindowTimer <= 0f)
        {
            TriggerScriptedComboWindow();
        }
    }

    private void TickScriptedFallbackAnimationEvents()
    {
        if (scriptedFallbackCombo <= 0)
        {
            return;
        }

        if (!isAttacking || currentCombo != scriptedFallbackCombo)
        {
            ClearScriptedFallbackAnimationEvents();
            return;
        }

        if (scriptedHitboxDelayTimer > 0f)
        {
            scriptedHitboxDelayTimer -= Time.deltaTime;
            if (scriptedHitboxDelayTimer <= 0f)
            {
                TriggerFirstScriptedHitbox();
            }
        }

        if (scriptedSecondHitboxDelayTimer > 0f)
        {
            scriptedSecondHitboxDelayTimer -= Time.deltaTime;
            if (scriptedSecondHitboxDelayTimer <= 0f)
            {
                TriggerSecondScriptedHitbox();
            }
        }

        if (scriptedComboWindowTimer > 0f)
        {
            scriptedComboWindowTimer -= Time.deltaTime;
            if (scriptedComboWindowTimer <= 0f)
            {
                TriggerScriptedComboWindow();
            }
        }

        if (scriptedResetTimer > 0f)
        {
            scriptedResetTimer -= Time.deltaTime;
            if (scriptedResetTimer <= 0f && isAttacking && currentCombo == scriptedFallbackCombo)
            {
                ResetCombo();
            }
        }
    }

    /// <summary>
    /// 推进简单动画职业的攻击释放计时。
    /// 战士在动作中段开启一次近战判定；弓箭手和法师在动画事件丢失时补发一次投射物。
    /// </summary>
    private void TickEventlessAttackReleaseDelay(float deltaTime)
    {
        if (eventlessAttackReleaseDelayTimer < 0f)
        {
            return;
        }

        bool usesCombatAnimationEvents = presentation != null
            ? presentation.UsesCombatAnimationEvents
            : animationStyle == CharacterAnimationStyle.DirectionalCombo;
        if (!isAttacking || usesCombatAnimationEvents)
        {
            eventlessAttackReleaseDelayTimer = -1f;
            return;
        }

        eventlessAttackReleaseDelayTimer -= Mathf.Max(0f, deltaTime);
        if (eventlessAttackReleaseDelayTimer <= 0f)
        {
            ResolveEventlessAttackRelease();
        }
    }

    private void ResolveEventlessAttackRelease()
    {
        eventlessAttackReleaseDelayTimer = -1f;
        if (IsProjectileBasicAttack)
        {
            TryReleaseRangedBasicAttack();
            return;
        }

        if (isControlledBasicAttack && activeControlledAttackAreaRadius > 0f)
        {
            ResolveControlledAreaAttack();
            return;
        }

        BeginEventlessAttackHitbox();
    }

    /// <summary>
    /// 满蓄力旋转重斩只在延迟到达时扫描一次圆形范围。
    /// 这里不打开武器 Collider，防止同一个目标同时被范围扫描和物理攻击盒重复结算。
    /// </summary>
    private void ResolveControlledAreaAttack()
    {
        float areaRadius = activeControlledAttackAreaRadius;
        activeControlledAttackAreaRadius = 0f;
        eventlessAttackReleaseDelayTimer = -1f;
        ForceWeaponDisable();
        attackHitWindowId++;

        Physics.SyncTransforms();
        PlayerBasicAttackDamageResolver.ApplyInRadius(
            this,
            transform.position,
            areaRadius,
            transform,
            null,
            chargedAreaOverlapBuffer,
            chargedAreaHitTargets);
        chargedAreaHitTargets.Clear();
        PlayerChargedSpinEffect.Play(transform.position, areaRadius);

        if (audioComponent != null && audioComponent.AutoPlayActions)
        {
            audioComponent.PlayAttack(currentCombo);
        }
    }

    private bool HasPendingProjectileRelease()
    {
        return IsProjectileBasicAttack &&
               isAttacking &&
               currentTimer > 0f &&
               eventlessAttackReleaseDelayTimer >= 0f &&
               activeAttackToken > 0 &&
               releasedProjectileAttackToken != activeAttackToken;
    }

    private void BeginEventlessAttackHitbox()
    {
        eventlessAttackReleaseDelayTimer = -1f;
        // 受控普攻会屏蔽动画事件，但松手后的代码计时仍必须能打开攻击盒。
        OpenWeaponHitWindow();
        fallbackHitboxTimer = Mathf.Max(
            0.05f,
            presentation != null
                ? presentation.BasicAttackDuration * eventlessAttackHitboxDurationRatio
                : 0.2f);
    }

    private void TriggerFirstScriptedHitbox()
    {
        scriptedHitboxDelayTimer = -1f;
        activeScriptedHitboxWindow = 1;
        shouldOpenSecondScriptedHitboxAfterFirstClose = true;
        WeaponEnable();
        fallbackHitboxTimer = Mathf.Max(0.05f, eventlessFirstAttackHitboxDuration);
    }

    private void TriggerSecondScriptedHitbox()
    {
        scriptedSecondHitboxDelayTimer = -1f;
        activeScriptedHitboxWindow = 2;
        shouldOpenSecondScriptedHitboxAfterFirstClose = false;
        WeaponEnable();

        // 第二次挥镰已经进入最后一个攻击窗口，提前清掉 Animator 参数，避免 Atk4 回 Empty 后又被 Any State 拉回 Atk4。
        if (presentation != null)
        {
            presentation.ClearComboIndexForScriptedAttack();
        }

        fallbackHitboxTimer = Mathf.Max(0.05f, eventlessFirstAttackSecondHitboxDuration);
    }

    private void TriggerScriptedComboWindow()
    {
        scriptedComboWindowTimer = -1f;
        OpenComboWindow();
    }

    private void ClearScriptedFallbackAnimationEvents()
    {
        scriptedFallbackCombo = 0;
        scriptedHitboxDelayTimer = -1f;
        scriptedSecondHitboxDelayTimer = -1f;
        scriptedComboWindowTimer = -1f;
        scriptedResetTimer = -1f;
        activeScriptedHitboxWindow = 0;
        shouldOpenSecondScriptedHitboxAfterFirstClose = false;
    }

    private void TickFallbackHitbox()
    {
        if (fallbackHitboxTimer <= 0f)
        {
            return;
        }

        fallbackHitboxTimer -= Time.deltaTime;
        if (fallbackHitboxTimer <= 0f)
        {
            bool finishedSecondScriptedHitbox = activeScriptedHitboxWindow == 2 &&
                scriptedFallbackCombo > 0 &&
                currentCombo == scriptedFallbackCombo;

            // 代码计时属于战斗组件内部收尾，不能被“忽略动画事件”的受控蓄力状态挡住。
            ForceWeaponDisable();
            if (finishedSecondScriptedHitbox && presentation != null)
            {
                presentation.FadeOutAttackLayerAfterScriptedAttack();
            }

            TryScheduleSecondScriptedHitboxAfterFirstClose();
        }
    }

    private void TryScheduleSecondScriptedHitboxAfterFirstClose()
    {
        if (activeScriptedHitboxWindow != 1 || !shouldOpenSecondScriptedHitboxAfterFirstClose)
        {
            activeScriptedHitboxWindow = 0;
            return;
        }

        activeScriptedHitboxWindow = 0;
        shouldOpenSecondScriptedHitboxAfterFirstClose = false;

        if (!isAttacking || currentCombo != scriptedFallbackCombo)
        {
            return;
        }

        scriptedSecondHitboxDelayTimer = Mathf.Max(0f, eventlessFirstAttackSecondHitboxDelayAfterClose);
        if (scriptedSecondHitboxDelayTimer <= 0f)
        {
            TriggerSecondScriptedHitbox();
        }
    }

    private float GetEventlessFirstAttackMinimumResetDelay()
    {
        return Mathf.Max(0.1f,
            eventlessFirstAttackHitboxDelay +
            eventlessFirstAttackHitboxDuration +
            eventlessFirstAttackSecondHitboxDelayAfterClose +
            eventlessFirstAttackSecondHitboxDuration +
            0.1f);
    }

    private float GetAttackTimeout()
    {
        return presentation != null && !presentation.UsesCombatAnimationEvents
            ? presentation.BasicAttackDuration
            : Mathf.Clamp(animationEventFallbackTimeout, 0.3f, Mathf.Max(0.3f, fullAttackTimeout));
    }

    private bool ShouldIgnoreCombatAnimationEvent()
    {
        return isControlledBasicAttack ||
               (presentation != null && presentation.IsSkillAnimationPlaying);
    }

    private void OpenWeaponHitWindow()
    {
        attackHitWindowId++;

        if (weaponCollider != null)
        {
            weaponCollider.enabled = true;
        }

        if (audioComponent != null && audioComponent.AutoPlayActions)
        {
            audioComponent.PlayAttack(currentCombo);
        }
    }

    private void ForceWeaponDisable()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }
}
