using System.Collections.Generic;

/// <summary>
/// 玩家成长存档快照：只携带数据库需要的长期数据，不包含当前血蓝、动画或场景引用。
/// </summary>
public sealed class PlayerProgressSaveData
{
    public int Level { get; set; }
    public int Exp { get; set; }
    public int PendingAttributeUpgradeCount { get; set; }
    public List<NAttributeUpgradeSave> AttributeUpgrades { get; } = new List<NAttributeUpgradeSave>();
    public List<NInventoryItemSave> InventoryItems { get; } = new List<NInventoryItemSave>();

    /// <summary>
    /// 主动重开只清理本局强化，不触碰等级、经验和关卡累计数据。
    /// 将规则放在存档快照上，便于网络发送前统一处理并单独测试。
    /// </summary>
    public void ClearRunUpgrades()
    {
        PendingAttributeUpgradeCount = 0;
        AttributeUpgrades.Clear();
    }

    /// <summary>
    /// 生成角色死亡后的固定成长快照。
    /// 这里仅重置角色成长字段；Boss 与宝箱累计由存档服务在同一次请求中传 0，
    /// 避免普通保存和死亡回档共用一组容易混淆的布尔规则。
    /// </summary>
    public void ResetAfterDeath(InventoryDatabase inventoryDatabase)
    {
        Level = 1;
        Exp = 0;
        PendingAttributeUpgradeCount = 0;
        AttributeUpgrades.Clear();

        // 药水属于本局战斗资源；材料和任务物品代表长期收集成果，死亡后继续保留。
        for (int i = InventoryItems.Count - 1; i >= 0; i--)
        {
            NInventoryItemSave savedItem = InventoryItems[i];
            bool shouldKeep = savedItem != null &&
                inventoryDatabase != null &&
                inventoryDatabase.TryGetItemById(savedItem.itemId, out InventoryItemDefinition item) &&
                item.Category != InventoryItemCategory.Consumable;
            if (!shouldKeep)
            {
                InventoryItems.RemoveAt(i);
            }
        }
    }
}
