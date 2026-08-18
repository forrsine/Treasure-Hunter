/// <summary>物品使用失败原因，UI 根据它显示明确反馈，但不参与具体恢复规则。</summary>
public enum InventoryUseFailureReason
{
    None,
    InvalidSlot,
    NotUsable,
    ResourceAlreadyFull
}

/// <summary>一次使用物品的同步结算结果。</summary>
public readonly struct InventoryUseResult
{
    public InventoryUseResult(
        InventoryItemDefinition item,
        bool succeeded,
        int actualRestoredAmount,
        InventoryUseFailureReason failureReason)
    {
        Item = item;
        Succeeded = succeeded;
        ActualRestoredAmount = actualRestoredAmount;
        FailureReason = failureReason;
    }

    public InventoryItemDefinition Item { get; }
    public bool Succeeded { get; }
    public int ActualRestoredAmount { get; }
    public InventoryUseFailureReason FailureReason { get; }
}
