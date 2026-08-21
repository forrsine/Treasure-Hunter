using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spider King Boss 控制器：负责 Boss 的生命、受击、死亡、移动、攻击和行为树决策。
/// 注意：玩家攻击系统只关心 FighterInterface，所以 Boss 只要实现 Hit，就能被普通攻击和技能统一命中。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class SpiderKingBossController : MonoBehaviour, FighterInterface
{
    [Header("基础数值")]
    [SerializeField] private string bossName = "Spider King";
    [SerializeField] private int maxHp = 4000;
    [SerializeField] private int biteDamage = 30;
    [SerializeField] private int clawDamage = 36;
    [SerializeField] private int spellDamage = 26;

    [Header("AI 范围")]
    [SerializeField] private float detectRange = 22f;
    [SerializeField] private float meleeRange = 3.2f;
    [SerializeField] private float spellRange = 10f;
    [SerializeField] private float spellImpactRadius = 2.2f;
    // Boss 追到这个距离后停止前进，避免持续朝玩家中心点挤压。
    [SerializeField] private float chaseStopDistance = 2.65f;
    // 如果 Boss 已经和玩家过近，会优先后退拉开空间，避免站到玩家头顶。
    [SerializeField] private float overlapSeparationDistance = 1.55f;

    [Header("移动与节奏")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float maximumRoundScaledMoveSpeed = 4.2f;
    // 只在过近分离时使用，速度略高于普通移动，防止卡在玩家碰撞体里。
    [SerializeField] private float overlapSeparationSpeed = 3.8f;
    [SerializeField] private float rotateSpeed = 540f;
    [SerializeField] private float meleeCooldown = 2.1f;
    [SerializeField] private float spellCooldown = 4.5f;
    [SerializeField] private float meleeActionLockDuration = 0.95f;
    [SerializeField] private float spellActionLockDuration = 1.25f;
    [SerializeField] private float meleeHitDelay = 0.42f;
    [SerializeField] private float spellHitDelay = 0.72f;

    [Header("狂暴阶段")]
    [SerializeField] [Range(0.05f, 0.95f)] private float enragedHpPercent = 0.35f;
    [SerializeField] private float enragedMoveSpeedMultiplier = 1.18f;
    [SerializeField] private float enragedDamageMultiplier = 1.25f;
    [SerializeField] private float enragedCooldownMultiplier = 0.85f;

    [Header("动画状态名")]
    [SerializeField] private string idleStateName = "Idle 0";
    [SerializeField] private string moveStateName = "Crawl Forward Fast In Place";
    [SerializeField] private string biteAttackStateName = "Bite Attack";
    [SerializeField] private string clawLeftAttackStateName = "Claw Left Attack";
    [SerializeField] private string clawRightAttackStateName = "Claw Right Attack";
    [SerializeField] private string castSpellStateName = "Cast Spell";
    [SerializeField] private string projectileAttackStateName = "Projectile Attack";
    [SerializeField] private string takeDamageStateName = "Take Damage";
    [SerializeField] private string dieStateName = "Die";
    [SerializeField] private float animationFadeTime = 0.12f;

    [Header("死亡表现")]
    [Tooltip("Boss 死亡动画播放多久后进入胜利结算。需要根据 Spider King 的 Die 动画长度微调。")]
    [SerializeField] private float deathAnimationDuration = 2.2f;
    [Tooltip("死亡动画结束后是否隐藏 Boss 模型和碰撞体。隐藏而不是 Destroy，方便传送门继续读取 Boss 死亡位置。")]
    [SerializeField] private bool hideAfterDeathAnimation = true;

    [Header("引用兜底")]
    [SerializeField] private Transform target;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    [Header("受击表现")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.18f, 0.18f, 1f);
    [SerializeField] private float hitFlashDuration = 0.12f;
    // 受击动画播放期间使用动作锁，防止刚进入受击动画就被行为树切回追击/攻击。
    [SerializeField] private float takeDamageAnimationDuration = 0.85f;
    // 受击动画结束后的冷却时间；冷却内再次受击只闪色，不重复播放受击动画。
    [SerializeField] private float takeDamageAnimationCooldownAfterFinish = 3f;
    [SerializeField] private Color spellImpactColor = new Color(0.82f, 0.12f, 1f, 0.55f);

    [Header("远程子弹表现")]
    [Tooltip("Boss 释放远程法术时飞出去的小圆球颜色。")]
    [SerializeField] private Color spellProjectileColor = new Color(0.65f, 0.08f, 1f, 1f);
    [Tooltip("紫色小圆球的发光颜色。")]
    [SerializeField] private Color spellProjectileEmissionColor = new Color(0.95f, 0.2f, 1f, 1f);
    [Tooltip("紫色小圆球发光强度。")]
    [SerializeField] private float spellProjectileEmissionIntensity = 1.8f;
    [Tooltip("紫色小圆球半径。")]
    [SerializeField] private float spellProjectileRadius = 0.35f;
    [Tooltip("紫色小圆球飞行速度。")]
    [SerializeField] private float spellProjectileSpeed = 12f;
    [Tooltip("紫色小圆球从 Boss 身前多远的位置生成。")]
    [SerializeField] private float spellProjectileForwardOffset = 1.4f;
    [Tooltip("紫色小圆球从 Boss 脚底向上多高的位置生成。")]
    [SerializeField] private float spellProjectileSpawnHeight = 1.25f;
    [Tooltip("紫色小圆球最多飞行多久，避免目标异常时无限存在。")]
    [SerializeField] private float spellProjectileMaxTravelTime = 2f;

    private readonly List<Material> cachedMaterials = new List<Material>();
    private readonly List<Color> cachedDefaultColors = new List<Color>();
    private readonly HashSet<string> missingAnimationStates = new HashSet<string>();

    private BossBehaviorNode behaviorTree;
    private PlayerRuntimeController currentPlayer;
    private Coroutine delayedDamageCoroutine;
    private Coroutine deathSequenceCoroutine;
    private int currentHp;
    private float meleeCooldownTimer;
    private float spellCooldownTimer;
    private float actionLockTimer;
    private float takeDamageAnimationCooldownTimer;
    private float verticalVelocity;
    private float hitFlashTimer;
    private string currentAnimationState;
    private bool isDead;
    private bool isDeathSequenceFinished;
    private bool hasCachedBaseStats;
    private int baseMaxHp;
    private int baseBiteDamage;
    private int baseClawDamage;
    private int baseSpellDamage;
    private float baseMoveSpeed;
    private float baseOverlapSeparationSpeed;
    private float baseMeleeCooldown;
    private float baseSpellCooldown;

    public event Action<SpiderKingBossController> BossStatsChanged;
    public event Action<SpiderKingBossController> BossDied;

    public string BossName => bossName;
    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;
    public bool IsDead => isDead;
    public bool IsDeathSequenceFinished => isDeathSequenceFinished;
    public float HpPercent => maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
    public string CurrentPhaseName => isDead ? isDeathSequenceFinished ? "已击败" : "倒下中" : IsEnraged ? "狂暴阶段" : "普通阶段";

    private bool IsEnraged => !isDead && HpPercent <= enragedHpPercent;
    private float CurrentMoveSpeed => moveSpeed * (IsEnraged ? enragedMoveSpeedMultiplier : 1f);
    private float CurrentCooldownMultiplier => IsEnraged ? enragedCooldownMultiplier : 1f;

    private void Awake()
    {
        CacheComponents();
        CacheHitMaterials();
        CacheBaseStatsIfNeeded();
        currentHp = Mathf.Max(1, maxHp);
    }

    private void OnEnable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;
        RefreshTargetFromRuntime();
    }

    private void Start()
    {
        BuildBehaviorTree();
        RefreshTargetFromRuntime();
        BossStatsChanged?.Invoke(this);
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        RefreshTargetIfMissing();
        TickTimers();
        TickHitFlash();

        if (behaviorTree == null)
        {
            BuildBehaviorTree();
        }

        behaviorTree?.Tick();
    }

    private void OnDisable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
        RestoreDefaultMaterials();
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        biteDamage = Mathf.Max(1, biteDamage);
        clawDamage = Mathf.Max(1, clawDamage);
        spellDamage = Mathf.Max(1, spellDamage);
        detectRange = Mathf.Max(0.1f, detectRange);
        meleeRange = Mathf.Max(0.1f, meleeRange);
        spellRange = Mathf.Max(meleeRange, spellRange);
        spellImpactRadius = Mathf.Max(0.1f, spellImpactRadius);
        chaseStopDistance = Mathf.Clamp(chaseStopDistance, 0.5f, meleeRange);
        overlapSeparationDistance = Mathf.Clamp(overlapSeparationDistance, 0.1f, chaseStopDistance);
        moveSpeed = Mathf.Max(0.01f, moveSpeed);
        maximumRoundScaledMoveSpeed = Mathf.Max(moveSpeed, maximumRoundScaledMoveSpeed);
        overlapSeparationSpeed = Mathf.Max(0.01f, overlapSeparationSpeed);
        rotateSpeed = Mathf.Max(1f, rotateSpeed);
        meleeCooldown = Mathf.Max(0.05f, meleeCooldown);
        spellCooldown = Mathf.Max(0.05f, spellCooldown);
        meleeActionLockDuration = Mathf.Max(0.05f, meleeActionLockDuration);
        spellActionLockDuration = Mathf.Max(0.05f, spellActionLockDuration);
        meleeHitDelay = Mathf.Max(0f, meleeHitDelay);
        spellHitDelay = Mathf.Max(0f, spellHitDelay);
        enragedMoveSpeedMultiplier = Mathf.Max(0.01f, enragedMoveSpeedMultiplier);
        enragedDamageMultiplier = Mathf.Max(0.01f, enragedDamageMultiplier);
        enragedCooldownMultiplier = Mathf.Max(0.01f, enragedCooldownMultiplier);
        animationFadeTime = Mathf.Max(0f, animationFadeTime);
        deathAnimationDuration = Mathf.Max(0f, deathAnimationDuration);
        hitFlashDuration = Mathf.Max(0.01f, hitFlashDuration);
        takeDamageAnimationDuration = Mathf.Max(0.05f, takeDamageAnimationDuration);
        takeDamageAnimationCooldownAfterFinish = Mathf.Max(0f, takeDamageAnimationCooldownAfterFinish);
        spellProjectileEmissionIntensity = Mathf.Max(0f, spellProjectileEmissionIntensity);
        spellProjectileRadius = Mathf.Max(0.05f, spellProjectileRadius);
        spellProjectileSpeed = Mathf.Max(0.1f, spellProjectileSpeed);
        spellProjectileForwardOffset = Mathf.Max(0f, spellProjectileForwardOffset);
        spellProjectileMaxTravelTime = Mathf.Max(0.05f, spellProjectileMaxTravelTime);
    }

    /// <summary>
    /// 行为树结构：动作锁定 > 近战 > 远程法术 > 追击 > 待机。
    /// 选择节点从左到右判断，所以越靠前的分支优先级越高。
    /// </summary>
    private void BuildBehaviorTree()
    {
        behaviorTree = new BossSelectorNode(
            new BossSequenceNode(
                new BossConditionNode(IsActionLocked),
                new BossActionNode(DoActionLock)),
            new BossSequenceNode(
                new BossConditionNode(ShouldSeparateFromTarget),
                new BossActionNode(DoSeparateFromTarget)),
            new BossSequenceNode(
                new BossConditionNode(CanMeleeAttack),
                new BossActionNode(DoMeleeAttack)),
            new BossSequenceNode(
                new BossConditionNode(CanSpellAttack),
                new BossActionNode(DoSpellAttack)),
            new BossSequenceNode(
                new BossConditionNode(CanChasePlayer),
                new BossActionNode(DoChase)),
            new BossActionNode(DoIdle));
    }

    /// <summary>
    /// 玩家攻击入口：扣血、通知 UI、播放受击表现，血量归零后进入死亡流程。
    /// </summary>
    public void Hit(int attackPower)
    {
        if (isDead || currentHp <= 0 || attackPower <= 0)
        {
            return;
        }

        int appliedDamage = Mathf.Min(currentHp, attackPower);
        currentHp = Mathf.Max(0, currentHp - attackPower);
        StartHitFlash();
        BossStatsChanged?.Invoke(this);

        if (currentHp <= 0)
        {
            Die();
            return;
        }

        if (appliedDamage > 0)
        {
            TryPlayTakeDamageReaction();
        }
    }

    private void CacheComponents()
    {
        characterController = characterController != null
            ? characterController
            : GetComponent<CharacterController>();
        animator = animator != null
            ? animator
            : GetComponentInChildren<Animator>();
    }

    private void CacheBaseStatsIfNeeded()
    {
        if (hasCachedBaseStats)
        {
            return;
        }

        hasCachedBaseStats = true;
        baseMaxHp = maxHp;
        baseBiteDamage = biteDamage;
        baseClawDamage = clawDamage;
        baseSpellDamage = spellDamage;
        baseMoveSpeed = moveSpeed;
        baseOverlapSeparationSpeed = overlapSeparationSpeed;
        baseMeleeCooldown = meleeCooldown;
        baseSpellCooldown = spellCooldown;
    }

    /// <summary>
    /// 根据 Boss 周回轮次增强 Spider King 数值。
    /// 这里仍然复用同一个 Boss 和同一棵行为树，只改变数值，方便以后扩展成配置表。
    /// </summary>
    public void ApplyBossRoundScaling(
        int bossRound,
        float hpGrowthPerRound,
        float damageGrowthPerRound,
        float moveSpeedGrowthPerRound,
        float cooldownReductionPerRound,
        float minimumCooldownMultiplier)
    {
        CacheBaseStatsIfNeeded();

        int roundIndex = Mathf.Max(0, bossRound - 1);
        float hpMultiplier = 1f + Mathf.Max(0f, hpGrowthPerRound) * roundIndex;
        float damageMultiplier = 1f + Mathf.Max(0f, damageGrowthPerRound) * roundIndex;
        float speedMultiplier = 1f + Mathf.Max(0f, moveSpeedGrowthPerRound) * roundIndex;
        float cooldownMultiplier = Mathf.Max(
            Mathf.Clamp01(minimumCooldownMultiplier),
            1f - Mathf.Max(0f, cooldownReductionPerRound) * roundIndex);

        maxHp = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * hpMultiplier));
        biteDamage = Mathf.Max(1, Mathf.RoundToInt(baseBiteDamage * damageMultiplier));
        clawDamage = Mathf.Max(1, Mathf.RoundToInt(baseClawDamage * damageMultiplier));
        spellDamage = Mathf.Max(1, Mathf.RoundToInt(baseSpellDamage * damageMultiplier));
        moveSpeed = Mathf.Clamp(baseMoveSpeed * speedMultiplier, 0.01f, maximumRoundScaledMoveSpeed);
        overlapSeparationSpeed = Mathf.Max(0.01f, baseOverlapSeparationSpeed * speedMultiplier);
        meleeCooldown = Mathf.Max(0.05f, baseMeleeCooldown * cooldownMultiplier);
        spellCooldown = Mathf.Max(0.05f, baseSpellCooldown * cooldownMultiplier);
        currentHp = maxHp;

        BossStatsChanged?.Invoke(this);
    }

    /// <summary>
    /// 应用一套推荐的 Boss 碰撞体参数。
    /// 注意：这个方法只给编辑器生成工具或“运行时缺失组件兜底”调用，不放在 Awake 里，
    /// 避免你在 BossRoomScene 里手动调好的 CharacterController 被每次运行时覆盖。
    /// </summary>
    [ContextMenu("应用推荐 Boss 碰撞体参数")]
    public void ApplyRecommendedCharacterControllerDefaults()
    {
        CacheComponents();
        if (characterController == null)
        {
            return;
        }

        // Spider King 模型横向较宽，默认给一个偏大的胶囊体；后续可以直接在场景 Inspector 里微调。
        characterController.radius = 1.35f;
        characterController.height = 1.6f;
        characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);
        characterController.stepOffset = 0.08f;
        characterController.slopeLimit = 45f;
    }

    private void CacheHitMaterials()
    {
        cachedMaterials.Clear();
        cachedDefaultColors.Clear();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            Material[] materials = currentRenderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null || !material.HasProperty("_Color"))
                {
                    continue;
                }

                cachedMaterials.Add(material);
                cachedDefaultColors.Add(material.color);
            }
        }
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        currentPlayer = player;
        target = player != null ? player.transform : null;
    }

    private void RefreshTargetFromRuntime()
    {
        currentPlayer = GameplayRuntime.Instance.CurrentPlayer;
        target = currentPlayer != null ? currentPlayer.transform : target;
    }

    private void RefreshTargetIfMissing()
    {
        if (target != null && currentPlayer != null && currentPlayer.gameObject.activeInHierarchy)
        {
            return;
        }

        RefreshTargetFromRuntime();
    }

    private void TickTimers()
    {
        meleeCooldownTimer = Mathf.Max(0f, meleeCooldownTimer - Time.deltaTime);
        spellCooldownTimer = Mathf.Max(0f, spellCooldownTimer - Time.deltaTime);
        actionLockTimer = Mathf.Max(0f, actionLockTimer - Time.deltaTime);
        takeDamageAnimationCooldownTimer = Mathf.Max(0f, takeDamageAnimationCooldownTimer - Time.deltaTime);
    }

    private void TickHitFlash()
    {
        if (hitFlashTimer <= 0f)
        {
            return;
        }

        hitFlashTimer -= Time.deltaTime;
        if (hitFlashTimer <= 0f)
        {
            RestoreDefaultMaterials();
        }
    }

    private bool IsActionLocked()
    {
        return actionLockTimer > 0f;
    }

    private bool HasLivingTarget()
    {
        return currentPlayer != null &&
               currentPlayer.gameObject.activeInHierarchy &&
               currentPlayer.Stats.CurrentHp > 0 &&
               target != null;
    }

    private bool CanMeleeAttack()
    {
        return HasLivingTarget() &&
               meleeCooldownTimer <= 0f &&
               GetHorizontalDistanceToTarget() <= meleeRange;
    }

    private bool CanSpellAttack()
    {
        float distance = GetHorizontalDistanceToTarget();
        return HasLivingTarget() &&
               spellCooldownTimer <= 0f &&
               distance > meleeRange * 0.9f &&
               distance <= spellRange;
    }

    private bool CanChasePlayer()
    {
        float distance = GetHorizontalDistanceToTarget();
        return HasLivingTarget() &&
               distance > chaseStopDistance &&
               distance <= detectRange;
    }

    private bool ShouldSeparateFromTarget()
    {
        return HasLivingTarget() &&
               GetHorizontalDistanceToTarget() < overlapSeparationDistance;
    }

    private BossBehaviorState DoActionLock()
    {
        FaceTarget();
        ApplyGravityOnly();
        return BossBehaviorState.Running;
    }

    private BossBehaviorState DoSeparateFromTarget()
    {
        // 过近分离放在攻击/追击之前，优先解决 Boss 和玩家碰撞体重叠的问题。
        FaceTarget();
        PlayAnimation(moveStateName, false);

        Vector3 awayDirection = -GetFlatDirectionToTarget();
        if (awayDirection.sqrMagnitude > 0.0001f)
        {
            Move(awayDirection.normalized, overlapSeparationSpeed);
        }
        else
        {
            Move(-transform.forward, overlapSeparationSpeed);
        }

        return BossBehaviorState.Running;
    }

    private BossBehaviorState DoMeleeAttack()
    {
        bool useBite = UnityEngine.Random.value < 0.5f;
        string animationName = useBite
            ? biteAttackStateName
            : UnityEngine.Random.value < 0.5f ? clawLeftAttackStateName : clawRightAttackStateName;
        int damage = CalculatePhaseDamage(useBite ? biteDamage : clawDamage);

        BeginAction(animationName, meleeActionLockDuration);
        meleeCooldownTimer = meleeCooldown * CurrentCooldownMultiplier;
        ScheduleDamageAfterDelay(meleeHitDelay, damage, meleeRange + 0.35f, false, target.position);
        return BossBehaviorState.Running;
    }

    private BossBehaviorState DoSpellAttack()
    {
        string animationName = UnityEngine.Random.value < 0.5f
            ? castSpellStateName
            : projectileAttackStateName;
        Vector3 impactPosition = target.position;
        impactPosition.y = transform.position.y;

        BeginAction(animationName, spellActionLockDuration);
        spellCooldownTimer = spellCooldown * CurrentCooldownMultiplier;
        ScheduleDamageAfterDelay(spellHitDelay, CalculatePhaseDamage(spellDamage), spellImpactRadius, true, impactPosition);
        return BossBehaviorState.Running;
    }

    private BossBehaviorState DoChase()
    {
        FaceTarget();

        Vector3 direction = GetFlatDirectionToTarget();
        if (direction.magnitude <= chaseStopDistance)
        {
            PlayAnimation(idleStateName, false);
            ApplyGravityOnly();
        }
        else if (direction.sqrMagnitude > 0.0001f)
        {
            PlayAnimation(moveStateName, false);
            Move(direction.normalized, CurrentMoveSpeed);
        }
        else
        {
            ApplyGravityOnly();
        }

        return BossBehaviorState.Running;
    }

    private BossBehaviorState DoIdle()
    {
        PlayAnimation(idleStateName, false);
        ApplyGravityOnly();
        return BossBehaviorState.Running;
    }

    private void BeginAction(string animationName, float lockDuration)
    {
        FaceTarget();
        actionLockTimer = Mathf.Max(actionLockTimer, lockDuration);
        PlayAnimation(animationName, true);
    }

    private void TryPlayTakeDamageReaction()
    {
        if (takeDamageAnimationCooldownTimer > 0f)
        {
            return;
        }

        // 受击硬直会打断 Boss 当前前摇伤害，避免画面已受击但伤害仍然延迟结算。
        CancelPendingDamage();
        actionLockTimer = Mathf.Max(actionLockTimer, takeDamageAnimationDuration);
        takeDamageAnimationCooldownTimer = takeDamageAnimationDuration + takeDamageAnimationCooldownAfterFinish;
        PlayAnimation(takeDamageStateName, true);
    }

    private int CalculatePhaseDamage(int baseDamage)
    {
        float multiplier = IsEnraged ? enragedDamageMultiplier : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseDamage * multiplier));
    }

    private void ScheduleDamageAfterDelay(
        float delay,
        int damage,
        float range,
        bool isAreaSpell,
        Vector3 impactPosition)
    {
        CancelPendingDamage();

        delayedDamageCoroutine = StartCoroutine(DealDamageAfterDelay(delay, damage, range, isAreaSpell, impactPosition));
    }

    private void CancelPendingDamage()
    {
        if (delayedDamageCoroutine == null)
        {
            return;
        }

        StopCoroutine(delayedDamageCoroutine);
        delayedDamageCoroutine = null;
    }

    private IEnumerator DealDamageAfterDelay(
        float delay,
        int damage,
        float range,
        bool isAreaSpell,
        Vector3 impactPosition)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delay));
        delayedDamageCoroutine = null;

        if (isDead || !HasLivingTarget())
        {
            yield break;
        }

        if (isAreaSpell)
        {
            yield return LaunchSpellProjectileToImpact(impactPosition, range, damage);
            yield break;
        }

        TryDamagePlayerInFront(range, damage);
    }

    /// <summary>
    /// Boss 远程攻击表现：先生成紫色小圆球飞向目标点，到达后再爆炸结算范围伤害。
    /// 伤害仍然在 Boss 控制器里统一结算，小圆球只负责视觉表现，避免和普通怪物 BulletCo 互相影响。
    /// </summary>
    private IEnumerator LaunchSpellProjectileToImpact(Vector3 impactPosition, float range, int damage)
    {
        Vector3 startPosition = GetSpellProjectileStartPosition();
        Vector3 targetPosition = impactPosition;
        targetPosition.y = Mathf.Max(impactPosition.y + 0.25f, 0.25f);

        GameObject projectile = CreateSpellProjectileVisual(startPosition, targetPosition);
        float elapsed = 0f;

        while (projectile != null &&
               elapsed < spellProjectileMaxTravelTime &&
               Vector3.Distance(projectile.transform.position, targetPosition) > 0.08f)
        {
            if (isDead)
            {
                Destroy(projectile);
                yield break;
            }

            elapsed += Time.deltaTime;
            projectile.transform.position = Vector3.MoveTowards(
                projectile.transform.position,
                targetPosition,
                spellProjectileSpeed * Time.deltaTime);
            yield return null;
        }

        if (projectile != null)
        {
            projectile.transform.position = targetPosition;
            Destroy(projectile);
        }

        SpawnSpellImpact(impactPosition, range);
        TryDamagePlayerAtPoint(impactPosition, range, damage);
    }

    private void TryDamagePlayerInFront(float range, int damage)
    {
        if (!HasLivingTarget())
        {
            return;
        }

        float distance = GetHorizontalDistanceToTarget();
        if (distance > range)
        {
            return;
        }

        Vector3 directionToTarget = GetFlatDirectionToTarget().normalized;
        if (Vector3.Dot(transform.forward, directionToTarget) < 0.25f)
        {
            return;
        }

        HitCurrentPlayer(damage);
    }

    private void TryDamagePlayerAtPoint(Vector3 point, float radius, int damage)
    {
        if (!HasLivingTarget())
        {
            return;
        }

        Vector3 playerPosition = target.position;
        playerPosition.y = point.y;
        if (Vector3.Distance(playerPosition, point) <= radius)
        {
            HitCurrentPlayer(damage);
        }
    }

    private void HitCurrentPlayer(int damage)
    {
        if (currentPlayer == null || damage <= 0)
        {
            return;
        }

        PlayerHealthComponent playerHealth = currentPlayer.GetComponent<PlayerHealthComponent>();
        FighterInterface fighter = playerHealth != null
            ? playerHealth
            : currentPlayer.GetComponent<FighterInterface>();
        fighter?.Hit(damage);
    }

    private Vector3 GetSpellProjectileStartPosition()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        return transform.position +
               forward.normalized * spellProjectileForwardOffset +
               Vector3.up * spellProjectileSpawnHeight;
    }

    private GameObject CreateSpellProjectileVisual(Vector3 startPosition, Vector3 targetPosition)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "SpiderKingSpellProjectile";
        projectile.transform.position = startPosition;
        projectile.transform.localScale = Vector3.one * (spellProjectileRadius * 2f);

        Vector3 flyDirection = targetPosition - startPosition;
        if (flyDirection.sqrMagnitude > 0.0001f)
        {
            projectile.transform.rotation = Quaternion.LookRotation(flyDirection.normalized, Vector3.up);
        }

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
        {
            Destroy(projectileCollider);
        }

        Renderer renderer = projectile.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = spellProjectileColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", spellProjectileEmissionColor * spellProjectileEmissionIntensity);
            renderer.material = material;
        }

        Light light = projectile.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = spellProjectileEmissionColor;
        light.range = 4f;
        light.intensity = 2.2f;

        return projectile;
    }

    private float GetHorizontalDistanceToTarget()
    {
        if (target == null)
        {
            return float.MaxValue;
        }

        return GetFlatDirectionToTarget().magnitude;
    }

    private Vector3 GetFlatDirectionToTarget()
    {
        if (target == null)
        {
            return Vector3.zero;
        }

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        return direction;
    }

    private void FaceTarget()
    {
        Vector3 direction = GetFlatDirectionToTarget();
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime);
    }

    private void Move(Vector3 direction, float speed)
    {
        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            verticalVelocity += Physics.gravity.y * Time.deltaTime;
            Vector3 velocity = direction * speed;
            velocity.y = verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
            return;
        }

        transform.position += direction * speed * Time.deltaTime;
    }

    private void ApplyGravityOnly()
    {
        if (characterController == null || !characterController.enabled)
        {
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -1f;
        }

        verticalVelocity += Physics.gravity.y * Time.deltaTime;
        characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);
    }

    private void PlayAnimation(string stateName, bool force)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
        {
            return;
        }

        if (!force && currentAnimationState == stateName)
        {
            return;
        }

        string playableStateName;
        if (!TryGetPlayableStateName(stateName, out playableStateName))
        {
            return;
        }

        animator.CrossFadeInFixedTime(playableStateName, animationFadeTime);
        currentAnimationState = stateName;
    }

    private bool TryGetPlayableStateName(string stateName, out string playableStateName)
    {
        playableStateName = stateName;
        if (animator.HasState(0, Animator.StringToHash(stateName)))
        {
            return true;
        }

        string fullPath = $"Base Layer.{stateName}";
        if (animator.HasState(0, Animator.StringToHash(fullPath)))
        {
            playableStateName = fullPath;
            return true;
        }

        if (missingAnimationStates.Add(stateName))
        {
            Debug.LogWarning($"Spider King Animator 没有找到状态：{stateName}，请检查 Animator Controller。", this);
        }

        return false;
    }

    private void StartHitFlash()
    {
        if (cachedMaterials.Count == 0)
        {
            CacheHitMaterials();
        }

        hitFlashTimer = hitFlashDuration;
        for (int i = 0; i < cachedMaterials.Count; i++)
        {
            Material material = cachedMaterials[i];
            if (material != null && material.HasProperty("_Color"))
            {
                material.color = hitFlashColor;
            }
        }
    }

    private void RestoreDefaultMaterials()
    {
        for (int i = 0; i < cachedMaterials.Count; i++)
        {
            Material material = cachedMaterials[i];
            if (material != null && material.HasProperty("_Color") && i < cachedDefaultColors.Count)
            {
                material.color = cachedDefaultColors[i];
            }
        }
    }

    private void SpawnSpellImpact(Vector3 center, float radius)
    {
        GameObject impact = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        impact.name = "SpiderKingSpellImpact";
        impact.transform.position = center + Vector3.up * 0.08f;
        impact.transform.localScale = new Vector3(radius * 2f, 0.08f, radius * 2f);

        Collider impactCollider = impact.GetComponent<Collider>();
        if (impactCollider != null)
        {
            Destroy(impactCollider);
        }

        Renderer renderer = impact.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = spellImpactColor;
            renderer.material = material;
        }

        Destroy(impact, 0.45f);
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHp = 0;
        actionLockTimer = 0f;
        RestoreDefaultMaterials();

        if (delayedDamageCoroutine != null)
        {
            StopCoroutine(delayedDamageCoroutine);
            delayedDamageCoroutine = null;
        }

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        PlayAnimation(dieStateName, true);
        BossStatsChanged?.Invoke(this);

        if (deathSequenceCoroutine != null)
        {
            StopCoroutine(deathSequenceCoroutine);
        }

        deathSequenceCoroutine = StartCoroutine(FinishDeathSequenceAfterAnimation());
    }

    /// <summary>
    /// 死亡动画结束后再真正进入胜利结算。
    /// 注意这里使用真实时间等待，避免后续 UI 暂停 Time.timeScale 后影响死亡收尾。
    /// </summary>
    private IEnumerator FinishDeathSequenceAfterAnimation()
    {
        yield return new WaitForSecondsRealtime(GetDeathAnimationWaitDuration());

        deathSequenceCoroutine = null;
        isDeathSequenceFinished = true;
        HideBossAfterDeathIfNeeded();

        BossStatsChanged?.Invoke(this);
        BossDied?.Invoke(this);
    }

    private float GetDeathAnimationWaitDuration()
    {
        float configuredDuration = Mathf.Max(0f, deathAnimationDuration);
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return configuredDuration;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name == dieStateName)
            {
                return Mathf.Max(0f, clip.length);
            }
        }

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.IndexOf(dieStateName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Max(0f, clip.length);
            }
        }

        return configuredDuration;
    }

    private void HideBossAfterDeathIfNeeded()
    {
        if (!hideAfterDeathAnimation)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
