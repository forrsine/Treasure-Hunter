using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 毒雾区域：负责持续范围伤害和简单减速。
/// 注意：这个对象现在由对象池复用，所以每次 Initialize 时必须重置运行时状态。
/// </summary>
public sealed class PoisonAreaEffect : MonoBehaviour
{
    private const string PoolKey = "PoisonAreaEffect";

    private struct SlimeSlowRecord
    {
        public float OriginalWalkSpeed;
        public int ReuseVersion;

        public SlimeSlowRecord(float originalWalkSpeed, int reuseVersion)
        {
            OriginalWalkSpeed = originalWalkSpeed;
            ReuseVersion = reuseVersion;
        }
    }

    private readonly HashSet<FighterInterface> damagedTargetsThisTick =
        new HashSet<FighterInterface>();

    private readonly Dictionary<SlimeCo, SlimeSlowRecord> slowedSlimes =
        new Dictionary<SlimeCo, SlimeSlowRecord>();

    private Transform ownerRoot;
    private PlayerCombatSystem combatSystem;
    private GameObject visualObject;

    private int damagePerTick;
    private float radius;
    private float duration;
    private float tickInterval;
    private float slowRate;

    private float lifeTimer;
    private float tickTimer;
    private bool isInitialized;

    /// <summary>
    /// 初始化毒雾。
    /// 因为对象会被对象池复用，所以这里要重新设置所有运行时数据。
    /// </summary>
    public void Initialize(
        Transform ownerRoot,
        int damagePerTick,
        float radius,
        float duration,
        float tickInterval,
        float slowRate,
        PlayerCombatSystem combatSystem)
    {
        // 防止对象池复用时，上一次毒雾留下减速状态。
        RestoreAllSlimes();
        damagedTargetsThisTick.Clear();

        this.ownerRoot = ownerRoot;
        this.damagePerTick = Mathf.Max(1, damagePerTick);
        this.radius = Mathf.Max(0.1f, radius);
        this.duration = Mathf.Max(0.1f, duration);
        this.tickInterval = Mathf.Max(0.1f, tickInterval);
        this.slowRate = Mathf.Clamp01(slowRate);
        this.combatSystem = combatSystem;

        lifeTimer = this.duration;
        tickTimer = 0f;
        isInitialized = true;

        EnsureSimpleVisual();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        lifeTimer -= Time.deltaTime;
        tickTimer -= Time.deltaTime;

        if (tickTimer <= 0f)
        {
            tickTimer = tickInterval;
            TickDamage();
        }

        if (lifeTimer <= 0f)
        {
            ReleaseToPool();
        }
    }

    /// <summary>
    /// 每隔 tickInterval 秒执行一次范围伤害。
    /// </summary>
    private void TickDamage()
    {
        damagedTargetsThisTick.Clear();

        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];

            if (ownerRoot != null && targetCollider.transform.root == ownerRoot)
            {
                continue;
            }

            FighterInterface fighter =
                targetCollider.GetComponent<FighterInterface>() ??
                targetCollider.GetComponentInParent<FighterInterface>();

            if (fighter == null || damagedTargetsThisTick.Contains(fighter))
            {
                continue;
            }

            damagedTargetsThisTick.Add(fighter);
            ApplyTickDamageWithFeedback(fighter, targetCollider);

            TryApplySlow(targetCollider);
        }
    }

    /// <summary>
    /// 应用一次毒雾 Tick 伤害，并显示对应伤害数字。
    /// 毒雾是持续技能，数字反馈能让玩家明确看到它每秒都在生效。
    /// </summary>
    private void ApplyTickDamageWithFeedback(FighterInterface fighter, Collider targetCollider)
    {
        if (fighter == null || damagePerTick <= 0)
        {
            return;
        }

        Transform feedbackTarget;
        bool shouldShowDamageText;
        int appliedDamage = CombatFeedbackUtility.PreviewAppliedDamage(
            fighter,
            targetCollider,
            damagePerTick,
            out feedbackTarget,
            out shouldShowDamageText);

        fighter.Hit(damagePerTick);

        CombatFeedbackUtility.ShowPlayerDamageText(
            feedbackTarget,
            targetCollider,
            appliedDamage,
            shouldShowDamageText,
            false);

        if (combatSystem != null && appliedDamage > 0)
        {
            combatSystem.HandleDamageDealt(appliedDamage);
        }
    }
    /// <summary>
    /// 第一版减速只处理 SlimeCo。
    /// 后续怪物类型多了，可以抽 IMoveSpeedModifier 接口统一处理。
    /// </summary>
    private void TryApplySlow(Collider targetCollider)
    {
        SlimeCo slime = targetCollider.GetComponentInParent<SlimeCo>();
        if (slime == null)
        {
            return;
        }

        if (slowedSlimes.TryGetValue(slime, out SlimeSlowRecord slowRecord))
        {
            if (slowRecord.ReuseVersion == slime.ReuseVersion)
            {
                return;
            }

            slowedSlimes.Remove(slime);
        }

        slowedSlimes.Add(slime, new SlimeSlowRecord(slime.walkSpeed, slime.ReuseVersion));
        slime.walkSpeed *= 1f - slowRate;
    }

    private void RestoreSlimeSpeed(SlimeCo slime, SlimeSlowRecord slowRecord)
    {
        if (slime == null)
        {
            return;
        }

        if (slime.ReuseVersion != slowRecord.ReuseVersion)
        {
            return;
        }

        slime.walkSpeed = slowRecord.OriginalWalkSpeed;
    }
    /// <summary>
    /// 毒雾结束或对象被禁用时，恢复被减速怪物的速度。
    /// 这是对象池里非常重要的一步，避免状态残留。
    /// </summary>
    private void RestoreAllSlimes()
    {
        foreach (KeyValuePair<SlimeCo, SlimeSlowRecord> pair in slowedSlimes)
        {
            RestoreSlimeSpeed(pair.Key, pair.Value);
        }

        slowedSlimes.Clear();
    }

    /// <summary>
    /// 创建或刷新毒雾的简单圆形表现。
    /// 对象池复用时，不重复创建多个圆柱体，只更新已有表现。
    /// </summary>
    private void EnsureSimpleVisual()
    {
        if (visualObject == null)
        {
            visualObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visualObject.name = "PoisonAreaVisual";
            visualObject.transform.SetParent(transform);

            Collider visualCollider = visualObject.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }
        }

        visualObject.SetActive(true);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);

        Renderer renderer = visualObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        }
    }

    /// <summary>
    /// 毒雾持续时间结束后回收到对象池。
    /// 如果场景里没有对象池，就退回 Destroy，保证功能不会直接报错。
    /// </summary>
    private void ReleaseToPool()
    {
        isInitialized = false;
        RestoreAllSlimes();

        if (SkillVisualPool.Instance != null)
        {
            SkillVisualPool.Instance.ReleaseVisual(PoolKey, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        RestoreAllSlimes();
    }

    private void OnDestroy()
    {
        RestoreAllSlimes();
    }
}