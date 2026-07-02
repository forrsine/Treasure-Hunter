using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCombatComponent : MonoBehaviour
{
    private const int MaxCombo = 3;

    private PlayerCo owner;
    private Animator animator;

    private int currentCombo;
    private float currentTimer;
    private float currentComboTimer;
    private bool isAttacking;
    private bool canComboNext;
    private float lifeStealBuffer;

    public bool IsAttacking => isAttacking;
    public int CurrentCombo => currentCombo;

    public void Initialize(PlayerCo player)
    {
        owner = player;
        animator = owner.PlayerAnimator;
    }

    public void Tick()
    {
        if (isAttacking)
        {
            currentTimer -= Time.deltaTime;
            if (currentTimer <= 0f)
            {
                ResetCombo();
            }
        }

        UpdateComboTimer();
        CheckAttackInput();
    }

    public void OpenComboWindow()
    {
        canComboNext = true;
        currentComboTimer = owner.ComboWindowTime;
    }

    public void ResetCombo()
    {
        currentCombo = 0;
        isAttacking = false;
        canComboNext = false;
        currentTimer = 0f;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", 0);
        }
    }

    public void WeaponEnable()
    {
        if (owner.PlayerWeaponCollider != null)
        {
            owner.PlayerWeaponCollider.enabled = true;
        }

        if (owner.AutoPlayActionSfx)
        {
            owner.PlayComboAttackSfx(currentCombo);
        }
    }

    public void WeaponDisable()
    {
        if (owner.PlayerWeaponCollider != null)
        {
            owner.PlayerWeaponCollider.enabled = false;
        }
    }

    public int RollAttackDamage(out bool isCritical)
    {
        int damage = Mathf.Max(1, owner.AtkPower);
        isCritical = UnityEngine.Random.value < owner.CritChance;

        if (isCritical)
        {
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * owner.CritDamageMultiplier));
        }

        return damage;
    }

    public int HandleDamageDealt(int appliedDamage)
    {
        if (owner.LifeSteal <= 0f || appliedDamage <= 0)
        {
            return 0;
        }

        lifeStealBuffer += appliedDamage * owner.LifeSteal;
        int healAmount = Mathf.FloorToInt(lifeStealBuffer);
        if (healAmount <= 0)
        {
            return 0;
        }

        int actualHealAmount = Mathf.Min(healAmount, Mathf.Max(0, owner.Hpmax - owner.Hp));
        lifeStealBuffer -= healAmount;
        if (actualHealAmount <= 0)
        {
            return 0;
        }

        owner.RecoverHp(actualHealAmount, showFloatingText: true);
        return actualHealAmount;
    }

    public void ResetRuntimeBuffers()
    {
        lifeStealBuffer = 0f;
    }

    private void CheckAttackInput()
    {
        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || !input.LeftMouseDown)
        {
            return;
        }

        if (currentCombo == 0 && !isAttacking)
        {
            StartFirstAttack();
            return;
        }

        if (canComboNext && currentCombo < MaxCombo)
        {
            TriggerNextCombo();
        }
    }

    private void StartFirstAttack()
    {
        isAttacking = true;
        currentCombo = 1;
        currentTimer = owner.FullAttackTimeout;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", currentCombo);
        }
    }

    private void TriggerNextCombo()
    {
        currentCombo++;
        canComboNext = false;
        currentTimer = owner.FullAttackTimeout;

        if (animator != null)
        {
            animator.SetInteger("ComboIndex", currentCombo);
        }
    }

    private void UpdateComboTimer()
    {
        if (!canComboNext)
        {
            return;
        }

        currentComboTimer -= Time.deltaTime;
        if (currentComboTimer <= 0f)
        {
            ResetCombo();
        }
    }
}
