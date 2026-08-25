#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 职业专属技能栏回归测试：确保镰刀大旋转只对刺客显示，其他职业不会残留图标或文字。
/// </summary>
public sealed class PlayerSkillBarUiTests
{
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";

    private IArchitecture architecture;
    private GameObject configObject;
    private GameObject skillDataManagerObject;

    [SetUp]
    public void SetUp()
    {
        configObject = new GameObject("PlayerSkillBarUiTestConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 300, 330 };
        GameConfig.instance = config;

        if (SkillDataManager.Instance == null)
        {
            skillDataManagerObject = new GameObject("PlayerSkillBarUiTestSkillDataManager");
            SkillDataManager skillDataManager = skillDataManagerObject.AddComponent<SkillDataManager>();
            SetSkillDataManagerInstance(skillDataManager);
            InvokePrivateMethod(skillDataManager, "LoadSkillDefine");
        }
        else if (SkillDataManager.Instance.GetAllSkills().Count == 0)
        {
            // 兼容编辑器中已经存在组件、但尚未进入 Play Mode 因而没有执行 Awake 的情况。
            InvokePrivateMethod(SkillDataManager.Instance, "LoadSkillDefine");
        }

        architecture = TreasureHunterArchitecture.Interface;
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.Deinit();
        architecture = null;
        GameConfig.instance = null;

        if (skillDataManagerObject != null)
        {
            SetSkillDataManagerInstance(null);
            Object.DestroyImmediate(skillDataManagerObject);
        }

        Object.DestroyImmediate(configObject);
    }

    [Test]
    public void GameplayUiPrefab_HasCompleteSkill3SlotReferences()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        PlayerSkillBarUi skillBarUi = prefab != null
            ? prefab.GetComponentInChildren<PlayerSkillBarUi>(true)
            : null;

        Assert.That(prefab, Is.Not.Null);
        Assert.That(skillBarUi, Is.Not.Null);
        Assert.That(skillBarUi.ValidatePrefabReferences(false), Is.True);
        Assert.That(FindChildByName(prefab.transform, "Skill3Slot"), Is.Not.Null);
    }

    [TestCase(1, false, TestName = "Warrior_HidesAssassinSkill3Slot")]
    [TestCase(2, false, TestName = "Wizard_HidesAssassinSkill3Slot")]
    [TestCase(3, false, TestName = "Archer_HidesAssassinSkill3Slot")]
    [TestCase(4, true, TestName = "Assassin_ShowsSkill3Slot")]
    public void Skill3Slot_IsVisibleOnlyForAllowedProfession(int classId, bool expectedVisible)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        GameObject uiInstance = Object.Instantiate(prefab);

        try
        {
            InitializePlayer(classId);
            PlayerSkillBarUi skillBarUi = uiInstance.GetComponentInChildren<PlayerSkillBarUi>(true);
            Transform skill3Slot = FindChildByName(uiInstance.transform, "Skill3Slot");
            Transform skill1Slot = FindChildByName(uiInstance.transform, "Skill1Slot");
            Transform skill2Slot = FindChildByName(uiInstance.transform, "Skill2Slot");

            Assert.That(skillBarUi, Is.Not.Null);
            Assert.That(skill3Slot, Is.Not.Null);
            InvokePrivateMethod(skillBarUi, "RefreshAllSlots");

            Assert.That(skill3Slot.gameObject.activeSelf, Is.EqualTo(expectedVisible));
            Assert.That(skill1Slot.gameObject.activeSelf, Is.True, "隐藏职业专属技能不能影响技能1。 ");
            Assert.That(skill2Slot.gameObject.activeSelf, Is.True, "隐藏职业专属技能不能影响技能2。 ");

            Text skill3Text = skill3Slot.GetComponentInChildren<Text>(true);
            Image cooldownMask = FindChildByName(skill3Slot, "Skill3CooldownMask").GetComponent<Image>();
            if (expectedVisible)
            {
                Assert.That(skill3Text.text, Does.Contain("镰刀大旋转"));
            }
            else
            {
                Assert.That(skill3Text.text, Is.Empty, "非刺客不能残留技能3文字。 ");
                Assert.That(cooldownMask.gameObject.activeSelf, Is.False, "非刺客不能残留技能3冷却遮罩。 ");
            }
        }
        finally
        {
            Object.DestroyImmediate(uiInstance);
        }
    }

    private void InitializePlayer(int classId)
    {
        NCharacter save = new NCharacter
        {
            id = classId,
            slotIndex = 0,
            classId = classId,
            level = 1,
            exp = 0
        };
        CharacterDefine define = new CharacterDefine
        {
            classId = classId,
            initLevel = 1,
            hp = 300f,
            mp = 180f,
            attack = 30f,
            moveSpeed = 5f
        };
        architecture.SendCommand(new InitializePlayerCommand(save, define));
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == targetName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"找不到测试目标方法：{methodName}");
        method.Invoke(target, null);
    }

    private static void SetSkillDataManagerInstance(SkillDataManager value)
    {
        PropertyInfo instanceProperty = typeof(SkillDataManager).GetProperty(
            nameof(SkillDataManager.Instance),
            BindingFlags.Static | BindingFlags.Public);
        MethodInfo privateSetter = instanceProperty != null
            ? instanceProperty.GetSetMethod(true)
            : null;
        Assert.That(privateSetter, Is.Not.Null, "SkillDataManager.Instance 缺少可供测试恢复的私有 setter。 ");
        privateSetter.Invoke(null, new object[] { value });
    }
}
#endif
