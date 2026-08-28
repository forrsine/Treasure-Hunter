using UnityEngine;

/// <summary>
/// 游戏输入只读接口。
/// 移动、战斗和摄像机依赖接口而不是具体的 InputCo，
/// 后续可以替换为新输入系统、手柄或网络回放输入。
/// </summary>
public interface IGameplayInput
{
    float XInput { get; }
    float YInput { get; }
    Vector3 MouseInput { get; }
    bool LeftMouseDown { get; }
    bool LeftMouseHeld { get; }
    bool LeftMouseUp { get; }
    bool RollDown { get; }
    bool DeveloperModeToggleDown { get; }
    bool DebugHighAttackToggleDown { get; }
    bool DebugInvincibilityToggleDown { get; }
    bool DebugAddGoldDown { get; }
    bool DebugCompleteVaultCycleDown { get; }
    bool DebugAddLevelDown { get; }
    bool DebugRestoreManaDown { get; }
    bool DebugZeroCooldownToggleDown { get; }
    bool InventoryToggleDown { get; }
    bool InteractDown { get; }

    bool Skill1Down { get; }
    bool Skill1Held { get; }
    bool Skill1Up { get; }
    bool Skill2Down { get; }
    bool Skill2Held { get; }
    bool Skill2Up { get; }
    bool Skill3Down { get; }
    bool Skill3Held { get; }
    bool Skill3Up { get; }
}
