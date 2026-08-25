using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 战士满蓄力旋转重斩的圆环表现。
/// 组件只负责显示与回收，不参与伤害判定；实际半径始终由战斗配置传入，避免表现和数值各写一份。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerChargedSpinEffect : MonoBehaviour
{
    private const string PoolKey = "PlayerChargedSpinEffect";
    private const int CircleSegments = 96;
    private const float EffectLifetime = 0.3f;
    private const float StartRadiusRatio = 0.35f;
    private static readonly Color GoldColor = new Color(1f, 0.835f, 0.29f, 0.95f);

    private static Material sharedLineMaterial;

    private LineRenderer outerRing;
    private LineRenderer innerRing;
    private float timer;
    private float radius;
    private bool isPlaying;
    private bool rentedFromPool;

    public float Radius => radius;

    /// <summary>
    /// 从公共技能表现池取得圆环；场景没有池时使用一次性对象兜底，保证伤害逻辑不依赖表现对象。
    /// </summary>
    public static PlayerChargedSpinEffect Play(Vector3 position, float radius)
    {
        float safeRadius = Mathf.Max(0f, radius);
        if (safeRadius <= 0f)
        {
            return null;
        }

        SkillVisualPool visualPool = SkillVisualPool.Instance;
        GameObject effectObject = visualPool != null
            ? visualPool.GetEffectObject(PoolKey)
            : new GameObject(PoolKey);
        PlayerChargedSpinEffect effect = effectObject.GetComponent<PlayerChargedSpinEffect>();
        if (effect == null)
        {
            effect = effectObject.AddComponent<PlayerChargedSpinEffect>();
        }

        effect.Begin(position, safeRadius, visualPool != null);
        return effect;
    }

    private void Awake()
    {
        EnsureVisuals();
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / EffectLifetime);
        // SmoothStep 让圆环先快速展开、末段减速，命中瞬间的力度会比匀速缩放更明显。
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        float visualRadius = Mathf.Lerp(radius * StartRadiusRatio, radius, easedProgress);
        transform.localScale = Vector3.one * visualRadius;
        SetRingAlpha(GoldColor.a * (1f - progress));

        if (progress >= 1f)
        {
            Release();
        }
    }

    private void Begin(Vector3 position, float effectRadius, bool usePool)
    {
        EnsureVisuals();
        radius = effectRadius;
        timer = 0f;
        rentedFromPool = usePool;
        isPlaying = true;
        transform.SetParent(null, true);
        transform.SetPositionAndRotation(position + Vector3.up * 0.08f, Quaternion.identity);
        transform.localScale = Vector3.one * (radius * StartRadiusRatio);
        SetRingAlpha(GoldColor.a);
        gameObject.SetActive(true);
    }

    private void Release()
    {
        isPlaying = false;
        timer = 0f;
        transform.localScale = Vector3.one;

        SkillVisualPool visualPool = SkillVisualPool.Instance;
        if (rentedFromPool && visualPool != null)
        {
            visualPool.ReleaseVisual(PoolKey, gameObject);
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void EnsureVisuals()
    {
        if (outerRing == null)
        {
            outerRing = CreateRing("OuterRing", 1f, 0.045f);
        }

        if (innerRing == null)
        {
            innerRing = CreateRing("InnerRing", 0.78f, 0.025f);
        }
    }

    private LineRenderer CreateRing(string ringName, float normalizedRadius, float width)
    {
        GameObject ringObject = new GameObject(ringName);
        ringObject.transform.SetParent(transform, false);

        LineRenderer line = ringObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = CircleSegments;
        line.sharedMaterial = GetLineMaterial();
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = GoldColor;
        line.endColor = GoldColor;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 20;

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = Mathf.PI * 2f * i / CircleSegments;
            line.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * normalizedRadius,
                    0f,
                    Mathf.Sin(angle) * normalizedRadius));
        }

        return line;
    }

    private void SetRingAlpha(float alpha)
    {
        Color color = GoldColor;
        color.a = Mathf.Clamp01(alpha);
        SetLineColor(outerRing, color);
        SetLineColor(innerRing, color);
    }

    private static void SetLineColor(LineRenderer line, Color color)
    {
        if (line == null)
        {
            return;
        }

        line.startColor = color;
        line.endColor = color;
    }

    private static Material GetLineMaterial()
    {
        if (sharedLineMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Hidden/Internal-Colored");
            }

            if (shader != null)
            {
                sharedLineMaterial = new Material(shader)
                {
                    name = "PlayerChargedSpinEffect_Material"
                };
            }
        }

        return sharedLineMaterial;
    }
}
