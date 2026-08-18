using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地面掉落物对象池：按拾取 Prefab 分队列复用，避免连续刷怪时频繁创建和销毁药水对象。
/// 它与怪物池分开，因为怪物和拾取物的配置、重置内容及生命周期不同。
/// </summary>
public sealed class WorldLootPool : MonoBehaviour
{
    private static WorldLootPool instance;

    public static WorldLootPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<WorldLootPool>();
            }

            if (instance == null)
            {
                GameObject poolObject = new GameObject("WorldLootPool");
                instance = poolObject.AddComponent<WorldLootPool>();
            }

            return instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<WorldItemPickup>> poolMap =
        new Dictionary<GameObject, Queue<WorldItemPickup>>();
    private readonly Dictionary<GameObject, Transform> poolRootMap =
        new Dictionary<GameObject, Transform>();
    private readonly HashSet<WorldItemPickup> pooledPickups = new HashSet<WorldItemPickup>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public WorldItemPickup Get(
        GameObject prefab,
        InventoryItemDefinition item,
        int amount,
        Vector3 position,
        float lifetimeSeconds)
    {
        if (prefab == null || item == null || amount <= 0)
        {
            return null;
        }

        Queue<WorldItemPickup> pool = GetOrCreatePool(prefab);
        WorldItemPickup pickup = null;
        while (pool.Count > 0 && pickup == null)
        {
            pickup = pool.Dequeue();
            if (pickup != null)
            {
                pooledPickups.Remove(pickup);
            }
        }

        if (pickup == null)
        {
            GameObject pickupObject = Instantiate(prefab, position, Quaternion.identity);
            pickup = pickupObject.GetComponent<WorldItemPickup>();
            if (pickup == null)
            {
                Debug.LogWarning($"地面掉落 Prefab {prefab.name} 缺少 WorldItemPickup。", prefab);
                Destroy(pickupObject);
                return null;
            }
        }
        else
        {
            pickup.transform.SetParent(null, true);
            pickup.transform.SetPositionAndRotation(position, Quaternion.identity);
            pickup.gameObject.SetActive(true);
        }

        pickup.Configure(this, prefab, item, amount, lifetimeSeconds);
        return pickup;
    }

    public void Release(GameObject prefab, WorldItemPickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        if (prefab == null)
        {
            Destroy(pickup.gameObject);
            return;
        }

        if (!pooledPickups.Add(pickup))
        {
            return;
        }

        pickup.PrepareRecycle();
        pickup.transform.SetParent(GetOrCreatePoolRoot(prefab), true);
        pickup.gameObject.SetActive(false);
        GetOrCreatePool(prefab).Enqueue(pickup);
    }

    private Queue<WorldItemPickup> GetOrCreatePool(GameObject prefab)
    {
        if (!poolMap.TryGetValue(prefab, out Queue<WorldItemPickup> pool))
        {
            pool = new Queue<WorldItemPickup>();
            poolMap.Add(prefab, pool);
        }

        return pool;
    }

    private Transform GetOrCreatePoolRoot(GameObject prefab)
    {
        if (poolRootMap.TryGetValue(prefab, out Transform root) && root != null)
        {
            return root;
        }

        GameObject rootObject = new GameObject($"{prefab.name}_Pool");
        rootObject.transform.SetParent(transform);
        root = rootObject.transform;
        poolRootMap[prefab] = root;
        return root;
    }
}
