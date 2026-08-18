using UnityEngine;

/// <summary>
/// 技能线稿特效：用 LineRenderer 生成简单技能范围表现。
/// 优点是不依赖美术资源，适合第一版快速展示技能范围和释放反馈。
/// </summary>
public sealed class SkillLineEffect : MonoBehaviour
{
    private const int CircleSegments = 72;
    private static Material lineMaterial;

    private LineRenderer[] lines;
    private float lifeTime = 1f;
    private float timer;
    private float rotateSpeed;
    private Vector3 startScale;
    private Vector3 endScale;
    private Color effectColor;

    public static void PlayFireballExplosion(Vector3 position, float radius)
    {
        GameObject root = new GameObject("VFX_FireballExplosion_Line");
        root.transform.position = position;

        Color color = new Color(1f, 0.35f, 0.05f, 1f);
        AddCircle(root.transform, radius, color, 0.08f, Quaternion.identity);
        AddCircle(root.transform, radius * 0.75f, color, 0.05f, Quaternion.Euler(90f, 0f, 0f));
        AddCircle(root.transform, radius * 0.5f, color, 0.04f, Quaternion.Euler(0f, 0f, 90f));

        SkillLineEffect effect = root.AddComponent<SkillLineEffect>();
        effect.Initialize(0.45f, 180f, Vector3.one * 0.35f, Vector3.one * 1.25f, color);
    }

    /// <summary>
    /// 创建大火球飞行中的线稿表现。
    /// 注意：这里只创建表现对象，不负责移动、不负责伤害。
    /// 移动和命中由 PlayerSkillCastComponent 的协程控制。
    /// </summary>
    public static GameObject CreateFireballProjectile(Vector3 position, float radius)
    {
        GameObject root = new GameObject("VFX_FireballProjectile_Line");
        root.transform.position = position;

        Color color = new Color(1f, 0.45f, 0.05f, 1f);

        // 三个不同方向的小圆圈叠在一起，看起来像一个线稿能量球。
        AddCircle(root.transform, radius, color, 0.05f, Quaternion.identity);
        AddCircle(root.transform, radius * 0.9f, color, 0.04f, Quaternion.Euler(90f, 0f, 0f));
        AddCircle(root.transform, radius * 0.75f, color, 0.04f, Quaternion.Euler(0f, 0f, 90f));

        return root;
    }

    public static void PlayPoisonArea(Vector3 position, float radius, float duration)
    {
        GameObject root = new GameObject("VFX_PoisonArea_Line");
        root.transform.position = position + Vector3.up * 0.05f;

        Color color = new Color(0.2f, 1f, 0.25f, 0.9f);
        AddCircle(root.transform, radius, color, 0.06f, Quaternion.identity);
        AddCircle(root.transform, radius * 0.72f, color, 0.03f, Quaternion.identity);

        SkillLineEffect effect = root.AddComponent<SkillLineEffect>();
        effect.Initialize(duration, 35f, Vector3.one, Vector3.one, color);
    }

    public static void PlayScytheSpin(Vector3 position, float radius)
    {
        GameObject root = new GameObject("VFX_ScytheSpin_Line");
        root.transform.position = position + Vector3.up * 0.08f;

        Color color = new Color(1f, 0.9f, 0.1f, 1f);
        AddArc(root.transform, radius, color, 0.09f, -35f, 145f);
        AddArc(root.transform, radius * 0.82f, color, 0.06f, 145f, 325f);

        SkillLineEffect effect = root.AddComponent<SkillLineEffect>();
        effect.Initialize(0.35f, 720f, Vector3.one * 0.8f, Vector3.one * 1.15f, color);
    }

    private void Initialize(float lifeTime, float rotateSpeed, Vector3 startScale, Vector3 endScale, Color color)
    {
        this.lifeTime = Mathf.Max(0.05f, lifeTime);
        this.rotateSpeed = rotateSpeed;
        this.startScale = startScale;
        this.endScale = endScale;
        effectColor = color;
        lines = GetComponentsInChildren<LineRenderer>();
        transform.localScale = startScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / lifeTime);

        transform.localScale = Vector3.Lerp(startScale, endScale, progress);
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        SetAlpha(effectColor.a * (1f - progress));

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void SetAlpha(float alpha)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
            {
                continue;
            }

            Color color = effectColor;
            color.a = alpha;
            lines[i].startColor = color;
            lines[i].endColor = color;
        }
    }

    private static void AddCircle(Transform parent, float radius, Color color, float width, Quaternion localRotation)
    {
        LineRenderer line = CreateLine(parent, "CircleLine", color, width, localRotation);
        line.positionCount = CircleSegments + 1;

        for (int i = 0; i <= CircleSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / CircleSegments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private static void AddArc(Transform parent, float radius, Color color, float width, float startAngle, float endAngle)
    {
        int segmentCount = 36;
        LineRenderer line = CreateLine(parent, "ArcLine", color, width, Quaternion.identity);
        line.positionCount = segmentCount + 1;

        for (int i = 0; i <= segmentCount; i++)
        {
            float t = i / (float)segmentCount;
            float angle = Mathf.Lerp(startAngle, endAngle, t) * Mathf.Deg2Rad;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private static LineRenderer CreateLine(Transform parent, string name, Color color, float width, Quaternion localRotation)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent);
        lineObject.transform.localPosition = Vector3.zero;
        lineObject.transform.localRotation = localRotation;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.material = GetLineMaterial();
        line.startColor = color;
        line.endColor = color;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        return line;
    }

    private static Material GetLineMaterial()
    {
        if (lineMaterial == null)
        {
            lineMaterial = new Material(Shader.Find("Sprites/Default"));
        }

        return lineMaterial;
    }
}