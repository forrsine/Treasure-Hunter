#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// PC 设置回归测试：覆盖数据换算、本机存储、Prefab 引用和登录场景装配。
/// 显示切换不在 EditMode 中真实执行，避免测试过程改变编辑器窗口分辨率。
/// </summary>
public sealed class GameSettingsTests
{
    private string temporaryPlayerPrefsKey;

    [SetUp]
    public void SetUp()
    {
        temporaryPlayerPrefsKey = "TreasureHunter.Tests.GameSettings." + Guid.NewGuid().ToString("N");
    }

    [TearDown]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(temporaryPlayerPrefsKey))
        {
            PlayerPrefs.DeleteKey(temporaryPlayerPrefsKey);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void LinearVolumeToDecibels_HandlesMuteHalfAndFullVolume()
    {
        Assert.That(GameSettingsService.LinearVolumeToDecibels(0f), Is.EqualTo(-80f));
        Assert.That(GameSettingsService.LinearVolumeToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(0.001f));
        Assert.That(GameSettingsService.LinearVolumeToDecibels(1f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void ResolutionOptions_FilterDuplicatesAndKeepCurrentAndSavedValues()
    {
        Vector2Int[] available =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(1024, 768)
        };

        List<Vector2Int> options = GameSettingsService.BuildResolutionOptions(
            available,
            new Vector2Int(1600, 900),
            new Vector2Int(1280, 720));

        Assert.That(options.Count(value => value == new Vector2Int(1920, 1080)), Is.EqualTo(1));
        Assert.That(options, Has.Member(new Vector2Int(2560, 1440)));
        Assert.That(options, Has.Member(new Vector2Int(1600, 900)));
        Assert.That(options, Has.Member(new Vector2Int(1280, 720)));
        Assert.That(options, Has.No.Member(new Vector2Int(1024, 768)));
        Assert.That(options[0], Is.EqualTo(new Vector2Int(2560, 1440)));
    }

    [Test]
    public void Storage_RoundTripsAllSettingsThroughPlayerPrefs()
    {
        GameSettingsData expected = new GameSettingsData
        {
            masterVolume = 0.7f,
            musicVolume = 0.4f,
            soundEffectsVolume = 0.9f,
            mouseSensitivity = 1.5f,
            resolutionWidth = 1600,
            resolutionHeight = 900,
            displayMode = GameDisplayMode.Windowed,
            qualityLevel = 3,
            verticalSync = false,
            frameRateLimit = 144
        };

        GameSettingsStorage.Save(temporaryPlayerPrefsKey, expected);
        bool loaded = GameSettingsStorage.TryLoad(temporaryPlayerPrefsKey, out GameSettingsData actual);

        Assert.That(loaded, Is.True);
        Assert.That(actual.masterVolume, Is.EqualTo(expected.masterVolume).Within(0.001f));
        Assert.That(actual.musicVolume, Is.EqualTo(expected.musicVolume).Within(0.001f));
        Assert.That(actual.soundEffectsVolume, Is.EqualTo(expected.soundEffectsVolume).Within(0.001f));
        Assert.That(actual.mouseSensitivity, Is.EqualTo(expected.mouseSensitivity).Within(0.001f));
        Assert.That(actual.resolutionWidth, Is.EqualTo(1600));
        Assert.That(actual.resolutionHeight, Is.EqualTo(900));
        Assert.That(actual.displayMode, Is.EqualTo(GameDisplayMode.Windowed));
        Assert.That(actual.qualityLevel, Is.EqualTo(3));
        Assert.That(actual.verticalSync, Is.False);
        Assert.That(actual.frameRateLimit, Is.EqualTo(144));
    }

    [Test]
    public void DisplayConfirmation_IsRequiredOnlyForResolutionOrModeChanges()
    {
        GameSettingsData original = new GameSettingsData();
        GameSettingsData audioOnly = original.Clone();
        audioOnly.masterVolume = 0.5f;
        Assert.That(audioOnly.HasRiskyDisplayDifference(original), Is.False);

        GameSettingsData changedResolution = original.Clone();
        changedResolution.resolutionWidth = 1280;
        Assert.That(changedResolution.HasRiskyDisplayDifference(original), Is.True);

        GameSettingsData changedMode = original.Clone();
        changedMode.displayMode = GameDisplayMode.Windowed;
        Assert.That(changedMode.HasRiskyDisplayDifference(original), Is.True);
    }

    [Test]
    public void VerticalSync_DisablesManualFrameRateLimitRule()
    {
        Assert.That(GameSettingsService.ResolveTargetFrameRate(true, 144), Is.EqualTo(-1));
        Assert.That(GameSettingsService.ResolveTargetFrameRate(false, 144), Is.EqualTo(144));
        Assert.That(GameSettingsService.ResolveTargetFrameRate(false, -1), Is.EqualTo(-1));
    }

    [Test]
    public void ConfigAsset_ReferencesMixerAndRequiredGroups()
    {
        GameSettingsConfig config = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(
            GameSettingsAssetSetupTool.ConfigAssetPath);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.AudioMixer, Is.Not.Null);
        Assert.That(config.MusicMixerGroup, Is.Not.Null);
        Assert.That(config.MusicMixerGroup.name, Is.EqualTo("Music"));
        Assert.That(config.SoundsMixerGroup, Is.Not.Null);
        Assert.That(config.SoundsMixerGroup.name, Is.EqualTo("Sounds"));
        Assert.That(config.MusicVolumeParameter, Is.EqualTo("musicVolume"));
        Assert.That(config.SoundsVolumeParameter, Is.EqualTo("soundsVolume"));
        Assert.That(config.SupportedFrameRateLimits, Is.EqualTo(new[] { 30, 60, 90, 120, 144, 165, 240, -1 }));
    }

    [Test]
    public void PanelPrefab_HasCompleteReferencesAndModalOverlay()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            GameSettingsAssetSetupTool.PanelPrefabPath);
        GameSettingsPanelController controller = prefab != null
            ? prefab.GetComponent<GameSettingsPanelController>()
            : null;

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.activeSelf, Is.False);
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.ValidatePrefabReferences(false), Is.True);
        Image overlay = prefab.GetComponent<Image>();
        Assert.That(overlay, Is.Not.Null);
        Assert.That(overlay.raycastTarget, Is.True, "模态遮罩必须阻止点击穿透到登录按钮。 ");
    }

    [Test]
    public void LoginScene_HasOneSettingsPanelAndBoundSettingButton()
    {
        Scene scene = default;
        try
        {
            scene = EditorSceneManager.OpenScene(
                GameSettingsAssetSetupTool.LoginScenePath,
                OpenSceneMode.Additive);
            GameObject canvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
            Assert.That(canvas, Is.Not.Null);

            GameSettingsPanelController[] panels = canvas
                .GetComponentsInChildren<GameSettingsPanelController>(true);
            Assert.That(panels, Has.Length.EqualTo(1));

            Transform buttonTransform = canvas.transform.Find("SettingButton");
            Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
            Assert.That(button, Is.Not.Null);

            bool hasOpenBinding = false;
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) == panels[0] &&
                    button.onClick.GetPersistentMethodName(i) == nameof(GameSettingsPanelController.Open))
                {
                    hasOpenBinding = true;
                    break;
                }
            }

            Assert.That(hasOpenBinding, Is.True, "SettingButton 必须持久绑定 GameSettingsPanelController.Open。 ");
            Assert.That(panels[0].transform.GetSiblingIndex(), Is.EqualTo(canvas.transform.childCount - 1));
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
