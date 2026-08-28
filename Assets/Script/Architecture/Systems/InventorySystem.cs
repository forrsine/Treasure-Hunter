using QFramework;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包规则系统：负责堆叠、占用空格、满包结算和会话重置。
/// 世界掉落、UI 和网络层都只能通过 Command 调用这里，避免出现多套不一致的加物品规则。
/// </summary>
public sealed class InventorySystem : AbstractSystem
{
    private InventoryModel model;

    public InventoryDatabase Database { get; private set; }

    protected override void OnInit()
    {
        model = this.GetModel<InventoryModel>();
        ConfigureDatabase(Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath));
    }

    /// <summary>
    /// 应用背包数据库。公开该入口主要用于 EditMode 测试和以后切换职业专属背包配置。
    /// </summary>
    public void ConfigureDatabase(InventoryDatabase database)
    {
        Database = database;
        model.ConfigureCapacity(database != null ? database.Capacity : InventoryModel.DefaultCapacity);
    }

    public InventoryAddResult TryAddItem(InventoryItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return new InventoryAddResult(item, amount, 0);
        }

        int remaining = amount;

        // 先填已有堆叠，避免背包里出现多个未满的同类格子。
        for (int i = 0; i < model.Slots.Count && remaining > 0; i++)
        {
            InventorySlotData slot = model.Slots[i];
            if (!slot.IsEmpty && slot.Item.IsSameItem(item))
            {
                remaining -= slot.AddUpToStackLimit(item, remaining);
            }
        }

        // 还有剩余时再依次占用空格，支持一次加入数量超过单格上限的情况。
        for (int i = 0; i < model.Slots.Count && remaining > 0; i++)
        {
            InventorySlotData slot = model.Slots[i];
            if (slot.IsEmpty)
            {
                remaining -= slot.AddUpToStackLimit(item, remaining);
            }
        }

        int added = amount - remaining;
        InventoryAddResult result = new InventoryAddResult(item, amount, added);
        if (result.AddedAnything)
        {
            this.SendEvent(new InventoryChangedEvent());
        }

        if (remaining > 0)
        {
            this.SendEvent(new InventoryFullEvent(item, remaining));
        }

        // 部分成功时把“获得数量 + 未加入数量”的组合提示放在最后，
        // 避免单独的满包提示覆盖更完整的结算信息。
        if (result.AddedAnything)
        {
            this.SendEvent(new InventoryItemAddedEvent(item, added, remaining));
        }

        return result;
    }

    /// <summary>
    /// 查询当前背包最多还能接收多少个指定物品，不修改数据也不广播事件。
    /// 地面拾取物用它做低频重试，避免背包已满时每个物理帧重复发送提示。
    /// </summary>
    public int GetAddableAmount(InventoryItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return 0;
        }

        int available = 0;
        for (int i = 0; i < model.Slots.Count && available < amount; i++)
        {
            InventorySlotData slot = model.Slots[i];
            if (slot.IsEmpty)
            {
                available += item.MaxStack;
            }
            else if (slot.Item.IsSameItem(item))
            {
                available += Mathf.Max(0, item.MaxStack - slot.Count);
            }
        }

        return Mathf.Min(amount, available);
    }

    /// <summary>
    /// 生成独立的背包存档快照。空格不写入存档，格子下标用于恢复原有排列。
    /// </summary>
    public List<NInventoryItemSave> CreateSaveSnapshot()
    {
        var result = new List<NInventoryItemSave>();
        for (int i = 0; i < model.Slots.Count; i++)
        {
            InventorySlotData slot = model.Slots[i];
            if (slot.IsEmpty || string.IsNullOrWhiteSpace(slot.Item.ItemId))
            {
                continue;
            }

            result.Add(new NInventoryItemSave
            {
                slotIndex = i,
                itemId = slot.Item.ItemId,
                count = slot.Count
            });
        }

        return result;
    }

    /// <summary>
    /// 使用权威存档重建运行时背包。
    /// 无效格子会被跳过而不是阻止角色进入，兼容以后删除或更名物品配置的情况。
    /// </summary>
    public void RestoreInventory(IReadOnlyList<NInventoryItemSave> savedItems)
    {
        model.Clear();
        var occupiedSlots = new HashSet<int>();

        if (savedItems != null)
        {
            for (int i = 0; i < savedItems.Count; i++)
            {
                NInventoryItemSave savedItem = savedItems[i];
                if (!TryValidateSavedItem(savedItem, occupiedSlots, out InventoryItemDefinition item))
                {
                    continue;
                }

                model.Slots[savedItem.slotIndex].Set(item, savedItem.count);
            }
        }

        // 批量恢复结束后只刷新一次 UI，避免逐格发送事件造成重复布局更新。
        this.SendEvent(new InventoryChangedEvent());
    }

    private bool TryValidateSavedItem(
        NInventoryItemSave savedItem,
        HashSet<int> occupiedSlots,
        out InventoryItemDefinition item)
    {
        item = null;
        if (savedItem == null ||
            savedItem.slotIndex < 0 || savedItem.slotIndex >= model.Slots.Count ||
            savedItem.count <= 0 ||
            !occupiedSlots.Add(savedItem.slotIndex))
        {
            Debug.LogWarning("背包存档包含无效或重复格子，已跳过该条数据。");
            return false;
        }

        if (Database == null || !Database.TryGetItemById(savedItem.itemId, out item))
        {
            Debug.LogWarning($"背包存档中的物品不存在，已跳过：{savedItem.itemId}");
            return false;
        }

        if (savedItem.count > item.MaxStack)
        {
            Debug.LogWarning($"背包物品数量超过单格上限，已跳过：{savedItem.itemId} x{savedItem.count}");
            item = null;
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从指定格子移除物品。消耗、丢弃和装备入口都应复用这里，不能让 UI 直接修改 Slot。
    /// </summary>
    public int TryRemoveItemAt(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= model.Slots.Count || amount <= 0)
        {
            return 0;
        }

        int removed = model.Slots[slotIndex].Remove(amount);
        if (removed > 0)
        {
            this.SendEvent(new InventoryChangedEvent());
        }

        return removed;
    }

    public void ResetInventory()
    {
        model.Clear();
        this.SendEvent(new InventoryChangedEvent());
    }

    /// <summary>
    /// 装备系统的原子交换入口：旧装备直接写回来源格，因此即使其余 23 格都满也能换装。
    /// 这里只修改数据，不发事件；EquipmentSystem 在两个模型都成功更新后统一广播。
    /// </summary>
    internal bool TryExchangeSingleItemAt(
        int slotIndex,
        InventoryItemDefinition expectedItem,
        InventoryItemDefinition replacementItem)
    {
        if (slotIndex < 0 || slotIndex >= model.Slots.Count)
        {
            return false;
        }

        InventorySlotData slot = model.Slots[slotIndex];
        if (slot.IsEmpty || slot.Count != 1 || !slot.Item.IsSameItem(expectedItem))
        {
            return false;
        }

        slot.Set(replacementItem, replacementItem != null ? 1 : 0);
        return true;
    }

    internal bool TryPlaceSingleItemInFirstEmptySlot(InventoryItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        for (int i = 0; i < model.Slots.Count; i++)
        {
            if (model.Slots[i].IsEmpty)
            {
                model.Slots[i].Set(item, 1);
                return true;
            }
        }

        return false;
    }

    internal void NotifyEquipmentTransactionCompleted()
    {
        this.SendEvent(new InventoryChangedEvent());
    }
}
