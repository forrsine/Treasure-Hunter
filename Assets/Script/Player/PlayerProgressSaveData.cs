using System.Collections.Generic;

/// <summary>
/// 玩家成长存档快照：只携带数据库需要的长期数据，不包含当前血蓝、动画或场景引用。
/// </summary>
public sealed class PlayerProgressSaveData
{
    public int Level { get; set; }
    public int Exp { get; set; }
    public int PendingAttributeUpgradeCount { get; set; }
    public List<NAttributeUpgradeSave> AttributeUpgrades { get; } = new List<NAttributeUpgradeSave>();

    /// <summary>
    /// 死亡或主动重开只清理本局强化，不触碰等级、经验和关卡累计数据。
    /// 将规则放在存档快照上，便于网络发送前统一处理并单独测试。
    /// </summary>
    public void ClearRunUpgrades()
    {
        PendingAttributeUpgradeCount = 0;
        AttributeUpgrades.Clear();
    }
}
