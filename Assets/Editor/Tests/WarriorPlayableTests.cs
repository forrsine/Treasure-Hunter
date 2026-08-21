#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;

/// <summary>
/// 战士可玩化回归测试：保护职业配置、减伤公式以及 Animator/Prefab 装配，
/// 防止后续替换资源或修改公共角色代码时让战士重新变成不可操作状态。
/// </summary>
public sealed class WarriorPlayableTests
{
    private const string CharacterDataPath = "Assets/Resources/Data/CharacterDefine.json";
    private const string ControllerPath = "Assets/Ani/Warrior.controller";
    private const string UpperBodyMaskPath = "Assets/Ani/WarriorUpperBody.mask";
    private const string WarriorPrefabPath = "Assets/Resources/Characters/Warrior.prefab";
    private const string WarriorPreviewPrefabPath = "Assets/Resources/Characters/WarriorPreview.prefab";
    private const string PlayerRuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";

    [Test]
    public void WarriorConfiguration_HasPlayableStatsAndSimpleAnimationStyle()
    {
        CharacterDefine warrior = LoadWarriorDefine();

        Assert.That(warrior.animationStyle, Is.EqualTo(CharacterAnimationStyle.SimpleSpeedAttack));
        Assert.That(warrior.basicAttackDuration, Is.EqualTo(0.75f).Within(0.001f));
        Assert.That(warrior.hp, Is.EqualTo(380f));
        Assert.That(warrior.mp, Is.EqualTo(120f));
        Assert.That(warrior.attack, Is.EqualTo(34f));
        Assert.That(warrior.defense, Is.EqualTo(20f));
        Assert.That(warrior.moveSpeed, Is.EqualTo(5f));
    }

    [Test]
    public void WarriorDefense_ReducesOneHundredDamageToEighty()
    {
        GameObject configObject = new GameObject("WarriorTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 360, 400 };
        GameConfig.instance = config;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;

        try
        {
            NCharacter save = new NCharacter
            {
                id = 1,
                slotIndex = 0,
                name = "WarriorTest",
                classId = 1,
                level = 1,
                exp = 0
            };
            architecture.SendCommand(new InitializePlayerCommand(save, LoadWarriorDefine()));

            PlayerStatsSnapshot before = architecture.SendQuery(new GetPlayerStatsQuery());
            PlayerDamageResult result = architecture.SendCommand(new TakePlayerDamageCommand(100, false));
            PlayerStatsSnapshot after = architecture.SendQuery(new GetPlayerStatsQuery());

            Assert.That(before.DamageReduction, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(result.ActualDamage, Is.EqualTo(80));
            Assert.That(after.CurrentHp, Is.EqualTo(before.MaxHp - 80));
        }
        finally
        {
            architecture.Deinit();
            GameConfig.instance = null;
            Object.DestroyImmediate(configObject);
        }
    }

    [Test]
    public void WarriorAnimator_ContainsWalkRunAttackAndSkillFallbacks()
    {
        EditorAnimatorController controller = AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null);
        AssertParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AssertParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        AssertParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AssertParameter(controller, "Skill", AnimatorControllerParameterType.Trigger);

        Assert.That(controller.layers.Length, Is.EqualTo(2));
        UnityEditor.Animations.AnimatorState locomotionState = controller.layers[0].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Locomotion");
        Assert.That(locomotionState, Is.Not.Null);
        UnityEditor.Animations.BlendTree locomotionTree = locomotionState.motion as UnityEditor.Animations.BlendTree;
        Assert.That(locomotionTree, Is.Not.Null);
        Assert.That(locomotionTree.blendParameter, Is.EqualTo("Speed"));
        Assert.That(locomotionTree.children.Length, Is.EqualTo(3));
        Assert.That(locomotionTree.children[0].threshold, Is.EqualTo(0f));
        Assert.That(locomotionTree.children[1].threshold, Is.EqualTo(0.5f));
        Assert.That(locomotionTree.children[1].timeScale, Is.EqualTo(1f));
        Assert.That(locomotionTree.children[2].threshold, Is.EqualTo(1f));
        Assert.That(locomotionTree.children[2].timeScale, Is.EqualTo(2f));

        Assert.That(controller.layers[1].name, Is.EqualTo("Attack Layer"));
        Assert.That(controller.layers[1].defaultWeight, Is.Zero);
        Assert.That(AssetDatabase.GetAssetPath(controller.layers[1].avatarMask), Is.EqualTo(UpperBodyMaskPath));
        string[] attackStateNames = controller.layers[1].stateMachine.states
            .Select(child => child.state.name)
            .ToArray();
        CollectionAssert.Contains(attackStateNames, "Attack");
        CollectionAssert.Contains(attackStateNames, "Skill");
        AssertSimpleTransitionsUseFixedTenthSecond(controller);
    }

    [TestCase(WarriorPrefabPath)]
    [TestCase(WarriorPreviewPrefabPath)]
    public void WarriorPrefabs_UseProjectOwnedController(string prefabPath)
    {
        EditorAnimatorController controller = AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ControllerPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefab, Is.Not.Null);
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
    }

    [Test]
    public void WarriorGameplayPrefab_IsSeventyPercentScaleAndFacesRuntimeForward()
    {
        GameObject warriorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPrefabPath);
        GameObject previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPreviewPrefabPath);

        Assert.That(warriorPrefab, Is.Not.Null);
        Assert.That(warriorPrefab.transform.localScale, Is.EqualTo(Vector3.one * 0.7f));
        Assert.That(
            Quaternion.Angle(warriorPrefab.transform.localRotation, Quaternion.identity),
            Is.LessThan(0.01f));

        // 本次只修正游戏内表现，选角界面继续沿用原来的预览尺寸与朝向。
        Assert.That(previewPrefab, Is.Not.Null);
        Assert.That(previewPrefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(
            Quaternion.Angle(previewPrefab.transform.localRotation, Quaternion.Euler(0f, 180f, 0f)),
            Is.LessThan(0.01f));
    }

    [Test]
    public void PlayerRuntime_UsesOriginalAssassinJumpHeightForEveryClass()
    {
        GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
        PlayerMovementComponent movement = runtimePrefab != null
            ? runtimePrefab.GetComponent<PlayerMovementComponent>()
            : null;

        Assert.That(runtimePrefab, Is.Not.Null);
        Assert.That(movement, Is.Not.Null);
        SerializedObject movementSerialized = new SerializedObject(movement);
        SerializedProperty jumpHeight = movementSerialized.FindProperty("jumpHeight");
        Assert.That(jumpHeight, Is.Not.Null);
        Assert.That(jumpHeight.floatValue, Is.EqualTo(1f).Within(0.001f));
    }

    [Test]
    public void WarriorPresentation_FadesIntoVisibleAttackStateInTenthSecond()
    {
        GameObject presentationRoot = new GameObject("WarriorPresentationTestRoot");
        PlayerPresentationComponent presentation =
            presentationRoot.AddComponent<PlayerPresentationComponent>();
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPrefabPath);
        GameObject visualInstance = Object.Instantiate(visualPrefab, presentationRoot.transform);

        try
        {
            Animator animator = visualInstance.GetComponentInChildren<Animator>(true);
            // EditMode 实例不会像进入 PlayMode 一样自动完成首帧 Animator 初始化。
            // 先 Rebind，测试才与游戏中玩家生成后的 Animator 状态一致。
            animator.Rebind();
            animator.Update(0f);
            presentation.BindVisual(visualInstance, LoadWarriorDefine());
            int attackLayerIndex = animator.GetLayerIndex("Attack Layer");
            Assert.That(attackLayerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(0f).Within(0.001f));

            presentation.SetCombo(1);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.Zero.Within(0.001f));
            InvokePrivateMethod(presentation, "AdvanceAttackLayerFadeIn", 0.05f);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(0.5f).Within(0.01f));
            InvokePrivateMethod(presentation, "AdvanceAttackLayerFadeIn", 0.05f);
            animator.Update(0.11f);

            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(attackLayerIndex);
            int attackStateHash = Animator.StringToHash("Attack");
            bool isPlayingAttack =
                currentState.shortNameHash == attackStateHash ||
                nextState.shortNameHash == attackStateHash;
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(1f).Within(0.001f));
            Assert.That(isPlayingAttack, Is.True, "战士左键攻击后没有进入 Attack 状态。");
        }
        finally
        {
            Object.DestroyImmediate(presentationRoot);
        }
    }

    [Test]
    public void WarriorAttackHitbox_DealsDamageOncePerHitWindow()
    {
        GameObject configObject = new GameObject("WarriorAttackTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 360, 400 };
        GameConfig.instance = config;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        GameObject runtimeInstance = null;
        GameObject targetObject = null;
        HashSet<int> existingFloatingTextIds = new HashSet<int>(
            Object.FindObjectsOfType<FloatingCombatText>(true)
                .Select(item => item.GetInstanceID()));

        try
        {
            CharacterDefine warrior = LoadWarriorDefine();
            NCharacter save = new NCharacter
            {
                id = 1,
                slotIndex = 0,
                name = "WarriorAttackTest",
                classId = 1,
                level = 1,
                exp = 0
            };

            GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
            runtimeInstance = Object.Instantiate(runtimePrefab);
            PlayerRuntimeController runtime = runtimeInstance.GetComponent<PlayerRuntimeController>();
            runtime.ApplyCharacterEntryData(save, warrior);

            PlayerCombatComponent combat = runtimeInstance.GetComponent<PlayerCombatComponent>();
            // 此测试只验证攻击盒与伤害链，不初始化受击闪烁等无关表现，避免 EditMode 创建临时材质。
            combat.Initialize(runtime);
            Transform hitboxTransform = runtimeInstance.transform.Find("AttackHitbox");
            SphereCollider hitbox = hitboxTransform.GetComponent<SphereCollider>();
            WeaponCo weapon = hitboxTransform.GetComponent<WeaponCo>();

            targetObject = new GameObject("WarriorAttackTestTarget");
            CubeTest target = targetObject.AddComponent<CubeTest>();
            BoxCollider targetCollider = targetObject.AddComponent<BoxCollider>();
            Vector3 originalScale = target.transform.localScale;

            combat.WeaponEnable();
            Assert.That(hitbox.enabled, Is.True);
            InvokePrivateMethod(weapon, "OnTriggerEnter", targetCollider);
            Vector3 scaleAfterFirstHit = target.transform.localScale;
            InvokePrivateMethod(weapon, "OnTriggerStay", targetCollider);

            Assert.That(scaleAfterFirstHit.x, Is.LessThan(originalScale.x));
            Assert.That(target.transform.localScale, Is.EqualTo(scaleAfterFirstHit));
        }
        finally
        {
            FloatingCombatText[] floatingTexts = Object.FindObjectsOfType<FloatingCombatText>(true);
            for (int i = 0; i < floatingTexts.Length; i++)
            {
                if (floatingTexts[i] != null &&
                    !existingFloatingTextIds.Contains(floatingTexts[i].GetInstanceID()))
                {
                    Object.DestroyImmediate(floatingTexts[i].gameObject);
                }
            }

            if (targetObject != null)
            {
                Object.DestroyImmediate(targetObject);
            }

            if (runtimeInstance != null)
            {
                Object.DestroyImmediate(runtimeInstance);
            }

            architecture.Deinit();
            GameConfig.instance = null;
            Object.DestroyImmediate(configObject);
        }
    }

    private static CharacterDefine LoadWarriorDefine()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterDataPath);
        Assert.That(json, Is.Not.Null);
        CharacterDefineTable table = JsonUtility.FromJson<CharacterDefineTable>(json.text);
        CharacterDefine warrior = table.characters.FirstOrDefault(define => define.classId == 1);
        Assert.That(warrior, Is.Not.Null);
        return warrior;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"测试目标缺少私有方法：{methodName}");
        method.Invoke(target, parameters);
    }

    private static void AssertParameter(
        EditorAnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        AnimatorControllerParameter parameter = controller.parameters
            .FirstOrDefault(item => item.name == parameterName);
        Assert.That(parameter, Is.Not.Null, $"战士 Animator 缺少参数：{parameterName}");
        Assert.That(parameter.type, Is.EqualTo(expectedType));
    }

    private static void AssertSimpleTransitionsUseFixedTenthSecond(
        EditorAnimatorController controller)
    {
        foreach (UnityEditor.Animations.AnimatorControllerLayer layer in controller.layers)
        {
            UnityEditor.Animations.AnimatorStateMachine stateMachine = layer.stateMachine;
            IEnumerable<UnityEditor.Animations.AnimatorStateTransition> transitions =
                stateMachine.anyStateTransitions.Concat(
                    stateMachine.states.SelectMany(child => child.state.transitions));
            foreach (UnityEditor.Animations.AnimatorStateTransition transition in transitions)
            {
                Assert.That(transition.hasFixedDuration, Is.True);
                Assert.That(transition.duration, Is.EqualTo(0.1f).Within(0.001f));
            }
        }
    }
}
#endif
