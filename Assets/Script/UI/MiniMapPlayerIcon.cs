using UnityEngine;

/// <summary>
/// 小地图玩家图标：负责让 UI 箭头显示玩家朝向。
/// 注意：玩家位置由小地图相机保持在中心，这个脚本只处理箭头旋转。
/// </summary>
public class MiniMapPlayerIcon : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Header("Icon Settings")]
    [Tooltip("箭头图片的原始朝向偏移。图片默认向上填 0，默认向右填 90。")]
    [SerializeField] private float iconRotationOffset = 90f;

    private void OnEnable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;

        if (GameplayRuntime.Instance.CurrentPlayer != null)
        {
            SetTarget(GameplayRuntime.Instance.CurrentPlayer.transform);
        }
    }

    private void OnDisable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
    }

    private void Update()
    {
        if (target == null)
        {
            return;
        }

        // UI 的 Z 轴旋转对应世界里的 Y 轴朝向。
        // 因为你的箭头图片默认指向右边，所以用 90 度偏移把它校正到“默认向上”。
        transform.localRotation = Quaternion.Euler(0f, 0f, iconRotationOffset - target.eulerAngles.y);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        SetTarget(player != null ? player.transform : null);
    }
}