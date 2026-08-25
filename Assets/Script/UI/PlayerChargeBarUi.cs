using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战士蓄力条表现：只读取当前玩家的蓄力状态，不修改战斗数据。
/// UI 与状态机分离后，主场景和 Boss 场景能复用同一个 GameplayUiRoot Prefab。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerChargeBarUi : MonoBehaviour
{
    [SerializeField] private GameObject barRoot;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text multiplierText;
    [SerializeField] private Color chargingColor = new Color(1f, 0.42f, 0.08f, 1f);
    [SerializeField] private Color fullChargeColor = new Color(1f, 0.82f, 0.2f, 1f);

    private PlayerChargedAttackComponent cachedChargedAttack;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            Hide();
            return;
        }

        GameplayRuntime.Instance.CurrentPlayerChanged += HandleCurrentPlayerChanged;
        HandleCurrentPlayerChanged(GameplayRuntime.Instance.CurrentPlayer);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            GameplayRuntime.Instance.CurrentPlayerChanged -= HandleCurrentPlayerChanged;
        }

        Hide();
    }

    private void Start()
    {
        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        Hide();
    }

    private void Update()
    {
        if (!Application.isPlaying || !ValidatePrefabReferences(false))
        {
            return;
        }

        if (cachedChargedAttack == null && GameplayRuntime.Instance.CurrentPlayer != null)
        {
            cachedChargedAttack = GameplayRuntime.Instance.CurrentPlayer.ChargedAttack;
        }

        if (cachedChargedAttack == null || !cachedChargedAttack.IsHoldingCharge || Time.timeScale <= 0f)
        {
            Hide();
            return;
        }

        float progress = cachedChargedAttack.ChargeProgress;
        if (!barRoot.activeSelf)
        {
            barRoot.SetActive(true);
        }

        // 使用左侧 Pivot 缩放填充宽度，不依赖额外 Sprite，美术资源缺失时也能稳定显示纯色进度。
        fillImage.rectTransform.localScale = new Vector3(progress, 1f, 1f);
        bool isFull = progress >= 0.999f;
        fillImage.color = isFull ? fullChargeColor : chargingColor;
        multiplierText.text = isFull
            ? $"蓄力完成 x{cachedChargedAttack.CurrentDamageMultiplier:0.0}"
            : $"蓄力 x{cachedChargedAttack.CurrentDamageMultiplier:0.0}";
    }

    /// <summary>
    /// 公共 UI Prefab 的静态装配校验，避免场景打包后因引用缺失而只看不到蓄力条。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (barRoot == null) missing = nameof(barRoot);
        else if (fillImage == null) missing = nameof(fillImage);
        else if (multiplierText == null) missing = nameof(multiplierText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"PlayerChargeBarUi 的 Prefab 引用未配置：{missing}。", this);
        }

        return false;
    }

    private void HandleCurrentPlayerChanged(PlayerRuntimeController player)
    {
        cachedChargedAttack = player != null ? player.ChargedAttack : null;
        Hide();
    }

    private void Hide()
    {
        if (fillImage != null)
        {
            fillImage.rectTransform.localScale = new Vector3(0f, 1f, 1f);
        }

        if (barRoot != null && barRoot.activeSelf)
        {
            barRoot.SetActive(false);
        }
    }
}
