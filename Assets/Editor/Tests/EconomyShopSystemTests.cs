#if UNITY_EDITOR
using NUnit.Framework;
using QFramework;
using UnityEngine;

/// <summary>经济与商店规则测试：覆盖上限保护、原子购买、限购和经济平衡。</summary>
public sealed class EconomyShopSystemTests
{
    private IArchitecture architecture;
    private EconomySystem economy;
    private ShopSystem shop;
    private InventorySystem inventory;
    private InventoryModel inventoryModel;
    private ShopCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        architecture = TreasureHunterArchitecture.Interface;
        economy = architecture.GetSystem<EconomySystem>();
        shop = architecture.GetSystem<ShopSystem>();
        inventory = architecture.GetSystem<InventorySystem>();
        inventoryModel = architecture.GetModel<InventoryModel>();
        catalog = Resources.Load<ShopCatalog>(ShopCatalog.ResourcesPath);
        InventoryDatabase database = Resources.Load<InventoryDatabase>(InventoryDatabase.ResourcesPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(database, Is.Not.Null);
        economy.Configure(Resources.Load<EconomyConfig>(EconomyConfig.ResourcesPath));
        shop.ConfigureCatalog(catalog);
        inventory.ConfigureDatabase(database);
        inventory.ResetInventory();
        economy.Reset();
        shop.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.Deinit();
        architecture = null;
    }

    [Test]
    public void Economy_AddSpendAndCap_AreProtected()
    {
        Assert.That(economy.AddGold(80), Is.EqualTo(80));
        Assert.That(economy.TrySpendGold(30), Is.True);
        Assert.That(economy.CurrentGold, Is.EqualTo(50));
        Assert.That(economy.TrySpendGold(51), Is.False);
        Assert.That(economy.AddGold(long.MaxValue), Is.EqualTo(EconomySystem.MaxGold - 50));
        Assert.That(economy.CurrentGold, Is.EqualTo(EconomySystem.MaxGold));
    }

    [Test]
    public void PurchaseConsumable_AddsOneAndDeductsExactPrice()
    {
        Assert.That(catalog.TryGetEntry("healing_potion", out ShopCatalogEntry entry), Is.True);
        economy.AddGold(50);

        ShopPurchaseResult result = shop.TryPurchase(entry);

        Assert.That(result.Success, Is.True);
        Assert.That(economy.CurrentGold, Is.EqualTo(25));
        Assert.That(inventoryModel.Slots[0].Item.ItemId, Is.EqualTo("healing_potion"));
        Assert.That(inventoryModel.Slots[0].Count, Is.EqualTo(1));
    }

    [Test]
    public void Purchase_WhenGoldInsufficient_ChangesNothing()
    {
        Assert.That(catalog.TryGetEntry("ancient_scroll", out ShopCatalogEntry entry), Is.True);
        economy.AddGold(99);

        ShopPurchaseResult result = shop.TryPurchase(entry);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure, Is.EqualTo(ShopPurchaseFailure.InsufficientGold));
        Assert.That(economy.CurrentGold, Is.EqualTo(99));
        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.Zero);
    }

    [Test]
    public void Purchase_WhenInventoryFull_DoesNotDeductGold()
    {
        Assert.That(catalog.TryGetEntry("merchant_training_hammer", out ShopCatalogEntry fillerEntry), Is.True);
        Assert.That(catalog.TryGetEntry("healing_potion", out ShopCatalogEntry targetEntry), Is.True);
        inventory.TryAddItem(fillerEntry.Item, InventoryModel.DefaultCapacity);
        economy.AddGold(500);

        ShopPurchaseResult result = shop.TryPurchase(targetEntry);

        Assert.That(inventoryModel.GetOccupiedSlotCount(), Is.EqualTo(InventoryModel.DefaultCapacity));
        Assert.That(result.Failure, Is.EqualTo(ShopPurchaseFailure.InventoryFull));
        Assert.That(economy.CurrentGold, Is.EqualTo(500));
    }

    [Test]
    public void LimitedEquipment_CanOnlyBePurchasedOncePerCharacter()
    {
        Assert.That(catalog.TryGetEntry("merchant_training_hammer", out ShopCatalogEntry entry), Is.True);
        economy.AddGold(1000);

        ShopPurchaseResult first = shop.TryPurchase(entry);
        ShopPurchaseResult second = shop.TryPurchase(entry);

        Assert.That(first.Success, Is.True);
        Assert.That(second.Success, Is.False);
        Assert.That(second.Failure, Is.EqualTo(ShopPurchaseFailure.SoldOut));
        Assert.That(economy.CurrentGold, Is.EqualTo(880));
        Assert.That(shop.CreatePurchasedSnapshot(), Is.EquivalentTo(new[] { "merchant_training_hammer" }));
    }

    [Test]
    public void EconomyConfig_ProducesPlannedClearVaultAndBossRewards()
    {
        EconomyConfig config = Resources.Load<EconomyConfig>(EconomyConfig.ResourcesPath);
        Assert.That(config, Is.Not.Null);

        float expectedClearAverage = 12 * 1.5f + 6 * 2.5f;
        Assert.That(expectedClearAverage, Is.EqualTo(33f));
        Assert.That(config.CalculateVaultGold(1), Is.EqualTo(30));
        Assert.That(config.CalculateVaultGold(5), Is.EqualTo(50));
        Assert.That(config.CalculateVaultGold(99), Is.EqualTo(50));
        Assert.That(config.CalculateBossGold(0), Is.EqualTo(150));
        Assert.That(config.CalculateBossGold(6), Is.EqualTo(300));
        Assert.That(config.CalculateBossGold(99), Is.EqualTo(300));
        Assert.That(config.RollMonsterGold(SlimeCo.SlimeType.Slime1, 0f), Is.EqualTo(1));
        Assert.That(config.RollMonsterGold(SlimeCo.SlimeType.Slime1, 1f), Is.EqualTo(2));
        Assert.That(config.RollMonsterGold(SlimeCo.SlimeType.Slime2, 0f), Is.EqualTo(2));
        Assert.That(config.RollMonsterGold(SlimeCo.SlimeType.Slime2, 1f), Is.EqualTo(3));
    }

    [Test]
    public void Catalog_ContainsSixteenEntriesAndKeepsEpicGearBossExclusive()
    {
        Assert.That(catalog.Entries, Has.Length.EqualTo(16));
        Assert.That(catalog.TryGetEntry("boss_moon_reaper", out _), Is.False);
        Assert.That(catalog.TryGetEntry("boss_tide_ring", out _), Is.False);
        Assert.That(catalog.TryGetEntry("spider_king_core", out _), Is.False);
    }
}
#endif
