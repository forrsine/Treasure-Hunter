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
    [Header("Equipment View")]
    [SerializeField] private EquipmentSlotView[] equipmentSlotViews;
    [SerializeField] private Image classIcon;
    [SerializeField] private Sprite[] classIcons;
    [SerializeField] private Text characterNameText;
    [SerializeField] private Text characterLevelText;
    [SerializeField] private Text finalStatsText;

    [Header("Loot Toast")]
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private Text toastText;
    [SerializeField, Min(0.1f)] private float toastDuration = 2.2f;

    private int selectedSlotIndex = -1;
    private EquipmentSlotType selectedEquipmentSlot = EquipmentSlotType.None;
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
        this.RegisterEvent<EquipmentChangedEvent>(HandleEquipmentChanged);
        this.RegisterEvent<PlayerStatsChangedEvent>(HandlePlayerStatsChanged);

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
        this.UnRegisterEvent<EquipmentChangedEvent>(HandleEquipmentChanged);
        this.UnRegisterEvent<PlayerStatsChangedEvent>(HandlePlayerStatsChanged);

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
        if (equipmentSlotViews != null)
        {
            for (int i = 0; i < equipmentSlotViews.Length; i++)
            {
                equipmentSlotViews[i]?.Initialize(HandleEquipmentSlotClicked);
            }
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
        else if (equipmentSlotViews == null || equipmentSlotViews.Length != 6) missing = nameof(equipmentSlotViews);
        else if (classIcon == null) missing = nameof(classIcon);
        else if (characterNameText == null) missing = nameof(characterNameText);
        else if (characterLevelText == null) missing = nameof(characterLevelText);
        else if (finalStatsText == null) missing = nameof(finalStatsText);
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
        sessionUi?.NotifyModalStateChanged();
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
        sessionUi?.NotifyModalStateChanged();
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
        sessionUi?.NotifyModalStateChanged();
    }

    private void HandleInventoryChanged(InventoryChangedEvent _)
    {
        RefreshView();
    }

    private void HandleEquipmentChanged(EquipmentChangedEvent _) => RefreshView();
    private void HandlePlayerStatsChanged(PlayerStatsChangedEvent _) => RefreshView();

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
        RefreshEquipmentView();
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

        InventorySlotData selectedSlot = selectedEquipmentSlot == EquipmentSlotType.None && selectedSlotIndex >= 0 && selectedSlotIndex < model.Slots.Count
            ? model.Slots[selectedSlotIndex]
            : null;
        RefreshDetail(selectedSlot, selectedEquipmentSlot);
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
        selectedEquipmentSlot = EquipmentSlotType.None;
        RefreshView();
    }

    private void HandleEquipmentSlotClicked(EquipmentSlotType slot)
    {
        selectedEquipmentSlot = slot;
        selectedSlotIndex = -1;
        RefreshView();
    }

    private void HandleUseButtonClicked()
    {
        if (selectedEquipmentSlot != EquipmentSlotType.None)
        {
            HandleEquipmentResult(this.SendCommand(new UnequipItemCommand(selectedEquipmentSlot)), false);
            return;
        }

        InventoryModel inventory = this.GetModel<InventoryModel>();
        if (selectedSlotIndex >= 0 && selectedSlotIndex < inventory.Slots.Count &&
            !inventory.Slots[selectedSlotIndex].IsEmpty && inventory.Slots[selectedSlotIndex].Item.IsEquipment)
        {
            HandleEquipmentResult(this.SendCommand(new EquipInventoryItemCommand(selectedSlotIndex)), true);
            return;
        }

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

    private void HandleEquipmentResult(EquipmentOperationResult result, bool equipping)
    {
        if (result.Succeeded)
        {
            ShowToast($"已{(equipping ? "装备" : "卸下")} {result.Item.DisplayName}", InventoryUiUtility.GetRarityColor(result.Item.Rarity));
            return;
        }

        string message = result.FailureReason == EquipmentOperationFailureReason.LevelLocked
            ? $"戒指槽需要达到 {EquipmentSystem.RingUnlockLevel} 级"
            : result.FailureReason == EquipmentOperationFailureReason.InventoryFull
                ? "背包已满，无法卸下装备"
                : "装备操作失败，请重新选择";
        ShowToast(message, new Color(1f, 0.65f, 0.35f, 1f));
    }

    private void RefreshDetail(InventorySlotData slot, EquipmentSlotType equippedSlot)
    {
        InventoryItemDefinition equippedItem = equippedSlot != EquipmentSlotType.None
            ? this.GetModel<EquipmentModel>().GetEquipped(equippedSlot)
            : null;
        bool hasItem = equippedItem != null || (slot != null && !slot.IsEmpty);
        InventoryItemDefinition selectedItem = equippedItem != null ? equippedItem : hasItem ? slot.Item : null;
        detailIcon.enabled = hasItem && selectedItem.Icon != null;
        detailIcon.sprite = hasItem ? selectedItem.Icon : null;
        detailIcon.color = hasItem ? selectedItem.DisplayTint : Color.white;

        if (useButton != null)
        {
            bool showAction = hasItem && (selectedItem.IsUsable || selectedItem.IsEquipment);
            useButton.gameObject.SetActive(showAction);
            Text label = useButton.GetComponentInChildren<Text>(true);
            if (label != null && showAction)
            {
                label.text = equippedItem != null ? "卸下" : selectedItem.IsEquipment ? "装备" : "使用";
            }
        }

        if (!hasItem)
        {
            detailNameText.text = "未选择物品";
            detailNameText.color = new Color(0.88f, 0.82f, 0.72f, 1f);
            detailMetaText.text = "点击背包或装备槽查看详情";
            detailCountText.text = string.Empty;
            detailDescriptionText.text = "击败小怪或 Boss 后拾取掉落物，可以获得物品。";
            return;
        }

        InventoryItemDefinition item = selectedItem;
        detailNameText.text = item.DisplayName;
        detailNameText.color = InventoryUiUtility.GetRarityColor(item.Rarity);
        detailMetaText.text = $"{InventoryUiUtility.GetRarityName(item.Rarity)} · {InventoryUiUtility.GetCategoryName(item.Category)}";
        detailCountText.text = equippedItem != null ? GetEquipmentSlotName(equippedSlot) : $"数量：{slot.Count} / {item.MaxStack}";
        InventoryItemDefinition comparison = item.IsEquipment && equippedItem == null
            ? this.GetModel<EquipmentModel>().GetEquipped(item.EquipmentSlot)
            : null;
        string modifierText = item.IsEquipment ? "\n\n" + BuildModifierText(item, comparison) : string.Empty;
        detailDescriptionText.text = (string.IsNullOrWhiteSpace(item.Description)
            ? "暂无物品说明。"
            : item.Description) + modifierText;
    }

    private void RefreshEquipmentView()
    {
        EquipmentModel equipment = this.GetModel<EquipmentModel>();
        PlayerModel player = this.GetModel<PlayerModel>();
        IPlayerStatsReadOnly stats = player.Stats;
        if (equipmentSlotViews != null)
        {
            for (int i = 0; i < equipmentSlotViews.Length; i++)
            {
                EquipmentSlotView view = equipmentSlotViews[i];
                view?.Refresh(equipment.GetEquipped(view.SlotType), view.SlotType == selectedEquipmentSlot, stats.Level);
            }
        }

        NCharacter character = player.CharacterSave;
        characterNameText.text = character != null ? character.name : "冒险者";
        characterLevelText.text = $"Lv.{stats.Level}";
        finalStatsText.text = $"攻击  {stats.AttackPower}\n生命  {stats.CurrentHp}/{stats.MaxHp}\n魔法  {stats.CurrentMp}/{stats.MaxMp}\n移速  {stats.CurrentMoveSpeed:0.00}\n暴击  {stats.CritChance * 100f:0.#}%\n闪避  {stats.DodgeChance * 100f:0.#}%\n减伤  {stats.DamageReduction * 100f:0.#}%\n吸血  {stats.LifeSteal * 100f:0.#}%";
        int classIndex = character != null ? character.classId - 1 : -1;
        if (classIcon != null && classIcons != null && classIndex >= 0 && classIndex < classIcons.Length)
        {
            classIcon.sprite = classIcons[classIndex];
            classIcon.enabled = classIcon.sprite != null;
        }
    }

    private static string BuildModifierText(InventoryItemDefinition item, InventoryItemDefinition comparison)
    {
        if (item.EquipmentStatModifiers == null) return string.Empty;
        var lines = new System.Text.StringBuilder("装备属性");
        for (int i = 0; i < item.EquipmentStatModifiers.Length; i++)
        {
            EquipmentStatModifier modifier = item.EquipmentStatModifiers[i];
            bool percent = modifier.StatType == EquipmentStatType.CritChance || modifier.StatType == EquipmentStatType.DodgeChance || modifier.StatType == EquipmentStatType.DamageReduction || modifier.StatType == EquipmentStatType.LifeSteal;
            lines.Append("\n+").Append(GetStatName(modifier.StatType)).Append(' ')
                .Append(percent ? (modifier.Value * 100f).ToString("0.#") + "%" : modifier.Value.ToString("0.##"));
        }
        if (comparison != null)
        {
            lines.Append("\n\n与当前装备对比");
            for (int typeValue = (int)EquipmentStatType.Attack; typeValue <= (int)EquipmentStatType.LifeSteal; typeValue++)
            {
                EquipmentStatType type = (EquipmentStatType)typeValue;
                float delta = GetModifierValue(item, type) - GetModifierValue(comparison, type);
                if (Mathf.Abs(delta) < 0.0001f) continue;
                bool percent = type == EquipmentStatType.CritChance || type == EquipmentStatType.DodgeChance || type == EquipmentStatType.DamageReduction || type == EquipmentStatType.LifeSteal;
                lines.Append("\n").Append(delta > 0f ? "+" : string.Empty).Append(GetStatName(type)).Append(' ')
                    .Append(percent ? (delta * 100f).ToString("0.#") + "%" : delta.ToString("0.##"));
            }
        }
        return lines.ToString();
    }

    private static float GetModifierValue(InventoryItemDefinition item, EquipmentStatType type)
    {
        float total = 0f;
        if (item?.EquipmentStatModifiers == null) return total;
        for (int i = 0; i < item.EquipmentStatModifiers.Length; i++)
        {
            if (item.EquipmentStatModifiers[i].StatType == type) total += item.EquipmentStatModifiers[i].Value;
        }
        return total;
    }

    private static string GetStatName(EquipmentStatType type)
    {
        switch (type)
        {
            case EquipmentStatType.Attack: return "攻击";
            case EquipmentStatType.MaxHp: return "生命";
            case EquipmentStatType.MaxMp: return "魔法";
            case EquipmentStatType.MoveSpeed: return "移速";
            case EquipmentStatType.CritChance: return "暴击";
            case EquipmentStatType.DodgeChance: return "闪避";
            case EquipmentStatType.DamageReduction: return "减伤";
            default: return "吸血";
        }
    }

    private static string GetEquipmentSlotName(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon: return "已穿戴 · 武器";
            case EquipmentSlotType.Armor: return "已穿戴 · 护甲";
            case EquipmentSlotType.Shield: return "已穿戴 · 盾牌";
            case EquipmentSlotType.Gloves: return "已穿戴 · 手套";
            case EquipmentSlotType.Boots: return "已穿戴 · 鞋子";
            default: return "已穿戴 · 戒指";
        }
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
