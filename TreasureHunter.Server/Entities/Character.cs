using Network;
using SkillBridge.Message;

namespace GameServer.Entities;

/// <summary>
/// 已进入游戏的角色实体：关联数据库记录与网络角色信息。
/// 数据库存档负责持久化，Info 负责发送给客户端，两者职责保持分离。
/// </summary>
public sealed class Character : IPostResponser
{
    public Character(CharacterType type, TCharacter data)
    {
        Data = data;
        Id = data.ID;
        Info = new NCharacterInfo { Type = type };
        ApplyPersistedData(data);
    }

    public long Id { get; }
    public TCharacter Data { get; private set; }
    public NCharacterInfo Info { get; }

    /// <summary>
    /// 在发送响应前给当前角色补充同步数据。
    /// 现在还是空实现，后续如果扩位置、血量、地图状态同步，可以从这里追加。
    /// </summary>
    public void PostProcess(NetMessageResponse message)
    {
    }

    /// <summary>
    /// 清理角色运行时资源。
    /// 当前版本角色运行时状态较少，所以暂时为空实现。
    /// </summary>
    public void Clear()
    {
    }

    /// <summary>
    /// 用数据库确认后的记录刷新在线实体，避免客户端缓存、Session 和数据库各持有一套不同进度。
    /// </summary>
    public void ApplyPersistedData(TCharacter data)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));

        Info.Id = checked((int)data.ID);
        Info.ConfigId = data.TID;
        Info.EntityId = checked((int)data.ID);
        Info.Name = data.Name;
        Info.Class = (CharacterClass)data.Class;
        Info.Level = data.Level;
        Info.mapId = data.MapID;
        Info.Gold = data.Gold;
        Info.SlotIndex = data.SlotIndex;
        Info.Exp = data.Exp;
        Info.PendingAttributeUpgradeCount = data.PendingAttributeUpgradeCount;
        Info.VaultDestroyedCount = data.VaultDestroyedCount;
        Info.CompletedBossCount = data.CompletedBossCount;
        Info.MerchantIntroCompleted = data.MerchantIntroCompleted;
        Info.AttributeUpgrades.Clear();
        Info.InventoryItems.Clear();
        Info.EquippedItems.Clear();
        Info.PurchasedLimitedShopItemIds.Clear();
        Info.QuestProgress.Clear();

        foreach ((int attributeType, int upgradeCount) in data.AttributeUpgradeCounts)
        {
            Info.AttributeUpgrades.Add(new NAttributeUpgradeInfo
            {
                AttributeType = attributeType,
                UpgradeCount = upgradeCount
            });
        }

        foreach (TInventoryItem item in data.InventoryItems)
        {
            Info.InventoryItems.Add(new NInventoryItemInfo
            {
                SlotIndex = item.SlotIndex,
                ItemId = item.ItemId,
                Count = item.Count
            });
        }

        foreach (TEquippedItem item in data.EquippedItems)
        {
            Info.EquippedItems.Add(new NEquippedItemInfo
            {
                EquipmentSlot = item.EquipmentSlot,
                ItemId = item.ItemId
            });
        }

        Info.PurchasedLimitedShopItemIds.AddRange(data.PurchasedLimitedShopItemIds);
        foreach (TQuestProgress progress in data.QuestProgress)
        {
            Info.QuestProgress.Add(new NQuestProgressInfo
            {
                QuestId = progress.QuestId,
                State = progress.State,
                CurrentCount = progress.CurrentCount
            });
        }
    }

    /// <summary>
    /// 返回一份新的基础信息 DTO，避免外部直接修改 Character 内部持有的 Info。
    /// </summary>
    public NCharacterInfo GetBasicInfo()
    {
        // 返回新的 DTO，避免调用方直接修改实体内部持有的 Info。
        var copy = new NCharacterInfo
        {
            Id = Info.Id,
            ConfigId = Info.ConfigId,
            Name = Info.Name,
            Type = Info.Type,
            Class = Info.Class,
            Level = Info.Level,
            Exp = Info.Exp,
            PendingAttributeUpgradeCount = Info.PendingAttributeUpgradeCount,
            VaultDestroyedCount = Info.VaultDestroyedCount,
            CompletedBossCount = Info.CompletedBossCount,
            mapId = Info.mapId,
            Gold = Info.Gold,
            SlotIndex = Info.SlotIndex,
            MerchantIntroCompleted = Info.MerchantIntroCompleted
        };

        foreach (NAttributeUpgradeInfo upgrade in Info.AttributeUpgrades)
        {
            copy.AttributeUpgrades.Add(new NAttributeUpgradeInfo
            {
                AttributeType = upgrade.AttributeType,
                UpgradeCount = upgrade.UpgradeCount
            });
        }

        foreach (NInventoryItemInfo item in Info.InventoryItems)
        {
            copy.InventoryItems.Add(new NInventoryItemInfo
            {
                SlotIndex = item.SlotIndex,
                ItemId = item.ItemId,
                Count = item.Count
            });
        }

        foreach (NEquippedItemInfo item in Info.EquippedItems)
        {
            copy.EquippedItems.Add(new NEquippedItemInfo
            {
                EquipmentSlot = item.EquipmentSlot,
                ItemId = item.ItemId
            });
        }

        copy.PurchasedLimitedShopItemIds.AddRange(Info.PurchasedLimitedShopItemIds);
        foreach (NQuestProgressInfo progress in Info.QuestProgress)
        {
            copy.QuestProgress.Add(new NQuestProgressInfo
            {
                QuestId = progress.QuestId,
                State = progress.State,
                CurrentCount = progress.CurrentCount
            });
        }

        return copy;
    }
}
