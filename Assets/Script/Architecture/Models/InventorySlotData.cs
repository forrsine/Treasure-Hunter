using System;

/// <summary>
/// 单个运行时背包格：只记录物品配置引用和当前数量。
/// 修改入口限制为 internal，外部 UI 只能读取，不能绕过 InventorySystem 直接改数量。
/// </summary>
[Serializable]
public sealed class InventorySlotData
{
    public InventoryItemDefinition Item { get; private set; }
    public int Count { get; private set; }
    public bool IsEmpty => Item == null || Count <= 0;

    internal void Set(InventoryItemDefinition item, int count)
    {
        Item = item;
        Count = item != null ? Math.Max(0, count) : 0;
        if (Count == 0)
        {
            Item = null;
        }
    }

    internal int AddUpToStackLimit(InventoryItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return 0;
        }

        if (!IsEmpty && !Item.IsSameItem(item))
        {
            return 0;
        }

        int space = IsEmpty ? item.MaxStack : Math.Max(0, Item.MaxStack - Count);
        int added = Math.Min(space, amount);
        if (added <= 0)
        {
            return 0;
        }

        Item = item;
        Count += added;
        return added;
    }

    internal int Remove(int amount)
    {
        if (IsEmpty || amount <= 0)
        {
            return 0;
        }

        int removed = Math.Min(Count, amount);
        Count -= removed;
        if (Count <= 0)
        {
            Clear();
        }

        return removed;
    }

    internal void Clear()
    {
        Item = null;
        Count = 0;
    }
}

/// <summary>
/// 一次加入背包的结算结果。调用方可以同时知道成功加入和因满包剩余的数量。
/// </summary>
public readonly struct InventoryAddResult
{
    public InventoryAddResult(InventoryItemDefinition item, int requestedAmount, int addedAmount)
    {
        Item = item;
        RequestedAmount = Math.Max(0, requestedAmount);
        AddedAmount = Math.Max(0, addedAmount);
    }

    public InventoryItemDefinition Item { get; }
    public int RequestedAmount { get; }
    public int AddedAmount { get; }
    public int RemainingAmount => Math.Max(0, RequestedAmount - AddedAmount);
    public bool AddedAnything => AddedAmount > 0;
    public bool AddedAll => AddedAmount == RequestedAmount && RequestedAmount > 0;
}
