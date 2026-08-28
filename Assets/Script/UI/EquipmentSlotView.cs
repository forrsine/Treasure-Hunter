using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>单个穿戴槽表现：显示槽位占位图、装备图标、锁定与选中状态。</summary>
[DisallowMultipleComponent]
public sealed class EquipmentSlotView : MonoBehaviour
{
    [SerializeField] private EquipmentSlotType slotType;
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image placeholderImage;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private GameObject lockedState;

    private Action<EquipmentSlotType> onClicked;
    public EquipmentSlotType SlotType => slotType;

    public void Initialize(Action<EquipmentSlotType> clickHandler)
    {
        onClicked = clickHandler;
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    public void Refresh(InventoryItemDefinition item, bool selected, int playerLevel)
    {
        bool hasItem = item != null;
        if (iconImage != null)
        {
            iconImage.enabled = hasItem && item.Icon != null;
            iconImage.sprite = hasItem ? item.Icon : null;
            iconImage.color = hasItem ? item.DisplayTint : Color.white;
        }

        if (placeholderImage != null)
        {
            placeholderImage.enabled = !hasItem;
        }

        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }

        if (lockedState != null)
        {
            lockedState.SetActive(slotType == EquipmentSlotType.Ring && playerLevel < EquipmentSystem.RingUnlockLevel);
        }
    }

    private void HandleClick() => onClicked?.Invoke(slotType);
}
