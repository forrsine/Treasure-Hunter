using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// PlayerPrefs 设置存储：只负责 JSON 与本机键值的读写，不关心设置如何应用。
/// 将存储独立出来后，测试可以使用临时键验证往返数据而不修改玩家真实设置。
/// </summary>
public static class GameSettingsStorage
{
    public static void Save(string key, GameSettingsData settings)
    {
        if (string.IsNullOrWhiteSpace(key) || settings == null)
        {
            return;
        }

        PlayerPrefs.SetString(key, JsonUtility.ToJson(settings));
        PlayerPrefs.Save();
    }

    public static bool TryLoad(string key, out GameSettingsData settings)
    {
        settings = null;
        if (string.IsNullOrWhiteSpace(key) || !PlayerPrefs.HasKey(key))
        {
            return false;
        }

        try
        {
            settings = JsonUtility.FromJson<GameSettingsData>(PlayerPrefs.GetString(key));
            return settings != null && settings.schemaVersion == GameSettingsData.CurrentSchemaVersion;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"本机设置读取失败，将恢复默认设置：{exception.Message}");
            settings = null;
            return false;
        }
    }
}

/// <summary>
/// 全局游戏设置服务：负责读取、校验、应用和保存本机 PC 设置。
/// 服务在首个场景前自动创建并跨场景保留，因此登录、主玩法和 Boss 房间共享同一份设置。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSettingsService : MonoBehaviour
{
    private const string PlayerPrefsKey = "TreasureHunter.GameSettings.V1";
    private const float MinimumAudibleDecibels = -80f;
    private static readonly int[] FallbackFrameRateLimits =
    {
        30, 60, 90, 120, 144, 165, 240, -1
    };

    private static GameSettingsService instance;

    private GameSettingsConfig config;
    private GameSettingsData currentSettings;
    private GameSettingsData defaultSettings;

    public static GameSettingsService Instance => GetOrCreate();

    /// <summary>
    /// 摄像机每帧只读取一个浮点倍率；服务不存在时返回1，保证旧场景和编辑器测试行为不变。
    /// </summary>
    public static float MouseSensitivityMultiplier =>
        instance != null && instance.currentSettings != null
            ? instance.currentSettings.mouseSensitivity
            : 1f;

    public GameSettingsData CurrentSettings =>
        currentSettings != null ? currentSettings.Clone() : DefaultSettings;

    public GameSettingsData DefaultSettings
    {
        get
        {
            EnsureInitialized();
            return defaultSettings.Clone();
        }
    }

    public AudioMixerGroup MusicMixerGroup => config != null ? config.MusicMixerGroup : null;
    public AudioMixerGroup SoundsMixerGroup => config != null ? config.SoundsMixerGroup : null;

    public IReadOnlyList<int> SupportedFrameRateLimits
    {
        get
        {
            int[] configuredLimits = config != null ? config.SupportedFrameRateLimits : null;
            return configuredLimits != null && configuredLimits.Length > 0
                ? configuredLimits
                : FallbackFrameRateLimits;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BootstrapBeforeFirstScene()
    {
        GetOrCreate();
    }

    /// <summary>
    /// 允许其他运行时音源在 Awake 中安全取得服务；编辑模式下不会创建隐藏场景对象。
    /// </summary>
    public static GameSettingsService GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<GameSettingsService>();
        if (instance != null)
        {
            instance.EnsureInitialized();
            return instance;
        }

        GameObject serviceObject = new GameObject(nameof(GameSettingsService));
        instance = serviceObject.AddComponent<GameSettingsService>();
        return instance;
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
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (currentSettings != null)
        {
            return;
        }

        config = Resources.Load<GameSettingsConfig>(GameSettingsConfig.ResourcesPath);
        if (config == null)
        {
            // 配置资产缺失时仍让游戏可运行，只是音乐/音效分轨会暂时不可用。
            config = ScriptableObject.CreateInstance<GameSettingsConfig>();
            Debug.LogWarning("缺少 Resources/Data/GameSettingsConfig，已使用运行时默认设置。 ", this);
        }

        Vector2Int nativeResolution = GetNativeResolution();
        defaultSettings = Sanitize(
            config.CreateDefaultSettings(nativeResolution.x, nativeResolution.y),
            false);

        if (!GameSettingsStorage.TryLoad(PlayerPrefsKey, out GameSettingsData loadedSettings))
        {
            loadedSettings = defaultSettings.Clone();
        }

        currentSettings = Sanitize(loadedSettings, true);
        ApplyRuntimeSettings(currentSettings, true);
    }

    /// <summary>
    /// 滑块拖动时只预览音量和灵敏度，不提前修改分辨率、画质或 PlayerPrefs。
    /// </summary>
    public void PreviewAudioAndSensitivity(GameSettingsData preview)
    {
        EnsureInitialized();
        if (preview == null)
        {
            return;
        }

        currentSettings.masterVolume = Mathf.Clamp01(preview.masterVolume);
        currentSettings.musicVolume = Mathf.Clamp01(preview.musicVolume);
        currentSettings.soundEffectsVolume = Mathf.Clamp01(preview.soundEffectsVolume);
        currentSettings.mouseSensitivity = Mathf.Clamp(preview.mouseSensitivity, 0.5f, 2f);
        ApplyAudio(currentSettings);
    }

    /// <summary>
    /// 应用完整草稿但不落盘。显示设置确认阶段使用此入口，超时后仍可恢复旧快照。
    /// </summary>
    public void Apply(GameSettingsData settings)
    {
        EnsureInitialized();
        currentSettings = Sanitize(settings, false);
        ApplyRuntimeSettings(currentSettings, true);
    }

    public void Restore(GameSettingsData snapshot)
    {
        Apply(snapshot);
    }

    /// <summary>
    /// 保存前再次校验并应用，保证 PlayerPrefs 中不会留下非法枚举、分辨率或帧率值。
    /// </summary>
    public void Save(GameSettingsData settings)
    {
        Apply(settings);
        GameSettingsStorage.Save(PlayerPrefsKey, currentSettings);
    }

    public void Save()
    {
        EnsureInitialized();
        GameSettingsStorage.Save(PlayerPrefsKey, currentSettings);
    }

    public GameSettingsData Sanitize(GameSettingsData source)
    {
        EnsureInitialized();
        return Sanitize(source, false);
    }

    /// <summary>
    /// 生成分辨率下拉选项：去重、过滤过小分辨率并降序排列，同时保留当前和已保存项。
    /// </summary>
    public List<Vector2Int> BuildResolutionOptions(GameSettingsData savedSettings = null)
    {
        EnsureInitialized();
        List<Vector2Int> available = new List<Vector2Int>();
        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            available.Add(new Vector2Int(resolutions[i].width, resolutions[i].height));
        }

        Vector2Int current = new Vector2Int(Screen.width, Screen.height);
        GameSettingsData saved = savedSettings ?? currentSettings;
        Vector2Int savedResolution = new Vector2Int(saved.resolutionWidth, saved.resolutionHeight);
        int minimumWidth = config != null ? config.MinimumResolutionWidth : 1280;
        int minimumHeight = config != null ? config.MinimumResolutionHeight : 720;

        return BuildResolutionOptions(
            available,
            current,
            savedResolution,
            minimumWidth,
            minimumHeight);
    }

    public static List<Vector2Int> BuildResolutionOptions(
        IEnumerable<Vector2Int> availableResolutions,
        Vector2Int currentResolution,
        Vector2Int savedResolution,
        int minimumWidth = 1280,
        int minimumHeight = 720)
    {
        HashSet<Vector2Int> unique = new HashSet<Vector2Int>();
        if (availableResolutions != null)
        {
            foreach (Vector2Int resolution in availableResolutions)
            {
                if (resolution.x >= minimumWidth && resolution.y >= minimumHeight)
                {
                    unique.Add(resolution);
                }
            }
        }

        if (currentResolution.x > 0 && currentResolution.y > 0)
        {
            unique.Add(currentResolution);
        }

        if (savedResolution.x > 0 && savedResolution.y > 0)
        {
            unique.Add(savedResolution);
        }

        List<Vector2Int> results = new List<Vector2Int>(unique);
        results.Sort((left, right) =>
        {
            long leftPixels = (long)left.x * left.y;
            long rightPixels = (long)right.x * right.y;
            int pixelComparison = rightPixels.CompareTo(leftPixels);
            return pixelComparison != 0 ? pixelComparison : right.x.CompareTo(left.x);
        });
        return results;
    }

    public static float LinearVolumeToDecibels(float linearVolume)
    {
        float clampedVolume = Mathf.Clamp01(linearVolume);
        return clampedVolume <= 0.0001f
            ? MinimumAudibleDecibels
            : Mathf.Max(MinimumAudibleDecibels, 20f * Mathf.Log10(clampedVolume));
    }

    /// <summary>
    /// 开启垂直同步时由显示器刷新率控制帧率；关闭后才使用玩家选择的上限。
    /// 独立成纯函数后可以在 EditMode 中验证规则，而不用真的切换项目全局状态。
    /// </summary>
    public static int ResolveTargetFrameRate(bool verticalSync, int selectedFrameRateLimit)
    {
        return verticalSync ? -1 : selectedFrameRateLimit;
    }

    /// <summary>
    /// 自动创建的音乐源统一接入 Music 分组；已有自定义分组时尊重 Inspector 配置。
    /// </summary>
    public static void RouteMusicSource(AudioSource source)
    {
        if (source == null || source.outputAudioMixerGroup != null || !Application.isPlaying)
        {
            return;
        }

        AudioMixerGroup group = GetOrCreate().MusicMixerGroup;
        if (group != null)
        {
            source.outputAudioMixerGroup = group;
        }
    }

    /// <summary>
    /// 玩家和怪物等运行时音效统一接入 Sounds 分组。
    /// </summary>
    public static void RouteSoundsSource(AudioSource source)
    {
        if (source == null || source.outputAudioMixerGroup != null || !Application.isPlaying)
        {
            return;
        }

        AudioMixerGroup group = GetOrCreate().SoundsMixerGroup;
        if (group != null)
        {
            source.outputAudioMixerGroup = group;
        }
    }

    private GameSettingsData Sanitize(GameSettingsData source, bool validateSavedResolution)
    {
        GameSettingsData fallback = defaultSettings ?? source ?? new GameSettingsData();
        GameSettingsData sanitized = source != null ? source.Clone() : fallback.Clone();
        sanitized.schemaVersion = GameSettingsData.CurrentSchemaVersion;
        sanitized.masterVolume = Mathf.Clamp01(sanitized.masterVolume);
        sanitized.musicVolume = Mathf.Clamp01(sanitized.musicVolume);
        sanitized.soundEffectsVolume = Mathf.Clamp01(sanitized.soundEffectsVolume);
        sanitized.mouseSensitivity = Mathf.Clamp(sanitized.mouseSensitivity, 0.5f, 2f);

        if (!Enum.IsDefined(typeof(GameDisplayMode), sanitized.displayMode))
        {
            sanitized.displayMode = fallback.displayMode;
        }

        int qualityCount = Mathf.Max(1, QualitySettings.names.Length);
        sanitized.qualityLevel = Mathf.Clamp(sanitized.qualityLevel, 0, qualityCount - 1);

        if (!IsSupportedFrameRate(sanitized.frameRateLimit))
        {
            sanitized.frameRateLimit = -1;
        }

        bool resolutionInvalid = sanitized.resolutionWidth <= 0 || sanitized.resolutionHeight <= 0;
        if (validateSavedResolution && !resolutionInvalid)
        {
            resolutionInvalid = !IsResolutionSupported(
                sanitized.resolutionWidth,
                sanitized.resolutionHeight);
        }

        if (resolutionInvalid)
        {
            Vector2Int nativeResolution = GetNativeResolution();
            sanitized.resolutionWidth = nativeResolution.x;
            sanitized.resolutionHeight = nativeResolution.y;
        }

        return sanitized;
    }

    private bool IsSupportedFrameRate(int frameRateLimit)
    {
        IReadOnlyList<int> limits = SupportedFrameRateLimits;
        for (int i = 0; i < limits.Count; i++)
        {
            if (limits[i] == frameRateLimit)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsResolutionSupported(int width, int height)
    {
        Resolution[] resolutions = Screen.resolutions;
        if (resolutions == null || resolutions.Length == 0)
        {
            return width > 0 && height > 0;
        }

        for (int i = 0; i < resolutions.Length; i++)
        {
            if (resolutions[i].width == width && resolutions[i].height == height)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2Int GetNativeResolution()
    {
        Resolution native = Screen.currentResolution;
        int width = native.width > 0 ? native.width : Mathf.Max(1, Screen.width);
        int height = native.height > 0 ? native.height : Mathf.Max(1, Screen.height);
        return new Vector2Int(width, height);
    }

    private void ApplyRuntimeSettings(GameSettingsData settings, bool applyDisplay)
    {
        ApplyAudio(settings);

        QualitySettings.SetQualityLevel(settings.qualityLevel, true);
        QualitySettings.vSyncCount = settings.verticalSync ? 1 : 0;
        Application.targetFrameRate = ResolveTargetFrameRate(
            settings.verticalSync,
            settings.frameRateLimit);

        if (!applyDisplay)
        {
            return;
        }

        FullScreenMode mode = settings.displayMode == GameDisplayMode.Windowed
            ? FullScreenMode.Windowed
            : FullScreenMode.FullScreenWindow;

        if (Screen.width != settings.resolutionWidth ||
            Screen.height != settings.resolutionHeight ||
            Screen.fullScreenMode != mode)
        {
            Screen.SetResolution(settings.resolutionWidth, settings.resolutionHeight, mode);
        }
    }

    private void ApplyAudio(GameSettingsData settings)
    {
        AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
        if (config == null || config.AudioMixer == null)
        {
            return;
        }

        config.AudioMixer.SetFloat(
            config.MusicVolumeParameter,
            LinearVolumeToDecibels(settings.musicVolume));
        config.AudioMixer.SetFloat(
            config.SoundsVolumeParameter,
            LinearVolumeToDecibels(settings.soundEffectsVolume));
    }
}
