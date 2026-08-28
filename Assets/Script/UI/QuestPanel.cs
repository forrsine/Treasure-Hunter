using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mushroom 任务面板：负责交互提示、卡片生成、显隐和玩家输入状态。
/// 业务结果来自 QuestSystem；该面板不直接写任务进度或金币。
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestPanel : MonoBehaviour, IController
{
    [Header("Shared References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameSessionUi sessionUi;
    [SerializeField] private InventoryPanel inventoryPanel;
    [SerializeField] private MerchantShopPanel merchantShopPanel;

    [Header("Quest UI")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Text promptText;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private QuestListItemView itemPrefab;
    [SerializeField] private Text feedbackText;

    private readonly Dictionary<string, QuestListItemView> itemViews =
        new Dictionary<string, QuestListItemView>(System.StringComparer.Ordinal);
    private bool isNpcNearby;
    private bool isOpen;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;

    public bool IsOpen => isOpen;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // 运行时默认关闭任务模态，避免编辑态预览留下的全屏 Graphic 遮住商店。
        if (promptRoot != null) promptRoot.SetActive(false);
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.RegisterEvent<QuestNpcProximityChangedEvent>(HandleNpcProximityChanged);
        this.RegisterEvent<QuestPanelOpenRequestedEvent>(HandleOpenRequested);
        this.RegisterEvent<QuestAcceptedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestProgressChangedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestRewardClaimedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestProgressRestoredEvent>(HandleQuestRestored);
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<QuestNpcProximityChangedEvent>(HandleNpcProximityChanged);
        this.UnRegisterEvent<QuestPanelOpenRequestedEvent>(HandleOpenRequested);
        this.UnRegisterEvent<QuestAcceptedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestProgressChangedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestRewardClaimedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestProgressRestoredEvent>(HandleQuestRestored);
        closeButton?.onClick.RemoveListener(Close);
        CloseImmediateAndRestoreState();
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        promptText.text = "按 E 查看蘑菇委托";
        panelRoot.SetActive(false);
        promptRoot.SetActive(false);
        feedbackText.text = "选择一项委托开始冒险。";
        EnsureItemViews();
        RefreshList();
    }

    public bool TryClose()
    {
        if (!isOpen)
        {
            return false;
        }

        Close();
        return true;
    }

    public void RefreshPromptVisibility()
    {
        if (promptRoot == null)
        {
            return;
        }

        bool blocked = sessionUi != null && sessionUi.HasBlockingOverlayExcludingQuest;
        promptRoot.SetActive(isNpcNearby && !isOpen && !blocked);
    }

    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (sessionUi == null) missing = nameof(sessionUi);
        else if (inventoryPanel == null) missing = nameof(inventoryPanel);
        else if (merchantShopPanel == null) missing = nameof(merchantShopPanel);
        else if (promptRoot == null) missing = nameof(promptRoot);
        else if (promptText == null) missing = nameof(promptText);
        else if (panelRoot == null) missing = nameof(panelRoot);
        else if (closeButton == null) missing = nameof(closeButton);
        else if (contentRoot == null) missing = nameof(contentRoot);
        else if (itemPrefab == null || !itemPrefab.ValidateReferences(false)) missing = nameof(itemPrefab);
        else if (feedbackText == null) missing = nameof(feedbackText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"QuestPanel 的 Prefab 引用未配置：{missing}。", this);
        }
        return false;
    }

    private void HandleNpcProximityChanged(QuestNpcProximityChangedEvent evt)
    {
        isNpcNearby = evt.IsNearby;
        RefreshPromptVisibility();
    }

    private void HandleOpenRequested(QuestPanelOpenRequestedEvent _)
    {
        if (!isNpcNearby || (sessionUi != null && sessionUi.HasBlockingOverlayExcludingQuest))
        {
            return;
        }

        Open();
    }

    private void Open()
    {
        if (isOpen)
        {
            return;
        }

        CaptureGameplayState();
        isOpen = true;
        promptRoot.SetActive(false);
        BringQuestFeatureToFront();
        panelRoot.SetActive(true);
        panelRoot.transform.SetAsLastSibling();
        feedbackText.text = "完成委托后返回 Mushroom 领取金币。";
        EnsureItemViews();
        RefreshList();
        sessionUi?.NotifyModalStateChanged();
    }

    /// <summary>
    /// 模态窗口的显示顺序由功能根节点决定，而不是内部 Panel 的兄弟顺序。
    /// 最后打开任务面板时提升 QuestFeature，关闭后商店仍可重新提升自己的 Feature。
    /// </summary>
    private void BringQuestFeatureToFront()
    {
        Transform featureRoot = panelRoot != null ? panelRoot.transform.parent : null;
        featureRoot?.SetAsLastSibling();
    }

    private void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        panelRoot.SetActive(false);
        RestoreGameplayState();
        RefreshPromptVisibility();
        sessionUi?.NotifyModalStateChanged();
    }

    private void HandleQuestChanged(QuestAcceptedEvent evt)
    {
        feedbackText.text = $"已接取：{evt.Snapshot.Definition.DisplayName}";
        RefreshList();
    }

    private void HandleQuestChanged(QuestProgressChangedEvent _)
    {
        if (isOpen) RefreshList();
    }

    private void HandleQuestChanged(QuestRewardClaimedEvent evt)
    {
        feedbackText.text = $"已领取 {evt.GoldReward:N0} 金币。";
        RefreshList();
    }

    private void HandleQuestRestored(QuestProgressRestoredEvent _)
    {
        if (isOpen) RefreshList();
    }

    private void EnsureItemViews()
    {
        IReadOnlyList<QuestSnapshot> snapshots = this.SendQuery(new GetQuestSnapshotsQuery());
        for (int i = 0; i < snapshots.Count; i++)
        {
            QuestSnapshot snapshot = snapshots[i];
            if (!snapshot.IsValid || itemViews.ContainsKey(snapshot.Definition.QuestId))
            {
                continue;
            }

            QuestListItemView view = Instantiate(itemPrefab, contentRoot);
            view.name = $"Quest_{snapshot.Definition.QuestId}";
            view.gameObject.SetActive(true);
            itemViews.Add(snapshot.Definition.QuestId, view);
        }
    }

    private void RefreshList()
    {
        IReadOnlyList<QuestSnapshot> snapshots = this.SendQuery(new GetQuestSnapshotsQuery());
        for (int i = 0; i < snapshots.Count; i++)
        {
            QuestSnapshot snapshot = snapshots[i];
            if (snapshot.IsValid && itemViews.TryGetValue(snapshot.Definition.QuestId, out QuestListItemView view))
            {
                view.Bind(snapshot, HandleQuestAction);
            }
        }
    }

    private void HandleQuestAction(string questId)
    {
        IReadOnlyList<QuestSnapshot> snapshots = this.SendQuery(new GetQuestSnapshotsQuery());
        QuestSnapshot current = default;
        for (int i = 0; i < snapshots.Count; i++)
        {
            if (snapshots[i].IsValid && snapshots[i].Definition.QuestId == questId)
            {
                current = snapshots[i];
                break;
            }
        }

        QuestActionResult result = current.State == QuestState.Available
            ? this.SendCommand(new AcceptQuestCommand(questId))
            : this.SendCommand(new ClaimQuestRewardCommand(questId));
        if (!result.Success)
        {
            feedbackText.text = GetFailureMessage(result.Failure);
        }
    }

    private void CaptureGameplayState()
    {
        if (!hasCapturedGameplayState)
        {
            hasCapturedGameplayState = true;
            cachedTimeScale = Time.timeScale;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreGameplayState()
    {
        if (!hasCapturedGameplayState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        if (sessionUi != null)
        {
            sessionUi.RequestGameplayCursorRestore();
        }
        else
        {
            UiCursorStateUtility.EnsureHiddenAndLocked();
        }
        hasCapturedGameplayState = false;
    }

    private void CloseImmediateAndRestoreState()
    {
        isOpen = false;
        if (panelRoot != null) panelRoot.SetActive(false);
        RestoreGameplayState();
    }

    private static string GetFailureMessage(QuestActionFailure failure)
    {
        switch (failure)
        {
            case QuestActionFailure.GoldLimitExceeded: return "金币接近上限，无法领取完整奖励。";
            case QuestActionFailure.InvalidState: return "当前任务状态不能执行该操作。";
            case QuestActionFailure.UnknownQuest: return "任务配置不存在。";
            default: return "任务操作失败，请重试。";
        }
    }
}
