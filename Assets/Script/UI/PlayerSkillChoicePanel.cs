using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家技能三选一面板：负责显示可学习/可升级技能，并把玩家选择交给 PlayerSkillSystem 处理。
/// 注意：UI 只负责显示和按钮点击，不直接修改玩家技能数据。
/// </summary>
[DisallowMultipleComponent]
public class PlayerSkillChoicePanel : MonoBehaviour, IController
{
    private const int ChoiceCount = 3;

    [Header("UI References")]
    [SerializeField] private GameObject overlayRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text queueText;
    [SerializeField] private Button[] optionButtons = new Button[ChoiceCount];
    [SerializeField] private Text[] optionTexts = new Text[ChoiceCount];

    private readonly List<PlayerSkillChoice> currentChoices = new List<PlayerSkillChoice>();
    private bool hasCapturedTimeScale;
    private float cachedTimeScale = 1f;

    public IArchitecture GetArchitecture()
    {
        return TreasureHunterArchitecture.Interface;
    }

    /// <summary>
    /// 面板启用时监听技能选择次数变化。
    /// 当 PlayerSkillSystem 发出事件时，这里决定打开或关闭面板。
    /// </summary>
    private void OnEnable()
    {
        this.RegisterEvent<PlayerSkillSelectionQueueChangedEvent>(HandleSkillSelectionQueueChanged);
        HidePanel();
    }

    /// <summary>
    /// 面板关闭或销毁时取消监听，避免 UI 对象失效后还收到事件。
    /// </summary>
    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerSkillSelectionQueueChangedEvent>(HandleSkillSelectionQueueChanged);
        RestoreTimeScaleIfNeeded();
    }

    /// <summary>
    /// 技能选择次数发生变化时调用。
    /// count > 0 表示还有选择没处理，需要打开面板。
    /// </summary>
    private void HandleSkillSelectionQueueChanged(PlayerSkillSelectionQueueChangedEvent e)
    {
        if (e.Count > 0)
        {
            ShowNextSelection();
        }
        else
        {
            HidePanel();
        }
    }

    /// <summary>
    /// 打开下一轮技能三选一。
    /// UI 通过 Query 向技能系统要候选项，而不是自己遍历技能配置表。
    /// </summary>
    private void ShowNextSelection()
    {
        currentChoices.Clear();
        currentChoices.AddRange(this.SendQuery(new GetPlayerSkillChoicesQuery(ChoiceCount)));

        if (currentChoices.Count <= 0)
        {
            HidePanel();
            return;
        }

        CaptureTimeScaleIfNeeded();

        if (overlayRoot != null)
        {
            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();
        }

        if (titleText != null)
        {
            titleText.text = "选择技能";
        }

        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();
        if (queueText != null)
        {
            queueText.text = $"待选择：{skillModel.PendingSkillSelectionCount}";
        }

        for (int i = 0; i < ChoiceCount; i++)
        {
            bool hasChoice = i < currentChoices.Count;

            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(hasChoice);
                optionButtons[i].onClick.RemoveAllListeners();
            }

            if (!hasChoice)
            {
                continue;
            }

            PlayerSkillChoice choice = currentChoices[i];

            if (optionTexts[i] != null)
            {
                optionTexts[i].text = this.SendQuery(new GetPlayerSkillChoiceTextQuery(choice));
            }

            if (optionButtons[i] != null)
            {
                // 注意：这里用局部变量 choice，避免循环变量捕获导致所有按钮都选最后一个。
                optionButtons[i].onClick.AddListener(() => OnChoiceClicked(choice));
            }
        }
    }

    /// <summary>
    /// 玩家点击某个技能选项。
    /// UI 不直接 LearnSkill / UpgradeSkill，而是发送 Command 给技能系统处理。
    /// </summary>
    private void OnChoiceClicked(PlayerSkillChoice choice)
    {
        bool success = this.SendCommand(new ResolvePlayerSkillChoiceCommand(choice));

        if (!success)
        {
            Debug.LogWarning("技能选择失败，可能是技能已满级、职业不符合或没有待选择次数。", this);
            return;
        }

        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();
        if (skillModel.PendingSkillSelectionCount > 0)
        {
            ShowNextSelection();
        }
        else
        {
            HidePanel();
        }
    }

    /// <summary>
    /// 隐藏面板。
    /// </summary>
    private void HidePanel()
    {
        if (overlayRoot != null)
        {
            overlayRoot.SetActive(false);
        }

        RestoreTimeScaleIfNeeded();
    }

    /// <summary>
    /// 打开技能选择时暂停游戏。
    /// 技能选择属于成长决策，不希望玩家一边被怪物打，一边点 UI。
    /// </summary>
    private void CaptureTimeScaleIfNeeded()
    {
        if (!hasCapturedTimeScale)
        {
            cachedTimeScale = Time.timeScale;
            hasCapturedTimeScale = true;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>
    /// 关闭面板时恢复游戏时间。
    /// </summary>
    private void RestoreTimeScaleIfNeeded()
    {
        if (!hasCapturedTimeScale)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        hasCapturedTimeScale = false;
    }
}