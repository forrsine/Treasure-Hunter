using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Boss 入口解锁控制器：监听箱子击破次数，达到指定次数后清理普通怪、隐藏箱子并生成传送门。
/// 这个类只管理“从普通场景进入 Boss 场景”的流程，不处理 Boss 战斗逻辑。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossPortalUnlockController : MonoBehaviour
{
    private const string RuntimeObjectName = "BossPortalUnlockController";

    [Header("解锁条件")]
    [SerializeField] private int requiredVaultDestroyedCount = 5;

    [Header("传送门")]
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private Vector3 portalPositionOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private float portalHorizontalOffsetFromVault = 4f;
    [SerializeField] private Vector3 generatedPortalScale = new Vector3(1.8f, 2.8f, 1.8f);
    [SerializeField] private Color portalColor = new Color(1f, 0.22f, 0.72f, 0.72f);
    [SerializeField] private Color portalEmissionColor = new Color(1f, 0.08f, 0.85f, 1f);
    [SerializeField] private float portalEmissionIntensity = 1.8f;

    [Header("清场规则")]
    [SerializeField] private bool clearAliveMonsters = true;
    [SerializeField] private bool clearMonsterProjectiles = true;
    [SerializeField] private bool hideAllVaults = true;

    private bool portalUnlocked;
    private int destroyedVaultCount;
    private GameObject spawnedPortal;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterSceneLoadedCallback()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        InstallForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallForScene(scene);
    }

    /// <summary>
    /// 自动把控制器安装到 MainScene。
    /// 这样你不用手动往主场景拖脚本，后续如果要改参数，也可以自己在场景里放一个同名管理器覆盖默认值。
    /// </summary>
    private static void InstallForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != GameSceneNames.GameplayScene)
        {
            return;
        }

        if (FindObjectOfType<BossPortalUnlockController>() != null)
        {
            return;
        }

        GameObject controllerObject = new GameObject(RuntimeObjectName);
        controllerObject.AddComponent<BossPortalUnlockController>();
    }

    private void OnEnable()
    {
        BossRunProgressState.ConfigureVaultsPerBoss(requiredVaultDestroyedCount);
        BoxCo.OnVaultDestroyed += HandleVaultDestroyed;
    }

    private void Start()
    {
        BossRunProgressState.ConfigureVaultsPerBoss(requiredVaultDestroyedCount);
        RestoreMainSceneVaultProgress();
    }

    private void OnDisable()
    {
        BoxCo.OnVaultDestroyed -= HandleVaultDestroyed;
    }

    private void OnValidate()
    {
        requiredVaultDestroyedCount = Mathf.Max(1, requiredVaultDestroyedCount);
        portalHorizontalOffsetFromVault = Mathf.Max(0f, portalHorizontalOffsetFromVault);
        generatedPortalScale.x = Mathf.Max(0.1f, generatedPortalScale.x);
        generatedPortalScale.y = Mathf.Max(0.1f, generatedPortalScale.y);
        generatedPortalScale.z = Mathf.Max(0.1f, generatedPortalScale.z);
        portalEmissionIntensity = Mathf.Max(0f, portalEmissionIntensity);
    }

    private void HandleVaultDestroyed(BoxCo vault)
    {
        if (portalUnlocked || vault == null)
        {
            return;
        }

        // 由 Boss 流程自己累计击破次数，而不是强依赖某一个箱子的内部等级。
        // 这样以后场景中有多个箱子时，仍然可以按“总共击破 5 次箱子”开启 Boss 入口。
        BossRunProgressState.ConfigureVaultsPerBoss(requiredVaultDestroyedCount);
        BossRunProgressState.RecordVaultDestroyed(vault);
        destroyedVaultCount = BossRunProgressState.TotalVaultDestroyedCount;

        if (!BossRunProgressState.IsBossEntranceReady)
        {
            return;
        }

        portalUnlocked = true;
        StartCoroutine(UnlockPortalNextFrame(vault));
    }

    /// <summary>
    /// 延迟到下一帧再隐藏箱子。
    /// BoxCo 触发事件时仍在执行击破结算，下一帧处理可以避免打断它本轮奖励和事件广播。
    /// </summary>
    private IEnumerator UnlockPortalNextFrame(BoxCo vault)
    {
        Vector3 portalPosition = CalculatePortalPosition(vault);
        Quaternion portalRotation = vault.transform.rotation;

        yield return null;

        if (clearAliveMonsters)
        {
            StopSpawningAndClearMonsters();
        }

        if (clearMonsterProjectiles)
        {
            ClearMonsterProjectiles();
        }

        if (hideAllVaults)
        {
            HideVaultsInScene();
        }

        SpawnPortal(portalPosition, portalRotation);
    }

    /// <summary>
    /// 主场景重载后，新宝箱会先按默认值 Awake。
    /// 这里在 Start 阶段把它恢复到跨场景保存的累计击破次数，保证返回主场景后不是新开一局。
    /// </summary>
    private void RestoreMainSceneVaultProgress()
    {
        BoxCo[] vaults = FindObjectsOfType<BoxCo>(true);
        for (int i = 0; i < vaults.Length; i++)
        {
            BossRunProgressState.RestoreVaultProgressIfNeeded(vaults[i]);
        }

        destroyedVaultCount = BossRunProgressState.TotalVaultDestroyedCount;
    }

    /// <summary>
    /// 计算传送门位置。
    /// 传送门会生成在“宝箱远离玩家的一侧”，避免玩家刚打破第 5 次宝箱就立刻碰到传送门。
    /// </summary>
    private Vector3 CalculatePortalPosition(BoxCo vault)
    {
        Vector3 vaultPosition = vault != null ? vault.transform.position : transform.position;
        Vector3 horizontalDirection = Vector3.zero;

        PlayerRuntimeController player = GameplayRuntime.Instance.CurrentPlayer;
        if (player != null)
        {
            horizontalDirection = vaultPosition - player.transform.position;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude < 0.01f && vault != null)
        {
            horizontalDirection = vault.transform.forward;
            horizontalDirection.y = 0f;
        }

        if (horizontalDirection.sqrMagnitude < 0.01f)
        {
            horizontalDirection = Vector3.forward;
        }

        return vaultPosition +
               horizontalDirection.normalized * portalHorizontalOffsetFromVault +
               portalPositionOffset;
    }

    /// <summary>
    /// 停止所有刷怪点，并清理场景里已经刷出的普通怪。
    /// 这里额外兜底查找 SlimeCo，是为了兼容不在 MonsterManager 管理下的测试怪。
    /// </summary>
    private void StopSpawningAndClearMonsters()
    {
        MonsterManager[] managers = FindObjectsOfType<MonsterManager>();
        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] != null)
            {
                managers[i].StopSpawningAndClearAliveMonsters();
            }
        }

        MonsSpawner[] spawners = FindObjectsOfType<MonsSpawner>();
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
            {
                spawners[i].StopSpawning();
            }
        }

        SlimeCo[] slimes = FindObjectsOfType<SlimeCo>();
        for (int i = 0; i < slimes.Length; i++)
        {
            if (slimes[i] != null)
            {
                Destroy(slimes[i].gameObject);
            }
        }
    }

    private void ClearMonsterProjectiles()
    {
        BulletCo[] bullets = FindObjectsOfType<BulletCo>();
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
            {
                Destroy(bullets[i].gameObject);
            }
        }
    }

    private void HideVaultsInScene()
    {
        BoxCo[] vaults = FindObjectsOfType<BoxCo>();
        for (int i = 0; i < vaults.Length; i++)
        {
            if (vaults[i] != null)
            {
                vaults[i].gameObject.SetActive(false);
            }
        }
    }

    private void SpawnPortal(Vector3 position, Quaternion rotation)
    {
        if (spawnedPortal != null)
        {
            return;
        }

        spawnedPortal = portalPrefab != null
            ? Instantiate(portalPrefab, position, rotation)
            : CreateGeneratedPortal(position, rotation);

        spawnedPortal.name = "BossScenePortal";

        BossScenePortal portal = spawnedPortal.GetComponent<BossScenePortal>();
        if (portal == null)
        {
            portal = spawnedPortal.AddComponent<BossScenePortal>();
        }

        portal.ConfigureTargetScene(GameSceneNames.BossRoomScene, true);
        ApplyPortalTint(spawnedPortal);
    }

    /// <summary>
    /// 没有美术 Prefab 时生成一个临时传送门。
    /// 它只用于先跑通功能，后面可以在 Inspector 里替换成正式传送门 Prefab。
    /// </summary>
    private GameObject CreateGeneratedPortal(Vector3 position, Quaternion rotation)
    {
        GameObject portalRoot = new GameObject("GeneratedBossPortal");
        portalRoot.transform.SetPositionAndRotation(position, rotation);

        SphereCollider trigger = portalRoot.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.8f;
        trigger.center = new Vector3(0f, 1.2f, 0f);

        Rigidbody rb = portalRoot.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        BossScenePortal portal = portalRoot.AddComponent<BossScenePortal>();

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

        portal.BindVisualRoot(visual.transform);
        return portalRoot;
    }

    private void ApplyPortalTint(GameObject portalRoot)
    {
        if (portalRoot == null)
        {
            return;
        }

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
