using QFramework;
using UnityEngine;

/// <summary>
/// 怪物死亡到任务系统的适配器：监听 SlimeCo 的实例死亡事件并发送任务命令。
/// 组件随对象池 OnEnable/OnDisable 成对订阅，避免复用后重复注册或清场销毁误计数。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SlimeCo))]
public sealed class MonsterQuestProgressReporter : MonoBehaviour, IController
{
    private SlimeCo monster;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        monster = GetComponent<SlimeCo>();
    }

    private void OnEnable()
    {
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
        if (deadMonster == monster)
        {
            this.SendCommand(new RecordMonsterDefeatedCommand(monster.MonsterKind));
        }
    }
}
