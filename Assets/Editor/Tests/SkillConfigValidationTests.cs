#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 技能配置回归测试：保护策划案批准的四级技能曲线和非法类型拦截。
/// 这些规则一旦被配置工具或手工改表破坏，测试应在进入玩法场景前直接失败。
/// </summary>
public sealed class SkillConfigValidationTests
{
    private const float FloatTolerance = 0.0005f;

    private SkillDefineTable table;

    [SetUp]
    public void SetUp()
    {
        string configPath = Path.Combine(Application.dataPath, "Resources/Data/SkillDefine.json");
        string json = File.ReadAllText(configPath);
        table = JsonUtility.FromJson<SkillDefineTable>(json);
    }

    [Test]
    public void SkillConfig_HasFourContinuousAndValidLevels()
    {
        Assert.That(table, Is.Not.Null);
        Assert.That(table.skills, Is.Not.Null.And.Not.Empty);

        for (int i = 0; i < table.skills.Count; i++)
        {
            SkillDefine skill = table.skills[i];
            Assert.That(skill.maxLevel, Is.EqualTo(4), $"技能 {skill.skillId} 的最高等级不是 4。");
            Assert.That(skill.levels, Has.Count.EqualTo(4), $"技能 {skill.skillId} 的等级配置数量不完整。");
            Assert.That(
                SkillDataManager.TryValidateSkill(skill, out string error),
                Is.True,
                $"技能 {skill.skillId} 配置校验失败：{error}");

            for (int level = 1; level <= skill.maxLevel; level++)
            {
                Assert.That(
                    skill.GetLevelData(level),
                    Is.Not.Null,
                    $"技能 {skill.skillId} 缺少 Lv.{level} 配置。");
            }
        }
    }

    [Test]
    public void ApprovedSkillBalance_MatchesVersionThreeDesign()
    {
        SkillDefine fireball = table.skills.Find(skill => skill.skillId == 1001);
        AssertSkillLevel(fireball, 1, 25, 5f, 1.8f, 2.6f, 0f, 0f);
        AssertSkillLevel(fireball, 4, 33, 4.1f, 3f, 3.5f, 0f, 0f);

        SkillDefine poison = table.skills.Find(skill => skill.skillId == 1002);
        AssertSkillLevel(poison, 1, 30, 9f, 0.35f, 3.2f, 5f, 0.25f);
        AssertSkillLevel(poison, 4, 39, 8.1f, 0.65f, 4.1f, 5f, 0.4f);

        SkillDefine scythe = table.skills.Find(skill => skill.skillId == 2001);
        AssertSkillLevel(scythe, 1, 32, 7f, 1.9f, 2.8f, 0f, 0f);
        AssertSkillLevel(scythe, 4, 41, 5.8f, 3.1f, 3.7f, 0f, 0f);
    }

    [Test]
    public void InvalidSkillType_IsRejectedInsteadOfFallingBackToSelfAoe()
    {
        SkillDefine invalidSkill = new SkillDefine
        {
            skillId = 9999,
            skillType = "SelfAOE_Typo",
            maxLevel = 1,
            levels = new System.Collections.Generic.List<SkillLevelDefine>
            {
                new SkillLevelDefine
                {
                    level = 1,
                    mpCost = 10,
                    cooldown = 1f,
                    damageRate = 1f,
                    radius = 1f
                }
            }
        };

        Assert.That(invalidSkill.GetSkillType(), Is.EqualTo(SkillType.Invalid));
        Assert.That(SkillDataManager.TryValidateSkill(invalidSkill, out string error), Is.False);
        StringAssert.Contains("skillType", error);
    }

    private static void AssertSkillLevel(
        SkillDefine skill,
        int level,
        int mpCost,
        float cooldown,
        float damageRate,
        float radius,
        float duration,
        float slowRate)
    {
        Assert.That(skill, Is.Not.Null);
        SkillLevelDefine data = skill.GetLevelData(level);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.mpCost, Is.EqualTo(mpCost));
        Assert.That(data.cooldown, Is.EqualTo(cooldown).Within(FloatTolerance));
        Assert.That(data.damageRate, Is.EqualTo(damageRate).Within(FloatTolerance));
        Assert.That(data.radius, Is.EqualTo(radius).Within(FloatTolerance));
        Assert.That(data.duration, Is.EqualTo(duration).Within(FloatTolerance));
        Assert.That(data.slowRate, Is.EqualTo(slowRate).Within(FloatTolerance));
    }
}
#endif
