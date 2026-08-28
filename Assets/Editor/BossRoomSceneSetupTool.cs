using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Boss 房间场景生成工具。
/// 用菜单一键创建基础 BossRoomScene，避免手动漏配出生点、输入、摄像机、Boss、UI 和 Build Settings。
/// </summary>
public static class BossRoomSceneSetupTool
{
    private const string ScenePath = "Assets/Scenes/BossRoomScene.unity";

    [MenuItem("Treasure Hunter/Boss/Create Or Refresh Boss Room Scene")]
    public static void CreateOrRefreshBossRoomScene()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Transform roomRoot = CreateRoot(BossRoomSceneBootstrap.BossRoomRootName);
        CreateGameConfig();
        CreateCharacterDataManager();
        CreateSkillDataManager();
        CreateInput();
        CreateEventSystem();
        CreateGameplayUiRoot();
        CreateBossRoomLighting(roomRoot);
        CreateArenaRoom(roomRoot);

        Transform playerSpawnPoint = CreateSpawnPoint(
            BossRoomSceneBootstrap.PlayerSpawnPointName,
            new Vector3(0f, 1f, -8f),
            roomRoot);
        Transform bossSpawnPoint = CreateSpawnPoint(
            BossRoomSceneBootstrap.BossSpawnPointName,
            new Vector3(0f, 0f, 6f),
            roomRoot);

        CameraCo cameraController = CreateGameplayCamera();
        CreateGameplayCharacterSpawner(playerSpawnPoint, cameraController);
        SpiderKingBossController boss = CreateSpiderKing(bossSpawnPoint, roomRoot);
        CreateBossBattleUi(boss);
        CreateBossLootDropController(boss);
        CreateBossVictoryPortalSpawner(boss);

        GameObject bootstrapObject = new GameObject("BossRoomSceneBootstrap");
        bootstrapObject.AddComponent<BossRoomSceneBootstrap>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Boss 房间场景已创建并加入 Build Settings：{ScenePath}");
    }

    private static Transform CreateRoot(string objectName)
    {
        GameObject root = new GameObject(objectName);
        return root.transform;
    }

    private static void CreateGameConfig()
    {
        new GameObject("GameConfig").AddComponent<GameConfig>();
    }

    private static void CreateCharacterDataManager()
    {
        new GameObject("CharacterDataManager").AddComponent<CharacterDataManager>();
    }

    private static void CreateSkillDataManager()
    {
        new GameObject("SkillDataManager").AddComponent<SkillDataManager>();
    }

    private static void CreateInput()
    {
        new GameObject("Input").AddComponent<InputCo>();
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    /// <summary>
    /// Boss 房间直接复用主场景 GameplayUiRoot Prefab。
    /// 这样玩家 HUD、技能栏、暂停面板都来自同一套 UI 资源，Boss 场景只额外叠加 Boss 血条。
    /// </summary>
    private static void CreateGameplayUiRoot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossRoomSceneBootstrap.GameplayUiRootPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"没有找到 GameplayUiRoot Prefab：{BossRoomSceneBootstrap.GameplayUiRootPrefabPath}");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance != null)
        {
            instance.name = "GameplayUiRoot";
        }
    }

    private static void CreateBossRoomLighting(Transform roomRoot)
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.31f, 0.29f, 0.36f, 1f);
        RenderSettings.ambientIntensity = 1f;

        GameObject lightObject = new GameObject(BossRoomSceneBootstrap.BossMainLightName);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.93f, 0.82f, 1f);
        light.intensity = 2f;
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(58f, -35f, 0f);

        CreatePointFillLight(
            BossRoomSceneBootstrap.BossCenterFillLightName,
            roomRoot,
            new Vector3(0f, BossRoomSceneBootstrap.DefaultArenaHeight * 0.72f, -2f),
            new Color(0.62f, 0.73f, 1f, 1f),
            1.05f,
            30f);
        CreatePointFillLight(
            BossRoomSceneBootstrap.BossBackFillLightName,
            roomRoot,
            new Vector3(0f, BossRoomSceneBootstrap.DefaultArenaHeight * 0.62f, BossRoomSceneBootstrap.DefaultArenaLength * 0.28f),
            new Color(0.88f, 0.72f, 1f, 1f),
            0.85f,
            22f);
    }

    private static void CreatePointFillLight(
        string objectName,
        Transform roomRoot,
        Vector3 localPosition,
        Color color,
        float intensity,
        float range)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(roomRoot, false);
        lightObject.transform.localPosition = localPosition;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    /// <summary>
    /// 编辑器一键生成 BossRoomScene 时，同步搭建封闭长方体灰盒房间。
    /// 这里复用 BossRoomSceneBootstrap 的默认尺寸，避免编辑器场景和运行时兜底逻辑不一致。
    /// </summary>
    private static void CreateArenaRoom(Transform roomRoot)
    {
        float width = BossRoomSceneBootstrap.DefaultArenaWidth;
        float length = BossRoomSceneBootstrap.DefaultArenaLength;
        float height = BossRoomSceneBootstrap.DefaultArenaHeight;
        float thickness = BossRoomSceneBootstrap.DefaultArenaWallThickness;
        float halfWidth = width * 0.5f;
        float halfLength = length * 0.5f;

        Color floorColor = new Color(0.26f, 0.26f, 0.31f, 1f);
        Color wallColor = new Color(0.24f, 0.21f, 0.29f, 1f);
        Color ceilingColor = new Color(0.18f, 0.17f, 0.23f, 1f);

        CreateArenaPiece(
            "BossArenaFloor",
            roomRoot,
            new Vector3(0f, -thickness * 0.5f, 0f),
            new Vector3(width, thickness, length),
            floorColor,
            false);

        CreateArenaPiece(
            "BossArenaNorthWall",
            roomRoot,
            new Vector3(0f, height * 0.5f, halfLength + thickness * 0.5f),
            new Vector3(width + thickness * 2f, height, thickness),
            wallColor,
            true);

        CreateArenaPiece(
            "BossArenaSouthWall",
            roomRoot,
            new Vector3(0f, height * 0.5f, -halfLength - thickness * 0.5f),
            new Vector3(width + thickness * 2f, height, thickness),
            wallColor,
            true);

        CreateArenaPiece(
            "BossArenaEastWall",
            roomRoot,
            new Vector3(halfWidth + thickness * 0.5f, height * 0.5f, 0f),
            new Vector3(thickness, height, length + thickness * 2f),
            wallColor,
            true);

        CreateArenaPiece(
            "BossArenaWestWall",
            roomRoot,
            new Vector3(-halfWidth - thickness * 0.5f, height * 0.5f, 0f),
            new Vector3(thickness, height, length + thickness * 2f),
            wallColor,
            true);

        CreateArenaPiece(
            "BossArenaCeiling",
            roomRoot,
            new Vector3(0f, height + thickness * 0.5f, 0f),
            new Vector3(width + thickness * 2f, thickness, length + thickness * 2f),
            ceilingColor,
            true);
    }

    /// <summary>
    /// 创建单个灰盒块。Cube 自带 BoxCollider，所以地板和墙体能直接阻挡 CharacterController。
    /// </summary>
    private static void CreateArenaPiece(
        string objectName,
        Transform roomRoot,
        Vector3 position,
        Vector3 scale,
        Color color,
        bool allowsCameraPassThrough)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = objectName;
        piece.transform.SetParent(roomRoot, false);
        piece.transform.localPosition = position;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = scale;

        if (allowsCameraPassThrough)
        {
            piece.AddComponent<CameraPassThroughOccluder>();
        }

        Renderer renderer = piece.GetComponent<Renderer>();
        if (renderer == null)
        {
            return;
        }

        Material material = new Material(Shader.Find("Standard"));
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 0.08f);
        renderer.sharedMaterial = material;
    }

    private static Transform CreateSpawnPoint(string objectName, Vector3 position, Transform roomRoot)
    {
        GameObject spawnPoint = new GameObject(objectName);
        spawnPoint.transform.SetParent(roomRoot, false);
        spawnPoint.transform.position = position;
        return spawnPoint.transform;
    }

    private static CameraCo CreateGameplayCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 5f, -12f);
        cameraObject.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        cameraObject.AddComponent<AudioListener>();
        CameraCo cameraController = cameraObject.AddComponent<CameraCo>();
        cameraObject.AddComponent<CameraOcclusionController>();
        return cameraController;
    }

    private static void CreateGameplayCharacterSpawner(Transform playerSpawnPoint, CameraCo cameraController)
    {
        GameObject spawnerObject = new GameObject("GameplayCharacterSpawner");
        GameplayCharacterSpawner spawner = spawnerObject.AddComponent<GameplayCharacterSpawner>();
        spawner.ConfigureSceneReferences(playerSpawnPoint, cameraController);
    }

    private static SpiderKingBossController CreateSpiderKing(Transform bossSpawnPoint, Transform roomRoot)
    {
        GameObject spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossRoomSceneBootstrap.SpiderKingPrefabPath);
        GameObject spider;

        if (spiderPrefab != null)
        {
            spider = (GameObject)PrefabUtility.InstantiatePrefab(spiderPrefab);
            spider.name = "Spider King";
            spider.transform.SetParent(roomRoot, true);
            spider.transform.SetPositionAndRotation(bossSpawnPoint.position, bossSpawnPoint.rotation);
            spider.transform.localScale = Vector3.one * 2f;
        }
        else
        {
            spider = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spider.name = "Spider King Placeholder";
            spider.transform.SetParent(roomRoot, false);
            spider.transform.position = bossSpawnPoint.position;
            spider.transform.localScale = new Vector3(3f, 3f, 3f);
            Debug.LogWarning($"没有找到 Spider King Prefab：{BossRoomSceneBootstrap.SpiderKingPrefabPath}，已生成 Boss 占位方块。");
        }

        SpiderKingBossController boss = spider.GetComponent<SpiderKingBossController>();
        if (boss == null)
        {
            boss = spider.AddComponent<SpiderKingBossController>();
        }

        if (spider.GetComponent<CharacterController>() == null)
        {
            spider.AddComponent<CharacterController>();
        }

        boss.ApplyRecommendedCharacterControllerDefaults();
        return boss;
    }

    private static void CreateBossBattleUi(SpiderKingBossController boss)
    {
        GameObject hudObject = new GameObject("BossBattleHudUi");
        BossBattleHudUi hud = hudObject.AddComponent<BossBattleHudUi>();
        hud.BindBoss(boss);
    }

    private static void CreateBossLootDropController(SpiderKingBossController boss)
    {
        GameObject dropObject = new GameObject("BossLootDropController");
        BossLootDropController dropController = dropObject.AddComponent<BossLootDropController>();
        dropController.BindBoss(boss);
    }

    private static void CreateBossVictoryPortalSpawner(SpiderKingBossController boss)
    {
        GameObject spawnerObject = new GameObject("BossVictoryPortalSpawner");
        BossVictoryPortalSpawner spawner = spawnerObject.AddComponent<BossVictoryPortalSpawner>();
        spawner.BindBoss(boss);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(scene => scene.path == scenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
