using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 鼠标弹出工具。
/// 
/// 作用：
/// 当暂停菜单、升级面板、游戏结束面板出现时，游戏会把鼠标从“锁定隐藏”改成“显示出来”。
/// Unity 默认经常会把刚显示出来的鼠标放在屏幕中间，容易误点中间按钮。
/// 所以所有需要“显示鼠标”的地方，都统一调用 ShowAtTopLeft，让鼠标出现在窗口左上角。
/// </summary>
public static class CursorPopupUtility
{
    // 鼠标不要贴死左上角，稍微留一点距离，看起来更自然，也不容易跑到窗口外。
    private const int TopLeftPaddingPixels = 16;

    // 这是一个隐藏的小组件，用来启动协程。
    // static 工具类本身不能直接 StartCoroutine，所以需要一个 MonoBehaviour 帮忙。
    private static CursorPopupPositioner positioner;

    /// <summary>
    /// 显示鼠标，并把鼠标移动到游戏窗口左上角。
    /// 
    /// 以后如果还有地方需要“弹出鼠标”，直接调用这个方法就行。
    /// </summary>
    public static void ShowAtTopLeft()
    {
        // 先解除鼠标锁定，否则鼠标还被游戏镜头控制着，移动位置也看不到效果。
        Cursor.lockState = CursorLockMode.None;

        // 再把鼠标显示出来。
        Cursor.visible = true;

        // 立刻移动一次，尽量让玩家马上看到鼠标在左上角。
        MoveToTopLeftNow();

        if (Application.isPlaying)
        {
            // Unity 在解锁鼠标后的几帧里，有时还会把鼠标拉回窗口中心。
            // 所以运行时再补移动两次，让位置更稳定。
            EnsurePositioner().MoveForNextFrames();
        }
    }

    /// <summary>
    /// 立即把系统鼠标移动到游戏窗口左上角。
    /// </summary>
    internal static void MoveToTopLeftNow()
    {
        // 这个移动鼠标的方法只在 Windows 编辑器和 Windows 打包版本里启用。
        // 其它平台不会编译这里的代码，也就不会因为 user32.dll 报错。
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        // 先算出“游戏窗口左上角附近”对应的屏幕坐标。
        Vector2Int targetPosition = GetTopLeftTargetPosition();

        // 调用 Windows 系统接口，真正把系统鼠标移动过去。
        SetCursorPos(targetPosition.x, targetPosition.y);
#endif
    }

    /// <summary>
    /// 当辅助物体销毁时清理静态引用。
    /// </summary>
    internal static void ClearPositioner(CursorPopupPositioner destroyedPositioner)
    {
        // 如果隐藏小组件被销毁了，就把缓存清空。
        // 这样下次需要时，会重新创建一个新的小组件。
        if (positioner == destroyedPositioner)
        {
            positioner = null;
        }
    }

    private static CursorPopupPositioner EnsurePositioner()
    {
        // 如果已经有小组件，就直接复用。
        if (positioner != null)
        {
            return positioner;
        }

        // 有时场景里可能已经存在这个小组件，先找一下，避免重复创建。
        positioner = UnityEngine.Object.FindObjectOfType<CursorPopupPositioner>();
        if (positioner != null)
        {
            return positioner;
        }

        // 场景里没有，就创建一个隐藏物体专门跑协程。
        GameObject positionerObject = new GameObject("CursorPopupPositioner");

        // 切换场景时不要销毁它，后面的暂停/升级也能继续复用。
        UnityEngine.Object.DontDestroyOnLoad(positionerObject);

        // 给隐藏物体挂上组件，这样它就能 StartCoroutine。
        positioner = positionerObject.AddComponent<CursorPopupPositioner>();
        return positioner;
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static Vector2Int GetTopLeftTargetPosition()
    {
        // 获取当前最前面的窗口。
        // 游戏运行时，一般就是 Unity 的 Game 窗口或打包后的游戏窗口。
        IntPtr windowHandle = GetForegroundWindow();
        if (windowHandle != IntPtr.Zero)
        {
            // 这里的坐标先按“窗口内部坐标”写：
            // X = 16 表示离窗口左边 16 像素。
            // Y = 16 表示离窗口上边 16 像素。
            POINT topLeft = new POINT
            {
                X = TopLeftPaddingPixels,
                Y = TopLeftPaddingPixels
            };

            // Windows 的 SetCursorPos 需要“整个屏幕坐标”，不是“窗口内部坐标”。
            // ClientToScreen 就是把窗口内部坐标转换成屏幕坐标。
            if (ClientToScreen(windowHandle, ref topLeft))
            {
                return new Vector2Int(topLeft.X, topLeft.Y);
            }
        }

        // 如果没拿到窗口，就退一步，放到整个屏幕左上角附近。
        return new Vector2Int(TopLeftPaddingPixels, TopLeftPaddingPixels);
    }

    // 下面几个 DllImport 是在调用 Windows 自带的 user32.dll。
    // 简单理解：Unity 2021 没有直接提供“移动系统鼠标”的接口，
    // 所以这里借用 Windows 系统自己的函数来完成。

    // 把“窗口内部坐标”转换成“屏幕坐标”。
    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    // 获取当前最前面的窗口。
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    // 把系统鼠标移动到指定屏幕坐标。
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    // Windows API 常用的点坐标结构。
    // X 是横向位置，Y 是纵向位置。
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
#endif
}

public sealed class CursorPopupPositioner : MonoBehaviour
{
    // 记录当前正在执行的协程。
    // 如果短时间内连续弹出多个面板，可以先停掉旧协程，再开新的。
    private Coroutine moveRoutine;

    /// <summary>
    /// 接下来几帧继续把鼠标放到左上角。
    /// 
    /// 原因：
    /// 鼠标从 Locked 变成 None 的时候，Unity/系统可能会在下一帧重新调整鼠标位置。
    /// 多补几次，可以避免鼠标闪回屏幕中心。
    /// </summary>
    public void MoveForNextFrames()
    {
        // 如果已经有一个移动协程在跑，先停掉，避免多个协程同时抢着移动鼠标。
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        // 启动新的补偿移动协程。
        moveRoutine = StartCoroutine(MoveForNextFramesRoutine());
    }

    /// <summary>
    /// 连续若干帧移动鼠标，抵消窗口聚焦和 UI 创建导致的位置回弹。
    /// </summary>
    private IEnumerator MoveForNextFramesRoutine()
    {
        // 等一帧。
        // 这样可以避开 Unity 刚解除鼠标锁定时的内部处理。
        yield return null;

        // 第一帧后，再移动一次。
        CursorPopupUtility.MoveToTopLeftNow();

        // 再等到这一帧真正结束。
        yield return new WaitForEndOfFrame();

        // 帧结束时再移动一次，进一步防止鼠标回到中心。
        CursorPopupUtility.MoveToTopLeftNow();

        // 协程结束，把记录清空。
        moveRoutine = null;
    }

    /// <summary>
    /// 销毁时通知工具类清空缓存。
    /// </summary>
    private void OnDestroy()
    {
        // 这个组件销毁时，通知工具类不要再保存旧引用。
        CursorPopupUtility.ClearPositioner(this);
    }
}
