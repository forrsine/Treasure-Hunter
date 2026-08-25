using QFramework;
using UnityEngine;

/// <summary>
/// 统一加物品命令：宝箱、任务和商店以后都可以复用，调用方不接触具体格子。
/// </summary>
public sealed class AddInventoryItemCommand : AbstractCommand<InventoryAddResult>
{
    private readonly InventoryItemDefinition item;
    private readonly int amount;

    public AddInventoryItemCommand(InventoryItemDefinition item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    protected override InventoryAddResult OnExecute()
    {
        return this.GetSystem<InventorySystem>().TryAddItem(item, amount);
    }
}

/// <summary>开始新的角色会话或登出时清空背包。</summary>
public sealed class ResetInventoryCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        this.GetSystem<InventorySystem>().ResetInventory();
    }
}

/// <summary>
/// 从角色存档恢复运行时背包。场景流程只传递数据，具体校验和格子写入仍由 InventorySystem 负责。
/// </summary>
public sealed class RestoreInventoryCommand : AbstractCommand
{
    private readonly System.Collections.Generic.IReadOnlyList<NInventoryItemSave> savedItems;

    public RestoreInventoryCommand(System.Collections.Generic.IReadOnlyList<NInventoryItemSave> savedItems)
    {
        this.savedItems = savedItems;
    }

    protected override void OnExecute()
    {
        this.GetSystem<InventorySystem>().RestoreInventory(savedItems);
    }
}

/// <summary>
/// 使用指定背包格中的一件消耗品。
/// Command 只负责协调背包系统与玩家资源系统；恢复公式和数量修改仍由各自 System 执行。
/// </summary>
public sealed class UseInventoryItemCommand : AbstractCommand<InventoryUseResult>
{
    private readonly int slotIndex;

    public UseInventoryItemCommand(int slotIndex)
    {
        this.slotIndex = slotIndex;
    }

    protected override InventoryUseResult OnExecute()
    {
        InventoryModel inventory = this.GetModel<InventoryModel>();
        if (slotIndex < 0 || slotIndex >= inventory.Slots.Count || inventory.Slots[slotIndex].IsEmpty)
        {
            return new InventoryUseResult(null, false, 0, InventoryUseFailureReason.InvalidSlot);
        }

        InventoryItemDefinition item = inventory.Slots[slotIndex].Item;
        if (item == null || !item.IsUsable)
        {
            return new InventoryUseResult(item, false, 0, InventoryUseFailureReason.NotUsable);
        }

        IPlayerStatsReadOnly stats = this.GetModel<PlayerModel>().Stats;
        int actualRestoredAmount = 0;
        switch (item.UseEffect)
        {
            case InventoryItemUseEffect.RestoreHealth:
                int requestedHealth = Mathf.CeilToInt(stats.MaxHp * item.RestorePercent);
                actualRestoredAmount = this.GetSystem<PlayerCombatSystem>().Heal(requestedHealth, true);
                break;
            case InventoryItemUseEffect.RestoreMana:
                int requestedMana = Mathf.CeilToInt(stats.MaxMp * item.RestorePercent);
                actualRestoredAmount = this.GetSystem<PlayerResourceSystem>().RestoreMana(requestedMana);
                break;
        }

        // 满血、满蓝或玩家死亡时实际恢复量为 0，此时必须保留药水。
        if (actualRestoredAmount <= 0)
        {
            return new InventoryUseResult(
                item,
                false,
                0,
                InventoryUseFailureReason.ResourceAlreadyFull);
        }

        int removed = this.GetSystem<InventorySystem>().TryRemoveItemAt(slotIndex, 1);
        return new InventoryUseResult(
            item,
            removed == 1,
            actualRestoredAmount,
            removed == 1 ? InventoryUseFailureReason.None : InventoryUseFailureReason.InvalidSlot);
    }
}
