using UnityEngine;

/// <summary>
/// 小地图相机控制器：负责让小地图相机跟随当前玩家，从玩家头顶俯视渲染地图。
/// 注意：它只控制相机位置和视野，不处理 UI 显示，避免相机逻辑和 UI 逻辑耦合。
/// </summary>
[RequireComponent(typeof(Camera))]
public class MiniMapCameraController : MonoBehaviour
{
    [Header("Follow Target")]
    [SerializeField] private Transform target;

    [Header("Camera Settings")]
    [SerializeField] private float followHeight = 35f;
    [SerializeField] private float orthographicSize = 25f;
    [SerializeField] private bool rotateWithPlayer = false;

    private Camera miniMapCamera;

    /// <summary>
    /// Awake 只缓存自身组件，并设置小地图相机为正交相机。
    /// 正交相机不会有近大远小的透视变形，更适合做俯视小地图。
    /// </summary>
    private void Awake()
    {
        miniMapCamera = GetComponent<Camera>();
        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = orthographicSize;
    }

    /// <summary>
    /// 启用时监听当前玩家变化。
    /// 因为玩家是运行时生成的，不能直接在场景里拖引用，所以通过 GameplayRuntime 获取当前玩家。
    /// </summary>
    private void OnEnable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;

        if (GameplayRuntime.Instance.CurrentPlayer != null)
        {
            SetTarget(GameplayRuntime.Instance.CurrentPlayer.transform);
        }
    }

    /// <summary>
    /// 禁用时取消监听，避免对象销毁后事件还回调到这个脚本。
    /// </summary>
    private void OnDisable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
    }

    /// <summary>
    /// LateUpdate 在玩家移动完成后再更新相机位置，减少画面抖动。
    /// </summary>
    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 cameraPosition = target.position + Vector3.up * followHeight;
        transform.position = cameraPosition;

        if (rotateWithPlayer)
        {
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    /// <summary>
    /// 给外部手动设置跟随目标，方便以后扩展到切换玩家或观战目标。
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        SetTarget(player != null ? player.transform : null);
    }
}