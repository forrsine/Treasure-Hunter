using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景传送门：负责检测玩家进入触发区域，并切换到指定场景。
/// 当前用于两种场景流转：
/// 1. 主场景宝箱击破 5 次后，传送到 BossRoomScene；
/// 2. Boss 战胜利后，传送回 MainScene。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossScenePortal : MonoBehaviour
{
    [SerializeField] private string targetSceneName = GameSceneNames.BossRoomScene;
    [SerializeField] private bool capturePlayerSnapshotBeforeLoad = true;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float rotateSpeed = 90f;

    private bool isTriggered;

    private void Awake()
    {
        EnsureTriggerSetup();
    }

    private void Update()
    {
        if (visualRoot != null)
        {
            visualRoot.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryEnterPortal(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryEnterPortal(other);
    }

    /// <summary>
    /// 运行时生成传送门后调用，配置它要传送到哪个场景。
    /// captureSnapshot 为 true 时，会在切场景前保存玩家当前属性，用于新场景恢复同一个角色状态。
    /// </summary>
    public void ConfigureTargetScene(string sceneName, bool captureSnapshot = true)
    {
        if (!string.IsNullOrWhiteSpace(sceneName))
        {
            targetSceneName = sceneName;
        }

        capturePlayerSnapshotBeforeLoad = captureSnapshot;
    }

    /// <summary>
    /// 运行时生成传送门时，绑定视觉节点，方便做旋转表现。
    /// </summary>
    public void BindVisualRoot(Transform newVisualRoot)
    {
        visualRoot = newVisualRoot;
    }

    /// <summary>
    /// 确保传送门具备 Trigger Collider 和 Kinematic Rigidbody。
    /// 这样玩家 CharacterController 接触时可以稳定触发 OnTriggerEnter/Stay。
    /// </summary>
    private void EnsureTriggerSetup()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.radius = 1.6f;
            sphereCollider.center = new Vector3(0f, 1.2f, 0f);
            triggerCollider = sphereCollider;
        }

        triggerCollider.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.useGravity = false;
        rb.isKinematic = true;
    }

    /// <summary>
    /// 玩家进入传送门后切换场景。
    /// isTriggered 用来防止玩家多个碰撞体同时进入导致重复 LoadScene。
    /// </summary>
    private void TryEnterPortal(Collider other)
    {
        if (isTriggered || other == null)
        {
            return;
        }

        PlayerRuntimeController player = other.GetComponentInParent<PlayerRuntimeController>();
        if (player == null)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"无法进入目标场景：{targetSceneName} 没有加入 Build Settings。", this);
            return;
        }

        if (capturePlayerSnapshotBeforeLoad && !PlayerSceneTransferState.TryCaptureFrom(player))
        {
            Debug.LogWarning("切换场景前没有成功保存玩家快照，新场景会退回普通角色生成流程。", this);
        }

        if (targetSceneName == GameSceneNames.BossRoomScene)
        {
            BossRunProgressState.BeginBossChallenge(player.transform.position, player.transform.rotation);
        }

        isTriggered = true;
        LoadTargetScene();
    }

    private void LoadTargetScene()
    {
        if (targetSceneName == GameSceneNames.BossRoomScene)
        {
            SceneFlowService.LoadBossRoomScene();
            return;
        }

        if (targetSceneName == GameSceneNames.GameplayScene)
        {
            SceneFlowService.RestartGameplay();
            return;
        }

        SceneFlowService.PrepareForSceneLoad();
        SceneManager.LoadScene(targetSceneName);
    }
}
