using System.Collections.Generic;
using UnityEngine;

/// <summary>金币地面拾取对象池，与物品掉落池分离，避免货币和背包物品共享错误状态。</summary>
public sealed class WorldGoldPool : MonoBehaviour
{
    private static WorldGoldPool instance;

    public static WorldGoldPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<WorldGoldPool>();
            }

            if (instance == null)
            {
                instance = new GameObject("WorldGoldPool").AddComponent<WorldGoldPool>();
            }

            return instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<WorldGoldPickup>> pools = new Dictionary<GameObject, Queue<WorldGoldPickup>>();
    private readonly Dictionary<GameObject, Transform> roots = new Dictionary<GameObject, Transform>();
    private readonly HashSet<WorldGoldPickup> pooledPickups = new HashSet<WorldGoldPickup>();

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

    public WorldGoldPickup Get(GameObject prefab, long amount, Vector3 position, float lifetimeSeconds)
    {
        if (prefab == null || amount <= 0L)
        {
            return null;
        }

        Queue<WorldGoldPickup> pool = GetOrCreatePool(prefab);
        WorldGoldPickup pickup = null;
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
            pickup = pickupObject.GetComponent<WorldGoldPickup>();
            if (pickup == null)
            {
                Debug.LogWarning($"金币掉落 Prefab {prefab.name} 缺少 WorldGoldPickup。", prefab);
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

        pickup.Configure(this, prefab, amount, lifetimeSeconds);
        return pickup;
    }

    public void Release(GameObject prefab, WorldGoldPickup pickup)
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
        pickup.transform.SetParent(GetOrCreateRoot(prefab), true);
        pickup.gameObject.SetActive(false);
        GetOrCreatePool(prefab).Enqueue(pickup);
    }

    private Queue<WorldGoldPickup> GetOrCreatePool(GameObject prefab)
    {
        if (!pools.TryGetValue(prefab, out Queue<WorldGoldPickup> pool))
        {
            pool = new Queue<WorldGoldPickup>();
            pools[prefab] = pool;
        }

        return pool;
    }

    private Transform GetOrCreateRoot(GameObject prefab)
    {
        if (roots.TryGetValue(prefab, out Transform root) && root != null)
        {
            return root;
        }

        root = new GameObject($"{prefab.name}_Pool").transform;
        root.SetParent(transform);
        roots[prefab] = root;
        return root;
    }
}
