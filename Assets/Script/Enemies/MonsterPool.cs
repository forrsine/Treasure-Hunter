using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物对象池：负责复用普通怪物 Prefab，减少反复 Instantiate / Destroy 带来的性能波动。
/// 注意：这个池子只管理怪物，不和技能特效对象池混用，避免不同生命周期的对象互相影响。
/// </summary>
public sealed class MonsterPool : MonoBehaviour
{
    private static MonsterPool instance;

    public static MonsterPool Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<MonsterPool>();
            }

            if (instance == null)
            {
                GameObject poolObject = new GameObject("MonsterPool");
                instance = poolObject.AddComponent<MonsterPool>();
            }

            return instance;
        }
    }

    private readonly Dictionary<GameObject, Queue<SlimeCo>> poolMap =
        new Dictionary<GameObject, Queue<SlimeCo>>();

    private readonly Dictionary<GameObject, Transform> poolRootMap =
        new Dictionary<GameObject, Transform>();

    // 记录已经进入池子的怪物，防止同一个对象被重复回收进队列。
    private readonly HashSet<SlimeCo> pooledMonsters = new HashSet<SlimeCo>();

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

    /// <summary>
    /// 从对象池获取一只怪物；池子里没有可复用对象时才创建新实例。
    /// 取出后会设置父物体、位置和旋转，并调用 SlimeCo.ResetEnemyForSpawn 重置运行时状态。
    /// </summary>
    public SlimeCo GetMonster(GameObject prefab, Vector3 position, Quaternion rotation, Transform activeParent)
    {
        if (prefab == null)
        {
            Debug.LogWarning("MonsterPool 获取怪物失败：Prefab 为空。");
            return null;
        }

        Queue<SlimeCo> pool = GetOrCreatePool(prefab);
        SlimeCo monster = null;

        while (pool.Count > 0 && monster == null)
        {
            monster = pool.Dequeue();
            if (monster != null)
            {
                pooledMonsters.Remove(monster);
            }
        }

        if (monster == null)
        {
            monster = CreateMonster(prefab, position, rotation, activeParent);
        }
        else
        {
            Transform monsterTransform = monster.transform;
            monsterTransform.SetParent(activeParent, true);
            monsterTransform.SetPositionAndRotation(position, rotation);
            monster.gameObject.SetActive(true);
        }

        if (monster == null)
        {
            return null;
        }

        monster.BindPool(this, prefab);
        monster.ResetEnemyForSpawn();
        return monster;
    }

    /// <summary>
    /// 回收怪物。
    /// 回收前先让 SlimeCo 清理自身状态，再隐藏并挂回池节点，等待下一次刷怪复用。
    /// </summary>
    public void ReleaseMonster(GameObject prefab, SlimeCo monster)
    {
        if (monster == null)
        {
            return;
        }

        if (prefab == null)
        {
            Destroy(monster.gameObject);
            return;
        }

        if (!pooledMonsters.Add(monster))
        {
            return;
        }

        monster.PrepareRecycle();

        Transform poolRoot = GetOrCreatePoolRoot(prefab);
        monster.transform.SetParent(poolRoot, true);
        monster.gameObject.SetActive(false);

        Queue<SlimeCo> pool = GetOrCreatePool(prefab);
        pool.Enqueue(monster);
    }

    private SlimeCo CreateMonster(GameObject prefab, Vector3 position, Quaternion rotation, Transform activeParent)
    {
        GameObject monsterObject = Instantiate(prefab, position, rotation, activeParent);
        SlimeCo monster = monsterObject.GetComponent<SlimeCo>();
        if (monster == null)
        {
            Debug.LogWarning($"MonsterPool 创建怪物失败：{prefab.name} 上没有 SlimeCo 组件。", prefab);
            Destroy(monsterObject);
            return null;
        }

        monsterObject.name = prefab.name;
        return monster;
    }

    private Queue<SlimeCo> GetOrCreatePool(GameObject prefab)
    {
        if (!poolMap.TryGetValue(prefab, out Queue<SlimeCo> pool))
        {
            pool = new Queue<SlimeCo>();
            poolMap.Add(prefab, pool);
        }

        return pool;
    }

    private Transform GetOrCreatePoolRoot(GameObject prefab)
    {
        if (poolRootMap.TryGetValue(prefab, out Transform poolRoot) && poolRoot != null)
        {
            return poolRoot;
        }

        GameObject poolRootObject = new GameObject($"{prefab.name}_Pool");
        poolRootObject.transform.SetParent(transform);
        poolRoot = poolRootObject.transform;
        poolRootMap[prefab] = poolRoot;
        return poolRoot;
    }
}
