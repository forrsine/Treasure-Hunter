using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Boss 房间启动器：进入 BossRoomScene 后自动补齐玩家、输入、摄像机、场地、Boss 和 Boss 战 UI。
/// 这样 Boss 房间即使暂时是空场景，也能先跑通“传送进 Boss 战”的完整闭环。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossRoomSceneBootstrap : MonoBehaviour
{
    public const string PlayerSpawnPointName = "PlayerSpawnPoint";
    public const string BossSpawnPointName = "BossSpawnPoint";
    public const string BossRoomRootName = "BossRoomRoot";
    public const string BossMainLightName = "BossRoomMainLight";
    public const string BossCenterFillLightName = "BossRoomCenterFillLight";
    public const string BossBackFillLightName = "BossRoomBackFillLight";

    // Boss 房间灰盒尺寸会被运行时 Bootstrap 和编辑器生成工具共用，避免两边生成出的场景不一致。
    public const float DefaultArenaWidth = 26f;
    public const float DefaultArenaLength = 36f;
    public const float DefaultArenaHeight = 8f;
    public const float DefaultArenaWallThickness = 0.5f;
    public const string BossBattleBgmObjectName = "BossBattleBgm";
    public const string GameplayUiRootPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    public const string SpiderKingPrefabPath =
        "Assets/AllResources/Monsters Ultimate Pack 01 Cute Series/Spider King Cute Series/Prefabs/Spider King.prefab";

    private const string GameplayUiRootResourcesPath = "UI/GameplayUiRoot";

    [SerializeField] private Vector3 playerSpawnPosition = new Vector3(0f, 1f, -8f);
    [SerializeField] private Vector3 bossSpawnPosition = new Vector3(0f, 0f, 6f);

    [Header("Boss 房间灰盒配置")]
    [Tooltip("Boss 房间内部空间尺寸，X 是宽度，Y 是高度，Z 是长度。")]
    [SerializeField] private Vector3 arenaSize = new Vector3(DefaultArenaWidth, DefaultArenaHeight, DefaultArenaLength);

    [Tooltip("墙体、地板、天花板的厚度，过薄可能导致高速移动时穿出房间。")]
    [SerializeField] private float arenaWallThickness = DefaultArenaWallThickness;

    [Header("Boss 战背景音乐")]
    [Tooltip("把 Boss 战 BGM 音频文件拖到这里；为空时不会播放，方便你后续手动配置。")]
    [SerializeField] private AudioClip bossBattleMusic;

    [Tooltip("可选：如果你想手动调 AudioSource 参数，也可以把场景里的 BossBattleBgm 音源拖到这里。")]
    [SerializeField] private AudioSource bossBattleBgmSource;

    [Tooltip("Boss 战 BGM 音量。")]
    [SerializeField, Range(0f, 1f)] private float bossBattleMusicVolume = 0.65f;

    [Tooltip("是否循环播放 Boss 战 BGM。")]
    [SerializeField] private bool loopBossBattleMusic = true;

    [Tooltip("进入 Boss 房间后是否自动播放已配置的 Boss 战 BGM。")]
    [SerializeField] private bool playBossBattleMusicOnStart = true;

    [Header("Boss 房间光照")]
    [SerializeField] private Color bossAmbientColor = new Color(0.31f, 0.29f, 0.36f, 1f);
    [SerializeField] private Color bossMainLightColor = new Color(1f, 0.93f, 0.82f, 1f);
    [SerializeField, Min(0f)] private float bossMainLightIntensity = 2f;
    [SerializeField] private Color bossFillLightColor = new Color(0.62f, 0.73f, 1f, 1f);
    [SerializeField, Min(0f)] private float centerFillLightIntensity = 1.05f;
    [SerializeField, Min(0f)] private float backFillLightIntensity = 0.85f;

    [Header("Boss 周回成长")]
    [Tooltip("每多挑战一轮 Boss，最大生命额外提升比例。0.35 表示第二轮血量为第一轮的 1.35 倍。")]
    [SerializeField] private float bossHpGrowthPerRound = 0.35f;
    [Tooltip("每多挑战一轮 Boss，攻击伤害额外提升比例。")]
    [SerializeField] private float bossDamageGrowthPerRound = 0.18f;
    [Tooltip("每多挑战一轮 Boss，移动速度额外提升比例。")]
    [SerializeField] private float bossMoveSpeedGrowthPerRound = 0.08f;
    [Tooltip("每多挑战一轮 Boss，攻击冷却减少比例。")]
    [SerializeField] private float bossCooldownReductionPerRound = 0.06f;
    [Tooltip("Boss 攻击冷却最低倍率，避免高轮次技能释放过密。")]
    [SerializeField] private float bossMinimumCooldownMultiplier = 0.65f;

    private bool initialized;

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

    private static void InstallForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != GameSceneNames.BossRoomScene)
        {
            return;
        }

        BossRoomSceneBootstrap bootstrap = FindObjectOfType<BossRoomSceneBootstrap>();
        if (bootstrap == null)
        {
            GameObject bootstrapObject = new GameObject("BossRoomSceneBootstrap");
            bootstrap = bootstrapObject.AddComponent<BossRoomSceneBootstrap>();
        }

        bootstrap.BuildRoomIfNeeded();
    }

    private void Start()
    {
        BuildRoomIfNeeded();
    }

    private void OnValidate()
    {
        arenaSize.x = Mathf.Max(1f, arenaSize.x);
        arenaSize.y = Mathf.Max(1f, arenaSize.y);
        arenaSize.z = Mathf.Max(1f, arenaSize.z);
        arenaWallThickness = Mathf.Max(0.1f, arenaWallThickness);
        bossBattleMusicVolume = Mathf.Clamp01(bossBattleMusicVolume);
        bossMainLightIntensity = Mathf.Max(0f, bossMainLightIntensity);
        centerFillLightIntensity = Mathf.Max(0f, centerFillLightIntensity);
        backFillLightIntensity = Mathf.Max(0f, backFillLightIntensity);
        bossHpGrowthPerRound = Mathf.Max(0f, bossHpGrowthPerRound);
        bossDamageGrowthPerRound = Mathf.Max(0f, bossDamageGrowthPerRound);
        bossMoveSpeedGrowthPerRound = Mathf.Max(0f, bossMoveSpeedGrowthPerRound);
        bossCooldownReductionPerRound = Mathf.Max(0f, bossCooldownReductionPerRound);
        bossMinimumCooldownMultiplier = Mathf.Clamp(bossMinimumCooldownMultiplier, 0.05f, 1f);
    }

    /// <summary>
    /// 构建 Boss 房间运行所需的最小对象集合。
    /// 多次调用不会重复创建对象，方便编辑器工具和运行时兜底共用。
    /// </summary>
    public void BuildRoomIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        Transform roomRoot = EnsureRoomRoot();
        EnsureGameConfig();
        EnsureCharacterDataManager();
        EnsureSkillDataManager();
        EnsureSkillVisualPool();
        EnsureInput();
        EnsureEventSystem();
        EnsureGameplayUiRoot();
        EnsureBossRoomLighting(roomRoot);
        EnsureArenaRoom(roomRoot);

        Transform playerSpawnPoint = EnsureSpawnPoint(PlayerSpawnPointName, playerSpawnPosition, roomRoot);
        Transform bossSpawnPoint = EnsureSpawnPoint(BossSpawnPointName, bossSpawnPosition, roomRoot);
        CameraCo cameraController = EnsureGameplayCamera();
        EnsureBossBattleBgm(roomRoot);
        EnsureGameplayCharacterSpawner(playerSpawnPoint, cameraController);
        SpiderKingBossController boss = EnsureSpiderKing(bossSpawnPoint, roomRoot);
        ApplyBossRoundScaling(boss);
        EnsureBossBattleUi(boss);
        EnsureBossLootDropController(boss);
        EnsureBossVictoryPortalSpawner(boss);
    }

    private Transform EnsureRoomRoot()
    {
        GameObject root = GameObject.Find(BossRoomRootName);
        if (root == null)
        {
            root = new GameObject(BossRoomRootName);
        }

        return root.transform;
    }

    private void EnsureGameConfig()
    {
        if (GameConfig.instance != null || FindObjectOfType<GameConfig>() != null)
        {
            return;
        }

        new GameObject("GameConfig").AddComponent<GameConfig>();
    }

    private void EnsureCharacterDataManager()
    {
        if (CharacterDataManager.Instance != null || FindObjectOfType<CharacterDataManager>() != null)
        {
            return;
        }

        new GameObject("CharacterDataManager").AddComponent<CharacterDataManager>();
    }

    private void EnsureSkillDataManager()
    {
        if (SkillDataManager.Instance != null || FindObjectOfType<SkillDataManager>() != null)
        {
            return;
        }

        new GameObject("SkillDataManager").AddComponent<SkillDataManager>();
    }

    private void EnsureSkillVisualPool()
    {
        if (FindObjectOfType<SkillVisualPool>() != null)
        {
            return;
        }

        // Boss 房间也允许玩家释放技能和显示飘字，因此补一个对象池，避免技能表现频繁 Instantiate/Destroy。
        new GameObject("SkillVisualPool").AddComponent<SkillVisualPool>();
    }

    private void EnsureInput()
    {
        if (FindObjectOfType<InputCo>() != null)
        {
            return;
        }

        new GameObject("Input").AddComponent<InputCo>();
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// Boss 房间复用主场景玩法 UI Prefab，只额外叠加 Boss 血条。
    /// 这样玩家血量、蓝量、体力、技能栏和暂停面板都走同一套 UI 逻辑，避免维护两份相似界面。
    /// </summary>
    private void EnsureGameplayUiRoot()
    {
        GameplayUiRoot existingRoot = FindObjectOfType<GameplayUiRoot>(true);
        if (existingRoot != null)
        {
            existingRoot.gameObject.SetActive(true);
            return;
        }

        GameObject uiPrefab = Resources.Load<GameObject>(GameplayUiRootResourcesPath);

#if UNITY_EDITOR
        if (uiPrefab == null)
        {
            uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiRootPrefabPath);
        }
#endif

        if (uiPrefab == null)
        {
            Debug.LogWarning(
                $"Boss 房间没有找到主场景 UI Prefab：{GameplayUiRootPrefabPath}。请使用菜单 Treasure Hunter/Boss/Create Or Refresh Boss Room Scene 刷新场景，或把 GameplayUiRoot 放到 BossRoomScene 中。",
                this);
            return;
        }

        GameObject uiObject = null;
#if UNITY_EDITOR
        uiObject = PrefabUtility.InstantiatePrefab(uiPrefab) as GameObject;
#endif
        if (uiObject == null)
        {
            uiObject = Instantiate(uiPrefab);
        }

        uiObject.name = "GameplayUiRoot";
    }

    /// <summary>
    /// 创建或更新 Boss 战灰盒房间：地板 + 四面墙 + 天花板。
    /// 这里用 Unity Cube 自带的 BoxCollider 做边界，避免玩家或 Boss 跑出场景后掉落。
    /// </summary>
    private void EnsureArenaRoom(Transform roomRoot)
    {
        // 老版本 BossRoomScene 可能没有序列化新加的 arenaSize 字段。
        // 这里用默认尺寸和出生点一起兜底，确保玩家/Boss 出生点始终落在房间内部。
        float width = Mathf.Max(
            DefaultArenaWidth,
            arenaSize.x,
            Mathf.Abs(playerSpawnPosition.x) * 2f + 6f,
            Mathf.Abs(bossSpawnPosition.x) * 2f + 6f);
        float height = Mathf.Max(DefaultArenaHeight, arenaSize.y);
        float length = Mathf.Max(
            DefaultArenaLength,
            arenaSize.z,
            Mathf.Abs(playerSpawnPosition.z) * 2f + 8f,
            Mathf.Abs(bossSpawnPosition.z) * 2f + 8f);
        float thickness = Mathf.Max(0.1f, arenaWallThickness);
        float halfWidth = width * 0.5f;
        float halfLength = length * 0.5f;

        Color floorColor = new Color(0.26f, 0.26f, 0.31f, 1f);
        Color wallColor = new Color(0.24f, 0.21f, 0.29f, 1f);
        Color ceilingColor = new Color(0.18f, 0.17f, 0.23f, 1f);

        CreateOrUpdateArenaPiece(
            "BossArenaFloor",
            roomRoot,
            new Vector3(0f, -thickness * 0.5f, 0f),
            new Vector3(width, thickness, length),
            floorColor);

        CreateOrUpdateArenaPiece(
            "BossArenaNorthWall",
            roomRoot,
            new Vector3(0f, height * 0.5f, halfLength + thickness * 0.5f),
            new Vector3(width + thickness * 2f, height, thickness),
            wallColor);

        CreateOrUpdateArenaPiece(
            "BossArenaSouthWall",
            roomRoot,
            new Vector3(0f, height * 0.5f, -halfLength - thickness * 0.5f),
            new Vector3(width + thickness * 2f, height, thickness),
            wallColor);

        CreateOrUpdateArenaPiece(
            "BossArenaEastWall",
            roomRoot,
            new Vector3(halfWidth + thickness * 0.5f, height * 0.5f, 0f),
            new Vector3(thickness, height, length + thickness * 2f),
            wallColor);

        CreateOrUpdateArenaPiece(
            "BossArenaWestWall",
            roomRoot,
            new Vector3(-halfWidth - thickness * 0.5f, height * 0.5f, 0f),
            new Vector3(thickness, height, length + thickness * 2f),
            wallColor);

        CreateOrUpdateArenaPiece(
            "BossArenaCeiling",
            roomRoot,
            new Vector3(0f, height + thickness * 0.5f, 0f),
            new Vector3(width + thickness * 2f, thickness, length + thickness * 2f),
            ceilingColor);
    }

    /// <summary>
    /// 创建单个房间组件；如果场景里已有同名对象，则更新它的位置和尺寸，保证重复进入场景时不会生成重复墙体。
    /// </summary>
    private void CreateOrUpdateArenaPiece(
        string objectName,
        Transform roomRoot,
        Vector3 position,
        Vector3 scale,
        Color color)
    {
        GameObject piece = GameObject.Find(objectName);
        if (piece == null)
        {
            piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = objectName;
        }

        piece.transform.SetParent(roomRoot, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = scale;

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = renderer.material;
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
        }

        material.color = color;
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.08f);
        }

        renderer.material = material;
    }

    /// <summary>
    /// 统一 Boss 房运行时光照。
    /// 直接打开 BossRoomScene 和从主场景传送进来都会走 Bootstrap，因此光照不会因为进入方式不同而变暗。
    /// </summary>
    private void EnsureBossRoomLighting(Transform roomRoot)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = bossAmbientColor;
        RenderSettings.ambientIntensity = 1f;

        Light mainLight = FindOrCreateDirectionalLight();
        mainLight.name = BossMainLightName;
        mainLight.type = LightType.Directional;
        mainLight.color = bossMainLightColor;
        mainLight.intensity = bossMainLightIntensity;
        mainLight.shadows = LightShadows.Soft;
        mainLight.transform.rotation = Quaternion.Euler(58f, -35f, 0f);

        EnsurePointFillLight(
            BossCenterFillLightName,
            roomRoot,
            new Vector3(0f, DefaultArenaHeight * 0.72f, -2f),
            bossFillLightColor,
            centerFillLightIntensity,
            30f);
        EnsurePointFillLight(
            BossBackFillLightName,
            roomRoot,
            new Vector3(0f, DefaultArenaHeight * 0.62f, DefaultArenaLength * 0.28f),
            new Color(0.88f, 0.72f, 1f, 1f),
            backFillLightIntensity,
            22f);
    }

    private Light FindOrCreateDirectionalLight()
    {
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light != null && light.type == LightType.Directional)
            {
                return light;
            }
        }

        GameObject lightObject = new GameObject(BossMainLightName);
        return lightObject.AddComponent<Light>();
    }

    private void EnsurePointFillLight(
        string objectName,
        Transform roomRoot,
        Vector3 localPosition,
        Color color,
        float intensity,
        float range)
    {
        GameObject lightObject = GameObject.Find(objectName);
        if (lightObject == null)
        {
            lightObject = new GameObject(objectName);
        }

        if (roomRoot != null)
        {
            lightObject.transform.SetParent(roomRoot, false);
        }

        lightObject.transform.localPosition = localPosition;
        Light light = lightObject.GetComponent<Light>();
        if (light == null)
        {
            light = lightObject.AddComponent<Light>();
        }

        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private Transform EnsureSpawnPoint(string objectName, Vector3 position, Transform roomRoot)
    {
        GameObject spawnPoint = GameObject.Find(objectName);
        if (spawnPoint == null)
        {
            spawnPoint = new GameObject(objectName);
        }

        spawnPoint.transform.SetParent(roomRoot, false);
        spawnPoint.transform.localPosition = position;
        spawnPoint.transform.localRotation = Quaternion.identity;
        return spawnPoint.transform;
    }

    private CameraCo EnsureGameplayCamera()
    {
        CameraCo cameraController = FindObjectOfType<CameraCo>();
        if (cameraController != null)
        {
            return cameraController;
        }

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 5f, -12f);
        cameraObject.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
        cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        return cameraObject.AddComponent<CameraCo>();
    }

    /// <summary>
    /// Boss 战背景音乐入口：Bootstrap 只负责补齐 AudioSource 和应用 Inspector 配置。
    /// 真正播放什么音乐由场景里的 bossBattleMusic 字段决定，方便你后续手动拖 AudioClip。
    /// </summary>
    private void EnsureBossBattleBgm(Transform roomRoot)
    {
        if (bossBattleBgmSource == null)
        {
            GameObject bgmObject = GameObject.Find(BossBattleBgmObjectName);
            if (bgmObject == null)
            {
                bgmObject = new GameObject(BossBattleBgmObjectName);
            }

            if (roomRoot != null)
            {
                bgmObject.transform.SetParent(roomRoot, false);
            }

            bossBattleBgmSource = bgmObject.GetComponent<AudioSource>();
            if (bossBattleBgmSource == null)
            {
                bossBattleBgmSource = bgmObject.AddComponent<AudioSource>();
            }
        }

        bossBattleBgmSource.playOnAwake = false;
        bossBattleBgmSource.loop = loopBossBattleMusic;
        bossBattleBgmSource.volume = bossBattleMusicVolume;
        bossBattleBgmSource.spatialBlend = 0f;

        AudioClip musicToPlay = bossBattleMusic != null
            ? bossBattleMusic
            : bossBattleBgmSource.clip;
        if (musicToPlay == null)
        {
            return;
        }

        bossBattleBgmSource.clip = musicToPlay;
        if (playBossBattleMusicOnStart && !bossBattleBgmSource.isPlaying)
        {
            bossBattleBgmSource.Play();
        }
    }

    private void EnsureGameplayCharacterSpawner(Transform playerSpawnPoint, CameraCo cameraController)
    {
        GameplayCharacterSpawner spawner = FindObjectOfType<GameplayCharacterSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("GameplayCharacterSpawner");
            spawner = spawnerObject.AddComponent<GameplayCharacterSpawner>();
        }

        spawner.ConfigureSceneReferences(playerSpawnPoint, cameraController);
    }

    private SpiderKingBossController EnsureSpiderKing(Transform bossSpawnPoint, Transform roomRoot)
    {
        SpiderKingBossController existingBoss = FindObjectOfType<SpiderKingBossController>();
        if (existingBoss != null)
        {
            ConfigureBossTransform(existingBoss.gameObject, bossSpawnPoint, roomRoot, Vector3.one * 2f);
            EnsureBossController(existingBoss.gameObject, false);
            return existingBoss;
        }

        GameObject existingSpider = GameObject.Find("Spider King");
        if (existingSpider != null)
        {
            ConfigureBossTransform(existingSpider, bossSpawnPoint, roomRoot, Vector3.one * 2f);
            return EnsureBossController(existingSpider, false);
        }

#if UNITY_EDITOR
        GameObject spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SpiderKingPrefabPath);
        if (spiderPrefab != null)
        {
            GameObject spider = (GameObject)PrefabUtility.InstantiatePrefab(spiderPrefab);
            spider.name = "Spider King";
            ConfigureBossTransform(spider, bossSpawnPoint, roomRoot, Vector3.one * 2f);
            return EnsureBossController(spider, true);
        }
#endif

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.name = "Spider King Placeholder";
        ConfigureBossTransform(placeholder, bossSpawnPoint, roomRoot, new Vector3(3f, 3f, 3f));
        Debug.LogWarning("没有找到 Spider King Prefab，已生成 Boss 占位方块。");
        return EnsureBossController(placeholder, true);
    }

    private void ApplyBossRoundScaling(SpiderKingBossController boss)
    {
        if (boss == null)
        {
            return;
        }

        boss.ApplyBossRoundScaling(
            BossRunProgressState.CurrentBossRound,
            bossHpGrowthPerRound,
            bossDamageGrowthPerRound,
            bossMoveSpeedGrowthPerRound,
            bossCooldownReductionPerRound,
            bossMinimumCooldownMultiplier);
    }

    /// <summary>
    /// 统一校正 Boss 的父节点、出生位置和缩放。
    /// 这样即使你手动把 Spider King 放进场景，运行时也会把它纳入 BossRoomRoot 管理并移动到 Boss 出生点。
    /// </summary>
    private void ConfigureBossTransform(
        GameObject bossObject,
        Transform bossSpawnPoint,
        Transform roomRoot,
        Vector3 localScale)
    {
        if (bossObject == null || bossSpawnPoint == null)
        {
            return;
        }

        if (roomRoot != null)
        {
            bossObject.transform.SetParent(roomRoot, true);
        }

        bossObject.transform.SetPositionAndRotation(bossSpawnPoint.position, bossSpawnPoint.rotation);
        bossObject.transform.localScale = localScale;
    }

    private SpiderKingBossController EnsureBossController(GameObject bossObject, bool applyRecommendedCollider)
    {
        if (bossObject == null)
        {
            return null;
        }

        SpiderKingBossController bossController = bossObject.GetComponent<SpiderKingBossController>();
        if (bossController == null)
        {
            bossController = bossObject.AddComponent<SpiderKingBossController>();
            applyRecommendedCollider = true;
        }

        if (applyRecommendedCollider)
        {
            bossController.ApplyRecommendedCharacterControllerDefaults();
        }

        return bossController;
    }

    private void EnsureBossBattleUi(SpiderKingBossController boss)
    {
        BossBattleHudUi hud = FindObjectOfType<BossBattleHudUi>();
        if (hud == null)
        {
            GameObject hudObject = new GameObject("BossBattleHudUi");
            hud = hudObject.AddComponent<BossBattleHudUi>();
        }

        hud.BindBoss(boss);
    }

    private void EnsureBossVictoryPortalSpawner(SpiderKingBossController boss)
    {
        BossVictoryPortalSpawner spawner = FindObjectOfType<BossVictoryPortalSpawner>();
        if (spawner == null)
        {
            GameObject spawnerObject = new GameObject("BossVictoryPortalSpawner");
            spawner = spawnerObject.AddComponent<BossVictoryPortalSpawner>();
        }

        spawner.BindBoss(boss);
    }

    private void EnsureBossLootDropController(SpiderKingBossController boss)
    {
        BossLootDropController dropController = FindObjectOfType<BossLootDropController>();
        if (dropController == null)
        {
            GameObject dropObject = new GameObject("BossLootDropController");
            dropController = dropObject.AddComponent<BossLootDropController>();
        }

        dropController.BindBoss(boss);
    }
}
