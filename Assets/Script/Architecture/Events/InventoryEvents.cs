/// <summary>背包格子数据发生变化，UI 收到后统一刷新。</summary>
public readonly struct InventoryChangedEvent { }

/// <summary>成功获得物品，用于显示获得提示。</summary>
public readonly struct InventoryItemAddedEvent
{
    public InventoryItemAddedEvent(InventoryItemDefinition item, int addedAmount, int remainingAmount)
    {
        Item = item;
        AddedAmount = addedAmount;
        RemainingAmount = remainingAmount;
    }

    public InventoryItemDefinition Item { get; }
    public int AddedAmount { get; }
    public int RemainingAmount { get; }
}

/// <summary>背包容量不足，表现层可以提示本次未能加入的数量。</summary>
public readonly struct InventoryFullEvent
{
    public InventoryFullEvent(InventoryItemDefinition item, int remainingAmount)
    {
        Item = item;
        RemainingAmount = remainingAmount;
    }

    public InventoryItemDefinition Item { get; }
    public int RemainingAmount { get; }
}
