#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 加载场景配置测试：保护 Build Settings、场景控制器和进度 UI 的必要引用。
/// 这些测试不执行真实场景跳转，避免 EditMode 测试打断当前测试流程。
/// </summary>
public sealed class LoadingSceneConfigurationTests
{
    [Test]
    public void LoadingScene_IsEnabledInBuildSettingsAfterCharacterSelectScene()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int characterSelectIndex = System.Array.FindIndex(
            scenes,
            scene => scene.path == "Assets/Scenes/CharacterSelectScene.unity" && scene.enabled);
        int loadingIndex = System.Array.FindIndex(
            scenes,
            scene => scene.path == LoadingSceneSetupTool.ScenePath && scene.enabled);

        Assert.That(characterSelectIndex, Is.GreaterThanOrEqualTo(0), "CharacterSelectScene 应在 Build Settings 中启用。 ");
        Assert.That(loadingIndex, Is.EqualTo(characterSelectIndex + 1), "LoadingScene 应紧跟在 CharacterSelectScene 后并启用。 ");
    }

    [Test]
    public void LoadingScene_HasControllerCanvasAndCompleteSerializedReferences()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(LoadingSceneSetupTool.ScenePath);
        Assert.That(sceneAsset, Is.Not.Null, "缺少 LoadingScene 资源。 ");

        Scene scene = SceneManager.GetSceneByPath(LoadingSceneSetupTool.ScenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
        {
            scene = EditorSceneManager.OpenScene(
                LoadingSceneSetupTool.ScenePath,
                OpenSceneMode.Additive);
        }

        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            LoadingSceneController[] controllers = roots
                .SelectMany(root => root.GetComponentsInChildren<LoadingSceneController>(true))
                .ToArray();

            Assert.That(controllers, Has.Length.EqualTo(1), "LoadingScene 应且只能有一个 LoadingSceneController。 ");

            LoadingSceneController controller = controllers[0];
            Canvas canvas = controller.GetComponent<Canvas>();
            CanvasScaler canvasScaler = controller.GetComponent<CanvasScaler>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvasScaler, Is.Not.Null);
            Assert.That(canvasScaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            Assert.That(canvasScaler.referenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));

            SerializedObject serializedController = new SerializedObject(controller);
            Slider slider = serializedController.FindProperty("progressSlider").objectReferenceValue as Slider;
            Text progressText = serializedController.FindProperty("progressText").objectReferenceValue as Text;
            Text loadingText = serializedController.FindProperty("loadingText").objectReferenceValue as Text;

            Assert.That(slider, Is.Not.Null, "LoadingSceneController 缺少进度条引用。 ");
            Assert.That(progressText, Is.Not.Null, "LoadingSceneController 缺少百分比文本引用。 ");
            Assert.That(loadingText, Is.Not.Null, "LoadingSceneController 缺少加载提示文本引用。 ");
            Assert.That(slider.minValue, Is.EqualTo(0f));
            Assert.That(slider.maxValue, Is.EqualTo(1f));
            Assert.That(slider.interactable, Is.False);
            Assert.That(
                serializedController.FindProperty("progressSmoothSpeed").floatValue,
                Is.EqualTo(1.5f).Within(0.001f));
            Assert.That(
                serializedController.FindProperty("minimumVisibleDuration").floatValue,
                Is.EqualTo(0.8f).Within(0.001f));
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
#endif
