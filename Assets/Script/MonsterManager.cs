using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 刷怪区域管理器。
/// 
/// 新手理解：
/// 1. 这个脚本通常挂在一个带 Trigger Collider 的区域物体上。
/// 2. 玩家进入区域时开启刷怪点，玩家离开时关闭刷怪点。
/// 3. 真正生成怪物的逻辑在 MonsSpawner，这里只负责统一开关多个刷怪点。
/// </summary>
public class MonsterManager : MonoBehaviour
{
    // 这个区域控制的所有刷怪点，在 Inspector 里拖进来。
    public MonsSpawner[] monsSpawners;

    /// <summary>
    /// 开局启用刷怪点并补满一批怪，避免区域一开始是空的。
    /// </summary>
    void Start()
    {
        // 开局先启用并补满怪物，避免玩家进入后场景空着。
        SetSpawnersEnabled(true, fillToMax: true);
    }

    /// <summary>
    /// 当前管理器不需要每帧工作，保留入口方便后续扩展区域规则。
    /// </summary>
    void Update()
    {
        // 暂时没有每帧逻辑，保留方法不影响运行。
    }

    /// <summary>
    /// 玩家进入区域时开启刷怪。
    /// </summary>
    public void OnTriggerEnter(Collider other)
    {
        // CompareTag 比 other.tag == "Player" 更安全：如果标签不存在，Unity 会提示。
        if (other.gameObject.CompareTag("Player"))
        {
            SetSpawnersEnabled(true, fillToMax: false);
        }
    }
    /// <summary>
    /// 玩家离开区域时关闭刷怪。
    /// </summary>
    public void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            Debug.Log("tc");
            SetSpawnersEnabled(false, fillToMax: false);
        }
    }

    /// <summary>
    /// 批量开关本区域下所有刷怪点，并按需立即补怪。
    /// </summary>
    private void SetSpawnersEnabled(bool isEnable, bool fillToMax)
    {
        if (monsSpawners == null)
        {
            return;
        }

        // 遍历这个区域里的每个刷怪点，逐个设置启用状态。
        for (int i = 0; i < monsSpawners.Length; i++)
        {
            MonsSpawner spawner = monsSpawners[i];
            if (spawner == null)
            {
                continue;
            }

            spawner.IsEnable = isEnable;

            // fillToMax 只在需要“立刻补满”时使用，普通进入区域不一定要瞬间刷满。
            if (isEnable && fillToMax)
            {
                spawner.FillToMax();
            }
        }
    }

}
