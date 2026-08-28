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
/// 游戏音频配置回归测试：防止 Cue 漏资源、场景重复播放 BGM 或 UI 按钮漏挂点击反馈。
/// </summary>
public sealed class GameAudioTests
{
    private const string CatalogPath = "Assets/Resources/Data/GameAudioCatalog.asset";
    private const string AudioRoot = "Assets/AllResources/Audio/TreasureHunter";

    private static readonly string[] FormalScenePaths =
    {
        "Assets/Scenes/LoadingScene.unity",
        "Assets/Scenes/LoginScene.unity",
        "Assets/Scenes/CharacterSelectScene.unity",
        "Assets/Scenes/MainScene.unity",
        "Assets/Scenes/BossRoomScene.unity"
    };

    private static readonly string[] UiPrefabPaths =
    {
        "Assets/Prefabs/UI/GameplayUiRoot.prefab",
        "Assets/Prefabs/UI/GameSettingsPanel.prefab",
        "Assets/Prefabs/UI/QuestListItem.prefab",
        "Assets/Prefabs/UI/Slot.prefab"
    };

    [Test]
    public void Catalog_HasUniqueSceneMusicAndEveryRequiredCue()
    {
        GameAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null, "缺少 GameAudioCatalog.asset。 ");
        Assert.That(catalog.StartupMusic, Is.Not.Null, "首次 Loading 没有启动音乐。 ");

        SceneMusicEntry[] sceneEntries = catalog.SceneMusicEntries.Where(entry => entry != null).ToArray();
        Assert.That(sceneEntries.Select(entry => entry.sceneName).Distinct().Count(), Is.EqualTo(sceneEntries.Length), "场景音乐名称重复。 ");
        Assert.That(sceneEntries.Select(entry => entry.clip).All(clip => clip != null), Is.True, "场景音乐存在空引用。 ");
        Assert.That(sceneEntries.Select(entry => entry.sceneName), Is.EquivalentTo(new[]
        {
            GameSceneNames.LoginScene,
            GameSceneNames.CharacterSelectScene,
            GameSceneNames.GameplayScene,
            GameSceneNames.BossRoomScene
        }));

        GameAudioCue[] cues = catalog.SoundEffectCues.Where(cue => cue != null).ToArray();
        Assert.That(cues.Select(cue => cue.id).Distinct().Count(), Is.EqualTo(cues.Length), "音效 Cue ID 重复。 ");

        foreach (GameSfxId id in Enum.GetValues(typeof(GameSfxId)))
        {
            if (id == GameSfxId.None)
            {
                continue;
            }

            Assert.That(catalog.TryGetCue(id, out GameAudioCue cue), Is.True, $"缺少必需 Cue：{id}");
            Assert.That(cue.clips, Is.Not.Null.And.Not.Empty, $"Cue 没有候选音频：{id}");
            Assert.That(cue.clips.All(clip => clip != null), Is.True, $"Cue 存在空 AudioClip：{id}");
        }
    }

    [Test]
    public void SceneMusicRule_UsesStartupFallbackAndKeepsMusicDuringLoading()
    {
        GameAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
        Assert.That(catalog, Is.Not.Null);

        bool firstLoading = GameAudioService.TryResolveMusicForScene(
            catalog,
            GameSceneNames.LoadingScene,
            null,
            out AudioClip startupClip,
            out float startupVolume);
        Assert.That(firstLoading, Is.True, "首次 Loading 应请求启动音乐。 ");
        Assert.That(startupClip, Is.EqualTo(catalog.StartupMusic));
        Assert.That(startupVolume, Is.EqualTo(catalog.StartupMusicVolume).Within(0.001f));

        bool transitionLoading = GameAudioService.TryResolveMusicForScene(
            catalog,
            GameSceneNames.LoadingScene,
            startupClip,
            out AudioClip keptClip,
            out _);
        Assert.That(transitionLoading, Is.False, "普通 Loading 不应强行切歌。 ");
        Assert.That(keptClip, Is.EqualTo(startupClip));

        Assert.That(GameAudioService.TryResolveMusicForScene(
            catalog,
            GameSceneNames.LoginScene,
            startupClip,
            out _,
            out _), Is.False, "Login 与启动音乐相同时不应重新播放。 ");

        Assert.That(GameAudioService.TryResolveMusicForScene(
            catalog,
            GameSceneNames.GameplayScene,
            startupClip,
            out AudioClip gameplayClip,
            out float gameplayVolume), Is.True);
        Assert.That(gameplayClip.name, Is.EqualTo("Forest"));
        Assert.That(gameplayVolume, Is.EqualTo(0.55f).Within(0.001f));
    }

    [Test]
    public void CuratedLibrary_ContainsNoMoreThanSixtyAudioClips()
    {
        string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });
        Assert.That(clipGuids.Length, Is.InRange(1, 60), "精选目录应控制在 60 个音频以内。 ");
        Assert.That(AssetDatabase.IsValidFolder("Assets/AllResources/游戏动画音效全集"), Is.False, "完整音效库仍在 Assets，会导致大量无效导入。 ");
    }

    [Test]
    public void MusicAndCoreSfx_UseExpectedLoadStrategies()
    {
        string[] musicNames = { "Happy", "Mystery", "Forest", "Darkness" };
        foreach (string name in musicNames)
        {
            string path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"{name} t:AudioClip", new[] { "Assets/AllResources/Audio/Casual & Relaxing Game Music" }).Single());
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), $"{name} 应使用 Streaming。 ");
            Assert.That(importer.loadInBackground, Is.True, $"{name} 应后台加载。 ");
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioRoot}/SFX/3D" }))
        {
            AudioImporter importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as AudioImporter;
            Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad));
            Assert.That(importer.forceToMono, Is.True, "3D 音效应转为单声道。 ");
        }
    }

    [Test]
    public void FormalScenes_HaveNoLegacyBgmAndMainAmbienceUsesSoundsGroup()
    {
        GameSettingsConfig settings = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>("Assets/Resources/Data/GameSettingsConfig.asset");
        Assert.That(settings, Is.Not.Null);

        foreach (string path in FormalScenePaths)
        {
            Scene scene = OpenSceneForTest(path, out bool openedByTest);
            try
            {
                AudioSource[] sources = SceneComponents<AudioSource>(scene).ToArray();
                Assert.That(sources.Any(source => source.clip != null && IsLegacyMusic(source.clip.name)), Is.False, $"{scene.name} 仍有旧 BGM AudioSource。 ");

                if (scene.name == GameSceneNames.GameplayScene)
                {
                    AudioSource[] ambience = sources.Where(source =>
                        source.gameObject.name.StartsWith("Birds", StringComparison.OrdinalIgnoreCase) ||
                        source.gameObject.name.StartsWith("Wind", StringComparison.OrdinalIgnoreCase) ||
                        source.gameObject.name.StartsWith("Water", StringComparison.OrdinalIgnoreCase)).ToArray();
                    Assert.That(ambience, Has.Length.EqualTo(11), "MainScene 应有 2 个鸟鸣、1 个风声和 8 个水声音源。 ");
                    Assert.That(ambience.All(source => source.clip != null), Is.True);
                    Assert.That(ambience.All(source => source.outputAudioMixerGroup == settings.SoundsMixerGroup), Is.True, "环境音应统一走 Sounds 分组。 ");
                }
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

    [Test]
    public void FormalUiButtons_HaveClickFeedback()
    {
        foreach (string path in UiPrefabPaths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, $"缺少 UI Prefab：{path}");
            Button[] buttons = prefab.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.All(button => button.GetComponent<UiAudioFeedback>() != null), Is.True, $"{path} 有按钮缺少 UiAudioFeedback。 ");
        }

        foreach (string path in FormalScenePaths)
        {
            Scene scene = OpenSceneForTest(path, out bool openedByTest);
            try
            {
                Button[] buttons = SceneComponents<Button>(scene).ToArray();
                Assert.That(buttons.All(button => button.GetComponent<UiAudioFeedback>() != null), Is.True, $"{scene.name} 有按钮缺少 UiAudioFeedback。 ");
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

    private static Scene OpenSceneForTest(string path, out bool openedByTest)
    {
        Scene scene = SceneManager.GetSceneByPath(path);
        openedByTest = !scene.IsValid() || !scene.isLoaded;
        return openedByTest ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
    }

    private static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static bool IsLegacyMusic(string clipName)
    {
        return clipName == "Happy" || clipName == "Mystery" || clipName == "Forest" ||
               clipName == "Darkness" || clipName == "Suntail - Mild Flight" || clipName == "Space Walk";
    }
}
#endif
