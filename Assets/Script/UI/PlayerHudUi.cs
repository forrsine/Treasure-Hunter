using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家常驻 HUD：负责显示主界面的等级、生命、魔法、经验和体力。
/// 注意：这里只读取 PlayerModel 和 PlayerMovementComponent，不直接修改玩家数据，
/// 避免 UI 层反向影响核心玩法逻辑。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PlayerHudUi : MonoBehaviour, IController
{
    [Header("Prefab References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Text levelText;
    [SerializeField] private Text hpText;
    [SerializeField] private Text mpText;
    [SerializeField] private Text staminaText;
    [SerializeField] private Text expText;
    [SerializeField] private Scrollbar hpBar;
    [SerializeField] private Scrollbar mpBar;
    [SerializeField] private Scrollbar staminaBar;
    [SerializeField] private Scrollbar expBar;

    [Header("Bar Animation")]
    [SerializeField] private float barChangeSpeed = 4f;

    [Header("Editor Preview")]
    [SerializeField] private bool editorPreviewVisible = true;

    private PlayerMovementComponent cachedMovement;
    private float targetHpPercent = 1f;
    private float targetMpPercent = 1f;
    private float targetStaminaPercent = 1f;
    private float targetExpPercent;
    private float displayedStaminaPercent = -1f;
    private int displayedStaminaCurrent = -1;
    private int displayedStaminaMax = -1;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 运行时订阅玩家属性变化和当前玩家切换事件。
    /// 血量、魔法、等级、经验由事件驱动刷新；体力会随跑步、翻滚逐帧变化，所以在 Update 中轻量同步。
    /// </summary>
    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            this.RegisterEvent<PlayerStatsChangedEvent>(HandlePlayerStatsChanged);
            GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;
            HandleCurrentPlayerChanged(GameplayRuntime.Instance.CurrentPlayer);
            return;
        }

        ApplyEditorPreviewIfReady();
    }

    /// <summary>
    /// 停用时解除事件订阅，避免切换场景后旧 UI 继续收到回调。
    /// </summary>
    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<PlayerStatsChangedEvent>(HandlePlayerStatsChanged);
        GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreviewIfReady();
        }
    }

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

        RefreshPlayerStats(true);
        RefreshStamina(true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshStamina(false);
        AnimateBars();
    }

    /// <summary>
    /// 校验 Prefab 静态引用是否完整。
    /// 少任何一个文本或 Scrollbar，都说明 GameplayUiRoot.prefab 没有装配好。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (hudRoot == null) missing = nameof(hudRoot);
        else if (levelText == null) missing = nameof(levelText);
        else if (hpText == null) missing = nameof(hpText);
        else if (mpText == null) missing = nameof(mpText);
        else if (staminaText == null) missing = nameof(staminaText);
        else if (expText == null) missing = nameof(expText);
        else if (hpBar == null) missing = nameof(hpBar);
        else if (mpBar == null) missing = nameof(mpBar);
        else if (staminaBar == null) missing = nameof(staminaBar);
        else if (expBar == null) missing = nameof(expBar);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"PlayerHudUi 的 Prefab 引用未配置：{missing}。请修复 GameplayUiRoot.prefab。", this);
        }

        return false;
    }

    private void HandlePlayerStatsChanged(PlayerStatsChangedEvent _)
    {
        RefreshPlayerStats(false);
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        cachedMovement = player != null ? player.GetComponent<PlayerMovementComponent>() : null;
        displayedStaminaPercent = -1f;
        displayedStaminaCurrent = -1;
        displayedStaminaMax = -1;
        RefreshPlayerStats(true);
        RefreshStamina(true);
    }

    /// <summary>
    /// 刷新血量、魔法、等级和经验。
    /// 这些数据来自 PlayerModel，所以用事件驱动即可，不需要每帧查询。
    /// </summary>
    private void RefreshPlayerStats(bool immediate)
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        PlayerStatsSnapshot stats = this.SendQuery(new GetPlayerStatsQuery());
        levelText.text = $"Lv.{stats.Level}";
        hpText.text = $"HP {stats.CurrentHp}/{stats.MaxHp}";
        mpText.text = $"MP {stats.CurrentMp}/{stats.MaxMp}";
        expText.text = $"EXP {stats.CurrentExp}/{stats.ExpToNextLevel}";

        SetBarTarget(hpBar, ref targetHpPercent, stats.MaxHp > 0 ? (float)stats.CurrentHp / stats.MaxHp : 0f, immediate);
        SetBarTarget(mpBar, ref targetMpPercent, stats.MaxMp > 0 ? (float)stats.CurrentMp / stats.MaxMp : 0f, immediate);
        SetBarTarget(expBar, ref targetExpPercent, stats.ExpToNextLevel > 0 ? (float)stats.CurrentExp / stats.ExpToNextLevel : 0f, immediate);
    }

    /// <summary>
    /// 刷新体力。
    /// 体力变化频率高，直接监听事件反而需要给移动组件增加大量事件。
    /// 这里缓存当前玩家移动组件，只在数值变化时更新 UI，开销很小。
    /// </summary>
    private void RefreshStamina(bool force)
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        if (cachedMovement == null && GameplayRuntime.Instance.CurrentPlayer != null)
        {
            cachedMovement = GameplayRuntime.Instance.CurrentPlayer.GetComponent<PlayerMovementComponent>();
        }

        float percent = cachedMovement != null ? cachedMovement.StaminaPercent : 0f;
        int current = cachedMovement != null ? Mathf.RoundToInt(cachedMovement.CurrentStamina) : 0;
        int max = cachedMovement != null ? Mathf.RoundToInt(cachedMovement.MaxStamina) : 0;
        bool changed =
            force ||
            Mathf.Abs(percent - displayedStaminaPercent) > 0.001f ||
            current != displayedStaminaCurrent ||
            max != displayedStaminaMax;

        if (!changed)
        {
            return;
        }

        displayedStaminaPercent = percent;
        displayedStaminaCurrent = current;
        displayedStaminaMax = max;
        staminaText.text = max > 0 ? $"SP {current}/{max}" : "SP --";
        SetBarTarget(staminaBar, ref targetStaminaPercent, percent, force);
    }

    private void AnimateBars()
    {
        AnimateBar(hpBar, targetHpPercent);
        AnimateBar(mpBar, targetMpPercent);
        AnimateBar(staminaBar, targetStaminaPercent);
        AnimateBar(expBar, targetExpPercent);
    }

    private void AnimateBar(Scrollbar bar, float targetPercent)
    {
        if (bar == null)
        {
            return;
        }

        float next = Mathf.MoveTowards(
            bar.size,
            Mathf.Clamp01(targetPercent),
            Mathf.Max(0.01f, barChangeSpeed) * Time.deltaTime);
        ApplyBarPercent(bar, next);
    }

    private void SetBarTarget(Scrollbar bar, ref float targetPercent, float percent, bool immediate)
    {
        targetPercent = Mathf.Clamp01(percent);
        if (immediate || !Application.isPlaying)
        {
            ApplyBarPercent(bar, targetPercent);
        }
    }

    private void ApplyBarPercent(Scrollbar bar, float percent)
    {
        if (bar == null)
        {
            return;
        }

        // Scrollbar 的 size 表示把手长度，更适合做资源条；value 固定为 0，让填充始终从左往右变化。
        bar.direction = Scrollbar.Direction.LeftToRight;
        bar.SetValueWithoutNotify(0f);
        bar.size = Mathf.Clamp01(percent);
    }

    private void ApplyEditorPreviewIfReady()
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        hudRoot.SetActive(editorPreviewVisible);
        levelText.text = "Lv.1";
        hpText.text = "HP 150/150";
        mpText.text = "MP 120/120";
        staminaText.text = "SP 100/100";
        expText.text = "EXP 0/50";
        SetBarTarget(hpBar, ref targetHpPercent, 1f, true);
        SetBarTarget(mpBar, ref targetMpPercent, 1f, true);
        SetBarTarget(staminaBar, ref targetStaminaPercent, 1f, true);
        SetBarTarget(expBar, ref targetExpPercent, 0f, true);
    }
}
