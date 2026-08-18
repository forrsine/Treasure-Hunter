using System.Collections;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背包面板：负责 B 键开关、暂停与鼠标状态、格子刷新、详情展示和获得物品提示。
/// 核心物品规则仍由 InventorySystem 处理，这里只消费只读模型和事件。
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryPanel : MonoBehaviour, IController
{
    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameSessionUi sessionUi;
    [SerializeField] private MiniMapPanelController miniMapPanel;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text emptyStateText;
    [SerializeField] private Image detailIcon;
    [SerializeField] private Text detailNameText;
    [SerializeField] private Text detailMetaText;
    [SerializeField] private Text detailCountText;
    [SerializeField] private Text detailDescriptionText;
    [SerializeField] private Button useButton;
    [SerializeField] private InventorySlotView[] slotViews;

    [Header("Loot Toast")]
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private Text toastText;
    [SerializeField, Min(0.1f)] private float toastDuration = 2.2f;

    private int selectedSlotIndex = -1;
    private bool isOpen;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;
    private Coroutine toastRoutine;

    public bool IsOpen => isOpen;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void OnEnable()
    {
        this.RegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        this.RegisterEvent<InventoryItemAddedEvent>(HandleItemAdded);
        this.RegisterEvent<InventoryFullEvent>(HandleInventoryFull);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (useButton != null)
        {
            useButton.onClick.RemoveListener(HandleUseButtonClicked);
            useButton.onClick.AddListener(HandleUseButtonClicked);
        }
    }

    private void OnDisable()
    {
        this.UnRegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        this.UnRegisterEvent<InventoryItemAddedEvent>(HandleItemAdded);
        this.UnRegisterEvent<InventoryFullEvent>(HandleInventoryFull);

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (useButton != null)
        {
            useButton.onClick.RemoveListener(HandleUseButtonClicked);
        }

        CloseImmediateAndRestoreState();
    }

    private void Start()
    {
        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            slotViews[i].Initialize(i, HandleSlotClicked);
        }

        panelRoot.SetActive(false);
        toastRoot.SetActive(false);
        RefreshView();
    }

    private void Update()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || !input.InventoryToggleDown)
        {
            return;
        }

        if (isOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (sessionUi == null) missing = nameof(sessionUi);
        else if (panelRoot == null) missing = nameof(panelRoot);
        else if (closeButton == null) missing = nameof(closeButton);
        else if (capacityText == null) missing = nameof(capacityText);
        else if (emptyStateText == null) missing = nameof(emptyStateText);
        else if (detailIcon == null) missing = nameof(detailIcon);
        else if (detailNameText == null) missing = nameof(detailNameText);
        else if (detailMetaText == null) missing = nameof(detailMetaText);
        else if (detailCountText == null) missing = nameof(detailCountText);
        else if (detailDescriptionText == null) missing = nameof(detailDescriptionText);
        else if (useButton == null) missing = nameof(useButton);
        else if (slotViews == null || slotViews.Length != InventoryModel.DefaultCapacity) missing = nameof(slotViews);
        else if (toastRoot == null) missing = nameof(toastRoot);
        else if (toastText == null) missing = nameof(toastText);

        if (missing == null)
        {
            for (int i = 0; i < slotViews.Length; i++)
            {
                if (slotViews[i] == null || !slotViews[i].ValidatePrefabReferences(false))
                {
                    missing = $"{nameof(slotViews)}[{i}]";
                    break;
                }
            }
        }

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"InventoryPanel 的 Prefab 引用未配置：{missing}。", this);
        }

        return false;
    }

    /// <summary>
    /// 打开背包前先检查其他模态 UI。升级、暂停、结算或开场引导正在占用时间状态时不抢焦点。
    /// </summary>
    public void Open()
    {
        if (isOpen || !CanOpen())
        {
            return;
        }

        if (miniMapPanel != null && miniMapPanel.IsExpanded)
        {
            miniMapPanel.CollapseMap();
        }

        CaptureGameplayState();
        isOpen = true;
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        SelectFirstAvailableSlot();
        RefreshView();
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        panelRoot.SetActive(false);
        RestoreGameplayState();
    }

    private bool CanOpen()
    {
        PlayerModel playerModel = this.GetModel<PlayerModel>();
        if (playerModel != null && playerModel.Stats.IsUpgradeSelectionActive)
        {
            return false;
        }

        if (sessionUi != null && sessionUi.IsGameplayInputBlocked)
        {
            return false;
        }

        // 开场引导等已有弹窗会把 timeScale 设为 0；背包不应覆盖它们缓存的全局状态。
        return Time.timeScale > 0f;
    }

    private void CaptureGameplayState()
    {
        if (hasCapturedGameplayState)
        {
            return;
        }

        cachedTimeScale = Time.timeScale;
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        hasCapturedGameplayState = true;
        Time.timeScale = 0f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
    }

    private void RestoreGameplayState()
    {
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        hasCapturedGameplayState = false;
    }

    private void CloseImmediateAndRestoreState()
    {
        isOpen = false;
        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }

        RestoreGameplayState();
    }

    private void HandleInventoryChanged(InventoryChangedEvent _)
    {
        RefreshView();
    }

    private void HandleItemAdded(InventoryItemAddedEvent e)
    {
        if (e.Item == null || e.AddedAmount <= 0)
        {
            return;
        }

        string message = $"获得 {e.Item.DisplayName} ×{e.AddedAmount}";
        if (e.RemainingAmount > 0)
        {
            message += $"（{e.RemainingAmount} 个未放入）";
        }

        ShowToast(message, InventoryUiUtility.GetRarityColor(e.Item.Rarity));
    }

    private void HandleInventoryFull(InventoryFullEvent e)
    {
        string itemName = e.Item != null ? e.Item.DisplayName : "物品";
        ShowToast($"背包已满，{itemName} 有 {e.RemainingAmount} 个未能放入", new Color(1f, 0.48f, 0.36f, 1f));
    }

    private void RefreshView()
    {
        if (slotViews == null || slotViews.Length == 0)
        {
            return;
        }

        InventoryModel model = this.GetModel<InventoryModel>();
        if (model == null)
        {
            return;
        }

        int occupied = model.GetOccupiedSlotCount();
        if (capacityText != null)
        {
            capacityText.text = $"容量  {occupied} / {model.Capacity}";
        }

        if (emptyStateText != null)
        {
            emptyStateText.gameObject.SetActive(occupied == 0);
        }

        if (selectedSlotIndex >= model.Slots.Count)
        {
            selectedSlotIndex = -1;
        }

        for (int i = 0; i < slotViews.Length; i++)
        {
            InventorySlotData slot = i < model.Slots.Count ? model.Slots[i] : null;
            slotViews[i].Refresh(slot, i == selectedSlotIndex);
        }

        InventorySlotData selectedSlot = selectedSlotIndex >= 0 && selectedSlotIndex < model.Slots.Count
            ? model.Slots[selectedSlotIndex]
            : null;
        RefreshDetail(selectedSlot);
    }

    private void SelectFirstAvailableSlot()
    {
        InventoryModel model = this.GetModel<InventoryModel>();
        if (model == null)
        {
            selectedSlotIndex = -1;
            return;
        }

        if (selectedSlotIndex >= 0 && selectedSlotIndex < model.Slots.Count && !model.Slots[selectedSlotIndex].IsEmpty)
        {
            return;
        }

        selectedSlotIndex = -1;
        for (int i = 0; i < model.Slots.Count; i++)
        {
            if (!model.Slots[i].IsEmpty)
            {
                selectedSlotIndex = i;
                break;
            }
        }
    }

    private void HandleSlotClicked(int index)
    {
        selectedSlotIndex = index;
        RefreshView();
    }

    private void HandleUseButtonClicked()
    {
        InventoryUseResult result = this.SendCommand(new UseInventoryItemCommand(selectedSlotIndex));
        if (result.Succeeded)
        {
            string resourceName = result.Item != null &&
                result.Item.UseEffect == InventoryItemUseEffect.RestoreMana
                ? "魔法"
                : "生命";
            ShowToast(
                $"使用 {result.Item.DisplayName}，恢复 {result.ActualRestoredAmount} 点{resourceName}",
                result.Item.DisplayTint);
            return;
        }

        switch (result.FailureReason)
        {
            case InventoryUseFailureReason.ResourceAlreadyFull:
                bool isMana = result.Item != null &&
                    result.Item.UseEffect == InventoryItemUseEffect.RestoreMana;
                ShowToast(isMana ? "魔法值已满，未消耗药水" : "生命值已满，未消耗药水", Color.white);
                break;
            case InventoryUseFailureReason.NotUsable:
                ShowToast("该物品当前不能使用", new Color(1f, 0.78f, 0.42f, 1f));
                break;
            default:
                ShowToast("所选格子已经没有可使用的物品", new Color(1f, 0.62f, 0.42f, 1f));
                break;
        }
    }

    private void RefreshDetail(InventorySlotData slot)
    {
        bool hasItem = slot != null && !slot.IsEmpty;
        detailIcon.enabled = hasItem && slot.Item.Icon != null;
        detailIcon.sprite = hasItem ? slot.Item.Icon : null;
        detailIcon.color = hasItem ? slot.Item.DisplayTint : Color.white;

        if (useButton != null)
        {
            useButton.gameObject.SetActive(hasItem && slot.Item.IsUsable);
        }

        if (!hasItem)
        {
            detailNameText.text = "未选择物品";
            detailNameText.color = new Color(0.88f, 0.82f, 0.72f, 1f);
            detailMetaText.text = "点击左侧格子查看详情";
            detailCountText.text = string.Empty;
            detailDescriptionText.text = "击败小怪或 Boss 后拾取掉落物，可以获得物品。";
            return;
        }

        InventoryItemDefinition item = slot.Item;
        detailNameText.text = item.DisplayName;
        detailNameText.color = InventoryUiUtility.GetRarityColor(item.Rarity);
        detailMetaText.text = $"{InventoryUiUtility.GetRarityName(item.Rarity)} · {InventoryUiUtility.GetCategoryName(item.Category)}";
        detailCountText.text = $"数量：{slot.Count} / {item.MaxStack}";
        detailDescriptionText.text = string.IsNullOrWhiteSpace(item.Description)
            ? "暂无物品说明。"
            : item.Description;
    }

    private void ShowToast(string message, Color color)
    {
        if (toastRoot == null || toastText == null)
        {
            return;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastText.text = message;
        toastText.color = color;
        toastRoot.SetActive(true);
        toastRoot.transform.SetAsLastSibling();
        toastRoutine = StartCoroutine(HideToastAfterDelay());
    }

    private IEnumerator HideToastAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, toastDuration));
        if (toastRoot != null)
        {
            toastRoot.SetActive(false);
        }

        toastRoutine = null;
    }
}
