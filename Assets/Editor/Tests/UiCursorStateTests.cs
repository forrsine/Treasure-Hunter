#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// UI 鼠标状态测试：确保登录、选角界面使用的公共入口始终解除锁定并显示鼠标。
/// </summary>
public sealed class UiCursorStateTests
{
    [Test]
    public void EnsureVisibleAndUnlocked_SetsUiCursorMode()
    {
        CursorLockMode originalLockState = Cursor.lockState;
        bool originalVisible = Cursor.visible;

        try
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            UiCursorStateUtility.EnsureVisibleAndUnlocked();

            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            Assert.That(Cursor.visible, Is.True);
        }
        finally
        {
            Cursor.lockState = originalLockState;
            Cursor.visible = originalVisible;
        }
    }

    [Test]
    public void EnsureHiddenAndLocked_RestoresGameplayCursorMode()
    {
        CursorLockMode originalLockState = Cursor.lockState;
        bool originalVisible = Cursor.visible;

        try
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            UiCursorStateUtility.EnsureHiddenAndLocked();

            // BatchMode 没有 Game View 窗口，Unity 会忽略 Locked 请求；有窗口的编辑器和正式包才校验锁定值。
            if (!Application.isBatchMode)
            {
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            }
            Assert.That(Cursor.visible, Is.False);
        }
        finally
        {
            Cursor.lockState = originalLockState;
            Cursor.visible = originalVisible;
        }
    }
}
#endif
