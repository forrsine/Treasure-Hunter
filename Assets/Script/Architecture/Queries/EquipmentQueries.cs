using System.Collections.Generic;
using QFramework;

public sealed class GetEquippedItemsQuery : AbstractQuery<IReadOnlyList<InventoryItemDefinition>>
{
    protected override IReadOnlyList<InventoryItemDefinition> OnDo() => this.GetModel<EquipmentModel>().CreateSnapshot();
}
