using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>获取玩家权威运行时数据的只读入口。</summary>
public sealed class GetPlayerStatsQuery : AbstractQuery<PlayerStatsSnapshot>
{
    protected override PlayerStatsSnapshot OnDo() => this.GetModel<PlayerModel>().CreateSnapshot();
}

/// <summary>生成独立的长期成长快照，网络层拿到的是副本而不是可写 Model。</summary>
public sealed class GetPlayerProgressSaveDataQuery : AbstractQuery<PlayerProgressSaveData>
{
    protected override PlayerProgressSaveData OnDo()
    {
        PlayerRuntimeStats stats = this.GetModel<PlayerModel>().MutableStats;
        var saveData = new PlayerProgressSaveData
        {
            Level = stats.Level,
            Exp = stats.CurrentExp,
            PendingAttributeUpgradeCount = stats.PendingUpgradeSelectionCount
        };

        for (int typeValue = (int)PlayerAttributeType.AttackPower;
             typeValue <= (int)PlayerAttributeType.LifeSteal;
             typeValue++)
        {
            PlayerAttributeType attributeType = (PlayerAttributeType)typeValue;
            int count = stats.GetAttributeUpgradeCount(attributeType);
            if (count <= 0)
            {
                continue;
            }

            saveData.AttributeUpgrades.Add(new NAttributeUpgradeSave
            {
                attributeType = typeValue,
                upgradeCount = count
            });
        }

        saveData.InventoryItems.AddRange(
            this.GetSystem<InventorySystem>().CreateSaveSnapshot());

        return saveData;
    }
}

/// <summary>
/// 判断当前玩家是否有足够魔法释放技能。
/// 这里只查询不扣蓝，真正扣蓝必须走 TrySpendPlayerManaCommand。
/// </summary>
public sealed class CanSpendPlayerManaQuery : AbstractQuery<bool>
{
    private readonly int amount;
    public CanSpendPlayerManaQuery(int amount) => this.amount = amount;
    protected override bool OnDo() => this.GetSystem<PlayerResourceSystem>().CanSpendMana(amount);
}

public sealed class GetPlayerUpgradeChoicesQuery : AbstractQuery<List<PlayerAttributeType>>
{
    private readonly int count;
    public GetPlayerUpgradeChoicesQuery(int count = 3) => this.count = count;
    protected override List<PlayerAttributeType> OnDo()
    {
        return this.GetSystem<PlayerProgressionSystem>().GetRandomUpgradeChoices(count);
    }
}

public sealed class GetPlayerUpgradeOptionTextQuery : AbstractQuery<string>
{
    private readonly PlayerAttributeType attributeType;
    public GetPlayerUpgradeOptionTextQuery(PlayerAttributeType attributeType) => this.attributeType = attributeType;
    protected override string OnDo()
    {
        return this.GetSystem<PlayerProgressionSystem>().GetUpgradeOptionText(attributeType);
    }
}

/// <summary>
/// 把 PlayerModel 转成 UI 需要的分组文本。Query 不保存 UI 引用，也不会修改玩家数据。
/// </summary>
public sealed class GetPlayerAttributeEntriesQuery : AbstractQuery<List<PlayerAttributeEntry>>
{
    protected override List<PlayerAttributeEntry> OnDo()
    {
        IPlayerStatsReadOnly stats = this.GetModel<PlayerModel>().Stats;
        GameConfig config = GameConfig.instance;
        string Name(PlayerAttributeType type, string fallback)
        {
            return config != null ? config.GetAttributeDisplayName(type) : fallback;
        }

        return new List<PlayerAttributeEntry>
        {
            new PlayerAttributeEntry("概览", "level", "等级", stats.Level.ToString()),
            new PlayerAttributeEntry("概览", "exp", "经验", $"{stats.CurrentExp}/{stats.ExpToNextLevel}"),
            new PlayerAttributeEntry("概览", "current_hp", "当前生命", $"{stats.CurrentHp}/{stats.MaxHp}"),
            new PlayerAttributeEntry("概览", "max_hp", Name(PlayerAttributeType.MaxHp, "最大生命"), stats.MaxHp.ToString()),
            new PlayerAttributeEntry("概览", "move_speed", Name(PlayerAttributeType.MoveSpeed, "移动速度"), stats.CurrentMoveSpeed.ToString("0.00")),
            new PlayerAttributeEntry("战斗", "attack_power", Name(PlayerAttributeType.AttackPower, "攻击力"), stats.AttackPower.ToString()),
            new PlayerAttributeEntry("战斗", "crit_chance", Name(PlayerAttributeType.CritChance, "暴击率"), Percent(stats.CritChance)),
            new PlayerAttributeEntry("战斗", "crit_damage", "暴击伤害", $"{stats.CritDamageMultiplier:0.00}x"),
            new PlayerAttributeEntry("生存", "dodge_chance", Name(PlayerAttributeType.DodgeChance, "闪避率"), Percent(stats.DodgeChance)),
            new PlayerAttributeEntry("生存", "health_regen", Name(PlayerAttributeType.HealthRegen, "生命恢复"), $"{stats.HealthRegenPerSecond:0.##}/s"),
            new PlayerAttributeEntry("生存", "damage_reduction", Name(PlayerAttributeType.DamageReduction, "伤害减免"), Percent(stats.DamageReduction)),
            new PlayerAttributeEntry("生存", "life_steal", Name(PlayerAttributeType.LifeSteal, "吸血"), Percent(stats.LifeSteal))
        };
    }

    private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";
}
