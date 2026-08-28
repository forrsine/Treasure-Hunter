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
            ["ancient_scroll"] = new InventoryItemRule(10, true),
            ["boss_iron_war_axe"] = new InventoryItemRule(1, true, 1),
            ["boss_moon_reaper"] = new InventoryItemRule(1, true, 1),
            ["boss_stoneplate_armor"] = new InventoryItemRule(1, true, 2),
            ["boss_crystalplate_armor"] = new InventoryItemRule(1, true, 2),
            ["boss_woodguard_shield"] = new InventoryItemRule(1, true, 3),
            ["boss_royal_shield"] = new InventoryItemRule(1, true, 3),
            ["boss_fang_gloves"] = new InventoryItemRule(1, true, 4),
            ["boss_bloodclaw_gloves"] = new InventoryItemRule(1, true, 4),
            ["boss_windleaf_boots"] = new InventoryItemRule(1, true, 5),
            ["boss_predator_boots"] = new InventoryItemRule(1, true, 5),
            ["boss_ruby_ring"] = new InventoryItemRule(1, true, 6),
            ["boss_tide_ring"] = new InventoryItemRule(1, true, 6),
            ["merchant_training_hammer"] = new InventoryItemRule(1, true, 1),
            ["merchant_traveler_armor"] = new InventoryItemRule(1, true, 2),
            ["merchant_oak_shield"] = new InventoryItemRule(1, true, 3),
            ["merchant_hunter_gloves"] = new InventoryItemRule(1, true, 4),
            ["merchant_lightstep_boots"] = new InventoryItemRule(1, true, 5),
            ["merchant_copper_ring"] = new InventoryItemRule(1, true, 6)
        };

    private static readonly HashSet<string> LimitedShopItemIds =
        new(StringComparer.Ordinal)
        {
            "merchant_training_hammer", "merchant_traveler_armor", "merchant_oak_shield",
            "merchant_hunter_gloves", "merchant_lightstep_boots", "merchant_copper_ring",
            "boss_iron_war_axe", "boss_stoneplate_armor", "boss_woodguard_shield",
            "boss_fang_gloves", "boss_windleaf_boots", "boss_ruby_ring"
        };

    public static bool TryGetRule(string itemId, out InventoryItemRule rule)
    {
        rule = default;
        return !string.IsNullOrWhiteSpace(itemId) && Rules.TryGetValue(itemId, out rule);
    }

    public static bool IsLimitedShopItem(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && LimitedShopItemIds.Contains(itemId);
    }
}

/// <summary>服务端验证背包条目时需要的最小静态规则。</summary>
internal readonly struct InventoryItemRule
{
    public InventoryItemRule(int maxStack, bool persistsAfterDeath, int equipmentSlot = 0)
    {
        MaxStack = maxStack;
        PersistsAfterDeath = persistsAfterDeath;
        EquipmentSlot = equipmentSlot;
    }

    public int MaxStack { get; }
    public bool PersistsAfterDeath { get; }
    public int EquipmentSlot { get; }
}
