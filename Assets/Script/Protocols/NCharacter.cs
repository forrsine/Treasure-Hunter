using System;
using System.Collections.Generic;

/// <summary>
/// 客户端使用的轻量角色存档模型。
/// 在线模式由网络 DTO 转换，游客模式由本地 JSON 读取，供角色选择、跨场景暂存和游戏角色生成使用。
/// </summary>
[Serializable]
public class NCharacter
{
    public long id;
    public int slotIndex;
    public string name;
    public int classId;
    public int level;
    public int exp;
    public int pendingAttributeUpgradeCount;
    public int vaultDestroyedCount;
    public int completedBossCount;
    public List<NAttributeUpgradeSave> attributeUpgrades = new List<NAttributeUpgradeSave>();

    /// <summary>
    /// 查询某一种属性的强化次数。返回值来自副本，外部不能直接改写存档集合。
    /// </summary>
    public int GetAttributeUpgradeCount(PlayerAttributeType attributeType)
    {
        if (attributeUpgrades == null)
        {
            return 0;
        }

        int typeValue = (int)attributeType;
        for (int i = 0; i < attributeUpgrades.Count; i++)
        {
            NAttributeUpgradeSave upgrade = attributeUpgrades[i];
            if (upgrade != null && upgrade.attributeType == typeValue)
            {
                return Math.Max(0, upgrade.upgradeCount);
            }
        }

        return 0;
    }

    /// <summary>创建深拷贝，避免网络缓存、选角数据和运行时存档共享同一个可写列表。</summary>
    public NCharacter Clone()
    {
        NCharacter copy = new NCharacter
        {
            id = id,
            slotIndex = slotIndex,
            name = name,
            classId = classId,
            level = level,
            exp = exp,
            pendingAttributeUpgradeCount = pendingAttributeUpgradeCount,
            vaultDestroyedCount = vaultDestroyedCount,
            completedBossCount = completedBossCount
        };

        if (attributeUpgrades != null)
        {
            for (int i = 0; i < attributeUpgrades.Count; i++)
            {
                NAttributeUpgradeSave upgrade = attributeUpgrades[i];
                if (upgrade == null)
                {
                    continue;
                }

                copy.attributeUpgrades.Add(new NAttributeUpgradeSave
                {
                    attributeType = upgrade.attributeType,
                    upgradeCount = upgrade.upgradeCount
                });
            }
        }

        return copy;
    }
}

/// <summary>
/// 客户端轻量属性强化存档。只记录“选过几次”，具体数值由当前 GameConfig 重新计算。
/// </summary>
[Serializable]
public sealed class NAttributeUpgradeSave
{
    public int attributeType;
    public int upgradeCount;
}
