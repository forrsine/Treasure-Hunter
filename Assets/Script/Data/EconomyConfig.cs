using UnityEngine;

/// <summary>金币产出配置：把怪物、金库和 Boss 的经济平衡从掉落脚本中移出。</summary>
[CreateAssetMenu(fileName = "EconomyConfig", menuName = "Treasure Hunter/Economy/Config")]
public sealed class EconomyConfig : ScriptableObject
{
    public const string ResourcesPath = "Data/Shop/EconomyConfig";

    [Header("普通怪直接到账")]
    [SerializeField, Min(0)] private int slimeOneMinGold = 1;
    [SerializeField, Min(0)] private int slimeOneMaxGold = 2;
    [SerializeField, Min(0)] private int slimeTwoMinGold = 2;
    [SerializeField, Min(0)] private int slimeTwoMaxGold = 3;

    [Header("金库地面金币")]
    [SerializeField, Min(0)] private int vaultBaseGold = 30;
    [SerializeField, Min(0)] private int vaultStepGold = 5;
    [SerializeField, Min(0)] private int vaultStepCap = 4;

    [Header("Boss 地面金币")]
    [SerializeField, Min(0)] private int bossBaseGold = 150;
    [SerializeField, Min(0)] private int bossStepGold = 25;
    [SerializeField, Min(0)] private int bossMaxGold = 300;

    [SerializeField] private GameObject worldGoldPickupPrefab;
    [SerializeField, Min(1f)] private float importantPickupLifetimeSeconds = 90f;

    public GameObject WorldGoldPickupPrefab => worldGoldPickupPrefab;
    public float ImportantPickupLifetimeSeconds => Mathf.Max(1f, importantPickupLifetimeSeconds);

    public int RollMonsterGold(SlimeCo.SlimeType slimeType, float roll01)
    {
        int min = slimeType == SlimeCo.SlimeType.Slime2 ? slimeTwoMinGold : slimeOneMinGold;
        int max = slimeType == SlimeCo.SlimeType.Slime2 ? slimeTwoMaxGold : slimeOneMaxGold;
        min = Mathf.Max(0, min);
        max = Mathf.Max(min, max);
        int range = max - min + 1;
        return min + Mathf.Min(range - 1, Mathf.FloorToInt(Mathf.Clamp01(roll01) * range));
    }

    public int CalculateVaultGold(int destroyedCountAfterBreak)
    {
        int step = Mathf.Clamp(destroyedCountAfterBreak - 1, 0, Mathf.Max(0, vaultStepCap));
        return Mathf.Max(0, vaultBaseGold) + Mathf.Max(0, vaultStepGold) * step;
    }

    public int CalculateBossGold(int completedBossCountBeforeReward)
    {
        int reward = Mathf.Max(0, bossBaseGold) + Mathf.Max(0, bossStepGold) * Mathf.Max(0, completedBossCountBeforeReward);
        return Mathf.Min(Mathf.Max(0, bossMaxGold), reward);
    }
}
