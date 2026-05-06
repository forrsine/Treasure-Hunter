using UnityEngine;

/// <summary>
/// 第三人称摄像机控制器。
/// 
/// 新手阅读顺序：
/// 1. Update 读取鼠标输入，计算镜头角度和缩放距离。
/// 2. LateUpdate 在玩家移动后再更新摄像机位置，减少画面抖动。
/// 3. ResolveCameraPosition 用球形射线检测墙体，防止镜头穿墙。
/// 4. ShouldIgnoreHit 排除玩家、怪物、子弹、金库等不该挡镜头的物体。
/// </summary>
public class CameraCo : MonoBehaviour
{
    [Header("Target")]
    // 摄像机围绕的目标，一般拖玩家 Transform。
    public Transform target;

    [Header("Rotation")]
    // 鼠标横向/纵向移动转成镜头旋转的速度。
    [SerializeField] private float xSpeed = 200f;
    [SerializeField] private float ySpeed = 125f;

    // 当前镜头角度。x 是绕 Y 轴的水平角，y 是上下俯仰角。
    public float x = 20f;
    public float y = 0f;

    // 限制上下看角度，避免镜头翻过去。
    public float yMinLimit = -10f;
    public float yMaxlimit = 70f;

    [Header("Distance")]
    // 摄像机离目标多远，滚轮会改变这个值。
    [SerializeField] private float distance = 4f;
    [SerializeField] private float zoomRate = 80f;

    // 镜头最近/最远距离。
    public float disMinLimit = 2f;
    [SerializeField] private float disMaxLimit = 10f;

    [Header("Offset")]
    // 目标点偏移：让镜头看向玩家胸口/头部附近，而不是脚底。
    public Vector3 offset = new Vector3(0f, 1f, 0f);

    // 在目标点后方的方向。默认 z = -1 表示站在目标后面。
    public Vector3 rotateOffset = new Vector3(0f, 0f, -1f);

    [Header("Collision")]
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionBuffer = 0.1f;
    [SerializeField] private float minCollisionDistance = 0.35f;
    [Tooltip("勾选后，摄像机避障会忽略带 BoxCo 的金库/箱子，解决箱子挡住视角导致镜头拉近的问题。")]
    [SerializeField] private bool ignoreBoxCollision = true;

    private void Start()
    {
        // 开局如果没有新手弹窗，就隐藏并锁定鼠标，让鼠标移动控制镜头。
        if (!GameplayStartupGuidePopup.IsRuntimePopupVisible)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 防止 Inspector 里填了超出范围的初始距离。
        distance = Mathf.Clamp(distance, disMinLimit, disMaxLimit);
    }

    /// <summary>
    /// 读取鼠标输入，更新镜头角度、玩家朝向和缩放距离。
    /// </summary>
    private void Update()
    {
        if (target == null || InputCo.Instance == null)
        {
            return;
        }

        if (GameplayStartupGuidePopup.IsRuntimePopupVisible)
        {
            return;
        }

        // 鼠标 X 控制水平旋转，鼠标 Y 控制上下俯仰。
        x += InputCo.Instance.MouseInput.x * xSpeed * Time.deltaTime;
        y -= InputCo.Instance.MouseInput.y * ySpeed * Time.deltaTime;
        y = ClampAngle(y, yMinLimit, yMaxlimit);

        // 玩家有移动输入时，让玩家朝向跟随摄像机水平角。
        if (Mathf.Abs(InputCo.Instance.Xinput) > 0.01f || Mathf.Abs(InputCo.Instance.Yinput) > 0.01f)
        {
            target.rotation = Quaternion.Euler(0f, x, 0f);
        }

        // 鼠标滚轮控制远近。乘以当前 distance 可以让远处缩放更快、近处更细腻。
        distance -= (InputCo.Instance.MouseInput.z * Time.deltaTime) * zoomRate * Mathf.Abs(distance);
        distance = Mathf.Clamp(distance, disMinLimit, disMaxLimit);
    }

    /// <summary>
    /// 在所有移动逻辑之后摆放摄像机，避免跟随目标时画面抖动。
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (GameplayStartupGuidePopup.IsRuntimePopupVisible)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(y, x, 0f);
        transform.rotation = rotation;

        // pivotPosition 是镜头围绕的中心点。
        Vector3 pivotPosition = target.position + offset;

        // desiredPosition 是“没有障碍物时”摄像机想去的位置。
        Vector3 desiredOffset = rotation * (rotateOffset * distance);
        Vector3 desiredPosition = pivotPosition + desiredOffset;

        transform.position = ResolveCameraPosition(pivotPosition, desiredPosition);
    }

    /// <summary>
    /// 根据玩家到镜头之间的障碍物，计算不会穿墙的最终摄像机位置。
    /// </summary>
    private Vector3 ResolveCameraPosition(Vector3 pivotPosition, Vector3 desiredPosition)
    {
        Vector3 castVector = desiredPosition - pivotPosition;
        float castDistance = castVector.magnitude;
        if (castDistance <= Mathf.Epsilon)
        {
            return pivotPosition;
        }

        Vector3 castDirection = castVector / castDistance;

        // SphereCastAll 像拿一个小球从玩家身边扫到镜头位置，能比普通射线更不容易穿边角。
        RaycastHit[] hits = Physics.SphereCastAll(
            pivotPosition,
            collisionRadius,
            castDirection,
            castDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = castDistance;
        bool hasObstacle = false;

        for (int i = 0; i < hits.Length; i++)
        {
            // 有些对象虽然会被射线扫到，但不应该把镜头往前推。
            if (ShouldIgnoreHit(hits[i].transform))
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                hasObstacle = true;
            }
        }

        if (!hasObstacle)
        {
            return desiredPosition;
        }

        // 有障碍物时，把摄像机放到障碍物前面一点点，collisionBuffer 是安全距离。
        float safeDistance = Mathf.Clamp(
            nearestDistance - collisionBuffer,
            minCollisionDistance,
            castDistance);

        return pivotPosition + castDirection * safeDistance;
    }

    /// <summary>
    /// 判断某个 SphereCast 命中的对象是否不应该阻挡摄像机。
    /// </summary>
    private bool ShouldIgnoreHit(Transform hitTransform)
    {
        if (hitTransform == null || target == null)
        {
            return false;
        }

        if (hitTransform.root == target.root)
        {
            return true;
        }

        if (hitTransform.GetComponentInParent<SlimeCo>() != null)
        {
            return true;
        }

        if (hitTransform.GetComponentInParent<BulletCo>() != null)
        {
            return true;
        }

        if (hitTransform.GetComponentInParent<PlayerCo>() != null)
        {
            return true;
        }

        // 金库是玩家持续攻击的目标，但不应该像墙体一样把第三人称镜头顶回玩家身边。
        if (ignoreBoxCollision && hitTransform.GetComponentInParent<BoxCo>() != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 把角度限制到给定范围，并避免角度值无限累加。
    /// </summary>
    private float ClampAngle(float angle, float min, float max)
    {
        // 角度过大时先绕回 -360 到 360 范围，避免数字无限增长。
        if (angle > 360f)
        {
            angle -= 360f;
        }
        else if (angle < -360f)
        {
            angle += 360f;
        }

        return Mathf.Clamp(angle, min, max);
    }
}
