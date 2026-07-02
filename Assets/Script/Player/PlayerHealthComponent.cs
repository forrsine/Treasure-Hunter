using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealthComponent : MonoBehaviour
{
    private PlayerCo owner;
    private SkinnedMeshRenderer hitRenderer;
    private Color[] defaultColors;
    private Renderer[] dodgeFlickerRenderers;
    private bool[] dodgeFlickerRendererDefaultEnabled;

    private bool isColorChange;
    private float changeTime;
    private bool isDodgeInvincible;
    private float dodgeInvincibleTimer;
    private float dodgeFlickerTimer;
    private bool dodgeFlickerVisible = true;
    private float regenBuffer;

    public void Initialize(PlayerCo player)
    {
        owner = player;
        CacheHitEffectRenderer();
        CacheDodgeFlickerRenderers();
    }

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
            SetDodgeFlickerVisible(!dodgeFlickerVisible);
            dodgeFlickerTimer = Mathf.Max(0.01f, owner.DodgeFlickerInterval);
        }

        if (dodgeInvincibleTimer <= 0f)
        {
            StopDodgeInvincibility();
        }
    }

    public void TickHitFlash()
    {
        if (hitRenderer == null || !isColorChange)
        {
            return;
        }

        if (Time.time - changeTime < owner.HitColorTime)
        {
            return;
        }

        isColorChange = false;
        Material[] materials = hitRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Color targetColor = defaultColors != null && i < defaultColors.Length
                ? defaultColors[i]
                : owner.DefaultHitColor;

            materials[i].color = targetColor;
        }
    }

    public void ApplyHealthRegen()
    {
        if (owner.HealthRegenPerSecond <= 0f)
        {
            return;
        }

        if (owner.Hp >= owner.Hpmax)
        {
            regenBuffer = 0f;
            return;
        }

        regenBuffer += owner.HealthRegenPerSecond * Time.deltaTime;
        int recoverAmount = Mathf.FloorToInt(regenBuffer);
        if (recoverAmount <= 0)
        {
            return;
        }

        regenBuffer -= recoverAmount;
        RecoverHp(recoverAmount, showFloatingText: true);
    }

    public void Hit(int incomingAttackPower)
    {
        if (isDodgeInvincible)
        {
            return;
        }

        if (TryDodgeIncomingHit())
        {
            StartDodgeInvincibility();
            return;
        }

        int finalDamage = ApplyDamageReduction(incomingAttackPower);
        if (finalDamage <= 0)
        {
            return;
        }

        int hpBeforeHit = owner.Hp;
        owner.Hp -= finalDamage;

        int actualDamageTaken = Mathf.Max(0, hpBeforeHit - Mathf.Max(0, owner.Hp));
        FloatingCombatText.ShowTakenDamage(transform, actualDamageTaken);

        if (owner.AutoPlayActionSfx)
        {
            owner.PlayHitSfxEvent();
        }

        if (owner.Hp <= 0)
        {
            owner.Hp = 0;
            ShowGameOver();
        }

        HitColorChange();
        owner.UpdateHpUI();
        owner.NotifyStatsChanged();
    }

    public void RecoverHp(int amount, bool showFloatingText = false)
    {
        if (amount <= 0 || owner.Hp >= owner.Hpmax)
        {
            return;
        }

        int hpBeforeRecover = owner.Hp;
        owner.Hp = Mathf.Min(owner.Hp + amount, owner.Hpmax);
        int actualRecoverAmount = Mathf.Max(0, owner.Hp - hpBeforeRecover);

        if (showFloatingText)
        {
            FloatingCombatText.ShowHealing(transform, actualRecoverAmount);
        }

        owner.UpdateHpUI();
        owner.NotifyStatsChanged();
    }

    public void FullHeal()
    {
        owner.Hp = owner.Hpmax;
        owner.UpdateHpUI();
        owner.NotifyStatsChanged();
    }

    public void RestoreDodgeFlickerRenderers()
    {
        dodgeFlickerVisible = true;

        if (dodgeFlickerRenderers == null || dodgeFlickerRendererDefaultEnabled == null)
        {
            return;
        }

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            bool defaultEnabled = i < dodgeFlickerRendererDefaultEnabled.Length &&
                                  dodgeFlickerRendererDefaultEnabled[i];
            currentRenderer.enabled = defaultEnabled;
        }
    }

    public void ResetRuntimeBuffers()
    {
        regenBuffer = 0f;
    }

    private void CacheHitEffectRenderer()
    {
        hitRenderer = owner.myRenderer;
        if (hitRenderer == null)
        {
            hitRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            owner.myRenderer = hitRenderer;
        }

        if (hitRenderer == null)
        {
            Debug.LogWarning("PlayerHealthComponent could not find a SkinnedMeshRenderer for hit feedback.", this);
            return;
        }

        Material[] materials = hitRenderer.materials;
        defaultColors = new Color[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            defaultColors[i] = materials[i].color;
        }
    }

    private void CacheDodgeFlickerRenderers()
    {
        dodgeFlickerRenderers = GetComponentsInChildren<Renderer>(true);
        dodgeFlickerRendererDefaultEnabled = new bool[dodgeFlickerRenderers.Length];

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            dodgeFlickerRendererDefaultEnabled[i] = currentRenderer != null && currentRenderer.enabled;
        }
    }

    private bool TryDodgeIncomingHit()
    {
        return owner.DodgeChance > 0f && UnityEngine.Random.value < owner.DodgeChance;
    }

    private void StartDodgeInvincibility()
    {
        FloatingCombatText.ShowMiss(transform);

        isDodgeInvincible = true;
        dodgeInvincibleTimer = Mathf.Max(0.01f, owner.DodgeInvincibleDuration);
        dodgeFlickerTimer = Mathf.Max(0.01f, owner.DodgeFlickerInterval);
        SetDodgeFlickerVisible(false);
    }

    private void StopDodgeInvincibility()
    {
        isDodgeInvincible = false;
        dodgeInvincibleTimer = 0f;
        dodgeFlickerTimer = 0f;
        RestoreDodgeFlickerRenderers();
    }

    private void SetDodgeFlickerVisible(bool visible)
    {
        dodgeFlickerVisible = visible;

        if (dodgeFlickerRenderers == null || dodgeFlickerRendererDefaultEnabled == null)
        {
            CacheDodgeFlickerRenderers();
        }

        for (int i = 0; i < dodgeFlickerRenderers.Length; i++)
        {
            Renderer currentRenderer = dodgeFlickerRenderers[i];
            if (currentRenderer == null)
            {
                continue;
            }

            bool defaultEnabled = i < dodgeFlickerRendererDefaultEnabled.Length &&
                                  dodgeFlickerRendererDefaultEnabled[i];
            currentRenderer.enabled = visible && defaultEnabled;
        }
    }

    private int ApplyDamageReduction(int incomingAttackPower)
    {
        int rawDamage = Mathf.Max(0, incomingAttackPower);
        if (rawDamage <= 0)
        {
            return 0;
        }

        float reduction = Mathf.Clamp01(owner.DamageReduction);
        return Mathf.Max(1, Mathf.FloorToInt(rawDamage * (1f - reduction)));
    }

    private void HitColorChange()
    {
        if (hitRenderer == null)
        {
            return;
        }

        isColorChange = true;
        changeTime = Time.time;

        Material[] materials = hitRenderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i].color = owner.HitFlashColor;
        }
    }

    private void ShowGameOver()
    {
        GameSessionUi sessionUi = owner.GetComponent<GameSessionUi>();
        if (sessionUi != null)
        {
            sessionUi.ShowGameOver();
        }
        else if (owner.ReStartPanel != null)
        {
            owner.ReStartPanel.SetActive(true);
        }
    }
}
