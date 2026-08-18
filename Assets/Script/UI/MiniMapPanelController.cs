using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图面板控制器：负责在“右上角小地图”和“屏幕中央大地图”之间切换。
/// 注意：这个脚本只控制 UI 大小、位置、鼠标状态和关闭按钮，不处理传送逻辑。
/// </summary>
[DisallowMultipleComponent]
public class MiniMapPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform miniMapRoot;
    [SerializeField] private Button closeButton;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;

    [Header("Expanded Map")]
    [SerializeField] private Vector2 expandedSize = new Vector2(800f, 800f);

    private bool isExpanded;

    private Vector2 smallAnchorMin;
    private Vector2 smallAnchorMax;
    private Vector2 smallPivot;
    private Vector2 smallAnchoredPosition;
    private Vector2 smallSizeDelta;

    public bool IsExpanded => isExpanded;

    private void Awake()
    {
        if (miniMapRoot == null)
        {
            miniMapRoot = transform as RectTransform;
        }

        CacheSmallMapLayout();
        SetCloseButtonVisible(false);
    }

    private void OnEnable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CollapseMap);
            closeButton.onClick.AddListener(CollapseMap);
        }
    }

    private void OnDisable()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CollapseMap);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            // 背包、暂停或结算界面打开时不允许大地图抢夺鼠标焦点。
            if (!isExpanded && GameSessionUi.Instance != null && GameSessionUi.Instance.IsGameplayInputBlocked)
            {
                return;
            }

            ToggleMap();
        }
    }

    /// <summary>
    /// 记录右上角小地图原本的位置和大小。
    /// 关闭大地图时，会用这些数据恢复回原来的小地图状态。
    /// </summary>
    private void CacheSmallMapLayout()
    {
        if (miniMapRoot == null)
        {
            return;
        }

        smallAnchorMin = miniMapRoot.anchorMin;
        smallAnchorMax = miniMapRoot.anchorMax;
        smallPivot = miniMapRoot.pivot;
        smallAnchoredPosition = miniMapRoot.anchoredPosition;
        smallSizeDelta = miniMapRoot.sizeDelta;
    }

    public void ToggleMap()
    {
        if (isExpanded)
        {
            CollapseMap();
        }
        else
        {
            ExpandMap();
        }
    }

    /// <summary>
    /// 打开大地图：移动到屏幕中心，并放大到指定尺寸。
    /// </summary>
    public void ExpandMap()
    {
        if (miniMapRoot == null)
        {
            return;
        }

        isExpanded = true;

        miniMapRoot.anchorMin = new Vector2(0.5f, 0.5f);
        miniMapRoot.anchorMax = new Vector2(0.5f, 0.5f);
        miniMapRoot.pivot = new Vector2(0.5f, 0.5f);
        miniMapRoot.anchoredPosition = Vector2.zero;
        miniMapRoot.sizeDelta = expandedSize;

        miniMapRoot.SetAsLastSibling();
        SetCloseButtonVisible(true);

        // 确保关闭按钮永远在小地图图片上面显示。
        if (closeButton != null)
        {
            closeButton.transform.SetAsLastSibling();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 关闭大地图：恢复为右上角小地图。
    /// </summary>
    public void CollapseMap()
    {
        if (miniMapRoot == null)
        {
            return;
        }

        isExpanded = false;

        miniMapRoot.anchorMin = smallAnchorMin;
        miniMapRoot.anchorMax = smallAnchorMax;
        miniMapRoot.pivot = smallPivot;
        miniMapRoot.anchoredPosition = smallAnchoredPosition;
        miniMapRoot.sizeDelta = smallSizeDelta;

        SetCloseButtonVisible(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(visible);
        }
    }
}
