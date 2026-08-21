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

    [Header("Rendering Performance")]
    [SerializeField, Range(1f, 30f)] private float refreshRate = 10f;

    private Camera miniMapCamera;
    private float nextRenderTime;
    private bool forceRender = true;

    /// <summary>
    /// Awake 只缓存自身组件，并设置小地图相机为正交相机。
    /// 正交相机不会有近大远小的透视变形，更适合做俯视小地图。
    /// </summary>
    private void Awake()
    {
        miniMapCamera = GetComponent<Camera>();
        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = orthographicSize;

        // 小地图背景不需要跟主画面一样每帧刷新。
        // 关闭 Camera 的自动渲染后，由本组件按 refreshRate 主动调用 Render，减少重复绘制密集场景的开销。
        miniMapCamera.enabled = false;
    }

    /// <summary>
    /// 启用时监听当前玩家变化。
    /// 因为玩家是运行时生成的，不能直接在场景里拖引用，所以通过 GameplayRuntime 获取当前玩家。
    /// </summary>
    private void OnEnable()
    {
        if (miniMapCamera == null)
        {
            miniMapCamera = GetComponent<Camera>();
        }

        // 即使场景配置被误改为启用，也要保证运行时不会恢复成每帧自动渲染。
        miniMapCamera.enabled = false;
        forceRender = true;
        nextRenderTime = 0f;

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

        if (miniMapCamera != null)
        {
            miniMapCamera.enabled = false;
        }
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

        TryRenderMiniMap();
    }

    /// <summary>
    /// 按固定刷新率手动渲染小地图背景。
    /// 使用 unscaledTime，避免暂停或升级界面把 Time.timeScale 设为 0 后计时状态异常。
    /// UI 图标由 MiniMapIconRenderer 每帧更新，因此降低背景刷新率不会降低图标操作反馈。
    /// </summary>
    private void TryRenderMiniMap()
    {
        if (miniMapCamera == null)
        {
            return;
        }

        float currentTime = Time.unscaledTime;
        if (!forceRender && currentTime < nextRenderTime)
        {
            return;
        }

        miniMapCamera.Render();

        forceRender = false;
        float safeRefreshRate = Mathf.Max(1f, refreshRate);
        nextRenderTime = currentTime + 1f / safeRefreshRate;
    }

    /// <summary>
    /// 给外部手动设置跟随目标，方便以后扩展到切换玩家或观战目标。
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        // 切换角色或场景传送后立即刷新一次，避免 RenderTexture 暂时保留旧位置的画面。
        forceRender = true;
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        SetTarget(player != null ? player.transform : null);
    }

    private void OnValidate()
    {
        followHeight = Mathf.Max(1f, followHeight);
        orthographicSize = Mathf.Max(1f, orthographicSize);
        refreshRate = Mathf.Clamp(refreshRate, 1f, 30f);
    }
}
