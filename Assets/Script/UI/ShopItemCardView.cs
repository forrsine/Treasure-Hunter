using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店商品卡片：只负责把一个 ShopCatalogEntry 显示出来，并把点击回调交给商店面板。
/// 购买规则不放在卡片中，避免多个卡片各自维护一套扣款逻辑。
/// </summary>
[DisallowMultipleComponent]
public sealed class ShopItemCardView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text nameText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Text stateText;

    private ShopCatalogEntry currentEntry;
    private Action<ShopCatalogEntry> onClicked;

    public ShopCatalogEntry CurrentEntry => currentEntry;

    public void Initialize(Action<ShopCatalogEntry> clickHandler)
    {
        onClicked = clickHandler;
        selectButton.onClick.RemoveListener(HandleClicked);
        selectButton.onClick.AddListener(HandleClicked);
    }

    public void Bind(ShopCatalogEntry entry, bool soldOut)
    {
        currentEntry = entry;
        bool hasEntry = entry != null && entry.Item != null;
        gameObject.SetActive(hasEntry);
        if (!hasEntry)
        {
            return;
        }

        InventoryItemDefinition item = entry.Item;
        iconImage.sprite = item.Icon;
        iconImage.color = item.Icon != null ? item.DisplayTint : Color.clear;
        nameText.text = item.DisplayName;
        descriptionText.text = BuildDescription(item);
        priceText.text = $"{entry.Price:N0} 金币";
        stateText.text = soldOut ? "已售罄" : "购买";
        selectButton.interactable = !soldOut;
    }

    public bool ValidateReferences(bool logError)
    {
        string missing = null;
        if (selectButton == null) missing = nameof(selectButton);
        else if (iconImage == null) missing = nameof(iconImage);
        else if (nameText == null) missing = nameof(nameText);
        else if (descriptionText == null) missing = nameof(descriptionText);
        else if (priceText == null) missing = nameof(priceText);
        else if (stateText == null) missing = nameof(stateText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"商店商品卡片引用未配置：{missing}", this);
        }

        return false;
    }

    private void HandleClicked()
    {
        if (currentEntry != null)
        {
            onClicked?.Invoke(currentEntry);
        }
    }

    private static string BuildDescription(InventoryItemDefinition item)
    {
        if (!item.IsEquipment)
        {
            return item.Description;
        }

        var builder = new StringBuilder();
        EquipmentStatModifier[] modifiers = item.EquipmentStatModifiers;
        for (int i = 0; modifiers != null && i < modifiers.Length; i++)
        {
            if (builder.Length > 0)
            {
                builder.Append("  ");
            }

            builder.Append(GetStatName(modifiers[i].StatType));
            builder.Append(" +");
            builder.Append(FormatStatValue(modifiers[i]));
        }

        if (item.EquipmentSlot == EquipmentSlotType.Ring)
        {
            builder.Append(builder.Length > 0 ? "\n" : string.Empty);
            builder.Append("Lv.10 可装备");
        }

        return builder.Length > 0 ? builder.ToString() : item.Description;
    }

    private static string GetStatName(EquipmentStatType statType)
    {
        switch (statType)
        {
            case EquipmentStatType.Attack: return "攻击";
            case EquipmentStatType.MaxHp: return "生命";
            case EquipmentStatType.MaxMp: return "魔法";
            case EquipmentStatType.MoveSpeed: return "移速";
            case EquipmentStatType.CritChance: return "暴击";
            case EquipmentStatType.DodgeChance: return "闪避";
            case EquipmentStatType.DamageReduction: return "减伤";
            case EquipmentStatType.LifeSteal: return "吸血";
            default: return statType.ToString();
        }
    }

    private static string FormatStatValue(EquipmentStatModifier modifier)
    {
        switch (modifier.StatType)
        {
            case EquipmentStatType.CritChance:
            case EquipmentStatType.DodgeChance:
            case EquipmentStatType.DamageReduction:
            case EquipmentStatType.LifeSteal:
                return $"{modifier.Value * 100f:0.#}%";
            default:
                return $"{modifier.Value:0.##}";
        }
    }
}
