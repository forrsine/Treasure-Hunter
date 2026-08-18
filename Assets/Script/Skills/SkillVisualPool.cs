using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能表现对象池：负责复用技能特效对象，避免频繁 Instantiate / Destroy。
/// 第一版同时支持两类对象：
/// 1. 内置几何体表现，例如火球爆炸球、镰刀旋转圆盘。
/// 2. 空 GameObject 技能区域，例如毒雾持续区域。
/// </summary>
public sealed class SkillVisualPool : MonoBehaviour
{
    private static SkillVisualPool instance;

    public static SkillVisualPool Instance
    {
        get { return instance; }
    }

    // key 是对象名字，value 是这个类型对应的可复用对象队列。
    private readonly Dictionary<string, Queue<GameObject>> poolMap =
        new Dictionary<string, Queue<GameObject>>();

    // Prefab 特效可能处于“正在播放”或“已回收到队列”两种状态，集合同时追踪两者，便于卸载资源前完整清理。
    private readonly Dictionary<string, HashSet<GameObject>> prefabInstanceMap =
        new Dictionary<string, HashSet<GameObject>>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    /// <summary>
    /// 获取一个几何体表现对象。
    /// 用于火球爆炸、镰刀旋转这种临时可视化。
    /// </summary>
    public GameObject GetVisual(string visualName, PrimitiveType primitiveType)
    {
        Queue<GameObject> pool = GetOrCreatePool(visualName);

        GameObject visual;
        if (pool.Count > 0)
        {
            visual = pool.Dequeue();
        }
        else
        {
            visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = visualName;

            // 特效表现不参与碰撞，避免影响技能命中检测。
            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                Destroy(visualCollider);
            }
        }

        visual.transform.SetParent(null);
        visual.SetActive(true);
        return visual;
    }

    /// <summary>
    /// 获取一个空技能效果对象。
    /// 用于毒雾这种需要挂脚本、持续 Update 的技能区域。
    /// </summary>
    public GameObject GetEffectObject(string effectName)
    {
        Queue<GameObject> pool = GetOrCreatePool(effectName);

        GameObject effectObject;
        if (pool.Count > 0)
        {
            effectObject = pool.Dequeue();
        }
        else
        {
            effectObject = new GameObject(effectName);
        }

        effectObject.transform.SetParent(null);
        effectObject.SetActive(true);
        return effectObject;
    }

    /// <summary>
    /// 回收对象。
    /// 无论是几何体表现，还是持续技能区域，都可以用这个方法回收。
    /// </summary>
    public void ReleaseVisual(string visualName, GameObject visual)
    {
        if (visual == null)
        {
            return;
        }

        visual.SetActive(false);
        visual.transform.SetParent(transform);

        Queue<GameObject> pool = GetOrCreatePool(visualName);
        pool.Enqueue(visual);
    }


    /// <summary>
    /// 从对象池获取一个 Prefab 特效对象。
    /// 如果池子里已有回收对象就直接复用；没有时才实例化新的 Prefab。
    /// </summary>
    public GameObject GetPrefabVfx(string poolKey, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"获取技能特效失败：{poolKey} 的 Prefab 为空。");
            return null;
        }

        Queue<GameObject> pool = GetOrCreatePool(poolKey);

        GameObject vfx;
        if (pool.Count > 0)
        {
            vfx = pool.Dequeue();
        }
        else
        {
            vfx = Instantiate(prefab);
            vfx.name = poolKey;
            GetOrCreatePrefabInstanceSet(poolKey).Add(vfx);
        }

        vfx.transform.SetParent(null);
        vfx.SetActive(true);
        return vfx;
    }

    /// <summary>
    /// 回收 Prefab 技能特效。
    /// 回收时只隐藏对象并挂回池节点，不销毁对象，方便下一次释放技能复用。
    /// </summary>
    public void ReleasePrefabVfx(string poolKey, GameObject vfx)
    {
        if (vfx == null)
        {
            return;
        }

        vfx.SetActive(false);
        vfx.transform.SetParent(transform);

        Queue<GameObject> pool = GetOrCreatePool(poolKey);
        pool.Enqueue(vfx);
    }

    /// <summary>
    /// 销毁指定 Prefab 池中的全部实例，包括正在播放和已经回收的对象。
    /// Addressables 句柄释放前必须先调用这里，避免实例仍依赖即将卸载的资源。
    /// </summary>
    public void ClearPrefabVfxPool(string poolKey)
    {
        if (string.IsNullOrEmpty(poolKey))
        {
            return;
        }

        HashSet<GameObject> instancesToDestroy = new HashSet<GameObject>();

        if (prefabInstanceMap.TryGetValue(poolKey, out HashSet<GameObject> trackedInstances))
        {
            instancesToDestroy.UnionWith(trackedInstances);
            trackedInstances.Clear();
            prefabInstanceMap.Remove(poolKey);
        }

        // 兼容升级前已经进入队列、但尚未被实例集合记录的对象。
        if (poolMap.TryGetValue(poolKey, out Queue<GameObject> pool))
        {
            instancesToDestroy.UnionWith(pool);
            pool.Clear();
            poolMap.Remove(poolKey);
        }

        foreach (GameObject instanceObject in instancesToDestroy)
        {
            if (instanceObject != null)
            {
                Destroy(instanceObject);
            }
        }
    }

    private HashSet<GameObject> GetOrCreatePrefabInstanceSet(string poolKey)
    {
        if (!prefabInstanceMap.TryGetValue(poolKey, out HashSet<GameObject> instances))
        {
            instances = new HashSet<GameObject>();
            prefabInstanceMap.Add(poolKey, instances);
        }

        return instances;
    }

    private Queue<GameObject> GetOrCreatePool(string visualName)
    {
        if (!poolMap.TryGetValue(visualName, out Queue<GameObject> pool))
        {
            pool = new Queue<GameObject>();
            poolMap.Add(visualName, pool);
        }

        return pool;
    }

    private void OnDestroy()
    {
        prefabInstanceMap.Clear();
        poolMap.Clear();

        if (instance == this)
        {
            instance = null;
        }
    }
}
