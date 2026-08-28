using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单条任务卡片：只把 QuestSnapshot 转成文字、进度条和按钮状态，不执行任务业务规则。
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestListItemView : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Image objectiveIcon;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text progressText;
    [SerializeField] private Image progressFill;
    [SerializeField] private Text rewardText;
    [SerializeField] private Button actionButton;
    [SerializeField] private Text actionButtonText;

    private string questId;
    private Action<string> actionCallback;

    public void Bind(QuestSnapshot snapshot, Action<string> onAction)
    {
        questId = snapshot.IsValid ? snapshot.Definition.QuestId : string.Empty;
        actionCallback = onAction;

        if (!snapshot.IsValid)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        titleText.text = snapshot.Definition.DisplayName;
        objectiveIcon.color = snapshot.Definition.TargetMonster == MonsterKind.RedSlime
            ? new Color32(255, 105, 105, 255)
            : new Color32(105, 235, 125, 255);
        descriptionText.text = snapshot.Definition.Description;
        progressText.text = $"{snapshot.CurrentCount}/{snapshot.Definition.RequiredCount}";
        progressFill.fillAmount = (float)snapshot.CurrentCount / snapshot.Definition.RequiredCount;
        rewardText.text = snapshot.Definition.GoldReward.ToString("N0");

        actionButton.onClick.RemoveListener(HandleActionClicked);
        actionButton.onClick.AddListener(HandleActionClicked);
        switch (snapshot.State)
        {
            case QuestState.Available:
                actionButton.interactable = true;
                actionButtonText.text = "接取任务";
                break;
            case QuestState.Active:
                actionButton.interactable = false;
                actionButtonText.text = "进行中";
                break;
            case QuestState.ReadyToClaim:
                actionButton.interactable = true;
                actionButtonText.text = "领取奖励";
                break;
            default:
                actionButton.interactable = false;
                actionButtonText.text = "已领取";
                break;
        }
    }

    private void OnDestroy()
    {
        if (actionButton != null)
        {
            actionButton.onClick.RemoveListener(HandleActionClicked);
        }
    }

    public bool ValidateReferences(bool logError)
    {
        string missing = null;
        if (titleText == null) missing = nameof(titleText);
        else if (objectiveIcon == null) missing = nameof(objectiveIcon);
        else if (descriptionText == null) missing = nameof(descriptionText);
        else if (progressText == null) missing = nameof(progressText);
        else if (progressFill == null) missing = nameof(progressFill);
        else if (rewardText == null) missing = nameof(rewardText);
        else if (actionButton == null) missing = nameof(actionButton);
        else if (actionButtonText == null) missing = nameof(actionButtonText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"QuestListItemView 的 Prefab 引用未配置：{missing}。", this);
        }
        return false;
    }

    private void HandleActionClicked()
    {
        if (!string.IsNullOrWhiteSpace(questId))
        {
            actionCallback?.Invoke(questId);
        }
    }
}
