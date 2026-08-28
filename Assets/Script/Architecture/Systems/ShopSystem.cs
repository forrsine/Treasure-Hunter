using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 商店业务系统：协调目录、钱包、背包与角色限购状态。
/// 购买规则集中在这里，UI 不能自行扣钱或直接写背包格。
/// </summary>
public sealed class ShopSystem : AbstractSystem
{
    private ShopModel shopModel;
    private EconomySystem economySystem;
    private InventorySystem inventorySystem;

    public ShopCatalog Catalog { get; private set; }

    protected override void OnInit()
    {
        shopModel = this.GetModel<ShopModel>();
        economySystem = this.GetSystem<EconomySystem>();
        inventorySystem = this.GetSystem<InventorySystem>();
        Catalog = Resources.Load<ShopCatalog>(ShopCatalog.ResourcesPath);
    }

    public void ConfigureCatalog(ShopCatalog catalog)
    {
        Catalog = catalog;
    }

    public ShopPurchaseResult TryPurchase(ShopCatalogEntry entry)
    {
        if (entry == null || Catalog == null || !Catalog.Contains(entry) || entry.Item == null)
        {
            return Failure(ShopPurchaseFailure.InvalidEntry, entry);
        }

        string itemId = entry.Item.ItemId;
        if (entry.LimitedOncePerCharacter && shopModel.HasPurchased(itemId))
        {
            return Failure(ShopPurchaseFailure.SoldOut, entry);
        }

        if (economySystem.CurrentGold < entry.Price)
        {
            return Failure(ShopPurchaseFailure.InsufficientGold, entry);
        }

        if (inventorySystem.GetAddableAmount(entry.Item, 1) < 1)
        {
            return Failure(ShopPurchaseFailure.InventoryFull, entry);
        }

        // Unity 玩法逻辑运行在主线程。完成全部只读预检后，背包容量和钱包余额不会在两条语句之间并发变化。
        if (!economySystem.TrySpendGold(entry.Price))
        {
            return Failure(ShopPurchaseFailure.InsufficientGold, entry);
        }

        InventoryAddResult addResult = inventorySystem.TryAddItem(entry.Item, 1);
        if (addResult.AddedAmount != 1)
        {
            // 理论上预检后不会失败；仍保留退款保护，防止未来改成异步背包时吞掉金币。
            economySystem.AddGold(entry.Price);
            return Failure(ShopPurchaseFailure.InternalError, entry);
        }

        if (entry.LimitedOncePerCharacter)
        {
            shopModel.MarkPurchased(itemId);
        }

        var result = new ShopPurchaseResult(true, ShopPurchaseFailure.None, entry, economySystem.CurrentGold);
        this.SendEvent(new ShopPurchaseCompletedEvent(result));
        return result;
    }

    public bool CompleteMerchantIntro()
    {
        if (!shopModel.CompleteMerchantIntro())
        {
            return false;
        }

        this.SendEvent(new MerchantIntroCompletedEvent());
        return true;
    }

    public bool HasPurchased(string itemId) => shopModel.HasPurchased(itemId);

    public List<string> CreatePurchasedSnapshot() => shopModel.CreatePurchasedSnapshot();

    public void Restore(bool introCompleted, IReadOnlyList<string> purchasedItemIds)
    {
        shopModel.Restore(introCompleted, purchasedItemIds);
        this.SendEvent(new ShopProgressRestoredEvent());
    }

    public void Reset()
    {
        shopModel.Reset();
        this.SendEvent(new ShopProgressRestoredEvent());
    }

    private ShopPurchaseResult Failure(ShopPurchaseFailure failure, ShopCatalogEntry entry)
    {
        return new ShopPurchaseResult(false, failure, entry, economySystem != null ? economySystem.CurrentGold : 0L);
    }
}
