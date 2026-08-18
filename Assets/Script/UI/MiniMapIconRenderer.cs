using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图图标渲染器：把世界中的 MiniMapIconTarget 转换成小地图 UI 坐标。
/// 注意：它只负责 UI 图标显示，不负责怪物生成、宝箱逻辑或小地图相机渲染。
/// </summary>
public class MiniMapIconRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform miniMapRect;
    [SerializeField] private RectTransform iconPrefab;
    [SerializeField] private Transform playerTarget;

    [Header("Map Settings")]
    [Tooltip("必须和 MiniMapCamera 的 Orthographic Size 保持一致。")]
    [SerializeField] private float orthographicSize = 25f;

    [Tooltip("如果小地图相机跟随玩家旋转，这里也要勾选。第一版建议不勾选。")]
    [SerializeField] private bool rotateWithPlayer;

    private readonly Dictionary<MiniMapIconTarget, RectTransform> iconViews = new Dictionary<MiniMapIconTarget, RectTransform>();
    private readonly List<MiniMapIconTarget> removeBuffer = new List<MiniMapIconTarget>();

    private void Awake()
    {
        if (miniMapRect == null)
        {
            miniMapRect = transform.parent as RectTransform;
        }
    }

    private void OnEnable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;

        if (GameplayRuntime.Instance.CurrentPlayer != null)
        {
            playerTarget = GameplayRuntime.Instance.CurrentPlayer.transform;
        }
    }

    private void OnDisable()
    {
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
        ClearAllIcons();
    }

    private void Update()
    {
        if (miniMapRect == null || iconPrefab == null || playerTarget == null)
        {
            return;
        }

        RefreshIcons();
    }

    private void RefreshIcons()
    {
        RemoveInvalidIcons();

        IReadOnlyList<MiniMapIconTarget> targets = MiniMapIconTarget.ActiveTargets;
        for (int i = 0; i < targets.Count; i++)
        {
            MiniMapIconTarget target = targets[i];
            if (target == null)
            {
                continue;
            }

            UpdateIcon(target);
        }
    }

    private void UpdateIcon(MiniMapIconTarget target)
    {
        RectTransform icon = GetOrCreateIcon(target);

        Vector3 offset = target.transform.position - playerTarget.position;

        // 如果小地图画面会跟着玩家旋转，图标坐标也要反向旋转，才能和画面对齐。
        if (rotateWithPlayer)
        {
            offset = Quaternion.Euler(0f, -playerTarget.eulerAngles.y, 0f) * offset;
        }

        float pixelsPerWorldUnit = miniMapRect.rect.height / (orthographicSize * 2f);
        Vector2 iconPosition = new Vector2(offset.x, offset.z) * pixelsPerWorldUnit;

        float halfWidth = miniMapRect.rect.width * 0.5f;
        float halfHeight = miniMapRect.rect.height * 0.5f;
        bool insideMap = Mathf.Abs(iconPosition.x) <= halfWidth && Mathf.Abs(iconPosition.y) <= halfHeight;

        icon.gameObject.SetActive(insideMap);
        if (!insideMap)
        {
            return;
        }

        icon.anchoredPosition = iconPosition;
        icon.sizeDelta = target.IconSize;

        Image image = icon.GetComponent<Image>();
        if (image != null)
        {
            image.color = target.IconColor;
        }
    }

    private RectTransform GetOrCreateIcon(MiniMapIconTarget target)
    {
        if (iconViews.TryGetValue(target, out RectTransform icon) && icon != null)
        {
            return icon;
        }

        icon = Instantiate(iconPrefab, transform);
        icon.anchorMin = new Vector2(0.5f, 0.5f);
        icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);

        iconViews[target] = icon;
        return icon;
    }

    private void RemoveInvalidIcons()
    {
        removeBuffer.Clear();

        foreach (KeyValuePair<MiniMapIconTarget, RectTransform> pair in iconViews)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled)
            {
                removeBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removeBuffer.Count; i++)
        {
            RemoveIcon(removeBuffer[i]);
        }
    }

    private void RemoveIcon(MiniMapIconTarget target)
    {
        if (!iconViews.TryGetValue(target, out RectTransform icon))
        {
            return;
        }

        if (icon != null)
        {
            Destroy(icon.gameObject);
        }

        iconViews.Remove(target);
    }

    private void ClearAllIcons()
    {
        foreach (RectTransform icon in iconViews.Values)
        {
            if (icon != null)
            {
                Destroy(icon.gameObject);
            }
        }

        iconViews.Clear();
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        playerTarget = player != null ? player.transform : null;
    }
}