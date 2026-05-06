using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 金库/宝箱控制器。
/// 负责处理受击、击破奖励、重生无敌、血条显示，以及对外广播“金库被击破”事件。
/// 
/// 新手阅读顺序：
/// 1. 玩家武器打到金库时，会通过 FighterInterface 调用 Hit。
/// 2. Hit 转到 TakeDamage，扣血并刷新 UI。
/// 3. 血量归零后 HandleDestroyed 负责发奖励、升级金库难度、广播事件、启动重生流程。
/// 4. RespawnAfterBreak 是协程，用等待和缩放动画表现“击破 -> 重生 -> 无敌一小会儿”。
/// 5. OnVaultStatsChanged / OnVaultDestroyed 是事件，HUD 和怪物成长会订阅它们。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class BoxCo : MonoBehaviour, FighterInterface
{
    /// <summary>
    /// 保存一个材质的默认颜色。
    /// 
    /// 新手理解：
    /// 金库受击时会短暂变红，但不同 Shader 使用的颜色属性名可能不同。
    /// 所以这里记录这个材质到底有哪些颜色属性，以及它们原本是什么颜色。
    /// </summary>
    private sealed class MaterialFlashState
    {
        // 需要被改色的运行时材质实例。
        public Material Material;
        // 这些布尔值记录当前材质支持哪些颜色属性，避免 SetColor 时访问不存在的属性。
        public bool HasColor;
        public bool HasBaseColor;
        public bool HasColor01;
        public bool HasColor02;
        public bool HasColor03;
        // 下面保存每个颜色属性的默认值，用于受击/无敌结束后恢复。
        public Color DefaultColor;
        public Color DefaultBaseColor;
        public Color DefaultColor01;
        public Color DefaultColor02;
        public Color DefaultColor03;
    }

    private const string ColorProperty = "_Color";
    private const string BaseColorProperty = "_BaseColor";
    private const string Color01Property = "_Color01";
    private const string Color02Property = "_Color02";
    private const string Color03Property = "_Color03";
    private const float DestroyExpRewardExponent = 1.2f;

    /// <summary>
    /// 方便其他系统快速访问当前场景中的金库。
    /// </summary>
    public static BoxCo instance;

    /// <summary>
    /// 金库被击破时广播，后续难度系统/刷怪系统可以订阅这个事件。
    /// </summary>
    public static event Action<BoxCo> OnVaultDestroyed;
    public static event Action<BoxCo> OnVaultStatsChanged;

    [Header("基础数值")]
    // 第 1 个金库的基础生命。
    [SerializeField] private int baseMaxHp = 200;

    // 每击破一次，下一轮金库生命按这个倍率增长。
    [SerializeField] private float hpGrowthPerDestroy = 1.6f;

    // 对外暴露的难度倍率，其他系统可以用它做扩展。
    [SerializeField] private float difficultyGrowthPerDestroy = 1.1f;

    // 金库重生后无敌多久，防止刚出现就被连击瞬间打爆。
    [SerializeField] private float invincibleDuration = 3f;

    [Header("击破/重生流程")]
    [Tooltip("击破反馈持续时间。没有 Animator 动画时，会作为缩小消失动画的时长。")]
    [SerializeField] private float breakAnimationDuration = 0.6f;

    [Tooltip("击破反馈结束后，等待多久开始重生。")]
    [SerializeField] private float respawnDelay = 0.2f;

    [Tooltip("重生反馈持续时间。没有 Animator 动画时，会作为放大出现动画的时长。")]
    [SerializeField] private float respawnAnimationDuration = 0.6f;

    [Tooltip("重生流程中是否继续保留碰撞体。建议勾选，防止玩家/怪物穿进箱子位置。")]
    [SerializeField] private bool keepColliderEnabledDuringRespawn = true;

    [Tooltip("没有 Break/Respawn 动画片段时，是否用缩放动画做兜底表现。")]
    [SerializeField] private bool playScaleFallbackAnimation = true;

    [Tooltip("用于播放缩放兜底动画的物体。为空时使用当前物体。")]
    [SerializeField] private Transform animationRoot;

    [Tooltip("击破后缩到原始大小的比例。0.05 表示几乎消失，但不会真的销毁物体。")]
    [SerializeField] private float destroyedScale = 0.05f;

    [Header("奖励设置")]
    // 击破金库给玩家的基础经验。
    [SerializeField] private int baseDestroyExpReward = 100;

    // 击破次数成长奖励的系数。实际公式：基础经验 + 系数 * 当前击破次数 ^ 1.2。
    [SerializeField] private int expRewardStep = 20;

    // 造成多少点伤害换算成 1 分。
    [SerializeField] private int scoreDamagePerPoint = 20;
    [SerializeField] private bool fullHealPlayerOnDestroy = false;
    [SerializeField] private GameObject extraExpPickupPrefab;
    [SerializeField] private int extraExpPickupReward = 20;
    [SerializeField] private Transform rewardSpawnPoint;

    [Header("场景引用")]
    [SerializeField] private Collider damageCollider;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Animator animator;
    [SerializeField] private string breakTriggerName = "Break";
    [SerializeField] private string respawnTriggerName = "Respawn";
    [SerializeField] private GameObject invincibleBubble;
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private GameObject breakVfxPrefab;
    [SerializeField] private GameObject respawnVfxPrefab;
    [SerializeField] private Transform effectSpawnPoint;
    [SerializeField] private Transform hpBillboard;
    [SerializeField] private Image hpBar;
    [SerializeField] private Text hpText;

    [Header("受击闪红")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.26f, 0.26f, 1f);
    [SerializeField] private float hitFlashDuration = 0.12f;
    [SerializeField] private Renderer[] hitFlashRenderers;
    [SerializeField] private Color invincibleTintColor = new Color(1f, 0.88f, 0.05f, 1f);

    [Header("运行时状态（只读）")]
    // vaultLevel 等于已经被击破的次数，也可以理解为当前难度层级。
    [SerializeField] private int vaultLevel;

    // 累计分数：之前完整击破的分数 + 当前金库已造成伤害换算的分数。
    [SerializeField] private int score;
    [SerializeField] private int currentVaultDamage;
    [SerializeField] private int currentVaultScore;
    [SerializeField] private int currentHp;
    [SerializeField] private int maxHp;
    [SerializeField] private bool isInvincible;
    [SerializeField] private float invincibleTimer;
    [SerializeField] private bool isRespawning;

    // 保存原始缩放，重生结束后恢复，避免多次击破后模型越缩越小。
    private Coroutine respawnRoutine;
    private Vector3 originalAnimationScale;
    private bool hasOriginalAnimationScale;
    private readonly List<MaterialFlashState> hitFlashStates = new List<MaterialFlashState>(8);
    private float hitFlashTimer;

    // 对外只读属性，HUD 和怪物成长系统通过这些属性读取金库当前状态。
    public int VaultLevel => vaultLevel;
    public int Score => score;
    public int CurrentVaultDamage => currentVaultDamage;
    public int CurrentVaultScore => currentVaultScore;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool IsInvincible => isInvincible;
    public bool IsRespawning => isRespawning;
    public int DestroyedCount => vaultLevel;
    public int CurrentRound => vaultLevel + 1;

    /// <summary>
    /// 当前难度倍率。击破次数越高，返回值越大。
    /// </summary>
    public float DifficultyMultiplier => Mathf.Pow(difficultyGrowthPerDestroy, vaultLevel);

    /// <summary>
    /// 初始化金库单例、组件引用、物理设置和当前层级血量。
    /// </summary>
    private void Awake()
    {
        // 设置单例，方便 HUD、怪物等脚本快速找到当前金库。
        instance = this;

        // 把 Inspector 没拖的引用自动补上。
        CacheReferences();

        // 金库不需要物理运动，但需要碰撞/触发检测，所以要配置 Rigidbody。
        EnsurePhysicsSetup();

        // 按当前等级计算血量。
        ApplyVaultStatsForLevel(keepCurrentHp: false);
        SetInvincibleState(false);
    }

    /// <summary>
    /// 场景开始时刷新一次血条显示。
    /// </summary>
    private void Start()
    {
        UpdateHpUI();
    }

    /// <summary>
    /// 每帧更新无敌倒计时和受击闪色恢复。
    /// </summary>
    private void Update()
    {
        // 每帧更新无敌倒计时和受击闪红恢复。
        UpdateInvincibleTimer();
        UpdateHitFlash();
    }

    /// <summary>
    /// 摄像机更新后再让世界空间血条面向摄像机。
    /// </summary>
    private void LateUpdate()
    {
        // 血条是世界空间 UI，需要在摄像机更新后再朝向摄像机。
        UpdateHpBillboard();
    }

    /// <summary>
    /// Inspector 数值变化时修正非法配置，并刷新编辑器预览数值。
    /// </summary>
    private void OnValidate()
    {
        // OnValidate 在编辑器里改 Inspector 数值时执行。
        // 这里把不合理的负数/0 修正掉，避免运行时出错。
        baseMaxHp = Mathf.Max(1, baseMaxHp);
        hpGrowthPerDestroy = Mathf.Max(1f, hpGrowthPerDestroy);
        difficultyGrowthPerDestroy = Mathf.Max(1f, difficultyGrowthPerDestroy);
        invincibleDuration = Mathf.Max(0f, invincibleDuration);
        breakAnimationDuration = Mathf.Max(0f, breakAnimationDuration);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        respawnAnimationDuration = Mathf.Max(0f, respawnAnimationDuration);
        destroyedScale = Mathf.Clamp(destroyedScale, 0f, 1f);
        baseDestroyExpReward = Mathf.Max(0, baseDestroyExpReward);
        expRewardStep = Mathf.Max(0, expRewardStep);
        scoreDamagePerPoint = Mathf.Max(1, scoreDamagePerPoint);
        extraExpPickupReward = Mathf.Max(0, extraExpPickupReward);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        vaultLevel = Mathf.Max(0, vaultLevel);

        if (damageCollider == null)
        {
            damageCollider = GetComponent<Collider>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        ResolveHpUiReferences();

        maxHp = CalculateMaxHp(vaultLevel);
        if (currentHp <= 0 || currentHp > maxHp)
        {
            currentHp = maxHp;
        }

        RefreshScoreFromCurrentProgress();
    }

    /// <summary>
    /// FighterInterface 入口，让玩家武器可以直接打到金库。
    /// </summary>
    public void Hit(int atkPower)
    {
        // 实现 FighterInterface，让 WeaponCo 不用知道这是金库还是怪物。
        TakeDamage(atkPower);
    }

    /// <summary>
    /// 文档要求的受伤接口：扣血、判定击破、触发无敌重生。
    /// </summary>
    public void TakeDamage(int damage)
    {
        // 无效伤害、无敌中、重生中都不扣血。
        if (damage <= 0 || isInvincible || isRespawning)
        {
            return;
        }

        // 扣血后立刻刷新分数、特效、血条和事件。
        currentHp = Mathf.Max(0, currentHp - damage);
        RefreshScoreFromCurrentProgress();
        SpawnEffect(hitVfxPrefab);
        TriggerHitFlash();
        UpdateHpUI();
        NotifyVaultStatsChanged();

        if (currentHp > 0)
        {
            // 还没被打爆就结束。
            return;
        }

        // 血量为 0，进入击破流程。
        HandleDestroyed();
    }

    /// <summary>
    /// 外部如果需要重开或调试，可以直接把金库恢复到初始状态。
    /// </summary>
    public void ResetVault()
    {
        // 如果正在重生协程中，先停止，避免重置后协程继续改状态。
        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
            respawnRoutine = null;
        }

        isRespawning = false;

        // 回到初始轮次、初始分数、满血状态。
        vaultLevel = 0;
        score = 0;
        currentVaultDamage = 0;
        currentVaultScore = 0;
        ApplyVaultStatsForLevel(keepCurrentHp: false);
        SetInvincibleState(false);
        RestoreAnimationScale();
        RestoreHitFlashState();
        UpdateHpUI();
        NotifyVaultStatsChanged();
    }

    /// <summary>
    /// 读取当前层级对应的击破经验奖励。
    /// 这里按“击破前已有次数”结算：奖励 = 100 + 20 * 当前击破次数 ^ 1.2。
    /// </summary>
    public int GetCurrentDestroyExpReward()
    {
        int destroyedCount = Mathf.Max(0, vaultLevel);
        return baseDestroyExpReward +
               Mathf.RoundToInt(expRewardStep * Mathf.Pow(destroyedCount, DestroyExpRewardExponent));
    }

    /// <summary>
    /// 读取“下一次重生后”的最大血量，方便外部 UI 预览难度。
    /// </summary>
    public int GetNextMaxHp()
    {
        return CalculateMaxHp(vaultLevel + 1);
    }

    /// <summary>
    /// 获取当前金库完整击破可贡献的分数。
    /// </summary>
    public int GetCurrentVaultFullScore()
    {
        return CalculateScoreFromDamage(maxHp);
    }

    /// <summary>
    /// 获取当前金库已经因受伤贡献的分数。
    /// </summary>
    public int GetCurrentVaultEarnedScore()
    {
        return currentVaultScore;
    }

    /// <summary>
    /// 缓存碰撞体、刚体、动画器、血条和材质状态等引用。
    /// </summary>
    private void CacheReferences()
    {
        // 这些引用可以手动拖，也可以让脚本自动在当前物体/子物体里找。
        if (damageCollider == null)
        {
            damageCollider = GetComponent<Collider>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (animationRoot == null)
        {
            animationRoot = transform;
        }

        ResolveHpUiReferences();
        CacheOriginalAnimationScale();
        CacheHitFlashStates();
    }

    /// <summary>
    /// 自动查找金库身上的血条 Image/Text 和血条朝向根节点。
    /// </summary>
    private void ResolveHpUiReferences()
    {
        // 尝试自动找到血条 Image 和血量 Text。
        if (hpBar == null)
        {
            hpBar = GetComponentInChildren<Image>(true);
        }

        if (hpText == null)
        {
            hpText = GetComponentInChildren<Text>(true);
        }

        if (hpBillboard != null)
        {
            return;
        }

        if (hpBar != null)
        {
            Canvas hpCanvas = hpBar.GetComponentInParent<Canvas>();
            if (hpCanvas != null && hpCanvas.renderMode == RenderMode.WorldSpace)
            {
                // 世界空间 Canvas 需要朝向摄像机，所以记录它的 Transform。
                hpBillboard = hpCanvas.transform;
                return;
            }

            RectTransform hpRect = hpBar.rectTransform;
            if (hpRect != null)
            {
                hpBillboard = hpRect.parent;
            }
        }
    }

    /// <summary>
    /// 确保金库有用于 Trigger 检测的运动学 Rigidbody。
    /// </summary>
    private void EnsurePhysicsSetup()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // 宝箱不需要物理运动，只需要满足 Trigger 碰撞回调条件。
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    /// <summary>
    /// 处理金库血量归零后的奖励、难度提升、事件广播和重生流程。
    /// </summary>
    private void HandleDestroyed()
    {
        if (isRespawning)
        {
            return;
        }

        // 标记正在重生，避免重复进入击破流程。
        isRespawning = true;
        int destroyExpReward = GetCurrentDestroyExpReward();

        // 击破次数 +1。后续 CalculateMaxHp 会按新层级计算下一轮血量。
        vaultLevel++;

        SpawnEffect(breakVfxPrefab);
        TrySetAnimatorTrigger(breakTriggerName);

        RewardPlayer(destroyExpReward);
        SpawnExtraExpPickup();

        // 击破后立刻按新层级重算血量，表示金库已经“重生”，但会进入短暂无敌期。
        ApplyVaultStatsForLevel(keepCurrentHp: false);
        StartInvincibilityWindow();

        OnVaultDestroyed?.Invoke(this);
        NotifyVaultStatsChanged();

        if (respawnRoutine != null)
        {
            StopCoroutine(respawnRoutine);
        }

        respawnRoutine = StartCoroutine(RespawnAfterBreak());
    }

    /// <summary>
    /// 播放击破消失、等待、重生出现和短暂无敌的完整协程。
    /// </summary>
    private IEnumerator RespawnAfterBreak()
    {
        // 协程可以跨多帧执行，yield return 表示“等一会儿再继续”。
        CacheOriginalAnimationScale();
        SetInvincibleState(true);

        if (damageCollider != null)
        {
            damageCollider.enabled = keepColliderEnabledDuringRespawn;
        }

        yield return PlayScaleAnimation(
            GetCurrentAnimationScale(),
            GetDestroyedAnimationScale(),
            breakAnimationDuration);

        // 击破动画结束后，可选等待一小段时间再重生。
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        ApplyVaultStatsForLevel(keepCurrentHp: false);
        SpawnEffect(respawnVfxPrefab);
        TrySetAnimatorTrigger(respawnTriggerName);

        yield return PlayScaleAnimation(
            GetCurrentAnimationScale(),
            originalAnimationScale,
            respawnAnimationDuration);

        // 重生动画结束，恢复碰撞和显示状态。
        RestoreAnimationScale();
        RestoreHitFlashState();
        isRespawning = false;

        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }

        StartInvincibilityWindow(playRespawnFeedback: false);
        respawnRoutine = null;
        NotifyVaultStatsChanged();
    }

    /// <summary>
    /// 使用缩放作为没有 Animator 时的击破/重生兜底动画。
    /// </summary>
    private IEnumerator PlayScaleAnimation(Vector3 fromScale, Vector3 toScale, float duration)
    {
        // 如果不使用缩放兜底动画，就只等待同样的时长，保持流程节奏一致。
        if (!playScaleFallbackAnimation || animationRoot == null)
        {
            if (duration > 0f)
            {
                yield return new WaitForSeconds(duration);
            }

            yield break;
        }

        if (duration <= 0f)
        {
            animationRoot.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // t 从 0 慢慢变到 1，Lerp 根据 t 在起始缩放和目标缩放之间过渡。
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            animationRoot.localScale = Vector3.Lerp(fromScale, toScale, t);
            yield return null;
        }

        animationRoot.localScale = toScale;
    }

    /// <summary>
    /// 读取当前动画根节点缩放。
    /// </summary>
    private Vector3 GetCurrentAnimationScale()
    {
        return animationRoot != null ? animationRoot.localScale : Vector3.one;
    }

    /// <summary>
    /// 计算击破消失动画的目标缩放。
    /// </summary>
    private Vector3 GetDestroyedAnimationScale()
    {
        CacheOriginalAnimationScale();
        return originalAnimationScale * destroyedScale;
    }

    /// <summary>
    /// 记录动画根节点初始缩放，重生后用于恢复。
    /// </summary>
    private void CacheOriginalAnimationScale()
    {
        if (hasOriginalAnimationScale || animationRoot == null)
        {
            return;
        }

        originalAnimationScale = animationRoot.localScale;
        hasOriginalAnimationScale = true;
    }

    /// <summary>
    /// 收集可变色材质及其默认颜色，用于受击和无敌染色。
    /// </summary>
    private void CacheHitFlashStates()
    {
        // 收集所有可闪红的材质，并记录默认颜色。
        hitFlashStates.Clear();

        if (hitFlashRenderers == null || hitFlashRenderers.Length == 0)
        {
            hitFlashRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (hitFlashRenderers == null)
        {
            return;
        }

        for (int i = 0; i < hitFlashRenderers.Length; i++)
        {
            Renderer renderer = hitFlashRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                MaterialFlashState state = new MaterialFlashState
                {
                    Material = material,
                    HasColor = material.HasProperty(ColorProperty),
                    HasBaseColor = material.HasProperty(BaseColorProperty),
                    HasColor01 = material.HasProperty(Color01Property),
                    HasColor02 = material.HasProperty(Color02Property),
                    HasColor03 = material.HasProperty(Color03Property)
                };

                if (!state.HasColor &&
                    !state.HasBaseColor &&
                    !state.HasColor01 &&
                    !state.HasColor02 &&
                    !state.HasColor03)
                {
                    // 这个材质没有可改的颜色属性，跳过。
                    continue;
                }

                if (state.HasColor)
                {
                    state.DefaultColor = material.GetColor(ColorProperty);
                }

                if (state.HasBaseColor)
                {
                    state.DefaultBaseColor = material.GetColor(BaseColorProperty);
                }

                if (state.HasColor01)
                {
                    state.DefaultColor01 = material.GetColor(Color01Property);
                }

                if (state.HasColor02)
                {
                    state.DefaultColor02 = material.GetColor(Color02Property);
                }

                if (state.HasColor03)
                {
                    state.DefaultColor03 = material.GetColor(Color03Property);
                }

                hitFlashStates.Add(state);
            }
        }
    }

    /// <summary>
    /// 把动画根节点缩放恢复到初始值。
    /// </summary>
    private void RestoreAnimationScale()
    {
        CacheOriginalAnimationScale();
        if (animationRoot != null && hasOriginalAnimationScale)
        {
            animationRoot.localScale = originalAnimationScale;
        }
    }

    /// <summary>
    /// 触发受击闪色反馈。
    /// </summary>
    private void TriggerHitFlash()
    {
        // 受击时把所有记录过的颜色属性临时改成 hitFlashColor。
        if (hitFlashDuration <= 0f)
        {
            return;
        }

        if (hitFlashStates.Count == 0)
        {
            CacheHitFlashStates();
        }

        if (hitFlashStates.Count == 0)
        {
            return;
        }

        hitFlashTimer = hitFlashDuration;
        for (int i = 0; i < hitFlashStates.Count; i++)
        {
            ApplyHitFlashColor(hitFlashStates[i]);
        }
    }

    /// <summary>
    /// 更新受击闪色计时，到点后恢复材质颜色。
    /// </summary>
    private void UpdateHitFlash()
    {
        if (hitFlashTimer <= 0f)
        {
            return;
        }

        hitFlashTimer -= Time.deltaTime;
        if (hitFlashTimer > 0f)
        {
            return;
        }

        RestoreHitFlashState();
    }

    /// <summary>
    /// 根据当前是否无敌恢复到默认颜色或无敌染色。
    /// </summary>
    private void RestoreHitFlashState()
    {
        hitFlashTimer = 0f;

        if (isInvincible)
        {
            ApplyTintColor(invincibleTintColor);
            return;
        }

        RestoreDefaultMaterialColors();
    }

    /// <summary>
    /// 把所有缓存材质颜色恢复到默认值。
    /// </summary>
    private void RestoreDefaultMaterialColors()
    {
        // 把所有材质颜色恢复到 CacheHitFlashStates 记录的默认值。
        if (hitFlashStates.Count == 0)
        {
            CacheHitFlashStates();
        }

        for (int i = 0; i < hitFlashStates.Count; i++)
        {
            MaterialFlashState state = hitFlashStates[i];
            if (state.Material == null)
            {
                continue;
            }

            if (state.HasColor)
            {
                state.Material.SetColor(ColorProperty, state.DefaultColor);
            }

            if (state.HasBaseColor)
            {
                state.Material.SetColor(BaseColorProperty, state.DefaultBaseColor);
            }

            if (state.HasColor01)
            {
                state.Material.SetColor(Color01Property, state.DefaultColor01);
            }

            if (state.HasColor02)
            {
                state.Material.SetColor(Color02Property, state.DefaultColor02);
            }

            if (state.HasColor03)
            {
                state.Material.SetColor(Color03Property, state.DefaultColor03);
            }
        }
    }

    /// <summary>
    /// 把单个材质状态切换到受击闪色。
    /// </summary>
    private void ApplyHitFlashColor(MaterialFlashState state)
    {
        ApplyTintColor(state, hitFlashColor);
    }

    /// <summary>
    /// 给所有可变色材质统一套用一个颜色。
    /// </summary>
    private void ApplyTintColor(Color color)
    {
        if (hitFlashStates.Count == 0)
        {
            CacheHitFlashStates();
        }

        for (int i = 0; i < hitFlashStates.Count; i++)
        {
            ApplyTintColor(hitFlashStates[i], color);
        }
    }

    /// <summary>
    /// 给单个材质状态记录的所有颜色属性写入同一个颜色。
    /// </summary>
    private void ApplyTintColor(MaterialFlashState state, Color color)
    {
        if (state == null || state.Material == null)
        {
            return;
        }

        if (state.HasColor)
        {
            state.Material.SetColor(ColorProperty, color);
        }

        if (state.HasBaseColor)
        {
            state.Material.SetColor(BaseColorProperty, color);
        }

        if (state.HasColor01)
        {
            state.Material.SetColor(Color01Property, color);
        }

        if (state.HasColor02)
        {
            state.Material.SetColor(Color02Property, color);
        }

        if (state.HasColor03)
        {
            state.Material.SetColor(Color03Property, color);
        }
    }

    /// <summary>
    /// 给玩家发放击破经验，并按配置决定是否治疗玩家。
    /// </summary>
    private void RewardPlayer(int destroyExpReward)
    {
        // 击破奖励直接发给 PlayerCo.instance。
        PlayerCo player = PlayerCo.instance;
        if (player == null)
        {
            return;
        }

        if (destroyExpReward > 0)
        {
            player.AddExp(destroyExpReward);
        }

        // The design doc wants a full heal on vault break.
        // The per-vault checkbox still works, but the central GameConfig can force the reward globally.
        bool shouldFullHealPlayer =
            fullHealPlayerOnDestroy ||
            (GameConfig.instance != null && GameConfig.instance.fullHealPlayerOnVaultDestroy);

        if (!shouldFullHealPlayer)
        {
            return;
        }

        player.FullHeal();
    }

    /// <summary>
    /// 额外生成经验拾取物，并尝试把经验数值写给拾取物脚本。
    /// </summary>
    private void SpawnExtraExpPickup()
    {
        if (extraExpPickupPrefab == null)
        {
            return;
        }

        Transform spawnRoot = rewardSpawnPoint != null ? rewardSpawnPoint : transform;
        GameObject pickup = Instantiate(
            extraExpPickupPrefab,
            spawnRoot.position,
            spawnRoot.rotation);

        // 预留兼容口：如果经验球脚本支持这些消息，就把数值塞进去。
        pickup.SendMessage("SetExpReward", extraExpPickupReward, SendMessageOptions.DontRequireReceiver);
        pickup.SendMessage("SetExp", extraExpPickupReward, SendMessageOptions.DontRequireReceiver);
    }

    /// <summary>
    /// 根据当前击破次数重算金库最大生命和当前生命。
    /// </summary>
    private void ApplyVaultStatsForLevel(bool keepCurrentHp)
    {
        // 根据当前 vaultLevel 重新计算最大血量。
        if (isRespawning && respawnRoutine == null)
        {
            return;
        }

        maxHp = CalculateMaxHp(vaultLevel);

        if (!keepCurrentHp)
        {
            // 新一轮开始时满血。
            currentHp = maxHp;
        }
        else
        {
            // 只改最大血量时，把当前血量夹到合法范围。
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        }

        RefreshScoreFromCurrentProgress();
        UpdateHpUI();
        NotifyVaultStatsChanged();
    }

    /// <summary>
    /// 按基础生命和成长倍率计算指定层级的金库最大生命。
    /// </summary>
    private int CalculateMaxHp(int level)
    {
        // 每层血量 = 基础血量 * 成长倍率 ^ 层级。
        float scaledHp = baseMaxHp * Mathf.Pow(hpGrowthPerDestroy, Mathf.Max(0, level));
        return Mathf.Max(1, Mathf.CeilToInt(scaledHp));
    }

    /// <summary>
    /// 把对金库造成的伤害换算成分数。
    /// </summary>
    private int CalculateScoreFromDamage(int damageAmount)
    {
        return Mathf.Max(0, damageAmount) / scoreDamagePerPoint;
    }

    /// <summary>
    /// 计算进入某层级之前，所有已完整击破金库累计贡献的分数。
    /// </summary>
    private int CalculateAccumulatedScoreBeforeLevel(int level)
    {
        int totalScore = 0;

        for (int i = 0; i < Mathf.Max(0, level); i++)
        {
            totalScore += CalculateScoreFromDamage(CalculateMaxHp(i));
        }

        return totalScore;
    }

    /// <summary>
    /// 根据当前血量刷新当前金库伤害、当前金库分数和总分。
    /// </summary>
    private void RefreshScoreFromCurrentProgress()
    {
        // 当前金库已经被打掉多少血。
        currentVaultDamage = Mathf.Clamp(maxHp - currentHp, 0, maxHp);

        // 当前这一个金库贡献了多少分。
        currentVaultScore = CalculateScoreFromDamage(currentVaultDamage);

        // 总分 = 之前完整击破的金库分数 + 当前金库已打出的分数。
        score = CalculateAccumulatedScoreBeforeLevel(vaultLevel) + currentVaultScore;
    }

    /// <summary>
    /// 开启金库重生后的短暂无敌窗口。
    /// </summary>
    private void StartInvincibilityWindow(bool playRespawnFeedback = true)
    {
        // 开启短暂无敌，并可选播放重生反馈。
        if (isRespawning && respawnRoutine == null)
        {
            return;
        }

        if (invincibleDuration <= 0f)
        {
            SetInvincibleState(false);
            return;
        }

        SetInvincibleState(true);
        invincibleTimer = invincibleDuration;
        if (playRespawnFeedback)
        {
            SpawnEffect(respawnVfxPrefab);
            TrySetAnimatorTrigger(respawnTriggerName);
        }
    }

    /// <summary>
    /// 每帧更新无敌倒计时，结束后关闭无敌状态。
    /// </summary>
    private void UpdateInvincibleTimer()
    {
        if (isRespawning)
        {
            return;
        }

        if (!isInvincible)
        {
            return;
        }

        invincibleTimer -= Time.deltaTime;
        if (invincibleTimer > 0f)
        {
            return;
        }

        SetInvincibleState(false);
    }

    /// <summary>
    /// 切换金库无敌状态，并同步护盾显示和材质染色。
    /// </summary>
    private void SetInvincibleState(bool value)
    {
        // 无敌只是“忽略伤害”，不是关闭碰撞体。
        isInvincible = value;
        if (!isInvincible)
        {
            invincibleTimer = 0f;
        }

        if (invincibleBubble != null)
        {
            invincibleBubble.SetActive(isInvincible);
        }

        if (isInvincible)
        {
            hitFlashTimer = 0f;
            ApplyTintColor(invincibleTintColor);
        }
        else
        {
            RestoreDefaultMaterialColors();
        }

        // 金库需要继续挡路/保留碰撞，因此这里不关闭碰撞体，只是在 TakeDamage 中忽略伤害。
        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }
    }

    /// <summary>
    /// 让世界空间血条始终朝向主摄像机。
    /// </summary>
    private void UpdateHpBillboard()
    {
        if (hpBillboard == null)
        {
            ResolveHpUiReferences();
        }

        if (hpBillboard == null || Camera.main == null)
        {
            return;
        }

        // Match the camera orientation so the world-space HP bar always stays readable.
        hpBillboard.rotation = Camera.main.transform.rotation;
    }

    /// <summary>
    /// 把当前生命值写入血条和血量文本。
    /// </summary>
    private void UpdateHpUI()
    {
        if (hpBar != null)
        {
            hpBar.fillAmount = maxHp > 0 ? (float)currentHp / maxHp : 0f;
        }

        if (hpText != null)
        {
            hpText.text = $"{currentHp}/{maxHp}";
        }
    }

    /// <summary>
    /// 在金库特效点生成指定特效。
    /// </summary>
    private void SpawnEffect(GameObject effectPrefab)
    {
        // 特效预制体为空就什么都不做，这样 Inspector 不拖特效也不会报错。
        if (effectPrefab == null)
        {
            return;
        }

        Transform spawnRoot = effectSpawnPoint != null ? effectSpawnPoint : transform;
        Instantiate(effectPrefab, spawnRoot.position, spawnRoot.rotation);
    }

    /// <summary>
    /// 如果 Animator 里存在指定 Trigger，就安全触发它。
    /// </summary>
    private void TrySetAnimatorTrigger(string triggerName)
    {
        // 触发动画前先检查 Animator 里是否真的有这个 Trigger，避免拼错名字时报错。
        if (animator == null || string.IsNullOrEmpty(triggerName))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger &&
                parameters[i].name == triggerName)
            {
                animator.SetTrigger(triggerName);
                return;
            }
        }
    }

    /// <summary>
    /// 广播金库数值变化，通知 HUD、怪物成长等系统刷新。
    /// </summary>
    private void NotifyVaultStatsChanged()
    {
        // ?.Invoke 表示：如果有人订阅事件，就通知；没人订阅也不会报错。
        OnVaultStatsChanged?.Invoke(this);
    }
}
