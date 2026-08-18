using System.Collections.Generic;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家属性面板：通过 Query 读取只读属性，并写入 Prefab 中预先配置好的属性行。
/// 运行时不会创建、删除或查找 UI 节点；新增属性时必须在 Prefab 中显式增加对应 key 的行。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerAttributePanel : MonoBehaviour, IController
{
    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameSessionUi sessionUi;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text summaryText;
    [SerializeField] private PlayerAttributeRowView[] rowViews;

    [Header("Interaction")]
    [SerializeField] private bool showOnStart;
    [SerializeField] private bool editorPreviewVisible = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    [Header("Value Highlight")]
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color rowHighlightColor = new Color(1f, 0.65f, 0.65f, 1f);
    [SerializeField] private Color valueColor = new Color(0.96f, 0.98f, 1f, 1f);
    [SerializeField] private Color valueHighlightColor = new Color(0.44f, 1f, 0.79f, 1f);
    [SerializeField] private float valueHighlightDuration = 0.9f;

    private readonly List<PlayerAttributeEntry> entryBuffer = new List<PlayerAttributeEntry>(16);
    private readonly Dictionary<string, PlayerAttributeRowView> rowsByKey =
        new Dictionary<string, PlayerAttributeRowView>(16);
    private bool isVisible;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 构建属性 key 到静态 RowView 的索引表。
    /// 后续刷新面板时，就能按 key 直接定位到预先放好的那一行。
    /// </summary>
    private void Awake()
    {
        BuildRowIndex();
    }

    /// <summary>
    /// 运行时订阅玩家属性变化事件；编辑器下只刷新预览。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            this.RegisterEvent<PlayerStatsChangedEvent>(HandleStatsChanged);
            return;
        }

        ApplyEditorPreviewIfReady();
    }

    /// <summary>
    /// 停用时解除事件订阅。
    /// </summary>
    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            this.UnRegisterEvent<PlayerStatsChangedEvent>(HandleStatsChanged);
        }
    }

    /// <summary>
    /// 编辑器里改引用或属性行数组时，立刻重建索引并刷新预览。
    /// </summary>
    private void OnValidate()
    {
        BuildRowIndex();
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
        }
    }

    /// <summary>
    /// Start 时做运行时校验、首帧刷新和默认显隐控制。
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

        BuildRowIndex();
        RefreshView();
        SetVisible(showOnStart);
    }

    /// <summary>
    /// 处理面板开关和高亮动画推进。
    /// 只有面板可见时，才需要逐行推进高亮淡出。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(toggleKey) && CanTogglePanel())
        {
            SetVisible(!isVisible);
        }

        if (isVisible)
        {
            for (int i = 0; i < rowViews.Length; i++)
            {
                rowViews[i].TickHighlight(
                    Time.unscaledDeltaTime,
                    rowColor,
                    valueColor,
                    rowHighlightColor,
                    valueHighlightColor);
            }
        }
    }

    /// <summary>
    /// 检查面板与全部属性行引用，并验证 key 唯一，防止错误 Prefab 在运行时静默丢失属性。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (sessionUi == null) missing = nameof(sessionUi);
        else if (panelRoot == null) missing = nameof(panelRoot);
        else if (contentRoot == null) missing = nameof(contentRoot);
        else if (titleText == null) missing = nameof(titleText);
        else if (summaryText == null) missing = nameof(summaryText);
        else if (rowViews == null || rowViews.Length == 0) missing = nameof(rowViews);

        if (missing == null)
        {
            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < rowViews.Length; i++)
            {
                PlayerAttributeRowView row = rowViews[i];
                if (row == null)
                {
                    missing = $"{nameof(rowViews)}[{i}]";
                    break;
                }

                if (!row.ValidatePrefabReferences(false))
                {
                    missing = $"{nameof(rowViews)}[{i}] 的内部引用";
                    break;
                }

                if (!keys.Add(row.Key))
                {
                    missing = $"重复属性 key：{row.Key}";
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
            Debug.LogError($"PlayerAttributePanel 的 Prefab 引用未配置：{missing}。请修复 GameplayUiRoot.prefab。", this);
        }

        return false;
    }

    private void BuildRowIndex()
    {
        rowsByKey.Clear();
        if (rowViews == null)
        {
            return;
        }

        for (int i = 0; i < rowViews.Length; i++)
        {
            PlayerAttributeRowView row = rowViews[i];
            if (row != null && !string.IsNullOrWhiteSpace(row.Key) && !rowsByKey.ContainsKey(row.Key))
            {
                rowsByKey.Add(row.Key, row);
            }
        }
    }

    /// <summary>
    /// 控制整个属性面板显隐。
    /// 打开时会立即刷新一次，避免显示旧数据。
    /// </summary>
    private void SetVisible(bool visible)
    {
        isVisible = visible;
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }

        if (visible && Application.isPlaying)
        {
            RefreshView();
        }
    }

    /// <summary>
    /// 判断当前是否允许切换属性面板。
    /// 升级三选一和会话暂停层打开时，属性面板不应抢输入焦点。
    /// </summary>
    private bool CanTogglePanel()
    {
        if (this.GetModel<PlayerModel>().Stats.IsUpgradeSelectionActive)
        {
            return false;
        }

        return sessionUi == null || !sessionUi.IsGameplayInputBlocked;
    }

    /// <summary>
    /// 玩家属性变化后刷新面板。
    /// </summary>
    private void HandleStatsChanged(PlayerStatsChangedEvent _)
    {
        RefreshView();
    }

    /// <summary>
    /// 从 Query 读取当前玩家属性列表，并把结果写入固定的静态行视图中。
    /// 这里不会创建新节点，所以缺哪一行就会直接报错提醒修 Prefab。
    /// </summary>
    private void RefreshView()
    {
        if (!ValidatePrefabReferences(true))
        {
            return;
        }

        BuildRowIndex();
        for (int i = 0; i < rowViews.Length; i++)
        {
            rowViews[i].SetVisible(false);
        }

        entryBuffer.Clear();
        entryBuffer.AddRange(this.SendQuery(new GetPlayerAttributeEntriesQuery()));
        for (int i = 0; i < entryBuffer.Count; i++)
        {
            PlayerAttributeEntry entry = entryBuffer[i];
            if (!rowsByKey.TryGetValue(entry.Key, out PlayerAttributeRowView row))
            {
                Debug.LogError($"GameplayUiRoot.prefab 缺少属性行：{entry.Key}。", this);
                continue;
            }

            row.SetContent(
                entry,
                rowColor,
                valueColor,
                rowHighlightColor,
                valueHighlightColor,
                valueHighlightDuration);
        }

        UpdateHeaderText();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    }

    /// <summary>
    /// 刷新面板顶部的等级、生命、魔法和经验摘要。
    /// </summary>
    private void UpdateHeaderText()
    {
        titleText.text = "角色属性";
        IPlayerStatsReadOnly stats = this.GetModel<PlayerModel>().Stats;
        summaryText.text = $"Lv.{stats.Level}    HP {stats.CurrentHp}/{stats.MaxHp}    MP {stats.CurrentMp}/{stats.MaxMp}    EXP {stats.CurrentExp}/{stats.ExpToNextLevel}";
    }

    /// <summary>
    /// 编辑器预览模式下填充一份示例内容。
    /// </summary>
    private void ApplyEditorPreviewIfReady()
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        BuildRowIndex();
        titleText.text = "角色属性";
        summaryText.text = "Lv.1    HP 150/150    MP 120/120    EXP 0/50";
        panelRoot.SetActive(editorPreviewVisible);
    }
}
