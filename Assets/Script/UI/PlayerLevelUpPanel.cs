using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家升级奖励三选一面板：复用同一套 UI 处理属性强化和技能学习/升级。
/// 注意：面板只负责展示候选项和提交玩家选择，具体规则仍然交给 ProgressionSystem 和 SkillSystem。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerLevelUpPanel : MonoBehaviour, IController
{
    private const int ChoiceCount = 3;

    /// <summary>
    /// 当前面板正在显示的奖励类型。
    /// Attribute 表示属性强化；Skill 表示技能学习或升级。
    /// </summary>
    private enum UpgradeRewardMode
    {
        None,
        Attribute,
        Skill
    }

    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text subtitleText;
    [SerializeField] private Text queueText;
    [SerializeField] private Button[] optionButtons = new Button[ChoiceCount];
    [SerializeField] private Text[] optionTexts = new Text[ChoiceCount];

    [Header("Editor Preview")]
    [SerializeField] private bool editorPreviewVisible = true;
    [SerializeField] private int editorPreviewPendingCount = 2;

    private UpgradeRewardMode currentMode = UpgradeRewardMode.None;
    private bool isVisible;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.Locked;
    private bool cachedCursorVisible;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 运行时同时监听属性选择队列和技能选择队列。
    /// 两类事件都进入 RefreshPanelState，保证同一时间只展示一种奖励选择。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            this.RegisterEvent<PlayerUpgradeQueueChangedEvent>(HandlePendingUpgradeSelectionsChanged);
            this.RegisterEvent<PlayerSkillSelectionQueueChangedEvent>(HandlePendingSkillSelectionsChanged);
            return;
        }

        ApplyEditorPreviewIfReady();
    }

    /// <summary>
    /// 停用时取消事件订阅，并恢复暂停前的游戏状态。
    /// </summary>
    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<PlayerUpgradeQueueChangedEvent>(HandlePendingUpgradeSelectionsChanged);
        this.UnRegisterEvent<PlayerSkillSelectionQueueChangedEvent>(HandlePendingSkillSelectionsChanged);
        HidePanel();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
        }
    }

    /// <summary>
    /// 运行时校验 Prefab 引用并默认隐藏面板。
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
            return;
        }

        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        SetVisible(false);
    }

    private void HandlePendingUpgradeSelectionsChanged(PlayerUpgradeQueueChangedEvent e)
    {
        RefreshPanelState();
    }

    private void HandlePendingSkillSelectionsChanged(PlayerSkillSelectionQueueChangedEvent e)
    {
        RefreshPanelState();
    }

    /// <summary>
    /// 校验静态 Prefab 引用。
    /// 这里不在运行时自动创建 UI，是为了避免隐藏引用问题被兜底逻辑盖住。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (overlayRoot == null) missing = nameof(overlayRoot);
        else if (panelRoot == null) missing = nameof(panelRoot);
        else if (titleText == null) missing = nameof(titleText);
        else if (subtitleText == null) missing = nameof(subtitleText);
        else if (queueText == null) missing = nameof(queueText);
        else if (optionButtons == null || optionButtons.Length != ChoiceCount) missing = nameof(optionButtons);
        else if (optionTexts == null || optionTexts.Length != ChoiceCount) missing = nameof(optionTexts);
        else
        {
            for (int i = 0; i < ChoiceCount; i++)
            {
                if (optionButtons[i] == null)
                {
                    missing = $"{nameof(optionButtons)}[{i}]";
                    break;
                }

                if (optionTexts[i] == null)
                {
                    missing = $"{nameof(optionTexts)}[{i}]";
                    break;
                }
            }
        }

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"PlayerLevelUpPanel 的 Prefab 引用未配置：{missing}。请修复 GameplayUiRoot.prefab。", this);
        }

        return false;
    }

    /// <summary>
    /// 编辑器预览内容，只用于检查布局和引用是否完整。
    /// </summary>
    private void ApplyEditorPreviewIfReady()
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        titleText.text = "等级提升";
        subtitleText.text = "选择 1 项奖励";
        queueText.text = $"待选择：{editorPreviewPendingCount}";
        for (int i = 0; i < ChoiceCount; i++)
        {
            optionButtons[i].gameObject.SetActive(true);
            optionButtons[i].onClick.RemoveAllListeners();
            optionTexts[i].text = $"示例奖励 {i + 1}\nPrefab 中可直接调整样式";
        }

        SetVisible(editorPreviewVisible);
    }

    /// <summary>
    /// 统一刷新面板状态。
    /// 优先显示属性强化；属性处理完后，如果仍有技能选择次数，再显示技能选择。
    /// </summary>
    private void RefreshPanelState()
    {
        if (!ValidatePrefabReferences(true))
        {
            return;
        }

        PlayerModel playerModel = this.GetModel<PlayerModel>();
        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();

        if (playerModel != null && playerModel.Stats.PendingUpgradeSelectionCount > 0)
        {
            ShowAttributeSelection();
            return;
        }

        if (skillModel != null && skillModel.PendingSkillSelectionCount > 0)
        {
            ShowSkillSelection();
            return;
        }

        HidePanel();
    }

    /// <summary>
    /// 显示属性三选一。
    /// 候选项来自 PlayerProgressionSystem，UI 不直接写属性成长规则。
    /// </summary>
    private void ShowAttributeSelection()
    {
        currentMode = UpgradeRewardMode.Attribute;

        var choices = this.SendQuery(new GetPlayerUpgradeChoicesQuery(ChoiceCount));
        if (choices.Count == 0)
        {
            HidePanel();
            return;
        }

        CaptureGameplayStateIfNeeded();
        this.SendCommand(new SetPlayerUpgradeSelectionStateCommand(true));

        titleText.text = "等级提升";
        subtitleText.text = "选择 1 项属性强化";
        queueText.text = $"属性待选择：{this.GetModel<PlayerModel>().Stats.PendingUpgradeSelectionCount}";

        for (int i = 0; i < ChoiceCount; i++)
        {
            bool hasChoice = i < choices.Count;
            optionButtons[i].gameObject.SetActive(hasChoice);
            optionButtons[i].onClick.RemoveAllListeners();
            if (!hasChoice)
            {
                continue;
            }

            PlayerAttributeType selectedType = choices[i];
            optionTexts[i].text = this.SendQuery(new GetPlayerUpgradeOptionTextQuery(selectedType));
            optionButtons[i].onClick.AddListener(() => OnAttributeOptionSelected(selectedType));
        }

        SetVisible(true);
    }

    /// <summary>
    /// 显示技能三选一。
    /// 候选项来自 PlayerSkillSystem，包括学习新技能和升级已有技能。
    /// </summary>
    private void ShowSkillSelection()
    {
        currentMode = UpgradeRewardMode.Skill;

        var choices = this.SendQuery(new GetPlayerSkillChoicesQuery(ChoiceCount));
        if (choices.Count == 0)
        {
            HidePanel();
            return;
        }

        CaptureGameplayStateIfNeeded();
        this.SendCommand(new SetPlayerUpgradeSelectionStateCommand(true));

        titleText.text = "技能选择";
        subtitleText.text = "学习新技能，或升级已有技能";
        queueText.text = $"技能待选择：{this.GetModel<PlayerSkillModel>().PendingSkillSelectionCount}";

        for (int i = 0; i < ChoiceCount; i++)
        {
            bool hasChoice = i < choices.Count;
            optionButtons[i].gameObject.SetActive(hasChoice);
            optionButtons[i].onClick.RemoveAllListeners();
            if (!hasChoice)
            {
                continue;
            }

            PlayerSkillChoice selectedChoice = choices[i];
            optionTexts[i].text = this.SendQuery(new GetPlayerSkillChoiceTextQuery(selectedChoice));
            optionButtons[i].onClick.AddListener(() => OnSkillOptionSelected(selectedChoice));
        }

        SetVisible(true);
    }

    /// <summary>
    /// 玩家选择属性强化后调用。
    /// 处理成功后继续检查队列，因此 5/10/15 级会接着显示技能选择。
    /// </summary>
    private void OnAttributeOptionSelected(PlayerAttributeType attributeType)
    {
        bool success = this.SendCommand(new ResolvePlayerUpgradeCommand(attributeType));
        if (!success)
        {
            Debug.LogWarning("属性强化选择失败，可能没有待处理的属性选择次数。", this);
            return;
        }

        RefreshPanelState();
    }

    /// <summary>
    /// 玩家选择技能学习或升级后调用。
    /// </summary>
    private void OnSkillOptionSelected(PlayerSkillChoice choice)
    {
        bool success = this.SendCommand(new ResolvePlayerSkillChoiceCommand(choice));
        if (!success)
        {
            Debug.LogWarning("技能选择失败，可能是职业不符合、技能满级或没有待处理的技能选择次数。", this);
            return;
        }

        RefreshPanelState();
    }

    /// <summary>
    /// 关闭升级奖励面板，并恢复正常游戏状态。
    /// </summary>
    private void HidePanel()
    {
        currentMode = UpgradeRewardMode.None;

        if (Application.isPlaying && TreasureHunterArchitecture.Interface.GetModel<PlayerModel>() != null)
        {
            this.SendCommand(new SetPlayerUpgradeSelectionStateCommand(false));
        }

        SetVisible(false);
        RestoreGameplayStateIfNeeded();
    }

    /// <summary>
    /// 控制遮罩与面板整体显隐。
    /// </summary>
    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(visible);
            if (visible)
            {
                overlayRoot.transform.SetAsLastSibling();
            }
        }

        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// 打开奖励选择时暂停游戏，并切换到 UI 操作模式。
    /// 属性选择和技能选择复用同一套暂停逻辑。
    /// </summary>
    private void CaptureGameplayStateIfNeeded()
    {
        if (currentMode == UpgradeRewardMode.None)
        {
            return;
        }

        if (!hasCapturedGameplayState)
        {
            cachedTimeScale = Time.timeScale;
            cachedCursorLockMode = Cursor.lockState;
            cachedCursorVisible = Cursor.visible;
            hasCapturedGameplayState = true;
        }

        Time.timeScale = 0f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
    }

    /// <summary>
    /// 所有待选择奖励处理完后，恢复进入面板前的游戏状态。
    /// </summary>
    private void RestoreGameplayStateIfNeeded()
    {
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        hasCapturedGameplayState = false;
    }
}
