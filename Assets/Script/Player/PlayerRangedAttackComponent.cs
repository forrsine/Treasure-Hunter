using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家远程普通攻击组件：负责选择职业投射物 Prefab、维护对象池并从动画释放点发射。
/// 它只管理“怎么生成和复用投射物”，攻击输入和攻击冷却仍由 PlayerCombatComponent 负责。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerRangedAttackComponent : MonoBehaviour
{
    private const string WizardClassKey = "Wizard";
    private const string ArcherClassKey = "Archer";
    private const string WizardExplosionPoolKey = "WizardBasicAttackExplosionVfx";
    private const int SpawnOverlapCapacity = 16;

    [SerializeField, Min(1)] private int prewarmCount = 8;
    [SerializeField] private float spawnHeight = 1.15f;
    [SerializeField] private float spawnForwardOffset = 0.7f;
    [SerializeField] private GameObject wizardProjectileVisualPrefab;
    [SerializeField] private GameObject wizardExplosionVisualPrefab;
    [SerializeField] private GameObject archerProjectileVisualPrefab;
    [SerializeField, Min(0.05f)] private float wizardExplosionVfxLifetime = 0.8f;

    private readonly Queue<PlayerBasicAttackProjectile> availableProjectiles =
        new Queue<PlayerBasicAttackProjectile>();
    private readonly HashSet<PlayerBasicAttackProjectile> allProjectiles =
        new HashSet<PlayerBasicAttackProjectile>();
    private readonly HashSet<PlayerBasicAttackProjectile> activeProjectiles =
        new HashSet<PlayerBasicAttackProjectile>();
    private readonly HashSet<GameObject> activeExplosionVisuals =
        new HashSet<GameObject>();
    private readonly Collider[] spawnOverlapBuffer = new Collider[SpawnOverlapCapacity];

    private PlayerCombatComponent combat;
    private CharacterDefine characterDefine;
    private Transform projectileSpawnPoint;
    private GameObject selectedProjectileVisualPrefab;
    private bool poolPrewarmed;
    private bool missingPrefabLogged;

    public int AvailableProjectileCount => availableProjectiles.Count;
    public int ActiveProjectileCount => activeProjectiles.Count;
    public bool IsProjectileAttack =>
        characterDefine != null &&
        characterDefine.basicAttackType == CharacterBasicAttackType.Projectile;

    /// <summary>
    /// PlayerRuntime 在职业模型和配置绑定后会再次调用这里。
    /// 只有远程职业才选择对应资源并预热对象池，战士和刺客不会创建无用投射物。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        combat = player != null
            ? player.GetComponent<PlayerCombatComponent>()
            : GetComponent<PlayerCombatComponent>();
        characterDefine = player != null ? player.EntryDefine : null;
        selectedProjectileVisualPrefab = ResolveProjectileVisualPrefab(characterDefine);
        projectileSpawnPoint = FindModelShootPoint();

        if (IsProjectileAttack)
        {
            EnsurePoolPrewarmed();
        }
    }

    /// <summary>
    /// 从对象池发射一次职业普通攻击，返回实例便于调试和自动化测试。
    /// </summary>
    public PlayerBasicAttackProjectile Fire()
    {
        if (!IsProjectileAttack || combat == null)
        {
            return null;
        }

        EnsurePoolPrewarmed();
        PlayerBasicAttackProjectile projectile = GetProjectile();
        if (projectile == null)
        {
            return null;
        }

        float projectileSpeed = Mathf.Max(0.01f, characterDefine.projectileSpeed);
        float projectileLifetime = Mathf.Max(0.05f, characterDefine.projectileLifetime);
        float projectileRadius = Mathf.Max(0.02f, characterDefine.projectileRadius);
        float visualScale = characterDefine.projectileVisualScale > 0f
            ? characterDefine.projectileVisualScale
            : 1f;
        Color projectileColor = ResolveProjectileColor(characterDefine.projectileColorHex);

        // 飞行方向始终使用 PlayerRuntime.forward，保证模型、移动和正式伤害方向一致。
        Vector3 direction = transform.forward;
        Vector3 startPosition = ResolveSafeSpawnPosition(direction, projectileRadius);

        activeProjectiles.Add(projectile);
        projectile.transform.SetParent(null, true);
        System.Action<Vector3, float, Color> explosionCallback = null;
        if (characterDefine.projectileExplosionRadius > 0f)
        {
            explosionCallback = PlayWizardExplosionVfx;
        }
        projectile.Launch(
            combat,
            transform,
            startPosition,
            direction,
            projectileSpeed,
            projectileLifetime,
            projectileRadius,
            projectileColor,
            characterDefine.projectileApplyTint,
            visualScale,
            characterDefine.projectileTrajectory,
            characterDefine.projectileArcHeight,
            characterDefine.projectileExplosionRadius,
            combat.AttackCollisionLayer,
            explosionCallback,
            ReleaseProjectile);
        return projectile;
    }

    /// <summary>
    /// 优先从模型武器口出生；如果武器动作把发射点带进墙体，就改用玩家前上方的安全点。
    /// 这项检查只在攻击瞬间执行，不放进 Update，避免持续物理查询。
    /// </summary>
    private Vector3 ResolveSafeSpawnPosition(Vector3 direction, float projectileRadius)
    {
        Vector3 fallbackPosition =
            transform.position +
            Vector3.up * spawnHeight +
            direction * spawnForwardOffset;
        Vector3 preferredPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : fallbackPosition;

        Physics.SyncTransforms();
        if (!IsSpawnPositionBlocked(preferredPosition, projectileRadius))
        {
            return preferredPosition;
        }

        // 通用点仍被墙体占用时继续沿正前方小步推出，最多尝试三次。
        float pushDistance = Mathf.Max(0.1f, projectileRadius * 2f);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Vector3 candidate = fallbackPosition + direction * (pushDistance * attempt);
            if (!IsSpawnPositionBlocked(candidate, projectileRadius))
            {
                return candidate;
            }
        }

        return fallbackPosition;
    }

    private bool IsSpawnPositionBlocked(Vector3 position, float projectileRadius)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            position,
            Mathf.Max(0.02f, projectileRadius),
            spawnOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);
        bool isBlocked = false;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = spawnOverlapBuffer[i];
            spawnOverlapBuffer[i] = null;
            if (overlap == null ||
                overlap.transform == transform ||
                overlap.transform.IsChildOf(transform))
            {
                continue;
            }

            // 怪物或可破坏箱子是攻击目标，不是需要把出生点向前推出的墙体。
            // 弓箭手会记录并忽略出生时已经覆盖弩口的目标，保证箭矢至少真正飞离发射点。
            if (PlayerBasicAttackDamageResolver.TryGetTarget(
                    overlap,
                    out FighterInterface ignoredTarget))
            {
                continue;
            }

            isBlocked = true;
        }

        return isBlocked;
    }

    private void EnsurePoolPrewarmed()
    {
        if (poolPrewarmed)
        {
            return;
        }

        if (selectedProjectileVisualPrefab == null)
        {
            if (!missingPrefabLogged)
            {
                string classKey = characterDefine != null ? characterDefine.classKey : "Unknown";
                Debug.LogError(
                    $"远程普通攻击缺少 {classKey} 投射物 Prefab。为避免退回临时球体，本次攻击不会生成投射物。",
                    this);
                missingPrefabLogged = true;
            }
            return;
        }

        poolPrewarmed = true;
        int count = Mathf.Max(1, prewarmCount);
        for (int i = 0; i < count; i++)
        {
            PlayerBasicAttackProjectile projectile = CreateProjectile();
            if (projectile != null)
            {
                availableProjectiles.Enqueue(projectile);
            }
        }
    }

    private PlayerBasicAttackProjectile GetProjectile()
    {
        while (availableProjectiles.Count > 0)
        {
            PlayerBasicAttackProjectile projectile = availableProjectiles.Dequeue();
            if (projectile != null)
            {
                return projectile;
            }
        }

        // 正常攻击频率下预热数量足够；极端情况下扩容一次，回收后继续复用。
        return CreateProjectile();
    }

    private PlayerBasicAttackProjectile CreateProjectile()
    {
        if (selectedProjectileVisualPrefab == null)
        {
            return null;
        }

        GameObject projectileObject = Instantiate(selectedProjectileVisualPrefab, transform);
        projectileObject.name = $"{selectedProjectileVisualPrefab.name}_BasicAttack";
        projectileObject.SetActive(false);

        PlayerBasicAttackProjectile projectile =
            projectileObject.GetComponent<PlayerBasicAttackProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<PlayerBasicAttackProjectile>();
        }
        projectile.InitializePoolVisual();
        allProjectiles.Add(projectile);
        return projectile;
    }

    private void ReleaseProjectile(PlayerBasicAttackProjectile projectile)
    {
        if (projectile == null || !allProjectiles.Contains(projectile))
        {
            return;
        }

        if (!activeProjectiles.Remove(projectile))
        {
            return;
        }

        projectile.transform.SetParent(transform, false);
        availableProjectiles.Enqueue(projectile);
    }

    private GameObject ResolveProjectileVisualPrefab(CharacterDefine define)
    {
        if (define == null)
        {
            return null;
        }

        if (define.classId == 2 || define.classKey == WizardClassKey)
        {
            return wizardProjectileVisualPrefab;
        }

        if (define.classId == 3 || define.classKey == ArcherClassKey)
        {
            return archerProjectileVisualPrefab;
        }

        return null;
    }

    /// <summary>
    /// 优先复用 Human Pack 模型已经摆好的 shootPoint；找不到时才使用 PlayerRuntime 的通用出生点。
    /// </summary>
    private Transform FindModelShootPoint()
    {
        triggerProjectile[] legacyShooters =
            GetComponentsInChildren<triggerProjectile>(true);
        for (int i = 0; i < legacyShooters.Length; i++)
        {
            if (legacyShooters[i] != null && legacyShooters[i].shootPoint != null)
            {
                return legacyShooters[i].shootPoint;
            }
        }

        return null;
    }

    private void PlayWizardExplosionVfx(Vector3 position, float radius, Color color)
    {
        if (wizardExplosionVisualPrefab == null)
        {
            Debug.LogWarning("法师普通攻击缺少爆炸 Prefab，范围伤害仍会正常结算。", this);
            return;
        }

        SkillVisualPool visualPool = SkillVisualPool.Instance;
        GameObject explosion = visualPool != null
            ? visualPool.GetPrefabVfx(WizardExplosionPoolKey, wizardExplosionVisualPrefab)
            : Instantiate(wizardExplosionVisualPrefab);
        if (explosion == null)
        {
            return;
        }

        // 第三方爆炸 Prefab 自带 DestroyByTime；对象池实例必须禁用它，统一由回收器管理生命周期。
        DestroyByTime[] legacyDestroyTimers =
            explosion.GetComponentsInChildren<DestroyByTime>(true);
        for (int i = 0; i < legacyDestroyTimers.Length; i++)
        {
            legacyDestroyTimers[i].enabled = false;
        }

        explosion.transform.SetPositionAndRotation(position, Quaternion.identity);
        explosion.transform.localScale = Vector3.one * Mathf.Max(0.1f, radius);
        PlayerProjectileVisualUtility.ApplyTint(explosion, color);
        PlayerProjectileVisualUtility.RestartEffects(explosion);

        if (!Application.isPlaying)
        {
            PlayerProjectileVisualUtility.StopEffects(explosion);
            if (visualPool != null)
            {
                visualPool.ReleasePrefabVfx(WizardExplosionPoolKey, explosion);
            }
            else
            {
                DestroyImmediate(explosion);
            }
            return;
        }

        activeExplosionVisuals.Add(explosion);
        StartCoroutine(ReleaseExplosionAfterDelay(explosion, visualPool));
    }

    private IEnumerator ReleaseExplosionAfterDelay(
        GameObject explosion,
        SkillVisualPool visualPool)
    {
        yield return new WaitForSeconds(wizardExplosionVfxLifetime);
        if (explosion == null || !activeExplosionVisuals.Remove(explosion))
        {
            yield break;
        }

        PlayerProjectileVisualUtility.StopEffects(explosion);
        if (visualPool != null)
        {
            visualPool.ReleasePrefabVfx(WizardExplosionPoolKey, explosion);
        }
        else
        {
            Destroy(explosion);
        }
    }

    private static Color ResolveProjectileColor(string htmlColor)
    {
        return !string.IsNullOrWhiteSpace(htmlColor) &&
               ColorUtility.TryParseHtmlString(htmlColor, out Color parsedColor)
            ? parsedColor
            : Color.white;
    }

    private void OnDestroy()
    {
        // 活动投射物发射后已经脱离玩家层级，场景切换时必须显式清理。
        foreach (PlayerBasicAttackProjectile projectile in allProjectiles)
        {
            if (projectile != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(projectile.gameObject);
                }
                else
                {
                    DestroyImmediate(projectile.gameObject);
                }
            }
        }

        availableProjectiles.Clear();
        activeProjectiles.Clear();
        allProjectiles.Clear();

        // 爆炸表现脱离玩家层级，玩家销毁时也要主动回收，避免场景中留下孤立特效。
        foreach (GameObject explosion in activeExplosionVisuals)
        {
            if (explosion == null)
            {
                continue;
            }

            PlayerProjectileVisualUtility.StopEffects(explosion);
            SkillVisualPool visualPool = SkillVisualPool.Instance;
            if (visualPool != null)
            {
                visualPool.ReleasePrefabVfx(WizardExplosionPoolKey, explosion);
            }
            else if (Application.isPlaying)
            {
                Destroy(explosion);
            }
            else
            {
                DestroyImmediate(explosion);
            }
        }
        activeExplosionVisuals.Clear();
        combat = null;
        characterDefine = null;
        projectileSpawnPoint = null;
        selectedProjectileVisualPrefab = null;
    }
}
