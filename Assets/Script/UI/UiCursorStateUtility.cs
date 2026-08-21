using UnityEngine;

/// <summary>
/// UI 场景鼠标状态工具：负责把全局鼠标切换为可见、可自由移动的界面操作模式。
/// Cursor 状态会跨场景保留，因此登录和选角界面不能依赖玩法场景自动恢复。
/// </summary>
public static class UiCursorStateUtility
{
    /// <summary>
    /// 解除玩法场景留下的鼠标锁定，并确保系统鼠标可见。
    /// 这里只处理状态，不主动移动鼠标位置，避免进入登录界面时鼠标突然跳动。
    /// </summary>
    public static void EnsureVisibleAndUnlocked()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
