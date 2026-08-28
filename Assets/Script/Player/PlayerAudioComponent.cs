using System;
using UnityEngine;

/// <summary>
/// 玩家音效表现组件：把移动、攻击和技能动作转换成统一的音效 Cue。
/// 组件不再保存具体 AudioClip，换音频时只修改 GameAudioCatalog，避免 Prefab 与资源强耦合。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAudioComponent : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private bool autoPlayFootstepSfx = true;
    [SerializeField] private bool autoPlayActionSfx = true;
    [SerializeField] private float walkFootstepInterval = 0.7f;
    [SerializeField] private float runFootstepInterval = 0.3f;

    private string classKey = "Warrior";

    public bool AutoPlayFootsteps => autoPlayFootstepSfx;
    public bool AutoPlayActions => autoPlayActionSfx;
    public float WalkFootstepInterval => walkFootstepInterval;
    public float RunFootstepInterval => runFootstepInterval;

    /// <summary>
    private void Awake()
    {
        if (source == null)
        {
            source = GetComponent<AudioSource>();
        }

        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 1f;
        GameSettingsService.RouteSoundsSource(source);
    }

    /// <summary>
    /// 角色生成器在职业确定后写入职业键，普通攻击便可选择匹配的武器声音。
    /// </summary>
    public void ConfigureProfile(string newClassKey)
    {
        if (!string.IsNullOrWhiteSpace(newClassKey))
        {
            classKey = newClassKey.Trim();
        }
    }

    public void PlayWalkFootstep() => Play(GameSfxId.PlayerFootstepWalk);
    public void PlayRunFootstep() => Play(GameSfxId.PlayerFootstepRun);
    public void PlayJump() => Play(GameSfxId.PlayerJump);
    public void PlayRoll() => Play(GameSfxId.PlayerRoll);
    public void PlayHit() => Play(GameSfxId.PlayerHit);

    /// <summary>
    /// 保留无参入口供旧动画事件使用；正式技能释放会传入技能 ID。
    /// </summary>
    public void PlaySkill() => PlaySkill(2001);

    public void PlaySkill(int skillId)
    {
        GameSfxId cueId = skillId switch
        {
            1001 => GameSfxId.SkillFireball,
            1002 => GameSfxId.SkillPoison,
            2001 => GameSfxId.SkillSpin,
            _ => GameSfxId.None
        };

        Play(cueId);
    }

    /// <summary>
    /// 同一个连击序号会根据职业映射到不同 Cue，战斗计算无需知道具体音频资源。
    /// </summary>
    public void PlayAttack(int comboIndex)
    {
        Play(ResolveAttackCue(comboIndex));
    }

    private GameSfxId ResolveAttackCue(int comboIndex)
    {
        int combo = Mathf.Clamp(comboIndex, 1, 3);
        if (string.Equals(classKey, "Assassin", StringComparison.OrdinalIgnoreCase))
        {
            return combo == 1 ? GameSfxId.PlayerAttackAssassin1
                : combo == 2 ? GameSfxId.PlayerAttackAssassin2
                : GameSfxId.PlayerAttackAssassin3;
        }

        if (string.Equals(classKey, "Archer", StringComparison.OrdinalIgnoreCase))
        {
            return GameSfxId.PlayerAttackArcher;
        }

        if (string.Equals(classKey, "Wizard", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(classKey, "Mage", StringComparison.OrdinalIgnoreCase))
        {
            return GameSfxId.PlayerAttackWizard;
        }

        return combo == 1 ? GameSfxId.PlayerAttackWarrior1
            : combo == 2 ? GameSfxId.PlayerAttackWarrior2
            : GameSfxId.PlayerAttackWarrior3;
    }

    private bool Play(GameSfxId cueId)
    {
        return cueId != GameSfxId.None && GameAudioService.PlayOn(cueId, source);
    }
}
