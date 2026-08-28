using QFramework;

/// <summary>角色钱包运行时模型，只保存当前角色的金币余额。</summary>
public sealed class EconomyModel : AbstractModel
{
    public long Gold { get; private set; }

    protected override void OnInit()
    {
        Gold = 0L;
    }

    internal void SetGold(long gold)
    {
        Gold = gold;
    }
}
