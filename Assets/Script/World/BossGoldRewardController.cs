using QFramework;
using UnityEngine;

/// <summary>Boss 金币奖励：绑定本轮 Boss，死亡后生成一个重要金币掉落。</summary>
[DisallowMultipleComponent]
public sealed class BossGoldRewardController : MonoBehaviour, IController
{
    [SerializeField] private SpiderKingBossController boss;
    [SerializeField] private EconomyConfig economyConfig;
    [SerializeField] private float verticalOffset = 0.8f;

    private bool registered;
    private bool rewarded;
    private int completedBossCountAtBind;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void OnEnable()
    {
        if (boss != null)
        {
            BindBoss(boss);
        }
    }

    private void OnDisable()
    {
        Unregister();
    }

    public void BindBoss(SpiderKingBossController newBoss)
    {
        if (boss != newBoss)
        {
            Unregister();
            boss = newBoss;
        }

        rewarded = false;
        completedBossCountAtBind = Mathf.Max(0, BossRunProgressState.CompletedBossCount);
        if (boss != null && !registered)
        {
            boss.BossDied += HandleBossDied;
            registered = true;
        }
    }

    private void HandleBossDied(SpiderKingBossController deadBoss)
    {
        if (rewarded || deadBoss == null || deadBoss != boss)
        {
            return;
        }

        rewarded = true;
        EconomyConfig config = economyConfig != null ? economyConfig : this.GetSystem<EconomySystem>().Config;
        if (config == null || config.WorldGoldPickupPrefab == null)
        {
            Debug.LogWarning("Boss 金币掉落缺少 EconomyConfig 或金币 Prefab。", this);
            return;
        }

        int gold = config.CalculateBossGold(completedBossCountAtBind);
        Vector3 position = deadBoss.transform.position + Vector3.up * verticalOffset;
        WorldGoldPool.Instance.Get(config.WorldGoldPickupPrefab, gold, position, config.ImportantPickupLifetimeSeconds);
    }

    private void Unregister()
    {
        if (boss != null && registered)
        {
            boss.BossDied -= HandleBossDied;
        }

        registered = false;
    }
}
