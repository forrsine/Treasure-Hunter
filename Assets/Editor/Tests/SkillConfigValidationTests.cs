#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 技能配置回归测试：保护 20 级伤害曲线、Lv.3 非伤害数值封顶和非法类型拦截。
/// 这些规则一旦被配置工具或手工改表破坏，测试应在进入玩法场景前直接失败。
/// </summary>
public sealed class SkillConfigValidationTests
{
    private const float DamageGrowthRate = 1.1f;
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
    public void SkillConfig_HasTwentyContinuousAndValidLevels()
    {
        Assert.That(table, Is.Not.Null);
        Assert.That(table.skills, Is.Not.Null.And.Not.Empty);

        for (int i = 0; i < table.skills.Count; i++)
        {
            SkillDefine skill = table.skills[i];
            Assert.That(skill.maxLevel, Is.EqualTo(20), $"技能 {skill.skillId} 的最高等级不是 20。");
            Assert.That(skill.levels, Has.Count.EqualTo(20), $"技能 {skill.skillId} 的等级配置数量不完整。");
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
    public void LevelsAfterThree_GrowDamageAndKeepOtherValuesAtLevelThree()
    {
        for (int i = 0; i < table.skills.Count; i++)
        {
            SkillDefine skill = table.skills[i];
            SkillLevelDefine levelThree = skill.GetLevelData(3);
            Assert.That(levelThree, Is.Not.Null, $"技能 {skill.skillId} 缺少 Lv.3 配置。");

            for (int level = 4; level <= skill.maxLevel; level++)
            {
                SkillLevelDefine levelData = skill.GetLevelData(level);
                float expectedDamage = Mathf.Round(
                    levelThree.damageRate * Mathf.Pow(DamageGrowthRate, level - 3) * 1000f) / 1000f;

                Assert.That(levelData.damageRate, Is.EqualTo(expectedDamage).Within(FloatTolerance));
                Assert.That(levelData.mpCost, Is.EqualTo(levelThree.mpCost));
                Assert.That(levelData.cooldown, Is.EqualTo(levelThree.cooldown).Within(FloatTolerance));
                Assert.That(levelData.radius, Is.EqualTo(levelThree.radius).Within(FloatTolerance));
                Assert.That(levelData.duration, Is.EqualTo(levelThree.duration).Within(FloatTolerance));
                Assert.That(levelData.tickInterval, Is.EqualTo(levelThree.tickInterval).Within(FloatTolerance));
                Assert.That(levelData.slowRate, Is.EqualTo(levelThree.slowRate).Within(FloatTolerance));
            }
        }
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
}
#endif
