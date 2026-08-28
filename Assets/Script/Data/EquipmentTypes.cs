using System;
using UnityEngine;

/// <summary>稳定装备槽编号：会写入游客存档、网络协议和服务端数据库，已有值不能改序号。</summary>
public enum EquipmentSlotType
{
    None = 0,
    Weapon = 1,
    Armor = 2,
    Shield = 3,
    Gloves = 4,
    Boots = 5,
    Ring = 6
}

/// <summary>装备可以提供的固定属性类型。</summary>
public enum EquipmentStatType
{
    Attack = 0,
    MaxHp = 1,
    MaxMp = 2,
    MoveSpeed = 3,
    CritChance = 4,
    DodgeChance = 5,
    DamageReduction = 6,
    LifeSteal = 7
}

[Serializable]
public struct EquipmentStatModifier
{
    [SerializeField] private EquipmentStatType statType;
    [SerializeField] private float value;

    public EquipmentStatType StatType => statType;
    public float Value => value;

    public EquipmentStatModifier(EquipmentStatType statType, float value)
    {
        this.statType = statType;
        this.value = value;
    }
}

/// <summary>
/// 当前整套装备的属性汇总。EquipmentSystem 每次从已穿戴槽重新计算总量，
/// 再用新旧差值更新玩家属性，避免反复穿脱造成数值漂移。
/// </summary>
public struct EquipmentBonusTotals
{
    public float Attack;
    public float MaxHp;
    public float MaxMp;
    public float MoveSpeed;
    public float CritChance;
    public float DodgeChance;
    public float DamageReduction;
    public float LifeSteal;

    public void Add(EquipmentStatModifier modifier)
    {
        switch (modifier.StatType)
        {
            case EquipmentStatType.Attack: Attack += modifier.Value; break;
            case EquipmentStatType.MaxHp: MaxHp += modifier.Value; break;
            case EquipmentStatType.MaxMp: MaxMp += modifier.Value; break;
            case EquipmentStatType.MoveSpeed: MoveSpeed += modifier.Value; break;
            case EquipmentStatType.CritChance: CritChance += modifier.Value; break;
            case EquipmentStatType.DodgeChance: DodgeChance += modifier.Value; break;
            case EquipmentStatType.DamageReduction: DamageReduction += modifier.Value; break;
            case EquipmentStatType.LifeSteal: LifeSteal += modifier.Value; break;
        }
    }
}

public enum EquipmentOperationFailureReason
{
    None,
    InvalidInventorySlot,
    NotEquipment,
    SlotMismatch,
    LevelLocked,
    InventoryFull,
    EmptyEquipmentSlot
}

public readonly struct EquipmentOperationResult
{
    public EquipmentOperationResult(bool succeeded, EquipmentOperationFailureReason failureReason,
        EquipmentSlotType slot, InventoryItemDefinition item)
    {
        Succeeded = succeeded;
        FailureReason = failureReason;
        Slot = slot;
        Item = item;
    }

    public bool Succeeded { get; }
    public EquipmentOperationFailureReason FailureReason { get; }
    public EquipmentSlotType Slot { get; }
    public InventoryItemDefinition Item { get; }
}
