using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 小地图标记目标：挂在怪物、宝箱等世界对象身上。
/// 它只负责把自己注册到小地图目标列表，不负责 UI 显示。
/// </summary>
[DisallowMultipleComponent]
public class MiniMapIconTarget : MonoBehaviour
{
    private static readonly List<MiniMapIconTarget> activeTargets = new List<MiniMapIconTarget>();

    public static IReadOnlyList<MiniMapIconTarget> ActiveTargets => activeTargets;

    [Header("Icon Display")]
    [SerializeField] private Color iconColor = Color.red;
    [SerializeField] private Vector2 iconSize = new Vector2(10f, 10f);

    public Color IconColor => iconColor;
    public Vector2 IconSize => iconSize;

    private void OnEnable()
    {
        // 对象启用时注册，避免小地图每帧 FindObjectOfType 查找怪物。
        if (!activeTargets.Contains(this))
        {
            activeTargets.Add(this);
        }
    }

    private void OnDisable()
    {
        // 对象禁用或死亡销毁前注销，避免 UI 继续追踪空对象。
        activeTargets.Remove(this);
    }

    private void OnDestroy()
    {
        activeTargets.Remove(this);
    }
}