using System;
using QFramework;
using UnityEngine;

/// <summary>金币规则系统：统一处理获得、消费、上限保护和角色存档恢复。</summary>
public sealed class EconomySystem : AbstractSystem
{
    public const long MaxGold = 9_999_999L;

    private EconomyModel model;

    public long CurrentGold => model != null ? model.Gold : 0L;
    public EconomyConfig Config { get; private set; }

    protected override void OnInit()
    {
        model = this.GetModel<EconomyModel>();
        Config = Resources.Load<EconomyConfig>(EconomyConfig.ResourcesPath);
    }

    public void Configure(EconomyConfig config)
    {
        Config = config;
    }

    public long AddGold(long amount)
    {
        if (amount <= 0L)
        {
            return 0L;
        }

        long before = model.Gold;
        long safeAmount = Math.Min(amount, MaxGold - before);
        if (safeAmount <= 0L)
        {
            return 0L;
        }

        model.SetGold(before + safeAmount);
        this.SendEvent(new GoldChangedEvent(before, model.Gold));
        return safeAmount;
    }

    public bool TrySpendGold(long amount)
    {
        if (amount <= 0L || model.Gold < amount)
        {
            return false;
        }

        long before = model.Gold;
        model.SetGold(before - amount);
        this.SendEvent(new GoldChangedEvent(before, model.Gold));
        return true;
    }

    public void Restore(long gold)
    {
        long before = model.Gold;
        model.SetGold(Math.Max(0L, Math.Min(MaxGold, gold)));
        if (before != model.Gold)
        {
            this.SendEvent(new GoldChangedEvent(before, model.Gold));
        }
    }

    public void Reset()
    {
        Restore(0L);
    }
}
