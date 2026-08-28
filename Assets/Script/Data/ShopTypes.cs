using System;
using UnityEngine;

/// <summary>商店筛选分类独立于背包分类，避免任务物品等历史分类限制商店展示。</summary>
public enum ShopCategory
{
    All = 0,
    Consumable = 1,
    Equipment = 2,
    Material = 3
}

/// <summary>购买失败原因使用稳定枚举，UI 只负责把结果翻译成提示文字。</summary>
public enum ShopPurchaseFailure
{
    None = 0,
    InvalidEntry = 1,
    InsufficientGold = 2,
    InventoryFull = 3,
    SoldOut = 4,
    InternalError = 5
}

/// <summary>一次同步购买的完整结果，业务层不依赖具体 UI。</summary>
public readonly struct ShopPurchaseResult
{
    public ShopPurchaseResult(bool success, ShopPurchaseFailure failure, ShopCatalogEntry entry, long remainingGold)
    {
        Success = success;
        Failure = failure;
        Entry = entry;
        RemainingGold = remainingGold;
    }

    public bool Success { get; }
    public ShopPurchaseFailure Failure { get; }
    public ShopCatalogEntry Entry { get; }
    public long RemainingGold { get; }
}

/// <summary>单个商品配置：静态定义售卖物品、价格、分类和角色限购规则。</summary>
[Serializable]
public sealed class ShopCatalogEntry
{
    [SerializeField] private string entryId;
    [SerializeField] private InventoryItemDefinition item;
    [SerializeField, Min(1)] private long price = 1;
    [SerializeField] private ShopCategory category;
    [SerializeField] private bool limitedOncePerCharacter;

    public string EntryId => string.IsNullOrWhiteSpace(entryId) && item != null ? item.ItemId : entryId;
    public InventoryItemDefinition Item => item;
    public long Price => Math.Max(1L, price);
    public ShopCategory Category => category;
    public bool LimitedOncePerCharacter => limitedOncePerCharacter;
}
