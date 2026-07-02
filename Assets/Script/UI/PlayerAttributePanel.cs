using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 角色属性面板。
/// 
/// 新手阅读顺序：
/// 1. 按 Tab 时 Update 会切换面板显示/隐藏。
/// 2. PlayerCo.StatsChanged 事件触发时，RefreshView 会重新读取玩家属性。
/// 3. 面板、分组、行都是代码自动生成的 UI，不需要你手动摆完整结构。
/// 4. 数值变化时会短暂高亮，帮助玩家看到刚刚提升了哪个属性。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerAttributePanel : MonoBehaviour
{
    /// <summary>
    /// 一个属性分组的 UI 引用。
    /// 例如“概览”“战斗”“生存”就是三个 SectionView。
    /// </summary>
    private class SectionView
    {
        // 分组名，例如“概览”“战斗”“生存”。
        public string Name;
        // 分组最外层物体，用于整体显示/隐藏。
        public GameObject Root;
        // 分组里真正承载属性行的 RectTransform。
        public RectTransform RowsRoot;
    }

    /// <summary>
    /// 一行属性的 UI 引用。
    /// 例如“攻击力 25”就是一个 AttributeRowView。
    /// </summary>
    private class AttributeRowView
    {
        // 稳定行标识，用来复用 UI 并检测数值变化。
        public string Key;
        // 属性行最外层物体。
        public GameObject Root;
        // 行背景，数值变化时会被高亮。
        public Image Background;
        // 左侧属性名文本。
        public Text LabelText;
        // 右侧属性值文本。
        public Text ValueText;
        // 上一次显示的值，用来判断是否需要高亮。
        public string LastValue;
        // 高亮剩余时间。
        public float HighlightTimer;
    }

    [SerializeField] private Canvas targetCanvas;

    // showOnStart 控制游戏开始时是否默认展开属性面板。
    [SerializeField] private bool showOnStart = false;
    [SerializeField] private bool editorPreviewVisible = true;

    // 切换面板的按键，默认 Tab。
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    // 下面这些尺寸/颜色主要用于 Inspector 调整外观。
    [SerializeField] private Vector2 panelSize = new Vector2(1080f, 360f);
    [SerializeField] private Vector2 anchoredPosition = new Vector2(-24f, -24f);
    [SerializeField] private Color backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.9f);
    [SerializeField] private Color headerTintColor = new Color(0.16f, 0.28f, 0.46f, 0.45f);
    [SerializeField] private Color sectionColor = new Color(0.12f, 0.15f, 0.21f, 0.85f);
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.06f);
    [SerializeField] private Color rowHighlightColor = new Color(0.18f, 0.48f, 0.36f, 0.35f);
    [SerializeField] private Color frameColor = new Color(0.43f, 0.54f, 0.72f, 0.8f);
    [SerializeField] private Color titleColor = Color.white;
    [SerializeField] private Color summaryColor = new Color(0.78f, 0.84f, 0.92f, 1f);
    [SerializeField] private Color sectionTitleColor = new Color(0.83f, 0.89f, 0.97f, 1f);
    [SerializeField] private Color labelColor = new Color(0.72f, 0.79f, 0.88f, 1f);
    [SerializeField] private Color valueColor = new Color(0.96f, 0.98f, 1f, 1f);
    [SerializeField] private Color valueHighlightColor = new Color(0.44f, 1f, 0.79f, 1f);
    [SerializeField] private float valueHighlightDuration = 0.9f;
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private Text titleText;
    [SerializeField] private Text summaryText;

    // entryBuffer 用来接收 PlayerCo 传来的属性数据。
    private readonly List<PlayerCo.AttributePanelEntry> entryBuffer = new List<PlayerCo.AttributePanelEntry>(16);

    // 保存当前生成的行，方便每帧更新高亮颜色。
    private readonly List<AttributeRowView> rowViewList = new List<AttributeRowView>(16);

    // 这些集合用于记录本次刷新里哪些分组/行仍然有效。
    private readonly List<string> activeRowKeys = new List<string>(16);
    private readonly List<string> activeSectionNames = new List<string>(4);

    // 字典可以通过 key 快速找到对应 UI 行/分组。
    private readonly Dictionary<string, AttributeRowView> rowViewsByKey = new Dictionary<string, AttributeRowView>(16);
    private readonly Dictionary<string, SectionView> sectionsByName = new Dictionary<string, SectionView>(4);

    // 缓存同一玩家物体上的依赖组件和字体。
    private PlayerCo player;
    private GameSessionUi sessionUi;
    private Font font;
    private bool isVisible;
#if UNITY_EDITOR
    private bool editorUiRefreshQueued;
#endif

    private void Awake()
    {
        // 属性面板和 PlayerCo、GameSessionUi 都挂在玩家物体上。
        player = GetComponent<PlayerCo>();
        sessionUi = GetComponent<GameSessionUi>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    /// <summary>
    /// 启用时订阅玩家属性变化，或在编辑器里排队刷新预览。
    /// </summary>
    private void OnEnable()
    {
        if (player == null)
        {
            player = GetComponent<PlayerCo>();
        }

        if (Application.isPlaying)
        {
            if (player != null)
            {
                // 玩家属性变化时刷新面板，不需要每帧重建 UI。
                player.StatsChanged += HandleStatsChanged;
            }
        }
        else
        {
            QueueEditorUiRefresh();
        }
    }

    /// <summary>
    /// 禁用时取消事件订阅和编辑器延迟刷新。
    /// </summary>
    private void OnDisable()
    {
        if (Application.isPlaying && player != null)
        {
            player.StatsChanged -= HandleStatsChanged;
        }

#if UNITY_EDITOR
        EditorApplication.delayCall -= RefreshEditorUiFromDelayCall;
        editorUiRefreshQueued = false;
#endif
    }

    /// <summary>
    /// Inspector 参数变化时刷新编辑器预览。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            QueueEditorUiRefresh();
        }
    }

    /// <summary>
    /// 运行开始时创建属性面板并刷新一次显示。
    /// </summary>
    private void Start()
    {
        if (!Application.isPlaying)
        {
            EnsureEditorUi();
            return;
        }

        EnsureCanvas();
        BuildPanelIfNeeded();
        RefreshView();
        SetVisible(showOnStart);
    }

    /// <summary>
    /// 处理 Tab 开关面板，并驱动属性变化高亮。
    /// </summary>
    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // 按 Tab 切换面板，但如果升级/暂停等 UI 正在挡住输入，就不切换。
        if (Input.GetKeyDown(toggleKey) && CanTogglePanel())
        {
            SetVisible(!isVisible);
        }

        if (isVisible)
        {
            UpdateValueHighlights();
        }
    }

    /// <summary>
    /// 编辑器路径：确保 UI 存在并显示预览数据。
    /// </summary>
    private void EnsureEditorUi()
    {
        player = GetComponent<PlayerCo>();
        sessionUi = GetComponent<GameSessionUi>();
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        EnsureCanvas();
        BuildPanelIfNeeded();
        RefreshView();
        if (panelRoot != null)
        {
            panelRoot.SetActive(editorPreviewVisible);
        }
    }

    /// <summary>
    /// 合并编辑器里的多次刷新请求，避免 OnValidate 反复重建 UI。
    /// </summary>
    private void QueueEditorUiRefresh()
    {
#if UNITY_EDITOR
        // 编辑器里 OnValidate 可能触发很多次，用 delayCall 合并成稍后刷新一次。
        if (editorUiRefreshQueued)
        {
            return;
        }

        editorUiRefreshQueued = true;
        EditorApplication.delayCall += RefreshEditorUiFromDelayCall;
#else
        EnsureEditorUi();
#endif
    }

#if UNITY_EDITOR
    private void RefreshEditorUiFromDelayCall()
    {
        EditorApplication.delayCall -= RefreshEditorUiFromDelayCall;
        editorUiRefreshQueued = false;

        if (this == null || gameObject == null || Application.isPlaying)
        {
            return;
        }

        EnsureEditorUi();
    }
#endif

    private void EnsureCanvas()
    {
        // 找不到手动指定的 Canvas 时，复用或创建一个运行时 UI Canvas。
        if (targetCanvas == null)
        {
            targetCanvas = RuntimeUiCanvasProvider.FindBestCanvas();
        }

        if (targetCanvas == null && Application.isPlaying)
        {
            targetCanvas = RuntimeUiCanvasProvider.GetOrCreateCanvas();
        }
    }

    /// <summary>
    /// 创建或复用属性面板根节点、头部和内容区域。
    /// </summary>
    private void BuildPanelIfNeeded()
    {
        if (targetCanvas == null)
        {
            return;
        }

        // 先找已有 UI，找不到才创建，方便你在层级里手动微调生成后的对象。
        ResolveExistingReferences();

        if (panelRoot == null)
        {
            // panelRoot 是整个属性面板的最外层背景。
            panelRoot = new GameObject("PlayerAttributePanel", typeof(RectTransform), typeof(Image), typeof(Outline));
            panelRoot.transform.SetParent(targetCanvas.transform, false);
        }

        panelRect = panelRoot.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 1f);
        panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        ApplyPanelRect();

        Image rootImage = panelRoot.GetComponent<Image>();
        rootImage.color = backgroundColor;

        Outline rootOutline = panelRoot.GetComponent<Outline>();
        rootOutline.effectColor = frameColor;
        rootOutline.effectDistance = new Vector2(1f, -1f);

        if (titleText == null || summaryText == null)
        {
            BuildOrResolveHeader();
        }

        if (contentRoot == null)
        {
            BuildOrResolveContentRoot();
        }
    }

    /// <summary>
    /// 尝试从已有层级里找回自动生成的面板引用。
    /// </summary>
    private void ResolveExistingReferences()
    {
        // 自动生成 UI 后，Unity 会把对象留在层级里。
        // 这段代码负责再次找到它们，避免重复生成一套。
        if (panelRoot == null)
        {
            Transform panelTransform = targetCanvas.transform.Find("PlayerAttributePanel");
            if (panelTransform != null)
            {
                panelRoot = panelTransform.gameObject;
            }
        }

        if (panelRoot == null)
        {
            return;
        }

        if (panelRect == null)
        {
            panelRect = panelRoot.GetComponent<RectTransform>();
        }

        Transform header = panelRoot.transform.Find("Header");
        if (titleText == null && header != null)
        {
            Transform child = header.Find("Title");
            if (child != null)
            {
                titleText = child.GetComponent<Text>();
            }
        }

        if (summaryText == null && header != null)
        {
            Transform child = header.Find("Summary");
            if (child != null)
            {
                summaryText = child.GetComponent<Text>();
            }
        }

        if (contentRoot == null)
        {
            Transform contentTransform = panelRoot.transform.Find("Content");
            if (contentTransform != null)
            {
                contentRoot = contentTransform.GetComponent<RectTransform>();
            }
        }
    }

    /// <summary>
    /// 创建或复用标题和摘要区域。
    /// </summary>
    private void BuildOrResolveHeader()
    {
        // Header 是面板顶部区域，显示标题和摘要。
        Transform headerTransform = panelRoot.transform.Find("Header");
        GameObject headerObject;
        if (headerTransform != null)
        {
            headerObject = headerTransform.gameObject;
        }
        else
        {
            headerObject = new GameObject("Header", typeof(RectTransform), typeof(Image));
            headerObject.transform.SetParent(panelRoot.transform, false);
        }

        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 76f);
        headerRect.anchoredPosition = Vector2.zero;

        Image headerImage = headerObject.GetComponent<Image>();
        headerImage.color = headerTintColor;

        if (titleText == null)
        {
            // 标题，比如“角色属性”。
            titleText = CreateText("Title", headerObject.transform, 24, titleColor, TextAnchor.UpperLeft);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(16f, 28f);
            titleRect.offsetMax = new Vector2(-16f, -10f);
        }

        if (summaryText == null)
        {
            // 摘要，比如等级、血量、经验。
            summaryText = CreateText("Summary", headerObject.transform, 14, summaryColor, TextAnchor.LowerLeft);
            RectTransform summaryRect = summaryText.rectTransform;
            summaryRect.anchorMin = new Vector2(0f, 0f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.offsetMin = new Vector2(16f, 10f);
            summaryRect.offsetMax = new Vector2(-16f, -34f);
        }
    }

    /// <summary>
    /// 创建或复用承载属性分组的内容根节点。
    /// </summary>
    private void BuildOrResolveContentRoot()
    {
        // Content 是下面放各个属性分组的容器，用 HorizontalLayoutGroup 横向排布。
        Transform contentTransform = panelRoot.transform.Find("Content");
        GameObject contentObject;
        if (contentTransform != null)
        {
            contentObject = contentTransform.gameObject;
        }
        else
        {
            contentObject = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            contentObject.transform.SetParent(panelRoot.transform, false);
        }

        contentRoot = contentObject.GetComponent<RectTransform>();
        contentRoot.anchorMin = new Vector2(0f, 0f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.offsetMin = new Vector2(14f, 14f);
        contentRoot.offsetMax = new Vector2(-14f, -88f);

        HorizontalLayoutGroup layout = contentObject.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    /// <summary>
    /// 创建一个统一字体、字号、颜色和对齐方式的 Text。
    /// </summary>
    private Text CreateText(string objectName, Transform parent, int fontSize, Color color, TextAnchor alignment)
    {
        // 统一创建 Text，避免每个地方重复设置字体、字号、颜色。
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    /// <summary>
    /// 显示或隐藏属性面板，并在显示时刷新内容。
    /// </summary>
    private void SetVisible(bool visible)
    {
        isVisible = visible;

        // 这里只负责开关 GameObject；真正内容刷新在显示时执行。
        if (panelRoot != null)
        {
            panelRoot.SetActive(visible);
        }

        if (visible)
        {
            RefreshView();
            UpdateValueHighlights();
        }
    }

    /// <summary>
    /// 判断当前是否允许玩家用快捷键打开/关闭属性面板。
    /// </summary>
    private bool CanTogglePanel()
    {
        // 升级选择打开时不能切属性面板，否则多个 UI 都抢鼠标。
        if (player != null && player.IsUpgradeSelectionActive)
        {
            return false;
        }

        if (sessionUi == null)
        {
            sessionUi = GetComponent<GameSessionUi>();
        }

        return sessionUi == null || !sessionUi.IsGameplayInputBlocked;
    }

    /// <summary>
    /// 玩家属性变化事件回调。
    /// </summary>
    private void HandleStatsChanged()
    {
        RefreshView();
    }

    /// <summary>
    /// 从 PlayerCo 读取最新属性条目，并同步到面板 UI。
    /// </summary>
    private void RefreshView()
    {
        if (player == null)
        {
            return;
        }

        EnsureCanvas();
        BuildPanelIfNeeded();
        if (panelRoot == null || contentRoot == null)
        {
            return;
        }

        ApplyPanelRect();

        // 这里每次都重建生成内容，代码更简单；属性行数量很少，性能压力不大。
        RebuildGeneratedContent();

        // 从 PlayerCo 获取“已经分好组、格式化好的属性行数据”。
        player.GetAttributePanelEntries(entryBuffer);
        activeRowKeys.Clear();
        activeSectionNames.Clear();

        for (int i = 0; i < entryBuffer.Count; i++)
        {
            PlayerCo.AttributePanelEntry entry = entryBuffer[i];
            SectionView sectionView = GetOrCreateSection(entry.GroupName);
            AttributeRowView rowView = GetOrCreateRow(sectionView, entry);

            // 把数据写进 UI 文本。
            sectionView.Root.SetActive(true);
            rowView.Root.SetActive(true);
            rowView.LabelText.text = entry.Label;
            rowView.ValueText.text = entry.Value;

            if (!string.IsNullOrEmpty(rowView.LastValue) && rowView.LastValue != entry.Value)
            {
                // 如果数值和上次不一样，启动高亮倒计时。
                rowView.HighlightTimer = valueHighlightDuration;
            }

            rowView.LastValue = entry.Value;

            if (!activeSectionNames.Contains(entry.GroupName))
            {
                activeSectionNames.Add(entry.GroupName);
            }

            activeRowKeys.Add(entry.Key);
        }

        UpdateHeaderText();
        ForceLayoutRefresh();
    }

    /// <summary>
    /// 根据属性条目创建/复用分组和行，并隐藏本次不用的旧 UI。
    /// </summary>
    private void RebuildGeneratedContent()
    {
        // 清空旧行和旧分组。因为属性数量不多，直接重建比复杂复用更容易看懂。
        rowViewList.Clear();
        rowViewsByKey.Clear();
        sectionsByName.Clear();

        if (contentRoot == null)
        {
            return;
        }

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = contentRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private SectionView GetOrCreateSection(string sectionName)
    {
        // 如果这个分组已经创建过，直接返回。
        SectionView sectionView;
        if (sectionsByName.TryGetValue(sectionName, out sectionView))
        {
            return sectionView;
        }

        GameObject sectionObject = new GameObject(
            $"Section_{sectionName}",
            typeof(RectTransform),
            typeof(Image),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        sectionObject.transform.SetParent(contentRoot, false);

        Image sectionImage = sectionObject.GetComponent<Image>();
        sectionImage.color = sectionColor;

        // LayoutGroup 负责自动排列子物体，避免手动计算每行坐标。
        Outline sectionOutline = sectionObject.AddComponent<Outline>();
        sectionOutline.effectColor = new Color(frameColor.r, frameColor.g, frameColor.b, 0.45f);
        sectionOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup sectionLayout = sectionObject.GetComponent<VerticalLayoutGroup>();
        sectionLayout.padding = new RectOffset(10, 10, 8, 8);
        sectionLayout.spacing = 4f;
        sectionLayout.childAlignment = TextAnchor.UpperCenter;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = false;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;

        ContentSizeFitter sectionFitter = sectionObject.GetComponent<ContentSizeFitter>();
        sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sectionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        LayoutElement sectionElement = sectionObject.AddComponent<LayoutElement>();
        sectionElement.flexibleWidth = 1f;

        Text sectionTitle = CreateText("SectionTitle", sectionObject.transform, 15, sectionTitleColor, TextAnchor.MiddleLeft);
        LayoutElement titleLayout = sectionTitle.gameObject.AddComponent<LayoutElement>();
        titleLayout.minHeight = 16f;
        sectionTitle.text = sectionName;

        GameObject rowsObject = new GameObject(
            "Rows",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        rowsObject.transform.SetParent(sectionObject.transform, false);

        VerticalLayoutGroup rowsLayout = rowsObject.GetComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 4f;
        rowsLayout.childAlignment = TextAnchor.UpperCenter;
        rowsLayout.childControlWidth = true;
        rowsLayout.childControlHeight = false;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;

        ContentSizeFitter rowsFitter = rowsObject.GetComponent<ContentSizeFitter>();
        rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rowsFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        sectionView = new SectionView
        {
            Name = sectionName,
            Root = sectionObject,
            RowsRoot = rowsObject.GetComponent<RectTransform>()
        };

        sectionsByName.Add(sectionName, sectionView);
        return sectionView;
    }

    private AttributeRowView GetOrCreateRow(SectionView sectionView, PlayerCo.AttributePanelEntry entry)
    {
        // 每个属性用 entry.Key 做唯一标识。
        AttributeRowView rowView;
        if (rowViewsByKey.TryGetValue(entry.Key, out rowView))
        {
            return rowView;
        }

        GameObject rowObject = new GameObject(
            $"Row_{entry.Key}",
            typeof(RectTransform),
            typeof(Image),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        rowObject.transform.SetParent(sectionView.RowsRoot, false);

        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = rowColor;

        HorizontalLayoutGroup rowLayout = rowObject.GetComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(10, 10, 6, 6);
        rowLayout.spacing = 12f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        LayoutElement rowElement = rowObject.GetComponent<LayoutElement>();
        rowElement.minHeight = 28f;
        rowElement.flexibleWidth = 1f;

        Text labelText = CreateText("Label", rowObject.transform, 14, labelColor, TextAnchor.MiddleLeft);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.flexibleWidth = 1f;
        labelLayout.minWidth = 88f;

        Text valueText = CreateText("Value", rowObject.transform, 14, valueColor, TextAnchor.MiddleRight);
        LayoutElement valueLayout = valueText.gameObject.AddComponent<LayoutElement>();
        valueLayout.minWidth = 84f;
        valueLayout.preferredWidth = 104f;

        rowView = new AttributeRowView
        {
            Key = entry.Key,
            Root = rowObject,
            Background = rowImage,
            LabelText = labelText,
            ValueText = valueText
        };

        rowViewsByKey.Add(entry.Key, rowView);
        rowViewList.Add(rowView);
        return rowView;
    }

    /// <summary>
    /// 刷新面板标题和当前等级/经验摘要。
    /// </summary>
    private void UpdateHeaderText()
    {
        // 顶部标题和摘要每次刷新时重新写入。
        if (titleText != null)
        {
            titleText.text = "角色属性";
        }

        if (summaryText != null && player != null)
        {
            summaryText.text = $"等级 {player.Lv}    HP {player.Hp}/{player.Hpmax}    EXP {player.curExp}/{player.curExpMax}";
        }
    }

    /// <summary>
    /// 让刚变化的属性行在一小段时间内高亮，然后渐变回普通颜色。
    /// </summary>
    private void UpdateValueHighlights()
    {
        // 使用 unscaledDeltaTime：即使升级面板/暂停时 Time.timeScale = 0，高亮也能正常消退。
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = 0; i < rowViewList.Count; i++)
        {
            AttributeRowView rowView = rowViewList[i];
            if (rowView.HighlightTimer > 0f)
            {
                rowView.HighlightTimer = Mathf.Max(0f, rowView.HighlightTimer - deltaTime);
            }

            float highlightT = valueHighlightDuration > 0f
                ? Mathf.Clamp01(rowView.HighlightTimer / valueHighlightDuration)
                : 0f;

            // Lerp 根据 highlightT 在普通颜色和高亮颜色之间插值。
            if (rowView.Background != null)
            {
                rowView.Background.color = Color.Lerp(rowColor, rowHighlightColor, highlightT);
            }

            if (rowView.ValueText != null)
            {
                rowView.ValueText.color = Color.Lerp(valueColor, valueHighlightColor, highlightT);
            }
        }
    }

    /// <summary>
    /// 把 Inspector 中的尺寸和锚点参数应用到面板 RectTransform。
    /// </summary>
    private void ApplyPanelRect()
    {
        if (panelRect == null)
        {
            return;
        }

        // 面板不能比屏幕还大；这里按 Canvas 尺寸做一个自适应限制。
        Vector2 resolvedSize = panelSize;
        if (targetCanvas != null)
        {
            RectTransform canvasRect = targetCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                float availableWidth = Mathf.Max(320f, canvasRect.rect.width - 48f);
                float availableHeight = Mathf.Max(240f, canvasRect.rect.height - 48f);
                resolvedSize = new Vector2(
                    Mathf.Min(panelSize.x, availableWidth),
                    Mathf.Min(panelSize.y, availableHeight));
            }
        }

        panelRect.sizeDelta = resolvedSize;
        panelRect.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// 强制刷新 Unity UI 布局，确保动态创建的行立即排列正确。
    /// </summary>
    private void ForceLayoutRefresh()
    {
        // 强制 Unity 立刻重新计算布局，避免刚创建 UI 时显示位置不对。
        if (panelRect == null || contentRoot == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Canvas.ForceUpdateCanvases();
    }
}
