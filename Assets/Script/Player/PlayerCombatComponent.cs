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

    [SerializeField] private SphereCollider weaponCollider;
    [SerializeField] private float comboWindowTime = 0.8f;
    [SerializeField] private float secondToThirdComboReleaseDelayAfterResetEvent = 0.04f;
    [SerializeField] private float fullAttackTimeout = 4f;
    [SerializeField] private float animationEventFallbackTimeout = 1.25f;
    [SerializeField] private float eventlessFirstAttackHitboxDelay = 0.24f;
    [SerializeField] private float eventlessFirstAttackHitboxDuration = 0.22f;
    [SerializeField] private float eventlessFirstAttackSecondHitboxDelayAfterClose = 0.1f;
    [SerializeField] private float eventlessFirstAttackSecondHitboxDuration = 0.22f;
    [SerializeField] private float eventlessFirstAttackComboWindowDelay = 0.72f;
    [SerializeField] private float eventlessFirstAttackResetDelay = 1.05f;

    private PlayerPresentationComponent presentation;
    private PlayerAudioComponent audioComponent;
    private int currentCombo;
    private int attackHitWindowId;
    private float currentTimer;
    private float currentComboTimer;
    private float fallbackHitboxTimer;
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

    public bool IsAttacking => isAttacking;
    public int CurrentCombo => currentCombo;
    public int AttackHitWindowId => attackHitWindowId;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 绑定表现层、音效和武器碰撞盒。
    /// 组件本身不管职业差异，只要能拿到当前职业对应的 Animator / 音效适配即可。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        audioComponent = player != null ? player.Audio : GetComponent<PlayerAudioComponent>();

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
        TickFallbackHitbox();
        if (isAttacking)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
            {
                ResetCombo();
            }
        }

        UpdateComboTimer();
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
        return roll.Damage;
    }

    /// <summary>
    /// 上报本次已经真实命中的伤害，主要给吸血等后结算逻辑使用。
    /// </summary>
    public int HandleDamageDealt(int appliedDamage)
    {
        return this.SendCommand(new RecordPlayerDamageDealtCommand(appliedDamage));
    }

    public void ResetRuntimeBuffers()
    {
        this.GetSystem<PlayerCombatSystem>().ResetRuntimeBuffers();
    }

    private void CheckAttackInput()
    {
        if (presentation != null && presentation.IsSkillAnimationPlaying)
        {
            return;
        }

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || !input.LeftMouseDown)
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
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = GetAttackTimeout();

        PlayAttackPresentation();
    }

    /// <summary>
    /// 进入下一段连击。
    /// </summary>
    private void TriggerNextCombo()
    {
        ClearScriptedFallbackAnimationEvents();
        fallbackHitboxTimer = 0f;
        ForceWeaponDisable();

        currentCombo++;
        canComboNext = false;
        queuedThirdComboAfterSecondAttack = false;
        queuedThirdComboReleaseTimer = -1f;
        currentTimer = GetAttackTimeout();
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
        StartFallbackHitboxIfNeeded();
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
        if (presentation == null)
        {
            return;
        }

        if (!presentation.UsesCombatAnimationEvents)
        {
            WeaponEnable();
            fallbackHitboxTimer = Mathf.Max(0.1f, presentation.BasicAttackDuration * 0.45f);
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

            WeaponDisable();
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
        return presentation != null && presentation.IsSkillAnimationPlaying;
    }

    private void ForceWeaponDisable()
    {
        if (weaponCollider != null)
        {
            weaponCollider.enabled = false;
        }
    }
}
