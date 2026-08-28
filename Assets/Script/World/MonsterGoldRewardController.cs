using QFramework;
using UnityEngine;

/// <summary>普通怪金币奖励：监听正式死亡事件并直接写入钱包，不生成大量地面对象。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SlimeCo))]
public sealed class MonsterGoldRewardController : MonoBehaviour, IController
{
    [SerializeField] private EconomyConfig economyConfig;

    private SlimeCo monster;
    private bool rewardedForCurrentLife;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        monster = GetComponent<SlimeCo>();
    }

    private void OnEnable()
    {
        rewardedForCurrentLife = false;
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
        if (rewardedForCurrentLife || deadMonster == null || deadMonster != monster)
        {
            return;
        }

        rewardedForCurrentLife = true;
        EconomyConfig config = economyConfig != null ? economyConfig : this.GetSystem<EconomySystem>().Config;
        if (config == null)
        {
            Debug.LogWarning("普通怪金币奖励缺少 EconomyConfig。", this);
            return;
        }

        int gold = config.RollMonsterGold(monster.slimeType, Random.value);
        if (gold > 0)
        {
            this.SendCommand(new AddGoldCommand(gold));
        }
    }
}
