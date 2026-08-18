using QFramework;

/// <summary>
/// 客户端玩法架构入口。
/// 这里相当于玩法层的“总装配表”：
/// Model 负责保存权威数据，System 负责处理可复用规则，
/// MonoBehaviour 只负责和 Unity 场景、动画、碰撞、UI 打交道。
/// 这样后面扩职业、扩技能或改 UI 时，不容易牵一发动全身。
/// </summary>
public sealed class TreasureHunterArchitecture : Architecture<TreasureHunterArchitecture>
{
    /// <summary>
    /// QFramework 在第一次访问 Interface 时会调用这里。
    /// 先注册 Model，再注册依赖这些数据的 System，
    /// 可以避免系统初始化时读不到玩家模型。
    /// </summary>
    protected override void Init()
    {
        RegisterModel(new PlayerModel());
        RegisterModel(new PlayerSkillModel());
        RegisterModel(new InventoryModel());
        RegisterSystem(new PlayerResourceSystem());
        RegisterSystem(new PlayerCombatSystem());
        RegisterSystem(new PlayerSkillSystem());
        RegisterSystem(new PlayerProgressionSystem());
        RegisterSystem(new InventorySystem());
    }
}
