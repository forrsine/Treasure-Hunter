using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 玩家技能释放组件：负责读取技能键，并在规则校验成功后执行技能效果。
/// 注意：是否学会、蓝量、冷却由 PlayerSkillSystem 判断；
/// 这里负责 Unity 场景里的表现和伤害，例如范围检测、生成毒雾区域。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillCastComponent : MonoBehaviour, IController
{
    private const int FireballSkillId = 1001;
    private const int PoisonAreaSkillId = 1002;
    private const int ScytheSpinSkillId = 2001;
    [SerializeField] private float fireballCastDistance = 5f;
    [SerializeField] private float poisonCastDistance = 4f;

    [Header("Fireball Visual")]
    [SerializeField] private float fireballFlyDuration = 0.28f;
    [SerializeField] private float fireballProjectileRadius = 0.35f;
    [SerializeField] private float fireballArcHeight = 0.6f;

    [Header("Skill VFX Prefabs")]
    [SerializeField] private GameObject fireballProjectileVfxPrefab;
    [SerializeField] private GameObject fireballExplosionVfxPrefab;
    [SerializeField] private GameObject poisonAreaVfxPrefab;
    [SerializeField] private GameObject scytheSpinVfxPrefab;

    [Header("Skill VFX Settings")]
    [SerializeField] private float fireballExplosionVfxLifeTime = 1.5f;
    [SerializeField] private float fireballExplosionVfxScale = 1f;
    [SerializeField] private float poisonAreaVfxScale = 100f;
    [SerializeField] private float scytheSpinVfxLifeTime = 1.2f;
    [SerializeField] private float scytheSpinVfxScale = 1f;
    [SerializeField] private float scytheSpinAnimationDuration = 1.05f;
    [SerializeField] private float debugScytheSpinRadius = 3f;

    [Header("Skill Range Preview")]
    [SerializeField] private float previewHeightOffset = 0.08f;
    [SerializeField] private float previewLineWidth = 0.06f;
    [SerializeField] private int previewCircleSegments = 96;
    [SerializeField] private Color fireballPreviewColor = new Color(1f, 0.35f, 0.05f, 0.9f);
    [SerializeField] private Color poisonPreviewColor = new Color(0.25f, 1f, 0.25f, 0.9f);
    [SerializeField] private Color scythePreviewColor = new Color(0.15f, 0.95f, 1f, 0.9f);

    private readonly HashSet<FighterInterface> hitTargets = new HashSet<FighterInterface>();
    private PlayerPresentationComponent presentation;
    private PlayerCombatComponent combat;
    private PlayerAudioComponent audioComponent;
    private bool defaultVfxLoaded;
    private bool defaultVfxLoadStarted;
    private Coroutine defaultVfxLoadCoroutine;
    private AddressableAssetService addressableAssetService;
    private AsyncOperationHandle<GameObject> fireballProjectileVfxHandle;
    private AsyncOperationHandle<GameObject> fireballExplosionVfxHandle;
    private AsyncOperationHandle<GameObject> poisonAreaVfxHandle;
    private AsyncOperationHandle<GameObject> scytheSpinVfxHandle;
    private GameObject previewRoot;
    private LineRenderer previewLine;
    private Material previewMaterial;
    private bool isPreviewActive;
    private int previewSkillId;
    public IArchitecture GetArchitecture()
    {
        return TreasureHunterArchitecture.Interface;
    }

    /// <summary>
    /// 玩家进入场景后立即预加载技能特效，让首次释放技能通常可以直接从内存中取得 Prefab。
    /// 异步加载不会阻塞主线程，加载期间技能伤害逻辑仍可正常执行。
    /// </summary>
    private void Start()
    {
        EnsureDefaultVfxLoaded();
    }

    /// <summary>
    /// 由 PlayerRuntimeController 注入运行时依赖。
    /// 技能系统只负责释放技能，具体动画仍交给表现组件，避免技能脚本直接操作 Animator 细节。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        combat = player != null ? player.GetComponent<PlayerCombatComponent>() : GetComponent<PlayerCombatComponent>();
        audioComponent = player != null ? player.Audio : GetComponent<PlayerAudioComponent>();
    }

    /// <summary>
    /// Unity 独立推进技能冷却。
    /// 不能把冷却放在 Tick 输入流程里，否则玩家翻滚、输入对象暂时缺失时会让冷却错误暂停。
    /// Time.timeScale 为 0 时 Time.deltaTime 也是 0，因此升级选择等正常暂停仍会保留。
    /// </summary>
    private void Update()
    {
        PlayerRuntimeController currentPlayer = GameplayRuntime.Instance.CurrentPlayer;
        if (currentPlayer != null && currentPlayer.gameObject != gameObject)
        {
            return;
        }

        this.GetSystem<PlayerSkillSystem>().TickSkillCooldowns(Time.deltaTime);
    }

    /// <summary>
    /// 每帧由 PlayerRuntimeController 调用。
    /// 这里只处理技能输入，不处理普通攻击，也不再承担冷却计时。
    /// </summary>
    public void Tick()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null)
        {
            return;
        }

        HandleSkillPreviewInput(input);
    }

    /// <summary>
    /// 处理技能按住预览和松手释放。
    /// 按下技能键时只显示范围，松开时才真正走 TryCast 扣蓝、冷却和释放逻辑。
    /// </summary>
    private void HandleSkillPreviewInput(IGameplayInput input)
    {
        if (input.Skill1Down)
        {
            BeginSkillPreview(FireballSkillId);
        }
        else if (input.Skill2Down)
        {
            BeginSkillPreview(PoisonAreaSkillId);
        }
        else if (input.Skill3Down)
        {
            BeginSkillPreview(ScytheSpinSkillId);
        }

        if (isPreviewActive)
        {
            if (IsPreviewKeyHeld(input, previewSkillId))
            {
                RefreshSkillPreview(previewSkillId);
            }
            else
            {
                HideSkillPreview();
            }
        }

        if (input.Skill1Up)
        {
            EndSkillPreviewAndCast(FireballSkillId);
        }
        else if (input.Skill2Up)
        {
            EndSkillPreviewAndCast(PoisonAreaSkillId);
        }
        else if (input.Skill3Up)
        {
            EndSkillPreviewAndCast(ScytheSpinSkillId);
        }
    }

    private bool IsPreviewKeyHeld(IGameplayInput input, int skillId)
    {
        switch (skillId)
        {
            case FireballSkillId:
                return input.Skill1Held;
            case PoisonAreaSkillId:
                return input.Skill2Held;
            case ScytheSpinSkillId:
                return input.Skill3Held;
            default:
                return false;
        }
    }

    private void BeginSkillPreview(int skillId)
    {
        previewSkillId = skillId;
        RefreshSkillPreview(skillId);
    }

    private void EndSkillPreviewAndCast(int skillId)
    {
        if (previewSkillId == skillId)
        {
            HideSkillPreview();
        }

        TryCast(skillId);
    }

    /// <summary>
    /// 刷新技能范围预览圈。
    /// 预览只读取已学习技能的当前等级数据，不扣蓝、不进冷却。
    /// </summary>
    private void RefreshSkillPreview(int skillId)
    {
        SkillDefine skill;
        SkillLevelDefine levelData;
        if (!TryGetPreviewSkillData(skillId, out skill, out levelData))
        {
            HideSkillPreview();
            return;
        }

        EnsurePreviewVisual();

        Vector3 center = GetPreviewCenter(skill.GetSkillType());
        Color color = GetPreviewColor(skillId);

        previewRoot.transform.position = center;
        previewRoot.transform.rotation = Quaternion.identity;
        previewRoot.SetActive(true);

        previewLine.startColor = color;
        previewLine.endColor = color;
        previewLine.startWidth = Mathf.Max(0.01f, previewLineWidth);
        previewLine.endWidth = Mathf.Max(0.01f, previewLineWidth);
        DrawPreviewCircle(levelData.radius);

        isPreviewActive = true;
        previewSkillId = skillId;
    }

    private bool TryGetPreviewSkillData(int skillId, out SkillDefine skill, out SkillLevelDefine levelData)
    {
        skill = null;
        levelData = null;

        if (SkillDataManager.Instance == null)
        {
            return false;
        }

        PlayerSkillRuntimeData runtimeData = this.GetModel<PlayerSkillModel>().GetSkillRuntimeData(skillId);
        if (runtimeData == null)
        {
            return false;
        }

        skill = SkillDataManager.Instance.GetSkill(skillId);
        if (skill == null)
        {
            return false;
        }

        levelData = skill.GetLevelData(runtimeData.level);
        return levelData != null;
    }

    private Vector3 GetPreviewCenter(SkillType skillType)
    {
        Vector3 center = transform.position;

        switch (skillType)
        {
            case SkillType.ProjectileAoe:
                center += transform.forward * fireballCastDistance;
                break;
            case SkillType.AreaDot:
                center += transform.forward * poisonCastDistance;
                break;
            case SkillType.SelfAoe:
                break;
        }

        center.y = transform.position.y + previewHeightOffset;
        return center;
    }

    private Color GetPreviewColor(int skillId)
    {
        switch (skillId)
        {
            case FireballSkillId:
                return fireballPreviewColor;
            case PoisonAreaSkillId:
                return poisonPreviewColor;
            case ScytheSpinSkillId:
                return scythePreviewColor;
            default:
                return Color.white;
        }
    }

    private void EnsurePreviewVisual()
    {
        if (previewRoot != null && previewLine != null)
        {
            return;
        }

        previewRoot = new GameObject("SkillRangePreview");
        previewRoot.transform.SetParent(transform, false);
        previewRoot.SetActive(false);

        previewLine = previewRoot.AddComponent<LineRenderer>();
        previewLine.useWorldSpace = false;
        previewLine.loop = true;
        previewLine.numCapVertices = 4;
        previewLine.numCornerVertices = 4;
        previewLine.material = GetPreviewMaterial();
    }

    private Material GetPreviewMaterial()
    {
        if (previewMaterial == null)
        {
            previewMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        return previewMaterial;
    }

    private void DrawPreviewCircle(float radius)
    {
        int segmentCount = Mathf.Max(24, previewCircleSegments);
        previewLine.positionCount = segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float angle = Mathf.PI * 2f * i / segmentCount;
            previewLine.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void HideSkillPreview()
    {
        isPreviewActive = false;
        previewSkillId = 0;

        if (previewRoot != null)
        {
            previewRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideSkillPreview();
    }

    private void OnDestroy()
    {
        if (defaultVfxLoadCoroutine != null)
        {
            StopCoroutine(defaultVfxLoadCoroutine);
            defaultVfxLoadCoroutine = null;
        }

        // 先销毁对象池中的 Prefab 实例，再延迟释放资源句柄，保证 Addressables 引用计数和实例生命周期对称。
        if (SkillVisualPool.Instance != null)
        {
            SkillVisualPool.Instance.ClearPrefabVfxPool(SkillVfxAddresses.FireballProjectilePoolKey);
            SkillVisualPool.Instance.ClearPrefabVfxPool(SkillVfxAddresses.FireballExplosionPoolKey);
            SkillVisualPool.Instance.ClearPrefabVfxPool(SkillVfxAddresses.PoisonAreaPoolKey);
            SkillVisualPool.Instance.ClearPrefabVfxPool(SkillVfxAddresses.ScytheSpinPoolKey);
        }

        ReleaseAddressableVfxHandle(fireballProjectileVfxHandle);
        ReleaseAddressableVfxHandle(fireballExplosionVfxHandle);
        ReleaseAddressableVfxHandle(poisonAreaVfxHandle);
        ReleaseAddressableVfxHandle(scytheSpinVfxHandle);
    }
    /// <summary>
    /// 技能统一释放入口。
    /// 先通过 Command 校验是否能释放；成功后再执行具体技能效果。
    /// </summary>
    private void TryCast(int skillId)
    {
        bool success = this.SendCommand(new TryCastPlayerSkillCommand(skillId));
        if (!success)
        {
            return;
        }

        SkillDefine skill = SkillDataManager.Instance.GetSkill(skillId);
        PlayerSkillRuntimeData runtimeData = this.GetModel<PlayerSkillModel>().GetSkillRuntimeData(skillId);
        if (skill == null || runtimeData == null)
        {
            return;
        }

        SkillLevelDefine levelData = skill.GetLevelData(runtimeData.level);
        if (levelData == null)
        {
            return;
        }

        // PlayerSkillCastComponent 是运行时自动补到玩家身上的，释放技能前先确保默认特效已经加载。
        EnsureDefaultVfxLoaded();

        if (skillId == ScytheSpinSkillId)
        {
            PlayScytheSpinAnimation();
        }

        switch (skill.GetSkillType())
        {
            case SkillType.ProjectileAoe:
                CastFireball(levelData);
                break;

            case SkillType.AreaDot:
                CastPoisonArea(levelData);
                break;

            case SkillType.SelfAoe:
                CastScytheSpin(levelData);
                break;
        }
    }

    /// <summary>
    /// 大火球：从玩家身前飞向目标点，到达后爆炸并造成范围伤害。
    /// 注意：扣蓝和冷却在 PlayerSkillSystem 已经完成，这里只处理表现和实际命中。
    /// </summary>
    private void CastFireball(SkillLevelDefine levelData)
    {
        Vector3 targetCenter = transform.position + transform.forward * fireballCastDistance;
        targetCenter.y = transform.position.y + 1f;

        int damage = CalculateSkillDamage(levelData);

        StartCoroutine(PlayFireballProjectileRoutine(targetCenter, levelData.radius, damage));

        Debug.Log($"释放大火球：伤害 {damage}，范围 {levelData.radius}");
    }

    /// <summary>
    /// 大火球飞行协程。
    /// 这里把“飞行表现”和“命中爆炸”串起来，让技能更像一个完整释放流程。
    /// </summary>
    private IEnumerator PlayFireballProjectileRoutine(Vector3 targetCenter, float explosionRadius, int damage)
    {
        Vector3 startPosition = transform.position + Vector3.up * 1.2f + transform.forward * 0.8f;

        GameObject projectileVisual = SpawnVfx(
            fireballProjectileVfxPrefab,
            startPosition,
            Quaternion.LookRotation(transform.forward));
        bool projectileVisualUsesPrefab = projectileVisual != null;

        // 如果真实特效资源没有加载成功，就回退到线稿火球，保证技能表现不会完全消失。
        if (projectileVisual == null)
        {
            projectileVisual = SkillLineEffect.CreateFireballProjectile(startPosition, fireballProjectileRadius);
            projectileVisualUsesPrefab = false;
        }

        float duration = Mathf.Max(0.05f, fireballFlyDuration);
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);

            // 基础直线移动。
            Vector3 position = Vector3.Lerp(startPosition, targetCenter, progress);

            // 加一点抛物线高度，让火球飞行更有动感。
            position.y += Mathf.Sin(progress * Mathf.PI) * fireballArcHeight;

            if (projectileVisual != null)
            {
                projectileVisual.transform.position = position;
                projectileVisual.transform.Rotate(Vector3.up, 720f * Time.deltaTime, Space.World);
                projectileVisual.transform.Rotate(Vector3.right, 360f * Time.deltaTime, Space.Self);
            }

            yield return null;
        }

        if (projectileVisual != null)
        {
            if (projectileVisualUsesPrefab)
            {
                ReleaseVfx(GetVfxPoolKey(fireballProjectileVfxPrefab), projectileVisual);
            }
            else
            {
                Destroy(projectileVisual);
            }
        }

        // 火球到达后才结算伤害，这样表现和逻辑更一致。
        DealDamageInRadius(targetCenter, explosionRadius, damage);

        GameObject explosionVfx = SpawnVfxWithAutoDestroy(
            fireballExplosionVfxPrefab,
            targetCenter,
            Quaternion.identity,
            fireballExplosionVfxLifeTime);

        if (explosionVfx != null)
        {
            explosionVfx.transform.localScale = Vector3.one * explosionRadius * fireballExplosionVfxScale;
        }
        else
        {
            SkillLineEffect.PlayFireballExplosion(targetCenter, explosionRadius);
        }
    }

    /// <summary>
    /// 毒雾：在玩家前方生成一个持续伤害区域。
    /// 现在毒雾对象也走对象池，避免持续技能频繁 New GameObject / Destroy。
    /// </summary>
    private void CastPoisonArea(SkillLevelDefine levelData)
    {
        Vector3 center = transform.position + transform.forward * poisonCastDistance;
        center.y = transform.position.y;

        GameObject poisonObject;
        if (SkillVisualPool.Instance != null)
        {
            poisonObject = SkillVisualPool.Instance.GetEffectObject("PoisonAreaEffect");
        }
        else
        {
            Debug.LogWarning("场景中没有 SkillVisualPool，毒雾会临时创建并销毁。");
            poisonObject = new GameObject("PoisonAreaEffect");
        }

        poisonObject.name = "PoisonAreaEffect";
        poisonObject.transform.position = center;
        poisonObject.transform.rotation = Quaternion.identity;

        PoisonAreaEffect effect = poisonObject.GetComponent<PoisonAreaEffect>();
        if (effect == null)
        {
            effect = poisonObject.AddComponent<PoisonAreaEffect>();
        }

        effect.Initialize(
            ownerRoot: transform.root,
            damagePerTick: CalculateSkillDamage(levelData),
            radius: levelData.radius,
            duration: levelData.duration,
            tickInterval: levelData.tickInterval,
            slowRate: levelData.slowRate,
            combatSystem: this.GetSystem<PlayerCombatSystem>());

        GameObject poisonVfx = SpawnVfxWithAutoDestroy(
            poisonAreaVfxPrefab,
            center,
            Quaternion.identity,
            levelData.duration);

        if (poisonVfx != null)
        {
            poisonVfx.transform.localScale = Vector3.one * levelData.radius * poisonAreaVfxScale;
        }
        else
        {
            SkillLineEffect.PlayPoisonArea(center, levelData.radius, levelData.duration);
        }

        Debug.Log($"释放毒雾：持续 {levelData.duration} 秒，范围 {levelData.radius}");
    }

    /// <summary>
    /// 镰刀旋转：以玩家自身为中心造成一次范围伤害。
    /// </summary>
    private void CastScytheSpin(SkillLevelDefine levelData)
    {
        Vector3 center = transform.position;
        center.y += 1f;

        int damage = CalculateSkillDamage(levelData);
        DealDamageInRadius(center, levelData.radius, damage);

        GameObject scytheVfx = SpawnVfxWithAutoDestroy(
            scytheSpinVfxPrefab,
            transform.position + Vector3.up * 0.1f,
            Quaternion.identity,
            scytheSpinVfxLifeTime);

        if (scytheVfx != null)
        {
            scytheVfx.transform.localScale = Vector3.one * levelData.radius * scytheSpinVfxScale;
        }
        else
        {
            SkillLineEffect.PlayScytheSpin(transform.position, levelData.radius);
        }

        Debug.Log($"释放镰刀大旋转：伤害 {damage}，范围 {levelData.radius}");
    }

    private void PlayScytheSpinAnimation()
    {
        if (combat == null || presentation == null || audioComponent == null)
        {
            Initialize(GetComponent<PlayerRuntimeController>());
        }

        // 大旋转的伤害来自技能范围检测，动画只负责表现。
        // 先取消普通攻击，避免复用 Atk3 动画时触发原本的普攻碰撞盒。
        combat?.CancelAttackForSkill();
        if (audioComponent != null && audioComponent.AutoPlayActions)
        {
            audioComponent.PlaySkill();
        }

        presentation?.PlaySkill(scytheSpinAnimationDuration);
    }

    private int CalculateSkillDamage(SkillLevelDefine levelData)
    {
        int attackPower = this.GetModel<PlayerModel>().Stats.AttackPower;
        return Mathf.Max(1, Mathf.RoundToInt(attackPower * levelData.damageRate));
    }

    /// <summary>
    /// 范围伤害通用方法。
    /// 火球和镰刀旋转都复用它，避免重复写 OverlapSphere 逻辑。
    /// </summary>
    private void DealDamageInRadius(Vector3 center, float radius, int damage)
    {
        hitTargets.Clear();

        Collider[] colliders = Physics.OverlapSphere(center, radius);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider targetCollider = colliders[i];

            if (targetCollider.transform.root == transform.root)
            {
                continue;
            }

            FighterInterface fighter =
                targetCollider.GetComponent<FighterInterface>() ??
                targetCollider.GetComponentInParent<FighterInterface>();

            if (fighter == null || hitTargets.Contains(fighter))
            {
                continue;
            }

            hitTargets.Add(fighter);
            ApplySkillDamageWithFeedback(fighter, targetCollider, damage);
        }
    }

    /// <summary>
    /// 应用一次技能伤害，并同步显示伤害数字。
    /// 这里先预估实际伤害，再调用目标 Hit，最后把实际伤害交给吸血等后续结算。
    /// </summary>
    private void ApplySkillDamageWithFeedback(FighterInterface fighter, Collider targetCollider, int damage)
    {
        if (fighter == null || damage <= 0)
        {
            return;
        }

        Transform feedbackTarget;
        bool shouldShowDamageText;
        int appliedDamage = CombatFeedbackUtility.PreviewAppliedDamage(
            fighter,
            targetCollider,
            damage,
            out feedbackTarget,
            out shouldShowDamageText);

        fighter.Hit(damage);

        CombatFeedbackUtility.ShowPlayerDamageText(
            feedbackTarget,
            targetCollider,
            appliedDamage,
            shouldShowDamageText,
            false);

        if (appliedDamage > 0)
        {
            this.GetSystem<PlayerCombatSystem>().HandleDamageDealt(appliedDamage);
        }
    }
    /// <summary>
    /// 从对象池获取球形表现，用来显示火球爆炸范围。
    /// </summary>
    private void CreatePooledSphereVisual(
        string visualName,
        Vector3 position,
        float radius,
        Color color,
        float lifeTime)
    {
        if (SkillVisualPool.Instance == null)
        {
            Debug.LogWarning("场景中没有 SkillVisualPool，技能表现不会显示。");
            return;
        }

        GameObject visual = SkillVisualPool.Instance.GetVisual(visualName, PrimitiveType.Sphere);
        visual.transform.position = position;
        visual.transform.rotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * radius * 2f;

        SetVisualColor(visual, color);

        StartCoroutine(ReleaseVisualAfterSeconds(visualName, visual, lifeTime));
    }

    /// <summary>
    /// 从对象池获取扁圆柱表现，用来显示自身范围技能。
    /// </summary>
    private void CreatePooledCircleVisual(
        string visualName,
        Vector3 position,
        float radius,
        Color color,
        float lifeTime)
    {
        if (SkillVisualPool.Instance == null)
        {
            Debug.LogWarning("场景中没有 SkillVisualPool，技能表现不会显示。");
            return;
        }

        GameObject visual = SkillVisualPool.Instance.GetVisual(visualName, PrimitiveType.Cylinder);
        visual.transform.position = position + Vector3.up * 0.05f;
        visual.transform.rotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(radius * 2f, 0.04f, radius * 2f);

        SetVisualColor(visual, color);

        StartCoroutine(ReleaseVisualAfterSeconds(visualName, visual, lifeTime));
    }

    /// <summary>
    /// 设置表现对象颜色。
    /// 这里单独抽出来，是为了以后替换材质、粒子特效时更好改。
    /// </summary>
    private void SetVisualColor(GameObject visual, Color color)
    {
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
    }

    /// <summary>
    /// 生成一个技能特效。
    /// 优先从 SkillVisualPool 获取 Prefab 实例，避免技能频繁释放时反复 Instantiate / Destroy。
    /// 如果场景中没有对象池，则退回普通 Instantiate，保证功能不会因为对象池缺失而中断。
    /// </summary>
    private GameObject SpawnVfx(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        EnsureDefaultVfxLoaded();

        if (prefab == null)
        {
            return null;
        }

        string poolKey = GetVfxPoolKey(prefab);
        GameObject vfx;

        if (SkillVisualPool.Instance != null)
        {
            vfx = SkillVisualPool.Instance.GetPrefabVfx(poolKey, prefab);
        }
        else
        {
            Debug.LogWarning("场景中没有 SkillVisualPool，技能特效会临时创建并销毁。");
            vfx = Instantiate(prefab);
        }

        if (vfx == null)
        {
            return null;
        }

        vfx.transform.SetParent(null);
        vfx.transform.position = position;
        vfx.transform.rotation = rotation;
        vfx.transform.localScale = Vector3.one;

        RestartParticleSystems(vfx);
        return vfx;
    }

    /// <summary>
    /// 生成一个会自动回收的技能特效。
    /// 注意：这里不再直接 Destroy，而是优先把对象放回对象池等待下次复用。
    /// </summary>
    private GameObject SpawnVfxWithAutoDestroy(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float lifeTime)
    {
        GameObject vfx = SpawnVfx(prefab, position, rotation);
        if (vfx == null)
        {
            return null;
        }

        StartCoroutine(ReleaseVfxAfterSeconds(GetVfxPoolKey(prefab), vfx, Mathf.Max(0.1f, lifeTime)));
        return vfx;
    }

    /// <summary>
    /// 等待一段时间后回收真实技能特效。
    /// 这是技能特效对象池的核心流程：播放结束后隐藏并复用，而不是销毁对象。
    /// </summary>
    private IEnumerator ReleaseVfxAfterSeconds(string poolKey, GameObject vfx, float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);
        ReleaseVfx(poolKey, vfx);
    }

    /// <summary>
    /// 回收一个真实技能特效。
    /// 有对象池时回收到池中；没有对象池时销毁，保证测试场景也能正常运行。
    /// </summary>
    private void ReleaseVfx(string poolKey, GameObject vfx)
    {
        if (vfx == null)
        {
            return;
        }

        if (SkillVisualPool.Instance != null && !string.IsNullOrEmpty(poolKey))
        {
            SkillVisualPool.Instance.ReleasePrefabVfx(poolKey, vfx);
        }
        else
        {
            Destroy(vfx);
        }
    }

    /// <summary>
    /// 获取真实特效在对象池中的分类 Key。
    /// 用 Prefab 名字作为 Key，方便在 Hierarchy 中观察每类特效的复用情况。
    /// </summary>
    private string GetVfxPoolKey(GameObject prefab)
    {
        return prefab != null ? prefab.name : string.Empty;
    }
    /// <summary>
    /// 开始异步加载默认技能特效。
    /// 因为 PlayerSkillCastComponent 是运行时自动补到玩家身上的，所以不依赖 Inspector 手动拖引用。
    /// </summary>
    private void EnsureDefaultVfxLoaded()
    {
        if (defaultVfxLoaded || defaultVfxLoadStarted)
        {
            return;
        }

        defaultVfxLoadStarted = true;
        defaultVfxLoadCoroutine = StartCoroutine(LoadDefaultVfxAsync());
    }

    /// <summary>
    /// 同时发起四个加载请求，再逐个等待结果。
    /// 请求会并行执行，等待顺序不会把四次磁盘读取变成串行加载。
    /// </summary>
    private IEnumerator LoadDefaultVfxAsync()
    {
        addressableAssetService = AddressableAssetService.GetOrCreate();

        if (fireballProjectileVfxPrefab == null)
        {
            fireballProjectileVfxHandle = addressableAssetService.LoadPrefabAsync(SkillVfxAddresses.FireballProjectile);
        }

        if (fireballExplosionVfxPrefab == null)
        {
            fireballExplosionVfxHandle = addressableAssetService.LoadPrefabAsync(SkillVfxAddresses.FireballExplosion);
        }

        if (poisonAreaVfxPrefab == null)
        {
            poisonAreaVfxHandle = addressableAssetService.LoadPrefabAsync(SkillVfxAddresses.PoisonArea);
        }

        if (scytheSpinVfxPrefab == null)
        {
            scytheSpinVfxHandle = addressableAssetService.LoadPrefabAsync(SkillVfxAddresses.ScytheSpin);
        }

        yield return LoadVfxPrefab(
            fireballProjectileVfxHandle,
            SkillVfxAddresses.FireballProjectile,
            prefab => fireballProjectileVfxPrefab = prefab);
        yield return LoadVfxPrefab(
            fireballExplosionVfxHandle,
            SkillVfxAddresses.FireballExplosion,
            prefab => fireballExplosionVfxPrefab = prefab);
        yield return LoadVfxPrefab(
            poisonAreaVfxHandle,
            SkillVfxAddresses.PoisonArea,
            prefab => poisonAreaVfxPrefab = prefab);
        yield return LoadVfxPrefab(
            scytheSpinVfxHandle,
            SkillVfxAddresses.ScytheSpin,
            prefab => scytheSpinVfxPrefab = prefab);

        defaultVfxLoaded = true;
        defaultVfxLoadCoroutine = null;
    }

    private IEnumerator LoadVfxPrefab(
        AsyncOperationHandle<GameObject> handle,
        string address,
        System.Action<GameObject> assignPrefab)
    {
        if (!handle.IsValid())
        {
            yield break;
        }

        yield return handle;

        if (addressableAssetService != null &&
            addressableAssetService.TryGetLoadedPrefab(handle, address, out GameObject prefab))
        {
            assignPrefab(prefab);
        }
    }

    private void ReleaseAddressableVfxHandle(AsyncOperationHandle<GameObject> handle)
    {
        if (addressableAssetService != null && handle.IsValid())
        {
            addressableAssetService.ReleasePrefabAfterInstanceCleanup(handle);
        }
    }

    /// <summary>
    /// 重新播放特效里的粒子。
    /// 有些 Prefab 被实例化后粒子可能处在默认状态，手动 Clear/Play 可以让释放反馈更稳定。
    /// </summary>
    private void RestartParticleSystems(GameObject vfx)
    {
        if (vfx == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }
    }

    /// <summary>
    /// 等待一段时间后，把表现对象还给对象池。    /// 这就是对象池和 Destroy 的核心区别：对象不会销毁，只是隐藏并复用。
    /// </summary>
    private IEnumerator ReleaseVisualAfterSeconds(string visualName, GameObject visual, float lifeTime)
    {
        yield return new WaitForSeconds(lifeTime);

        if (SkillVisualPool.Instance != null)
        {
            SkillVisualPool.Instance.ReleaseVisual(visualName, visual);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 fireballCenter = transform.position + transform.forward * fireballCastDistance;
        fireballCenter.y = transform.position.y + 1f;
        Gizmos.DrawWireSphere(fireballCenter, 3f);

        Gizmos.color = Color.green;
        Vector3 poisonCenter = transform.position + transform.forward * poisonCastDistance;
        Gizmos.DrawWireSphere(poisonCenter, 3f);

        Gizmos.color = Color.cyan;
        Vector3 scytheCenter = transform.position;
        scytheCenter.y += 1f;
        Gizmos.DrawWireSphere(scytheCenter, debugScytheSpinRadius);
    }
}

