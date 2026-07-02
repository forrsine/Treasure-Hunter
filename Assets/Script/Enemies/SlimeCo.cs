using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 史莱姆敌人行为控制器：实现状态机（闲置/巡逻/追击/攻击/死亡），
/// 处理玩家检测、移动、攻击、受击动画及材质颜色变化。
/// 
/// 新手阅读顺序：
/// 1. enemyState 决定史莱姆当前做什么。
/// 2. Update 每帧调用 EveryFrame，EveryFrame 根据 enemyState 分发到 DoIdle/DoPatrol/DoPersuit/DoAtk/DoDie。
/// 3. Hit 是被玩家攻击时的入口，负责扣血、播放受击效果、死亡奖励。
/// 4. ApplyVaultDifficulty 会在金库被击破后提升怪物血量、攻击、经验。
/// 5. Shoot、EnableAtk、DisableAtk 通常由动画事件调用，和攻击动画配合。
/// </summary>
public class SlimeCo : MonoBehaviour, FighterInterface
{
    // ---------- 状态枚举 ----------
    public enum EnemyState
    {
        Idle,       // 闲置
        Patrol,     // 巡逻
        Persuit,    // 追击（注意拼写保留原样）
        Atk,        // 攻击
        Die         // 死亡
    }
    public EnemyState enemyState;   // 当前状态

    public enum SlimeType
    {
        Slime1,     // 史莱姆类型1（近战？）
        Slime2,     // 史莱姆类型2（远程射击？）
    }
    public SlimeType slimeType;     // 当前史莱姆类型

    // ---------- 基本参数 ----------
    public float IdleTime = 3.5f;               // 闲置持续时间
    float curIdleTime = 0f;                     // 闲置计时器
    public float checkDistance = 5f;            // 检测玩家的距离
    public Transform target;                    // 玩家目标
    public Transform[] partrolTargets;          // 巡逻路径点
    int partrolIndex;                           // 当前巡逻点索引
    public Animator animator;                   // 动画控制器

    public float walkSpeed = 2f;                // 移动速度
    public CharacterController cc;              // 角色控制器
    public float maxPersuitDistance = 8f;       // 最大追击距离（超出则放弃）
    public float atkDistance = 2f;              // 攻击距离
    public float rotateSpeed = 15f;             // 旋转速度

    private float atkCd = 1f;                   // 攻击冷却时间
    private float curAtkCd = 0f;                // 当前攻击冷却计时
    public Collider atkCollider;                // 攻击碰撞体（近战用）
    public int Hp = 5;                          // 生命值
    public int HpMax = 5;
    private bool isDie = false;                 // 是否已死亡
    bool destroyScheduled = false;              // 是否已安排销毁

    // ---------- 材质颜色及纹理变化 ----------
    public SkinnedMeshRenderer myRenderer;      // 渲染器
    public Color hitColor;                      // 受击时颜色
    public Color defaultColor;                  // 默认颜色
    public Texture hitTt;                       // 受击时纹理
    public Texture defaultTt;                   // 默认纹理
    public float colorTime = 0.1f;              // 颜色变化持续时间
    public float destroyDelay = 2f;             // 死亡后销毁延迟

    // ---------- 受击动画相关 ----------
    public string hitTriggerName = "GetHit";            // 受击触发参数名
    public string hitStateName = "GetHit";              // 受击动画状态名
    public float hitAnimationCooldown = 3f;             // 受击动画冷却时间
    public float hitAnimationFallbackDuration = 0.6f;   // 受击动画回退等待时间

    bool isColorChange = false;                 // 是否正在颜色变化中
    float changeTime;                           // 颜色变化开始时间
    Material runtimeMaterial;                   // 运行时材质实例
    bool useMaskTintShader;                     // 是否使用带 Mask 的着色器（支持多个颜色通道）
    Color defaultTintColor01;                   // 默认颜色01（Mask着色器）
    Color defaultTintColor02;                   // 默认颜色02
    Color defaultTintColor03;                   // 默认颜色03
    Texture defaultMainTexture;                 // 默认主纹理
    Texture defaultAlbedoTexture;               // 默认漫反射纹理

    // ---------- 远程攻击参数 ----------
    public GameObject Bullet;                   // 子弹预制体
    public Transform shootPos;                  // 发射位置
    public float bulletSpeed = 8f;              // 子弹速度
    public float bulletLifeTime = 2f;           // 子弹存活时间

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip meleeAttackClip;
    [SerializeField] private AudioClip rangedShootClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] [Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float hitVolume = 1f;

    // ---------- 着色器属性名称常量 ----------
    const string MainTexProperty = "_MainTex";
    const string AlbedoProperty = "_Albedo";
    const string ColorProperty = "_Color";
    const string Color01Property = "_Color01";
    const string Color02Property = "_Color02";
    const string Color03Property = "_Color03";

    // ---------- 受击动画状态机控制 ----------
    float nextHitAnimationAllowedTime;          // 下一次允许播放受击动画的时间（用于冷却）
    bool isHitAnimating;                        // 是否正在播放受击动画（锁定其他动作）
    int hitStateShortHash;                      // 受击动画状态的短哈希
    int hitStateFullPathHash;                   // 受击动画状态的全路径哈希
    EnemyState stateBeforeHit;                  // 受击前的状态（用于恢复）
    Coroutine hitRecoverCoroutine;              // 受击恢复协程引用

    public int Exp;      // 死亡后给玩家的经验。
    public int AtkPower; // 当前攻击力，近战和远程子弹都会读取。
    public Image HpBar;  // 头顶血条填充图。

    // 下面三项保存预制体里配置的“基础数值”。
    // 金库每次被击破后，实际生命/攻击/经验都从基础数值重新乘倍率，避免重复叠乘导致数值失控。
    private int baseHpMax;
    private int baseAtkPower;
    private int baseExp;

    // -1 表示还没有按金库击破次数应用过难度；出生时会用当前难度初始化。
    private int appliedVaultDestroyedCount = -1;
    private bool baseStatsCached;

    /// <summary>
    /// 刷新史莱姆头顶血条比例。
    /// </summary>
    public void UpDateHpBar()
    {
        if (HpBar != null)
        {
            HpBar.fillAmount = HpMax > 0 ? (float)Hp / (float)HpMax : 0f;
        }
    }

    // ---------- 初始化 ----------
    private void Awake()
    {
        // 先记录预制体上的基础数值，后续难度成长都按基础值计算。
        CacheBaseStats();
        CacheHitAnimationHashes();  // 缓存动画状态哈希
        EnsureAudioSource();
    }

    /// <summary>
    /// Inspector 数值变化时修正怪物基础数值，避免负数或 0。
    /// </summary>
    private void OnValidate()
    {
        // 编辑器中值改变时重新计算哈希和约束参数
        CacheHitAnimationHashes();
        hitAnimationCooldown = Mathf.Max(0f, hitAnimationCooldown);
        hitAnimationFallbackDuration = Mathf.Max(0.05f, hitAnimationFallbackDuration);
    }

    /// <summary>
    /// 初始化组件、状态、基础数值和当前金库难度。
    /// </summary>
    private void Start()
    {
        RefreshTargetFromRuntime();

        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (myRenderer == null)
        {
            myRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        if (myRenderer != null)
        {
            // 复制材质实例，避免影响预制体
            runtimeMaterial = myRenderer.material;
            CacheDefaultMaterialState();    // 缓存默认材质属性
        }
        else
        {
            Debug.LogWarning("SlimeCo is missing SkinnedMeshRenderer, hit color change will not work.", this);
        }

        // 怪物可能是在金库已被击破多次后才生成的，所以出生时要立刻套用当前全局难度。
        ApplyVaultDifficulty(GetCurrentVaultDestroyedCount(), false);
        UpDateHpBar();
    }

    /// <summary>
    /// 启用时订阅金库击破事件，后续跟随难度成长。
    /// </summary>
    private void OnEnable()
    {
        // 场上已有怪物也要跟随金库击破次数变强。
        BoxCo.OnVaultDestroyed += HandleVaultDestroyed;
        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;
        RefreshTargetFromRuntime();

        // 等一帧再分离巡逻点，避免刚生成时层级/位置还没稳定。
        StartCoroutine(initBronPos());
    }

    /// <summary>
    /// 禁用时取消金库事件订阅。
    /// </summary>
    private void OnDisable()
    {
        BoxCo.OnVaultDestroyed -= HandleVaultDestroyed;
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
    }

    private void HandleCurrentPlayerChanged(PlayerCo player)
    {
        target = player != null ? player.transform : null;
    }

    private void RefreshTargetFromRuntime()
    {
        if (GameplayRuntime.Instance.TryGetPlayerTransform(out Transform playerTransform))
        {
            target = playerTransform;
        }
    }

    IEnumerator initBronPos()
    {
        yield return null;
        // 将巡逻点从父级分离，避免移动时跟随
        

        for (int i = 0; i < partrolTargets.Length; i++)
        {
            if (partrolTargets[i] != null)
            {
                partrolTargets[i].parent = null;
            }
        }
    }

    // ---------- 每帧更新 ----------
    private void Update()
    {
        EveryFrame();
        if (HpBar != null && Camera.main != null)
        {
            HpBar.transform.rotation = Camera.main.transform.rotation;
        }
    }

    /// <summary>
    /// 主逻辑循环：处理受击动画锁定、状态机切换、颜色恢复
    /// </summary>
    private void EveryFrame()
    {
        // 如果正在播放受击动画且未死亡，则强制停止移动、关闭攻击碰撞体、恢复颜色，并跳过其他状态
        if (isHitAnimating && enemyState != EnemyState.Die)
        {
            if (animator != null)
            {
                animator.SetBool("Move", false);
            }

            DisableAtk();
            MakeColorDefault();
            return;
        }

        // 根据当前状态执行对应方法
        // 这就是“状态机”：同一个 Update，根据状态不同执行不同逻辑。
        switch (enemyState)
        {
            case EnemyState.Idle:
                DoIdle();
                break;
            case EnemyState.Patrol:
                DoPatrol();
                break;
            case EnemyState.Persuit:
                DoPersuit();
                break;
            case EnemyState.Atk:
                DoAtk();
                break;
            case EnemyState.Die:
                DoDie();
                break;
        }

        // 每帧检查是否恢复默认颜色
        MakeColorDefault();
    }

    // ---------- 闲置状态 ----------
    private void DoIdle()
    {
        // 闲置状态：站着不动，等待一段时间后去巡逻；如果发现玩家，立刻追击。
        if (animator != null)
        {
            animator.SetBool("Move", false);
        }

        CheckPlayer();  // 检测玩家是否进入范围
        curIdleTime += Time.deltaTime;
        if (curIdleTime >= IdleTime)    // 闲置时间结束 -> 进入巡逻
        {
            curIdleTime = 0f;
            enemyState = EnemyState.Patrol;
        }
    }

    /// <summary>
    /// 检测玩家是否在检测范围内，若在则切换到追击状态
    /// </summary>
    void CheckPlayer()
    {
        if (target == null)
        {
            return;
        }

        if (Vector3.Distance(target.position, transform.position) <= checkDistance)
        {
            enemyState = EnemyState.Persuit;
        }
    }

    // ---------- 巡逻状态 ----------
    private void DoPatrol()
    {
        // 巡逻状态：朝当前巡逻点移动，到达后切到下一个点并回到闲置。
        CheckPlayer();  // 巡逻时也持续检测玩家

        // 如果没有巡逻点或当前点无效，退回闲置
        if (partrolTargets == null || partrolTargets.Length == 0 || partrolTargets[partrolIndex] == null)
        {
            enemyState = EnemyState.Idle;
            return;
        }

        // 朝向目标巡逻点（只旋转Y轴）
        Vector3 patrolTargetPosition = new Vector3(
            partrolTargets[partrolIndex].position.x,
            transform.position.y,
            partrolTargets[partrolIndex].position.z);
        Quaternion temp = Quaternion.LookRotation(patrolTargetPosition - transform.position);
        transform.rotation = Quaternion.Lerp(transform.rotation, temp, Time.deltaTime * rotateSpeed);

        // 向前移动
        Vector3 offset = transform.forward * walkSpeed;
        if (cc != null)
        {
            cc.SimpleMove(offset);
        }

        if (animator != null)
        {
            animator.SetBool("Move", true);
        }

        // 到达巡逻点 -> 切换到下一个点，状态转为闲置
        if (Vector3.Distance(patrolTargetPosition, transform.position) <= 0.1f)
        {
            partrolIndex = (partrolIndex + 1) % partrolTargets.Length;
            enemyState = EnemyState.Idle;
        }
    }

    // ---------- 追击状态 ----------
    private void DoPersuit()
    {
        // 追击状态：转向玩家并向玩家移动。
        if (target == null)
        {
            enemyState = EnemyState.Idle;
            return;
        }

        // 朝向玩家
        Vector3 targetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
        Quaternion temp = Quaternion.LookRotation(targetPosition - transform.position);
        transform.rotation = Quaternion.Lerp(transform.rotation, temp, Time.deltaTime * rotateSpeed);

        // 加速移动（1.5倍行走速度）
        Vector3 offset = transform.forward * walkSpeed * 1.5f;
        if (cc != null)
        {
            cc.SimpleMove(offset);
        }

        if (animator != null)
        {
            animator.SetBool("Move", true);
        }

        float curDistance = Vector3.Distance(targetPosition, transform.position);
        if (curDistance <= atkDistance)          // 进入攻击距离
        {
            enemyState = EnemyState.Atk;
        }
        else if (curDistance >= maxPersuitDistance) // 超出最大追击距离 -> 放弃
        {
            enemyState = EnemyState.Idle;
        }
    }

    // ---------- 攻击状态 ----------
    private void DoAtk()
    {
        // 攻击状态：停下来，冷却结束后根据距离决定攻击/追击/闲置。
        if (target == null)
        {
            enemyState = EnemyState.Idle;
            return;
        }

        if (animator != null)
        {
            animator.SetBool("Move", false);
        }

        curAtkCd -= Time.deltaTime;     // 攻击冷却递减
        float curDistance = Vector3.Distance(
            new Vector3(target.position.x, transform.position.y, target.position.z),
            transform.position);

        // 冷却结束
        if (curAtkCd <= 0f)
        {
            // 如果目标已超出攻击距离，根据范围决定追击还是闲置
            if (curDistance > atkDistance)
            {
                if (curDistance <= checkDistance)
                {
                    enemyState = EnemyState.Persuit;
                }
                else if (curDistance >= maxPersuitDistance)
                {
                    enemyState = EnemyState.Idle;
                }
            }
            else    // 在攻击距离内，执行攻击
            {
                curAtkCd = atkCd;
                Attack();
            }
        }
    }

    /// <summary>
    /// 执行攻击：根据史莱姆类型触发不同动画（Slime1近战、Slime2远程）
    /// </summary>
    private void Attack()
    {
        // Attack 只触发动画；真正造成伤害由动画事件打开碰撞体或发射子弹。
        if (animator == null)
        {
            return;
        }

        if (slimeType == SlimeType.Slime1)
        {
            // 触发近战攻击动画（需要动画控制器中有 "Atk1" 触发器）
            if (HasAnimatorParameter("Atk1", AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger("Atk1");
            }
        }
        else if (slimeType == SlimeType.Slime2)
        {
            // 远程攻击：先转向玩家（快速旋转）
            if (target != null)
            {
                Quaternion temp = Quaternion.LookRotation(
                    new Vector3(target.position.x, transform.position.y, target.position.z) - transform.position);
                transform.rotation = Quaternion.Lerp(transform.rotation, temp, Time.deltaTime * rotateSpeed * 5f);
            }

            // 触发远程攻击动画（优先 "Atk2"，否则 "Atk"）
            if (HasAnimatorParameter("Atk2", AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger("Atk2");
            }
            else if (HasAnimatorParameter("Atk", AnimatorControllerParameterType.Trigger))
            {
                animator.SetTrigger("Atk");
            }
        }
    }

    /// <summary>
    /// 远程攻击发射子弹（由动画事件调用）
    /// </summary>
    private void Shoot()
    {
        // 远程史莱姆的动画事件会调用这里生成子弹。
        if (Bullet == null || shootPos == null)
        {
            return;
        }

        PlayRangedShootSfxEvent();

        GameObject bullet_ = Instantiate(Bullet, shootPos.position, shootPos.rotation);
        BulletCo bulletCo = bullet_.GetComponent<BulletCo>();
        if (bulletCo == null)
        {
            bulletCo = bullet_.AddComponent<BulletCo>();
        }

        bulletCo.Initialize(transform, AtkPower, bulletSpeed, bulletLifeTime);
    }

    // ---------- 近战攻击碰撞体开关 ----------
    void EnableAtk()
    {
        // 近战攻击动画事件：打开攻击 Trigger Collider。
        if (atkCollider != null)
        {
            atkCollider.enabled = true;
        }

        PlayMeleeAttackSfxEvent();
    }

    /// <summary>
    /// 关闭近战攻击碰撞盒，通常由攻击动画事件调用。
    /// </summary>
    void DisableAtk()
    {
        // 近战攻击动画事件：关闭攻击 Trigger Collider。
        if (atkCollider != null)
        {
            atkCollider.enabled = false;
        }
    }

    // ---------- 死亡状态 ----------
    private void DoDie()
    {
        // 死亡状态只需要执行一次死亡逻辑，所以用 isDie 防止重复触发。
        StopHitAnimationLock();     // 停止受击动画锁定

        if (animator != null)
        {
            animator.SetBool("Move", false);
        }

        if (!isDie)     // 首次进入死亡状态
        {
            if (animator != null)
            {
                animator.SetTrigger("Die");
            }

            isDie = true;
            DisableAtk();   // 关闭攻击碰撞体

            if (cc != null)
            {
                cc.enabled = false; // 禁用角色控制器，防止死亡后仍被推动
            }

            ScheduleDestroy(); // 安排延迟销毁
            Reward();
        }
    }

    /// <summary>
    /// 怪物死亡时给玩家发放经验。
    /// </summary>
    private void Reward()
    {
        GameplayRuntime.Instance.AddExpToCurrentPlayer(Exp);
    }

    /// <summary>
    /// 按金库击破次数刷新怪物数值。
    /// preserveHealthPercent 为 true 时，场上已有怪物保留当前血量百分比，只提升血量上限。
    /// </summary>
    public void ApplyVaultDifficulty(int destroyedVaultCount, bool preserveHealthPercent = true)
    {
        // 金库击破次数越多，怪物越强。
        // 注意：这里按“基础数值 * 倍率”算，避免重复乘导致数值爆炸。
        if (isDie || enemyState == EnemyState.Die)
        {
            return;
        }

        CacheBaseStats();
        destroyedVaultCount = Mathf.Max(0, destroyedVaultCount);

        float hpMultiplier = GameConfig.instance != null
            ? GameConfig.instance.GetMonsterHpMultiplier(destroyedVaultCount)
            : Mathf.Pow(1.1f, destroyedVaultCount);
        float atkMultiplier = GameConfig.instance != null
            ? GameConfig.instance.GetMonsterAtkMultiplier(destroyedVaultCount)
            : Mathf.Pow(1.1f, destroyedVaultCount);
        float expMultiplier = GameConfig.instance != null
            ? GameConfig.instance.GetMonsterExpMultiplier(destroyedVaultCount)
            : 1f + 0.05f * destroyedVaultCount;

        // 先记住旧血量百分比，再按基础值重新计算新数值。
        float hpPercent = HpMax > 0 ? Mathf.Clamp01((float)Hp / HpMax) : 1f;

        // 按基础值重新计算，而不是拿当前值继续乘。
        HpMax = Mathf.Max(1, Mathf.RoundToInt(baseHpMax * hpMultiplier));
        AtkPower = Mathf.Max(0, Mathf.RoundToInt(baseAtkPower * atkMultiplier));
        Exp = Mathf.Max(0, Mathf.RoundToInt(baseExp * expMultiplier));

        if (preserveHealthPercent && appliedVaultDestroyedCount >= 0)
        {
            Hp = Mathf.Clamp(Mathf.CeilToInt(HpMax * hpPercent), 1, HpMax);
        }
        else
        {
            Hp = HpMax;
        }

        appliedVaultDestroyedCount = destroyedVaultCount;
        UpDateHpBar();
    }

    /// <summary>
    /// 金库被击破后刷新本怪物的难度倍率。
    /// </summary>
    private void HandleVaultDestroyed(BoxCo vault)
    {
        // BoxCo.DestroyedCount 已经是击破后的次数，所以这里直接拿来做难度层级。
        int destroyedVaultCount = vault != null ? vault.DestroyedCount : GetCurrentVaultDestroyedCount();
        ApplyVaultDifficulty(destroyedVaultCount, true);
    }

    /// <summary>
    /// 获取当前全局金库击破次数。
    /// </summary>
    private int GetCurrentVaultDestroyedCount()
    {
        return GameplayRuntime.Instance.CurrentVaultDestroyedCount;
    }

    /// <summary>
    /// 缓存怪物原始生命、攻击和经验，用于按难度重新计算。
    /// </summary>
    private void CacheBaseStats()
    {
        // 只缓存一次预制体基础值。后面难度刷新都用这份基础值。
        if (baseStatsCached)
        {
            return;
        }

        // HpMax 没配时用 Hp 兜底，保证旧预制体也能得到一个可靠基础生命值。
        baseHpMax = Mathf.Max(1, HpMax > 0 ? HpMax : Hp);
        baseAtkPower = Mathf.Max(0, AtkPower);
        baseExp = Mathf.Max(0, Exp);
        baseStatsCached = true;
    }



    // ---------- 受击接口（实现 FighterInterface） ----------
    public void Hit(int AtkPower)
    {
        // 玩家武器命中时会走到这里。
        // 如果已经死亡或生命值归零，忽略本次打击
        if (isDie || enemyState == EnemyState.Die || Hp <= 0)
        {
            return;
        }

        PlayHitSfxEvent();
        HitColorChange();   // 显示受击颜色

        // 扣除传入的攻击力。
        Hp-=AtkPower;
        UpDateHpBar();
        //Debug.Log("Slime hit, hp: " + Hp);

        if (Hp <= 0)    // 生命归零 -> 死亡
        {
            Hp = 0;
            enemyState = EnemyState.Die;
            DoDie();
            return;
        }

        TryPlayHitAnimation();  // 尝试播放受击动画（有冷却限制）
    }

    // ---------- 动画事件：死亡完成时调用 ----------
    private void Dead()
    {
        ScheduleDestroy();
    }

    // ---------- 延迟销毁协程 ----------
    IEnumerator DestorySelf()
    {
        yield return new WaitForSeconds(destroyDelay);
        Destroy(gameObject);
    }

    /// <summary>
    /// 安排死亡后的延迟销毁，避免重复启动销毁协程。
    /// </summary>
    void ScheduleDestroy()
    {
        if (destroyScheduled)
        {
            return;
        }

        destroyScheduled = true;
        StartCoroutine(DestorySelf());
    }

    // ---------- 材质颜色变化逻辑 ----------
    /// <summary>
    /// 触发受击颜色变化（记录变化开始时间）
    /// </summary>
    void HitColorChange()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        isColorChange = true;
        changeTime = Time.time;
        ApplyHitMaterialState();
    }

    /// <summary>
    /// 每帧检查颜色变化是否结束，若超时则恢复默认
    /// </summary>
    void MakeColorDefault()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (isColorChange && Time.time - changeTime >= colorTime)
        {
            isColorChange = false;
            RestoreDefaultMaterialState();
        }
    }

    /// <summary>
    /// 缓存材质的默认颜色和纹理，用于恢复
    /// </summary>
    void CacheDefaultMaterialState()
    {
        // 检查是否使用支持多颜色通道的 Mask 着色器
        useMaskTintShader =
            runtimeMaterial.HasProperty(Color01Property) &&
            runtimeMaterial.HasProperty(Color02Property) &&
            runtimeMaterial.HasProperty(Color03Property);

        if (useMaskTintShader)
        {
            defaultTintColor01 = runtimeMaterial.GetColor(Color01Property);
            defaultTintColor02 = runtimeMaterial.GetColor(Color02Property);
            defaultTintColor03 = runtimeMaterial.GetColor(Color03Property);

            if (runtimeMaterial.HasProperty(AlbedoProperty))
            {
                defaultAlbedoTexture = runtimeMaterial.GetTexture(AlbedoProperty);
            }

            return;
        }

        // 普通着色器
        if (runtimeMaterial.HasProperty(ColorProperty))
        {
            defaultColor = runtimeMaterial.GetColor(ColorProperty);
        }

        if (runtimeMaterial.HasProperty(MainTexProperty))
        {
            defaultMainTexture = runtimeMaterial.GetTexture(MainTexProperty);
        }
    }

    /// <summary>
    /// 应用受击时的材质状态（颜色变红、纹理替换）
    /// </summary>
    void ApplyHitMaterialState()
    {
        if (useMaskTintShader)
        {
            runtimeMaterial.SetColor(Color01Property, hitColor);
            runtimeMaterial.SetColor(Color02Property, hitColor);
            runtimeMaterial.SetColor(Color03Property, hitColor);

            if (hitTt != null && runtimeMaterial.HasProperty(AlbedoProperty))
            {
                runtimeMaterial.SetTexture(AlbedoProperty, hitTt);
            }

            return;
        }

        if (runtimeMaterial.HasProperty(ColorProperty))
        {
            runtimeMaterial.SetColor(ColorProperty, hitColor);
        }

        if (runtimeMaterial.HasProperty(MainTexProperty))
        {
            runtimeMaterial.SetTexture(MainTexProperty, hitTt != null ? hitTt : Texture2D.whiteTexture);
        }
    }

    /// <summary>
    /// 恢复材质到默认状态
    /// </summary>
    void RestoreDefaultMaterialState()
    {
        if (useMaskTintShader)
        {
            runtimeMaterial.SetColor(Color01Property, defaultTintColor01);
            runtimeMaterial.SetColor(Color02Property, defaultTintColor02);
            runtimeMaterial.SetColor(Color03Property, defaultTintColor03);

            if (runtimeMaterial.HasProperty(AlbedoProperty))
            {
                runtimeMaterial.SetTexture(AlbedoProperty, defaultAlbedoTexture);
            }

            return;
        }

        if (runtimeMaterial.HasProperty(ColorProperty))
        {
            runtimeMaterial.SetColor(ColorProperty, defaultColor);
        }

        if (runtimeMaterial.HasProperty(MainTexProperty))
        {
            runtimeMaterial.SetTexture(MainTexProperty, defaultMainTexture);
        }
    }

    // ---------- 受击动画控制 ----------
    /// <summary>
    /// 尝试播放受击动画（有冷却限制，且若已在播放则忽略）
    /// </summary>
    void TryPlayHitAnimation()
    {
        if (animator == null || isHitAnimating || Time.time < nextHitAnimationAllowedTime)
        {
            return;
        }

        // 检查动画控制器是否存在受击触发器
        if (!HasAnimatorParameter(hitTriggerName, AnimatorControllerParameterType.Trigger))
        {
            Debug.LogWarning($"Slime Animator is missing hit trigger parameter: {hitTriggerName}", this);
            return;
        }

        stateBeforeHit = enemyState;    // 记录当前状态
        isHitAnimating = true;
        DisableAtk();                   // 受击时关闭攻击
        animator.SetBool("Move", false);
        // 重置所有攻击触发器，防止受击时插入攻击动画
        ResetAnimatorTriggerIfExists("Atk1");
        ResetAnimatorTriggerIfExists("Atk2");
        ResetAnimatorTriggerIfExists("Atk");
        animator.SetTrigger(hitTriggerName);

        // 启动协程等待受击动画播放完毕
        if (hitRecoverCoroutine != null)
        {
            StopCoroutine(hitRecoverCoroutine);
        }

        hitRecoverCoroutine = StartCoroutine(WaitForHitAnimationFinished());
    }

    /// <summary>
    /// 等待受击动画播放完毕的协程
    /// 先等待一个回退时间让动画状态机进入受击状态，然后持续监控直到退出受击状态或归一化时间 >=1
    /// </summary>
    IEnumerator WaitForHitAnimationFinished()
    {
        // 协程：等待受击动画真正开始并播放结束。
        // 这样史莱姆受击时不会一边挨打动画一边继续追击/攻击。
        float fallbackEndTime = Time.time + hitAnimationFallbackDuration;
        bool enteredHitState = false;

        // 第一阶段：等待动画状态机进入受击状态（最多等待 fallback 时间）
        while (Time.time < fallbackEndTime)
        {
            if (isDie || enemyState == EnemyState.Die || Hp <= 0)
            {
                hitRecoverCoroutine = null;
                yield break;
            }

            if (IsPlayingHitState())
            {
                enteredHitState = true;
                break;
            }

            yield return null;
        }

        if (enteredHitState)
        {
            // 第二阶段：持续监控直到动画播放完毕或退出受击状态
            while (true)
            {
                if (isDie || enemyState == EnemyState.Die || Hp <= 0)
                {
                    hitRecoverCoroutine = null;
                    yield break;
                }

                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                bool isCurrentHitState =
                    stateInfo.shortNameHash == hitStateShortHash ||
                    stateInfo.fullPathHash == hitStateFullPathHash;

                if (!isCurrentHitState) // 已退出受击状态
                {
                    break;
                }

                // 动画播放结束（normalizedTime>=1 且不在过渡中）
                if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(0))
                {
                    break;
                }

                yield return null;
            }
        }
        else
        {
            // 未能进入受击状态，等待回退时间后强制结束
            yield return new WaitForSeconds(hitAnimationFallbackDuration);
        }

        // 恢复标志和冷却
        isHitAnimating = false;
        hitRecoverCoroutine = null;
        nextHitAnimationAllowedTime = Time.time + hitAnimationCooldown;
        ResumeStateAfterHit();
    }

    /// <summary>
    /// 受击动画结束后恢复状态（根据距离重新判断追击/攻击/闲置）
    /// </summary>
    void ResumeStateAfterHit()
    {
        if (isDie || enemyState == EnemyState.Die || Hp <= 0)
        {
            enemyState = EnemyState.Die;
            DoDie();
            return;
        }

        if (target == null)
        {
            enemyState = stateBeforeHit == EnemyState.Patrol ? EnemyState.Patrol : EnemyState.Idle;
            return;
        }

        Vector3 currentTargetPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
        float curDistance = Vector3.Distance(currentTargetPosition, transform.position);

        if (curDistance <= atkDistance)
        {
            enemyState = EnemyState.Atk;
        }
        else if (curDistance <= checkDistance)
        {
            enemyState = EnemyState.Persuit;
        }
        else if (stateBeforeHit == EnemyState.Patrol)
        {
            enemyState = EnemyState.Patrol;
        }
        else
        {
            enemyState = EnemyState.Idle;
        }
    }

    /// <summary>
    /// 停止受击动画锁定（例如死亡时强制取消）
    /// </summary>
    void StopHitAnimationLock()
    {
        isHitAnimating = false;

        if (hitRecoverCoroutine != null)
        {
            StopCoroutine(hitRecoverCoroutine);
            hitRecoverCoroutine = null;
        }
    }

    /// <summary>
    /// 缓存受击动画状态的哈希值，便于快速比较
    /// </summary>
    void CacheHitAnimationHashes()
    {
        if (string.IsNullOrEmpty(hitStateName))
        {
            hitStateShortHash = 0;
            hitStateFullPathHash = 0;
            return;
        }

        hitStateShortHash = Animator.StringToHash(hitStateName);
        hitStateFullPathHash = Animator.StringToHash($"Base Layer.{hitStateName}");
    }

    /// <summary>
    /// 检查当前动画是否正在播放受击状态
    /// </summary>
    bool IsPlayingHitState()
    {
        if (animator == null || hitStateShortHash == 0)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return
            stateInfo.shortNameHash == hitStateShortHash ||
            stateInfo.fullPathHash == hitStateFullPathHash;
    }

    // ---------- 工具方法 ----------
    /// <summary>
    /// 检查动画控制器中是否存在指定类型和名称的参数
    /// </summary>
    bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == parameterType && parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 如果动画控制器中存在指定触发器，将其重置
    /// </summary>
    void ResetAnimatorTriggerIfExists(string triggerName)
    {
        if (HasAnimatorParameter(triggerName, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(triggerName);
        }
    }

    /// <summary>
    /// 确保史莱姆身上有播放音效用的 AudioSource。
    /// </summary>
    void EnsureAudioSource()
    {
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
    /// 安全播放一个怪物音效。
    /// </summary>
    bool PlayClip(AudioClip clip, float volume)
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
    /// 动画事件调用：播放近战攻击音效。
    /// </summary>
    public void PlayMeleeAttackSfxEvent()
    {
        PlayClip(meleeAttackClip, attackVolume);
    }

    /// <summary>
    /// 动画事件调用：播放远程发射音效。
    /// </summary>
    public void PlayRangedShootSfxEvent()
    {
        PlayClip(rangedShootClip, attackVolume);
    }

    /// <summary>
    /// 动画事件或受击流程调用：播放受击音效。
    /// </summary>
    public void PlayHitSfxEvent()
    {
        PlayClip(hitClip, hitVolume);
    }
}
