using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 玩家成长表现入口：把 Unity 世界中的经验奖励和 UI 选择转换成 Command。
/// 所有等级、经验和属性升级公式都在 PlayerProgressionSystem 中，组件不再依赖巨型控制脚本。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerProgressionComponent : MonoBehaviour, IController
{
    private PlayerRuntimeController runtimeController;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 绑定运行时控制器。
    /// 该组件本身不保存升级规则，只负责把 Unity 世界里的行为转成 Command 或 Query。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        runtimeController = player;
    }

    /// <summary>
    /// 仅为未挂载的旧版脚本保留编译入口；新项目不执行这条初始化路径。
    /// </summary>
    public void Initialize(MonoBehaviour obsoleteOwner)
    {
        runtimeController = GetComponent<PlayerRuntimeController>();
    }

    /// <summary>
    /// 根据当前存档和职业配置初始化成长数据。
    /// 这通常在角色刚进入玩法场景时调用一次。
    /// </summary>
    public void InitializeStatsFromConfig()
    {
        if (runtimeController != null)
        {
            this.SendCommand(new InitializePlayerCommand(runtimeController.EntrySave, runtimeController.EntryDefine));
        }
    }

    public void ApplyEntryCharacterStats()
    {
        InitializeStatsFromConfig();
    }

    /// <summary>
    /// 给玩家增加经验。
    /// 这里不关心升级细节，只负责把请求交给成长系统。
    /// </summary>
    public void AddExp(int exp)
    {
        if (exp <= 0)
        {
            return;
        }

        this.SendCommand(new AddPlayerExpCommand(exp));
    }

    /// <summary>
    /// 控制升级选择面板的激活状态。
    /// </summary>
    public void SetUpgradeSelectionState(bool active)
    {
        this.SendCommand(new SetPlayerUpgradeSelectionStateCommand(active));
    }

    /// <summary>
    /// 结算一次玩家在升级面板中的选择。
    /// </summary>
    public bool ResolvePendingUpgradeSelection(PlayerAttributeType attributeType)
    {
        return this.SendCommand(new ResolvePlayerUpgradeCommand(attributeType));
    }

    public bool CanApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        return this.GetSystem<PlayerProgressionSystem>().CanApplyAttributeUpgrade(attributeType);
    }

    public bool TryApplyAttributeUpgrade(PlayerAttributeType attributeType)
    {
        return this.SendCommand(new ApplyPlayerUpgradeCommand(attributeType));
    }

    public List<PlayerAttributeType> GetRandomUpgradeChoices(int choiceCount = 3)
    {
        return this.SendQuery(new GetPlayerUpgradeChoicesQuery(choiceCount));
    }

    public string GetUpgradeOptionText(PlayerAttributeType attributeType)
    {
        return this.SendQuery(new GetPlayerUpgradeOptionTextQuery(attributeType));
    }
}
