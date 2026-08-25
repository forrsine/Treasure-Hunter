using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板控制器：维护“打开前快照”和“正在编辑的草稿”。
/// UI 只负责收集玩家选择，真正的持久化与 Unity 全局设置操作交给 GameSettingsService。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameSettingsPanelController : MonoBehaviour
{
    private const float DisplayConfirmationDuration = 10f;

    [Header("Audio And Input")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Text masterVolumeValueText;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Text musicVolumeValueText;
    [SerializeField] private Slider soundEffectsVolumeSlider;
    [SerializeField] private Text soundEffectsVolumeValueText;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Text mouseSensitivityValueText;

    [Header("Display And Performance")]
    [SerializeField] private Dropdown resolutionDropdown;
    [SerializeField] private Dropdown displayModeDropdown;
    [SerializeField] private Dropdown qualityDropdown;
    [SerializeField] private Toggle verticalSyncToggle;
    [SerializeField] private Dropdown frameRateDropdown;

    [Header("Actions")]
    [SerializeField] private Button restoreDefaultsButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button applyButton;

    [Header("Display Confirmation")]
    [SerializeField] private GameObject displayConfirmationPanel;
    [SerializeField] private Text displayConfirmationText;
    [SerializeField] private Button keepDisplaySettingsButton;
    [SerializeField] private Button revertDisplaySettingsButton;

    private readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();
    private readonly List<int> frameRateOptions = new List<int>();

    private GameSettingsService settingsService;
    private GameSettingsData openingSnapshot;
    private GameSettingsData draft;
    private float displayConfirmationRemaining;
    private bool suppressUiCallbacks;
    private bool listenersRegistered;
    private bool hasOpenSession;
    private bool awaitingDisplayConfirmation;

    public bool IsAwaitingDisplayConfirmation => awaitingDisplayConfirmation;

    private void Awake()
    {
        EnsureService();
    }

    private void OnEnable()
    {
        RegisterListeners();
    }

    private void OnDisable()
    {
        UnregisterListeners();

        // 切场景、对象被禁用或确认弹窗意外关闭时都恢复快照，防止临时黑屏设置残留。
        if (hasOpenSession && openingSnapshot != null && settingsService != null)
        {
            settingsService.Restore(openingSnapshot);
        }

        hasOpenSession = false;
        awaitingDisplayConfirmation = false;
        openingSnapshot = null;
        draft = null;
    }

    private void Update()
    {
        if (!hasOpenSession)
        {
            return;
        }

        if (awaitingDisplayConfirmation)
        {
            displayConfirmationRemaining -= Time.unscaledDeltaTime;
            RefreshDisplayConfirmationText();
            if (displayConfirmationRemaining <= 0f)
            {
                RevertDisplaySettings();
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (awaitingDisplayConfirmation)
            {
                RevertDisplaySettings();
            }
            else
            {
                CancelAndClose();
            }
        }
    }

    /// <summary>
    /// 登录场景设置按钮调用入口。每次打开都重新读取当前设置并建立取消用快照。
    /// </summary>
    public void Open()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        EnsureService();
        RegisterListeners();
        UiCursorStateUtility.EnsureVisibleAndUnlocked();

        openingSnapshot = settingsService.CurrentSettings;
        draft = openingSnapshot.Clone();
        hasOpenSession = true;
        awaitingDisplayConfirmation = false;
        PopulateStaticOptions();
        RefreshAllControls();

        if (displayConfirmationPanel != null)
        {
            displayConfirmationPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Prefab 回归测试和 Inspector 排查共用的完整性检查。
    /// </summary>
    public bool ValidatePrefabReferences(bool logErrors = true)
    {
        bool valid = masterVolumeSlider != null &&
                     masterVolumeValueText != null &&
                     musicVolumeSlider != null &&
                     musicVolumeValueText != null &&
                     soundEffectsVolumeSlider != null &&
                     soundEffectsVolumeValueText != null &&
                     mouseSensitivitySlider != null &&
                     mouseSensitivityValueText != null &&
                     resolutionDropdown != null &&
                     displayModeDropdown != null &&
                     qualityDropdown != null &&
                     verticalSyncToggle != null &&
                     frameRateDropdown != null &&
                     restoreDefaultsButton != null &&
                     cancelButton != null &&
                     applyButton != null &&
                     displayConfirmationPanel != null &&
                     displayConfirmationText != null &&
                     keepDisplaySettingsButton != null &&
                     revertDisplaySettingsButton != null;

        if (!valid && logErrors)
        {
            Debug.LogError("GameSettingsPanel Prefab 的序列化引用不完整，请运行设置面板装配工具。", this);
        }

        return valid;
    }

    private void EnsureService()
    {
        if (settingsService == null)
        {
            settingsService = GameSettingsService.Instance;
        }
    }

    private void RegisterListeners()
    {
        if (listenersRegistered || !ValidatePrefabReferences(false))
        {
            return;
        }

        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        soundEffectsVolumeSlider.onValueChanged.AddListener(OnSoundEffectsVolumeChanged);
        mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        displayModeDropdown.onValueChanged.AddListener(OnDisplayModeChanged);
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        verticalSyncToggle.onValueChanged.AddListener(OnVerticalSyncChanged);
        frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);
        restoreDefaultsButton.onClick.AddListener(RestoreDefaults);
        cancelButton.onClick.AddListener(CancelAndClose);
        applyButton.onClick.AddListener(ApplyDraft);
        keepDisplaySettingsButton.onClick.AddListener(KeepDisplaySettings);
        revertDisplaySettingsButton.onClick.AddListener(RevertDisplaySettings);
        listenersRegistered = true;
    }

    private void UnregisterListeners()
    {
        if (!listenersRegistered)
        {
            return;
        }

        masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        soundEffectsVolumeSlider.onValueChanged.RemoveListener(OnSoundEffectsVolumeChanged);
        mouseSensitivitySlider.onValueChanged.RemoveListener(OnMouseSensitivityChanged);
        resolutionDropdown.onValueChanged.RemoveListener(OnResolutionChanged);
        displayModeDropdown.onValueChanged.RemoveListener(OnDisplayModeChanged);
        qualityDropdown.onValueChanged.RemoveListener(OnQualityChanged);
        verticalSyncToggle.onValueChanged.RemoveListener(OnVerticalSyncChanged);
        frameRateDropdown.onValueChanged.RemoveListener(OnFrameRateChanged);
        restoreDefaultsButton.onClick.RemoveListener(RestoreDefaults);
        cancelButton.onClick.RemoveListener(CancelAndClose);
        applyButton.onClick.RemoveListener(ApplyDraft);
        keepDisplaySettingsButton.onClick.RemoveListener(KeepDisplaySettings);
        revertDisplaySettingsButton.onClick.RemoveListener(RevertDisplaySettings);
        listenersRegistered = false;
    }

    private void PopulateStaticOptions()
    {
        suppressUiCallbacks = true;

        resolutionOptions.Clear();
        resolutionOptions.AddRange(settingsService.BuildResolutionOptions(draft));
        List<string> resolutionLabels = new List<string>(resolutionOptions.Count);
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            resolutionLabels.Add($"{resolutionOptions[i].x} × {resolutionOptions[i].y}");
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(resolutionLabels);

        displayModeDropdown.ClearOptions();
        displayModeDropdown.AddOptions(new List<string> { "无边框全屏", "窗口化" });

        qualityDropdown.ClearOptions();
        string[] qualityNames = QualitySettings.names;
        List<string> qualityLabels = new List<string>(qualityNames.Length);
        for (int i = 0; i < qualityNames.Length; i++)
        {
            qualityLabels.Add(GetLocalizedQualityName(qualityNames[i]));
        }

        qualityDropdown.AddOptions(qualityLabels);

        frameRateOptions.Clear();
        IReadOnlyList<int> configuredFrameRates = settingsService.SupportedFrameRateLimits;
        List<string> frameRateLabels = new List<string>(configuredFrameRates.Count);
        for (int i = 0; i < configuredFrameRates.Count; i++)
        {
            int frameRate = configuredFrameRates[i];
            frameRateOptions.Add(frameRate);
            frameRateLabels.Add(frameRate < 0 ? "不限" : $"{frameRate} FPS");
        }

        frameRateDropdown.ClearOptions();
        frameRateDropdown.AddOptions(frameRateLabels);
        suppressUiCallbacks = false;
    }

    private void RefreshAllControls()
    {
        if (draft == null || !ValidatePrefabReferences(false))
        {
            return;
        }

        suppressUiCallbacks = true;
        masterVolumeSlider.SetValueWithoutNotify(draft.masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(draft.musicVolume);
        soundEffectsVolumeSlider.SetValueWithoutNotify(draft.soundEffectsVolume);
        mouseSensitivitySlider.SetValueWithoutNotify(draft.mouseSensitivity);
        resolutionDropdown.SetValueWithoutNotify(FindResolutionIndex(draft));
        displayModeDropdown.SetValueWithoutNotify(
            draft.displayMode == GameDisplayMode.Windowed ? 1 : 0);
        qualityDropdown.SetValueWithoutNotify(
            Mathf.Clamp(draft.qualityLevel, 0, Mathf.Max(0, qualityDropdown.options.Count - 1)));
        verticalSyncToggle.SetIsOnWithoutNotify(draft.verticalSync);
        frameRateDropdown.SetValueWithoutNotify(FindFrameRateIndex(draft.frameRateLimit));
        frameRateDropdown.interactable = !draft.verticalSync;
        suppressUiCallbacks = false;
        RefreshPercentageLabels();
    }

    private void RefreshPercentageLabels()
    {
        if (draft == null)
        {
            return;
        }

        masterVolumeValueText.text = FormatPercentage(draft.masterVolume);
        musicVolumeValueText.text = FormatPercentage(draft.musicVolume);
        soundEffectsVolumeValueText.text = FormatPercentage(draft.soundEffectsVolume);
        mouseSensitivityValueText.text = FormatPercentage(draft.mouseSensitivity);
    }

    private void OnMasterVolumeChanged(float value)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.masterVolume = value;
        PreviewAudioAndSensitivity();
    }

    private void OnMusicVolumeChanged(float value)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.musicVolume = value;
        PreviewAudioAndSensitivity();
    }

    private void OnSoundEffectsVolumeChanged(float value)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.soundEffectsVolume = value;
        PreviewAudioAndSensitivity();
    }

    private void OnMouseSensitivityChanged(float value)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.mouseSensitivity = value;
        PreviewAudioAndSensitivity();
    }

    private void PreviewAudioAndSensitivity()
    {
        settingsService.PreviewAudioAndSensitivity(draft);
        RefreshPercentageLabels();
    }

    private void OnResolutionChanged(int index)
    {
        if (!CanHandleUiCallback() || index < 0 || index >= resolutionOptions.Count)
        {
            return;
        }

        draft.resolutionWidth = resolutionOptions[index].x;
        draft.resolutionHeight = resolutionOptions[index].y;
    }

    private void OnDisplayModeChanged(int index)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.displayMode = index == 1
            ? GameDisplayMode.Windowed
            : GameDisplayMode.BorderlessFullscreen;
    }

    private void OnQualityChanged(int index)
    {
        if (CanHandleUiCallback())
        {
            draft.qualityLevel = index;
        }
    }

    private void OnVerticalSyncChanged(bool enabled)
    {
        if (!CanHandleUiCallback())
        {
            return;
        }

        draft.verticalSync = enabled;
        frameRateDropdown.interactable = !enabled;
    }

    private void OnFrameRateChanged(int index)
    {
        if (!CanHandleUiCallback() || index < 0 || index >= frameRateOptions.Count)
        {
            return;
        }

        draft.frameRateLimit = frameRateOptions[index];
    }

    private void RestoreDefaults()
    {
        if (!hasOpenSession || awaitingDisplayConfirmation)
        {
            return;
        }

        draft = settingsService.DefaultSettings;
        PopulateStaticOptions();
        RefreshAllControls();
        settingsService.PreviewAudioAndSensitivity(draft);
    }

    private void ApplyDraft()
    {
        if (!hasOpenSession || draft == null || awaitingDisplayConfirmation)
        {
            return;
        }

        draft = settingsService.Sanitize(draft);
        bool requiresDisplayConfirmation = draft.HasRiskyDisplayDifference(openingSnapshot);
        settingsService.Apply(draft);

        if (!requiresDisplayConfirmation)
        {
            settingsService.Save();
            FinishSessionAndClose();
            return;
        }

        awaitingDisplayConfirmation = true;
        displayConfirmationRemaining = DisplayConfirmationDuration;
        displayConfirmationPanel.SetActive(true);
        RefreshDisplayConfirmationText();
    }

    private void KeepDisplaySettings()
    {
        if (!awaitingDisplayConfirmation)
        {
            return;
        }

        settingsService.Save();
        awaitingDisplayConfirmation = false;
        FinishSessionAndClose();
    }

    private void RevertDisplaySettings()
    {
        if (!awaitingDisplayConfirmation || openingSnapshot == null)
        {
            return;
        }

        settingsService.Restore(openingSnapshot);
        draft = openingSnapshot.Clone();
        awaitingDisplayConfirmation = false;
        displayConfirmationPanel.SetActive(false);
        PopulateStaticOptions();
        RefreshAllControls();
    }

    private void CancelAndClose()
    {
        if (!hasOpenSession || awaitingDisplayConfirmation)
        {
            return;
        }

        settingsService.Restore(openingSnapshot);
        FinishSessionAndClose();
    }

    private void FinishSessionAndClose()
    {
        hasOpenSession = false;
        awaitingDisplayConfirmation = false;
        openingSnapshot = null;
        draft = null;
        gameObject.SetActive(false);
    }

    private void RefreshDisplayConfirmationText()
    {
        if (displayConfirmationText == null)
        {
            return;
        }

        int seconds = Mathf.Max(0, Mathf.CeilToInt(displayConfirmationRemaining));
        displayConfirmationText.text =
            $"是否保留新的显示设置？\n{seconds} 秒后自动恢复";
    }

    private int FindResolutionIndex(GameSettingsData settings)
    {
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].x == settings.resolutionWidth &&
                resolutionOptions[i].y == settings.resolutionHeight)
            {
                return i;
            }
        }

        return 0;
    }

    private int FindFrameRateIndex(int frameRateLimit)
    {
        int unlimitedIndex = 0;
        for (int i = 0; i < frameRateOptions.Count; i++)
        {
            if (frameRateOptions[i] < 0)
            {
                unlimitedIndex = i;
            }

            if (frameRateOptions[i] == frameRateLimit)
            {
                return i;
            }
        }

        return unlimitedIndex;
    }

    private bool CanHandleUiCallback()
    {
        return !suppressUiCallbacks && hasOpenSession && draft != null && !awaitingDisplayConfirmation;
    }

    private static string FormatPercentage(float value)
    {
        return $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private static string GetLocalizedQualityName(string qualityName)
    {
        switch (qualityName)
        {
            case "Very Low":
                return "极低";
            case "Low":
                return "低";
            case "Medium":
                return "中";
            case "High":
                return "高";
            case "Very High":
                return "很高";
            case "Ultra":
                return "极致";
            default:
                return qualityName;
        }
    }
}
