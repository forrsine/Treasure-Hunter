using System;
using System.Collections.Generic;
using QFramework;

/// <summary>
/// 当前角色的商人进度：保存首次对话和限购商品 ID，不保存静态价格与物品定义。
/// </summary>
public sealed class ShopModel : AbstractModel
{
    private readonly HashSet<string> purchasedLimitedItemIds = new HashSet<string>(StringComparer.Ordinal);

    public bool MerchantIntroCompleted { get; private set; }
    public IReadOnlyCollection<string> PurchasedLimitedItemIds => purchasedLimitedItemIds;

    protected override void OnInit()
    {
        Reset();
    }

    internal bool CompleteMerchantIntro()
    {
        if (MerchantIntroCompleted)
        {
            return false;
        }

        MerchantIntroCompleted = true;
        return true;
    }

    internal bool MarkPurchased(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && purchasedLimitedItemIds.Add(itemId);
    }

    internal bool HasPurchased(string itemId)
    {
        return !string.IsNullOrWhiteSpace(itemId) && purchasedLimitedItemIds.Contains(itemId);
    }

    internal void Restore(bool introCompleted, IReadOnlyList<string> purchasedItemIds)
    {
        MerchantIntroCompleted = introCompleted;
        purchasedLimitedItemIds.Clear();
        if (purchasedItemIds == null)
        {
            return;
        }

        for (int i = 0; i < purchasedItemIds.Count; i++)
        {
            string itemId = purchasedItemIds[i];
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                purchasedLimitedItemIds.Add(itemId);
            }
        }
    }

    internal void Reset()
    {
        MerchantIntroCompleted = false;
        purchasedLimitedItemIds.Clear();
    }

    public List<string> CreatePurchasedSnapshot()
    {
        var result = new List<string>(purchasedLimitedItemIds);
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
