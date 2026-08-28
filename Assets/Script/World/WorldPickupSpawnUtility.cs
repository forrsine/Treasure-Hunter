using UnityEngine;

/// <summary>
/// 世界掉落点计算工具：把需要拾取的对象放到阻挡碰撞体外侧。
/// 这里只计算位置，不负责实例化和发奖励，方便宝箱、Boss 等掉落来源复用与测试。
/// </summary>
public static class WorldPickupSpawnUtility
{
    private const float DirectionEpsilon = 0.0001f;

    /// <summary>
    /// 沿首选方向寻找碰撞体表面，再向外保留完整拾取半径和额外间距。
    /// preferredDirection 通常指向玩家，使奖励落在玩家已经能够通行的一侧。
    /// </summary>
    public static Vector3 CalculateOutsidePosition(
        Collider blockingCollider,
        Vector3 fallbackOrigin,
        Vector3 preferredDirection,
        float clearanceFromSurface,
        float heightAboveColliderBottom)
    {
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(preferredDirection, Vector3.up);
        if (horizontalDirection.sqrMagnitude <= DirectionEpsilon)
        {
            horizontalDirection = Vector3.forward;
        }

        horizontalDirection.Normalize();
        float safeClearance = Mathf.Max(0f, clearanceFromSurface);
        float safeHeight = Mathf.Max(0f, heightAboveColliderBottom);

        if (blockingCollider == null)
        {
            return fallbackOrigin + horizontalDirection * safeClearance + Vector3.up * safeHeight;
        }

        Bounds bounds = blockingCollider.bounds;
        // 探测点必须位于包围盒外，Collider.ClosestPoint 才会返回朝向玩家一侧的真实表面点。
        float probeDistance = bounds.extents.magnitude + safeClearance + 1f;
        Vector3 outsideProbe = bounds.center + horizontalDirection * probeDistance;
        outsideProbe.y = bounds.min.y + Mathf.Min(safeHeight, bounds.size.y);

        Vector3 surfacePoint = blockingCollider.ClosestPoint(outsideProbe);
        // 用“表面到外部探测点”的方向作为真实外法线。斜着靠近长方体时，
        // 直接沿玩家方向偏移会把一部分距离浪费在切线方向，导致实际净空小于金币半径。
        Vector3 outwardDirection = Vector3.ProjectOnPlane(outsideProbe - surfacePoint, Vector3.up);
        if (outwardDirection.sqrMagnitude <= DirectionEpsilon)
        {
            outwardDirection = horizontalDirection;
        }

        return surfacePoint + outwardDirection.normalized * safeClearance;
    }
}
