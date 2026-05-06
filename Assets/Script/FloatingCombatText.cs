using UnityEngine;

/// <summary>
/// 漂浮战斗文字。
/// 
/// 新手阅读顺序：
/// 1. 外部脚本不用提前在场景里摆 UI，也不用拖预制体，直接调用 ShowDamage / ShowHealing / ShowExperience / ShowTakenDamage / ShowMiss。
/// 2. Show 方法会在目标斜上方创建一个 TextMesh，也就是 Unity 自带的 3D 文字。
/// 3. Update 每帧让文字面朝摄像机、向上飘、逐渐透明，时间到了自动销毁。
/// </summary>
public class FloatingCombatText : MonoBehaviour
{
    // 暴击使用紫黑色。它和玩家受伤的普通红字区分更明显。
    private static readonly Color CriticalDamageColor = new Color(0.18f, 0f, 0.28f, 1f);

    // 文字存在多久。时间短一点，战斗时不会堆太多数字。
    private const float DefaultLifetime = 1f;

    // 文字每秒向上飘多少世界单位。
    private const float DefaultFloatSpeed = 0.9f;

    // 文字距离目标头顶再高一点，避免挡住血条或模型。
    private const float UpExtraOffset = 0.45f;

    // 文字在目标左右两边偏移多少。伤害和吸血会用相反方向。
    private const float SideExtraOffset = 0.55f;

    // TextMesh 的实际世界大小。想让数字更大/更小，优先调这个值。
    private const float CharacterSize = 0.08f;

    // FontSize 主要影响文字清晰度，CharacterSize 才主要影响世界大小。
    private const int FontSize = 96;

    // 单条漂浮文字的运行时状态。
    private TextMesh textMesh;
    private Camera targetCamera;
    private Color startColor;
    private float lifetime;
    private float floatSpeed;
    private float age;

    /// <summary>
    /// 显示玩家造成的伤害。
    /// 普通伤害用白色，暴击伤害用深红色。
    /// </summary>
    public static void ShowDamage(Transform target, Collider hitCollider, int damage, bool isCritical)
    {
        if (damage <= 0)
        {
            return;
        }

        Color textColor = isCritical ? CriticalDamageColor : Color.white;
        Show(target, hitCollider, damage.ToString(), textColor, -1f);
    }

    /// <summary>
    /// 显示玩家回血。
    /// 按需求：吸血和自然回血都显示在玩家头顶，前面带 + 号，颜色为绿色。
    /// </summary>
    public static void ShowHealing(Transform target, int healAmount)
    {
        if (healAmount <= 0)
        {
            return;
        }

        Show(target, null, "+" + healAmount, Color.green, 0f);
    }

    /// <summary>
    /// 显示玩家获得的经验。
    /// 按需求：黄色，显示在玩家头顶，格式类似 +10exp。
    /// </summary>
    public static void ShowExperience(Transform target, int expAmount)
    {
        if (expAmount <= 0)
        {
            return;
        }

        Show(target, null, "+" + expAmount + "exp", Color.yellow, 0f);
    }

    /// <summary>
    /// 显示玩家被敌人打掉的生命值。
    /// 按需求：红色，显示在玩家头顶，前面带 - 号。
    /// </summary>
    public static void ShowTakenDamage(Transform target, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        Show(target, null, "-" + damage, Color.red, 0f);
    }

    /// <summary>
    /// 显示闪避成功的 miss。
    /// 闪避发生在玩家身上，所以通常 target 传玩家自己的 transform。
    /// </summary>
    public static void ShowMiss(Transform target)
    {
        Show(target, null, "miss", Color.white, 0f);
    }

    /// <summary>
    /// 创建一条世界空间战斗文字，并初始化它的位置、颜色和内容。
    /// </summary>
    private static void Show(Transform target, Collider hitCollider, string content, Color color, float sideSign)
    {
        if (target == null || string.IsNullOrEmpty(content))
        {
            return;
        }

        // 新建一个普通 GameObject，再挂上本脚本和 TextMesh。
        // 这样不需要你手动做预制体，直接运行就能看到效果。
        GameObject textObject = new GameObject("FloatingCombatText");
        FloatingCombatText floatingText = textObject.AddComponent<FloatingCombatText>();
        TextMesh mesh = textObject.AddComponent<TextMesh>();

        floatingText.Initialize(mesh, color);
        mesh.text = content;

        textObject.transform.position = CalculateSpawnPosition(target, hitCollider, sideSign);
        floatingText.FaceCamera();
    }

    /// <summary>
    /// 初始化 TextMesh 的字号、颜色、对齐方式和生命周期参数。
    /// </summary>
    private void Initialize(TextMesh mesh, Color color)
    {
        textMesh = mesh;
        targetCamera = Camera.main;
        startColor = color;
        lifetime = DefaultLifetime;
        floatSpeed = DefaultFloatSpeed;
        age = 0f;

        // TextMesh 是世界空间文字，不依赖 Canvas。
        // anchor/alignment 都居中，数字飘动时视觉中心更稳定。
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = FontSize;
        textMesh.characterSize = CharacterSize;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.color = startColor;

        // 提高渲染顺序，减少文字被同类透明物体盖住的概率。
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            meshRenderer.sortingOrder = 100;
        }
    }

    /// <summary>
    /// 驱动文字向上飘、面向摄像机、逐渐淡出并按时销毁。
    /// </summary>
    private void Update()
    {
        age += Time.deltaTime;

        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 文字向上飘一点，玩家会感觉数字是从目标身上弹出来的。
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        // 摄像机可能在运行中切换，所以丢失时再找一次。
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        FaceCamera();
        FadeOut();
    }

    /// <summary>
    /// 让 3D 文字始终朝向主摄像机，避免从侧面看不清。
    /// </summary>
    private void FaceCamera()
    {
        if (targetCamera == null)
        {
            return;
        }

        // TextMesh 的正面和摄像机保持同样朝向时，玩家能看到文字正面。
        // 这样无论摄像机绕到哪里，数字都会始终朝向屏幕。
        transform.rotation = targetCamera.transform.rotation;
    }

    /// <summary>
    /// 根据存活时间调整透明度，实现逐渐消失的效果。
    /// </summary>
    private void FadeOut()
    {
        if (textMesh == null)
        {
            return;
        }

        // age / lifetime 从 0 变到 1；这里反过来当透明度，让文字慢慢消失。
        float alpha = 1f - Mathf.Clamp01(age / lifetime);
        Color fadedColor = startColor;
        fadedColor.a = alpha;
        textMesh.color = fadedColor;
    }

    /// <summary>
    /// 根据目标模型或碰撞体包围盒，计算漂浮文字的生成位置。
    /// </summary>
    private static Vector3 CalculateSpawnPosition(Transform target, Collider hitCollider, float sideSign)
    {
        Camera mainCamera = Camera.main;
        Vector3 cameraRight = mainCamera != null ? mainCamera.transform.right : Vector3.right;

        Bounds targetBounds;
        bool hasBounds = TryGetTargetBounds(target, hitCollider, out targetBounds);

        if (!hasBounds)
        {
            // 找不到模型/碰撞体时，用一个安全的默认位置兜底。
            Vector3 fallbackSideOffset = cameraRight.normalized * sideSign * SideExtraOffset;
            return target.position + Vector3.up * 1.8f + fallbackSideOffset;
        }

        // bounds.center 是目标包围盒中心，extents.y 是从中心到顶部的高度。
        // center + up * extents.y 就是目标头顶附近，再加一点 UpExtraOffset 让文字更清楚。
        Vector3 topPosition = targetBounds.center + Vector3.up * (targetBounds.extents.y + UpExtraOffset);

        // 目标越大，左右偏移也稍微大一点；小怪则保持最小偏移，避免数字贴到模型上。
        float horizontalExtent = Mathf.Max(targetBounds.extents.x, targetBounds.extents.z);
        float sideDistance = Mathf.Max(SideExtraOffset, horizontalExtent + 0.2f);
        Vector3 sideOffset = cameraRight.normalized * sideSign * sideDistance;

        return topPosition + sideOffset;
    }

    /// <summary>
    /// 尝试从 Renderer 或 Collider 里合并出目标整体包围盒。
    /// </summary>
    private static bool TryGetTargetBounds(Transform target, Collider hitCollider, out Bounds targetBounds)
    {
        // 优先用 Renderer，因为它更接近玩家实际看到的模型大小。
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        targetBounds = new Bounds(target.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null || !currentRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                targetBounds = currentRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                targetBounds.Encapsulate(currentRenderer.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        // 没有 Renderer 时再看碰撞体。传进来的 hitCollider 通常就是这次被打中的碰撞体。
        if (hitCollider != null)
        {
            targetBounds = hitCollider.bounds;
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider currentCollider = colliders[i];
            if (currentCollider == null || !currentCollider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                targetBounds = currentCollider.bounds;
                hasBounds = true;
            }
            else
            {
                targetBounds.Encapsulate(currentCollider.bounds);
            }
        }

        return hasBounds;
    }
}
