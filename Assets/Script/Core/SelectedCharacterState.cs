/// <summary>
/// 跨场景暂存玩家在角色选择界面选中的存档角色。
/// 这里只保存本次切场景所需的轻量数据，进入游戏后由 GameplayCharacterManager 创建运行时角色。
/// </summary>
public static class SelectedCharacterState
{
    public static NCharacter CurrentCharacter { get; private set; }

    /// <summary>
    /// 在角色选择界面记录当前准备进入游戏的角色。
    /// 这里只做轻量暂存，不负责创建运行时角色对象。
    /// </summary>
    public static void SetCharacter(NCharacter save)
    {
        CurrentCharacter = save != null ? save.Clone() : null;
    }

    /// <summary>
    /// 清空跨场景暂存的角色。
    /// 登出、重置或重新选角时都应该调用这里，避免上一位角色残留到下一次流程。
    /// </summary>
    public static void Clear()
    {
        CurrentCharacter = null;
    }
}
