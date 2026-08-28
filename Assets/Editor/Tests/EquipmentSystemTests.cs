#if UNITY_EDITOR
using NUnit.Framework;
using QFramework;
using UnityEngine;

/// <summary>装备规则专项测试：覆盖空槽穿戴、同槽原子交换、等级锁和属性回滚。</summary>
public sealed class EquipmentSystemTests
{
    private IArchitecture architecture;
    private InventoryDatabase database;
    private InventorySystem inventory;
    private EquipmentSystem equipment;
    private InventoryModel inventoryModel;
    private EquipmentModel equipmentModel;

    [SetUp]
    public void SetUp()
    {
        architecture = TreasureHunterArchitecture.Interface;
        database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);
        Assert.That(database, Is.Not.Null);
        inventory = architecture.GetSystem<InventorySystem>();
        equipment = architecture.GetSystem<EquipmentSystem>();
        inventoryModel = architecture.GetModel<InventoryModel>();
        equipmentModel = architecture.GetModel<EquipmentModel>();
        inventory.ConfigureDatabase(database);
        inventory.ResetInventory();
        equipment.ResetEquipment();
        InitializePlayer(10);
    }

    [TearDown]
    public void TearDown()
    {
        equipment.ResetEquipment();
        inventory.ResetInventory();
    }

    [Test]
    public void EquipIntoEmptySlot_RemovesBagItemAndAppliesStats()
    {
        InventoryItemDefinition axe = GetItem("boss_iron_war_axe");
        int attackBefore = architecture.SendQuery(new GetPlayerStatsQuery()).AttackPower;
        inventory.TryAddItem(axe, 1);

        EquipmentOperationResult result = equipment.EquipFromInventory(0);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(inventoryModel.Slots[0].IsEmpty, Is.True);
        Assert.That(equipmentModel.GetEquipped(EquipmentSlotType.Weapon), Is.SameAs(axe));
        Assert.That(architecture.SendQuery(new GetPlayerStatsQuery()).AttackPower, Is.EqualTo(attackBefore + 12));
    }

    [Test]
    public void EquipSameSlot_WhenBagHasNoOtherSpace_AtomicallyReturnsOldItemToSourceSlot()
    {
        InventoryItemDefinition oldAxe = GetItem("boss_iron_war_axe");
        InventoryItemDefinition newAxe = GetItem("boss_moon_reaper");
        inventory.TryAddItem(oldAxe, 1);
        Assert.That(equipment.EquipFromInventory(0).Succeeded, Is.True);
        inventory.TryAddItem(newAxe, 1);
        InventoryItemDefinition material = GetItem("experience_crystal");
        inventory.TryAddItem(material, material.MaxStack * (InventoryModel.DefaultCapacity - 1));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.EqualTo(InventoryModel.DefaultCapacity));

        EquipmentOperationResult result = equipment.EquipFromInventory(0);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(equipmentModel.GetEquipped(EquipmentSlotType.Weapon), Is.SameAs(newAxe));
        Assert.That(inventoryModel.Slots[0].Item, Is.SameAs(oldAxe));
    }

    [Test]
    public void RingBelowLevelTen_ReturnsLevelLockedWithoutMutation()
    {
        equipment.ResetEquipment();
        inventory.ResetInventory();
        InitializePlayer(1);
        InventoryItemDefinition ring = GetItem("boss_ruby_ring");
        inventory.TryAddItem(ring, 1);

        EquipmentOperationResult result = equipment.EquipFromInventory(0);

        Assert.That(result.FailureReason, Is.EqualTo(EquipmentOperationFailureReason.LevelLocked));
        Assert.That(inventoryModel.Slots[0].Item, Is.SameAs(ring));
        Assert.That(equipmentModel.GetEquipped(EquipmentSlotType.Ring), Is.Null);
    }

    [Test]
    public void RepeatedEquipAndUnequip_DoesNotDriftFinalStats()
    {
        InventoryItemDefinition boots = GetItem("boss_windleaf_boots");
        PlayerStatsSnapshot before = architecture.SendQuery(new GetPlayerStatsQuery());
        inventory.TryAddItem(boots, 1);
        for (int i = 0; i < 5; i++)
        {
            Assert.That(equipment.EquipFromInventory(0).Succeeded, Is.True);
            Assert.That(equipment.Unequip(EquipmentSlotType.Boots).Succeeded, Is.True);
        }

        PlayerStatsSnapshot after = architecture.SendQuery(new GetPlayerStatsQuery());
        Assert.That(after.CurrentMoveSpeed, Is.EqualTo(before.CurrentMoveSpeed).Within(0.0001f));
        Assert.That(after.DodgeChance, Is.EqualTo(before.DodgeChance).Within(0.0001f));
    }

    [Test]
    public void Unequip_WhenInventoryIsFull_KeepsEquipmentUnchanged()
    {
        InventoryItemDefinition boots = GetItem("boss_windleaf_boots");
        inventory.TryAddItem(boots, 1);
        Assert.That(equipment.EquipFromInventory(0).Succeeded, Is.True);
        InventoryItemDefinition material = GetItem("experience_crystal");
        inventory.TryAddItem(material, material.MaxStack * InventoryModel.DefaultCapacity);

        EquipmentOperationResult result = equipment.Unequip(EquipmentSlotType.Boots);

        Assert.That(result.FailureReason, Is.EqualTo(EquipmentOperationFailureReason.InventoryFull));
        Assert.That(equipmentModel.GetEquipped(EquipmentSlotType.Boots), Is.SameAs(boots));
    }

    [Test]
    public void EquipMaterial_ReturnsNotEquipmentAndKeepsInventory()
    {
        InventoryItemDefinition material = GetItem("spider_king_core");
        inventory.TryAddItem(material, 1);

        EquipmentOperationResult result = equipment.EquipFromInventory(0);

        Assert.That(result.FailureReason, Is.EqualTo(EquipmentOperationFailureReason.NotEquipment));
        Assert.That(inventoryModel.Slots[0].Item, Is.SameAs(material));
    }

    [Test]
    public void AttackUpgrade_ExcludesEquipmentBeforeApplyingGrowthMultiplier()
    {
        InventoryItemDefinition axe = GetItem("boss_iron_war_axe");
        inventory.TryAddItem(axe, 1);
        Assert.That(equipment.EquipFromInventory(0).Succeeded, Is.True);

        Assert.That(architecture.GetSystem<PlayerProgressionSystem>().TryApplyAttributeUpgrade(PlayerAttributeType.AttackPower), Is.True);

        // 基础攻击 10 先按 12% 成长到 12，再加装备 12；装备本身不能被成长倍率放大。
        Assert.That(architecture.SendQuery(new GetPlayerStatsQuery()).AttackPower, Is.EqualTo(24));
    }

    private InventoryItemDefinition GetItem(string itemId)
    {
        Assert.That(database.TryGetItemById(itemId, out InventoryItemDefinition item), Is.True);
        return item;
    }

    private void InitializePlayer(int level)
    {
        architecture.SendCommand(new InitializePlayerCommand(
            new NCharacter { id = 1, slotIndex = 0, name = "EquipmentTest", classId = 1, level = level },
            new CharacterDefine { classId = 1, initLevel = 1, hp = 100f, mp = 100f, attack = 10f, moveSpeed = 3f }));
    }
}
#endif
