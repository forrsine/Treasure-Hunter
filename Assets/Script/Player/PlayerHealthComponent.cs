using QFramework;
using UnityEngine;

/// <summary>
/// 玩家生命表现组件：实现受击接口，并负责受击数字、受击闪红、满蓄力闪黄和闪避闪烁。
/// 生命值、闪避率和减伤公式由 PlayerCombatSystem 处理，本组件不直接修改权威数据。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHealthComponent : MonoBehaviour, FighterInterface, IController
{
    private enum CombatTintState
    {
        Default,
        FullCharge,
        Hit
    }

    [SerializeField] private Color hitFlashColor = new Color(0.86f, 0.22f, 0.22f, 1f);
    [SerializeField] private Color defaultHitColor = Color.white;
    [SerializeField] private float hitColorTime = 0.1f;
    [SerializeField] private Color fullChargeFlashColor = new Color(1f, 0.8352941f, 0.2901961f, 1f);
    [SerializeField, Min(0.01f)] private float fullChargeFlashInterval = 0.18f;
    [SerializeField] private float dodgeInvincibleDuration = 1f;
    [SerializeField] private float dodgeFlickerInterval = 0.08f;

    private PlayerRuntimeController runtimeController;
    private PlayerPresentationComponent presentation;
    private PlayerAudioComponent audioComponent;
    private PlayerChargedAttackComponent chargedAttack;
    private SkinnedMeshRenderer hitRenderer;
    private Material[] hitMaterials;
    private Color[] defaultColors;
    private Renderer[] dodgeRenderers;
    private bool[] dodgeRendererDefaultEnabled;
    private bool isHitFlashing;
    private float hitFlashStartedAt;
    private bool wasFullChargeGuardActive;
    private bool fullChargeFlashVisible;
    private float fullChargeFlashTimer;
    private CombatTintState appliedTintState = CombatTintState.Default;
    private bool isDodgeInvincible;
    private float dodgeInvincibleTimer;
    private float dodgeFlickerTimer;
    private bool dodgeVisible = true;
    private float regenBuffer;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    /// <summary>
    /// 绑定表现层和音效层，并缓存受击和闪避要用到的渲染器。
    /// </summary>
    public void Initialize(PlayerRuntimeController player)
    {
        RestoreDefaultMaterialColors();
        runtimeController = player;
        presentation = player != null ? player.Presentation : GetComponent<PlayerPresentationComponent>();
        audioComponent = player != null ? player.Audio : GetComponent<PlayerAudioComponent>();
        chargedAttack = player != null ? player.ChargedAttack : GetComponent<PlayerChargedAttackComponent>();
        CacheHitRenderer();
        CacheDodgeRenderers();
        ResetFullChargeFlashState();
    }

    /// <summary>
    /// 旧版脚本兼容入口，不再作为正式运行时依赖。
    /// </summary>
    public void Initialize(MonoBehaviour obsoleteOwner)
    {
        Initialize(GetComponent<PlayerRuntimeController>());
    }

    /// <summary>
    /// 推进闪避无敌帧和闪烁表现。
    /// 无敌时间结束后会自动恢复所有渲染器显示状态。
    /// </summary>
    public void TickInvincibility()
    {
        if (!isDodgeInvincible)
        {
            return;
        }

        dodgeInvincibleTimer -= Time.deltaTime;
        dodgeFlickerTimer -= Time.deltaTime;
        if (dodgeFlickerTimer <= 0f)
        {
            SetDodgeVisible(!dodgeVisible);
            dodgeFlickerTimer = Mathf.Max(0.01f, dodgeFlickerInterval);
        }

        if (dodgeInvincibleTimer <= 0f)
        {
            StopDodgeInvincibility();
        }
    }

    /// <summary>
    /// 推进受击闪红和满蓄力黄色闪烁。
    /// 两种效果由同一个入口按“受击红色 > 满蓄力黄色 > 原色”排序，避免多个组件互相覆盖材质。
    /// </summary>
    public void TickHitFlash()
    {
        if (isHitFlashing && Time.time - hitFlashStartedAt >= hitColorTime)
        {
            isHitFlashing = false;
        }

        TickFullChargeFlash(Time.deltaTime);
        CombatTintState desiredState = isHitFlashing
            ? CombatTintState.Hit
            : wasFullChargeGuardActive && fullChargeFlashVisible
                ? CombatTintState.FullCharge
                : CombatTintState.Default;
        ApplyTintState(desiredState);
    }

    /// <summary>
    /// 处理持续回血。
    /// 使用 buffer 累计小数，是为了支持每秒回复这种非整数数值，又不丢精度。
    /// </summary>
    public void ApplyHealthRegen()
    {
        IPlayerStatsReadOnly stats = this.GetModel<PlayerModel>().Stats;
        if (stats.HealthRegenPerSecond <= 0f || stats.CurrentHp >= stats.MaxHp || stats.CurrentHp <= 0)
        {
            if (stats.CurrentHp >= stats.MaxHp)
            {
                regenBuffer = 0f;
            }
            return;
        }

        regenBuffer += stats.HealthRegenPerSecond * Time.deltaTime;
        int amount = Mathf.FloorToInt(regenBuffer);
        if (amount <= 0)
        {
            return;
        }

        regenBuffer -= amount;
        RecoverHp(amount, true);
    }

    /// <summary>
     /// 所有怪物近战和子弹最终都进入这里，再由 Command 统一结算，避免不同攻击源各写一套扣血规则。
    /// </summary>
    public void Hit(int incomingAttackPower)
    {
        if (isDodgeInvincible)
        {
            return;
        }

        float temporaryDamageReduction = chargedAttack != null && chargedAttack.IsFullChargeGuardActive
            ? chargedAttack.FullChargeDamageReduction
            : 0f;
        PlayerDamageResult result = this.SendCommand(new TakePlayerDamageCommand(
            incomingAttackPower,
            true,
            temporaryDamageReduction));
        if (result.Dodged)
        {
            FloatingCombatText.ShowMiss(transform);
            StartDodgeInvincibility();
            return;
        }

        if (result.ActualDamage <= 0)
        {
            return;
        }

        FloatingCombatText.ShowTakenDamage(transform, result.ActualDamage);
        if (audioComponent != null && audioComponent.AutoPlayActions)
        {
            audioComponent.PlayHit();
        }

        StartHitFlash();
    }

    /// <summary>
    /// 统一回血入口。
    /// </summary>
    public void RecoverHp(int amount, bool showFloatingText = false)
    {
        this.SendCommand(new HealPlayerCommand(amount, showFloatingText));
    }

    /// <summary>
    /// 直接回满血量。
    /// </summary>
    public void FullHeal()
    {
        this.SendCommand(new FullHealPlayerCommand());
    }

    /// <summary>
    /// 清理运行时临时状态，常用于切场景或重置角色。
    /// </summary>
    public void ResetRuntimeBuffers()
    {
        regenBuffer = 0f;
        StopDodgeInvincibility();
        isHitFlashing = false;
        ResetFullChargeFlashState();
        ApplyTintState(CombatTintState.Default, true);
    }

    /// <summary>
    /// 当对象被禁用或销毁时，恢复闪避闪烁期间可能被关闭的 Renderer。
    /// 否则下次重新启用对象时会出现角色“半透明消失”的假象。
    /// </summary>
    public void RestoreDodgeFlickerRenderers()
    {
        if (dodgeRenderers == null || dodgeRendererDefaultEnabled == null)
        {
            return;
        }

        for (int i = 0; i < dodgeRenderers.Length; i++)
        {
            if (dodgeRenderers[i] != null)
            {
                dodgeRenderers[i].enabled = i < dodgeRendererDefaultEnabled.Length &&
                                            dodgeRendererDefaultEnabled[i];
            }
        }

        dodgeVisible = true;
    }

    private void CacheHitRenderer()
    {
        hitRenderer = presentation != null ? presentation.PrimaryRenderer : null;
        if (hitRenderer == null)
        {
            hitRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        if (hitRenderer == null)
        {
            hitMaterials = null;
            defaultColors = null;
            return;
        }

        // Renderer.materials 会为角色创建运行时材质实例，只在绑定模型时缓存一次，
        // 后续闪色直接复用，避免 Update 中反复分配数组或实例化材质。
        hitMaterials = hitRenderer.materials;
        defaultColors = new Color[hitMaterials.Length];
        for (int i = 0; i < hitMaterials.Length; i++)
        {
            defaultColors[i] = hitMaterials[i] != null
                ? hitMaterials[i].color
                : defaultHitColor;
        }

        appliedTintState = CombatTintState.Default;
    }

    private void CacheDodgeRenderers()
    {
        dodgeRenderers = GetComponentsInChildren<Renderer>(true);
        dodgeRendererDefaultEnabled = new bool[dodgeRenderers.Length];
        for (int i = 0; i < dodgeRenderers.Length; i++)
        {
            dodgeRendererDefaultEnabled[i] = dodgeRenderers[i] != null && dodgeRenderers[i].enabled;
        }
    }

    private void StartDodgeInvincibility()
    {
        isDodgeInvincible = true;
        dodgeInvincibleTimer = Mathf.Max(0.01f, dodgeInvincibleDuration);
        dodgeFlickerTimer = Mathf.Max(0.01f, dodgeFlickerInterval);
        SetDodgeVisible(false);
    }

    private void StopDodgeInvincibility()
    {
        isDodgeInvincible = false;
        dodgeInvincibleTimer = 0f;
        dodgeFlickerTimer = 0f;
        RestoreDodgeFlickerRenderers();
    }

    private void SetDodgeVisible(bool visible)
    {
        dodgeVisible = visible;
        if (dodgeRenderers == null || dodgeRendererDefaultEnabled == null)
        {
            CacheDodgeRenderers();
        }

        for (int i = 0; i < dodgeRenderers.Length; i++)
        {
            if (dodgeRenderers[i] == null)
            {
                continue;
            }

            bool enabledByDefault = i < dodgeRendererDefaultEnabled.Length && dodgeRendererDefaultEnabled[i];
            dodgeRenderers[i].enabled = visible && enabledByDefault;
        }
    }

    private void StartHitFlash()
    {
        if (hitRenderer == null || hitMaterials == null)
        {
            CacheHitRenderer();
        }

        if (hitRenderer == null || hitMaterials == null)
        {
            return;
        }

        isHitFlashing = true;
        hitFlashStartedAt = Time.time;
        ApplyTintState(CombatTintState.Hit);
    }

    /// <summary>
    /// 满蓄力只切换材质颜色，不关闭 Renderer，避免和闪避无敌的显隐闪烁产生相同语义。
    /// </summary>
    private void TickFullChargeFlash(float deltaTime)
    {
        bool isGuardActive = chargedAttack != null && chargedAttack.IsFullChargeGuardActive;
        if (!isGuardActive)
        {
            ResetFullChargeFlashState();
            return;
        }

        if (!wasFullChargeGuardActive)
        {
            wasFullChargeGuardActive = true;
            fullChargeFlashVisible = true;
            fullChargeFlashTimer = Mathf.Max(0.01f, fullChargeFlashInterval);
            return;
        }

        fullChargeFlashTimer -= Mathf.Max(0f, deltaTime);
        if (fullChargeFlashTimer > 0f)
        {
            return;
        }

        fullChargeFlashVisible = !fullChargeFlashVisible;
        fullChargeFlashTimer = Mathf.Max(0.01f, fullChargeFlashInterval);
    }

    private void ResetFullChargeFlashState()
    {
        wasFullChargeGuardActive = false;
        fullChargeFlashVisible = false;
        fullChargeFlashTimer = 0f;
    }

    private void ApplyTintState(CombatTintState tintState, bool force = false)
    {
        if (!force && appliedTintState == tintState)
        {
            return;
        }

        appliedTintState = tintState;
        if (hitMaterials == null)
        {
            return;
        }

        for (int i = 0; i < hitMaterials.Length; i++)
        {
            if (hitMaterials[i] == null)
            {
                continue;
            }

            hitMaterials[i].color = tintState == CombatTintState.Hit
                ? hitFlashColor
                : tintState == CombatTintState.FullCharge
                    ? fullChargeFlashColor
                    : defaultColors != null && i < defaultColors.Length
                        ? defaultColors[i]
                        : defaultHitColor;
        }
    }

    private void RestoreDefaultMaterialColors()
    {
        ApplyTintState(CombatTintState.Default, true);
    }

    private void OnDisable()
    {
        isHitFlashing = false;
        ResetFullChargeFlashState();
        StopDodgeInvincibility();
        RestoreDefaultMaterialColors();
    }

}
