using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 装备业务系统：集中处理穿戴、原子交换、卸下、存档恢复和属性结算。
/// UI 只发送命令，不能直接写背包或玩家属性。
/// </summary>
public sealed class EquipmentSystem : AbstractSystem
{
    public const int RingUnlockLevel = 10;

    private EquipmentModel equipmentModel;
    private InventoryModel inventoryModel;
    private PlayerModel playerModel;
    private InventorySystem inventorySystem;
    private EquipmentBonusTotals appliedBonuses;

    public EquipmentBonusTotals CurrentBonuses => appliedBonuses;

    protected override void OnInit()
    {
        equipmentModel = this.GetModel<EquipmentModel>();
        inventoryModel = this.GetModel<InventoryModel>();
        playerModel = this.GetModel<PlayerModel>();
        inventorySystem = this.GetSystem<InventorySystem>();
    }

    public EquipmentOperationResult EquipFromInventory(int inventorySlotIndex)
    {
        if (inventorySlotIndex < 0 || inventorySlotIndex >= inventoryModel.Slots.Count ||
            inventoryModel.Slots[inventorySlotIndex].IsEmpty)
        {
            return Failure(EquipmentOperationFailureReason.InvalidInventorySlot);
        }

        InventoryItemDefinition item = inventoryModel.Slots[inventorySlotIndex].Item;
        if (item == null || !item.IsEquipment)
        {
            return Failure(EquipmentOperationFailureReason.NotEquipment, item: item);
        }

        EquipmentSlotType targetSlot = item.EquipmentSlot;
        if (targetSlot == EquipmentSlotType.Ring && playerModel.Stats.Level < RingUnlockLevel)
        {
            return Failure(EquipmentOperationFailureReason.LevelLocked, targetSlot, item);
        }

        InventoryItemDefinition oldEquipment = equipmentModel.GetEquipped(targetSlot);
        if (!inventorySystem.TryExchangeSingleItemAt(inventorySlotIndex, item, oldEquipment))
        {
            return Failure(EquipmentOperationFailureReason.InvalidInventorySlot, targetSlot, item);
        }

        equipmentModel.SetEquipped(targetSlot, item);
        CompleteTransaction(targetSlot);
        return new EquipmentOperationResult(true, EquipmentOperationFailureReason.None, targetSlot, item);
    }

    public EquipmentOperationResult Unequip(EquipmentSlotType slot)
    {
        InventoryItemDefinition item = equipmentModel.GetEquipped(slot);
        if (item == null)
        {
            return Failure(EquipmentOperationFailureReason.EmptyEquipmentSlot, slot);
        }

        // 先确认背包写入成功，再清空穿戴槽，失败时两个模型都保持原状态。
        if (!inventorySystem.TryPlaceSingleItemInFirstEmptySlot(item))
        {
            return Failure(EquipmentOperationFailureReason.InventoryFull, slot, item);
        }

        equipmentModel.SetEquipped(slot, null);
        CompleteTransaction(slot);
        return new EquipmentOperationResult(true, EquipmentOperationFailureReason.None, slot, item);
    }

    public List<NEquippedItemSave> CreateSaveSnapshot()
    {
        var result = new List<NEquippedItemSave>(6);
        for (int value = (int)EquipmentSlotType.Weapon; value <= (int)EquipmentSlotType.Ring; value++)
        {
            EquipmentSlotType slot = (EquipmentSlotType)value;
            InventoryItemDefinition item = equipmentModel.GetEquipped(slot);
            if (item != null && !string.IsNullOrWhiteSpace(item.ItemId))
            {
                result.Add(new NEquippedItemSave { equipmentSlot = value, itemId = item.ItemId });
            }
        }

        return result;
    }

    public void RestoreEquipment(IReadOnlyList<NEquippedItemSave> savedItems)
    {
        equipmentModel.Clear();
        var occupiedSlots = new HashSet<EquipmentSlotType>();

        if (savedItems != null && inventorySystem.Database != null)
        {
            for (int i = 0; i < savedItems.Count; i++)
            {
                NEquippedItemSave saved = savedItems[i];
                EquipmentSlotType slot = saved != null ? (EquipmentSlotType)saved.equipmentSlot : EquipmentSlotType.None;
                if (saved == null || slot <= EquipmentSlotType.None || slot > EquipmentSlotType.Ring ||
                    !occupiedSlots.Add(slot) ||
                    !inventorySystem.Database.TryGetItemById(saved.itemId, out InventoryItemDefinition item) ||
                    !item.IsEquipment || item.EquipmentSlot != slot)
                {
                    Debug.LogWarning($"已跳过无效装备存档：{saved?.itemId}");
                    continue;
                }

                equipmentModel.SetEquipped(slot, item);
            }
        }

        // 在线保存返回的权威装备会在玩法中热恢复，此时必须用新旧差值更新属性；
        // 首次进场尚未初始化玩家时则留给 InitializePlayerCommand 统一应用。
        if (playerModel.CharacterSave != null)
        {
            ApplyCalculatedBonuses(CalculateBonuses());
            this.SendEvent(new PlayerStatsChangedEvent());
        }

        this.SendEvent(new EquipmentChangedEvent(EquipmentSlotType.None));
    }

    public void ResetEquipment()
    {
        RemoveAppliedBonuses();
        equipmentModel.Clear();
        this.SendEvent(new EquipmentChangedEvent(EquipmentSlotType.None));
    }

    /// <summary>玩家 Reset 后旧属性已不存在，因此从零重新应用整套装备，不能再减旧值。</summary>
    public void ReapplyAfterPlayerReset()
    {
        appliedBonuses = default;
        ApplyCalculatedBonuses(CalculateBonuses());
    }

    private void CompleteTransaction(EquipmentSlotType changedSlot)
    {
        ApplyCalculatedBonuses(CalculateBonuses());
        inventorySystem.NotifyEquipmentTransactionCompleted();
        this.SendEvent(new EquipmentChangedEvent(changedSlot));
        this.SendEvent(new PlayerStatsChangedEvent());
    }

    private EquipmentBonusTotals CalculateBonuses()
    {
        EquipmentBonusTotals totals = default;
        for (int value = (int)EquipmentSlotType.Weapon; value <= (int)EquipmentSlotType.Ring; value++)
        {
            InventoryItemDefinition item = equipmentModel.GetEquipped((EquipmentSlotType)value);
            if (item == null || item.EquipmentStatModifiers == null)
            {
                continue;
            }

            for (int i = 0; i < item.EquipmentStatModifiers.Length; i++)
            {
                totals.Add(item.EquipmentStatModifiers[i]);
            }
        }

        return totals;
    }

    private void RemoveAppliedBonuses()
    {
        ApplyCalculatedBonuses(default);
    }

    private void ApplyCalculatedBonuses(EquipmentBonusTotals next)
    {
        PlayerRuntimeStats stats = playerModel.MutableStats;
        stats.AttackPower = Mathf.Max(1, stats.AttackPower - Mathf.RoundToInt(appliedBonuses.Attack) + Mathf.RoundToInt(next.Attack));
        stats.CurrentMoveSpeed = Mathf.Max(0.01f, stats.CurrentMoveSpeed - appliedBonuses.MoveSpeed + next.MoveSpeed);
        stats.CritChance = Mathf.Clamp01(stats.CritChance - appliedBonuses.CritChance + next.CritChance);
        stats.DodgeChance = Mathf.Clamp01(stats.DodgeChance - appliedBonuses.DodgeChance + next.DodgeChance);
        stats.DamageReduction = Mathf.Clamp(stats.DamageReduction - appliedBonuses.DamageReduction + next.DamageReduction, 0f, 0.95f);
        stats.LifeSteal = Mathf.Clamp01(stats.LifeSteal - appliedBonuses.LifeSteal + next.LifeSteal);

        stats.EquipmentMaxHpBonus = Mathf.Max(0, Mathf.RoundToInt(next.MaxHp));
        stats.EquipmentMaxMpBonus = Mathf.Max(0, Mathf.RoundToInt(next.MaxMp));
        playerModel.RecalculateMaxHp(false);
        playerModel.RecalculateMaxMp(false);
        appliedBonuses = next;
    }

    private static EquipmentOperationResult Failure(EquipmentOperationFailureReason reason,
        EquipmentSlotType slot = EquipmentSlotType.None, InventoryItemDefinition item = null)
    {
        return new EquipmentOperationResult(false, reason, slot, item);
    }
}
