#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏音频一次性搭建工具：统一导入参数、生成 Catalog，并修正正式场景和 UI Prefab。
/// 配置集中在编辑器工具里，避免手工拖拽几十个资源时漏配 Mixer 或重复 BGM。
/// </summary>
public static class GameAudioSetupTool
{
    private const string CatalogPath = "Assets/Resources/Data/GameAudioCatalog.asset";
    private const string SettingsConfigPath = "Assets/Resources/Data/GameSettingsConfig.asset";
    private const string MusicRoot = "Assets/AllResources/Audio/Casual & Relaxing Game Music";
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

    [MenuItem("Tools/Treasure Hunter/Audio/Configure Game Audio")]
    public static void ConfigureProject()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureAudioImporters();
        CreateOrUpdateCatalog();
        ConfigurePrefabs();
        ConfigureScenes();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("游戏音频配置完成：Catalog、导入设置、环境音、UI 点击声和旧 BGM 清理均已更新。");
    }

    private static void ConfigureAudioImporters()
    {
        ConfigureImporter(Music("Happy.wav"), AudioClipLoadType.Streaming, false, true, false, AudioCompressionFormat.Vorbis, 0.72f);
        ConfigureImporter(Music("Mystery.wav"), AudioClipLoadType.Streaming, false, true, false, AudioCompressionFormat.Vorbis, 0.72f);
        ConfigureImporter(Music("Forest.wav"), AudioClipLoadType.Streaming, false, true, false, AudioCompressionFormat.Vorbis, 0.72f);
        ConfigureImporter(Music("Darkness.wav"), AudioClipLoadType.Streaming, false, true, false, AudioCompressionFormat.Vorbis, 0.72f);

        foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioRoot}/SFX/2D" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            ConfigureImporter(path, AudioClipLoadType.DecompressOnLoad, false, false, true, AudioCompressionFormat.ADPCM, 1f);
        }

        foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioRoot}/SFX/3D" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            ConfigureImporter(path, AudioClipLoadType.DecompressOnLoad, true, false, true, AudioCompressionFormat.ADPCM, 1f);
        }

        foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { $"{AudioRoot}/Ambience" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            ConfigureImporter(path, AudioClipLoadType.CompressedInMemory, true, true, true, AudioCompressionFormat.Vorbis, 0.7f);
        }
    }

    private static void ConfigureImporter(
        string path,
        AudioClipLoadType loadType,
        bool forceToMono,
        bool loadInBackground,
        bool preloadAudioData,
        AudioCompressionFormat compressionFormat,
        float quality)
    {
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null)
        {
            throw new InvalidOperationException($"找不到音频导入器：{path}");
        }

        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        settings.loadType = loadType;
        settings.compressionFormat = compressionFormat;
        settings.quality = quality;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = forceToMono;
        importer.loadInBackground = loadInBackground;
        importer.preloadAudioData = preloadAudioData;
        importer.SaveAndReimport();
    }

    private static void CreateOrUpdateCatalog()
    {
        GameAudioCatalog catalog = AssetDatabase.LoadAssetAtPath<GameAudioCatalog>(CatalogPath);
        if (catalog == null)
        {
            string directory = Path.GetDirectoryName(CatalogPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            catalog = ScriptableObject.CreateInstance<GameAudioCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        AudioClip happy = Clip(Music("Happy.wav"));
        SceneMusicEntry[] sceneMusic =
        {
            SceneMusic(GameSceneNames.LoginScene, happy, 0.55f),
            SceneMusic(GameSceneNames.CharacterSelectScene, Clip(Music("Mystery.wav")), 0.5f),
            SceneMusic(GameSceneNames.GameplayScene, Clip(Music("Forest.wav")), 0.55f),
            SceneMusic(GameSceneNames.BossRoomScene, Clip(Music("Darkness.wav")), 0.7f)
        };

        GameAudioCue[] cues =
        {
            Cue(GameSfxId.UiClick, 0.7f, 0.96f, 1.04f, false, TwoD("ui_click_01.wav"), TwoD("ui_click_02.wav")),
            Cue(GameSfxId.UiError, 0.8f, 1f, 1f, false, TwoD("ui_error.wav")),
            Cue(GameSfxId.QuestAccepted, 0.8f, 0.98f, 1.02f, false, TwoD("quest_accept.wav")),
            Cue(GameSfxId.QuestRewarded, 0.9f, 0.98f, 1.02f, false, TwoD("quest_reward.wav")),
            Cue(GameSfxId.ShopPurchase, 0.85f, 0.98f, 1.04f, false, TwoD("coin_gain.wav")),
            Cue(GameSfxId.GoldPickup, 0.85f, 0.98f, 1.06f, false, TwoD("coin_gain.wav")),
            Cue(GameSfxId.ItemPickup, 0.8f, 0.96f, 1.05f, false, TwoD("item_pickup_01.wav"), TwoD("item_pickup_02.wav")),

            Cue(GameSfxId.PlayerFootstepWalk, 0.42f, 0.9f, 1.05f, true, ThreeD("footstep_grass_01.wav"), ThreeD("footstep_grass_02.wav"), ThreeD("footstep_grass_03.wav"), ThreeD("footstep_grass_04.wav")),
            Cue(GameSfxId.PlayerFootstepRun, 0.5f, 1.05f, 1.18f, true, ThreeD("footstep_grass_01.wav"), ThreeD("footstep_grass_02.wav"), ThreeD("footstep_grass_03.wav"), ThreeD("footstep_grass_04.wav")),
            Cue(GameSfxId.PlayerJump, 0.7f, 0.97f, 1.04f, true, ThreeD("player_jump.wav")),
            Cue(GameSfxId.PlayerRoll, 0.75f, 0.97f, 1.06f, true, ThreeD("player_roll.wav")),
            Cue(GameSfxId.PlayerAttackWarrior1, 0.85f, 0.94f, 1.04f, true, ThreeD("warrior_attack_01.wav")),
            Cue(GameSfxId.PlayerAttackWarrior2, 0.88f, 0.94f, 1.04f, true, ThreeD("warrior_attack_02.wav")),
            Cue(GameSfxId.PlayerAttackWarrior3, 0.92f, 0.92f, 1.02f, true, ThreeD("warrior_attack_03.wav")),
            Cue(GameSfxId.PlayerAttackAssassin1, 0.8f, 0.98f, 1.08f, true, ThreeD("assassin_attack_01.wav")),
            Cue(GameSfxId.PlayerAttackAssassin2, 0.82f, 0.98f, 1.08f, true, ThreeD("assassin_attack_02.wav")),
            Cue(GameSfxId.PlayerAttackAssassin3, 0.84f, 0.98f, 1.08f, true, ThreeD("assassin_attack_03.wav")),
            Cue(GameSfxId.PlayerAttackArcher, 0.86f, 0.96f, 1.05f, true, ThreeD("archer_attack_01.wav"), ThreeD("archer_attack_02.wav"), ThreeD("archer_attack_03.wav")),
            Cue(GameSfxId.PlayerAttackWizard, 0.82f, 0.95f, 1.06f, true, ThreeD("wizard_attack_01.wav"), ThreeD("wizard_attack_02.wav")),
            Cue(GameSfxId.PlayerHit, 0.82f, 0.94f, 1.05f, true, ThreeD("player_hit.wav")),
            Cue(GameSfxId.PlayerDeath, 0.95f, 0.96f, 1f, true, ThreeD("player_death.wav")),
            Cue(GameSfxId.SkillFireball, 0.9f, 0.95f, 1.05f, true, ThreeD("skill_fireball_01.wav"), ThreeD("skill_fireball_02.wav")),
            Cue(GameSfxId.SkillPoison, 0.82f, 0.94f, 1.04f, true, ThreeD("skill_poison_01.wav"), ThreeD("skill_poison_02.wav")),
            Cue(GameSfxId.SkillSpin, 0.9f, 0.95f, 1.05f, true, ThreeD("skill_spin_01.wav"), ThreeD("skill_spin_02.wav")),
            Cue(GameSfxId.VaultHit, 0.85f, 0.92f, 1.04f, true, ThreeD("vault_hit_01.wav"), ThreeD("vault_hit_02.wav")),
            Cue(GameSfxId.VaultBreak, 1f, 0.96f, 1.02f, true, ThreeD("vault_break.wav")),
            Cue(GameSfxId.PortalEnter, 0.9f, 0.98f, 1.02f, true, ThreeD("portal_enter.wav")),
            Cue(GameSfxId.SlimeMelee, 0.76f, 0.93f, 1.07f, true, ThreeD("slime_attack_01.wav"), ThreeD("slime_attack_02.wav")),
            Cue(GameSfxId.SlimeRanged, 0.8f, 0.94f, 1.05f, true, ThreeD("slime_ranged.wav")),
            Cue(GameSfxId.SlimeHit, 0.78f, 0.94f, 1.08f, true, ThreeD("slime_hit_01.wav"), ThreeD("slime_hit_02.wav")),
            Cue(GameSfxId.SlimeDeath, 0.9f, 0.94f, 1.04f, true, ThreeD("slime_death.wav")),
            Cue(GameSfxId.BossBite, 0.95f, 0.92f, 1.02f, true, ThreeD("boss_bite.wav")),
            Cue(GameSfxId.BossClaw, 0.95f, 0.92f, 1.03f, true, ThreeD("boss_claw_01.wav"), ThreeD("boss_claw_02.wav")),
            Cue(GameSfxId.BossSpell, 0.95f, 0.95f, 1.03f, true, ThreeD("boss_spell.wav")),
            Cue(GameSfxId.BossHit, 0.92f, 0.93f, 1.05f, true, ThreeD("boss_hit_01.wav"), ThreeD("boss_hit_02.wav"), ThreeD("boss_hit_03.wav")),
            Cue(GameSfxId.BossDeath, 1f, 0.96f, 1f, true, ThreeD("boss_death.wav"))
        };

        catalog.ConfigureForEditor(happy, 0.55f, 1f, sceneMusic, cues);
        EditorUtility.SetDirty(catalog);
    }

    private static void ConfigurePrefabs()
    {
        foreach (string path in UiPrefabPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"跳过不存在的 UI Prefab：{path}");
                continue;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = AddButtonFeedback(root);
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureScenes()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in FormalScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                bool changed = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    changed |= AddButtonFeedback(root);
                }

                changed |= RemoveLegacyMusicSources(scene);
                if (scene.name == GameSceneNames.GameplayScene)
                {
                    changed |= ConfigureMainSceneAmbience(scene);
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }
    }

    private static bool AddButtonFeedback(GameObject root)
    {
        bool changed = false;
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<UiAudioFeedback>() == null)
            {
                button.gameObject.AddComponent<UiAudioFeedback>();
                changed = true;
            }
        }

        return changed;
    }

    private static bool RemoveLegacyMusicSources(Scene scene)
    {
        bool changed = false;
        HashSet<string> legacyMusicNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Happy", "Mystery", "Forest", "Darkness", "Suntail - Mild Flight", "Space Walk"
        };

        foreach (AudioSource source in SceneComponents<AudioSource>(scene).ToArray())
        {
            bool namedLegacySource = source.gameObject.name == "Music" || source.gameObject.name == "BossBattleBgm";
            bool hasLegacyMusicClip = source.clip != null && legacyMusicNames.Contains(source.clip.name);
            if (!namedLegacySource && !hasLegacyMusicClip)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(source, true);
            changed = true;
        }

        return changed;
    }

    private static bool ConfigureMainSceneAmbience(Scene scene)
    {
        AudioMixerGroup soundsGroup = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(SettingsConfigPath)?.SoundsMixerGroup;
        AudioClip[] birds = { Clip(Ambience("amb_forest_birds_01.wav")), Clip(Ambience("amb_forest_birds_02.wav")) };
        AudioClip wind = Clip(Ambience("amb_forest_wind.wav"));
        AudioClip[] water = { Clip(Ambience("amb_water_01.wav")), Clip(Ambience("amb_water_02.wav")), Clip(Ambience("amb_water_03.wav")) };
        int birdIndex = 0;
        int waterIndex = 0;
        bool changed = false;

        foreach (AudioSource source in SceneComponents<AudioSource>(scene).ToArray())
        {
            string objectName = source.gameObject.name;
            if (objectName.StartsWith("Birds", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureAmbienceSource(source, birds[birdIndex++ % birds.Length], 0.55f, 45f, soundsGroup);
                changed = true;
            }
            else if (objectName.StartsWith("Wind", StringComparison.OrdinalIgnoreCase))
            {
                ConfigureAmbienceSource(source, wind, 0.42f, 55f, soundsGroup);
                changed = true;
            }
            else if (objectName.StartsWith("Water", StringComparison.OrdinalIgnoreCase))
            {
                // 正式地图只保留 8 个水声区域；旧场景多出的第 9 个音源移除，避免环境声过密。
                if (waterIndex >= 8)
                {
                    UnityEngine.Object.DestroyImmediate(source, true);
                    changed = true;
                    continue;
                }

                ConfigureAmbienceSource(source, water[waterIndex++ % water.Length], 0.52f, 24f, soundsGroup);
                changed = true;
            }
        }

        return changed;
    }

    private static void ConfigureAmbienceSource(AudioSource source, AudioClip clip, float volume, float maxDistance, AudioMixerGroup group)
    {
        source.clip = clip;
        source.outputAudioMixerGroup = group;
        source.playOnAwake = true;
        source.loop = true;
        source.volume = volume;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 3f;
        source.maxDistance = maxDistance;
        EditorUtility.SetDirty(source);
    }

    private static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (T component in root.GetComponentsInChildren<T>(true))
            {
                yield return component;
            }
        }
    }

    private static SceneMusicEntry SceneMusic(string sceneName, AudioClip clip, float volume)
    {
        return new SceneMusicEntry { sceneName = sceneName, clip = clip, volume = volume };
    }

    private static GameAudioCue Cue(GameSfxId id, float volume, float minPitch, float maxPitch, bool spatial, params string[] clipPaths)
    {
        return new GameAudioCue
        {
            id = id,
            clips = clipPaths.Select(Clip).ToArray(),
            volume = volume,
            pitchRange = new Vector2(minPitch, maxPitch),
            spatialBlend = spatial ? 1f : 0f,
            minDistance = spatial ? 1.5f : 1f,
            maxDistance = spatial ? 28f : 1f
        };
    }

    private static AudioClip Clip(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
        {
            throw new InvalidOperationException($"Catalog 缺少音频：{path}");
        }

        return clip;
    }

    private static string Music(string fileName) => $"{MusicRoot}/{fileName}";
    private static string TwoD(string fileName) => $"{AudioRoot}/SFX/2D/{fileName}";
    private static string ThreeD(string fileName) => $"{AudioRoot}/SFX/3D/{fileName}";
    private static string Ambience(string fileName) => $"{AudioRoot}/Ambience/{fileName}";
}
#endif
