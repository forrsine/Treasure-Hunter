using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个背包格子视图：只负责显示图标、数量、品质和选中状态，并把点击索引回传给面板。
/// 它不直接读取或修改 InventoryModel，避免 24 个格子各自订阅数据事件。
/// </summary>
[DisallowMultipleComponent]
public sealed class InventorySlotView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text countText;
    [SerializeField] private GameObject selectedFrame;

    private int slotIndex;
    private Action<int> onClicked;

    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (button == null) missing = nameof(button);
        else if (frameImage == null) missing = nameof(frameImage);
        else if (iconImage == null) missing = nameof(iconImage);
        else if (countText == null) missing = nameof(countText);
        else if (selectedFrame == null) missing = nameof(selectedFrame);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"InventorySlotView 引用未配置：{missing}。", this);
        }

        return false;
    }

    public void Initialize(int index, Action<int> clickHandler)
    {
        slotIndex = index;
        onClicked = clickHandler;
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(HandleClick);
        button.onClick.AddListener(HandleClick);
    }

    public void Refresh(InventorySlotData slot, bool selected)
    {
        bool hasItem = slot != null && !slot.IsEmpty;
        if (iconImage != null)
        {
            iconImage.enabled = hasItem && slot.Item.Icon != null;
            iconImage.sprite = hasItem ? slot.Item.Icon : null;
            iconImage.color = hasItem ? slot.Item.DisplayTint : Color.white;
        }

        if (countText != null)
        {
            countText.text = hasItem && slot.Count > 1 ? slot.Count.ToString() : string.Empty;
        }

        if (frameImage != null)
        {
            frameImage.color = hasItem
                ? InventoryUiUtility.GetRarityColor(slot.Item.Rarity)
                : new Color(0.65f, 0.58f, 0.52f, 0.78f);
        }

        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }
    }

    private void HandleClick()
    {
        onClicked?.Invoke(slotIndex);
    }
}

/// <summary>背包表现层的品质颜色和中文文案映射。</summary>
public static class InventoryUiUtility
{
    public static Color GetRarityColor(InventoryItemRarity rarity)
    {
        switch (rarity)
        {
            case InventoryItemRarity.Uncommon:
                return new Color(0.36f, 0.86f, 0.42f, 1f);
            case InventoryItemRarity.Rare:
                return new Color(0.35f, 0.62f, 1f, 1f);
            case InventoryItemRarity.Epic:
                return new Color(0.76f, 0.39f, 1f, 1f);
            default:
                return new Color(0.92f, 0.88f, 0.78f, 1f);
        }
    }

    public static string GetRarityName(InventoryItemRarity rarity)
    {
        switch (rarity)
        {
            case InventoryItemRarity.Uncommon: return "优秀";
            case InventoryItemRarity.Rare: return "稀有";
            case InventoryItemRarity.Epic: return "史诗";
            default: return "普通";
        }
    }

    public static string GetCategoryName(InventoryItemCategory category)
    {
        switch (category)
        {
            case InventoryItemCategory.Consumable: return "消耗品";
            case InventoryItemCategory.Quest: return "任务物品";
            default: return "材料";
        }
    }
}
