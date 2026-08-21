using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家远程普通攻击投射物。
/// 只负责职业视觉、飞行轨迹、碰撞和回收；伤害公式交给 PlayerBasicAttackDamageResolver，
/// 因此弓箭手单体箭矢和法师范围火球仍共用暴击、飘字与吸血流程。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerBasicAttackProjectile : MonoBehaviour
{
    private const int ExplosionOverlapCapacity = 64;
    private const int SweepHitCapacity = 16;
    // 怪物聚集时每个单位可能同时带身体和多个攻击 Trigger，扩大复用缓冲避免查询结果被过早截断。
    private const int InitialOverlapCapacity = 64;

    private readonly Collider[] explosionOverlapBuffer = new Collider[ExplosionOverlapCapacity];
    private readonly RaycastHit[] sweepHitBuffer = new RaycastHit[SweepHitCapacity];
    private readonly Collider[] initialOverlapBuffer = new Collider[InitialOverlapCapacity];
    private readonly HashSet<FighterInterface> explosionHitTargets =
        new HashSet<FighterInterface>();
    private readonly HashSet<Collider> ignoredSpawnOverlapTargets =
        new HashSet<Collider>();

    private Rigidbody projectileRigidbody;
    private SphereCollider projectileCollider;
    private PlayerCombatComponent ownerCombat;
    private Transform ownerTransform;
    private Action<Vector3, float, Color> explosionVisualCallback;
    private Action<PlayerBasicAttackProjectile> releaseCallback;
    private CharacterProjectileTrajectory trajectory;
    private Vector3 launchPosition;
    private Vector3 launchDirection;
    private Vector3 baseVisualScale = Vector3.one;
    private Color projectileColor = Color.white;
    private float speed;
    private float totalLifetime;
    private float elapsedFlightTime;
    private float arcHeight;
    private float explosionRadius;
    private float collisionRadius;
    private float visualScaleMultiplier = 1f;
    private bool baseScaleCached;
    private bool released = true;

    public bool IsReleased => released;
    public CharacterProjectileTrajectory Trajectory => trajectory;
    public float ExplosionRadius => explosionRadius;
    public float VisualScaleMultiplier => visualScaleMultiplier;

    private void Awake()
    {
        EnsureComponents();
        CacheBaseVisualScale();
    }

    /// <summary>
    /// Prefab 刚加入对象池时记录其原始缩放。后续的 0.7 是基于资源原尺寸，而不是覆盖为 0.7 个世界单位。
    /// </summary>
    public void InitializePoolVisual()
    {
        EnsureComponents();
        CacheBaseVisualScale();
    }

    /// <summary>
    /// 每次从对象池取出时重置完整运行状态，避免残留上一发的拥有者、轨迹、粒子或回调。
    /// </summary>
    public void Launch(
        PlayerCombatComponent newOwnerCombat,
        Transform newOwnerTransform,
        Vector3 position,
        Vector3 direction,
        float newSpeed,
        float lifetime,
        float radius,
        Color color,
        bool applyTint,
        float newVisualScaleMultiplier,
        CharacterProjectileTrajectory newTrajectory,
        float newArcHeight,
        float newExplosionRadius,
        int collisionLayer,
        Action<Vector3, float, Color> onExplosionVisual,
        Action<PlayerBasicAttackProjectile> onRelease)
    {
        EnsureComponents();
        CacheBaseVisualScale();

        ownerCombat = newOwnerCombat;
        ownerTransform = newOwnerTransform;
        explosionVisualCallback = onExplosionVisual;
        releaseCallback = onRelease;
        speed = Mathf.Max(0.01f, newSpeed);
        totalLifetime = Mathf.Max(0.05f, lifetime);
        elapsedFlightTime = 0f;
        trajectory = newTrajectory;
        arcHeight = Mathf.Max(0f, newArcHeight);
        explosionRadius = Mathf.Max(0f, newExplosionRadius);
        ignoredSpawnOverlapTargets.Clear();
        projectileColor = color;
        visualScaleMultiplier = Mathf.Max(0.01f, newVisualScaleMultiplier);
        launchPosition = position;
        launchDirection = direction.sqrMagnitude > 0.001f
            ? direction.normalized
            : Vector3.forward;
        released = false;

        gameObject.layer = Mathf.Clamp(collisionLayer, 0, 31);
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(launchDirection));
        transform.localScale = baseVisualScale * visualScaleMultiplier;
        ConfigureCollisionRadius(radius);
        bool usesSweepOnlyCollision = IsStraightSingleTargetProjectile();
        projectileCollider.enabled = !usesSweepOnlyCollision;

        gameObject.SetActive(true);
        projectileRigidbody.position = position;
        projectileRigidbody.rotation = transform.rotation;

        if (applyTint)
        {
            PlayerProjectileVisualUtility.ApplyTint(gameObject, color);
        }
        PlayerProjectileVisualUtility.RestartEffects(gameObject);
        if (usesSweepOnlyCollision)
        {
            CaptureSpawnOverlapTargets(position);
        }
        else
        {
            TryResolveInitialOverlap(position);
        }
    }

    private void Update()
    {
        if (released)
        {
            return;
        }

        if (elapsedFlightTime >= totalLifetime)
        {
            Vector3 endpoint = EvaluateTrajectoryPosition(1f);
            projectileRigidbody.position = endpoint;
            transform.position = endpoint;

            if (explosionRadius > 0f)
            {
                Explode(null);
            }
            else
            {
                Release();
            }
        }
    }

    private void FixedUpdate()
    {
        if (released)
        {
            return;
        }

        elapsedFlightTime = Mathf.Min(
            totalLifetime,
            elapsedFlightTime + Time.fixedDeltaTime);
        float progress = totalLifetime > 0f
            ? elapsedFlightTime / totalLifetime
            : 1f;
        Vector3 nextPosition = EvaluateTrajectoryPosition(progress);
        Vector3 movementDirection = nextPosition - projectileRigidbody.position;
        if (TrySweepToNextPosition(nextPosition))
        {
            return;
        }

        if (movementDirection.sqrMagnitude > 0.0001f)
        {
            projectileRigidbody.MoveRotation(Quaternion.LookRotation(movementDirection.normalized));
        }
        projectileRigidbody.MovePosition(nextPosition);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryHandleCollision(other);
    }

    /// <summary>
    /// 对本物理帧的完整移动线段做球形扫掠。
    /// 高速箭矢每帧可能跨过较薄的怪物碰撞体，不能只依赖终点位置产生的 Trigger 回调。
    /// </summary>
    private bool TrySweepToNextPosition(Vector3 nextPosition)
    {
        Vector3 currentPosition = projectileRigidbody.position;
        Vector3 displacement = nextPosition - currentPosition;
        float distance = displacement.magnitude;
        if (distance <= 0.0001f)
        {
            return false;
        }

        Vector3 direction = displacement / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            currentPosition,
            Mathf.Max(0.02f, collisionRadius),
            direction,
            sweepHitBuffer,
            distance,
            ~0,
            QueryTriggerInteraction.Collide);

        Collider closestCollider = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = sweepHitBuffer[i];
            sweepHitBuffer[i] = default;
            Collider candidate = hit.collider;
            if (!IsCollisionCandidate(candidate) || hit.distance >= closestDistance)
            {
                continue;
            }

            closestCollider = candidate;
            closestDistance = hit.distance;
        }

        if (closestCollider == null)
        {
            return false;
        }

        // 先把视觉与爆炸位置移动到真实接触点，再进入和 Trigger 相同的结算入口。
        Vector3 hitPosition = currentPosition + direction * Mathf.Max(0f, closestDistance);
        projectileRigidbody.position = hitPosition;
        transform.position = hitPosition;
        return TryHandleCollision(closestCollider);
    }

    /// <summary>
    /// 法师火球保留出生点重叠爆炸；弓箭手不会进入这里，避免箭矢在生成帧立即回池。
    /// </summary>
    private bool TryResolveInitialOverlap(Vector3 position)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            position,
            Mathf.Max(0.02f, collisionRadius),
            initialOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Collide);

        Collider closestCollider = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = initialOverlapBuffer[i];
            initialOverlapBuffer[i] = null;
            if (!IsCollisionCandidate(candidate))
            {
                continue;
            }

            Vector3 closestPoint = candidate.ClosestPoint(position);
            float sqrDistance = (closestPoint - position).sqrMagnitude;
            if (sqrDistance < closestDistance)
            {
                closestDistance = sqrDistance;
                closestCollider = candidate;
            }
        }

        return closestCollider != null && TryHandleCollision(closestCollider);
    }

    /// <summary>
    /// 记录弓箭出生时已经重叠的怪物身体，但不立即伤害或回收。
    /// 这些目标在本支箭生命周期内保持忽略，保证每次点击都能看到箭真正飞离弩口。
    /// </summary>
    private void CaptureSpawnOverlapTargets(Vector3 position)
    {
        int overlapCount = Physics.OverlapSphereNonAlloc(
            position,
            Mathf.Max(0.02f, collisionRadius),
            initialOverlapBuffer,
            ~0,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider candidate = initialOverlapBuffer[i];
            initialOverlapBuffer[i] = null;
            if (candidate == null ||
                candidate.transform == transform ||
                candidate.transform.IsChildOf(transform) ||
                (ownerTransform != null &&
                 (candidate.transform == ownerTransform ||
                  candidate.transform.IsChildOf(ownerTransform))))
            {
                continue;
            }

            if (!candidate.isTrigger &&
                PlayerBasicAttackDamageResolver.TryGetTarget(
                    candidate,
                    out FighterInterface ignoredTarget))
            {
                ignoredSpawnOverlapTargets.Add(candidate);
            }
        }
    }

    private bool IsStraightSingleTargetProjectile()
    {
        return trajectory == CharacterProjectileTrajectory.Straight &&
               explosionRadius <= 0f;
    }

    private bool IsCollisionCandidate(Collider other)
    {
        if (released || other == null ||
            other.transform == transform ||
            other.transform.IsChildOf(transform))
        {
            return false;
        }

        // 投射物发射后会暂时脱离玩家层级，因此显式保存玩家 Transform 来过滤自身碰撞。
        if (ownerTransform != null &&
            (other.transform == ownerTransform || other.transform.IsChildOf(ownerTransform)))
        {
            return false;
        }

        bool isStraightSingleTarget = IsStraightSingleTargetProjectile();
        if (!isStraightSingleTarget &&
            Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
        {
            return false;
        }

        bool isDamageTarget = PlayerBasicAttackDamageResolver.TryGetTarget(
            other,
            out FighterInterface ignoredTarget);
        if (isStraightSingleTarget)
        {
            // 弓箭的扫掠查询自行完成目标过滤，不再依赖 Layer 碰撞矩阵。
            // 只接受正式怪物身体；墙体、地面、场景物件和所有 Trigger 都不会终止箭矢。
            return !other.isTrigger &&
                   isDamageTarget &&
                   !ignoredSpawnOverlapTargets.Contains(other);
        }

        return isDamageTarget || !other.isTrigger;
    }

    /// <summary>
    /// Trigger、初始重叠和连续扫掠共用的唯一命中入口。
    /// Release 保持幂等，因此同一物理帧收到多种回调也只会结算一次。
    /// </summary>
    private bool TryHandleCollision(Collider other)
    {
        if (!IsCollisionCandidate(other))
        {
            return false;
        }

        bool hitDamageTarget = PlayerBasicAttackDamageResolver.TryGetTarget(
            other,
            out FighterInterface ignoredTarget);
        if (explosionRadius > 0f)
        {
            Explode(hitDamageTarget ? other : null);
            return released;
        }

        if (PlayerBasicAttackDamageResolver.TryApply(ownerCombat, other))
        {
            Release();
            return true;
        }

        return false;
    }

    /// <summary>
    /// 法师火球爆炸时按 FighterInterface 去重，范围内每个目标各结算一次完整普通攻击。
    /// </summary>
    private void Explode(Collider directHit)
    {
        if (released)
        {
            return;
        }

        Vector3 explosionPosition = projectileRigidbody != null
            ? projectileRigidbody.position
            : transform.position;
        Physics.SyncTransforms();
        PlayerBasicAttackDamageResolver.ApplyInRadius(
            ownerCombat,
            explosionPosition,
            explosionRadius,
            ownerTransform,
            directHit,
            explosionOverlapBuffer,
            explosionHitTargets);

        explosionVisualCallback?.Invoke(explosionPosition, explosionRadius, projectileColor);
        Release();
    }

    /// <summary>
    /// 返回给定归一化进度的轨迹位置，测试可用它验证法师中点确实高于直线路径。
    /// </summary>
    private Vector3 EvaluateTrajectoryPosition(float normalizedProgress)
    {
        float progress = Mathf.Clamp01(normalizedProgress);
        Vector3 position =
            launchPosition +
            launchDirection * (speed * totalLifetime * progress);
        if (trajectory == CharacterProjectileTrajectory.Arc)
        {
            position += Vector3.up * (Mathf.Sin(Mathf.PI * progress) * arcHeight);
        }

        return position;
    }

    /// <summary>
    /// 回收入口保持幂等，同一物理帧触发多个碰撞时也只会入池一次。
    /// </summary>
    public void Release()
    {
        if (released)
        {
            return;
        }

        released = true;
        PlayerProjectileVisualUtility.StopEffects(gameObject);
        gameObject.SetActive(false);

        Action<PlayerBasicAttackProjectile> callback = releaseCallback;
        releaseCallback = null;
        explosionVisualCallback = null;
        ownerCombat = null;
        ownerTransform = null;
        ignoredSpawnOverlapTargets.Clear();
        explosionHitTargets.Clear();
        callback?.Invoke(this);
    }

    private void OnDisable()
    {
        if (projectileRigidbody != null)
        {
            projectileRigidbody.velocity = Vector3.zero;
            projectileRigidbody.angularVelocity = Vector3.zero;
        }

        elapsedFlightTime = 0f;
        ignoredSpawnOverlapTargets.Clear();
    }

    private void EnsureComponents()
    {
        projectileCollider = projectileCollider != null
            ? projectileCollider
            : GetComponent<SphereCollider>();
        if (projectileCollider == null)
        {
            projectileCollider = gameObject.AddComponent<SphereCollider>();
        }
        projectileCollider.isTrigger = true;
        projectileCollider.enabled = true;

        projectileRigidbody = projectileRigidbody != null
            ? projectileRigidbody
            : GetComponent<Rigidbody>();
        if (projectileRigidbody == null)
        {
            projectileRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        projectileRigidbody.useGravity = false;
        projectileRigidbody.isKinematic = true;
        projectileRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        projectileRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void CacheBaseVisualScale()
    {
        if (baseScaleCached)
        {
            return;
        }

        baseVisualScale = transform.localScale;
        baseScaleCached = true;
    }

    private void ConfigureCollisionRadius(float worldRadius)
    {
        collisionRadius = Mathf.Max(0.02f, worldRadius);
        float largestScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.y),
            Mathf.Abs(transform.lossyScale.z));
        projectileCollider.radius =
            collisionRadius /
            Mathf.Max(0.0001f, largestScale);
    }
}

/// <summary>
/// 普攻投射物表现工具：只修改实例颜色和播放状态，不修改共享 Material 或第三方 Prefab。
/// 这样法师普攻可以呈现紫色，而技能火球仍保留原来的颜色与资源配置。
/// </summary>
public static class PlayerProjectileVisualUtility
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

    public static void ApplyTint(GameObject visualRoot, Color tint)
    {
        if (visualRoot == null)
        {
            return;
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, tint);
            propertyBlock.SetColor(ColorId, tint);
            propertyBlock.SetColor(TintColorId, tint);
            renderer.SetPropertyBlock(propertyBlock);
        }

        ParticleSystem[] particleSystems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.startColor = tint;
        }

        TrailRenderer[] trails = visualRoot.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(tint, 0f),
                    new GradientColorKey(tint, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(tint.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            trails[i].colorGradient = gradient;
        }

        Light[] lights = visualRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].color = tint;
        }
    }

    /// <summary>
    /// 对象从池中取出时清除旧 Trail，并从头播放粒子，避免复用后出现上一发的残影。
    /// </summary>
    public static void RestartEffects(GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        TrailRenderer[] trails = visualRoot.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].enabled = true;
            trails[i].emitting = true;
            trails[i].Clear();
        }

        Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
        }

        ParticleSystem[] particleSystems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystems[i].Play(true);
        }

        Animator[] animators = visualRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].Rebind();
            animators[i].Update(0f);
        }
    }

    public static void StopEffects(GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = visualRoot.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        TrailRenderer[] trails = visualRoot.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].emitting = false;
            trails[i].Clear();
        }
    }

}
