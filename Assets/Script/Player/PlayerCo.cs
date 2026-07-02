using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 玩家总控制器。
/// 
/// 新手阅读顺序：
/// 1. Awake 缓存组件，并自动挂上运行时 UI 脚本。
/// 2. Start 从 GameConfig 初始化基础属性、等级、血量和经验。
/// 3. Update 是主循环：处理升级面板暂停、翻滚、攻击、跳跃、移动、回血和受击颜色恢复。
/// 4. Hit / RollAttackDamage / HandleDamageDealt 负责战斗伤害、闪避、减伤、暴击、吸血。
/// 5. AddExp / DoLevelUp / TryApplyAttributeUpgrade 负责经验、升级和属性三选一。
/// 6. GetAttributePanelEntries 给属性面板提供要显示的数据。
/// </summary>
public class PlayerCo : MonoBehaviour, FighterInterface
{
    /// <summary>
    /// 属性面板里的一行数据。
    /// 
    /// 新手理解：
    /// 这个 struct 只装数据，不负责显示 UI。
    /// PlayerCo 把属性整理成一行行 AttributePanelEntry，
    /// PlayerAttributePanel 再根据这些数据创建文本。
    /// </summary>
    public struct AttributePanelEntry
    {
        // 分组名决定这一行出现在属性面板的哪个栏目下。
        public string GroupName;
        // Key 是稳定标识，用来复用已有 UI 行并判断数值是否变化。
        public string Key;
        // Label 是玩家看到的属性名称。
        public string Label;
        // Value 是已经格式化好的属性值文本。
        public string Value;

        public AttributePanelEntry(string groupName, string key, string label, string value)
        {
            GroupName = groupName;
            Key = key;
            Label = label;
            Value = value;
        }
    }

    // 最大连击段数。当前动画参数 ComboIndex 只设计了 1、2、3 段。
    private const int MaxCombo = 3;

    // CharacterController 贴地时给一个小的负速度，避免角色在地面边缘抖动。
    private const float GroundedVerticalVelocity = -2f;

    // 判断“是否还可以继续升级”的极小阈值，避免浮点误差导致到上限后还显示可升级。
    private const float MinUpgradeableThreshold = 0.0001f;

    // Reused buffer for weighted upgrade choice generation to avoid extra allocations every level-up.
    private static readonly List<PlayerAttributeType> UpgradeChoiceBuffer = new List<PlayerAttributeType>(8);

    public static PlayerCo instance;

    // 属性变化事件：属性面板订阅它，玩家数值变化时自动刷新 UI。
    public event Action StatsChanged;

    // 待选择升级数量变化事件：升级三选一面板订阅它。
    public event Action<int> PendingUpgradeSelectionsChanged;

    [Header("Player Feature Components")]
    [SerializeField] private PlayerMovementComponent movementComponent;
    [SerializeField] private PlayerCombatComponent combatComponent;
    [SerializeField] private PlayerProgressionComponent progressionComponent;
    [SerializeField] private PlayerHealthComponent healthComponent;

    [Header("Component References")]
    // CharacterController 负责移动和碰撞，不使用 Rigidbody 推玩家。
    [SerializeField] private CharacterController cc;

    // Animator 控制玩家动画参数和触发器。
    public Animator animator;

    // 武器碰撞体，攻击动画事件会启用/关闭它。
    public SphereCollider WeaponCollider;

    // 玩家模型渲染器，用来做受击变红。
    public SkinnedMeshRenderer myRenderer;

    // 播放脚步、跳跃、翻滚、攻击、受击音效。
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio")]
    // 是否由脚本自动播放脚步和动作音效。也可以关闭，改用动画事件手动调用。
    [SerializeField] private bool autoPlayFootstepSfx = true;
    [SerializeField] private bool autoPlayActionSfx = true;
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] runFootstepClips;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip rollClip;
    [SerializeField] private AudioClip attack1Clip;
    [SerializeField] private AudioClip attack2Clip;
    [SerializeField] private AudioClip attack3Clip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private float walkFootstepInterval = 0.9f;
    [SerializeField] private float runFootstepInterval = 0.3f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float jumpVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float rollVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 1f;

    private float footstepTimer;
    private bool footstepLoopActive;

    [Header("Movement")]
    // 普通走路速度。
    [SerializeField] private float Speed = 3f;

    // 按 Shift 跑步速度。
    [SerializeField] private float runSpeed = 5f;

    // 翻滚速度和持续时间。
    [SerializeField] private float rollSpeed = 12f;
    [SerializeField] private float rollDuration = 0.5f;
    public float jumpHeight = 2f;
    public float gravity = -9.81f;

    // 土狼时间：刚离开地面的一小段时间内仍允许跳跃，手感更宽容。
    public float coyoteTime = 0.12f;

    // 跳跃缓存：提前按下跳跃，落地后的一小段时间内仍能起跳。
    public float jumpBufferTime = 0.12f;

    // 这些布尔值直接反映当前移动/动作状态，动画和其他脚本会读取它们。
    public bool IsRunning;
    public bool IsRolling;
    public bool IsWalk;

    // 下面是跳跃和翻滚过程中的运行时计时/方向缓存。
    private bool isJumping;
    private float rollTimer;
    private Vector3 rollDirection;
    private Vector3 verticalVelocity;
    private float coyoteTimer;
    private float jumpBufferTimer;

    [Header("Stamina")]
    // 体力最大值。这里用 120，方便做到：跳跃 2 次、翻滚 3 次刚好用完。
    [SerializeField] private float maxStamina = 120f;

    // 当前体力。运行时会从 maxStamina 开始。
    [SerializeField] private float currentStamina;

    // 跳跃一次消耗 60，120 点体力最多连续支持 2 次跳跃。
    [SerializeField] private float jumpStaminaCost = 60f;

    // 翻滚一次消耗 40，120 点体力最多连续支持 3 次翻滚。
    [SerializeField] private float rollStaminaCost = 40f;

    // 跑步是持续消耗。18 点/秒大约能连续跑 6 秒多，可以按手感继续微调。
    [SerializeField] private float runStaminaCostPerSecond = 18f;

    // 体力耗尽后，需要至少恢复到这个值，才允许重新开始跑步，避免按住 Shift 时跑步动画一闪一闪。
    [SerializeField] private float minimumStaminaToStartRun = 5f;

    // 没有消耗体力时，每秒恢复多少点体力。
    [SerializeField] private float staminaRecoverPerSecond = 15f;

    // 本帧是否已经消耗过体力。用它防止一边跑步消耗、一边同帧回血式回体力。
    private bool staminaConsumedThisFrame;

    [Header("Combat")]
    // 连击窗口：动画事件 OpenComboWindow 后，玩家必须在这个时间内点下一次攻击。
    public float comboWindowTime = 0.8f;

    // 整套攻击超时保护：避免动画事件漏掉导致永远卡在攻击状态。
    public float fullAttackTimeout = 4f;
    public int AtkPower = 25;

    // 连击运行时状态：当前段数、计时器、是否正在攻击、是否允许接下一段。
    private int currentCombo;
    private float currentTimer;
    private float currentComboTimer;
    private bool isAttacking;
    private bool canComboNext;

    [Header("Feedback")]
    public Color hitColor = Color.red;
    public Color defaultColor = Color.white;
    public float colorTime = 0.1f;

    // 受击闪色运行时状态。
    private bool isColorChange;
    private float changeTime;
    private Color[] defaultColors;

    [Header("Dodge Feedback")]
    // 闪避成功后无敌多久。按你的需求默认 1 秒。
    [SerializeField] private float dodgeInvincibleDuration = 1f;

    // 闪烁间隔越短，闪得越快；0.08 秒大约一秒闪十几次。
    [SerializeField] private float dodgeFlickerInterval = 0.08f;

    // isDodgeInvincible 为 true 时，玩家暂时不再受到伤害。
    private bool isDodgeInvincible;
    private float dodgeInvincibleTimer;
    private float dodgeFlickerTimer;
    private bool dodgeFlickerVisible = true;

    // 闪烁不是改颜色，而是快速开/关模型 Renderer。
    // 这里记录默认开关状态，闪烁结束后能恢复到原来的样子。
    private Renderer[] dodgeFlickerRenderers;
    private bool[] dodgeFlickerRendererDefaultEnabled;

    [Header("Progression")]
    // 玩家生命、等级、经验。
    public int Hp;
    public int Hpmax;
    public int Lv = 1;
    public int Lvmax = 999;
    public int curExp;
    public int curExpMax;

    [Header("Player Attributes")]
    // 下面是完整属性系统。base 表示基础值，bonus 表示额外加成。
    [SerializeField] private int baseMaxHp = 150;
    [SerializeField] private int bonusMaxHp;
    [SerializeField] private int baseAttackPower = 25;
    [SerializeField] private float baseMoveSpeed = 3f;
    [SerializeField] private float runSpeedMultiplier = 1.6666667f;
    [SerializeField] [Range(0f, 1f)] private float critChance;
    [SerializeField] private float critDamageMultiplier = 1.5f;
    [SerializeField] [Range(0f, 1f)] private float dodgeChance;
    [SerializeField] private float healthRegenPerSecond;
    [SerializeField] [Range(0f, 1f)] private float damageReduction;
    [SerializeField] [Range(0f, 1f)] private float lifeSteal;

    // Fractional buffers let regen/life-steal preserve precision even when values are below 1 HP per tick.
    private float regenBuffer;
    private float lifeStealBuffer;
    private int healthRegenUpgradeCount;

    [Header("Upgrade State")]
    // pendingUpgradeSelectionCount 表示还有几次升级选择没点。
    [SerializeField] private int pendingUpgradeSelectionCount;

    // 升级面板打开时为 true，Update 会停止移动/攻击输入。
    [SerializeField] private bool isUpgradeSelectionActive;

    private NCharacter entryCharacterSave;
    private CharacterDefine entryCharacterDefine;
    private bool statsInitialized;

    [Header("UI References")]
    public Text THp;
    public Text THpmax;
    public Text TLv;
    public Text TcurExp;
    public Text TcurExpMax;
    public Image HpBar;
    public Image StaminaBar;
    public Image ExpBar;
    public GameObject ReStartPanel;

    // 对外只读属性面板数据，避免其他脚本直接改内部字段。
    public float CritChance => critChance;
    public float CritDamageMultiplier => critDamageMultiplier;
    public float DodgeChance => dodgeChance;
    public float HealthRegenPerSecond => healthRegenPerSecond;
    public float DamageReduction => damageReduction;
    public float LifeSteal => lifeSteal;
    public float WalkSpeed => Speed;
    public float RunSpeed => runSpeed;
    public int BaseMaxHp => GetLevelBaseMaxHp(Lv);
    public int BonusMaxHp => bonusMaxHp;
    public int PendingUpgradeSelectionCount => pendingUpgradeSelectionCount;
    public bool IsUpgradeSelectionActive => isUpgradeSelectionActive;
    public NCharacter EntryCharacterSave => entryCharacterSave;
    public CharacterDefine EntryCharacterDefine => entryCharacterDefine;

    internal CharacterController CharacterController => cc;
    internal Animator PlayerAnimator => animator;
    internal SphereCollider PlayerWeaponCollider => WeaponCollider;
    internal bool AutoPlayFootstepSfx => autoPlayFootstepSfx;
    internal bool AutoPlayActionSfx => autoPlayActionSfx;
    internal float WalkFootstepInterval => walkFootstepInterval;
    internal float RunFootstepInterval => runFootstepInterval;
    internal float ComboWindowTime => comboWindowTime;
    internal float FullAttackTimeout => fullAttackTimeout;
    internal Color HitFlashColor => hitColor;
    internal Color DefaultHitColor => defaultColor;
    internal float HitColorTime => colorTime;
    internal float DodgeInvincibleDuration => dodgeInvincibleDuration;
    internal float DodgeFlickerInterval => dodgeFlickerInterval;

    internal float LegacyWalkSpeed { get => Speed; set => Speed = value; }
    internal float LegacyRunSpeed { get => runSpeed; set => runSpeed = value; }
    internal float LegacyRollSpeed { get => rollSpeed; set => rollSpeed = value; }
    internal float LegacyRollDuration { get => rollDuration; set => rollDuration = value; }
    internal float LegacyJumpHeight { get => jumpHeight; set => jumpHeight = value; }
    internal float LegacyGravity { get => gravity; set => gravity = value; }
    internal float LegacyCoyoteTime { get => coyoteTime; set => coyoteTime = value; }
    internal float LegacyJumpBufferTime { get => jumpBufferTime; set => jumpBufferTime = value; }
    internal float LegacyMaxStamina { get => maxStamina; set => maxStamina = value; }
    internal float LegacyCurrentStamina { get => currentStamina; set => currentStamina = value; }
    internal float LegacyJumpStaminaCost { get => jumpStaminaCost; set => jumpStaminaCost = value; }
    internal float LegacyRollStaminaCost { get => rollStaminaCost; set => rollStaminaCost = value; }
    internal float LegacyRunStaminaCostPerSecond { get => runStaminaCostPerSecond; set => runStaminaCostPerSecond = value; }
    internal float LegacyMinimumStaminaToStartRun { get => minimumStaminaToStartRun; set => minimumStaminaToStartRun = value; }
    internal float LegacyStaminaRecoverPerSecond { get => staminaRecoverPerSecond; set => staminaRecoverPerSecond = value; }
    internal int BaseMaxHpValue { get => baseMaxHp; set => baseMaxHp = value; }
    internal int BonusMaxHpValue { get => bonusMaxHp; set => bonusMaxHp = value; }
    internal int BaseAttackPowerValue { get => baseAttackPower; set => baseAttackPower = value; }
    internal float BaseMoveSpeedValue { get => baseMoveSpeed; set => baseMoveSpeed = value; }
    internal float RunSpeedMultiplierValue { get => runSpeedMultiplier; set => runSpeedMultiplier = value; }
    internal float CritChanceValue { get => critChance; set => critChance = value; }
    internal float CritDamageMultiplierValue { get => critDamageMultiplier; set => critDamageMultiplier = value; }
    internal float DodgeChanceValue { get => dodgeChance; set => dodgeChance = value; }
    internal float HealthRegenPerSecondValue { get => healthRegenPerSecond; set => healthRegenPerSecond = value; }
    internal float DamageReductionValue { get => damageReduction; set => damageReduction = value; }
    internal float LifeStealValue { get => lifeSteal; set => lifeSteal = value; }
    internal int HealthRegenUpgradeCountValue { get => healthRegenUpgradeCount; set => healthRegenUpgradeCount = value; }
    internal int PendingUpgradeSelectionCountValue { get => pendingUpgradeSelectionCount; set => pendingUpgradeSelectionCount = value; }
    internal bool IsUpgradeSelectionActiveValue { get => isUpgradeSelectionActive; set => isUpgradeSelectionActive = value; }

#if UNITY_EDITOR
    private bool editorUiEnsureQueued;
#endif

    private void Awake()
    {
        // 单例赋值，方便武器、子弹、怪物、UI 找到玩家。
        instance = this;
        instance = this;
        GameplayRuntime.Instance.RegisterPlayer(this);
        CacheComponents();
        EnsurePlayerFeatureComponents();
        InitializePlayerFeatureComponents();
        EnsureRuntimeUiComponents();

        // 缓存 CharacterController、Animator、AudioSource 等常用组件。
        CacheComponents();

        // 记录默认材质颜色，用于受击变红后恢复。
        CacheHitEffectRenderer();

        // 记录哪些 Renderer 需要在闪避无敌时闪烁。
        CacheDodgeFlickerRenderers();

        // 自动补齐属性面板、升级面板、暂停/结束 UI。
        EnsureRuntimeUiComponents();
    }

    /// <summary>
    /// 玩家对象禁用时恢复闪避闪烁过的 Renderer。
    /// </summary>
    private void OnDisable()
    {
        if (healthComponent != null)
        {
            healthComponent.RestoreDodgeFlickerRenderers();
        }

        // 如果角色在闪避闪烁时被禁用，先把 Renderer 恢复，避免重新启用后模型还保持隐藏。
        RestoreDodgeFlickerRenderers();
    }

    private void OnDestroy()
    {
        GameplayRuntime.Instance.UnregisterPlayer(this);

        if (instance == this)
        {
            instance = null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        QueueEnsureRuntimeUiComponentsInEditor();
    }
#endif

    public void ApplyCharacterEntryData(NCharacter save, CharacterDefine define)
    {
        entryCharacterSave = save;
        entryCharacterDefine = define;

        if (!Application.isPlaying || !statsInitialized)
        {
            return;
        }

        progressionComponent.ApplyEntryCharacterStats();
        RecalculateMaxHp(fillCurrentHp: true);
        Hp = Hpmax;
        curExpMax = GetNextExpForLevel(Lv);
        movementComponent.InitializeStamina();
        EnsureStaminaBarUI();
        UpdateHpUI();
        UpdateStaminaUI();
        UpdateLvUI();
        NotifyStatsChanged();
        NotifyPendingUpgradeSelectionsChanged();
    }

    private void Start()
    {
        // 从 GameConfig 读取玩家初始属性。
        progressionComponent.InitializeStatsFromConfig();
        progressionComponent.ApplyEntryCharacterStats();
        Lv = Mathf.Max(1, Lv);

        if (GameConfig.instance != null)
        {
            Lvmax = Mathf.Max(Lv, GameConfig.instance.GetDefaultLevelCap());
        }

        curExp = Mathf.Max(0, curExp);
        curExpMax = GetNextExpForLevel(Lv);

        // fillCurrentHp = true 表示最大血量算完后，当前血量直接补满。
        RecalculateMaxHp(fillCurrentHp: true);
        Hp = Hpmax;
        movementComponent.InitializeStamina();
        EnsureStaminaBarUI();
        UpdateHpUI();
        UpdateStaminaUI();
        UpdateLvUI();
        statsInitialized = true;
        NotifyStatsChanged();
        NotifyPendingUpgradeSelectionsChanged();
    }

    /// <summary>
    /// 玩家主循环：处理输入、移动、攻击、翻滚、回血、体力和调试快捷键。
    /// </summary>
    private void Update()
    {
        if (TickFeatureComponents())
        {
            return;
        }

        // 每帧先重置，后面跑步/跳跃/翻滚如果消耗体力，会把它设回 true。
        staminaConsumedThisFrame = false;

        // 闪避无敌是按时间结束的，所以放在 Update 开头每帧更新。
        UpdateDodgeInvincibility();

        // When the level-up panel is open, gameplay input should not leak through to movement or attacks.
        if (isUpgradeSelectionActive)
        {
            MakeColorDefault();
            return;
        }

        if (Time.timeScale <= 0f)
        {
            MakeColorDefault();
            return;
        }

        if (IsRolling)
        {
            // 翻滚期间只处理翻滚移动、回血、颜色恢复，不允许攻击/普通移动打断。
            HandleRoll();
            ApplyHealthRegen();
            ApplyStaminaRecovery();
            UpdateStaminaUI();
            MakeColorDefault();
            Test();
            return;
        }

        if (CanStartRoll())
        {
            // 右键 + 有方向输入时开始翻滚。
            StartRoll();
            UpdateStaminaUI();
            return;
        }

        if (isAttacking)
        {
            // 攻击状态超时保护，防止动画事件没触发 ResetCombo 时卡住。
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
            {
                ResetCombo();
            }
        }

        UpdateComboTimer();
        CheckAttackInput();
        UpdateJumpTimers();
        UpdateGroundedState();

        Vector3 horizontalVelocity = Vector3.zero;
        if (!isAttacking)
        {
            // 攻击期间不允许普通移动和跳跃，避免动画和位移互相打架。
            TryJump();
            horizontalVelocity = Move();
        }

        // 垂直速度单独处理重力；最后和水平速度合并交给 CharacterController。
        verticalVelocity.y += gravity * Time.deltaTime;
        cc.Move((horizontalVelocity + verticalVelocity) * Time.deltaTime);

        UpdateGroundedAnimationState();
        UpdateMovementAudio();
        ApplyHealthRegen();
        ApplyStaminaRecovery();
        UpdateStaminaUI();
        MakeColorDefault();
        Test();
    }

    /// <summary>
    /// 缓存玩家常用组件引用，缺失时输出错误或自动补齐。
    /// </summary>
    private bool TickFeatureComponents()
    {
        if (movementComponent == null || combatComponent == null || progressionComponent == null || healthComponent == null)
        {
            return false;
        }

        movementComponent.BeginFrame();
        healthComponent.TickInvincibility();

        if (isUpgradeSelectionActive || Time.timeScale <= 0f)
        {
            healthComponent.TickHitFlash();
            return true;
        }

        if (movementComponent.TickRolling())
        {
            healthComponent.ApplyHealthRegen();
            movementComponent.ApplyStaminaRecovery();
            UpdateStaminaUI();
            healthComponent.TickHitFlash();
            Test();
            return true;
        }

        if (movementComponent.TryStartRoll(combatComponent.IsAttacking))
        {
            UpdateStaminaUI();
            return true;
        }

        combatComponent.Tick();
        movementComponent.TickNormalMovement(combatComponent.IsAttacking);
        healthComponent.ApplyHealthRegen();
        movementComponent.ApplyStaminaRecovery();
        UpdateStaminaUI();
        healthComponent.TickHitFlash();
        Test();
        return true;
    }

    private void CacheComponents()
    {
        // Inspector 没拖引用时，自动在当前物体上查找。
        if (cc == null)
        {
            cc = GetComponent<CharacterController>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        EnsureAudioSource();

        if (cc == null)
        {
            Debug.LogError("Player is missing CharacterController.", this);
        }

        if (animator == null)
        {
            Debug.LogError("Player is missing Animator.", this);
        }
    }

    /// <summary>
    /// 确保玩家身上有用于播放动作音效的 AudioSource。
    /// </summary>
    private void EnsureAudioSource()
    {
        // 音效播放需要 AudioSource，没有就自动添加。
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }

        sfxSource.playOnAwake = false;
    }

    /// <summary>
    /// 记录受击变色要用的模型 Renderer 和默认材质颜色。
    /// </summary>
    private void CacheHitEffectRenderer()
    {
        // 找到玩家模型的 SkinnedMeshRenderer，用于受击闪红。
        if (myRenderer == null)
        {
            myRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (myRenderer == null)
        {
            Debug.LogWarning("PlayerCo could not find a SkinnedMeshRenderer for hit feedback.", this);
            return;
        }

        Material[] materials = myRenderer.materials;
        defaultColors = new Color[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            // 记录每个材质原本的颜色，恢复时要用。
            defaultColors[i] = materials[i].color;
        }
    }

    /// <summary>
    /// 缓存闪避无敌期间需要快速显示/隐藏的所有 Renderer。
    /// </summary>
    private void CacheDodgeFlickerRenderers()
    {
        // 找到角色身上所有 Renderer，包括身体、武器、装备等。
        // includeInactive: true 表示就算某个子物体当前隐藏，也能记录到，恢复时更稳。
        dodgeFlickerRenderers = GetComponentsInChildren<Renderer>(true);
        dodgeFlickerRendererDefaultEnabled = new bool[dodgeFlickerRenderers.Length];

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            dodgeFlickerRendererDefaultEnabled[i] = currentRenderer != null && currentRenderer.enabled;
        }
    }

    // The runtime panels are attached automatically so the prototype works without manual scene wiring.
    private void EnsureRuntimeUiComponents()
    {
        EnsureUiComponent<PlayerAttributePanel>();
        EnsureUiComponent<PlayerLevelUpPanel>();
        EnsureUiComponent<GameSessionUi>();
    }

    private void EnsurePlayerFeatureComponents()
    {
        movementComponent = EnsureFeatureComponent(movementComponent);
        combatComponent = EnsureFeatureComponent(combatComponent);
        progressionComponent = EnsureFeatureComponent(progressionComponent);
        healthComponent = EnsureFeatureComponent(healthComponent);
    }

    private T EnsureFeatureComponent<T>(T component) where T : Component
    {
        if (component == null)
        {
            component = GetComponent<T>();
        }

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    private void InitializePlayerFeatureComponents()
    {
        movementComponent.Initialize(this);
        combatComponent.Initialize(this);
        progressionComponent.Initialize(this);
        healthComponent.Initialize(this);
    }

    internal void SetMovementRuntimeState(bool isRunning, bool isRolling, bool isWalk)
    {
        IsRunning = isRunning;
        IsRolling = isRolling;
        IsWalk = isWalk;
    }

    internal void SetMovementSpeeds(float walkSpeed, float targetRunSpeed)
    {
        Speed = Mathf.Max(0.01f, walkSpeed);
        runSpeed = Mathf.Max(Speed, targetRunSpeed);

        if (movementComponent != null)
        {
            movementComponent.SetSpeeds(Speed, runSpeed);
        }
    }

    internal void ResetPlayerRuntimeBuffers()
    {
        combatComponent?.ResetRuntimeBuffers();
        healthComponent?.ResetRuntimeBuffers();
    }

    internal void PlayComboAttackSfx(int comboIndex)
    {
        PlayComboAttackAudio(comboIndex);
    }

    private T EnsureUiComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

#if UNITY_EDITOR
    private void QueueEnsureRuntimeUiComponentsInEditor()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid() || editorUiEnsureQueued)
        {
            return;
        }

        editorUiEnsureQueued = true;
        EditorApplication.delayCall += EnsureRuntimeUiComponentsInEditor;
    }

    /// <summary>
    /// 编辑器中延迟补齐运行时 UI 组件，避免 OnValidate 直接改组件列表。
    /// </summary>
    private void EnsureRuntimeUiComponentsInEditor()
    {
        editorUiEnsureQueued = false;

        if (this == null || gameObject == null || Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        int componentCountBefore = GetComponents<Component>().Length;
        EnsureRuntimeUiComponents();

        if (GetComponents<Component>().Length != componentCountBefore)
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif

    private void InitializeStatsFromConfig()
    {
        // 优先用 GameConfig 中央配置；没有配置时使用本脚本 Inspector 上的兜底值。
        GameConfig config = GameConfig.instance;
        if (config != null)
        {
            baseMaxHp = config.GetPlayerBaseMaxHp();
            baseAttackPower = config.GetPlayerBaseAttack();
            baseMoveSpeed = config.GetPlayerBaseMoveSpeed();
            runSpeedMultiplier = config.GetPlayerRunSpeedMultiplier();
            critChance = config.playerBaseCritChance;
            critDamageMultiplier = Mathf.Max(1f, config.playerCritDamageMultiplier);
            dodgeChance = config.playerBaseDodgeChance;
            healthRegenPerSecond = Mathf.Max(0f, config.playerBaseHpRegenPerSecond);
            damageReduction = config.playerBaseDamageReduction;
            lifeSteal = config.playerBaseLifeSteal;
        }
        else
        {
            baseMaxHp = Mathf.Max(1, baseMaxHp);
            baseAttackPower = Mathf.Max(1, baseAttackPower);
            baseMoveSpeed = Mathf.Max(0.01f, baseMoveSpeed);
            runSpeedMultiplier = Mathf.Max(1f, runSpeedMultiplier);
            critDamageMultiplier = Mathf.Max(1f, critDamageMultiplier);
        }

        bonusMaxHp = Mathf.Max(0, bonusMaxHp);

        // 把基础属性真正写入运行时字段。
        AtkPower = Mathf.Max(1, baseAttackPower);
        Speed = Mathf.Max(0.01f, baseMoveSpeed);
        runSpeed = Speed * runSpeedMultiplier;
        RecalculateMaxHp(fillCurrentHp: true);
        regenBuffer = 0f;
        lifeStealBuffer = 0f;
        healthRegenUpgradeCount = 0;
    }

    private void ApplyEntryCharacterStats()
    {
        if (entryCharacterDefine != null)
        {
            if (entryCharacterDefine.hp > 0f)
            {
                baseMaxHp = Mathf.Max(1, Mathf.RoundToInt(entryCharacterDefine.hp));
            }

            if (entryCharacterDefine.attack > 0f)
            {
                baseAttackPower = Mathf.Max(1, Mathf.RoundToInt(entryCharacterDefine.attack));
            }

            if (entryCharacterDefine.moveSpeed > 0f)
            {
                baseMoveSpeed = Mathf.Max(0.01f, entryCharacterDefine.moveSpeed);
            }

            Lv = Mathf.Max(1, entryCharacterDefine.initLevel);
        }

        if (entryCharacterSave != null)
        {
            Lv = Mathf.Max(1, entryCharacterSave.level);
            curExp = Mathf.Max(0, entryCharacterSave.exp);
        }

        AtkPower = Mathf.Max(1, baseAttackPower);
        Speed = Mathf.Max(0.01f, baseMoveSpeed);
        runSpeed = Speed * runSpeedMultiplier;
    }

    /// <summary>
    /// 初始化体力上限、当前体力和相关消耗/恢复数值。
    /// </summary>
    private void InitializeStamina()
    {
        // 初始化体力数值，并把 Inspector 里可能填错的负数/0 做保护。
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
    /// 判断当前输入和体力是否允许开始翻滚。
    /// </summary>
    private bool CanStartRoll()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;

        // 必须不是攻击/翻滚中，并且这一帧按下鼠标右键。
        if (input == null || isAttacking || IsRolling || !Input.GetKeyDown(KeyCode.Mouse1))
        {
            return false;
        }

        // 还必须有移动方向，否则原地右键不翻滚。
        bool hasMoveInput =
            Mathf.Abs(input.XInput) > 0.1f ||
            Mathf.Abs(input.YInput) > 0.1f;

        if (!hasMoveInput)
        {
            return false;
        }

        // 体力不足时不能翻滚。这里要求体力至少够一次完整翻滚，避免 1 点体力也能翻滚。
        return HasEnoughStamina(rollStaminaCost);
    }

    /// <summary>
    /// 更新跳跃缓冲和土狼时间计时器。
    /// </summary>
    private void UpdateJumpTimers()
    {
        // 跳跃缓存：按下 Space 时给一小段“有效期”。
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
    /// 更新是否在地面上，并在落地时重置垂直速度。
    /// </summary>
    private void UpdateGroundedState()
    {
        // cc.isGrounded 是 CharacterController 判断当前是否接触地面。
        if (cc.isGrounded)
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
    /// 满足缓冲、落地窗口和体力条件时执行跳跃。
    /// </summary>
    private void TryJump()
    {
        // 必须同时满足“跳跃输入还在缓存期内”和“角色仍在可跳跃时间内”。
        if (jumpBufferTimer <= 0f || coyoteTimer <= 0f)
        {
            return;
        }

        if (!HasEnoughStamina(jumpStaminaCost))
        {
            // 体力不足时取消这次跳跃缓存，避免刚恢复一点体力后自动跳出去。
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

        if (autoPlayActionSfx)
        {
            PlayJumpSfxEvent();
        }
    }

    /// <summary>
    /// 根据输入和相机方向计算水平移动速度，并更新跑步体力消耗。
    /// </summary>
    private Vector3 Move()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null)
        {
            IsWalk = false;
            IsRunning = false;
            return Vector3.zero;
        }

        // 从统一输入接口读取已经处理过的输入。
        float inputX = input.XInput;
        float inputY = input.YInput;

        Vector3 dir = transform.TransformDirection(inputX, 0f, inputY);

        // Shift 跑步；只要有方向输入，IsWalk 就为 true。
        bool wantsRun = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        IsWalk = dir.magnitude > 0.01f;

        if (!IsWalk)
        {
            IsRunning = false;

            if (animator != null)
            {
                animator.SetBool("IsRunning", false);
            }

            // 没输入时把动画速度参数清零。
            ResetMoveAnimationParams();
            return Vector3.zero;
        }

        // 已经在跑时可以一直跑到体力归零；体力归零后要恢复到起跑阈值才允许重新开始跑。
        bool wasRunning = IsRunning;
        bool hasEnoughStaminaToStartRun = wasRunning
            ? currentStamina > 0f
            : currentStamina >= minimumStaminaToStartRun;
        IsRunning = wantsRun && hasEnoughStaminaToStartRun;
        if (IsRunning)
        {
            ConsumeStaminaAllowPartial(runStaminaCostPerSecond * Time.deltaTime);
        }

        if (animator != null)
        {
            animator.SetBool("IsRunning", IsRunning);
        }

        UpdateMoveAnimationParams(inputX, inputY);

        // 返回水平速度，真正 Move 在 Update 末尾统一执行。
        return dir * (IsRunning ? runSpeed : Speed);
    }

    /// <summary>
    /// 把当前移动输入写入 Animator，驱动走/跑动画混合。
    /// </summary>
    private void UpdateMoveAnimationParams(float inputX, float inputY)
    {
        if (animator == null)
        {
            return;
        }

        if (IsRunning)
        {
            animator.SetFloat("SpeedX_Run", inputX);
            animator.SetFloat("SpeedY_Run", inputY);
            return;
        }

        animator.SetFloat("SpeedX", inputX);
        animator.SetFloat("SpeedY", inputY);
    }

    /// <summary>
    /// 停止移动时把 Animator 移动参数归零。
    /// </summary>
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

    /// <summary>
    /// 消耗体力并进入翻滚状态。
    /// </summary>
    private void StartRoll()
    {
        // StartRoll 只会在 CanStartRoll 成功后进入，这里真正扣除翻滚体力。
        ConsumeStamina(rollStaminaCost);

        // 翻滚会记录方向，然后在 HandleRoll 里持续移动一段时间。
        IsRolling = true;
        rollTimer = rollDuration;
        ResetFootstepLoop();

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        float inputX = input != null ? input.XInput : 0f;
        float inputY = input != null ? input.YInput : 0f;

        Vector3 localDirection = new Vector3(inputX, 0f, inputY);

        // 输入方向是本地坐标，要转成世界方向。
        rollDirection = transform.TransformDirection(localDirection).normalized;

        if (animator != null)
        {
            animator.SetFloat("RollX", inputX);
            animator.SetFloat("RollY", inputY);
            animator.SetTrigger("Roll");
        }

        IsRunning = false;
        IsWalk = false;

        if (autoPlayActionSfx)
        {
            PlayRollSfxEvent();
        }
    }

    /// <summary>
    /// 翻滚期间按固定方向推进角色，并在时间结束后退出翻滚。
    /// </summary>
    private void HandleRoll()
    {
        // 倒计时结束后退出翻滚。
        rollTimer -= Time.deltaTime;

        if (cc.isGrounded && verticalVelocity.y < 0f)
        {
            verticalVelocity.y = GroundedVerticalVelocity;
        }

        if (rollTimer > 0f)
        {
            // 翻滚水平移动 + 重力垂直移动。
            Vector3 move = rollDirection * rollSpeed;
            cc.Move(move * Time.deltaTime);

            verticalVelocity.y += gravity * Time.deltaTime;
            cc.Move(verticalVelocity * Time.deltaTime);
            return;
        }

        IsRolling = false;
        rollTimer = 0f;
    }

    /// <summary>
    /// 读取鼠标攻击输入，并根据当前连击状态决定开第一刀或接下一刀。
    /// </summary>
    private void CheckAttackInput()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;

        // leftMouseDown 只在按下那一帧为 true，适合触发一次攻击。
        if (input == null || !input.LeftMouseDown)
        {
            return;
        }

        if (currentCombo == 0 && !isAttacking)
        {
            // 当前没有连击，开始第一段攻击。
            StartFirstAttack();
            return;
        }

        if (canComboNext && currentCombo < MaxCombo)
        {
            // 动画事件打开连击窗口后，允许进入下一段。
            TriggerNextCombo();
        }
    }

    /// <summary>
    /// 从待机状态启动第一段攻击。
    /// </summary>
    private void StartFirstAttack()
    {
        isAttacking = true;
        currentCombo = 1;
        currentTimer = fullAttackTimeout;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", currentCombo);
        }
    }

    /// <summary>
    /// 在连击窗口内触发下一段攻击。
    /// </summary>
    private void TriggerNextCombo()
    {
        currentCombo++;
        canComboNext = false;
        currentTimer = fullAttackTimeout;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", currentCombo);
        }
    }

    /// <summary>
    /// 更新连击窗口计时，超时后关闭可接招状态。
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
    /// 动画事件调用：打开下一段连击输入窗口。
    /// </summary>
    public void OpenComboWindow()
    {
        if (combatComponent != null)
        {
            combatComponent.OpenComboWindow();
            return;
        }

        // 通常由攻击动画事件调用，告诉脚本“现在可以接下一段攻击了”。
        canComboNext = true;
        currentComboTimer = comboWindowTime;
    }

    /// <summary>
    /// 动画事件或超时保护调用：重置整套连击状态。
    /// </summary>
    public void ResetCombo()
    {
        if (combatComponent != null)
        {
            combatComponent.ResetCombo();
            return;
        }

        // 攻击结束或超时时，把所有连击状态清回初始。
        currentCombo = 0;
        isAttacking = false;
        canComboNext = false;
        currentTimer = 0f;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", 0);
        }
    }

    /// <summary>
    /// 动画事件调用：启用武器碰撞盒。
    /// </summary>
    private void WeaponEnable()
    {
        if (combatComponent != null)
        {
            combatComponent.WeaponEnable();
            return;
        }

        // 通常由攻击动画事件调用，只在刀真正挥到目标的几帧打开碰撞体。
        if (WeaponCollider != null)
        {
            WeaponCollider.enabled = true;
        }

        if (autoPlayActionSfx)
        {
            PlayComboAttackAudio(currentCombo);
        }
    }

    /// <summary>
    /// 动画事件调用：关闭武器碰撞盒。
    /// </summary>
    private void WeaponDisable()
    {
        if (combatComponent != null)
        {
            combatComponent.WeaponDisable();
            return;
        }

        // 攻击判定结束后关闭，避免武器一直造成伤害。
        if (WeaponCollider != null)
        {
            WeaponCollider.enabled = false;
        }
    }

    /// <summary>
    /// Rolls the actual hit damage for one player attack, including critical-strike logic.
    /// </summary>
    public int RollAttackDamage(out bool isCritical)
    {
        if (combatComponent != null)
        {
            return combatComponent.RollAttackDamage(out isCritical);
        }

        // 先用当前攻击力作为基础伤害。
        int damage = Mathf.Max(1, AtkPower);

        // Random.value 是 0 到 1 的随机数，小于暴击率就暴击。
        isCritical = UnityEngine.Random.value < critChance;

        if (isCritical)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * critDamageMultiplier));
        }

        return damage;
    }

    /// <summary>
    /// Applies life-steal after the real damage dealt has been estimated by the weapon hit logic.
    /// </summary>
    public int HandleDamageDealt(int appliedDamage)
    {
        if (combatComponent != null)
        {
            return combatComponent.HandleDamageDealt(appliedDamage);
        }

        // 吸血只在造成了有效伤害时生效。
        if (lifeSteal <= 0f || appliedDamage <= 0)
        {
            return 0;
        }

        lifeStealBuffer += appliedDamage * lifeSteal;

        // FloorToInt 只取整数回血，小数留在 buffer 里下次继续累积。
        int healAmount = Mathf.FloorToInt(lifeStealBuffer);
        if (healAmount <= 0)
        {
            return 0;
        }

        // 真正能恢复多少，要看玩家当前缺多少血。
        // 例如只缺 3 点血，就算吸血算出 10，也只显示并恢复 3。
        int actualHealAmount = Mathf.Min(healAmount, Mathf.Max(0, Hpmax - Hp));
        lifeStealBuffer -= healAmount;
        if (actualHealAmount <= 0)
        {
            return 0;
        }

        // 吸血属于战斗回血，要在玩家头顶显示绿色 +数字。
        RecoverHp(actualHealAmount, showFloatingText: true);

        return actualHealAmount;
    }

    /// <summary>
    /// Unified damage entry point. Dodge is checked first, then additive damage reduction is applied.
    /// </summary>
    public void Hit(int incomingAttackPower)
    {
        if (healthComponent != null)
        {
            healthComponent.Hit(incomingAttackPower);
            return;
        }

        // 这是 FighterInterface 的受击入口，怪物攻击/子弹都会调用这里。
        if (isDodgeInvincible)
        {
            // 闪避成功后的 1 秒无敌期内，直接忽略后续伤害。
            return;
        }

        if (TryDodgeIncomingHit())
        {
            // 闪避成功：显示 miss，开始闪烁，并进入短暂无敌。
            StartDodgeInvincibility();
            return;
        }

        // 先应用减伤，再扣血。
        int finalDamage = ApplyDamageReduction(incomingAttackPower);
        if (finalDamage <= 0)
        {
            return;
        }

        int hpBeforeHit = Hp;
        Hp -= finalDamage;

        // 敌人/子弹打到玩家时，在玩家头顶显示红色 -数字。
        // 这里显示“实际扣掉的血量”，如果玩家只剩 2 血，就不会显示 -999 这种误导数字。
        int actualDamageTaken = Mathf.Max(0, hpBeforeHit - Mathf.Max(0, Hp));
        FloatingCombatText.ShowTakenDamage(transform, actualDamageTaken);

        if (autoPlayActionSfx)
        {
            PlayHitSfxEvent();
        }

        if (Hp <= 0)
        {
            // 死亡后打开重开/退出面板。
            Hp = 0;
            GameSessionUi sessionUi = GetComponent<GameSessionUi>();
            if (sessionUi != null)
            {
                sessionUi.ShowGameOver();
            }
            else if (ReStartPanel != null)
            {
                ReStartPanel.SetActive(true);
            }
        }

        HitColorChange();
        UpdateHpUI();
        NotifyStatsChanged();
    }

    /// <summary>
    /// 根据闪避率判定本次受击是否被完全闪避。
    /// </summary>
    private bool TryDodgeIncomingHit()
    {
        return dodgeChance > 0f && UnityEngine.Random.value < dodgeChance;
    }

    /// <summary>
    /// 闪避成功后开启短暂无敌和模型闪烁。
    /// </summary>
    private void StartDodgeInvincibility()
    {
        // 闪避成功时在玩家头顶显示 miss。
        FloatingCombatText.ShowMiss(transform);

        // 开启无敌计时。Max 保底一下，避免 Inspector 里误填 0 后状态立刻结束。
        isDodgeInvincible = true;
        dodgeInvincibleTimer = Mathf.Max(0.01f, dodgeInvincibleDuration);
        dodgeFlickerTimer = Mathf.Max(0.01f, dodgeFlickerInterval);

        // 立刻隐藏一次，让玩家马上看出“闪避成功”的反馈。
        SetDodgeFlickerVisible(false);
    }

    /// <summary>
    /// 更新闪避无敌计时和闪烁节奏。
    /// </summary>
    private void UpdateDodgeInvincibility()
    {
        if (!isDodgeInvincible)
        {
            return;
        }

        dodgeInvincibleTimer -= Time.deltaTime;
        dodgeFlickerTimer -= Time.deltaTime;

        if (dodgeFlickerTimer <= 0f)
        {
            // 每隔一小段时间切换显示/隐藏，就形成闪烁。
            SetDodgeFlickerVisible(!dodgeFlickerVisible);
            dodgeFlickerTimer = Mathf.Max(0.01f, dodgeFlickerInterval);
        }

        if (dodgeInvincibleTimer > 0f)
        {
            return;
        }

        StopDodgeInvincibility();
    }

    /// <summary>
    /// 结束闪避无敌并恢复模型显示。
    /// </summary>
    private void StopDodgeInvincibility()
    {
        // 无敌结束后恢复正常显示，并清理计时器。
        isDodgeInvincible = false;
        dodgeInvincibleTimer = 0f;
        dodgeFlickerTimer = 0f;
        RestoreDodgeFlickerRenderers();
    }

    /// <summary>
    /// 批量切换被缓存 Renderer 的显示状态，实现闪烁反馈。
    /// </summary>
    private void SetDodgeFlickerVisible(bool visible)
    {
        dodgeFlickerVisible = visible;

        if (dodgeFlickerRenderers == null || dodgeFlickerRendererDefaultEnabled == null)
        {
            CacheDodgeFlickerRenderers();
        }

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            // 如果某个 Renderer 开局就是隐藏的，闪烁时也不强行打开它。
            bool defaultEnabled = i < dodgeFlickerRendererDefaultEnabled.Length &&
                                  dodgeFlickerRendererDefaultEnabled[i];
            currentRenderer.enabled = visible && defaultEnabled;
        }
    }

    /// <summary>
    /// 把所有闪烁过的 Renderer 恢复到进入闪避前的默认开关。
    /// </summary>
    private void RestoreDodgeFlickerRenderers()
    {
        dodgeFlickerVisible = true;

        if (dodgeFlickerRenderers == null || dodgeFlickerRendererDefaultEnabled == null)
        {
            return;
        }

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            bool defaultEnabled = i < dodgeFlickerRendererDefaultEnabled.Length &&
                                  dodgeFlickerRendererDefaultEnabled[i];
            currentRenderer.enabled = defaultEnabled;
        }
    }

    /// <summary>
    /// 按当前伤害减免属性计算实际受到的伤害。
    /// </summary>
    private int ApplyDamageReduction(int incomingAttackPower)
    {
        // 伤害先夹到非负数，再按 damageReduction 比例减免。
        int rawDamage = Mathf.Max(0, incomingAttackPower);
        if (rawDamage <= 0)
        {
            return 0;
        }

        float reduction = Mathf.Clamp01(damageReduction);
        return Mathf.Max(1, Mathf.FloorToInt(rawDamage * (1f - reduction)));
    }

    /// <summary>
    /// 判断当前体力是否足够支付指定消耗。
    /// </summary>
    private bool HasEnoughStamina(float cost)
    {
        // cost <= 0 表示这个动作不需要体力，永远允许。
        if (cost <= 0f)
        {
            return true;
        }

        return currentStamina >= cost;
    }

    /// <summary>
    /// 尝试消耗体力，成功返回 true。
    /// </summary>
    private bool ConsumeStamina(float amount)
    {
        // 跑步会每帧传入一个很小的消耗值；跳跃/翻滚会传入一次性消耗。
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

    /// <summary>
    /// 按可用体力消耗最多 amount，允许体力被耗到 0。
    /// </summary>
    private void ConsumeStaminaAllowPartial(float amount)
    {
        // 跑步是持续动作，最后一帧允许把剩余体力扣到 0。
        // 这样玩家会感觉自己是“跑到体力耗尽”，而不是剩下一点点突然停住。
        if (amount <= 0f)
        {
            return;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        staminaConsumedThisFrame = true;
    }

    /// <summary>
    /// 没有在本帧消耗体力时按秒恢复体力。
    /// </summary>
    private void ApplyStaminaRecovery()
    {
        // 翻滚中不恢复体力；跑步/跳跃等本帧消耗过体力时也不恢复。
        if (staminaConsumedThisFrame || IsRolling)
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
    /// 按每秒回血属性持续恢复生命，并保留小数回血进度。
    /// </summary>
    private void ApplyHealthRegen()
    {
        // 每秒回血用 deltaTime 转成“这一帧该回血多少”。
        if (healthRegenPerSecond <= 0f)
        {
            return;
        }

        if (Hp >= Hpmax)
        {
            regenBuffer = 0f;
            return;
        }

        regenBuffer += healthRegenPerSecond * Time.deltaTime;

        // 小数回血会留在 regenBuffer 里，积累到 1 点才真正恢复。
        int recoverAmount = Mathf.FloorToInt(regenBuffer);
        if (recoverAmount <= 0)
        {
            return;
        }

        regenBuffer -= recoverAmount;
        // 自然回血也属于战斗中的回血反馈，所以显示在玩家头顶。
        RecoverHp(recoverAmount, showFloatingText: true);
    }

    /// <summary>
    /// 恢复指定生命值，并可选择显示回血漂浮文字。
    /// </summary>
    public void RecoverHp(int amount, bool showFloatingText = false)
    {
        if (healthComponent != null)
        {
            healthComponent.RecoverHp(amount, showFloatingText);
            return;
        }

        // 公共回血入口：升级、吸血、奖励都走这里。
        if (amount <= 0 || Hp >= Hpmax)
        {
            return;
        }

        int hpBeforeRecover = Hp;
        Hp = Mathf.Min(Hp + amount, Hpmax);
        int actualRecoverAmount = Mathf.Max(0, Hp - hpBeforeRecover);

        // showFloatingText 默认是 false，所以升级回血不会弹数字。
        // 吸血和自然回血会明确传 true。
        if (showFloatingText)
        {
            FloatingCombatText.ShowHealing(transform, actualRecoverAmount);
        }

        UpdateHpUI();
        NotifyStatsChanged();
    }

    /// <summary>
    /// 将当前生命恢复到最大生命。
    /// </summary>
    public void FullHeal()
    {
        if (healthComponent != null)
        {
            healthComponent.FullHeal();
            return;
        }

        Hp = Hpmax;
        UpdateHpUI();
        NotifyStatsChanged();
    }

    /// <summary>
    /// 受击时把角色材质临时切换为受击颜色。
    /// </summary>
    private void HitColorChange()
    {
        // 受击时把玩家材质颜色改成 hitColor，并记录开始时间。
        if (myRenderer == null)
        {
            return;
        }

        isColorChange = true;
        changeTime = Time.time;

        Material[] materials = myRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].color = hitColor;
        }
    }

    /// <summary>
    /// 受击闪色结束后恢复默认材质颜色。
    /// </summary>
    private void MakeColorDefault()
    {
        // 超过 colorTime 后恢复默认颜色。
        if (myRenderer == null || !isColorChange)
        {
            return;
        }

        if (Time.time - changeTime < colorTime)
        {
            return;
        }

        isColorChange = false;
        Material[] materials = myRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Color targetColor = defaultColors != null && i < defaultColors.Length
                ? defaultColors[i]
                : defaultColor;

            materials[i].color = targetColor;
        }
    }

    /// <summary>
    /// 把是否落地写入 Animator。
    /// </summary>
    private void UpdateGroundedAnimationState()
    {
        if (animator != null)
        {
            animator.SetBool("IsGrounded", cc.isGrounded);
        }

        isJumping = !cc.isGrounded;
    }

    /// <summary>
    /// 根据走路/跑步状态自动循环播放脚步声。
    /// </summary>
    private void UpdateMovementAudio()
    {
        // 自动脚步声：根据走/跑状态用不同间隔播放随机脚步音。
        if (!autoPlayFootstepSfx)
        {
            ResetFootstepLoop();
            return;
        }

        bool canPlayFootstep = cc.isGrounded && IsWalk && !IsRolling && !isAttacking;
        if (!canPlayFootstep)
        {
            // 停止移动、跳起、翻滚或攻击时，重置脚步循环。
            ResetFootstepLoop();
            return;
        }

        // 跑步脚步更密，走路脚步更慢。
        float interval = Mathf.Max(0.05f, IsRunning ? runFootstepInterval : walkFootstepInterval);
        if (!footstepLoopActive)
        {
            // 刚开始移动时立刻播放一次脚步声。
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

        if (IsRunning)
        {
            PlayRunFootstepSfxEvent();
        }
        else
        {
            PlayWalkFootstepSfxEvent();
        }

        footstepTimer = interval;
    }

    /// <summary>
    /// 停止脚步循环计时。
    /// </summary>
    private void ResetFootstepLoop()
    {
        footstepLoopActive = false;
        footstepTimer = 0f;
    }

    /// <summary>
    /// 根据当前攻击段数播放对应攻击音效。
    /// </summary>
    private void PlayComboAttackAudio(int comboIndex)
    {
        // 根据当前连击段数选择不同攻击音效。
        AudioClip clip = null;
        if (comboIndex == 1)
        {
            clip = attack1Clip;
        }
        else if (comboIndex == 2)
        {
            clip = attack2Clip;
        }
        else if (comboIndex >= 3)
        {
            clip = attack3Clip;
        }

        PlayClip(clip, attackVolume);
    }

    /// <summary>
    /// 从音效数组里随机播放一个可用音效。
    /// </summary>
    private bool PlayRandomClip(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        int validClipCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validClipCount++;
            }
        }

        if (validClipCount == 0)
        {
            return false;
        }

        int selectedIndex = UnityEngine.Random.Range(0, validClipCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                return PlayClip(clips[i], volume);
            }

            selectedIndex--;
        }

        return false;
    }

    /// <summary>
    /// 播放单个音效，并处理 AudioSource 或 clip 缺失的情况。
    /// </summary>
    private bool PlayClip(AudioClip clip, float volume = 1f)
    {
        if (clip == null)
        {
            return false;
        }

        EnsureAudioSource();
        sfxSource.PlayOneShot(clip, volume);
        return true;
    }

    /// <summary>
    /// 动画事件调用：播放走路脚步声。
    /// </summary>
    public void PlayWalkFootstepSfxEvent()
    {
        PlayRandomClip(walkFootstepClips, footstepVolume);
    }

    /// <summary>
    /// 动画事件调用：播放跑步脚步声。
    /// </summary>
    public void PlayRunFootstepSfxEvent()
    {
        if (!PlayRandomClip(runFootstepClips, footstepVolume))
        {
            PlayRandomClip(walkFootstepClips, footstepVolume);
        }
    }

    /// <summary>
    /// 动画事件调用：播放跳跃音效。
    /// </summary>
    public void PlayJumpSfxEvent()
    {
        PlayClip(jumpClip, jumpVolume);
    }

    /// <summary>
    /// 动画事件调用：播放翻滚音效。
    /// </summary>
    public void PlayRollSfxEvent()
    {
        PlayClip(rollClip, rollVolume);
    }

    /// <summary>
    /// 动画事件调用：播放第一段攻击音效。
    /// </summary>
    public void PlayAttack1SfxEvent()
    {
        PlayClip(attack1Clip, attackVolume);
    }

    /// <summary>
    /// 动画事件调用：播放第二段攻击音效。
    /// </summary>
    public void PlayAttack2SfxEvent()
    {
        PlayClip(attack2Clip, attackVolume);
    }

    /// <summary>
    /// 动画事件调用：播放第三段攻击音效。
    /// </summary>
    public void PlayAttack3SfxEvent()
    {
        PlayClip(attack3Clip, attackVolume);
    }

    /// <summary>
    /// 动画事件或受击流程调用：播放受击音效。
    /// </summary>
    public void PlayHitSfxEvent()
    {
        PlayClip(hitClip, hitVolume);
    }

    /// <summary>
    /// 把当前生命、最大生命和血条比例写入 UI。
    /// </summary>
    public void UpdateHpUI()
    {
        if (THp != null)
        {
            THp.text = Hp.ToString();
        }

        if (THpmax != null)
        {
            THpmax.text = Hpmax.ToString();
        }

        if (HpBar != null)
        {
            HpBar.fillAmount = Hpmax > 0 ? (float)Hp / Hpmax : 0f;
        }
    }

    /// <summary>
    /// 确保体力条 UI 存在；缺失时根据血条位置自动创建。
    /// </summary>
    private void EnsureStaminaBarUI()
    {
        // 如果已经在 Inspector 里拖好了体力条，就只确保它是“横向填充”模式。
        if (StaminaBar != null)
        {
            ConfigureStaminaFillImage(StaminaBar);
            return;
        }

        // 体力条要放在血条上方，所以必须先有血条作为位置参考。
        if (HpBar == null)
        {
            return;
        }

        RectTransform healthReferenceRect = GetHealthBarReferenceRect();
        if (healthReferenceRect == null || healthReferenceRect.parent == null)
        {
            return;
        }

        Transform existingRoot = healthReferenceRect.parent.Find("StaminaBarRoot");
        if (existingRoot != null)
        {
            Transform existingFill = existingRoot.Find("StaminaFill");
            if (existingFill != null)
            {
                StaminaBar = existingFill.GetComponent<Image>();
                ConfigureStaminaFillImage(StaminaBar);
                return;
            }
        }

        // 创建背景条。它和血条放在同一个父物体下，再整体向上偏移。
        GameObject staminaRootObject = new GameObject(
            "StaminaBarRoot",
            typeof(RectTransform),
            typeof(Image));
        staminaRootObject.transform.SetParent(healthReferenceRect.parent, false);

        RectTransform staminaRootRect = staminaRootObject.GetComponent<RectTransform>();
        CopyBarRectTransform(healthReferenceRect, staminaRootRect);

        Image healthReferenceImage = healthReferenceRect.GetComponent<Image>();
        Image backgroundImage = staminaRootObject.GetComponent<Image>();
        if (healthReferenceImage != null)
        {
            backgroundImage.sprite = healthReferenceImage.sprite;
            backgroundImage.type = healthReferenceImage.type;
        }

        backgroundImage.color = new Color(0.18f, 0.15f, 0.02f, 0.85f);

        // 创建真正会变化的黄色填充条。
        GameObject staminaFillObject = new GameObject(
            "StaminaFill",
            typeof(RectTransform),
            typeof(Image));
        staminaFillObject.transform.SetParent(staminaRootObject.transform, false);

        RectTransform fillRect = staminaFillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);

        StaminaBar = staminaFillObject.GetComponent<Image>();
        if (HpBar != null)
        {
            StaminaBar.sprite = HpBar.sprite;
        }

        StaminaBar.color = Color.yellow;
        ConfigureStaminaFillImage(StaminaBar);
    }

    /// <summary>
    /// 获取用于摆放体力条的血条 RectTransform 参考。
    /// </summary>
    private RectTransform GetHealthBarReferenceRect()
    {
        RectTransform hpRect = HpBar != null ? HpBar.rectTransform : null;
        if (hpRect == null)
        {
            return null;
        }

        RectTransform parentRect = hpRect.parent as RectTransform;
        if (parentRect == null)
        {
            return hpRect;
        }

        bool hpLooksLikeFillChild =
            hpRect.anchorMin == Vector2.zero &&
            hpRect.anchorMax == Vector2.one;

        // 很多血条会把“填充 Image”拉满放在背景条里面。
        // 如果 HpBar 看起来是这种填充子物体，就用它的父物体作为整体血条位置参考。
        return hpLooksLikeFillChild ? parentRect : hpRect;
    }

    /// <summary>
    /// 复制血条布局参数，并把体力条放到血条上方。
    /// </summary>
    private void CopyBarRectTransform(RectTransform source, RectTransform target)
    {
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.pivot = source.pivot;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;

        float sourceWidth = source.rect.width > 1f ? source.rect.width : Mathf.Abs(source.sizeDelta.x);
        float sourceHeight = source.rect.height > 1f ? source.rect.height : Mathf.Abs(source.sizeDelta.y);
        sourceWidth = Mathf.Max(80f, sourceWidth);
        sourceHeight = Mathf.Max(8f, sourceHeight);
        float staminaHeight = Mathf.Max(8f, sourceHeight * 0.45f);

        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, sourceWidth);
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, staminaHeight);
        target.anchoredPosition += new Vector2(0f, sourceHeight * 0.5f + staminaHeight * 0.5f + 6f);
    }

    /// <summary>
    /// 配置体力条填充图片，让它通过横向缩放表现百分比。
    /// </summary>
    private void ConfigureStaminaFillImage(Image targetImage)
    {
        if (targetImage == null)
        {
            return;
        }

        // 这里不用 Image.fillAmount 驱动体力条。
        // 原因是不同图片类型/九宫格设置下，fillAmount 可能看不出变化。
        // 我们统一用 RectTransform.localScale.x 缩放宽度，UI 一定会跟着体力减少/恢复。
        targetImage.type = Image.Type.Simple;
        targetImage.color = Color.yellow;

        RectTransform targetRect = targetImage.rectTransform;
        if (targetRect != null)
        {
            targetRect.pivot = new Vector2(0f, 0.5f);
        }
    }

    /// <summary>
    /// 根据当前体力刷新体力条显示比例。
    /// </summary>
    private void UpdateStaminaUI()
    {
        EnsureStaminaBarUI();

        if (StaminaBar != null)
        {
            float staminaPercent = maxStamina > 0f
                ? Mathf.Clamp01(currentStamina / maxStamina)
                : 0f;

            RectTransform staminaRect = StaminaBar.rectTransform;
            if (staminaRect != null)
            {
                Vector3 scale = staminaRect.localScale;
                scale.x = staminaPercent;
                staminaRect.localScale = scale;
            }
        }
    }

    /// <summary>
    /// 刷新等级、当前经验、升级所需经验和经验条。
    /// </summary>
    public void UpdateLvUI()
    {
        curExpMax = GetNextExpForLevel(Lv);

        if (TcurExp != null)
        {
            TcurExp.text = curExp.ToString();
        }

        if (TcurExpMax != null)
        {
            TcurExpMax.text = curExpMax.ToString();
        }

        if (TLv != null)
        {
            TLv.text = Lv.ToString();
        }

        if (ExpBar != null)
        {
            ExpBar.fillAmount = curExpMax > 0
                ? Mathf.Clamp01((float)curExp / curExpMax)
                : 0f;
        }
    }

    /// <summary>
    /// 增加经验并触发可能的连续升级。
    /// </summary>
    public void AddExp(int exp)
    {
        if (progressionComponent != null)
        {
            progressionComponent.AddExp(exp);
            return;
        }

        // 外部发经验时调用，比如击杀怪物、击破金库。
        if (exp <= 0)
        {
            return;
        }

        // 每次真正获得经验时，在玩家头顶显示黄色 +数字exp。
        // 这里放在 AddExp 入口，所有来源都会自动显示：击杀怪物、击破宝箱、调试按键等。
        FloatingCombatText.ShowExperience(transform, exp);

        curExp += exp;
        DoLevelUp();
        UpdateLvUI();
        NotifyStatsChanged();
    }

    /// <summary>
    /// 根据当前经验循环结算升级，并排队升级三选一。
    /// </summary>
    private void DoLevelUp()
    {
        // while 允许一次获得大量经验时连续升多级。
        while (Lv < Lvmax && curExp >= GetNextExpForLevel(Lv))
        {
            curExp -= GetNextExpForLevel(Lv);
            Lv++;
            RecalculateMaxHp(fillCurrentHp: false);
            ApplyLevelUpRecovery();

            // 每升一级，增加一次待选择升级。
            QueueUpgradeSelection();
        }

        if (Lv >= Lvmax)
        {
            curExp = Mathf.Min(curExp, GetNextExpForLevel(Lv));
        }

        curExpMax = GetNextExpForLevel(Lv);
    }

    /// <summary>
    /// 升级时按配置给玩家恢复生命。
    /// </summary>
    private void ApplyLevelUpRecovery()
    {
        GameConfig config = GameConfig.instance;
        float healPercent = config != null ? config.levelUpHealPercent : 0.3f;
        int minimumHeal = config != null ? config.minimumLevelUpHeal : 30;
        int healAmount = Mathf.Max(minimumHeal, Mathf.CeilToInt(Hpmax * healPercent));
        RecoverHp(healAmount);
    }

    /// <summary>
    /// 增加一次待选择升级次数，并通知升级面板。
    /// </summary>
    private void QueueUpgradeSelection()
    {
        // 只记录“有几次没选”，真正显示 UI 由 PlayerLevelUpPanel 监听事件完成。
        pendingUpgradeSelectionCount++;
        NotifyPendingUpgradeSelectionsChanged();
    }

    /// <summary>
    /// 设置升级面板是否正在占用玩家输入。
    /// </summary>
    public void SetUpgradeSelectionState(bool active)
    {
        if (progressionComponent != null)
        {
            progressionComponent.SetUpgradeSelectionState(active);
            return;
        }

        isUpgradeSelectionActive = active;
    }

    /// <summary>
    /// 应用玩家选择的升级，并减少一次待选择次数。
    /// </summary>
    public bool ResolvePendingUpgradeSelection(PlayerAttributeType attributeType)
    {
        if (progressionComponent != null)
        {
            return progressionComponent.ResolvePendingUpgradeSelection(attributeType);
        }

        // 玩家点了升级选项后，升级面板调用这里。
        if (pendingUpgradeSelectionCount <= 0)
        {
            return false;
        }

        if (!TryApplyAttributeUpgrade(attributeType))
        {
            return false;
        }

        pendingUpgradeSelectionCount = Mathf.Max(0, pendingUpgradeSelectionCount - 1);
        NotifyPendingUpgradeSelectionsChanged();
        return true;
    }

    /// <summary>
    /// 判断某个属性升级是否仍可选择，已达上限的属性会被过滤。
    /// </summary>
    public bool CanApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        if (progressionComponent != null)
        {
            return progressionComponent.CanApplyAttributeUpgrade(attributeType);
        }

        // 判断某个升级是否还没到上限，用于随机选项过滤和点击前校验。
        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
            case PlayerAttributeType.MaxHp:
                return true;
            case PlayerAttributeType.HealthRegen:
                return GetNextHealthRegenUpgradeAmount() > MinUpgradeableThreshold;
            case PlayerAttributeType.MoveSpeed:
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                return GetCurrentMoveSpeedBonusPercent() + MinUpgradeableThreshold <
                       moveSpeedCapPercent;
            case PlayerAttributeType.CritChance:
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                return critChance + MinUpgradeableThreshold < critCap;
            case PlayerAttributeType.DodgeChance:
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                return dodgeChance + MinUpgradeableThreshold < dodgeCap;
            case PlayerAttributeType.DamageReduction:
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                return damageReduction + MinUpgradeableThreshold < damageReductionCap;
            case PlayerAttributeType.LifeSteal:
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                return lifeSteal + MinUpgradeableThreshold < lifeStealCap;
            default:
                return false;
        }
    }

    /// <summary>
    /// Applies one upgrade choice and immediately refreshes player-facing UI/state.
    /// </summary>
    public bool TryApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        if (progressionComponent != null)
        {
            return progressionComponent.TryApplyAttributeUpgrade(attributeType);
        }

        // 真正应用一次升级选择。
        if (!CanApplyAttributeUpgrade(attributeType))
        {
            return false;
        }

        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                // 攻击力按当前攻击力百分比成长。
                float attackUpgradePercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                AtkPower = Mathf.Max(1, Mathf.CeilToInt(AtkPower * (1f + attackUpgradePercent)));
                break;
            case PlayerAttributeType.MaxHp:
                // 最大生命固定增加，并立刻恢复同等数值。
                int hpBonus = config != null ? config.playerMaxHpUpgradeFlat : 50;
                bonusMaxHp += hpBonus;
                RecalculateMaxHp(fillCurrentHp: false);
                RecoverHp(hpBonus);
                break;
            case PlayerAttributeType.MoveSpeed:
                // 移速有上限，避免无限叠到不可控。
                float moveSpeedUpgradePercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                float maxSpeed = baseMoveSpeed * (1f + moveSpeedCapPercent);
                Speed = Mathf.Min(maxSpeed, Speed * (1f + moveSpeedUpgradePercent));
                runSpeed = Speed * runSpeedMultiplier;
                break;
            case PlayerAttributeType.CritChance:
                float critUpgrade = config != null ? config.playerCritChanceUpgrade : 0.1f;
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                critChance = Mathf.Min(critCap, critChance + critUpgrade);
                break;
            case PlayerAttributeType.DodgeChance:
                float dodgeUpgrade = config != null ? config.playerDodgeChanceUpgrade : 0.1f;
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                dodgeChance = Mathf.Min(dodgeCap, dodgeChance + dodgeUpgrade);
                break;
            case PlayerAttributeType.HealthRegen:
                float regenUpgrade = GetNextHealthRegenUpgradeAmount();
                healthRegenPerSecond = Mathf.Min(GetHealthRegenCap(), healthRegenPerSecond + regenUpgrade);
                healthRegenUpgradeCount++;
                break;
            case PlayerAttributeType.DamageReduction:
                float damageReductionUpgrade = config != null ? config.playerDamageReductionUpgrade : 0.1f;
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                damageReduction = Mathf.Min(damageReductionCap, damageReduction + damageReductionUpgrade);
                break;
            case PlayerAttributeType.LifeSteal:
                float lifeStealUpgrade = config != null ? config.playerLifeStealUpgrade : 0.05f;
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                lifeSteal = Mathf.Min(lifeStealCap, lifeSteal + lifeStealUpgrade);
                break;
            default:
                return false;
        }

        // 数值变了，刷新 UI 并通知属性面板。
        UpdateHpUI();
        UpdateLvUI();
        NotifyStatsChanged();
        return true;
    }

    /// <summary>
    /// Returns three weighted, non-duplicated upgrade candidates for the level-up panel.
    /// </summary>
    public List<PlayerAttributeType> GetRandomUpgradeChoices(int choiceCount = 3)
    {
        if (progressionComponent != null)
        {
            return progressionComponent.GetRandomUpgradeChoices(choiceCount);
        }

        // 生成升级三选一。
        // 流程：列出全部属性 -> 过滤已到上限的属性 -> 按权重随机抽取且不重复。
        UpgradeChoiceBuffer.Clear();

        PlayerAttributeType[] allTypes =
        {
            PlayerAttributeType.AttackPower,
            PlayerAttributeType.MaxHp,
            PlayerAttributeType.MoveSpeed,
            PlayerAttributeType.CritChance,
            PlayerAttributeType.DodgeChance,
            PlayerAttributeType.HealthRegen,
            PlayerAttributeType.DamageReduction,
            PlayerAttributeType.LifeSteal
        };

        List<PlayerAttributeType> availableTypes = new List<PlayerAttributeType>(allTypes.Length);
        for (int i = 0; i < allTypes.Length; i++)
        {
            // 到达上限的属性不会出现在候选池里。
            if (CanApplyAttributeUpgrade(allTypes[i]))
            {
                availableTypes.Add(allTypes[i]);
            }
        }

        int resultCount = Mathf.Min(Mathf.Max(0, choiceCount), availableTypes.Count);
        for (int i = 0; i < resultCount; i++)
        {
            float totalWeight = 0f;
            for (int j = 0; j < availableTypes.Count; j++)
            {
                totalWeight += GetUpgradeWeight(availableTypes[j]);
            }

            if (totalWeight <= 0f)
            {
                break;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulatedWeight = 0f;
            int selectedIndex = 0;

            // 轮盘赌随机：权重越大，占的区间越长，被 roll 命中的概率越高。
            for (int j = 0; j < availableTypes.Count; j++)
            {
                accumulatedWeight += GetUpgradeWeight(availableTypes[j]);
                if (roll <= accumulatedWeight)
                {
                    selectedIndex = j;
                    break;
                }
            }

            UpgradeChoiceBuffer.Add(availableTypes[selectedIndex]);

            // 移除已选项，保证同一轮三选一不会重复。
            availableTypes.RemoveAt(selectedIndex);
        }

        return new List<PlayerAttributeType>(UpgradeChoiceBuffer);
    }

    /// <summary>
    /// 组合升级按钮的标题、效果、当前/下级预览和上限说明。
    /// </summary>
    public string GetUpgradeOptionText(PlayerAttributeType attributeType)
    {
        if (progressionComponent != null)
        {
            return progressionComponent.GetUpgradeOptionText(attributeType);
        }

        // 拼出升级按钮上的多行文字：标题、效果、当前->下级预览、上限说明。
        GameConfig config = GameConfig.instance;
        string title = config != null
            ? config.GetAttributeDisplayName(attributeType)
            : attributeType.ToString();
        string effect = GetUpgradeEffectText(attributeType);
        string preview = GetUpgradePreviewValueText(attributeType);
        string capText = config != null
            ? config.GetAttributeUpgradeCapText(attributeType)
            : string.Empty;

        return $"{title}\n{effect}\n{preview}\n{capText}";
    }

    /// <summary>
    /// 获取升级效果文案；生命恢复使用当前指数成长值动态显示。
    /// </summary>
    private string GetUpgradeEffectText(PlayerAttributeType attributeType)
    {
        if (attributeType == PlayerAttributeType.HealthRegen)
        {
            return $"+{GetNextHealthRegenUpgradeAmount():0.##}/s \u751f\u547d\u6062\u590d";
        }

        GameConfig config = GameConfig.instance;
        return config != null
            ? config.GetAttributeUpgradeEffectText(attributeType)
            : string.Empty;
    }

    /// <summary>
    /// 计算某个升级选项在当前属性下的“当前值 -> 升级后值”预览。
    /// </summary>
    private string GetUpgradePreviewValueText(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                float attackUpgradePercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                int nextAttack = Mathf.Max(1, Mathf.CeilToInt(AtkPower * (1f + attackUpgradePercent)));
                return $"\u5f53\u524d {AtkPower} -> {nextAttack}";
            case PlayerAttributeType.MaxHp:
                int hpBonus = config != null ? config.playerMaxHpUpgradeFlat : 50;
                return $"\u5f53\u524d {Hpmax} -> {Hpmax + hpBonus}";
            case PlayerAttributeType.MoveSpeed:
                float moveSpeedUpgradePercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                float maxSpeed = baseMoveSpeed * (1f + moveSpeedCapPercent);
                float nextMoveSpeed = Mathf.Min(maxSpeed, Speed * (1f + moveSpeedUpgradePercent));
                return $"\u5f53\u524d {FormatDecimal(Speed)} -> {FormatDecimal(nextMoveSpeed)}";
            case PlayerAttributeType.CritChance:
                float critUpgrade = config != null ? config.playerCritChanceUpgrade : 0.1f;
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                return $"\u5f53\u524d {FormatPercent(critChance)} -> {FormatPercent(Mathf.Min(critCap, critChance + critUpgrade))}";
            case PlayerAttributeType.DodgeChance:
                float dodgeUpgrade = config != null ? config.playerDodgeChanceUpgrade : 0.1f;
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                return $"\u5f53\u524d {FormatPercent(dodgeChance)} -> {FormatPercent(Mathf.Min(dodgeCap, dodgeChance + dodgeUpgrade))}";
            case PlayerAttributeType.HealthRegen:
                float regenUpgrade = GetNextHealthRegenUpgradeAmount();
                return $"\u5f53\u524d {healthRegenPerSecond:0.##}/s -> {Mathf.Min(GetHealthRegenCap(), healthRegenPerSecond + regenUpgrade):0.##}/s";
            case PlayerAttributeType.DamageReduction:
                float damageReductionUpgrade = config != null ? config.playerDamageReductionUpgrade : 0.1f;
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                return $"\u5f53\u524d {FormatPercent(damageReduction)} -> {FormatPercent(Mathf.Min(damageReductionCap, damageReduction + damageReductionUpgrade))}";
            case PlayerAttributeType.LifeSteal:
                float lifeStealUpgrade = config != null ? config.playerLifeStealUpgrade : 0.05f;
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                return $"\u5f53\u524d {FormatPercent(lifeSteal)} -> {FormatPercent(Mathf.Min(lifeStealCap, lifeSteal + lifeStealUpgrade))}";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 从配置读取某个属性在随机三选一中的基础权重。
    /// </summary>
    private float GetUpgradeWeight(PlayerAttributeType attributeType)
    {
        // 从 GameConfig 读取基础权重。
        GameConfig config = GameConfig.instance;
        return Mathf.Max(0f, config != null ? config.GetUpgradeBaseWeight(attributeType) : 1f);
    }

    /// <summary>
    /// 计算下一次生命恢复升级值：1、2、4、8...，并受总上限限制。
    /// </summary>
    private float GetNextHealthRegenUpgradeAmount()
    {
        GameConfig config = GameConfig.instance;
        float baseUpgrade = config != null ? config.playerHpRegenUpgrade : 1f;
        float exponentialUpgrade = baseUpgrade * Mathf.Pow(2f, Mathf.Max(0, healthRegenUpgradeCount));
        float remainingToCap = GetHealthRegenCap() - healthRegenPerSecond;

        return Mathf.Max(0f, Mathf.Min(exponentialUpgrade, remainingToCap));
    }

    /// <summary>
    /// 获取生命恢复每秒总上限。
    /// </summary>
    private float GetHealthRegenCap()
    {
        GameConfig config = GameConfig.instance;
        return config != null ? Mathf.Max(0f, config.playerHpRegenCap) : 32f;
    }

    /// <summary>
    /// 计算当前移动速度相对基础移动速度的加成百分比。
    /// </summary>
    private float GetCurrentMoveSpeedBonusPercent()
    {
        if (baseMoveSpeed <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, Speed / baseMoveSpeed - 1f);
    }

    /// <summary>
    /// 根据等级基础生命和额外生命重新计算最大生命。
    /// </summary>
    internal void RecalculateMaxHp(bool fillCurrentHp)
    {
        // 重新计算最大血量。升级/等级变化时会调用。
        int previousMaxHp = Mathf.Max(1, Hpmax);

        // 记录旧血量百分比，最大血量变化后尽量保持当前血量比例。
        float hpPercent = previousMaxHp > 0 ? (float)Hp / previousMaxHp : 1f;

        Hpmax = Mathf.Max(1, GetLevelBaseMaxHp(Lv) + bonusMaxHp);

        if (fillCurrentHp)
        {
            Hp = Hpmax;
        }
        else if (previousMaxHp > 0)
        {
            Hp = Mathf.Clamp(Mathf.CeilToInt(Hpmax * hpPercent), 0, Hpmax);
        }
        else
        {
            Hp = Mathf.Clamp(Hp, 0, Hpmax);
        }
    }

    /// <summary>
    /// 从配置读取指定等级的基础最大生命。
    /// </summary>
    private int GetLevelBaseMaxHp(int level)
    {
        if (entryCharacterDefine != null && entryCharacterDefine.hp > 0f)
        {
            int characterBaseHp = Mathf.Max(1, Mathf.RoundToInt(entryCharacterDefine.hp));
            GameConfig characterConfig = GameConfig.instance;
            if (characterConfig == null)
            {
                return characterBaseHp;
            }

            int levelOneHp = characterConfig.getMaxHp(1);
            int levelHp = characterConfig.getMaxHp(level);
            int growthHp = Mathf.Max(0, levelHp - levelOneHp);
            return characterBaseHp + growthHp;
        }

        GameConfig config = GameConfig.instance;
        if (config != null)
        {
            return Mathf.Max(1, config.getMaxHp(level));
        }

        return Mathf.Max(1, baseMaxHp);
    }

    /// <summary>
    /// 从配置读取指定等级升到下一级所需经验。
    /// </summary>
    internal int GetNextExpForLevel(int level)
    {
        if (GameConfig.instance == null)
        {
            return Mathf.Max(1, curExpMax > 0 ? curExpMax : 50);
        }

        return Mathf.Max(1, GameConfig.instance.getNextExp(level));
    }

    /// <summary>
    /// Provides grouped, preformatted rows for the runtime attribute panel.
    /// </summary>
    public void GetAttributePanelEntries(List<AttributePanelEntry> results)
    {
        // 属性面板调用这里拿要显示的数据。
        // 这里不直接操作 UI，只负责准备“分组、key、标签、数值”。
        if (results == null)
        {
            return;
        }

        results.Clear();

        string overviewGroup = "\u6982\u89c8";
        string combatGroup = "\u6218\u6597";
        string survivalGroup = "\u751f\u5b58";

        GameConfig config = GameConfig.instance;
        string attackName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.AttackPower)
            : "\u653b\u51fb\u529b";
        string maxHpName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.MaxHp)
            : "\u6700\u5927\u751f\u547d";
        string moveSpeedName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.MoveSpeed)
            : "\u79fb\u52a8\u901f\u5ea6";
        string critChanceName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.CritChance)
            : "\u66b4\u51fb\u7387";
        string dodgeChanceName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.DodgeChance)
            : "\u95ea\u907f\u7387";
        string healthRegenName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.HealthRegen)
            : "\u751f\u547d\u6062\u590d";
        string damageReductionName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.DamageReduction)
            : "\u4f24\u5bb3\u51cf\u514d";
        string lifeStealName = config != null
            ? config.GetAttributeDisplayName(PlayerAttributeType.LifeSteal)
            : "\u5438\u8840";

        AddAttributePanelEntry(results, overviewGroup, "level", "\u7b49\u7ea7", Lv.ToString());
        AddAttributePanelEntry(results, overviewGroup, "exp", "\u7ecf\u9a8c", $"{curExp}/{curExpMax}");
        AddAttributePanelEntry(results, overviewGroup, "current_hp", "\u5f53\u524d\u751f\u547d", $"{Hp}/{Hpmax}");
        AddAttributePanelEntry(results, overviewGroup, "max_hp", maxHpName, Hpmax.ToString());
        AddAttributePanelEntry(results, overviewGroup, "move_speed", moveSpeedName, FormatDecimal(Speed));

        AddAttributePanelEntry(results, combatGroup, "attack_power", attackName, AtkPower.ToString());
        AddAttributePanelEntry(results, combatGroup, "crit_chance", critChanceName, FormatPercent(critChance));
        AddAttributePanelEntry(results, combatGroup, "crit_damage", "\u66b4\u51fb\u4f24\u5bb3", $"{critDamageMultiplier:0.00}x");

        AddAttributePanelEntry(results, survivalGroup, "dodge_chance", dodgeChanceName, FormatPercent(dodgeChance));
        AddAttributePanelEntry(results, survivalGroup, "health_regen", healthRegenName, $"{healthRegenPerSecond:0.##}/s");
        AddAttributePanelEntry(results, survivalGroup, "damage_reduction", damageReductionName, FormatPercent(damageReduction));
        AddAttributePanelEntry(results, survivalGroup, "life_steal", lifeStealName, FormatPercent(lifeSteal));
    }

    /// <summary>
    /// 返回旧版纯文本属性面板内容，供调试或兼容旧 UI 使用。
    /// </summary>
    public string GetAttributePanelText()
    {
        return
            $"\u7b49\u7ea7\uff1a{Lv}\n" +
            $"\u7ecf\u9a8c\uff1a{curExp}/{curExpMax}\n" +
            $"\u5f53\u524d\u751f\u547d\uff1a{Hp}/{Hpmax}\n" +
            $"\u6700\u5927\u751f\u547d\uff1a{Hpmax}\n" +
            $"\u653b\u51fb\u529b\uff1a{AtkPower}\n" +
            $"\u79fb\u52a8\u901f\u5ea6\uff1a{FormatDecimal(Speed)}\n" +
            $"\u66b4\u51fb\u7387\uff1a{FormatPercent(critChance)}\n" +
            $"\u66b4\u51fb\u4f24\u5bb3\uff1a{critDamageMultiplier:0.00}x\n" +
            $"\u95ea\u907f\u7387\uff1a{FormatPercent(dodgeChance)}\n" +
            $"\u751f\u547d\u6062\u590d\uff1a{healthRegenPerSecond:0.##}/s\n" +
            $"\u4f24\u5bb3\u51cf\u514d\uff1a{FormatPercent(damageReduction)}\n" +
            $"\u5438\u8840\uff1a{FormatPercent(lifeSteal)}";
    }

    /// <summary>
    /// 向属性面板数据列表添加一行。
    /// </summary>
    private void AddAttributePanelEntry(List<AttributePanelEntry> results, string groupName, string key, string label, string value)
    {
        results.Add(new AttributePanelEntry(groupName, key, label, value));
    }

    /// <summary>
    /// 把 0-1 小数格式化为整数百分比文本。
    /// </summary>
    private string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    /// <summary>
    /// 把浮点数格式化为两位小数文本。
    /// </summary>
    private string FormatDecimal(float value)
    {
        return value.ToString("0.00");
    }

    /// <summary>
    /// 通知属性面板等订阅者：玩家属性发生了变化。
    /// </summary>
    internal void NotifyStatsChanged()
    {
        // 通知所有订阅者：玩家属性变了。
        StatsChanged?.Invoke();
    }

    /// <summary>
    /// 通知升级面板：待选择升级次数发生了变化。
    /// </summary>
    internal void NotifyPendingUpgradeSelectionsChanged()
    {
        // 通知升级面板：还有几次升级选择没有完成。
        PendingUpgradeSelectionsChanged?.Invoke(pendingUpgradeSelectionCount);
    }

    // Debug shortcut for quick local verification of level-up flow.
    private void Test()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            AddExp(100);
        }
    }
}
