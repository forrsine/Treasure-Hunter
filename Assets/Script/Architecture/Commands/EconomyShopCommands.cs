using System.Collections.Generic;
using QFramework;

/// <summary>统一获得金币命令，怪物、拾取物和以后任务奖励均复用该入口。</summary>
public sealed class AddGoldCommand : AbstractCommand<long>
{
    private readonly long amount;

    public AddGoldCommand(long amount)
    {
        this.amount = amount;
    }

    protected override long OnExecute() => this.GetSystem<EconomySystem>().AddGold(amount);
}

public sealed class PurchaseShopItemCommand : AbstractCommand<ShopPurchaseResult>
{
    private readonly ShopCatalogEntry entry;

    public PurchaseShopItemCommand(ShopCatalogEntry entry)
    {
        this.entry = entry;
    }

    protected override ShopPurchaseResult OnExecute() => this.GetSystem<ShopSystem>().TryPurchase(entry);
}

public sealed class CompleteMerchantIntroCommand : AbstractCommand<bool>
{
    protected override bool OnExecute() => this.GetSystem<ShopSystem>().CompleteMerchantIntro();
}

/// <summary>选择角色后恢复钱包和商人进度。</summary>
public sealed class RestoreEconomyAndShopCommand : AbstractCommand
{
    private readonly long gold;
    private readonly bool merchantIntroCompleted;
    private readonly IReadOnlyList<string> purchasedItemIds;

    public RestoreEconomyAndShopCommand(long gold, bool merchantIntroCompleted, IReadOnlyList<string> purchasedItemIds)
    {
        this.gold = gold;
        this.merchantIntroCompleted = merchantIntroCompleted;
        this.purchasedItemIds = purchasedItemIds;
    }

    protected override void OnExecute()
    {
        this.GetSystem<EconomySystem>().Restore(gold);
        this.GetSystem<ShopSystem>().Restore(merchantIntroCompleted, purchasedItemIds);
    }
}

public sealed class ResetEconomyAndShopCommand : AbstractCommand
{
    protected override void OnExecute()
    {
        this.GetSystem<EconomySystem>().Reset();
        this.GetSystem<ShopSystem>().Reset();
    }
}
