using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个场景的背景音乐配置。
/// 场景名称负责匹配 Build Settings，音量是该曲目的基础混音音量。
/// </summary>
[Serializable]
public sealed class SceneMusicEntry
{
    public string sceneName;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 0.6f;
}

/// <summary>
/// 一个语义音效 Cue，可包含多个随机片段，降低脚步和受击声音的机械重复感。
/// </summary>
[Serializable]
public sealed class GameAudioCue
{
    public GameSfxId id;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    public Vector2 pitchRange = Vector2.one;
    [Range(0f, 1f)] public float spatialBlend;
    [Min(0.01f)] public float minDistance = 1f;
    [Min(0.01f)] public float maxDistance = 25f;
}

/// <summary>
/// 游戏音频配置中心：只保存音乐与音效数据，不负责播放。
/// 播放职责交给 GameAudioService，避免 ScriptableObject 持有运行时状态。
/// </summary>
[CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "Treasure Hunter/Game Audio Catalog")]
public sealed class GameAudioCatalog : ScriptableObject
{
    public const string ResourcesPath = "Data/GameAudioCatalog";

    [Header("Scene Music")]
    [SerializeField] private AudioClip startupMusic;
    [SerializeField, Range(0f, 1f)] private float startupMusicVolume = 0.55f;
    [SerializeField, Min(0f)] private float musicCrossFadeDuration = 1f;
    [SerializeField] private SceneMusicEntry[] sceneMusicEntries;

    [Header("Sound Effects")]
    [SerializeField] private GameAudioCue[] soundEffectCues;

    private Dictionary<string, SceneMusicEntry> sceneMusicLookup;
    private Dictionary<GameSfxId, GameAudioCue> cueLookup;

    public AudioClip StartupMusic => startupMusic;
    public float StartupMusicVolume => Mathf.Clamp01(startupMusicVolume);
    public float MusicCrossFadeDuration => Mathf.Max(0f, musicCrossFadeDuration);
    public IReadOnlyList<SceneMusicEntry> SceneMusicEntries => sceneMusicEntries;
    public IReadOnlyList<GameAudioCue> SoundEffectCues => soundEffectCues;

    public bool TryGetSceneMusic(string sceneName, out SceneMusicEntry entry)
    {
        EnsureLookups();
        entry = null;
        return !string.IsNullOrWhiteSpace(sceneName) && sceneMusicLookup.TryGetValue(sceneName, out entry);
    }

    public bool TryGetCue(GameSfxId id, out GameAudioCue cue)
    {
        EnsureLookups();
        cue = null;
        return id != GameSfxId.None && cueLookup.TryGetValue(id, out cue);
    }

    /// <summary>
    /// 配置被编辑器工具更新后清空缓存，确保不重新进 Play Mode 也能读到新数据。
    /// </summary>
    public void RebuildLookups()
    {
        sceneMusicLookup = null;
        cueLookup = null;
        EnsureLookups();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        AudioClip newStartupMusic,
        float newStartupVolume,
        float newCrossFadeDuration,
        SceneMusicEntry[] newSceneMusicEntries,
        GameAudioCue[] newSoundEffectCues)
    {
        startupMusic = newStartupMusic;
        startupMusicVolume = Mathf.Clamp01(newStartupVolume);
        musicCrossFadeDuration = Mathf.Max(0f, newCrossFadeDuration);
        sceneMusicEntries = newSceneMusicEntries;
        soundEffectCues = newSoundEffectCues;
        RebuildLookups();
    }
#endif

    private void OnEnable()
    {
        RebuildLookups();
    }

    private void OnValidate()
    {
        startupMusicVolume = Mathf.Clamp01(startupMusicVolume);
        musicCrossFadeDuration = Mathf.Max(0f, musicCrossFadeDuration);
        RebuildLookups();
    }

    private void EnsureLookups()
    {
        if (sceneMusicLookup != null && cueLookup != null)
        {
            return;
        }

        sceneMusicLookup = new Dictionary<string, SceneMusicEntry>(StringComparer.Ordinal);
        if (sceneMusicEntries != null)
        {
            for (int i = 0; i < sceneMusicEntries.Length; i++)
            {
                SceneMusicEntry entry = sceneMusicEntries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.sceneName) || entry.clip == null)
                {
                    continue;
                }

                sceneMusicLookup[entry.sceneName] = entry;
            }
        }

        cueLookup = new Dictionary<GameSfxId, GameAudioCue>();
        if (soundEffectCues == null)
        {
            return;
        }

        for (int i = 0; i < soundEffectCues.Length; i++)
        {
            GameAudioCue cue = soundEffectCues[i];
            if (cue == null || cue.id == GameSfxId.None || cue.clips == null || cue.clips.Length == 0)
            {
                continue;
            }

            cueLookup[cue.id] = cue;
        }
    }
}
