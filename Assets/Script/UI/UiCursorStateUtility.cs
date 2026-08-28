using UnityEngine;

/// <summary>
/// 全局鼠标状态工具：统一切换 UI 操作模式和第三人称玩法模式。
/// Cursor 状态会跨场景保留，因此打开/关闭模态界面时必须显式设置，不能依赖上一次缓存值碰巧正确。
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

    /// <summary>
    /// 恢复第三人称玩法需要的鼠标状态，让鼠标移动重新用于控制镜头。
    /// 商店等玩法内模态界面关闭后应调用本方法，避免把打开前偶然处于解锁状态的值恢复回来。
    /// </summary>
    public static void EnsureHiddenAndLocked()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
