using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 摄像机遮挡处理器：在角色观察点和镜头之间检测可穿透墙体，
/// 临时关闭遮挡墙渲染，确保镜头穿到房间外时仍能看到角色。
/// </summary>
[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraCo))]
public sealed class CameraOcclusionController : MonoBehaviour
{
    private const int RaycastBufferCapacity = 16;

    [Tooltip("参与角色与摄像机之间遮挡检测的物理层。")]
    [SerializeField] private LayerMask occlusionMask = ~0;

    private readonly RaycastHit[] raycastHits = new RaycastHit[RaycastBufferCapacity];
    private readonly Dictionary<Renderer, bool> originalRenderingStates =
        new Dictionary<Renderer, bool>();

    private HashSet<CameraPassThroughOccluder> activeOccluders =
        new HashSet<CameraPassThroughOccluder>();
    private HashSet<CameraPassThroughOccluder> frameOccluders =
        new HashSet<CameraPassThroughOccluder>();

    private CameraCo cameraController;

    private void Awake()
    {
        cameraController = GetComponent<CameraCo>();
    }

    /// <summary>
    /// 执行顺序晚于 CameraCo，确保检测使用的是本帧最终镜头位置。
    /// RaycastNonAlloc 和复用 HashSet 可以避免每帧产生托管垃圾。
    /// </summary>
    private void LateUpdate()
    {
        if (cameraController == null)
        {
            cameraController = GetComponent<CameraCo>();
        }

        if (cameraController == null || cameraController.target == null)
        {
            RestoreAllOccluders();
            return;
        }

        Vector3 focusPosition = cameraController.target.position + cameraController.offset;
        Vector3 cameraVector = transform.position - focusPosition;
        float cameraDistance = cameraVector.magnitude;
        if (cameraDistance <= Mathf.Epsilon)
        {
            RestoreAllOccluders();
            return;
        }

        frameOccluders.Clear();
        int hitCount = Physics.RaycastNonAlloc(
            focusPosition,
            cameraVector / cameraDistance,
            raycastHits,
            cameraDistance,
            occlusionMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = raycastHits[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            CameraPassThroughOccluder occluder =
                hitCollider.GetComponentInParent<CameraPassThroughOccluder>();
            if (occluder != null && occluder.isActiveAndEnabled)
            {
                frameOccluders.Add(occluder);
            }
        }

        RefreshHiddenOccluders();
    }

    private void OnDisable()
    {
        // 切场景或关闭组件时必须恢复 Renderer，防止静态场景对象残留隐藏状态。
        RestoreAllOccluders();
    }

    private void RefreshHiddenOccluders()
    {
        foreach (CameraPassThroughOccluder occluder in activeOccluders)
        {
            if (!frameOccluders.Contains(occluder))
            {
                RestoreOccluder(occluder);
            }
        }

        foreach (CameraPassThroughOccluder occluder in frameOccluders)
        {
            if (!activeOccluders.Contains(occluder))
            {
                HideOccluder(occluder);
            }
        }

        // 交换两个复用集合，避免把本帧结果复制到新的集合中。
        HashSet<CameraPassThroughOccluder> previousOccluders = activeOccluders;
        activeOccluders = frameOccluders;
        frameOccluders = previousOccluders;
        frameOccluders.Clear();
    }

    private void HideOccluder(CameraPassThroughOccluder occluder)
    {
        if (occluder == null)
        {
            return;
        }

        IReadOnlyList<Renderer> renderers = occluder.Renderers;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!originalRenderingStates.ContainsKey(renderer))
            {
                originalRenderingStates.Add(renderer, renderer.forceRenderingOff);
            }

            // forceRenderingOff 不会影响 Collider，也不会改动或实例化材质。
            renderer.forceRenderingOff = true;
        }
    }

    private void RestoreOccluder(CameraPassThroughOccluder occluder)
    {
        if (occluder == null)
        {
            return;
        }

        IReadOnlyList<Renderer> renderers = occluder.Renderers;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            bool originalState;
            if (originalRenderingStates.TryGetValue(renderer, out originalState))
            {
                renderer.forceRenderingOff = originalState;
                originalRenderingStates.Remove(renderer);
            }
        }
    }

    private void RestoreAllOccluders()
    {
        foreach (KeyValuePair<Renderer, bool> entry in originalRenderingStates)
        {
            if (entry.Key != null)
            {
                entry.Key.forceRenderingOff = entry.Value;
            }
        }

        originalRenderingStates.Clear();
        activeOccluders.Clear();
        frameOccluders.Clear();
    }
}
