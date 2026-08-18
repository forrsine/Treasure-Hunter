using System.Collections.Generic;
using QFramework;

/// <summary>
/// 背包运行时模型：维护当前角色会话中的权威格子数据。
/// 它不读取按键、不随机掉落、不操作 UI；所有写操作都由 InventorySystem 统一执行。
/// </summary>
public sealed class InventoryModel : AbstractModel
{
    public const int DefaultCapacity = 24;

    private readonly List<InventorySlotData> slots = new List<InventorySlotData>(DefaultCapacity);

    public IReadOnlyList<InventorySlotData> Slots => slots;
    public int Capacity => slots.Count;

    protected override void OnInit()
    {
        ConfigureCapacity(DefaultCapacity);
    }

    internal void ConfigureCapacity(int capacity)
    {
        int safeCapacity = capacity > 0 ? capacity : DefaultCapacity;
        while (slots.Count < safeCapacity)
        {
            slots.Add(new InventorySlotData());
        }

        // 只允许删除尾部空格，防止重新加载配置时静默丢失已有物品。
        while (slots.Count > safeCapacity && slots[slots.Count - 1].IsEmpty)
        {
            slots.RemoveAt(slots.Count - 1);
        }
    }

    internal void Clear()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].Clear();
        }
    }

    public int GetOccupiedSlotCount()
    {
        int occupied = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
            {
                occupied++;
            }
        }

        return occupied;
    }
}
