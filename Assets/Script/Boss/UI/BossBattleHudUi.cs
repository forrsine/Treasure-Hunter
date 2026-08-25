using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 战叠加 UI：只负责 Boss 血条和阶段提示。
/// 玩家血量、蓝量、体力、技能栏、暂停面板统一复用主场景 GameplayUiRoot，避免维护两套相似玩家 HUD。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBattleHudUi : MonoBehaviour, IController
{
    // GameplayUiRoot 的 Canvas 排序为 5000，Boss 战关键信息需要稳定显示在它的上层。
    private const int BossHudSortingOrder = 6000;

    [Header("Runtime References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private RectTransform hudRoot;
    [SerializeField] private SpiderKingBossController boss;

    private Text bossNameText;
    private Text bossHpText;
    private Text bossPhaseText;
    private Image bossHpFill;
    private Text hintText;
    private GameObject victoryPanel;
    private Text victoryText;
    private Button returnButton;
    private Font defaultFont;
    private bool bossEventsRegistered;
    private int lastDisplayedBossHp = int.MinValue;
    private int lastDisplayedBossMaxHp = int.MinValue;
    private bool lastDisplayedBossDead;
    private bool lastDisplayedBossDeathFinished;
    private bool hasCapturedVictoryPauseState;
    private float cachedTimeScale = 1f;
    private CursorLockMode cachedCursorLockMode;
    private bool cachedCursorVisible;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        BuildUiIfNeeded();
    }

    private void OnEnable()
    {
        BuildUiIfNeeded();
        RegisterBossEventsIfNeeded();
        TryAutoBindBoss();
        RefreshBoss();
    }

    private void Start()
    {
        TryAutoBindBoss();
        RefreshBoss();
    }

    private void Update()
    {
        // Boss 可能由 Bootstrap 稍后创建；未绑定时才做轻量查找，避免每帧无意义搜索。
        if (boss == null)
        {
            TryAutoBindBoss();
            return;
        }

        RefreshBossIfStatsChanged();
    }

    private void OnDisable()
    {
        UnregisterBossEventsIfNeeded();
        ResumeGameAfterVictoryPanel();
    }

    /// <summary>
    /// 外部生成 Boss 后主动绑定，UI 只监听 Boss 状态事件，不直接管理 Boss 战斗逻辑。
    /// </summary>
    public void BindBoss(SpiderKingBossController newBoss)
    {
        if (boss == newBoss)
        {
            RegisterBossEventsIfNeeded();
            RefreshBoss();
            return;
        }

        UnregisterBossEventsIfNeeded();
        boss = newBoss;
        ResetBossDisplayCache();
        RegisterBossEventsIfNeeded();
        RefreshBoss();
    }

    private void BuildUiIfNeeded()
    {
        if (hudRoot != null && bossHpFill != null && victoryPanel != null)
        {
            return;
        }

        EnsureCanvas();

        GameObject rootObject = new GameObject("BossBattleHudRoot", typeof(RectTransform));
        rootObject.transform.SetParent(targetCanvas.transform, false);
        hudRoot = rootObject.GetComponent<RectTransform>();
        StretchToParent(hudRoot);

        CreateBossPanel();
        CreateHintText();
        CreateVictoryPanel();
    }

    private void EnsureCanvas()
    {
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
        }

        if (targetCanvas == null)
        {
            targetCanvas = gameObject.AddComponent<Canvas>();
        }

        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.sortingOrder = BossHudSortingOrder;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
        }

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }
    }

    private void CreateBossPanel()
    {
        RectTransform panel = CreatePanel(
            "BossPanel",
            hudRoot,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(760f, 112f),
            new Vector2(0f, -20f),
            new Color(0.03f, 0.02f, 0.04f, 0.72f));

        bossNameText = CreateText(
            "BossNameText",
            panel,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(700f, 30f),
            new Vector2(0f, -10f),
            "Spider King",
            24,
            Color.white,
            TextAnchor.MiddleCenter);

        bossPhaseText = CreateText(
            "BossPhaseText",
            panel,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(700f, 24f),
            new Vector2(0f, -42f),
            "等待 Boss 初始化",
            18,
            new Color(1f, 0.78f, 0.35f, 1f),
            TextAnchor.MiddleCenter);

        bossHpFill = CreateBar(
            "BossHpBar",
            panel,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(650f, 24f),
            new Vector2(0f, 22f),
            new Color(0.16f, 0.02f, 0.03f, 0.95f),
            new Color(0.82f, 0.08f, 0.12f, 1f));

        bossHpText = CreateText(
            "BossHpText",
            panel,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(650f, 24f),
            new Vector2(0f, 22f),
            "-- / --",
            17,
            Color.white,
            TextAnchor.MiddleCenter);
    }

    private void CreateHintText()
    {
        hintText = CreateText(
            "BossHintText",
            hudRoot,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(900f, 40f),
            new Vector2(0f, 34f),
            "击败 Spider King：近身会触发咬/爪击，中距离会释放范围法术",
            20,
            new Color(0.95f, 0.9f, 0.72f, 1f),
            TextAnchor.MiddleCenter);
    }

    private void CreateVictoryPanel()
    {
        RectTransform panel = CreatePanel(
            "VictoryPanel",
            hudRoot,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(520f, 260f),
            Vector2.zero,
            new Color(0.01f, 0.01f, 0.015f, 0.88f));
        victoryPanel = panel.gameObject;

        victoryText = CreateText(
            "VictoryText",
            panel,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(460f, 110f),
            new Vector2(0f, -32f),
            "Boss 已击败！\n你可以把这段作为项目 Boss 战闭环展示。",
            26,
            new Color(1f, 0.86f, 0.35f, 1f),
            TextAnchor.MiddleCenter);

        returnButton = CreateButton(
            "ReturnButton",
            panel,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(240f, 54f),
            new Vector2(0f, 42f),
            "关闭弹窗");
        returnButton.onClick.AddListener(CloseVictoryPanel);

        victoryPanel.SetActive(false);
    }

    private RectTransform CreatePanel(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Color color)
    {
        GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        ApplyRect(rect, anchorMin, anchorMax, pivot, size, position);

        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        return rect;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        string content,
        int fontSize,
        Color color,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        ApplyRect(rect, anchorMin, anchorMax, pivot, size, position);

        Text text = textObject.GetComponent<Text>();
        text.font = GetDefaultFont();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Image CreateBar(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        Color backgroundColor,
        Color fillColor)
    {
        RectTransform background = CreatePanel(
            objectName,
            parent,
            anchorMin,
            anchorMax,
            pivot,
            size,
            position,
            backgroundColor);

        GameObject fillObject = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(background, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        StretchToParent(fillRect, 3f);

        Image fill = fillObject.GetComponent<Image>();
        fill.color = fillColor;
        fill.type = Image.Type.Simple;
        fill.fillAmount = 1f;
        fill.raycastTarget = false;
        return fill;
    }

    private Button CreateButton(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position,
        string label)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        ApplyRect(rect, anchorMin, anchorMax, pivot, size, position);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.85f, 0.54f, 0.18f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.72f, 0.28f, 1f);
        colors.pressedColor = new Color(0.62f, 0.34f, 0.08f, 1f);
        button.colors = colors;

        CreateText(
            "Label",
            rect,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero,
            label,
            22,
            Color.white,
            TextAnchor.MiddleCenter);

        return button;
    }

    private void ApplyRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 size,
        Vector2 position)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private void StretchToParent(RectTransform rect, float padding = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    private Font GetDefaultFont()
    {
        if (defaultFont == null)
        {
            defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return defaultFont;
    }

    private void TryAutoBindBoss()
    {
        if (boss != null)
        {
            return;
        }

        SpiderKingBossController foundBoss = FindObjectOfType<SpiderKingBossController>();
        if (foundBoss != null)
        {
            BindBoss(foundBoss);
        }
    }

    private void RegisterBossEventsIfNeeded()
    {
        if (boss == null || bossEventsRegistered)
        {
            return;
        }

        boss.BossStatsChanged += HandleBossStatsChanged;
        boss.BossDied += HandleBossDied;
        bossEventsRegistered = true;
    }

    private void UnregisterBossEventsIfNeeded()
    {
        if (boss == null || !bossEventsRegistered)
        {
            bossEventsRegistered = false;
            return;
        }

        boss.BossStatsChanged -= HandleBossStatsChanged;
        boss.BossDied -= HandleBossDied;
        bossEventsRegistered = false;
    }

    private void HandleBossStatsChanged(SpiderKingBossController _)
    {
        RefreshBoss();
    }

    private void HandleBossDied(SpiderKingBossController _)
    {
        RefreshBoss();
    }

    /// <summary>
    /// 事件驱动是主要刷新方式，这里只做轻量兜底。
    /// 如果 UI 绑定时机错过了某次扣血事件，也能在下一帧根据 Boss 当前血量补刷新。
    /// </summary>
    private void RefreshBossIfStatsChanged()
    {
        if (boss == null)
        {
            return;
        }

        if (lastDisplayedBossHp == boss.CurrentHp &&
            lastDisplayedBossMaxHp == boss.MaxHp &&
            lastDisplayedBossDead == boss.IsDead &&
            lastDisplayedBossDeathFinished == boss.IsDeathSequenceFinished)
        {
            return;
        }

        RefreshBoss();
    }

    private void RefreshBoss()
    {
        if (bossNameText == null || bossHpText == null || bossPhaseText == null || bossHpFill == null)
        {
            return;
        }

        if (boss == null)
        {
            bossNameText.text = "Spider King";
            bossPhaseText.text = "等待 Boss 初始化";
            bossHpText.text = "-- / --";
            SetFill(bossHpFill, 0f);
            SetVictoryVisible(false);
            ResetBossDisplayCache();
            return;
        }

        bossNameText.text = boss.BossName;
        bossPhaseText.text = boss.CurrentPhaseName;
        bossHpText.text = $"{boss.CurrentHp}/{boss.MaxHp}";
        bossHpFill.color = boss.IsDead
            ? new Color(0.38f, 0.38f, 0.38f, 1f)
            : boss.HpPercent <= 0.35f
                ? new Color(1f, 0.23f, 0.05f, 1f)
                : new Color(0.82f, 0.08f, 0.12f, 1f);
        SetFill(bossHpFill, boss.HpPercent);
        SetVictoryVisible(ShouldShowVictoryPanel());

        if (hintText != null)
        {
            hintText.text = boss.IsDeathSequenceFinished
                ? "Boss 已击败，拾取发光战利品后走进粉色传送门返回主场景"
                : boss.IsDead
                    ? "Boss 正在倒下，准备开启返回传送门"
                : "击败 Spider King：近身会触发咬/爪击，中距离会释放范围法术";
        }

        lastDisplayedBossHp = boss.CurrentHp;
        lastDisplayedBossMaxHp = boss.MaxHp;
        lastDisplayedBossDead = boss.IsDead;
        lastDisplayedBossDeathFinished = boss.IsDeathSequenceFinished;
    }

    private void SetFill(Image fill, float percent)
    {
        if (fill == null)
        {
            return;
        }

        float clampedPercent = Mathf.Clamp01(percent);
        fill.fillAmount = clampedPercent;
        fill.enabled = clampedPercent > 0.001f;

        RectTransform fillRect = fill.rectTransform;
        if (fillRect == null)
        {
            return;
        }

        // 运行时代码创建的纯色 Image 不一定适合依赖 Filled 模式；
        // 直接缩放 RectTransform 更稳定，也更符合“UI 表现层读取血量百分比”的职责。
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.localScale = new Vector3(clampedPercent, 1f, 1f);
    }

    private void ResetBossDisplayCache()
    {
        lastDisplayedBossHp = int.MinValue;
        lastDisplayedBossMaxHp = int.MinValue;
        lastDisplayedBossDead = false;
        lastDisplayedBossDeathFinished = false;
    }

    private void SetVictoryVisible(bool visible)
    {
        // 当前版本 Boss 胜利后不再弹出结算窗口，避免打断玩家拾取掉落物和走传送门。
        // 保留方法是为了少改动 HUD 刷新流程，后续如果要做结算面板可以在这里重新接回。
        if (victoryPanel != null && victoryPanel.activeSelf)
        {
            victoryPanel.SetActive(false);
        }
    }

    private bool ShouldShowVictoryPanel()
    {
        return false;
    }

    /// <summary>
    /// 胜利弹窗出现时暂停游戏并呼出鼠标。
    /// 这里和普通暂停菜单一样缓存旧状态，关闭弹窗后可以恢复玩家操作。
    /// </summary>
    private void PauseGameForVictoryPanel()
    {
        if (!hasCapturedVictoryPauseState)
        {
            cachedTimeScale = Time.timeScale;
            cachedCursorLockMode = Cursor.lockState;
            cachedCursorVisible = Cursor.visible;
            hasCapturedVictoryPauseState = true;
        }

        Time.timeScale = 0f;
        CursorPopupUtility.ShowAtUpperCenterQuarter();
    }

    private void ResumeGameAfterVictoryPanel()
    {
        if (!hasCapturedVictoryPauseState)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        hasCapturedVictoryPauseState = false;
    }

    /// <summary>
    /// 胜利弹窗按钮只关闭弹窗，不再直接切回主场景。
    /// 返回主场景交给 Boss 死亡后生成的粉色传送门处理，让玩家主动走门完成闭环。
    /// </summary>
    private void CloseVictoryPanel()
    {
        SetVictoryVisible(false);
        ResumeGameAfterVictoryPanel();
    }
}
