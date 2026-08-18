using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏角色生成器：监听角色进入/离开事件，并把“通用玩家逻辑壳”和“职业模型”组合起来。
/// 通用能力来自 PlayerRuntime，四个职业 Prefab 只提供模型和 Animator。
/// </summary>
public class GameplayCharacterSpawner : MonoBehaviour
{
    private const string PlayerRuntimePrefabPath = "Characters/PlayerRuntime";

    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CameraCo gameplayCamera;
    [SerializeField] private int fallbackClassId = 1;

    private readonly Dictionary<long, GameObject> spawnedCharacters = new Dictionary<long, GameObject>();
    private PlayerSceneTransferSnapshot pendingSceneTransferSnapshot;

    public GameObject CurrentPlayer { get; private set; }

    /// <summary>
    /// 订阅角色进入/离开事件。
    /// 生成器本身不维护角色数据，只监听管理器的事件再决定是否实例化或销毁对象。
    /// </summary>
    private void OnEnable()
    {
        GameplayCharacterManager.Instance.CharacterEntered += OnCharacterEnter;
        GameplayCharacterManager.Instance.CharacterLeft += OnCharacterLeave;
    }

    /// <summary>
    /// 取消事件订阅，避免对象销毁后管理器还回调到失效生成器。
    /// </summary>
    private void OnDisable()
    {
        GameplayCharacterManager.Instance.CharacterEntered -= OnCharacterEnter;
        GameplayCharacterManager.Instance.CharacterLeft -= OnCharacterLeave;
    }

    /// <summary>
    /// 场景启动后，让当前选中的角色真正进入玩法场景。
    /// </summary>
    private void Start()
    {
        EnsureCharacterDataManager();
        ResolveSceneReferences();
        EnterSelectedCharacter();
    }

    /// <summary>
    /// 外部场景搭建脚本可主动传入出生点和摄像机。
    /// Boss 房间这类新场景用代码生成基础对象时，走这个入口可以避免手动拖 Inspector 引用。
    /// </summary>
    public void ConfigureSceneReferences(Transform newSpawnPoint, CameraCo newGameplayCamera)
    {
        if (newSpawnPoint != null)
        {
            spawnPoint = newSpawnPoint;
        }

        if (newGameplayCamera != null)
        {
            gameplayCamera = newGameplayCamera;
        }
    }

    /// <summary>
    /// 自动补齐常见场景引用。
    /// 主场景仍然优先使用 Inspector 引用；新建 Boss 房间时如果忘记拖引用，就按固定名字兜底查找。
    /// </summary>
    private void ResolveSceneReferences()
    {
        if (spawnPoint == null)
        {
            GameObject spawnPointObject = GameObject.Find("PlayerSpawnPoint");
            if (spawnPointObject != null)
            {
                spawnPoint = spawnPointObject.transform;
            }
        }

        if (gameplayCamera == null)
        {
            gameplayCamera = FindObjectOfType<CameraCo>();
        }
    }

    /// <summary>
    /// 直接从 MainScene 开始测试时，登录/选角场景里的全局配置对象不会提前创建。
    /// 这里在生成角色前补齐 CharacterDataManager，保证 fallbackClassId 也能查到职业配置。
    /// </summary>
    private void EnsureCharacterDataManager()
    {
        if (CharacterDataManager.Instance != null)
        {
            return;
        }

        CharacterDataManager existingManager = FindObjectOfType<CharacterDataManager>();
        if (existingManager != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("CharacterDataManager");
        managerObject.AddComponent<CharacterDataManager>();
        Debug.Log("直接进入玩法场景测试：已自动创建 CharacterDataManager。");
    }

    /// <summary>
    /// 从跨场景状态里读取当前选中的角色，并把出生点交给角色管理器。
    /// </summary>
    private void EnterSelectedCharacter()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        if (SceneManager.GetActiveScene().name == GameSceneNames.GameplayScene &&
            BossRunProgressState.TryConsumeMainSceneReturnSpawn(out _, out _))
        {
            // 从 Boss 房间返回主场景时，只消费旧的返回点状态，不再使用它覆盖出生点。
            // 这样玩家会回到主场景初始出生点，而不是停在箱子或 Boss 入口附近。
        }

        if (PlayerSceneTransferState.TryConsume(out PlayerSceneTransferSnapshot snapshot))
        {
            pendingSceneTransferSnapshot = snapshot;
            NCharacter transferredSave = snapshot.CreateCharacterSaveCopy();
            GameplayCharacterManager.Instance.EnterCurrentCharacter(
                transferredSave,
                position,
                rotation,
                snapshot.ClassId);
            return;
        }

        GameplayCharacterManager.Instance.EnterCurrentCharacter(
            SelectedCharacterState.CurrentCharacter,
            position,
            rotation,
            fallbackClassId);
    }

    /// <summary>
    /// 监听到“角色进入”事件后，创建对应的场景对象。
    /// </summary>
    private void OnCharacterEnter(GameplayCharacter character)
    {
        CreateCharacterObject(character);
    }

    /// <summary>
    /// 监听到“角色离开”事件后，销毁对应的场景对象并清理当前玩家引用。
    /// </summary>
    private void OnCharacterLeave(GameplayCharacter character)
    {
        if (character == null)
        {
            return;
        }

        if (!spawnedCharacters.TryGetValue(character.EntityId, out GameObject characterObject))
        {
            return;
        }

        if (characterObject != null)
        {
            Destroy(characterObject);
        }

        spawnedCharacters.Remove(character.EntityId);

        if (CurrentPlayer == characterObject)
        {
            CurrentPlayer = null;
        }
    }

    /// <summary>
    /// 创建一个场景中的角色对象。
    /// 这里采用“通用运行时壳 + 职业模型”的组合方式，而不是四个职业各自复制一整套玩家逻辑。
    /// </summary>
    private void CreateCharacterObject(GameplayCharacter character)
    {
        if (character == null || character.Define == null)
        {
            return;
        }

        if (!spawnedCharacters.TryGetValue(character.EntityId, out GameObject characterObject) || characterObject == null)
        {
            GameObject runtimePrefab = Resources.Load<GameObject>(PlayerRuntimePrefabPath);
            if (runtimePrefab == null)
            {
                Debug.LogError($"没有找到通用玩家 Prefab：Resources/{PlayerRuntimePrefabPath}");
                return;
            }

            characterObject = Instantiate(runtimePrefab, transform);
            characterObject.name = $"Character_{character.EntityId}_{character.Name}";

            string visualPrefabPath = !string.IsNullOrWhiteSpace(character.Define.visualPrefabPath)
                ? character.Define.visualPrefabPath
                : character.Define.gamePrefabPath;
            GameObject visualPrefab = Resources.Load<GameObject>(visualPrefabPath);
            if (visualPrefab == null)
            {
                Debug.LogError($"没有找到职业模型：Resources/{visualPrefabPath}");
                Destroy(characterObject);
                return;
            }

            // 职业模型只作为 PlayerRuntime 的表现层子物体，不再自己承载玩家业务逻辑。
            GameObject visualObject = Instantiate(visualPrefab, characterObject.transform);
            visualObject.name = "CharacterVisual";
            visualObject.transform.localPosition = Vector3.zero;
            // 保留模型 Prefab 自带的局部旋转和缩放；部分 Human Pack 模型依靠 180 度旋转校正朝向。

            PlayerRuntimeController player = characterObject.GetComponent<PlayerRuntimeController>();
            if (player == null)
            {
                Debug.LogError("PlayerRuntime Prefab 缺少 PlayerRuntimeController，无法初始化角色。", characterObject);
                Destroy(characterObject);
                return;
            }

            player.BindCharacterVisual(visualObject, character.Define);
            spawnedCharacters[character.EntityId] = characterObject;
        }

        InitCharacterObject(characterObject, character);
    }

    /// <summary>
    /// 把位置、朝向、存档数据和摄像机跟随关系接到刚创建好的角色对象上。
    /// 到这里，角色才算真正“可操作、可表现、可被摄像机跟随”。
    /// </summary>
    private void InitCharacterObject(GameObject characterObject, GameplayCharacter character)
    {
        ResolveSceneReferences();
        characterObject.transform.SetPositionAndRotation(character.Position, character.Rotation);

        PlayerRuntimeController player = characterObject.GetComponent<PlayerRuntimeController>();
        if (player != null)
        {
            player.ApplyCharacterEntryData(character.Save, character.Define);

            if (character.IsCurrentPlayer && pendingSceneTransferSnapshot != null)
            {
                TreasureHunterArchitecture.Interface.SendCommand(
                    new RestorePlayerSceneTransferSnapshotCommand(pendingSceneTransferSnapshot));
                Debug.Log("已恢复主场景玩家数据到 Boss 房间角色。");
                pendingSceneTransferSnapshot = null;
            }
        }

        if (character.IsCurrentPlayer)
        {
            CurrentPlayer = characterObject;

            if (gameplayCamera != null)
            {
                gameplayCamera.target = characterObject.transform;
            }
        }

        Debug.Log($"\u5df2\u751f\u6210\u89d2\u8272\uff1a{character.Name}");
    }
}
