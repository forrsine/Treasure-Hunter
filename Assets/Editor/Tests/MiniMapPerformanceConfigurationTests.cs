#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 小地图性能配置测试：保护手动刷新频率、相机裁剪和 RenderTexture 配置，
/// 避免后续编辑场景时误把小地图恢复成每帧完整渲染。
/// </summary>
public sealed class MiniMapPerformanceConfigurationTests
{
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";
    private const string MiniMapRenderTexturePath = "Assets/RenderTextures/MiniMapRT.renderTexture";

    [Test]
    public void MainScene_MiniMapCameraUsesThrottledRenderingSettings()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
        Assert.That(sceneAsset, Is.Not.Null, "缺少 MainScene 场景资源。");

        Scene scene = SceneManager.GetSceneByPath(MainScenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
        {
            scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Additive);
        }

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            MiniMapCameraController[] controllers = roots
                .SelectMany(root => root.GetComponentsInChildren<MiniMapCameraController>(true))
                .ToArray();

            Assert.That(controllers, Has.Length.EqualTo(1), "MainScene 应且只能有一个 MiniMapCameraController。");

            MiniMapCameraController controller = controllers[0];
            Camera miniMapCamera = controller.GetComponent<Camera>();
            Assert.That(miniMapCamera, Is.Not.Null);
            Assert.That(miniMapCamera.enabled, Is.False, "小地图 Camera 必须关闭自动逐帧渲染。");
            Assert.That(miniMapCamera.orthographic, Is.True);
            Assert.That(miniMapCamera.farClipPlane, Is.EqualTo(80f).Within(0.001f));
            Assert.That(miniMapCamera.allowHDR, Is.False);
            Assert.That(miniMapCamera.allowMSAA, Is.False);

            int expectedCullingMask =
                (1 << LayerMask.NameToLayer("Default")) |
                (1 << LayerMask.NameToLayer("Water"));
            Assert.That(
                miniMapCamera.cullingMask,
                Is.EqualTo(expectedCullingMask),
                "小地图只应渲染 Default 和 Water，角色、怪物与宝箱由 UI 图标显示。");

            RenderTexture expectedRenderTexture =
                AssetDatabase.LoadAssetAtPath<RenderTexture>(MiniMapRenderTexturePath);
            Assert.That(miniMapCamera.targetTexture, Is.SameAs(expectedRenderTexture));
            Assert.That(controller.GetComponent<AudioListener>(), Is.Null, "小地图相机不应挂载第二个 AudioListener。");

            AudioListener[] listeners = roots
                .SelectMany(root => root.GetComponentsInChildren<AudioListener>(true))
                .Where(listener => listener.enabled)
                .ToArray();
            Assert.That(listeners, Has.Length.EqualTo(1), "MainScene 中应只有主相机的一个 AudioListener。");

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty refreshRateProperty = serializedController.FindProperty("refreshRate");
            Assert.That(refreshRateProperty, Is.Not.Null);
            Assert.That(refreshRateProperty.floatValue, Is.EqualTo(10f).Within(0.001f));
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void MiniMapRenderTexture_KeepsResolutionAndDisablesMsaa()
    {
        RenderTexture renderTexture =
            AssetDatabase.LoadAssetAtPath<RenderTexture>(MiniMapRenderTexturePath);

        Assert.That(renderTexture, Is.Not.Null, "缺少 MiniMapRT RenderTexture。");
        Assert.That(renderTexture.width, Is.EqualTo(512));
        Assert.That(renderTexture.height, Is.EqualTo(512));
        Assert.That(renderTexture.antiAliasing, Is.EqualTo(1), "小地图 RenderTexture 不应开启 MSAA。");
    }
}
#endif
