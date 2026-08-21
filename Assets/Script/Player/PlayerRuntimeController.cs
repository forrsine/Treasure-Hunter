using QFramework;
using UnityEngine;

/// <summary>
/// 玩家运行时装配控制器：负责连接 Unity 组件与 QFramework，不保存业务公式。
/// 它相当于“插线板”，每帧只安排组件执行顺序，移动、战斗、生命和成长仍由各自模块负责。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class PlayerRuntimeController : MonoBehaviour, IController
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayerPresentationComponent presentation;
    [SerializeField] private PlayerMovementComponent movement;
    [SerializeField] private PlayerCombatComponent combat;
    [SerializeField] private PlayerHealthComponent health;
    [SerializeField] private PlayerProgressionComponent progression;
    [SerializeField] private PlayerAudioComponent audioComponent;
    [SerializeField] private PlayerRangedAttackComponent rangedAttack;
    [SerializeField] private PlayerSkillCastComponent skillCaster;
    [SerializeField] private PlayerDeveloperModeComponent developerMode;

    private NCharacter entrySave;
    private CharacterDefine entryDefine;
    private bool initialized;

    public CharacterController CharacterController => characterController;
    public PlayerPresentationComponent Presentation => presentation;
    public PlayerAudioComponent Audio => audioComponent;
    public PlayerRangedAttackComponent RangedAttack => rangedAttack;
    public IPlayerStatsReadOnly Stats => this.GetModel<PlayerModel>().Stats;
    public NCharacter EntrySave => entrySave;
    public CharacterDefine EntryDefine => entryDefine;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// Awake 只做本对象的基础装配：
    /// 缓存组件、注册到运行时上下文，并初始化各功能组件之间的引用。
    /// 这里不读取存档，因为角色数据会在生成器完成装配后再传入。
    /// </summary>
    private void Awake()
    {
        CacheComponents();
        GameplayRuntime.Instance.RegisterPlayer(this);
        InitializeFeatureComponents();
    }

    /// <summary>
    /// OnEnable 负责注册局内事件。
    /// 经验和回血事件在这里监听，是为了让飘字逻辑跟随对象启用/禁用生命周期。
    /// </summary>
    private void OnEnable()
    {
        this.RegisterEvent<PlayerExperienceGainedEvent>(HandleExperienceGained);
        this.RegisterEvent<PlayerHealedEvent>(HandlePlayerHealed);
    }

    /// <summary>
    /// 玩家运行时的每帧主调度入口。
    /// 它本身不写移动、战斗公式，而是决定这一帧先处理什么、后处理什么：
    /// 先更新基础状态，再处理暂停/升级面板，再处理翻滚，再处理正常战斗和移动。
    /// </summary>
    private void Update()
    {
        if (!initialized || movement == null || combat == null || health == null)
        {
            return;
        }

        movement.BeginFrame();
        health.TickInvincibility();
        developerMode.Tick();

        if (Stats.IsUpgradeSelectionActive || Time.timeScale <= 0f)
        {
            health.TickHitFlash();
            return;
        }

        if (movement.TickRolling())
        {
            health.ApplyHealthRegen();
            movement.ApplyStaminaRecovery();
            health.TickHitFlash();
            return;
        }

        if (movement.TryStartRoll(combat.IsAttacking))
        {
            return;
        }

        skillCaster.Tick();
        combat.Tick();
        // 攻击不再阻塞水平移动，但会限制移动速度上限，避免边攻击边高速奔跑导致动作表现发飘。
        float horizontalSpeedLimit = combat.IsAttacking ? movement.AttackMoveSpeedLimit : -1f;
        movement.TickNormalMovement(false, combat.IsAttacking, horizontalSpeedLimit);
        health.ApplyHealthRegen();
        movement.ApplyStaminaRecovery();
        health.TickHitFlash();
    }

    /// <summary>
    /// 停用时解除事件并恢复闪避期间可能被关闭的渲染器。
    /// </summary>
    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerExperienceGainedEvent>(HandleExperienceGained);
        this.UnRegisterEvent<PlayerHealedEvent>(HandlePlayerHealed);

        if (health != null)
        {
            health.RestoreDodgeFlickerRenderers();
        }
    }

    /// <summary>
    /// 对象销毁时把自己从全局运行时上下文中移除。
    /// </summary>
    private void OnDestroy()
    {
        GameplayRuntime.Instance.UnregisterPlayer(this);
    }

    /// <summary>
    /// 生成器在职业模型实例化后调用。Animator、Renderer 和动画事件都在这里统一重新绑定。
    /// </summary>
    public void BindCharacterVisual(GameObject visualObject, CharacterDefine define)
    {
        entryDefine = define;
        presentation.BindVisual(visualObject, define);
        InitializeFeatureComponents();
    }

    /// <summary>
    /// 把服务端存档和职业配置交给 Command 初始化，控制器本身不直接修改属性。
    /// </summary>
    public void ApplyCharacterEntryData(NCharacter save, CharacterDefine define)
    {
        entrySave = save;
        entryDefine = define;
        this.SendCommand(new InitializePlayerCommand(save, define));
        movement.InitializeStamina();
        initialized = true;
    }

    public void AddExp(int amount) => this.SendCommand(new AddPlayerExpCommand(amount));
    public int Heal(int amount, bool showFloatingText = false) => this.SendCommand(new HealPlayerCommand(amount, showFloatingText));
    public void FullHeal() => this.SendCommand(new FullHealPlayerCommand());
    public bool CanSpendMana(int amount) => this.SendQuery(new CanSpendPlayerManaQuery(amount));
    public bool TrySpendMana(int amount) => this.SendCommand(new TrySpendPlayerManaCommand(amount));
    public int RestoreMana(int amount) => this.SendCommand(new RestorePlayerManaCommand(amount));
    public int FullRestoreMana() => this.SendCommand(new FullRestorePlayerManaCommand());

    /// <summary>
    /// 经验变化后显示经验飘字。
    /// 这里故意不在 System 里直接操作 UI，因为 System 不应该依赖 Unity 场景对象。
    /// </summary>
    private void HandleExperienceGained(PlayerExperienceGainedEvent e)
    {
        FloatingCombatText.ShowExperience(transform, e.Amount);
    }

    /// <summary>
    /// 回血事件的表现层入口。
    /// 只有开启了浮字展示时才显示治疗飘字，避免升级自动回血刷屏。
    /// </summary>
    private void HandlePlayerHealed(PlayerHealedEvent e)
    {
        if (e.ShowFloatingText && e.Amount > 0)
        {
            FloatingCombatText.ShowHealing(transform, e.Amount);
        }
    }

    /// <summary>
    /// 缓存并补齐运行时依赖组件。
    /// 这是混合架构里很关键的一步：Prefab 里可以手动拖引用，但即使漏拖也能自动兜住核心组件。
    /// </summary>
    private void CacheComponents()
    {
        characterController = characterController != null ? characterController : GetComponent<CharacterController>();
        presentation = EnsureComponent(presentation);
        movement = EnsureComponent(movement);
        combat = EnsureComponent(combat);
        health = EnsureComponent(health);
        progression = EnsureComponent(progression);
        audioComponent = EnsureComponent(audioComponent);
        rangedAttack = EnsureComponent(rangedAttack);
        skillCaster = EnsureComponent(skillCaster);
        developerMode = EnsureComponent(developerMode);
    }

    /// <summary>
    /// 把控制器自身作为依赖传给各功能组件。
    /// 这样组件依赖的是一个清晰的运行时入口，而不是反向抓取旧的巨型控制脚本。
    /// </summary>
    private void InitializeFeatureComponents()
    {
        movement.Initialize(this);
        combat.Initialize(this);
        rangedAttack.Initialize(this);
        health.Initialize(this);
        progression.Initialize(this);
        skillCaster.Initialize(this);
        developerMode.Initialize(this);
    }

    private T EnsureComponent<T>(T component) where T : Component
    {
        if (component == null)
        {
            component = GetComponent<T>();
        }

        return component != null ? component : gameObject.AddComponent<T>();
    }
}
