using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家技能栏 UI：显示 1/2/3 三个技能槽的学习状态、等级、冷却和蓝量状态。
/// 注意：这里只负责显示，不负责释放技能。
/// 技能释放由 PlayerSkillCastComponent 处理。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerSkillBarUi : MonoBehaviour, IController
{
    private const int FireballSkillId = 1001;
    private const int PoisonAreaSkillId = 1002;
    private const int ScytheSpinSkillId = 2001;

    [Header("Skill Texts")]
    [SerializeField] private Text skill1Text;
    [SerializeField] private Text skill2Text;
    [SerializeField] private Text skill3Text;

    [Header("Cooldown Masks")]
    [SerializeField] private Image skill1CooldownMask;
    [SerializeField] private Image skill2CooldownMask;
    [SerializeField] private Image skill3CooldownMask;

    [Header("Slot Colors")]
    [SerializeField] private Color readyColor = Color.white;
    [SerializeField] private Color lockedColor = new Color(0.45f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color notEnoughManaColor = new Color(1f, 0.55f, 0.55f, 1f);

    public IArchitecture GetArchitecture()
    {
        return TreasureHunterArchitecture.Interface;
    }

    private void OnEnable()
    {
        this.RegisterEvent<PlayerSkillChangedEvent>(OnSkillChanged);
    }

    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerSkillChangedEvent>(OnSkillChanged);
    }

    private void Start()
    {
        RefreshAllSlots();
    }

    private void Update()
    {
        // 冷却时间每帧变化，所以第一版直接每帧刷新。
        // 后续优化时可以改成每 0.1 秒刷新一次。
        RefreshAllSlots();
    }

    private void OnSkillChanged(PlayerSkillChangedEvent e)
    {
        RefreshAllSlots();
    }

    private void RefreshAllSlots()
    {
        RefreshSlot(skill1Text, skill1CooldownMask, "1", FireballSkillId);
        RefreshSlot(skill2Text, skill2CooldownMask, "2", PoisonAreaSkillId);
        RefreshSlot(skill3Text, skill3CooldownMask, "3", ScytheSpinSkillId);
    }

    /// <summary>
    /// 刷新单个技能槽。
    /// keyName 是玩家看到的按键名，例如 1/2/3。
    /// </summary>
    private void RefreshSlot(Text targetText, Image cooldownMask, string keyName, int skillId)
    {
        if (targetText == null)
        {
            return;
        }

        ResetCooldownMask(cooldownMask);

        SkillDefine skill = GetSkillDefine(skillId);
        if (skill == null)
        {
            targetText.color = lockedColor;
            targetText.text = $"{keyName}\n无配置";
            return;
        }

        PlayerSkillModel skillModel = this.GetModel<PlayerSkillModel>();
        if (skillModel == null)
        {
            targetText.color = lockedColor;
            targetText.text = $"{keyName}\n{skill.name}\n无技能数据";
            return;
        }

        PlayerSkillRuntimeData runtimeData = skillModel.GetSkillRuntimeData(skillId);
        if (runtimeData == null)
        {
            targetText.color = lockedColor;
            targetText.text = $"{keyName}\n{skill.name}\n未学习";
            return;
        }

        SkillLevelDefine levelData = skill.GetLevelData(runtimeData.level);
        int mpCost = levelData != null ? levelData.mpCost : 0;
        bool hasEnoughMana = HasEnoughMana(mpCost);

        if (runtimeData.cooldownRemaining > 0f)
        {
            targetText.color = lockedColor;
            targetText.text =
                $"{keyName}\n{skill.name} Lv.{runtimeData.level}\n{runtimeData.cooldownRemaining:0.0}s";

            RefreshCooldownMask(cooldownMask, runtimeData, levelData);
            return;
        }

        targetText.color = hasEnoughMana ? readyColor : notEnoughManaColor;
        targetText.text = hasEnoughMana
            ? $"{keyName}\n{skill.name} Lv.{runtimeData.level}\n可释放"
            : $"{keyName}\n{skill.name} Lv.{runtimeData.level}\n蓝量不足";
    }

    private SkillDefine GetSkillDefine(int skillId)
    {
        if (SkillDataManager.Instance == null)
        {
            return null;
        }

        return SkillDataManager.Instance.GetSkill(skillId);
    }

    private bool HasEnoughMana(int mpCost)
    {
        PlayerModel playerModel = this.GetModel<PlayerModel>();
        if (playerModel == null || playerModel.Stats == null)
        {
            return false;
        }

        return playerModel.Stats.CurrentMp >= mpCost;
    }

    private void ResetCooldownMask(Image cooldownMask)
    {
        if (cooldownMask == null)
        {
            return;
        }

        cooldownMask.fillAmount = 0f;
        cooldownMask.gameObject.SetActive(false);
    }

    private void RefreshCooldownMask(
        Image cooldownMask,
        PlayerSkillRuntimeData runtimeData,
        SkillLevelDefine levelData)
    {
        if (cooldownMask == null || runtimeData == null || levelData == null || levelData.cooldown <= 0f)
        {
            return;
        }

        cooldownMask.gameObject.SetActive(true);
        cooldownMask.fillAmount = Mathf.Clamp01(runtimeData.cooldownRemaining / levelData.cooldown);
    }
}