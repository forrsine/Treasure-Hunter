using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 游戏结束面板。
/// 
/// 新手阅读顺序：
/// 1. 这个脚本挂在一个 UI 面板物体上。
/// 2. EnsureUi 会自动创建标题、分数文本、重新开始按钮和退出按钮。
/// 3. ReStart 重新加载主场景，ExitGame 在编辑器里停止播放/打包后退出程序。
/// 4. ShowRuntimeGameOver 会暂停游戏、显示鼠标、刷新本局分数和最高分。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ReStartPanel : MonoBehaviour
{
    // 自动创建出来的子物体名字。用常量可以避免多处字符串不一致。
    private const string TitleObjectName = "RuntimeTitleText";
    private const string SummaryObjectName = "RuntimeSummaryText";
    private const string RestartButtonObjectName = "RuntimeRestartButton";
    private const string QuitButtonObjectName = "RuntimeQuitButton";

    [SerializeField] private bool editorPreviewVisible = true;

    // 自动创建 UI 时复用的字体和根 RectTransform。
    private Font font;
    private RectTransform rootRect;

    // 运行时自动创建或找到的 UI 引用。
    private Text titleText;
    private Text summaryText;
    private Button restartButton;
    private Button quitButton;
    private bool hasHiddenOnPlayStart;

    /// <summary>
    /// 缓存字体和 RectTransform，并确保运行时结束面板结构存在。
    /// </summary>
    private void Awake()
    {
        // Awake/OnEnable/OnValidate 都会确保 UI 存在，这样编辑器预览和运行时都稳定。
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        rootRect = GetComponent<RectTransform>();
        EnsureUi();
        ApplyEditorPreview();
    }

    /// <summary>
    /// 面板启用时区分编辑器预览、开局隐藏和真正游戏结束三种情况。
    /// </summary>
    private void OnEnable()
    {
        font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        rootRect = GetComponent<RectTransform>();
        EnsureUi();

        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
            return;
        }

        if (!hasHiddenOnPlayStart)
        {
            // 开局先隐藏结束面板，等玩家死亡时再由外部 SetActive(true) 打开。
            hasHiddenOnPlayStart = true;
            gameObject.SetActive(false);
            return;
        }

        ShowRuntimeGameOver();
    }

    /// <summary>
    /// 编辑器里改 Inspector 参数时重建预览 UI。
    /// </summary>
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            rootRect = GetComponent<RectTransform>();
            EnsureUi();
            ApplyEditorPreview();
        }
    }

    /// <summary>
    /// 点击“重新开始”时保存分数并重载主场景。
    /// </summary>
    public void ReStart()
    {
        // 重开前先保存分数并恢复时间，避免新场景被暂停。
        PersistCurrentScore();
        RestoreGameplayState();
        SceneManager.LoadScene(GameSceneNames.GameplayScene);
    }

    /// <summary>
    /// 点击“退出游戏”时保存分数并退出播放/程序。
    /// </summary>
    public void ExitGame()
    {
        // 退出前也保存分数。
        PersistCurrentScore();
        RestoreGameplayState();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// 真正进入游戏结束状态时暂停时间、显示鼠标并刷新分数。
    /// </summary>
    private void ShowRuntimeGameOver()
    {
        // 面板真正显示时，锁住玩法并释放鼠标让玩家点按钮。
        PersistCurrentScore();
        Time.timeScale = 0f;

        // 游戏结束面板出现时会显示鼠标；放到左上角可以避免刚死亡时误点按钮。
        CursorPopupUtility.ShowAtTopLeft();
        RefreshSummary();
        gameObject.SetActive(true);
    }

    /// <summary>
    /// 在非运行状态下填入示例文案，方便编辑器里调面板布局。
    /// </summary>
    private void ApplyEditorPreview()
    {
        // 非运行状态下用于预览面板排版。
        if (titleText != null)
        {
            titleText.text = "游戏结束";
        }

        if (summaryText != null)
        {
            summaryText.text = "当前分数：123\n历史最高分：456";
        }

        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
        }

        if (quitButton != null)
        {
            quitButton.gameObject.SetActive(true);
        }

        gameObject.SetActive(editorPreviewVisible);
    }

    /// <summary>
    /// 统一确保结束面板需要的文字和按钮都存在。
    /// </summary>
    private void EnsureUi()
    {
        // 统一入口：隐藏旧按钮、确保状态文字和按钮存在。
        if (rootRect == null)
        {
            rootRect = GetComponent<RectTransform>();
        }

        HideLegacyButtons();
        EnsureStatusTexts();
        EnsureButtons();
    }

    /// <summary>
    /// 隐藏旧版手摆按钮，避免和本脚本生成的按钮重叠。
    /// </summary>
    private void HideLegacyButtons()
    {
        // 如果场景里以前手动摆过旧按钮，这里先隐藏，避免和运行时按钮重叠。
        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (button.name == RestartButtonObjectName || button.name == QuitButtonObjectName)
            {
                continue;
            }

            button.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 创建或查找标题文本和分数摘要文本。
    /// </summary>
    private void EnsureStatusTexts()
    {
        // 创建/查找“游戏结束”标题。
        titleText = FindChildText(TitleObjectName);
        if (titleText == null)
        {
            titleText = CreateText(
                TitleObjectName,
                transform,
                34,
                Color.white,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 135f),
                new Vector2(420f, 44f));
            titleText.fontStyle = FontStyle.Bold;
        }

        // 创建/查找分数摘要文本。
        summaryText = FindChildText(SummaryObjectName);
        if (summaryText == null)
        {
            summaryText = CreateText(
                SummaryObjectName,
                transform,
                24,
                Color.white,
                TextAnchor.MiddleCenter,
                new Vector2(0f, 50f),
                new Vector2(460f, 90f));
        }
    }

    /// <summary>
    /// 创建或查找重新开始、退出游戏两个按钮。
    /// </summary>
    private void EnsureButtons()
    {
        // 创建/查找两个按钮：重新开始、退出游戏。
        restartButton = FindChildButton(RestartButtonObjectName);
        if (restartButton == null)
        {
            restartButton = CreateButton(
                RestartButtonObjectName,
                new Vector2(0f, -30f),
                new Color(0.92f, 0.92f, 0.92f, 1f),
                Color.black);
        }

        quitButton = FindChildButton(QuitButtonObjectName);
        if (quitButton == null)
        {
            quitButton = CreateButton(
                QuitButtonObjectName,
                new Vector2(0f, -120f),
                new Color(0.85f, 0.27f, 0.27f, 1f),
                Color.white);
        }

        ConfigureButton(restartButton, "重新开始", ReStart);
        ConfigureButton(quitButton, "退出游戏", ExitGame);
    }

    /// <summary>
    /// 统一配置按钮文字、点击事件和交互颜色。
    /// </summary>
    private void ConfigureButton(Button button, string label, UnityEngine.Events.UnityAction action)
    {
        // 给按钮配置显示文字和点击事件。
        if (button == null)
        {
            return;
        }

        Text labelText = button.GetComponentInChildren<Text>(true);
        if (labelText == null)
        {
            labelText = CreateText(
                "Label",
                button.transform,
                24,
                Color.white,
                TextAnchor.MiddleCenter,
                Vector2.zero,
                new Vector2(220f, 64f));
            labelText.rectTransform.anchorMin = Vector2.zero;
            labelText.rectTransform.anchorMax = Vector2.one;
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
        }

        labelText.font = font;
        labelText.fontSize = 24;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontStyle = FontStyle.Bold;
        labelText.supportRichText = false;
        labelText.text = label;

        button.onClick.RemoveAllListeners();
        if (Application.isPlaying)
        {
            // 只在运行时绑定点击事件，编辑器预览不执行游戏逻辑。
            button.onClick.AddListener(action);
        }
    }

    /// <summary>
    /// 刷新本局分数和历史最高分显示。
    /// </summary>
    private void RefreshSummary()
    {
        // 每次显示结束面板时刷新当前分数和历史最高分。
        if (titleText != null)
        {
            titleText.text = "游戏结束";
        }

        if (summaryText != null)
        {
            summaryText.text = $"当前分数：{GetCurrentScore()}\n历史最高分：{GameHighScore.GetHighScore()}";
        }
    }

    /// <summary>
    /// 从金库系统读取当前分数。
    /// </summary>
    private int GetCurrentScore()
    {
        // 分数来源是 BoxCo；如果场景里暂时没有金库，就显示 0。
        return BoxCo.instance != null ? BoxCo.instance.Score : 0;
    }

    /// <summary>
    /// 把当前分数写入最高分记录。
    /// </summary>
    private void PersistCurrentScore()
    {
        GameHighScore.UpdateHighScore(GetCurrentScore());
    }

    /// <summary>
    /// 离开结束面板前恢复时间流速和鼠标锁定。
    /// </summary>
    private void RestoreGameplayState()
    {
        // 保证退出/重开后时间恢复正常。
        Time.timeScale = 1f;

        // 退出或重开前释放鼠标，也保持在左上角。
        CursorPopupUtility.ShowAtTopLeft();
    }

    /// <summary>
    /// 在子物体里按名字查找按钮。
    /// </summary>
    private Button FindChildButton(string objectName)
    {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<Button>() : null;
    }

    /// <summary>
    /// 在子物体里按名字查找文本。
    /// </summary>
    private Text FindChildText(string objectName)
    {
        Transform child = transform.Find(objectName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    /// <summary>
    /// 创建一个带背景、文本和 Button 组件的 UI 按钮。
    /// </summary>
    private Button CreateButton(string objectName, Vector2 anchoredPosition, Color backgroundColor, Color textColor)
    {
        // 运行时创建按钮，包括背景 Image、Button 组件和文字 Label。
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button));
        buttonObject.transform.SetParent(transform, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = new Vector2(220f, 64f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = backgroundColor * 1.05f;
        colors.pressedColor = backgroundColor * 0.9f;
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        Text labelText = CreateText(
            "Label",
            buttonObject.transform,
            24,
            textColor,
            TextAnchor.MiddleCenter,
            Vector2.zero,
            new Vector2(220f, 64f));
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = Vector2.zero;
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.fontStyle = FontStyle.Bold;

        return button;
    }

    private Text CreateText(
        string objectName,
        Transform parent,
        int fontSize,
        Color color,
        TextAnchor alignment,
        Vector2 anchoredPosition,
        Vector2 size)
    {
        // 运行时创建 Text 的公共工具。
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = false;
        return text;
    }
}
