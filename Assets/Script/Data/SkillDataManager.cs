using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 技能配置管理器：负责读取 SkillDefine.json，并提供按 skillId 查询技能配置的入口。
/// 注意：这个类只负责“查表”，不负责释放技能、不扣蓝、不计算冷却。
/// 这样可以让配置读取和战斗逻辑分开，后续更容易扩展和接服务器。
/// </summary>
public class SkillDataManager : MonoBehaviour
{
    /// <summary>
    /// 全局访问入口。
    /// 后续其他系统可以通过 SkillDataManager.Instance.GetSkill(skillId) 查询技能。
    /// </summary>
    public static SkillDataManager Instance { get; private set; }

    /// <summary>
    /// 技能配置列表。
    /// 用 List 是为了方便调试时查看所有技能。
    /// </summary>
    public List<SkillDefine> Skills { get; private set; } = new List<SkillDefine>();

    /// <summary>
    /// 技能字典索引。
    /// key 是 skillId，value 是技能配置。
    /// 用字典可以做到 O(1) 查询，比每次遍历 List 更适合战斗中频繁查询。
    /// </summary>
    private readonly Dictionary<int, SkillDefine> skillMap = new Dictionary<int, SkillDefine>();

    /// <summary>
    /// 初始化技能配置管理器。
    /// 如果场景中重复出现 SkillDataManager，就销毁多余的，避免加载两份配置。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 技能配置属于全局静态数据，后续从登录场景切到主场景时也可以继续使用。
        DontDestroyOnLoad(gameObject);

        LoadSkillDefine();
    }

    /// <summary>
    /// 从 Resources/Data/SkillDefine.json 读取技能配置。
    /// Resources.Load 不需要写 .json 后缀，路径从 Resources 文件夹内部开始。
    /// </summary>
    private void LoadSkillDefine()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/SkillDefine");

        if (jsonAsset == null)
        {
            Debug.LogError("没有找到技能配置表：Resources/Data/SkillDefine.json");
            return;
        }

        SkillDefineTable table = JsonUtility.FromJson<SkillDefineTable>(jsonAsset.text);

        if (table == null || table.skills == null)
        {
            Debug.LogError("技能配置表格式错误，请检查 SkillDefine.json 的根节点是否包含 skills 数组。");
            return;
        }

        List<SkillDefine> loadedSkills = table.skills;
        Skills = new List<SkillDefine>(loadedSkills.Count);
        skillMap.Clear();

        for (int i = 0; i < loadedSkills.Count; i++)
        {
            SkillDefine skill = loadedSkills[i];

            if (skill == null)
            {
                Debug.LogError($"技能配置存在空项，索引：{i}");
                continue;
            }

            if (skill.skillId <= 0)
            {
                Debug.LogError($"技能配置存在非法 skillId，索引：{i}");
                continue;
            }

            if (!TryValidateSkill(skill, out string validationError))
            {
                Debug.LogError($"技能配置无效，skillId = {skill.skillId}：{validationError}");
                continue;
            }

            if (skillMap.ContainsKey(skill.skillId))
            {
                Debug.LogError($"技能配置存在重复 skillId：{skill.skillId}");
                continue;
            }

            skillMap.Add(skill.skillId, skill);
            Skills.Add(skill);
        }

        Debug.Log($"技能配置表加载完成，共 {skillMap.Count} 个技能。");

        // 临时测试读取，确认第一步是否成功。
        // 以后正式接入技能系统后，可以删掉这行测试日志。
        SkillDefine testSkill = GetSkill(1001);
        if (testSkill != null)
        {
            Debug.Log($"测试读取技能：{testSkill.name}");
        }
    }

    /// <summary>
    /// 校验单个技能配置。
    /// 配置错误时不让技能进入运行时技能池，避免非法类型被当成其他技能释放，
    /// 也避免缺少等级数据后已经扣蓝、进入冷却却无法执行表现。
    /// </summary>
    public static bool TryValidateSkill(SkillDefine skill, out string error)
    {
        if (skill == null)
        {
            error = "技能对象为空。";
            return false;
        }

        if (!skill.TryGetSkillType(out SkillType skillType))
        {
            error = $"skillType 非法：{skill.skillType}";
            return false;
        }

        if (skill.maxLevel <= 0)
        {
            error = "maxLevel 必须大于 0。";
            return false;
        }

        if (skill.levels == null || skill.levels.Count != skill.maxLevel)
        {
            int levelCount = skill.levels != null ? skill.levels.Count : 0;
            error = $"等级配置数量必须等于 maxLevel，当前数量：{levelCount}，maxLevel：{skill.maxLevel}。";
            return false;
        }

        HashSet<int> levelSet = new HashSet<int>();
        for (int i = 0; i < skill.levels.Count; i++)
        {
            SkillLevelDefine levelData = skill.levels[i];
            if (levelData == null)
            {
                error = $"第 {i} 个等级配置为空。";
                return false;
            }

            if (levelData.level < 1 || levelData.level > skill.maxLevel || !levelSet.Add(levelData.level))
            {
                error = $"等级必须在 1 到 {skill.maxLevel} 之间且不能重复，当前值：{levelData.level}。";
                return false;
            }

            if (levelData.mpCost < 0 ||
                levelData.cooldown < 0f ||
                levelData.damageRate <= 0f ||
                levelData.radius < 0f ||
                levelData.duration < 0f ||
                levelData.tickInterval < 0f ||
                levelData.slowRate < 0f ||
                levelData.slowRate > 1f)
            {
                error = $"Lv.{levelData.level} 存在负数、零伤害倍率或非法减速比例。";
                return false;
            }

            if (skillType == SkillType.AreaDot &&
                (levelData.duration <= 0f || levelData.tickInterval <= 0f))
            {
                error = $"持续范围技能 Lv.{levelData.level} 的 duration 和 tickInterval 必须大于 0。";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// 根据 skillId 获取技能配置。
    /// 找不到时返回 null，并输出错误日志，方便你检查配置或调用代码。
    /// </summary>
    public SkillDefine GetSkill(int skillId)
    {
        if (skillMap.TryGetValue(skillId, out SkillDefine skill))
        {
            return skill;
        }

        Debug.LogError($"没有找到技能配置：skillId = {skillId}");
        return null;
    }

    /// <summary>
    /// 根据 skillId 和技能等级获取某一级的技能数值。
    /// 后续释放技能时，会用这个方法读取蓝耗、冷却、伤害倍率等。
    /// </summary>
    public SkillLevelDefine GetSkillLevel(int skillId, int level)
    {
        SkillDefine skill = GetSkill(skillId);

        if (skill == null)
        {
            return null;
        }

        SkillLevelDefine levelData = skill.GetLevelData(level);

        if (levelData == null)
        {
            Debug.LogError($"没有找到技能等级配置：skillId = {skillId}, level = {level}");
        }

        return levelData;
    }

    /// <summary>
    /// 获取所有技能配置。
    /// 后续技能三选一面板会用它筛选“当前角色可学习或可升级的技能”。
    /// </summary>
    public List<SkillDefine> GetAllSkills()
    {
        return Skills;
    }
}
