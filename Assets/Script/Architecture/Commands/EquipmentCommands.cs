using System.Collections.Generic;
using QFramework;

public sealed class EquipInventoryItemCommand : AbstractCommand<EquipmentOperationResult>
{
    private readonly int slotIndex;
    public EquipInventoryItemCommand(int slotIndex) => this.slotIndex = slotIndex;
    protected override EquipmentOperationResult OnExecute() => this.GetSystem<EquipmentSystem>().EquipFromInventory(slotIndex);
}

public sealed class UnequipItemCommand : AbstractCommand<EquipmentOperationResult>
{
    private readonly EquipmentSlotType slot;
    public UnequipItemCommand(EquipmentSlotType slot) => this.slot = slot;
    protected override EquipmentOperationResult OnExecute() => this.GetSystem<EquipmentSystem>().Unequip(slot);
}

public sealed class RestoreEquipmentCommand : AbstractCommand
{
    private readonly IReadOnlyList<NEquippedItemSave> savedItems;
    public RestoreEquipmentCommand(IReadOnlyList<NEquippedItemSave> savedItems) => this.savedItems = savedItems;
    protected override void OnExecute() => this.GetSystem<EquipmentSystem>().RestoreEquipment(savedItems);
}

public sealed class ResetEquipmentCommand : AbstractCommand
{
    protected override void OnExecute() => this.GetSystem<EquipmentSystem>().ResetEquipment();
}
