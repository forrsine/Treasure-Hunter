using System;
using UnityEngine;

/// <summary>
/// PC 显示模式。当前只开放无边框全屏和窗口化，避免独占全屏切换失败后难以恢复。
/// </summary>
public enum GameDisplayMode
{
    BorderlessFullscreen = 0,
    Windowed = 1
}

/// <summary>
/// 本机游戏设置数据。
/// 这里只描述数据，不直接操作 Screen、AudioMixer 或 QualitySettings，避免数据层依赖表现层。
/// </summary>
[Serializable]
public sealed class GameSettingsData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float soundEffectsVolume = 1f;
    public float mouseSensitivity = 1f;
    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public GameDisplayMode displayMode = GameDisplayMode.BorderlessFullscreen;
    public int qualityLevel = 5;
    public bool verticalSync = true;
    public int frameRateLimit = -1;

    /// <summary>
    /// 面板编辑的是副本，只有确认应用后才替换全局设置，避免点取消时污染正式数据。
    /// </summary>
    public GameSettingsData Clone()
    {
        return new GameSettingsData
        {
            schemaVersion = schemaVersion,
            masterVolume = masterVolume,
            musicVolume = musicVolume,
            soundEffectsVolume = soundEffectsVolume,
            mouseSensitivity = mouseSensitivity,
            resolutionWidth = resolutionWidth,
            resolutionHeight = resolutionHeight,
            displayMode = displayMode,
            qualityLevel = qualityLevel,
            verticalSync = verticalSync,
            frameRateLimit = frameRateLimit
        };
    }

    /// <summary>
    /// 分辨率或窗口模式改变时需要进入10秒确认流程，其他设置不需要承担黑屏风险。
    /// </summary>
    public bool HasRiskyDisplayDifference(GameSettingsData other)
    {
        if (other == null)
        {
            return true;
        }

        return resolutionWidth != other.resolutionWidth ||
               resolutionHeight != other.resolutionHeight ||
               displayMode != other.displayMode;
    }
}
