using UnityEngine;

/// <summary>
/// 玩家音效表现组件：只负责播放脚步、跳跃、翻滚、攻击和受击音效。
/// 音效资源与战斗规则分离后，更换职业或音频方案不会影响伤害计算。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAudioComponent : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private bool autoPlayFootstepSfx = true;
    [SerializeField] private bool autoPlayActionSfx = true;
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] runFootstepClips;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip rollClip;
    [SerializeField] private AudioClip attack1Clip;
    [SerializeField] private AudioClip attack2Clip;
    [SerializeField] private AudioClip attack3Clip;
    [SerializeField] private AudioClip skillClip;
    [SerializeField] private AudioClip hitClip;
    [SerializeField] private float walkFootstepInterval = 0.7f;
    [SerializeField] private float runFootstepInterval = 0.3f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float rollVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float skillVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float hitVolume = 1f;

    public bool AutoPlayFootsteps => autoPlayFootstepSfx;
    public bool AutoPlayActions => autoPlayActionSfx;
    public float WalkFootstepInterval => walkFootstepInterval;
    public float RunFootstepInterval => runFootstepInterval;

    /// <summary>
    /// 缓存或补齐 AudioSource。
    /// 组件允许 Prefab 手动配置，也允许在漏挂时自动补一个最基础的播放源。
    /// </summary>
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
    }

    /// <summary>
    /// 播放走路脚步声。
    /// </summary>
    public void PlayWalkFootstep() => PlayRandom(walkFootstepClips, footstepVolume);

    /// <summary>
    /// 播放跑步脚步声。
    /// 如果没有单独的跑步音效，就回退到走路脚步声，避免完全静音。
    /// </summary>
    public void PlayRunFootstep()
    {
        if (!PlayRandom(runFootstepClips, footstepVolume))
        {
            PlayRandom(walkFootstepClips, footstepVolume);
        }
    }

    public void PlayJump() => Play(jumpClip, jumpVolume);
    public void PlayRoll() => Play(rollClip, rollVolume);
    public void PlaySkill() => Play(skillClip, skillVolume);
    public void PlayHit() => Play(hitClip, hitVolume);

    /// <summary>
    /// 根据当前连击段数选择攻击音效。
    /// </summary>
    public void PlayAttack(int comboIndex)
    {
        AudioClip clip = comboIndex <= 1 ? attack1Clip : comboIndex == 2 ? attack2Clip : attack3Clip;
        Play(clip, attackVolume);
    }

    /// <summary>
    /// 从一组可选音效里随机播放一个有效片段。
    /// 这样同一种脚步声不会每次都完全重复，听感更自然。
    /// </summary>
    private bool PlayRandom(AudioClip[] clips, float volume)
    {
        if (clips == null || clips.Length == 0)
        {
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return false;
        }

        int selected = Random.Range(0, validCount);
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (selected-- == 0)
            {
                return Play(clips[i], volume);
            }
        }

        return false;
    }

    /// <summary>
    /// 最底层播放入口。
    /// </summary>
    private bool Play(AudioClip clip, float volume)
    {
        if (clip == null || source == null)
        {
            return false;
        }

        source.PlayOneShot(clip, volume);
        return true;
    }
}
