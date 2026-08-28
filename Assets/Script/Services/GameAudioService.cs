using System.Collections;
using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局音频服务：统一管理跨场景 BGM、2D UI 音效和可复用的 3D 音源池。
/// 业务层只传入 GameSfxId，不需要知道 AudioClip 路径或 AudioMixer 结构。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameAudioService : MonoBehaviour, IController
{
    private const int SpatialSourcePoolSize = 16;

    private static GameAudioService instance;

    private readonly List<AudioSource> spatialSources = new List<AudioSource>(SpatialSourcePoolSize);
    private readonly Dictionary<GameSfxId, int> lastClipIndices = new Dictionary<GameSfxId, int>();

    private GameAudioCatalog catalog;
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource currentMusicSource;
    private AudioSource otherMusicSource;
    private AudioSource twoDimensionalSource;
    private AudioClip currentMusicClip;
    private Coroutine musicCrossFadeRoutine;

    public static GameAudioService Instance => GetOrCreate();
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapBeforeFirstScene()
    {
        GetOrCreate();
    }

    public static GameAudioService GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<GameAudioService>();
        if (instance != null)
        {
            return instance;
        }

        GameObject serviceObject = new GameObject(nameof(GameAudioService));
        instance = serviceObject.AddComponent<GameAudioService>();
        return instance;
    }

    public static bool Play2D(GameSfxId id)
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        return GetOrCreate().PlayCue(id, null, null);
    }

    public static bool PlayAt(GameSfxId id, Vector3 position)
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        return GetOrCreate().PlayCue(id, null, position);
    }

    public static bool PlayOn(GameSfxId id, AudioSource source)
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        return GetOrCreate().PlayCue(id, source, null);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<GameAudioCatalog>(GameAudioCatalog.ResourcesPath);
        if (catalog == null)
        {
            Debug.LogWarning("缺少 Resources/Data/GameAudioCatalog，背景音乐和统一音效暂不可用。", this);
        }

        BuildAudioSources();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (!Application.isPlaying)
        {
            return;
        }

        this.RegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
        this.RegisterEvent<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompleted);
        this.RegisterEvent<QuestAcceptedEvent>(HandleQuestAccepted);
        this.RegisterEvent<QuestRewardClaimedEvent>(HandleQuestRewarded);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
        this.UnRegisterEvent<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompleted);
        this.UnRegisterEvent<QuestAcceptedEvent>(HandleQuestAccepted);
        this.UnRegisterEvent<QuestRewardClaimedEvent>(HandleQuestRewarded);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (TryResolveMusicForScene(catalog, scene.name, currentMusicClip, out AudioClip nextClip, out float targetVolume))
        {
            PlayMusic(nextClip, targetVolume);
        }
    }

    /// <summary>
    /// 纯逻辑的场景音乐决策入口，既供运行时使用，也便于 EditMode 测试 Loading 过渡规则。
    /// 返回 false 表示继续保持当前音乐，不需要重新播放。
    /// </summary>
    public static bool TryResolveMusicForScene(
        GameAudioCatalog audioCatalog,
        string sceneName,
        AudioClip currentClip,
        out AudioClip nextClip,
        out float targetVolume)
    {
        nextClip = currentClip;
        targetVolume = 0f;
        if (audioCatalog == null || string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        // LoadingScene 是短暂过渡场景。已有音乐时保持原曲，首次启动时才播放菜单兜底音乐。
        if (sceneName == GameSceneNames.LoadingScene)
        {
            if (currentClip != null || audioCatalog.StartupMusic == null)
            {
                return false;
            }

            nextClip = audioCatalog.StartupMusic;
            targetVolume = audioCatalog.StartupMusicVolume;
            return true;
        }

        if (!audioCatalog.TryGetSceneMusic(sceneName, out SceneMusicEntry entry) || entry.clip == currentClip)
        {
            return false;
        }

        nextClip = entry.clip;
        targetVolume = entry.volume;
        return true;
    }

    private void BuildAudioSources()
    {
        musicSourceA = CreateSource("Music A", false);
        musicSourceB = CreateSource("Music B", false);
        twoDimensionalSource = CreateSource("2D Sounds", false);

        ConfigureMusicSource(musicSourceA);
        ConfigureMusicSource(musicSourceB);
        twoDimensionalSource.spatialBlend = 0f;
        GameSettingsService.RouteSoundsSource(twoDimensionalSource);

        currentMusicSource = musicSourceA;
        otherMusicSource = musicSourceB;

        for (int i = 0; i < SpatialSourcePoolSize; i++)
        {
            AudioSource source = CreateSource($"3D Sound {i + 1:00}", false);
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            GameSettingsService.RouteSoundsSource(source);
            spatialSources.Add(source);
        }
    }

    private AudioSource CreateSource(string objectName, bool loop)
    {
        GameObject sourceObject = new GameObject(objectName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.dopplerLevel = 0f;
        return source;
    }

    private void ConfigureMusicSource(AudioSource source)
    {
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        GameSettingsService.RouteMusicSource(source);
    }

    private void PlayMusic(AudioClip clip, float targetVolume)
    {
        if (clip == null || currentMusicClip == clip)
        {
            return;
        }

        if (musicCrossFadeRoutine != null)
        {
            StopCoroutine(musicCrossFadeRoutine);
            musicCrossFadeRoutine = null;
        }

        // 上一次淡化被新场景打断时，先停掉更旧的第三路残留，只保留当前正在听到的音乐参与下一次淡出。
        if (otherMusicSource != null && otherMusicSource != currentMusicSource && otherMusicSource.isPlaying)
        {
            otherMusicSource.Stop();
            otherMusicSource.clip = null;
        }

        musicCrossFadeRoutine = StartCoroutine(CrossFadeMusic(clip, Mathf.Clamp01(targetVolume)));
    }

    private IEnumerator CrossFadeMusic(AudioClip nextClip, float targetVolume)
    {
        AudioSource outgoing = currentMusicSource != null && currentMusicSource.isPlaying ? currentMusicSource : null;
        AudioSource incoming = currentMusicSource == musicSourceA ? musicSourceB : musicSourceA;
        float outgoingStartVolume = outgoing != null ? outgoing.volume : 0f;
        float duration = catalog != null ? catalog.MusicCrossFadeDuration : 1f;

        currentMusicSource = incoming;
        otherMusicSource = outgoing;
        currentMusicClip = nextClip;
        incoming.Stop();
        incoming.clip = nextClip;
        incoming.volume = 0f;
        incoming.Play();

        if (duration <= 0f)
        {
            incoming.volume = targetVolume;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                incoming.volume = Mathf.Lerp(0f, targetVolume, progress);
                if (outgoing != null)
                {
                    outgoing.volume = Mathf.Lerp(outgoingStartVolume, 0f, progress);
                }

                yield return null;
            }
        }

        if (outgoing != null)
        {
            outgoing.Stop();
            outgoing.clip = null;
            outgoing.volume = 0f;
        }

        musicCrossFadeRoutine = null;
    }

    private bool PlayCue(GameSfxId id, AudioSource requestedSource, Vector3? position)
    {
        if (catalog == null || !catalog.TryGetCue(id, out GameAudioCue cue))
        {
            return false;
        }

        AudioClip clip = SelectClip(cue);
        if (clip == null)
        {
            return false;
        }

        AudioSource source = requestedSource;
        if (source == null)
        {
            source = position.HasValue ? GetAvailableSpatialSource() : twoDimensionalSource;
        }

        if (source == null)
        {
            return false;
        }

        GameSettingsService.RouteSoundsSource(source);
        source.loop = false;
        source.spatialBlend = position.HasValue ? Mathf.Max(cue.spatialBlend, 1f) : cue.spatialBlend;
        source.minDistance = Mathf.Max(0.01f, cue.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        source.pitch = Random.Range(
            Mathf.Min(cue.pitchRange.x, cue.pitchRange.y),
            Mathf.Max(cue.pitchRange.x, cue.pitchRange.y));

        if (position.HasValue)
        {
            source.transform.position = position.Value;
            source.clip = clip;
            source.volume = Mathf.Clamp01(cue.volume);
            source.Play();
        }
        else
        {
            source.PlayOneShot(clip, Mathf.Clamp01(cue.volume));
        }

        return true;
    }

    private AudioSource GetAvailableSpatialSource()
    {
        for (int i = 0; i < spatialSources.Count; i++)
        {
            if (!spatialSources[i].isPlaying)
            {
                return spatialSources[i];
            }
        }

        // 同时声音超过池容量时复用最早的音源，保持内存稳定而不是临时创建对象。
        return spatialSources.Count > 0 ? spatialSources[0] : null;
    }

    private AudioClip SelectClip(GameAudioCue cue)
    {
        if (cue == null || cue.clips == null || cue.clips.Length == 0)
        {
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < cue.clips.Length; i++)
        {
            if (cue.clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return null;
        }

        int selectedValidIndex = Random.Range(0, validCount);
        if (validCount > 1 && lastClipIndices.TryGetValue(cue.id, out int previousValidIndex) &&
            selectedValidIndex == previousValidIndex)
        {
            selectedValidIndex = (selectedValidIndex + 1) % validCount;
        }

        lastClipIndices[cue.id] = selectedValidIndex;
        for (int i = 0; i < cue.clips.Length; i++)
        {
            if (cue.clips[i] == null)
            {
                continue;
            }

            if (selectedValidIndex-- == 0)
            {
                return cue.clips[i];
            }
        }

        return null;
    }

    private void HandlePlayerDied(PlayerDiedEvent _)
    {
        PlayerRuntimeController player = GameplayRuntime.Instance.CurrentPlayer;
        if (player != null)
        {
            PlayAt(GameSfxId.PlayerDeath, player.transform.position);
        }
        else
        {
            Play2D(GameSfxId.PlayerDeath);
        }
    }

    private void HandleShopPurchaseCompleted(ShopPurchaseCompletedEvent _)
    {
        Play2D(GameSfxId.ShopPurchase);
    }

    private void HandleQuestAccepted(QuestAcceptedEvent _)
    {
        Play2D(GameSfxId.QuestAccepted);
    }

    private void HandleQuestRewarded(QuestRewardClaimedEvent _)
    {
        Play2D(GameSfxId.QuestRewarded);
    }
}
