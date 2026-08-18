using System.Collections;
using UnityEngine;

/// <summary>
/// Boss 胜利传送门生成器：监听 Boss 死亡事件，在 Boss 房间生成返回主场景的传送门。
/// 它只负责“胜利后开门”，真正的触发切场景逻辑仍然交给 BossScenePortal，避免重复写传送代码。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossVictoryPortalSpawner : MonoBehaviour
{
    [Header("Boss 引用")]
    [SerializeField] private SpiderKingBossController boss;

    [Header("返回传送门")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private float spawnDelay = 0f;
    [SerializeField] private float distanceFromBoss = 4f;
    [SerializeField] private Vector3 portalPositionOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private Vector3 generatedPortalScale = new Vector3(1.8f, 2.8f, 1.8f);
    [SerializeField] private Color portalColor = new Color(1f, 0.22f, 0.72f, 0.72f);
    [SerializeField] private Color portalEmissionColor = new Color(1f, 0.08f, 0.85f, 1f);
    [SerializeField] private float portalEmissionIntensity = 1.8f;

    private GameObject spawnedPortal;
    private Coroutine spawnRoutine;
    private bool bossEventRegistered;

    private void OnEnable()
    {
        TryAutoBindBoss();
        RegisterBossEventIfNeeded();
    }

    private void Start()
    {
        TryAutoBindBoss();
        RegisterBossEventIfNeeded();
    }

    private void OnDisable()
    {
        UnregisterBossEventIfNeeded();
    }

    private void OnValidate()
    {
        spawnDelay = Mathf.Max(0f, spawnDelay);
        distanceFromBoss = Mathf.Max(0.5f, distanceFromBoss);
        generatedPortalScale.x = Mathf.Max(0.1f, generatedPortalScale.x);
        generatedPortalScale.y = Mathf.Max(0.1f, generatedPortalScale.y);
        generatedPortalScale.z = Mathf.Max(0.1f, generatedPortalScale.z);
        portalEmissionIntensity = Mathf.Max(0f, portalEmissionIntensity);
    }

    /// <summary>
    /// BossRoomSceneBootstrap 或编辑器工具生成 Boss 后主动绑定，避免运行时靠名字查找。
    /// </summary>
    public void BindBoss(SpiderKingBossController newBoss)
    {
        if (boss == newBoss)
        {
            RegisterBossEventIfNeeded();
            return;
        }

        UnregisterBossEventIfNeeded();
        boss = newBoss;
        RegisterBossEventIfNeeded();

        if (boss != null && boss.IsDeathSequenceFinished)
        {
            SpawnReturnPortal();
        }
    }

    private void TryAutoBindBoss()
    {
        if (boss != null)
        {
            return;
        }

        boss = FindObjectOfType<SpiderKingBossController>();
    }

    private void RegisterBossEventIfNeeded()
    {
        if (boss == null || bossEventRegistered)
        {
            return;
        }

        boss.BossDied += HandleBossDied;
        bossEventRegistered = true;
    }

    private void UnregisterBossEventIfNeeded()
    {
        if (boss == null || !bossEventRegistered)
        {
            bossEventRegistered = false;
            return;
        }

        boss.BossDied -= HandleBossDied;
        bossEventRegistered = false;
    }

    private void HandleBossDied(SpiderKingBossController _)
    {
        if (spawnedPortal != null || spawnRoutine != null)
        {
            return;
        }

        BossRunProgressState.MarkBossDefeated();
        spawnRoutine = StartCoroutine(SpawnReturnPortalAfterDelay());
    }

    private IEnumerator SpawnReturnPortalAfterDelay()
    {
        // 使用真实时间等待，避免以后又接入暂停 UI 时把返回传送门卡住。
        yield return new WaitForSecondsRealtime(spawnDelay);
        spawnRoutine = null;
        SpawnReturnPortal();
    }

    private void SpawnReturnPortal()
    {
        if (spawnedPortal != null)
        {
            return;
        }

        Vector3 position = CalculatePortalPosition();
        Quaternion rotation = CalculatePortalRotation(position);

        spawnedPortal = portalPrefab != null
            ? Instantiate(portalPrefab, position, rotation)
            : CreateGeneratedPortal(position, rotation);

        spawnedPortal.name = "ReturnToMainScenePortal";
        ConfigurePortal(spawnedPortal);
    }

    private Vector3 CalculatePortalPosition()
    {
        Vector3 bossPosition = boss != null ? boss.transform.position : transform.position;
        Vector3 horizontalDirection = Vector3.zero;

        PlayerRuntimeController player = GameplayRuntime.Instance.CurrentPlayer;
        if (player != null)
        {
            horizontalDirection = bossPosition - player.transform.position;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude < 0.01f && boss != null)
        {
            horizontalDirection = -boss.transform.forward;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude < 0.01f)
        {
            horizontalDirection = Vector3.back;
        }

        Vector3 rawPosition = bossPosition +
                              horizontalDirection.normalized * distanceFromBoss +
                              portalPositionOffset;

        return ClampPortalPositionInsideArena(rawPosition);
    }

    /// <summary>
    /// 把胜利传送门限制在 Boss 房间内部。
    /// Boss 如果死在墙边，单纯按方向偏移可能把门刷进墙里；这里用默认灰盒尺寸做安全夹取。
    /// </summary>
    private Vector3 ClampPortalPositionInsideArena(Vector3 position)
    {
        float safeMargin = Mathf.Max(2.2f, generatedPortalScale.x);
        float halfWidth = BossRoomSceneBootstrap.DefaultArenaWidth * 0.5f - safeMargin;
        float halfLength = BossRoomSceneBootstrap.DefaultArenaLength * 0.5f - safeMargin;

        if (halfWidth > 0f)
        {
            position.x = Mathf.Clamp(position.x, -halfWidth, halfWidth);
        }

        if (halfLength > 0f)
        {
            position.z = Mathf.Clamp(position.z, -halfLength, halfLength);
        }

        // 传送门要贴近地面生成，避免受 Boss 死亡姿态或异常高度影响刷到空中。
        position.y = portalPositionOffset.y;
        return position;
    }

    private Quaternion CalculatePortalRotation(Vector3 portalPosition)
    {
        PlayerRuntimeController player = GameplayRuntime.Instance.CurrentPlayer;
        if (player == null)
        {
            return Quaternion.identity;
        }

        Vector3 lookDirection = player.transform.position - portalPosition;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude < 0.01f)
        {
            return Quaternion.identity;
        }

        return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private GameObject CreateGeneratedPortal(Vector3 position, Quaternion rotation)
    {
        GameObject portalRoot = new GameObject("GeneratedReturnPortal");
        portalRoot.transform.SetPositionAndRotation(position, rotation);

        SphereCollider trigger = portalRoot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.8f;
        trigger.center = new Vector3(0f, 1.2f, 0f);

        Rigidbody rb = portalRoot.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "PortalVisual";
        visual.transform.SetParent(portalRoot.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        visual.transform.localScale = generatedPortalScale;

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.color = portalColor;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", portalEmissionColor * portalEmissionIntensity);
            renderer.material = material;
        }

        Light light = portalRoot.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = portalEmissionColor;
        light.range = 6f;
        light.intensity = 3f;

        return portalRoot;
    }

    private void ConfigurePortal(GameObject portalRoot)
    {
        if (portalRoot == null)
        {
            return;
        }

        BossScenePortal portal = portalRoot.GetComponent<BossScenePortal>();
        if (portal == null)
        {
            portal = portalRoot.AddComponent<BossScenePortal>();
        }

        portal.ConfigureTargetScene(GameSceneNames.GameplayScene, true);

        Transform visual = portalRoot.transform.Find("PortalVisual");
        if (visual != null)
        {
            portal.BindVisualRoot(visual);
        }

        ApplyPortalTint(portalRoot);
    }

    private void ApplyPortalTint(GameObject portalRoot)
    {
        Renderer[] renderers = portalRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            Material[] materials = currentRenderer.materials;
            for (int j = 0; j < materials.Length; j++)
            {
                Material material = materials[j];
                if (material == null)
                {
                    continue;
                }

                if (material.HasProperty("_Color"))
                {
                    material.color = portalColor;
                }

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", portalEmissionColor * portalEmissionIntensity);
                }
            }
        }

        Light[] lights = portalRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null)
            {
                lights[i].color = portalEmissionColor;
            }
        }
    }
}
