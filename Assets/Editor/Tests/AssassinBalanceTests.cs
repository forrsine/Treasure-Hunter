#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 刺客高风险高回报回归测试：保护基础生存削弱、技能3前摇、动作锁定和取消清理规则。
/// </summary>
public sealed class AssassinBalanceTests
{
    private const string CharacterDataPath = "Assets/Resources/Data/CharacterDefine.json";
    private const string SkillDataPath = "Assets/Resources/Data/SkillDefine.json";
    private const string PlayerRuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";
    private const string AssassinPrefabPath = "Assets/Resources/Characters/Assassin.prefab";

    [Test]
    public void AssassinConfiguration_KeepsBurstAndLowersBaseSurvivability()
    {
        CharacterDefine assassin = LoadCharacterTable().characters.First(item => item.classId == 4);

        Assert.That(assassin.hp, Is.EqualTo(240f).Within(0.001f));
        Assert.That(assassin.defense, Is.EqualTo(5f).Within(0.001f));
        Assert.That(assassin.attack, Is.EqualTo(44f).Within(0.001f));
        Assert.That(assassin.moveSpeed, Is.EqualTo(6f).Within(0.001f));
        Assert.That(assassin.basicAttackDuration, Is.EqualTo(0.62f).Within(0.001f));
    }

    [Test]
    public void ScytheSpinConfiguration_UsesConfirmedCommitmentWithoutChangingDamageCurve()
    {
        SkillDefineTable table = LoadSkillTable();
        SkillDefine scythe = table.skills.First(item => item.skillId == 2001);

        Assert.That(scythe.castCommitment, Is.Not.Null);
        Assert.That(scythe.castCommitment.enabled, Is.True);
        Assert.That(scythe.castCommitment.hitDelay, Is.EqualTo(0.35f).Within(0.001f));
        Assert.That(scythe.castCommitment.lockDuration, Is.EqualTo(1.05f).Within(0.001f));
        Assert.That(scythe.castCommitment.movementSpeedLimit, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(SkillDataManager.TryValidateSkill(scythe, out string error), Is.True, error);

        SkillLevelDefine levelOne = scythe.GetLevelData(1);
        SkillLevelDefine levelFour = scythe.GetLevelData(4);
        Assert.That(levelOne.mpCost, Is.EqualTo(32));
        Assert.That(levelOne.cooldown, Is.EqualTo(7f).Within(0.001f));
        Assert.That(levelOne.damageRate, Is.EqualTo(1.9f).Within(0.001f));
        Assert.That(levelOne.radius, Is.EqualTo(2.8f).Within(0.001f));
        Assert.That(levelFour.mpCost, Is.EqualTo(41));
        Assert.That(levelFour.cooldown, Is.EqualTo(5.8f).Within(0.001f));
        Assert.That(levelFour.damageRate, Is.EqualTo(3.1f).Within(0.001f));
        Assert.That(levelFour.radius, Is.EqualTo(3.7f).Within(0.001f));

        foreach (SkillDefine otherSkill in table.skills.Where(item => item.skillId != 2001))
        {
            Assert.That(
                otherSkill.castCommitment == null || !otherSkill.castCommitment.enabled,
                Is.True,
                $"技能 {otherSkill.skillId} 不应被刺客动作锁定规则影响。");
        }
    }

    [Test]
    public void CommitmentValidation_RejectsHitAfterUnlockAndInvalidMovementLimit()
    {
        SkillDefine scythe = LoadSkillTable().skills.First(item => item.skillId == 2001);
        scythe.castCommitment.hitDelay = 1.1f;

        Assert.That(SkillDataManager.TryValidateSkill(scythe, out string lateHitError), Is.False);
        StringAssert.Contains("hitDelay", lateHitError);

        scythe.castCommitment.hitDelay = 0.35f;
        scythe.castCommitment.movementSpeedLimit = 0f;
        Assert.That(SkillDataManager.TryValidateSkill(scythe, out string movementError), Is.False);
        StringAssert.Contains("movementSpeedLimit", movementError);
    }

    [Test]
    public void CommittedSpin_DelaysOneHitAndKeepsControlLockedUntilOnePointZeroFiveSeconds()
    {
        GameObject runtimeObject = null;
        GameObject targetObject = null;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        HashSet<int> existingLineEffects = CaptureInstanceIds<SkillLineEffect>();
        Dictionary<int, bool> existingFloatingTexts = CaptureActiveStates<FloatingCombatText>();

        try
        {
            CharacterDefine assassin = LoadCharacterTable().characters.First(item => item.classId == 4);
            SkillDefine scythe = LoadSkillTable().skills.First(item => item.skillId == 2001);
            runtimeObject = CreateAssassinRuntime(assassin, out PlayerSkillCastComponent skillCaster);
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 4, level = 1 },
                assassin));

            bool started = InvokePrivateMethod<bool>(
                skillCaster,
                "TryBeginCommittedScytheSpin",
                scythe,
                scythe.GetLevelData(1));
            Assert.That(started, Is.True);
            Assert.That(skillCaster.IsCommittedCastActive, Is.True);
            Assert.That(skillCaster.CommittedMovementSpeedLimit, Is.EqualTo(1.5f).Within(0.001f));

            targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            targetObject.name = "AssassinCommitmentTarget";
            targetObject.transform.position = runtimeObject.transform.position + Vector3.forward;
            targetObject.AddComponent<CubeTest>();
            Physics.SyncTransforms();

            InvokePrivateMethod(skillCaster, "TickCommittedCast", 0.349f);
            Assert.That(targetObject.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));

            InvokePrivateMethod(skillCaster, "TickCommittedCast", 0.002f);
            Assert.That(targetObject.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));

            // 再推进但不跨越动作结束点，同一目标不能被重复结算。
            InvokePrivateMethod(skillCaster, "TickCommittedCast", 0.698f);
            Assert.That(targetObject.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(skillCaster.IsCommittedCastActive, Is.True);

            InvokePrivateMethod(skillCaster, "TickCommittedCast", 0.002f);
            Assert.That(skillCaster.IsCommittedCastActive, Is.False);
            Assert.That(targetObject.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
        }
        finally
        {
            CleanupTransientCombatObjects(existingLineEffects, existingFloatingTexts);
            if (targetObject != null)
            {
                Object.DestroyImmediate(targetObject);
            }

            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }

            architecture.Deinit();
        }
    }

    [Test]
    public void PauseDeltaAndDisable_DoNotAdvanceOrLeavePendingCommittedHit()
    {
        GameObject runtimeObject = null;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;

        try
        {
            CharacterDefine assassin = LoadCharacterTable().characters.First(item => item.classId == 4);
            SkillDefine scythe = LoadSkillTable().skills.First(item => item.skillId == 2001);
            runtimeObject = CreateAssassinRuntime(assassin, out PlayerSkillCastComponent skillCaster);
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 4, level = 1 },
                assassin));
            InvokePrivateMethod<bool>(
                skillCaster,
                "TryBeginCommittedScytheSpin",
                scythe,
                scythe.GetLevelData(1));

            InvokePrivateMethod(skillCaster, "TickCommittedCast", 0f);
            Assert.That(GetPrivateField<float>(skillCaster, "committedCastElapsed"), Is.Zero.Within(0.001f));
            Assert.That(skillCaster.IsCommittedCastActive, Is.True);

            // EditMode 中普通 MonoBehaviour 不会像 PlayMode 一样自动收到 OnDisable，
            // 这里显式调用生命周期入口，验证真实禁用时执行的清理逻辑。
            InvokePrivateMethod(skillCaster, "OnDisable");
            Assert.That(skillCaster.IsCommittedCastActive, Is.False);
            Assert.That(GetPrivateField<int>(skillCaster, "pendingCommittedDamage"), Is.Zero);
            Assert.That(GetPrivateField<float>(skillCaster, "pendingCommittedRadius"), Is.Zero.Within(0.001f));
        }
        finally
        {
            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }

            architecture.Deinit();
        }
    }

    [Test]
    public void CommittedSpin_BlocksBasicAttackInputUntilControlReturns()
    {
        GameObject runtimeObject = null;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        IGameplayInput previousInput = GameplayRuntime.Instance.CurrentInput;
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true };

        try
        {
            CharacterDefine assassin = LoadCharacterTable().characters.First(item => item.classId == 4);
            SkillDefine scythe = LoadSkillTable().skills.First(item => item.skillId == 2001);
            runtimeObject = CreateAssassinRuntime(assassin, out PlayerSkillCastComponent skillCaster);
            PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 4, level = 1 },
                assassin));
            GameplayRuntime.Instance.RegisterInput(input);

            InvokePrivateMethod<bool>(
                skillCaster,
                "TryBeginCommittedScytheSpin",
                scythe,
                scythe.GetLevelData(1));
            InvokePrivateMethod(combat, "CheckAttackInput");
            Assert.That(combat.IsAttacking, Is.False, "技能承诺动作期间不能插入普通攻击。");

            skillCaster.CancelCommittedCast();
            SetPrivateField(runtimeObject.GetComponent<PlayerPresentationComponent>(), "skillAnimationTimer", 0f);
            InvokePrivateMethod(combat, "CheckAttackInput");
            Assert.That(combat.IsAttacking, Is.True, "控制恢复后同一套普攻输入应重新生效。");
        }
        finally
        {
            GameplayRuntime.Instance.UnregisterInput(input);
            if (previousInput != null)
            {
                GameplayRuntime.Instance.RegisterInput(previousInput);
            }

            if (runtimeObject != null)
            {
                Object.DestroyImmediate(runtimeObject);
            }

            architecture.Deinit();
        }
    }

    private static GameObject CreateAssassinRuntime(
        CharacterDefine assassin,
        out PlayerSkillCastComponent skillCaster)
    {
        GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssassinPrefabPath);
        Assert.That(runtimePrefab, Is.Not.Null);
        Assert.That(visualPrefab, Is.Not.Null);

        GameObject runtimeObject = Object.Instantiate(runtimePrefab);
        GameObject visualObject = Object.Instantiate(visualPrefab, runtimeObject.transform);
        PlayerRuntimeController runtime = runtimeObject.GetComponent<PlayerRuntimeController>();
        InvokePrivateMethod(runtime, "CacheComponents");
        SetPrivateField(runtime, "entryDefine", assassin);
        runtime.Presentation.BindVisual(visualObject, assassin);
        runtimeObject.GetComponent<PlayerCombatComponent>().Initialize(runtime);
        skillCaster = runtimeObject.GetComponent<PlayerSkillCastComponent>();
        skillCaster.Initialize(runtime);
        return runtimeObject;
    }

    private static CharacterDefineTable LoadCharacterTable()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterDataPath);
        Assert.That(json, Is.Not.Null);
        CharacterDefineTable table = JsonUtility.FromJson<CharacterDefineTable>(json.text);
        Assert.That(table, Is.Not.Null);
        return table;
    }

    private static SkillDefineTable LoadSkillTable()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(SkillDataPath);
        Assert.That(json, Is.Not.Null);
        SkillDefineTable table = JsonUtility.FromJson<SkillDefineTable>(json.text);
        Assert.That(table, Is.Not.Null);
        return table;
    }

    private static HashSet<int> CaptureInstanceIds<T>() where T : Component
    {
        return new HashSet<int>(
            Object.FindObjectsOfType<T>(true)
                .Where(item => item != null)
                .Select(item => item.GetInstanceID()));
    }

    private static Dictionary<int, bool> CaptureActiveStates<T>() where T : Component
    {
        return Object.FindObjectsOfType<T>(true)
            .Where(item => item != null)
            .ToDictionary(item => item.GetInstanceID(), item => item.gameObject.activeSelf);
    }

    private static void CleanupTransientCombatObjects(
        ISet<int> existingLineEffects,
        IReadOnlyDictionary<int, bool> existingFloatingTexts)
    {
        foreach (SkillLineEffect effect in Object.FindObjectsOfType<SkillLineEffect>(true))
        {
            if (effect != null && !existingLineEffects.Contains(effect.GetInstanceID()))
            {
                Object.DestroyImmediate(effect.gameObject);
            }
        }

        foreach (FloatingCombatText floatingText in Object.FindObjectsOfType<FloatingCombatText>(true))
        {
            if (floatingText == null)
            {
                continue;
            }

            int instanceId = floatingText.GetInstanceID();
            bool existedBefore = existingFloatingTexts.TryGetValue(instanceId, out bool wasActive);
            bool activatedByThisTest = existedBefore && !wasActive && floatingText.gameObject.activeSelf;
            bool createdByThisTest = !existedBefore;
            if (!activatedByThisTest && !createdByThisTest)
            {
                continue;
            }

            if (SkillVisualPool.Instance != null)
            {
                InvokePrivateMethod(floatingText, "ReleaseToPool");
            }
            else
            {
                Object.DestroyImmediate(floatingText.gameObject);
            }
        }
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = FindPrivateMethod(target, methodName);
        method.Invoke(target, parameters);
    }

    private static T InvokePrivateMethod<T>(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = FindPrivateMethod(target, methodName);
        return (T)method.Invoke(target, parameters);
    }

    private static MethodInfo FindPrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"测试目标缺少私有方法：{methodName}");
        return method;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"测试目标缺少私有字段：{fieldName}");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"测试目标缺少私有字段：{fieldName}");
        return (T)field.GetValue(target);
    }

    private sealed class TestGameplayInput : IGameplayInput
    {
        public bool LeftMouseDownValue { get; set; }

        public float XInput => 0f;
        public float YInput => 0f;
        public Vector3 MouseInput => Vector3.zero;
        public bool LeftMouseDown => LeftMouseDownValue;
        public bool LeftMouseHeld => false;
        public bool LeftMouseUp => false;
        public bool RollDown => false;
        public bool DeveloperModeToggleDown => false;
        public bool DebugAddLevelsDown => false;
        public bool DebugAddExpDown => false;
        public bool DebugRestoreManaDown => false;
        public bool DebugBreakVaultDown => false;
        public bool InventoryToggleDown => false;
        public bool Skill1Down => false;
        public bool Skill1Held => false;
        public bool Skill1Up => false;
        public bool Skill2Down => false;
        public bool Skill2Held => false;
        public bool Skill2Up => false;
        public bool Skill3Down => false;
        public bool Skill3Held => false;
        public bool Skill3Up => false;
    }
}
#endif
