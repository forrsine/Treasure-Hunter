using System.Collections.Generic;
using QFramework;

public sealed class GetGoldQuery : AbstractQuery<long>
{
    protected override long OnDo() => this.GetSystem<EconomySystem>().CurrentGold;
}

public sealed class IsMerchantIntroCompletedQuery : AbstractQuery<bool>
{
    protected override bool OnDo() => this.GetModel<ShopModel>().MerchantIntroCompleted;
}

public sealed class IsLimitedShopItemPurchasedQuery : AbstractQuery<bool>
{
    private readonly string itemId;

    public IsLimitedShopItemPurchasedQuery(string itemId)
    {
        this.itemId = itemId;
    }

    protected override bool OnDo() => this.GetSystem<ShopSystem>().HasPurchased(itemId);
}

public sealed class GetPurchasedLimitedShopItemsQuery : AbstractQuery<IReadOnlyList<string>>
{
    protected override IReadOnlyList<string> OnDo() => this.GetSystem<ShopSystem>().CreatePurchasedSnapshot();
}
