using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个刷怪点。
/// 
/// 新手理解：
/// 1. 这个脚本挂在一个空物体上，空物体的位置就是刷怪中心。
/// 2. 生成出来的怪物会挂到这个刷怪点下面当子物体。
/// 3. 怪物死亡后会回收到 MonsterPool，所以活怪数量不能再简单依赖 childCount。
/// </summary>
public class MonsSpawner : MonoBehaviour
{
    /// <summary>
    /// 当前活着的怪物数量。
    /// 对象池会把回收后的怪物挂到 MonsterPool 节点下并隐藏，这里只统计当前刷怪点下仍然处于启用状态的 SlimeCo。
    /// 注意：这里使用 activeSelf，不使用 activeInHierarchy，避免刷怪点父物体未激活时 FillToMax 误判为 0 导致死循环。
    /// </summary>
    public int curliveNum
    {
        get
        {
            int liveCount = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.gameObject.activeSelf && child.GetComponent<SlimeCo>() != null)
                {
                    liveCount++;
                }
            }

            return liveCount;
        }
    }

    // 这个刷怪点最多同时存在多少只怪。
    public int maxNum = 3;

    // 每隔多少秒生成一只怪。
    public float spawnerTime = 7f;

    // 当前生成倒计时。
    public float curSpawnerTime;

    // 是否允许这个刷怪点工作。MonsterManager 会根据玩家进入/离开区域来开关它。
    public bool IsEnable;

    // 要生成的怪物预制体。
    public GameObject monsPrefab;

    [Header("出生点避让")]
    [SerializeField, Min(0.5f)] private float spawnRadius = 5f;
    [SerializeField, Min(0.1f)] private float spawnCheckRadius = 0.9f;
    [SerializeField, Min(1)] private int spawnPositionAttempts = 10;
    [SerializeField, Tooltip("刷怪时需要避开的层，默认包含 Player、Enemy 和 Box。")]
    private LayerMask spawnBlockLayers;

    /// <summary>
    /// 修正 Inspector 里可能填错的刷怪数值。
    /// </summary>
    private void Awake()
    {
        // 做一层数值保护，防止 Inspector 里填了负数导致逻辑异常。
        maxNum = Mathf.Max(0, maxNum);
        spawnerTime = Mathf.Max(0.1f, spawnerTime);
        curSpawnerTime = Mathf.Clamp(curSpawnerTime, 0f, spawnerTime);
        EnsureSpawnBlockLayerMask();
    }

    private void OnValidate()
    {
        maxNum = Mathf.Max(0, maxNum);
        spawnerTime = Mathf.Max(0.1f, spawnerTime);
        curSpawnerTime = Mathf.Clamp(curSpawnerTime, 0f, spawnerTime);
        spawnRadius = Mathf.Max(0.5f, spawnRadius);
        spawnCheckRadius = Mathf.Max(0.1f, spawnCheckRadius);
        spawnPositionAttempts = Mathf.Max(1, spawnPositionAttempts);
        EnsureSpawnBlockLayerMask();
    }

    /// <summary>
    /// 开启时按倒计时生成怪物，并遵守同时存在数量上限。
    /// </summary>
    void Update()
    {
        // 没开启时不刷怪。
        if (!IsEnable)
        {
            return;
        }

        // 达到上限时不再生成，等怪物死亡回收到对象池后数量变少。
        if (curliveNum >= maxNum)
        {
            return;
        }

        // 倒计时到 0 就生成一只，然后重置倒计时。
        curSpawnerTime -= Time.deltaTime;
        if (curSpawnerTime <= 0)
        {
            curSpawnerTime = spawnerTime;
            Spawner();
        }
    }

    /// <summary>
    /// 在刷怪点附近创建一只怪物，并挂到当前刷怪点下面。
    /// </summary>
    private bool Spawner()
    {
        if (monsPrefab == null)
        {
            Debug.LogWarning("MonsSpawner 刷怪失败：monsPrefab 没有在 Inspector 里拖入。", this);
            return false;
        }

        // 先找一个不会和玩家、箱子、其他怪物重叠的出生位置，再从对象池取出或创建怪物。
        // 怪物会挂到当前刷怪点下面，死亡回收后再移回 MonsterPool 节点。
        if (!TryFindSpawnPosition(out Vector3 spawnPosition))
        {
            Debug.LogWarning("MonsSpawner 刷怪跳过：附近没有找到足够安全的出生位置。", this);
            return false;
        }

        SlimeCo monster = MonsterPool.Instance.GetMonster(monsPrefab, spawnPosition, monsPrefab.transform.rotation, transform);
        return monster != null;
    }

    /// <summary>
    /// 多次随机候选位置，并用 Physics.CheckSphere 做轻量重叠检查。
    /// 这比生成后再把怪物推开更稳定，能减少一出生就叠在玩家、箱子或其他怪物身上的情况。
    /// </summary>
    private bool TryFindSpawnPosition(out Vector3 spawnPosition)
    {
        EnsureSpawnBlockLayerMask();
        for (int i = 0; i < spawnPositionAttempts; i++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            Vector3 candidate = transform.position + new Vector3(randomOffset.x, 0f, randomOffset.y);
            Vector3 checkCenter = candidate + Vector3.up * 0.75f;
            if (!Physics.CheckSphere(checkCenter, spawnCheckRadius, spawnBlockLayers, QueryTriggerInteraction.Ignore))
            {
                spawnPosition = candidate;
                return true;
            }
        }

        spawnPosition = transform.position;
        return false;
    }

    private void EnsureSpawnBlockLayerMask()
    {
        if (spawnBlockLayers.value != 0)
        {
            return;
        }

        spawnBlockLayers = LayerMask.GetMask("Player", "Enemy", "Box");
    }

    /// <summary>
    /// 立刻补怪到 maxNum。
    /// 适合开局或玩家刚进入区域时，让场上马上有怪。
    /// </summary>
    public void FillToMax()
    {
        int needSpawnCount = Mathf.Max(0, maxNum - curliveNum);
        for (int i = 0; i < needSpawnCount; i++)
        {
            if (!Spawner())
            {
                Debug.LogWarning("MonsSpawner 补怪中断：怪物生成失败，已停止本次 FillToMax，避免进入死循环。", this);
                break;
            }
        }

        curSpawnerTime = spawnerTime;
    }

    /// <summary>
    /// 停止这个刷怪点继续生成怪物。
    /// Boss 传送门开启后会调用这里，避免玩家准备进 Boss 房间时场景里继续刷普通怪。
    /// </summary>
    public void StopSpawning()
    {
        IsEnable = false;
        curSpawnerTime = spawnerTime;
    }

    /// <summary>
    /// 清理当前刷怪点下面仍然存活的普通怪。
    /// 这里直接销毁是因为马上要进入 Boss 流程，不需要再保留野外普通怪的运行时状态。
    /// </summary>
    public void ClearAliveMonsters()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
            {
                continue;
            }

            SlimeCo slime = child.GetComponent<SlimeCo>();
            if (slime == null)
            {
                continue;
            }

            Destroy(child.gameObject);
        }
    }

}
