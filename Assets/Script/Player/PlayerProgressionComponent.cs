using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerProgressionComponent : MonoBehaviour
{
    private const float MinUpgradeableThreshold = 0.0001f;

    private static readonly List<PlayerAttributeType> UpgradeChoiceBuffer = new List<PlayerAttributeType>(8);

    private PlayerCo owner;

    public void Initialize(PlayerCo player)
    {
        owner = player;
    }

    public void InitializeStatsFromConfig()
    {
        GameConfig config = GameConfig.instance;
        if (config != null)
        {
            owner.BaseMaxHpValue = config.GetPlayerBaseMaxHp();
            owner.BaseAttackPowerValue = config.GetPlayerBaseAttack();
            owner.BaseMoveSpeedValue = config.GetPlayerBaseMoveSpeed();
            owner.RunSpeedMultiplierValue = config.GetPlayerRunSpeedMultiplier();
            owner.CritChanceValue = config.playerBaseCritChance;
            owner.CritDamageMultiplierValue = Mathf.Max(1f, config.playerCritDamageMultiplier);
            owner.DodgeChanceValue = config.playerBaseDodgeChance;
            owner.HealthRegenPerSecondValue = Mathf.Max(0f, config.playerBaseHpRegenPerSecond);
            owner.DamageReductionValue = config.playerBaseDamageReduction;
            owner.LifeStealValue = config.playerBaseLifeSteal;
        }
        else
        {
            owner.BaseMaxHpValue = Mathf.Max(1, owner.BaseMaxHpValue);
            owner.BaseAttackPowerValue = Mathf.Max(1, owner.BaseAttackPowerValue);
            owner.BaseMoveSpeedValue = Mathf.Max(0.01f, owner.BaseMoveSpeedValue);
            owner.RunSpeedMultiplierValue = Mathf.Max(1f, owner.RunSpeedMultiplierValue);
            owner.CritDamageMultiplierValue = Mathf.Max(1f, owner.CritDamageMultiplierValue);
        }

        owner.BonusMaxHpValue = Mathf.Max(0, owner.BonusMaxHpValue);
        owner.AtkPower = Mathf.Max(1, owner.BaseAttackPowerValue);
        owner.SetMovementSpeeds(
            Mathf.Max(0.01f, owner.BaseMoveSpeedValue),
            Mathf.Max(0.01f, owner.BaseMoveSpeedValue) * owner.RunSpeedMultiplierValue);
        owner.RecalculateMaxHp(fillCurrentHp: true);
        owner.HealthRegenUpgradeCountValue = 0;
        owner.ResetPlayerRuntimeBuffers();
    }

    public void ApplyEntryCharacterStats()
    {
        CharacterDefine define = owner.EntryCharacterDefine;
        if (define != null)
        {
            if (define.hp > 0f)
            {
                owner.BaseMaxHpValue = Mathf.Max(1, Mathf.RoundToInt(define.hp));
            }

            if (define.attack > 0f)
            {
                owner.BaseAttackPowerValue = Mathf.Max(1, Mathf.RoundToInt(define.attack));
            }

            if (define.moveSpeed > 0f)
            {
                owner.BaseMoveSpeedValue = Mathf.Max(0.01f, define.moveSpeed);
            }

            owner.Lv = Mathf.Max(1, define.initLevel);
        }

        NCharacter save = owner.EntryCharacterSave;
        if (save != null)
        {
            owner.Lv = Mathf.Max(1, save.level);
            owner.curExp = Mathf.Max(0, save.exp);
        }

        owner.AtkPower = Mathf.Max(1, owner.BaseAttackPowerValue);
        owner.SetMovementSpeeds(
            Mathf.Max(0.01f, owner.BaseMoveSpeedValue),
            Mathf.Max(0.01f, owner.BaseMoveSpeedValue) * owner.RunSpeedMultiplierValue);
    }

    public void AddExp(int exp)
    {
        if (exp <= 0)
        {
            return;
        }

        FloatingCombatText.ShowExperience(transform, exp);
        owner.curExp += exp;
        DoLevelUp();
        owner.UpdateLvUI();
        owner.NotifyStatsChanged();
    }

    public void SetUpgradeSelectionState(bool active)
    {
        owner.IsUpgradeSelectionActiveValue = active;
    }

    public bool ResolvePendingUpgradeSelection(PlayerAttributeType attributeType)
    {
        if (owner.PendingUpgradeSelectionCountValue <= 0)
        {
            return false;
        }

        if (!TryApplyAttributeUpgrade(attributeType))
        {
            return false;
        }

        owner.PendingUpgradeSelectionCountValue = Mathf.Max(0, owner.PendingUpgradeSelectionCountValue - 1);
        owner.NotifyPendingUpgradeSelectionsChanged();
        return true;
    }

    public bool CanApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
            case PlayerAttributeType.MaxHp:
                return true;
            case PlayerAttributeType.HealthRegen:
                return GetNextHealthRegenUpgradeAmount() > MinUpgradeableThreshold;
            case PlayerAttributeType.MoveSpeed:
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                return GetCurrentMoveSpeedBonusPercent() + MinUpgradeableThreshold < moveSpeedCapPercent;
            case PlayerAttributeType.CritChance:
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                return owner.CritChanceValue + MinUpgradeableThreshold < critCap;
            case PlayerAttributeType.DodgeChance:
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                return owner.DodgeChanceValue + MinUpgradeableThreshold < dodgeCap;
            case PlayerAttributeType.DamageReduction:
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                return owner.DamageReductionValue + MinUpgradeableThreshold < damageReductionCap;
            case PlayerAttributeType.LifeSteal:
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                return owner.LifeStealValue + MinUpgradeableThreshold < lifeStealCap;
            default:
                return false;
        }
    }

    public bool TryApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        if (!CanApplyAttributeUpgrade(attributeType))
        {
            return false;
        }

        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                float attackUpgradePercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                owner.AtkPower = Mathf.Max(1, Mathf.CeilToInt(owner.AtkPower * (1f + attackUpgradePercent)));
                break;
            case PlayerAttributeType.MaxHp:
                int hpBonus = config != null ? config.playerMaxHpUpgradeFlat : 50;
                owner.BonusMaxHpValue += hpBonus;
                owner.RecalculateMaxHp(fillCurrentHp: false);
                owner.RecoverHp(hpBonus);
                break;
            case PlayerAttributeType.MoveSpeed:
                float moveSpeedUpgradePercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                float maxSpeed = owner.BaseMoveSpeedValue * (1f + moveSpeedCapPercent);
                float nextWalkSpeed = Mathf.Min(maxSpeed, owner.LegacyWalkSpeed * (1f + moveSpeedUpgradePercent));
                owner.SetMovementSpeeds(nextWalkSpeed, nextWalkSpeed * owner.RunSpeedMultiplierValue);
                break;
            case PlayerAttributeType.CritChance:
                float critUpgrade = config != null ? config.playerCritChanceUpgrade : 0.1f;
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                owner.CritChanceValue = Mathf.Min(critCap, owner.CritChanceValue + critUpgrade);
                break;
            case PlayerAttributeType.DodgeChance:
                float dodgeUpgrade = config != null ? config.playerDodgeChanceUpgrade : 0.1f;
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                owner.DodgeChanceValue = Mathf.Min(dodgeCap, owner.DodgeChanceValue + dodgeUpgrade);
                break;
            case PlayerAttributeType.HealthRegen:
                float regenUpgrade = GetNextHealthRegenUpgradeAmount();
                owner.HealthRegenPerSecondValue = Mathf.Min(GetHealthRegenCap(), owner.HealthRegenPerSecondValue + regenUpgrade);
                owner.HealthRegenUpgradeCountValue++;
                break;
            case PlayerAttributeType.DamageReduction:
                float damageReductionUpgrade = config != null ? config.playerDamageReductionUpgrade : 0.1f;
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                owner.DamageReductionValue = Mathf.Min(damageReductionCap, owner.DamageReductionValue + damageReductionUpgrade);
                break;
            case PlayerAttributeType.LifeSteal:
                float lifeStealUpgrade = config != null ? config.playerLifeStealUpgrade : 0.05f;
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                owner.LifeStealValue = Mathf.Min(lifeStealCap, owner.LifeStealValue + lifeStealUpgrade);
                break;
            default:
                return false;
        }

        owner.UpdateHpUI();
        owner.UpdateLvUI();
        owner.NotifyStatsChanged();
        return true;
    }

    public List<PlayerAttributeType> GetRandomUpgradeChoices(int choiceCount = 3)
    {
        UpgradeChoiceBuffer.Clear();

        PlayerAttributeType[] allTypes =
        {
            PlayerAttributeType.AttackPower,
            PlayerAttributeType.MaxHp,
            PlayerAttributeType.MoveSpeed,
            PlayerAttributeType.CritChance,
            PlayerAttributeType.DodgeChance,
            PlayerAttributeType.HealthRegen,
            PlayerAttributeType.DamageReduction,
            PlayerAttributeType.LifeSteal
        };

        List<PlayerAttributeType> availableTypes = new List<PlayerAttributeType>(allTypes.Length);
        for (int i = 0; i < allTypes.Length; i++)
        {
            if (CanApplyAttributeUpgrade(allTypes[i]))
            {
                availableTypes.Add(allTypes[i]);
            }
        }

        int resultCount = Mathf.Min(Mathf.Max(0, choiceCount), availableTypes.Count);
        for (int i = 0; i < resultCount; i++)
        {
            float totalWeight = 0f;
            for (int j = 0; j < availableTypes.Count; j++)
            {
                totalWeight += GetUpgradeWeight(availableTypes[j]);
            }

            if (totalWeight <= 0f)
            {
                break;
            }

            float roll = UnityEngine.Random.Range(0f, totalWeight);
            float accumulatedWeight = 0f;
            int selectedIndex = 0;

            for (int j = 0; j < availableTypes.Count; j++)
            {
                accumulatedWeight += GetUpgradeWeight(availableTypes[j]);
                if (roll <= accumulatedWeight)
                {
                    selectedIndex = j;
                    break;
                }
            }

            UpgradeChoiceBuffer.Add(availableTypes[selectedIndex]);
            availableTypes.RemoveAt(selectedIndex);
        }

        return new List<PlayerAttributeType>(UpgradeChoiceBuffer);
    }

    public string GetUpgradeOptionText(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        string title = config != null
            ? config.GetAttributeDisplayName(attributeType)
            : attributeType.ToString();
        string effect = GetUpgradeEffectText(attributeType);
        string preview = GetUpgradePreviewValueText(attributeType);
        string capText = config != null
            ? config.GetAttributeUpgradeCapText(attributeType)
            : string.Empty;

        return $"{title}\n{effect}\n{preview}\n{capText}";
    }

    private void DoLevelUp()
    {
        while (owner.Lv < owner.Lvmax && owner.curExp >= owner.GetNextExpForLevel(owner.Lv))
        {
            owner.curExp -= owner.GetNextExpForLevel(owner.Lv);
            owner.Lv++;
            owner.RecalculateMaxHp(fillCurrentHp: false);
            ApplyLevelUpRecovery();
            QueueUpgradeSelection();
        }

        if (owner.Lv >= owner.Lvmax)
        {
            owner.curExp = Mathf.Min(owner.curExp, owner.GetNextExpForLevel(owner.Lv));
        }

        owner.curExpMax = owner.GetNextExpForLevel(owner.Lv);
    }

    private void ApplyLevelUpRecovery()
    {
        GameConfig config = GameConfig.instance;
        float healPercent = config != null ? config.levelUpHealPercent : 0.3f;
        int minimumHeal = config != null ? config.minimumLevelUpHeal : 30;
        int healAmount = Mathf.Max(minimumHeal, Mathf.CeilToInt(owner.Hpmax * healPercent));
        owner.RecoverHp(healAmount);
    }

    private void QueueUpgradeSelection()
    {
        owner.PendingUpgradeSelectionCountValue++;
        owner.NotifyPendingUpgradeSelectionsChanged();
    }

    private string GetUpgradeEffectText(PlayerAttributeType attributeType)
    {
        if (attributeType == PlayerAttributeType.HealthRegen)
        {
            return $"+{GetNextHealthRegenUpgradeAmount():0.##}/s \u751f\u547d\u6062\u590d";
        }

        GameConfig config = GameConfig.instance;
        return config != null
            ? config.GetAttributeUpgradeEffectText(attributeType)
            : string.Empty;
    }

    private string GetUpgradePreviewValueText(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        switch (attributeType)
        {
            case PlayerAttributeType.AttackPower:
                float attackUpgradePercent = config != null ? config.playerAttackUpgradePercent : 0.3f;
                int nextAttack = Mathf.Max(1, Mathf.CeilToInt(owner.AtkPower * (1f + attackUpgradePercent)));
                return $"\u5f53\u524d {owner.AtkPower} -> {nextAttack}";
            case PlayerAttributeType.MaxHp:
                int hpBonus = config != null ? config.playerMaxHpUpgradeFlat : 50;
                return $"\u5f53\u524d {owner.Hpmax} -> {owner.Hpmax + hpBonus}";
            case PlayerAttributeType.MoveSpeed:
                float moveSpeedUpgradePercent = config != null ? config.playerMoveSpeedUpgradePercent : 0.15f;
                float moveSpeedCapPercent = config != null ? config.playerMoveSpeedUpgradeCapPercent : 0.6f;
                float maxSpeed = owner.BaseMoveSpeedValue * (1f + moveSpeedCapPercent);
                float nextMoveSpeed = Mathf.Min(maxSpeed, owner.LegacyWalkSpeed * (1f + moveSpeedUpgradePercent));
                return $"\u5f53\u524d {FormatDecimal(owner.LegacyWalkSpeed)} -> {FormatDecimal(nextMoveSpeed)}";
            case PlayerAttributeType.CritChance:
                float critUpgrade = config != null ? config.playerCritChanceUpgrade : 0.1f;
                float critCap = config != null ? config.playerCritChanceCap : 0.8f;
                return $"\u5f53\u524d {FormatPercent(owner.CritChanceValue)} -> {FormatPercent(Mathf.Min(critCap, owner.CritChanceValue + critUpgrade))}";
            case PlayerAttributeType.DodgeChance:
                float dodgeUpgrade = config != null ? config.playerDodgeChanceUpgrade : 0.1f;
                float dodgeCap = config != null ? config.playerDodgeChanceCap : 0.5f;
                return $"\u5f53\u524d {FormatPercent(owner.DodgeChanceValue)} -> {FormatPercent(Mathf.Min(dodgeCap, owner.DodgeChanceValue + dodgeUpgrade))}";
            case PlayerAttributeType.HealthRegen:
                float regenUpgrade = GetNextHealthRegenUpgradeAmount();
                return $"\u5f53\u524d {owner.HealthRegenPerSecondValue:0.##}/s -> {Mathf.Min(GetHealthRegenCap(), owner.HealthRegenPerSecondValue + regenUpgrade):0.##}/s";
            case PlayerAttributeType.DamageReduction:
                float damageReductionUpgrade = config != null ? config.playerDamageReductionUpgrade : 0.1f;
                float damageReductionCap = config != null ? config.playerDamageReductionCap : 0.7f;
                return $"\u5f53\u524d {FormatPercent(owner.DamageReductionValue)} -> {FormatPercent(Mathf.Min(damageReductionCap, owner.DamageReductionValue + damageReductionUpgrade))}";
            case PlayerAttributeType.LifeSteal:
                float lifeStealUpgrade = config != null ? config.playerLifeStealUpgrade : 0.05f;
                float lifeStealCap = config != null ? config.playerLifeStealCap : 0.5f;
                return $"\u5f53\u524d {FormatPercent(owner.LifeStealValue)} -> {FormatPercent(Mathf.Min(lifeStealCap, owner.LifeStealValue + lifeStealUpgrade))}";
            default:
                return string.Empty;
        }
    }

    private float GetUpgradeWeight(PlayerAttributeType attributeType)
    {
        GameConfig config = GameConfig.instance;
        return Mathf.Max(0f, config != null ? config.GetUpgradeBaseWeight(attributeType) : 1f);
    }

    private float GetNextHealthRegenUpgradeAmount()
    {
        GameConfig config = GameConfig.instance;
        float baseUpgrade = config != null ? config.playerHpRegenUpgrade : 1f;
        float exponentialUpgrade = baseUpgrade * Mathf.Pow(2f, Mathf.Max(0, owner.HealthRegenUpgradeCountValue));
        float remainingToCap = GetHealthRegenCap() - owner.HealthRegenPerSecondValue;

        return Mathf.Max(0f, Mathf.Min(exponentialUpgrade, remainingToCap));
    }

    private float GetHealthRegenCap()
    {
        GameConfig config = GameConfig.instance;
        return config != null ? Mathf.Max(0f, config.playerHpRegenCap) : 32f;
    }

    private float GetCurrentMoveSpeedBonusPercent()
    {
        if (owner.BaseMoveSpeedValue <= 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, owner.LegacyWalkSpeed / owner.BaseMoveSpeedValue - 1f);
    }

    private string FormatPercent(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private string FormatDecimal(float value)
    {
        return value.ToString("0.00");
    }
}
