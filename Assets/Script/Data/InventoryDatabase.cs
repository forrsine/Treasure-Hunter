using System;
using UnityEngine;

/// <summary>
/// 宝箱掉落项：权重决定被抽中的相对概率，数量范围决定单次加入背包的数量。
/// </summary>
[Serializable]
public sealed class VaultLootEntry
{
    [SerializeField] private InventoryItemDefinition item;
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField, Min(1)] private int minAmount = 1;
    [SerializeField, Min(1)] private int maxAmount = 1;

    public InventoryItemDefinition Item => item;
    public float Weight => Mathf.Max(0f, weight);
    public int MinAmount => Mathf.Max(1, minAmount);
    public int MaxAmount => Mathf.Max(MinAmount, maxAmount);
}

/// <summary>普通怪掉落项：普通怪先判定总掉率，成功后再按这里的权重选择具体物品。</summary>
[Serializable]
public sealed class MonsterLootEntry
{
    [SerializeField] private InventoryItemDefinition item;
    [SerializeField, Min(0f)] private float weight = 1f;

    public InventoryItemDefinition Item => item;
    public float Weight => Mathf.Max(0f, weight);
}

/// <summary>
/// Boss 掉落项：Boss 不复用小怪药水表，单独配置权重和数量，方便以后扩展专属材料、装备或任务道具。
/// </summary>
[Serializable]
public sealed class BossLootEntry
{
    [SerializeField] private InventoryItemDefinition item;
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField, Min(1)] private int minAmount = 1;
    [SerializeField, Min(1)] private int maxAmount = 1;

    public InventoryItemDefinition Item => item;
    public float Weight => Mathf.Max(0f, weight);
    public int MinAmount => Mathf.Max(1, minAmount);
    public int MaxAmount => Mathf.Max(MinAmount, maxAmount);
}

/// <summary>
/// 背包总配置：集中保存容量、可用物品、小怪掉落表和 Boss 掉落表。
/// 运行时规则读取这份配置，但不会修改 ScriptableObject，避免编辑器资源被玩法过程污染。
/// </summary>
[CreateAssetMenu(fileName = "InventoryDatabase", menuName = "Treasure Hunter/Inventory/Database")]
public sealed class InventoryDatabase : ScriptableObject
{
    public const string ResourcesPath = "Data/Inventory/InventoryDatabase";

    [SerializeField, Min(1)] private int capacity = 24;
    [SerializeField] private InventoryItemDefinition[] items = Array.Empty<InventoryItemDefinition>();
    [SerializeField] private VaultLootEntry[] vaultLootEntries = Array.Empty<VaultLootEntry>();
    [SerializeField, Range(0f, 1f)] private float monsterDropChance = 0.1f;
    [SerializeField] private MonsterLootEntry[] monsterLootEntries = Array.Empty<MonsterLootEntry>();
    [SerializeField] private GameObject worldPickupPrefab;
    [SerializeField, Min(1)] private int bossDropOrbCount = 3;
    [SerializeField] private BossLootEntry[] bossLootEntries = Array.Empty<BossLootEntry>();
    [SerializeField] private BossLootEntry[] bossEquipmentLootEntries = Array.Empty<BossLootEntry>();
    [SerializeField] private GameObject bossLootOrbPrefab;

    public int Capacity => Mathf.Max(1, capacity);
    public InventoryItemDefinition[] Items => items;
    public VaultLootEntry[] VaultLootEntries => vaultLootEntries;
    public float MonsterDropChance => Mathf.Clamp01(monsterDropChance);
    public MonsterLootEntry[] MonsterLootEntries => monsterLootEntries;
    public GameObject WorldPickupPrefab => worldPickupPrefab;
    public int BossDropOrbCount => Mathf.Max(1, bossDropOrbCount);
    public BossLootEntry[] BossLootEntries => bossLootEntries;
    public BossLootEntry[] BossEquipmentLootEntries => bossEquipmentLootEntries;
    public GameObject BossLootOrbPrefab => bossLootOrbPrefab;

    /// <summary>
    /// 根据稳定物品 ID 查找静态配置。
    /// 该入口主要用于存档恢复；背包只有 5 类物品，低频线性查询比额外维护运行时字典更直观。
    /// </summary>
    public bool TryGetItemById(string itemId, out InventoryItemDefinition item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(itemId) || items == null)
        {
            return false;
        }

        for (int i = 0; i < items.Length; i++)
        {
            InventoryItemDefinition candidate = items[i];
            if (candidate != null &&
                string.Equals(candidate.ItemId, itemId, StringComparison.Ordinal))
            {
                item = candidate;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 按权重抽取一次宝箱奖励。
    /// roll01 作为参数传入，既方便运行时使用 Random.value，也方便 EditMode 测试固定边界结果。
    /// </summary>
    public bool TryRollVaultLoot(float roll01, out InventoryItemDefinition item, out int amount)
    {
        item = null;
        amount = 0;
        if (vaultLootEntries == null || vaultLootEntries.Length == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < vaultLootEntries.Length; i++)
        {
            VaultLootEntry entry = vaultLootEntries[i];
            if (entry != null && entry.Item != null)
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float target = Mathf.Clamp(roll01, 0f, 0.999999f) * totalWeight;
        float accumulated = 0f;
        VaultLootEntry selected = null;
        for (int i = 0; i < vaultLootEntries.Length; i++)
        {
            VaultLootEntry entry = vaultLootEntries[i];
            if (entry == null || entry.Item == null || entry.Weight <= 0f)
            {
                continue;
            }

            accumulated += entry.Weight;
            selected = entry;
            if (target < accumulated)
            {
                break;
            }
        }

        if (selected == null)
        {
            return false;
        }

        item = selected.Item;
        amount = UnityEngine.Random.Range(selected.MinAmount, selected.MaxAmount + 1);
        return amount > 0;
    }

    /// <summary>
    /// 普通怪掉落使用两个独立随机值：dropRoll01 决定是否掉落，lootRoll01 决定掉哪一种。
    /// 随机值由调用方传入，EditMode 测试可以稳定覆盖 10% 和权重边界。
    /// </summary>
    public bool TryRollMonsterLoot(
        float dropRoll01,
        float lootRoll01,
        out InventoryItemDefinition item)
    {
        item = null;
        if (Mathf.Clamp01(dropRoll01) >= MonsterDropChance ||
            monsterLootEntries == null || monsterLootEntries.Length == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < monsterLootEntries.Length; i++)
        {
            MonsterLootEntry entry = monsterLootEntries[i];
            if (entry != null && entry.Item != null)
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float target = Mathf.Clamp(lootRoll01, 0f, 0.999999f) * totalWeight;
        float accumulated = 0f;
        for (int i = 0; i < monsterLootEntries.Length; i++)
        {
            MonsterLootEntry entry = monsterLootEntries[i];
            if (entry == null || entry.Item == null || entry.Weight <= 0f)
            {
                continue;
            }

            accumulated += entry.Weight;
            if (target < accumulated)
            {
                item = entry.Item;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Boss 掉落独立于小怪药水表和旧宝箱表。
    /// 好处是以后排查“某类掉落概率异常”时，可以直接定位到对应来源的配置。
    /// </summary>
    public bool TryRollBossLoot(float roll01, out InventoryItemDefinition item, out int amount)
    {
        item = null;
        amount = 0;
        if (bossLootEntries == null || bossLootEntries.Length == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < bossLootEntries.Length; i++)
        {
            BossLootEntry entry = bossLootEntries[i];
            if (entry != null && entry.Item != null)
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float target = Mathf.Clamp(roll01, 0f, 0.999999f) * totalWeight;
        float accumulated = 0f;
        BossLootEntry selected = null;
        for (int i = 0; i < bossLootEntries.Length; i++)
        {
            BossLootEntry entry = bossLootEntries[i];
            if (entry == null || entry.Item == null || entry.Weight <= 0f)
            {
                continue;
            }

            accumulated += entry.Weight;
            selected = entry;
            if (target < accumulated)
            {
                break;
            }
        }

        if (selected == null)
        {
            return false;
        }

        item = selected.Item;
        amount = UnityEngine.Random.Range(selected.MinAmount, selected.MaxAmount + 1);
        return amount > 0;
    }

    /// <summary>Boss 装备使用独立权重池，每次击杀额外必定尝试生成一个装备球。</summary>
    public bool TryRollBossEquipment(float roll01, out InventoryItemDefinition item)
    {
        item = null;
        if (bossEquipmentLootEntries == null || bossEquipmentLootEntries.Length == 0)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < bossEquipmentLootEntries.Length; i++)
        {
            BossLootEntry entry = bossEquipmentLootEntries[i];
            if (entry != null && entry.Item != null && entry.Item.IsEquipment)
            {
                totalWeight += entry.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        float target = Mathf.Clamp(roll01, 0f, 0.999999f) * totalWeight;
        float accumulated = 0f;
        for (int i = 0; i < bossEquipmentLootEntries.Length; i++)
        {
            BossLootEntry entry = bossEquipmentLootEntries[i];
            if (entry == null || entry.Item == null || !entry.Item.IsEquipment || entry.Weight <= 0f)
            {
                continue;
            }

            accumulated += entry.Weight;
            if (target < accumulated)
            {
                item = entry.Item;
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        capacity = Mathf.Max(1, capacity);
        monsterDropChance = Mathf.Clamp01(monsterDropChance);
        bossDropOrbCount = Mathf.Max(1, bossDropOrbCount);
    }
#endif
}
