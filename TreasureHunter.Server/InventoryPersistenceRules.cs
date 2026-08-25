namespace GameServer;

using System;
using System.Collections.Generic;

/// <summary>
/// 服务端背包白名单：只保存客户端配置中真实存在的物品，并限制单格数量。
/// 当前掉落仍由客户端结算，因此这里负责结构和边界校验，不宣称验证物品的实际掉落来源。
/// </summary>
internal static class InventoryPersistenceRules
{
    public const int Capacity = 24;

    private static readonly Dictionary<string, InventoryItemRule> Rules =
        new(StringComparer.Ordinal)
        {
            ["healing_potion"] = new InventoryItemRule(20, false),
            ["mana_potion"] = new InventoryItemRule(20, false),
            ["experience_crystal"] = new InventoryItemRule(99, true),
            ["spider_king_core"] = new InventoryItemRule(10, true),
            ["ancient_scroll"] = new InventoryItemRule(10, true)
        };

    public static bool TryGetRule(string itemId, out InventoryItemRule rule)
    {
        rule = default;
        return !string.IsNullOrWhiteSpace(itemId) && Rules.TryGetValue(itemId, out rule);
    }
}

/// <summary>服务端验证背包条目时需要的最小静态规则。</summary>
internal readonly struct InventoryItemRule
{
    public InventoryItemRule(int maxStack, bool persistsAfterDeath)
    {
        MaxStack = maxStack;
        PersistsAfterDeath = persistsAfterDeath;
    }

    public int MaxStack { get; }
    public bool PersistsAfterDeath { get; }
}
