#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Windows 演示包关键装配回归测试：保护 Boss 玩家 UI、登录分辨率适配和退出按钮。
/// 这些问题在编辑器里可能被运行时兜底掩盖，因此直接检查进入构建的场景资源。
/// </summary>
public sealed class StandaloneBuildConfigurationTests
{
    private const string BossScenePath = "Assets/Scenes/BossRoomScene.unity";
    private const string LoginScenePath = "Assets/Scenes/LoginScene.unity";

    [Test]
    public void BossScene_ContainsExactlyOneGameplayUiRootPrefab()
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenScene(BossScenePath, OpenSceneMode.Additive);
            GameplayUiRoot[] uiRoots = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<GameplayUiRoot>(true))
                .ToArray();

            Assert.That(uiRoots.Length, Is.EqualTo(1),
                "BossRoomScene 必须直接引用一份 GameplayUiRoot，不能依赖仅编辑器可用的 AssetDatabase 兜底。");
        }
        finally
        {
            if (scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void BossHud_RendersAboveGameplayUiRoot()
    {
        GameObject hudObject = new GameObject("BossHudSortingTest");
        try
        {
            BossBattleHudUi hud = hudObject.AddComponent<BossBattleHudUi>();
            MethodInfo ensureCanvas = typeof(BossBattleHudUi).GetMethod(
                "EnsureCanvas",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(ensureCanvas, Is.Not.Null);
            ensureCanvas.Invoke(hud, null);

            Canvas bossCanvas = hudObject.GetComponent<Canvas>();
            Assert.That(bossCanvas, Is.Not.Null);
            Assert.That(bossCanvas.sortingOrder, Is.GreaterThan(5000));
        }
        finally
        {
            Object.DestroyImmediate(hudObject);
        }
    }

    [Test]
    public void LoginScene_UsesResponsiveCanvasBackgroundAndQuitButton()
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenScene(LoginScenePath, OpenSceneMode.Additive);
            GameObject canvasObject = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == "Canvas");
            Assert.That(canvasObject, Is.Not.Null);

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.001f));

            RectTransform background = canvasObject.transform.Find("BG") as RectTransform;
            Assert.That(background, Is.Not.Null);
            Assert.That(background.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(background.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(background.sizeDelta, Is.EqualTo(Vector2.zero));

            Transform exitButtonTransform = canvasObject.transform.Find("ExitGameButton");
            Assert.That(exitButtonTransform, Is.Not.Null);
            Assert.That(exitButtonTransform.GetComponent<Button>(), Is.Not.Null);
            Assert.That(exitButtonTransform.GetComponent<ApplicationQuitButton>(), Is.Not.Null);
            Assert.That(
                exitButtonTransform.GetComponentInChildren<Text>(true).text,
                Is.EqualTo("退出游戏"));
        }
        finally
        {
            if (scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
