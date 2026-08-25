using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 设置系统的静态配置：保存 AudioMixer 引用、默认值和可选帧率。
/// 配置资产放在 Resources/Data 下，让任意首个场景都能初始化设置服务。
/// </summary>
[CreateAssetMenu(fileName = "GameSettingsConfig", menuName = "Treasure Hunter/Game Settings Config")]
public sealed class GameSettingsConfig : ScriptableObject
{
    public const string ResourcesPath = "Data/GameSettingsConfig";

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicMixerGroup;
    [SerializeField] private AudioMixerGroup soundsMixerGroup;
    [SerializeField] private string musicVolumeParameter = "musicVolume";
    [SerializeField] private string soundsVolumeParameter = "soundsVolume";

    [Header("Defaults")]
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultSoundEffectsVolume = 1f;
    [SerializeField, Range(0.5f, 2f)] private float defaultMouseSensitivity = 1f;
    [SerializeField] private GameDisplayMode defaultDisplayMode = GameDisplayMode.BorderlessFullscreen;
    [SerializeField, Min(0)] private int defaultQualityLevel = 5;
    [SerializeField] private bool defaultVerticalSync = true;
    [SerializeField] private int defaultFrameRateLimit = -1;

    [Header("Options")]
    [SerializeField] private int minimumResolutionWidth = 1280;
    [SerializeField] private int minimumResolutionHeight = 720;
    [SerializeField] private int[] supportedFrameRateLimits =
    {
        30, 60, 90, 120, 144, 165, 240, -1
    };

    public AudioMixer AudioMixer => audioMixer;
    public AudioMixerGroup MusicMixerGroup => musicMixerGroup;
    public AudioMixerGroup SoundsMixerGroup => soundsMixerGroup;
    public string MusicVolumeParameter => musicVolumeParameter;
    public string SoundsVolumeParameter => soundsVolumeParameter;
    public int MinimumResolutionWidth => Mathf.Max(1, minimumResolutionWidth);
    public int MinimumResolutionHeight => Mathf.Max(1, minimumResolutionHeight);
    public int[] SupportedFrameRateLimits => supportedFrameRateLimits;

    /// <summary>
    /// 默认分辨率在启动时读取当前显示器，而不是把开发机器的分辨率写死进配置资产。
    /// </summary>
    public GameSettingsData CreateDefaultSettings(int nativeWidth, int nativeHeight)
    {
        return new GameSettingsData
        {
            schemaVersion = GameSettingsData.CurrentSchemaVersion,
            masterVolume = Mathf.Clamp01(defaultMasterVolume),
            musicVolume = Mathf.Clamp01(defaultMusicVolume),
            soundEffectsVolume = Mathf.Clamp01(defaultSoundEffectsVolume),
            mouseSensitivity = Mathf.Clamp(defaultMouseSensitivity, 0.5f, 2f),
            resolutionWidth = Mathf.Max(1, nativeWidth),
            resolutionHeight = Mathf.Max(1, nativeHeight),
            displayMode = defaultDisplayMode,
            qualityLevel = Mathf.Max(0, defaultQualityLevel),
            verticalSync = defaultVerticalSync,
            frameRateLimit = defaultFrameRateLimit
        };
    }

    private void OnValidate()
    {
        defaultMasterVolume = Mathf.Clamp01(defaultMasterVolume);
        defaultMusicVolume = Mathf.Clamp01(defaultMusicVolume);
        defaultSoundEffectsVolume = Mathf.Clamp01(defaultSoundEffectsVolume);
        defaultMouseSensitivity = Mathf.Clamp(defaultMouseSensitivity, 0.5f, 2f);
        defaultQualityLevel = Mathf.Max(0, defaultQualityLevel);
        minimumResolutionWidth = Mathf.Max(1, minimumResolutionWidth);
        minimumResolutionHeight = Mathf.Max(1, minimumResolutionHeight);

        if (supportedFrameRateLimits == null || supportedFrameRateLimits.Length == 0)
        {
            supportedFrameRateLimits = new[] { 30, 60, 90, 120, 144, 165, 240, -1 };
        }
    }
}
