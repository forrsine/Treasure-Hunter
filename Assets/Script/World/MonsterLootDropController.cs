using QFramework;
using UnityEngine;

/// <summary>
/// 普通怪掉落控制器：监听自身 SlimeCo 的正式死亡事件，负责掉落判定与地面物生成。
/// 怪物状态机只广播“已经死亡”，不会直接依赖背包、配置表或拾取对象池。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SlimeCo))]
public sealed class MonsterLootDropController : MonoBehaviour, IController
{
    [SerializeField] private InventoryDatabase inventoryDatabase;
    [SerializeField, Min(0f)] private float horizontalScatterRadius = 0.35f;
    [SerializeField] private float verticalOffset = 0.2f;
    [SerializeField, Min(1f)] private float pickupLifetimeSeconds = 45f;

    private SlimeCo monster;
    private bool droppedForCurrentLife;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        monster = GetComponent<SlimeCo>();
    }

    private void OnEnable()
    {
        droppedForCurrentLife = false;
        if (monster == null)
        {
            monster = GetComponent<SlimeCo>();
        }

        if (monster != null)
        {
            monster.Died -= HandleMonsterDied;
            monster.Died += HandleMonsterDied;
        }
    }

    private void OnDisable()
    {
        if (monster != null)
        {
            monster.Died -= HandleMonsterDied;
        }
    }

    private void HandleMonsterDied(SlimeCo deadMonster)
    {
        if (droppedForCurrentLife || deadMonster == null || deadMonster != monster)
        {
            return;
        }

        droppedForCurrentLife = true;
        InventoryDatabase database = inventoryDatabase != null
            ? inventoryDatabase
            : this.GetSystem<InventorySystem>().Database;
        if (database == null || database.WorldPickupPrefab == null)
        {
            Debug.LogWarning("普通怪掉落缺少 InventoryDatabase 或地面拾取 Prefab。", this);
            return;
        }

        if (!database.TryRollMonsterLoot(Random.value, Random.value, out InventoryItemDefinition item))
        {
            return;
        }

        Vector2 scatter = Random.insideUnitCircle * horizontalScatterRadius;
        Vector3 spawnPosition = deadMonster.transform.position +
            new Vector3(scatter.x, verticalOffset, scatter.y);
        WorldLootPool.Instance.Get(
            database.WorldPickupPrefab,
            item,
            1,
            spawnPosition,
            pickupLifetimeSeconds);
    }
}
