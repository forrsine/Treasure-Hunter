using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 摄像机可穿透遮挡物标记。
/// 物体仍保留 Collider 阻挡角色，只允许 CameraCo 忽略镜头碰撞，
/// 并向 CameraOcclusionController 提供需要临时隐藏的 Renderer。
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraPassThroughOccluder : MonoBehaviour
{
    private Renderer[] cachedRenderers;

    /// <summary>
    /// 缓存遮挡物自身及子节点的 Renderer，避免摄像机每帧重复执行组件查找。
    /// </summary>
    public IReadOnlyList<Renderer> Renderers
    {
        get
        {
            if (cachedRenderers == null)
            {
                CacheRenderers();
            }

            return cachedRenderers;
        }
    }

    private void Awake()
    {
        CacheRenderers();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheRenderers();
    }
#endif

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
    }
}
