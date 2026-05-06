using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个刷怪点。
/// 
/// 新手理解：
/// 1. 这个脚本挂在一个空物体上，空物体的位置就是刷怪中心。
/// 2. 生成出来的怪物会挂到这个刷怪点下面当子物体。
/// 3. curliveNum 直接用 childCount 统计当前还活着的怪物数量。
/// </summary>
public class MonsSpawner : MonoBehaviour
{
    /// <summary>
    /// 当前活着的怪物数量。
    /// 因为怪物生成后会成为本物体的子物体，所以 childCount 就是数量。
    /// </summary>
    public int curliveNum
    {
        get {
            return this.transform.childCount;
        } 
    }

    // 这个刷怪点最多同时存在多少只怪。
    public int maxNum = 3;

    // 每隔多少秒生成一只怪。
    public float spawnerTime;

    // 当前生成倒计时。
    public float curSpawnerTime;

    // 是否允许这个刷怪点工作。MonsterManager 会根据玩家进入/离开区域来开关它。
    public bool IsEnable;

    // 要生成的怪物预制体。
    public GameObject monsPrefab;

    /// <summary>
    /// 修正 Inspector 里可能填错的刷怪数值。
    /// </summary>
    private void Awake()
    {
        // 做一层数值保护，防止 Inspector 里填了负数导致逻辑异常。
        maxNum = Mathf.Max(0, maxNum);
        spawnerTime = Mathf.Max(0.1f, spawnerTime);
        curSpawnerTime = Mathf.Clamp(curSpawnerTime, 0f, spawnerTime);
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

        // 达到上限时不再生成，等怪物死亡销毁后 childCount 变少。
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
    private void Spawner()
    {
        if (monsPrefab == null)
        {
            return;
        }

        // 在刷怪点附近半径约 5 的圆形范围内随机一个位置。
        GameObject temp = Instantiate(monsPrefab);
        Vector2 vector2 = Random.insideUnitCircle * 5f;
        temp.transform.position = this.transform.position + new Vector3(vector2.x, 0f, vector2.y);

        // 设置父物体，方便 curliveNum 用 childCount 统计数量。
        temp.transform.parent = this.transform;
    }

    /// <summary>
    /// 立刻补怪到 maxNum。
    /// 适合开局或玩家刚进入区域时，让场上马上有怪。
    /// </summary>
    public void FillToMax()
    {
        while (curliveNum < maxNum)
        {
            Spawner();
        }

        curSpawnerTime = spawnerTime;
    }

}
