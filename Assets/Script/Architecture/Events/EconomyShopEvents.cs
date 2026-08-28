/// <summary>金币余额发生变化，HUD 与存档服务通过事件被动刷新。</summary>
public readonly struct GoldChangedEvent
{
    public GoldChangedEvent(long previousGold, long currentGold)
    {
        PreviousGold = previousGold;
        CurrentGold = currentGold;
    }

    public long PreviousGold { get; }
    public long CurrentGold { get; }
    public long Delta => CurrentGold - PreviousGold;
}

public readonly struct ShopPurchaseCompletedEvent
{
    public ShopPurchaseCompletedEvent(ShopPurchaseResult result)
    {
        Result = result;
    }

    public ShopPurchaseResult Result { get; }
}

public readonly struct MerchantIntroCompletedEvent { }
public readonly struct ShopProgressRestoredEvent { }

/// <summary>商人进入或离开交互范围，UI 据此显示提示而不是每帧查找 NPC。</summary>
public readonly struct MerchantProximityChangedEvent
{
    public MerchantProximityChangedEvent(bool isNearby)
    {
        IsNearby = isNearby;
    }

    public bool IsNearby { get; }
}

public readonly struct MerchantDialogueRequestedEvent { }
public readonly struct ShopOpenRequestedEvent { }
