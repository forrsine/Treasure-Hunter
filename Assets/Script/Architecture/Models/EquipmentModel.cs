using System.Collections.Generic;
using QFramework;

/// <summary>
/// 穿戴栏运行时模型：只保存六个槽位当前引用，不操作背包、属性或 UI。
/// 写入口限定在 EquipmentSystem，避免 UI 绕过换装校验。
/// </summary>
public sealed class EquipmentModel : AbstractModel
{
    private readonly InventoryItemDefinition[] equippedItems = new InventoryItemDefinition[7];

    protected override void OnInit() { }

    public InventoryItemDefinition GetEquipped(EquipmentSlotType slot)
    {
        int index = (int)slot;
        return index > 0 && index < equippedItems.Length ? equippedItems[index] : null;
    }

    internal void SetEquipped(EquipmentSlotType slot, InventoryItemDefinition item)
    {
        int index = (int)slot;
        if (index > 0 && index < equippedItems.Length)
        {
            equippedItems[index] = item;
        }
    }

    internal void Clear()
    {
        System.Array.Clear(equippedItems, 0, equippedItems.Length);
    }

    public IReadOnlyList<InventoryItemDefinition> CreateSnapshot()
    {
        return (InventoryItemDefinition[])equippedItems.Clone();
    }
}
