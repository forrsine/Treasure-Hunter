#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEngine;

/// <summary>
/// 开发者模式专项测试：保护临时作弊状态、正式进度入口和宝箱本轮补足规则。
/// </summary>
public sealed class DeveloperModeSystemTests
{
    private const int TestSkillId = 9001;
    private IArchitecture architecture;
    private GameObject skillManagerObject;
    private SkillDataManager previousSkillManager;

    [SetUp]
    public void SetUp()
    {
        architecture = TreasureHunterArchitecture.Interface;
        architecture.SendCommand(new ResetDeveloperModeCommand());
        architecture.GetSystem<EconomySystem>().Reset();
        BossRunProgressState.ResetRun();
        CreateTestSkillManager();
        InitializePlayer();
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.SendCommand(new ResetDeveloperModeCommand());
        architecture?.Deinit();
        architecture = null;
        BossRunProgressState.ResetRun();

        SetSkillDataManagerInstance(previousSkillManager);
        if (skillManagerObject != null)
        {
            Object.DestroyImmediate(skillManagerObject);
            skillManagerObject = null;
        }
    }

    [Test]
    public void HighAttack_IsCalculatedAtDamageTimeWithoutChangingBaseStats()
    {
        PlayerStatsSnapshot before = architecture.SendQuery(new GetPlayerStatsQuery());
        Assert.That(architecture.SendQuery(new GetEffectivePlayerAttackPowerQuery()), Is.EqualTo(before.AttackPower));

        Assert.That(architecture.SendCommand(new ToggleDeveloperHighAttackCommand()), Is.True);

        Assert.That(
            architecture.SendQuery(new GetEffectivePlayerAttackPowerQuery()),
            Is.EqualTo(before.AttackPower + DeveloperModeSystem.HighAttackBonus));
        Assert.That(
            architecture.SendQuery(new GetPlayerStatsQuery()).AttackPower,
            Is.EqualTo(before.AttackPower),
            "开发者攻击加成不应写回角色基础属性。 ");

        architecture.SendCommand(new ResetDeveloperModeCommand());
        Assert.That(architecture.SendQuery(new GetEffectivePlayerAttackPowerQuery()), Is.EqualTo(before.AttackPower));
    }

    [Test]
    public void Invincibility_BlocksFormalDamageAndResetRestoresDamage()
    {
        int hpBefore = architecture.SendQuery(new GetPlayerStatsQuery()).CurrentHp;
        Assert.That(architecture.SendCommand(new ToggleDeveloperInvincibilityCommand()), Is.True);

        PlayerDamageResult blocked = architecture.SendCommand(new TakePlayerDamageCommand(25, false));

        Assert.That(blocked.ActualDamage, Is.Zero);
        Assert.That(architecture.SendQuery(new GetPlayerStatsQuery()).CurrentHp, Is.EqualTo(hpBefore));

        architecture.SendCommand(new ResetDeveloperModeCommand());
        PlayerDamageResult applied = architecture.SendCommand(new TakePlayerDamageCommand(25, false));
        Assert.That(applied.ActualDamage, Is.GreaterThan(0));
        Assert.That(architecture.SendQuery(new GetPlayerStatsQuery()).CurrentHp, Is.LessThan(hpBefore));
    }

    [Test]
    public void ZeroCooldown_ClearsExistingCooldownAndStopsTickUntilDisabled()
    {
        PlayerSkillRuntimeData runtimeData = AddTestSkillRuntimeData(TestSkillId);
        runtimeData.StartCooldown(5f);

        Assert.That(architecture.SendCommand(new ToggleDeveloperZeroCooldownCommand()), Is.True);
        Assert.That(runtimeData.cooldownRemaining, Is.Zero);

        PlayerSkillSystem skillSystem = architecture.GetSystem<PlayerSkillSystem>();
        int manaBeforeCast = architecture.SendQuery(new GetPlayerStatsQuery()).CurrentMp;
        Assert.That(skillSystem.TryCastSkill(TestSkillId), Is.True);
        Assert.That(runtimeData.cooldownRemaining, Is.Zero, "零冷却开启时释放技能不应写入新 CD。 ");
        Assert.That(
            architecture.SendQuery(new GetPlayerStatsQuery()).CurrentMp,
            Is.EqualTo(manaBeforeCast - 5),
            "零冷却不等于无限蓝，技能仍应正常消耗魔法。 ");

        Assert.That(architecture.SendCommand(new ToggleDeveloperZeroCooldownCommand()), Is.False);
        Assert.That(skillSystem.TryCastSkill(TestSkillId), Is.True);
        Assert.That(runtimeData.cooldownRemaining, Is.EqualTo(4f));
    }

    [Test]
    public void ProgressHotkeys_UseFormalGoldLevelAndManaSystems()
    {
        Assert.That(architecture.SendCommand(new AddGoldCommand(10_000L)), Is.EqualTo(10_000L));
        Assert.That(architecture.SendQuery(new GetGoldQuery()), Is.EqualTo(10_000L));

        int levelBefore = architecture.SendQuery(new GetPlayerStatsQuery()).Level;
        Assert.That(architecture.SendCommand(new AddPlayerLevelsForDevelopmentCommand(1)), Is.EqualTo(1));
        Assert.That(architecture.SendQuery(new GetPlayerStatsQuery()).Level, Is.EqualTo(levelBefore + 1));

        Assert.That(architecture.SendCommand(new TrySpendPlayerManaCommand(20)), Is.True);
        int restored = architecture.SendCommand(new FullRestorePlayerManaCommand());
        PlayerStatsSnapshot stats = architecture.SendQuery(new GetPlayerStatsQuery());
        Assert.That(restored, Is.EqualTo(20));
        Assert.That(stats.CurrentMp, Is.EqualTo(stats.MaxMp));
    }

    [TestCase(0, 5)]
    [TestCase(2, 3)]
    [TestCase(4, 1)]
    public void CompleteVaultCycle_OnlyAddsRemainingBreaks(int startingBreaks, int expectedAddedBreaks)
    {
        GameObject vaultObject = new GameObject($"DeveloperVault_{startingBreaks}");
        vaultObject.AddComponent<BoxCollider>();
        Rigidbody rigidbody = vaultObject.AddComponent<Rigidbody>();
        rigidbody.useGravity = false;
        rigidbody.isKinematic = true;
        BoxCo vault = vaultObject.AddComponent<BoxCo>();
        BossRunProgressState.RestorePersistentProgress(startingBreaks, 0);
        vault.RestoreProgress(startingBreaks);
        BoxCo.OnVaultDestroyed += BossRunProgressState.RecordVaultDestroyed;

        try
        {
            int requestedBreaks = BossRunProgressState.VaultsUntilNextBoss;
            int completedBreaks = vault.BreakRepeatedlyForDevelopment(requestedBreaks);

            Assert.That(completedBreaks, Is.EqualTo(expectedAddedBreaks));
            Assert.That(vault.DestroyedCount, Is.EqualTo(5));
            Assert.That(BossRunProgressState.TotalVaultDestroyedCount, Is.EqualTo(5));
            Assert.That(BossRunProgressState.VaultsUntilNextBoss, Is.Zero);
            Assert.That(vault.IsRespawning, Is.False);
            Assert.That(vault.IsInvincible, Is.False);
            Assert.That(vault.CurrentHp, Is.EqualTo(vault.MaxHp));
        }
        finally
        {
            BoxCo.OnVaultDestroyed -= BossRunProgressState.RecordVaultDestroyed;
            Object.DestroyImmediate(vaultObject);
        }
    }

    private PlayerSkillRuntimeData AddTestSkillRuntimeData(int skillId)
    {
        PlayerSkillModel skillModel = architecture.GetModel<PlayerSkillModel>();
        FieldInfo mapField = typeof(PlayerSkillModel).GetField(
            "learnedSkillMap",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(mapField, Is.Not.Null);

        var map = mapField.GetValue(skillModel) as Dictionary<int, PlayerSkillRuntimeData>;
        Assert.That(map, Is.Not.Null);

        var runtimeData = new PlayerSkillRuntimeData(skillId, 1);
        map.Add(skillId, runtimeData);
        return runtimeData;
    }

    private void CreateTestSkillManager()
    {
        previousSkillManager = SkillDataManager.Instance;
        skillManagerObject = new GameObject("DeveloperModeTestSkillManager");
        SkillDataManager manager = skillManagerObject.AddComponent<SkillDataManager>();
        SetSkillDataManagerInstance(manager);

        FieldInfo mapField = typeof(SkillDataManager).GetField(
            "skillMap",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(mapField, Is.Not.Null);
        var map = mapField.GetValue(manager) as Dictionary<int, SkillDefine>;
        Assert.That(map, Is.Not.Null);

        var skill = new SkillDefine
        {
            skillId = TestSkillId,
            skillKey = "developer_mode_test_skill",
            name = "开发者测试技能",
            skillType = nameof(SkillType.ProjectileAoe),
            isCommon = true,
            maxLevel = 1,
            unlockLevel = 1,
            levels = new List<SkillLevelDefine>
            {
                new SkillLevelDefine
                {
                    level = 1,
                    mpCost = 5,
                    cooldown = 4f,
                    damageRate = 1f,
                    radius = 1f
                }
            }
        };

        map.Clear();
        map.Add(skill.skillId, skill);
    }

    private static void SetSkillDataManagerInstance(SkillDataManager value)
    {
        PropertyInfo instanceProperty = typeof(SkillDataManager).GetProperty(
            nameof(SkillDataManager.Instance),
            BindingFlags.Static | BindingFlags.Public);
        MethodInfo privateSetter = instanceProperty != null
            ? instanceProperty.GetSetMethod(true)
            : null;
        Assert.That(privateSetter, Is.Not.Null);
        privateSetter.Invoke(null, new object[] { value });
    }

    private void InitializePlayer()
    {
        architecture.SendCommand(new InitializePlayerCommand(
            new NCharacter
            {
                id = 1,
                slotIndex = 0,
                name = "DeveloperModeTest",
                classId = 1,
                level = 1
            },
            new CharacterDefine
            {
                classId = 1,
                initLevel = 1,
                hp = 100f,
                mp = 100f,
                attack = 25f,
                moveSpeed = 3f
            }));
    }
}
#endif
