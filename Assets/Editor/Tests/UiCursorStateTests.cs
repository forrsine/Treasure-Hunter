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
}
#endif
