using UnityEngine;

/// <summary>
/// 子弹行为控制器：管理子弹的移动、生命周期、碰撞检测与销毁。
/// 
/// 新手阅读顺序：
/// 1. SlimeCo.Shoot 生成子弹，并调用 Initialize 设置伤害、速度、寿命。
/// 2. FixedUpdate 每个物理帧推动子弹向前飞。
/// 3. OnTriggerEnter 碰到玩家后调用 PlayerCo.Hit 扣血。
/// 4. 子弹命中或寿命结束都会 Destroy 自己。
/// </summary>
public class BulletCo : MonoBehaviour
{
    [SerializeField] float speed = 8f;          // 子弹移动速度（可在Inspector中调整）
    [SerializeField] float lifeTime = 2f;       // 子弹存活时间（秒），超时自动销毁

    Rigidbody rb;                               // 引用子弹上的刚体组件
    Transform ownerRoot;                        // 发射者的根Transform（用于避免击中发射者自身）
    float lifeTimer;                            // 当前存活倒计时
    bool hasHit;                                // 是否已经命中目标（防止重复触发）

    [SerializeField] int damage;                // 当前子弹实际伤害；由 Initialize 传入

    // 兼容旧用法：如果旧预制体没有传 damage，但拖了 slime，就从 slime.AtkPower 兜底取伤害。
    public SlimeCo slime;

    /// <summary>
    /// 缓存并配置 Rigidbody，保证子弹一生成就能按物理帧移动。
    /// </summary>
    void Awake()
    {
        // 在脚本唤醒时确保刚体存在
        EnsureRigidbody();
    }

    /// <summary>
    /// 每次启用时重置运行状态，兼容以后改成对象池复用。
    /// </summary>
    void OnEnable()
    {
        // 每次从对象池启用时，重置生命计时器和命中状态
        lifeTimer = lifeTime;
        hasHit = false;
    }

    /// <summary>
    /// 初始化子弹（通常由发射者调用，用于设置发射者、速度与生命时长）
    /// </summary>
    /// <param name="owner">发射者的Transform</param>
    /// <param name="bulletSpeed">子弹速度</param>
    /// <param name="bulletLifeTime">子弹存活时间</param>
    public void Initialize(Transform owner, float bulletSpeed, float bulletLifeTime)
    {
        // 老版本只传速度和寿命，所以这里继续支持；伤害使用 Inspector 里的 damage。
        Initialize(owner, damage, bulletSpeed, bulletLifeTime);
    }

    /// <summary>
    /// 初始化子弹的完整版本。
    /// owner 用来避免打到发射者自己，bulletDamage 是本次伤害。
    /// </summary>
    public void Initialize(Transform owner, int bulletDamage, float bulletSpeed, float bulletLifeTime)
    {
        // 记录发射者的根物体（避免子弹碰触到发射者自己）
        ownerRoot = owner != null ? owner.root : null;
        damage = Mathf.Max(0, bulletDamage);
        speed = bulletSpeed;
        lifeTime = bulletLifeTime;
        lifeTimer = lifeTime;
        hasHit = false;
        // 再次确保刚体存在（可能在生成时组件被意外移除）
        EnsureRigidbody();
    }

    /// <summary>
    /// 处理子弹寿命倒计时，超时自动销毁。
    /// </summary>
    void Update()
    {
        // 如果已经命中，不再执行任何逻辑（等待销毁）
        if (hasHit)
        {
            return;
        }

        // 递减寿命计时器，若计时结束则销毁子弹
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 在物理帧里推进子弹位置，减少高速碰撞穿透概率。
    /// </summary>
    void FixedUpdate()
    {
        // 如果已经命中，不再移动
        if (hasHit)
        {
            return;
        }

        // 计算下一帧的位置：沿子弹前方向量 * 速度 * 物理帧时间
        Vector3 nextPosition = transform.position + transform.forward * speed * Time.fixedDeltaTime;

        if (rb != null)
        {
            // 使用刚体的 MovePosition 实现平滑的物理移动（推荐用于Kinematic刚体）
            rb.MovePosition(nextPosition);
        }
        else
        {
            // 若无刚体，直接修改 Transform 位置（后备方案）
            transform.position = nextPosition;
        }
    }

    /// <summary>
    /// 命中玩家时结算伤害，并过滤发射者自身。
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // 如果已经命中过，忽略后续碰撞
        if (hasHit)
        {
            return;
        }

        // 如果碰撞物是发射者自身的任何部分，则忽略（避免误伤自己）
        if (ownerRoot != null && other.transform.root == ownerRoot)
        {
            return;
        }

        // 尝试在碰撞物或其父级上获取 PlayerCo 组件（玩家脚本）
        PlayerCo player = other.GetComponentInParent<PlayerCo>();
        if (player == null)
        {
            return; // 不是玩家，忽略
        }

        // 标记已命中，调用玩家受伤方法，然后销毁子弹
        hasHit = true;
        int finalDamage = damage;

        // 如果没有通过 Initialize 设置伤害，就尝试用旧字段 slime 兜底。
        if (finalDamage <= 0 && slime != null)
        {
            finalDamage = slime.AtkPower;
        }

        if (finalDamage > 0)
        {
            player.Hit(finalDamage);
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// 确保子弹上挂载一个刚体组件，并设置为运动学（Kinematic）、禁用重力、启用连续碰撞检测与插值。
    /// </summary>
    void EnsureRigidbody()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb == null)
        {
            // 如果没有刚体，则添加一个
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // 配置刚体属性
        rb.useGravity = false;                              // 子弹不受重力影响
        rb.isKinematic = true;                              // 使用运动学刚体（位置由脚本控制）
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // 连续碰撞检测（防止高速穿透）
        rb.interpolation = RigidbodyInterpolation.Interpolate; // 开启插值，使移动更平滑
    }
}
