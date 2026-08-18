using QFramework;
using UnityEngine;

/// <summary>
/// Boss 掉落控制器：监听 Boss 的正式死亡事件，并在死亡点附近生成发光小球掉落物。
/// 注意：它只负责“死亡后生成世界掉落物”，拾取、满包和堆叠仍交给 WorldItemPickup 与 InventorySystem。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossLootDropController : MonoBehaviour, IController
{
    [SerializeField] private SpiderKingBossController boss;
    [SerializeField] private InventoryDatabase inventoryDatabase;
    [SerializeField] private GameObject bossLootOrbPrefab;
    [SerializeField, Min(0.2f)] private float horizontalScatterRadius = 1.8f;
    [SerializeField] private float verticalOffset = 0.35f;
    [SerializeField, Min(1f)] private float pickupLifetimeSeconds = 90f;

    private bool bossEventRegistered;
    private bool droppedForCurrentBoss;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void OnEnable()
    {
        TryAutoBindBoss();
        RegisterBossEventIfNeeded();
    }

    private void Start()
    {
        TryAutoBindBoss();
        RegisterBossEventIfNeeded();
    }

    private void OnDisable()
    {
        UnregisterBossEventIfNeeded();
    }

    private void OnValidate()
    {
        horizontalScatterRadius = Mathf.Max(0.2f, horizontalScatterRadius);
        pickupLifetimeSeconds = Mathf.Max(1f, pickupLifetimeSeconds);
    }

    /// <summary>
    /// BossRoomSceneBootstrap 创建或找到 Boss 后主动绑定，避免每帧通过名字查找。
    /// </summary>
    public void BindBoss(SpiderKingBossController newBoss)
    {
        if (boss == newBoss)
        {
            RegisterBossEventIfNeeded();
            return;
        }

        UnregisterBossEventIfNeeded();
        boss = newBoss;
        droppedForCurrentBoss = false;
        RegisterBossEventIfNeeded();
    }

    private void TryAutoBindBoss()
    {
        if (boss != null)
        {
            return;
        }

        boss = FindObjectOfType<SpiderKingBossController>();
    }

    private void RegisterBossEventIfNeeded()
    {
        if (boss == null || bossEventRegistered)
        {
            return;
        }

        boss.BossDied += HandleBossDied;
        bossEventRegistered = true;
    }

    private void UnregisterBossEventIfNeeded()
    {
        if (boss == null || !bossEventRegistered)
        {
            bossEventRegistered = false;
            return;
        }

        boss.BossDied -= HandleBossDied;
        bossEventRegistered = false;
    }

    private void HandleBossDied(SpiderKingBossController deadBoss)
    {
        if (droppedForCurrentBoss || deadBoss == null || deadBoss != boss)
        {
            return;
        }

        droppedForCurrentBoss = true;
        InventoryDatabase database = inventoryDatabase != null
            ? inventoryDatabase
            : this.GetSystem<InventorySystem>().Database;
        if (database == null)
        {
            Debug.LogWarning("Boss 掉落失败：缺少 InventoryDatabase。", this);
            return;
        }

        GameObject pickupPrefab = bossLootOrbPrefab != null
            ? bossLootOrbPrefab
            : database.BossLootOrbPrefab;
        if (pickupPrefab == null)
        {
            Debug.LogWarning("Boss 掉落失败：缺少 Boss 发光小球 Prefab。", this);
            return;
        }

        BossLootEntry[] bossLootEntries = database.BossLootEntries;
        if (bossLootEntries == null || bossLootEntries.Length == 0)
        {
            Debug.LogWarning("Boss 掉落失败：Boss 掉落表没有可用物品。", database);
            return;
        }

        int dropCount = Mathf.Min(database.BossDropOrbCount, bossLootEntries.Length);
        bool[] usedEntries = new bool[bossLootEntries.Length];
        for (int i = 0; i < dropCount; i++)
        {
            if (!TryRollBossLootWithoutReplacement(
                    bossLootEntries,
                    usedEntries,
                    Random.value,
                    out InventoryItemDefinition item,
                    out int amount))
            {
                Debug.LogWarning("Boss 掉落失败：Boss 掉落表没有可用物品。", database);
                return;
            }

            WorldLootPool.Instance.Get(
                pickupPrefab,
                item,
                amount,
                CalculateSpawnPosition(deadBoss.transform.position, i, dropCount),
                pickupLifetimeSeconds);
        }
    }

    /// <summary>
    /// 掉落物围绕 Boss 死亡点分散生成，避免多个小球完全重叠导致玩家看不清。
    /// </summary>
    private Vector3 CalculateSpawnPosition(Vector3 bossPosition, int index, int totalCount)
    {
        float safeTotal = Mathf.Max(1, totalCount);
        float angle = (360f / safeTotal) * index + Random.Range(-18f, 18f);
        Vector2 direction = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        float radius = Random.Range(horizontalScatterRadius * 0.45f, horizontalScatterRadius);
        Vector3 position = bossPosition + new Vector3(direction.x * radius, verticalOffset, direction.y * radius);

        float safeMargin = 2f;
        float halfWidth = BossRoomSceneBootstrap.DefaultArenaWidth * 0.5f - safeMargin;
        float halfLength = BossRoomSceneBootstrap.DefaultArenaLength * 0.5f - safeMargin;
        position.x = Mathf.Clamp(position.x, -halfWidth, halfWidth);
        position.z = Mathf.Clamp(position.z, -halfLength, halfLength);
        position.y = Mathf.Max(verticalOffset, position.y);
        return position;
    }

    /// <summary>
    /// Boss 一次死亡会掉多个光球，这里使用“不放回权重抽取”，避免同一轮掉落刷出多个同色同物品光球。
    /// </summary>
    private bool TryRollBossLootWithoutReplacement(
        BossLootEntry[] entries,
        bool[] usedEntries,
        float roll01,
        out InventoryItemDefinition item,
        out int amount)
    {
        item = null;
        amount = 0;
        if (entries == null || usedEntries == null || entries.Length != usedEntries.Length)
        {
            return false;
        }

        float totalWeight = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            BossLootEntry entry = entries[i];
            if (!usedEntries[i] && entry != null && entry.Item != null)
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
        for (int i = 0; i < entries.Length; i++)
        {
            BossLootEntry entry = entries[i];
            if (usedEntries[i] || entry == null || entry.Item == null || entry.Weight <= 0f)
            {
                continue;
            }

            accumulated += entry.Weight;
            if (target < accumulated)
            {
                usedEntries[i] = true;
                item = entry.Item;
                amount = Random.Range(entry.MinAmount, entry.MaxAmount + 1);
                return amount > 0;
            }
        }

        return false;
    }
}
