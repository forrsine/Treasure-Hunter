namespace GameServer;

using System.Collections.Generic;

/// <summary>
/// 数据库角色记录，与 PlayerCharacters 表字段对应。
/// 这是持久化层对象，不直接等同于客户端协议对象。
/// </summary>
public sealed class TCharacter
{
    public long ID { get; set; }
    public long UserId { get; set; }
    public int SlotIndex { get; set; }
    public string Name { get; set; } = "";
    public int Class { get; set; }
    public int Level { get; set; }
    public int Exp { get; set; }
    public int PendingAttributeUpgradeCount { get; set; }
    public int VaultDestroyedCount { get; set; }
    public int CompletedBossCount { get; set; }
    public Dictionary<int, int> AttributeUpgradeCounts { get; } = new();
    public List<TInventoryItem> InventoryItems { get; } = new();
    public int TID { get; set; }
    public int MapID { get; set; } = 1;
    public long Gold { get; set; }
}

/// <summary>数据库中的一个角色背包格。</summary>
public sealed class TInventoryItem
{
    public int SlotIndex { get; set; }
    public string ItemId { get; set; } = "";
    public int Count { get; set; }
}
