#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;

/// <summary>
/// 弓箭手和法师可玩化回归测试。
/// 保护职业数据、动画状态机、动画释放事件和 Prefab 装配，避免公共玩家逻辑调整后远程职业再次失效。
/// </summary>
public sealed class RangedCharacterPlayableTests
{
    private const string CharacterDataPath = "Assets/Resources/Data/CharacterDefine.json";
    private const string UpperBodyMaskPath = "Assets/Ani/RangedUpperBody.mask";
    private const string PlayerRuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";
    private const string ArcherControllerPath = "Assets/Ani/Archer.controller";
    private const string WizardControllerPath = "Assets/Ani/Wizard.controller";
    private const string ArcherAttackClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human Shoot Crossbow 2_shootTrigger.anim";
    private const string WizardAttackClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human_atkStaff.anim";
    private const string WizardProjectilePrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/MagicMissile 1.prefab";
    private const string WizardExplosionPrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/Magic Explosion 1.prefab";
    private const string ArcherProjectilePrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/Human Bolt.prefab";
    private const string SkillFireballPrefabPath = "Assets/AddressableAssets/SkillVFX/FireballProjectileVfx.prefab";
    private const string SlimePrefabPath = "Assets/Prefabs/Slime1.prefab";

    [TestCase(2, 0.85f, 0.5f, 14f, 0.9f, 0.18f, "#7D6BFFFF",
        CharacterProjectileTrajectory.Arc, 3f, 0.7f, true, 1.5f)]
    [TestCase(3, 0.25f, 0.4f, 12f, 1.25f, 0.12f, "#FFFFFFFF",
        CharacterProjectileTrajectory.Straight, 0f, 1f, false, 0f)]
    public void RangedConfiguration_HasCompleteProjectileParameters(
        int classId,
        float attackDuration,
        float releaseRatio,
        float speed,
        float lifetime,
        float radius,
        string colorHex,
        CharacterProjectileTrajectory trajectory,
        float arcHeight,
        float visualScale,
        bool applyTint,
        float explosionRadius)
    {
        CharacterDefine define = LoadDefine(classId);

        Assert.That(define.animationStyle, Is.EqualTo(CharacterAnimationStyle.SimpleSpeedAttack));
        Assert.That(define.basicAttackType, Is.EqualTo(CharacterBasicAttackType.Projectile));
        Assert.That(define.basicAttackDuration, Is.EqualTo(attackDuration).Within(0.001f));
        Assert.That(define.projectileReleaseRatio, Is.EqualTo(releaseRatio).Within(0.001f));
        Assert.That(define.projectileSpeed, Is.EqualTo(speed).Within(0.001f));
        Assert.That(define.projectileLifetime, Is.EqualTo(lifetime).Within(0.001f));
        Assert.That(define.projectileRadius, Is.EqualTo(radius).Within(0.001f));
        Assert.That(define.projectileColorHex, Is.EqualTo(colorHex));
        Assert.That(define.projectileTrajectory, Is.EqualTo(trajectory));
        Assert.That(define.projectileArcHeight, Is.EqualTo(arcHeight).Within(0.001f));
        Assert.That(define.projectileVisualScale, Is.EqualTo(visualScale).Within(0.001f));
        Assert.That(define.projectileApplyTint, Is.EqualTo(applyTint));
        Assert.That(define.projectileExplosionRadius, Is.EqualTo(explosionRadius).Within(0.001f));
    }

    [Test]
    public void ArcherBalance_UsesFifteenAttackAndConfiguredCadence()
    {
        CharacterDefine define = LoadDefine(3);

        Assert.That(define.attack, Is.EqualTo(15f).Within(0.001f));
        Assert.That(define.basicAttackDuration, Is.EqualTo(0.25f).Within(0.001f));
    }

    [Test]
    public void ArcherHeldAttack_FiresOnlyAfterConfiguredInterval()
    {
        GameObject runtimeInstance = null;
        IGameplayInput previousInput = GameplayRuntime.Instance.CurrentInput;
        TestGameplayInput testInput = new TestGameplayInput
        {
            LeftMouseHeldValue = true
        };

        try
        {
            CharacterDefine archerDefine = LoadDefine(3);
            GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Characters/Archer.prefab");
            runtimeInstance = Object.Instantiate(runtimePrefab);
            GameObject visualInstance = Object.Instantiate(visualPrefab, runtimeInstance.transform);
            PlayerRuntimeController runtime = runtimeInstance.GetComponent<PlayerRuntimeController>();
            InvokePrivateMethod(runtime, "CacheComponents");
            FieldInfo entryDefineField = typeof(PlayerRuntimeController).GetField(
                "entryDefine",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(entryDefineField, Is.Not.Null);
            entryDefineField.SetValue(runtime, archerDefine);

            PlayerPresentationComponent presentation =
                runtimeInstance.GetComponent<PlayerPresentationComponent>();
            PlayerCombatComponent combat = runtimeInstance.GetComponent<PlayerCombatComponent>();
            PlayerRangedAttackComponent rangedAttack =
                runtimeInstance.GetComponent<PlayerRangedAttackComponent>();
            // 本测试只验证输入与攻速，不初始化生命组件，避免 EditMode 创建临时受击材质干扰断言。
            presentation.BindVisual(visualInstance, archerDefine);
            rangedAttack.Initialize(runtime);
            combat.Initialize(runtime);
            GameplayRuntime.Instance.RegisterInput(testInput);

            InvokePrivateMethod(combat, "CheckAttackInput");
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));

            // 按住期间重复检查不会每帧发射，未满 0.25 秒仍保持一支箭。
            InvokePrivateMethod(combat, "CheckAttackInput");
            InvokePrivateMethod(
                combat,
                "TickArcherBasicAttackCooldown",
                archerDefine.basicAttackDuration - 0.01f);
            InvokePrivateMethod(combat, "CheckAttackInput");
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));

            InvokePrivateMethod(combat, "TickArcherBasicAttackCooldown", 0.02f);
            InvokePrivateMethod(combat, "CheckAttackInput");
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(2));
        }
        finally
        {
            GameplayRuntime.Instance.UnregisterInput(testInput);
            if (previousInput != null)
            {
                GameplayRuntime.Instance.RegisterInput(previousInput);
            }

            if (runtimeInstance != null)
            {
                Object.DestroyImmediate(runtimeInstance);
            }
        }
    }

    [TestCase(1)]
    [TestCase(4)]
    public void ExistingMeleeClasses_KeepMeleeBasicAttack(int classId)
    {
        Assert.That(LoadDefine(classId).basicAttackType, Is.EqualTo(CharacterBasicAttackType.Melee));
    }

    [TestCase(ArcherControllerPath, ArcherAttackClipPath, 0.375f, 0.75f)]
    [TestCase(WizardControllerPath, WizardAttackClipPath, 0.8f, 0.8f)]
    public void RangedAnimator_ContainsMovementAndUpperBodyAttackFlow(
        string controllerPath,
        string expectedAttackClipPath,
        float expectedAttackDuration,
        float expectedSkillDuration)
    {
        EditorAnimatorController controller = AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(controllerPath);
        Assert.That(controller, Is.Not.Null, $"远程职业控制器缺失：{controllerPath}");
        AssertParameter(controller, "Speed", AnimatorControllerParameterType.Float);
        AssertParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
        AssertParameter(controller, "Attack", AnimatorControllerParameterType.Trigger);
        AssertParameter(controller, "Skill", AnimatorControllerParameterType.Trigger);

        Assert.That(controller.layers.Length, Is.EqualTo(2));
        AnimatorState locomotionState = controller.layers[0].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Locomotion");
        Assert.That(locomotionState, Is.Not.Null);
        BlendTree locomotionTree = locomotionState.motion as BlendTree;
        Assert.That(locomotionTree, Is.Not.Null);
        Assert.That(locomotionTree.blendParameter, Is.EqualTo("Speed"));
        Assert.That(locomotionTree.children.Length, Is.EqualTo(3));
        Assert.That(locomotionTree.children[0].threshold, Is.EqualTo(0f));
        Assert.That(locomotionTree.children[1].threshold, Is.EqualTo(0.5f));
        Assert.That(locomotionTree.children[1].timeScale, Is.EqualTo(1f));
        Assert.That(locomotionTree.children[2].threshold, Is.EqualTo(1f));
        Assert.That(locomotionTree.children[2].timeScale, Is.EqualTo(2f));

        Assert.That(controller.layers[1].name, Is.EqualTo("Attack Layer"));
        Assert.That(AssetDatabase.GetAssetPath(controller.layers[1].avatarMask), Is.EqualTo(UpperBodyMaskPath));
        Assert.That(controller.layers[1].defaultWeight, Is.Zero);
        AnimatorState attackState = controller.layers[1].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Attack");
        Assert.That(attackState, Is.Not.Null);
        AnimationClip attackClip = attackState.motion as AnimationClip;
        Assert.That(AssetDatabase.GetAssetPath(attackClip), Is.EqualTo(expectedAttackClipPath));
        Assert.That(attackClip.length / attackState.speed, Is.EqualTo(expectedAttackDuration).Within(0.01f));

        AnimatorState skillState = controller.layers[1].stateMachine.states
            .Select(child => child.state)
            .FirstOrDefault(state => state.name == "Skill");
        Assert.That(skillState, Is.Not.Null);
        AnimationClip skillClip = skillState.motion as AnimationClip;
        Assert.That(skillClip, Is.SameAs(attackClip));
        Assert.That(skillClip.length / skillState.speed, Is.EqualTo(expectedSkillDuration).Within(0.01f));

        string[] stateNames = controller.layers[1].stateMachine.states
            .Select(child => child.state.name)
            .ToArray();
        CollectionAssert.Contains(stateNames, "Skill");
        AssertSimpleTransitionsUseFixedTenthSecond(controller);
    }

    [TestCase(ArcherAttackClipPath)]
    [TestCase(WizardAttackClipPath)]
    public void RangedAttackClip_RetainsShootAnimationEvent(string attackClipPath)
    {
        AnimationClip attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(attackClipPath);
        Assert.That(attackClip, Is.Not.Null);
        AnimationEvent shootEvent = AnimationUtility.GetAnimationEvents(attackClip)
            .FirstOrDefault(animationEvent => animationEvent.functionName == "shoot");
        Assert.That(shootEvent, Is.Not.Null, $"攻击动画缺少 shoot 释放事件：{attackClipPath}");
        Assert.That(shootEvent.time, Is.GreaterThan(0f));
        Assert.That(shootEvent.time, Is.LessThan(attackClip.length));
    }

    [Test]
    public void ArcherAttack_ShootEventPlaysAtFifteenHundredthsSecond()
    {
        EditorAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ArcherControllerPath);
        AnimatorState attackState = controller.layers[1].stateMachine.states
            .Select(child => child.state)
            .First(state => state.name == "Attack");
        AnimationClip attackClip = attackState.motion as AnimationClip;
        AnimationEvent shootEvent = AnimationUtility.GetAnimationEvents(attackClip)
            .First(animationEvent => animationEvent.functionName == "shoot");

        Assert.That(shootEvent.time / attackState.speed, Is.EqualTo(0.15f).Within(0.01f));
    }

    [TestCase("Assets/Resources/Characters/Archer.prefab", ArcherControllerPath)]
    [TestCase("Assets/Resources/Characters/ArcherPreview.prefab", ArcherControllerPath)]
    [TestCase("Assets/Resources/Characters/Wizard.prefab", WizardControllerPath)]
    [TestCase("Assets/Resources/Characters/WizardPreview.prefab", WizardControllerPath)]
    public void RangedPrefabs_UseProjectOwnedController(string prefabPath, string controllerPath)
    {
        EditorAnimatorController controller = AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(controllerPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(prefab, Is.Not.Null);
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
    }

    [TestCase("Assets/Resources/Characters/Archer.prefab", "Assets/Resources/Characters/ArcherPreview.prefab")]
    [TestCase("Assets/Resources/Characters/Wizard.prefab", "Assets/Resources/Characters/WizardPreview.prefab")]
    public void RangedGameplayPrefabs_UseCorrectedTransformAndKeepPreviewUnchanged(
        string gameplayPrefabPath,
        string previewPrefabPath)
    {
        GameObject gameplayPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(gameplayPrefabPath);
        GameObject previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(previewPrefabPath);

        Assert.That(gameplayPrefab, Is.Not.Null);
        Assert.That(gameplayPrefab.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(gameplayPrefab.transform.localScale, Is.EqualTo(Vector3.one * 0.7f));
        Assert.That(
            Quaternion.Angle(gameplayPrefab.transform.localRotation, Quaternion.identity),
            Is.LessThan(0.01f));

        Assert.That(previewPrefab, Is.Not.Null);
        Assert.That(previewPrefab.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(
            Quaternion.Angle(previewPrefab.transform.localRotation, Quaternion.Euler(0f, 180f, 0f)),
            Is.LessThan(0.01f));
    }

    [Test]
    public void PlayerRuntime_ContainsReusableRangedAttackPoolComponent()
    {
        GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
        Assert.That(runtimePrefab, Is.Not.Null);
        PlayerRangedAttackComponent rangedAttack =
            runtimePrefab.GetComponent<PlayerRangedAttackComponent>();
        Assert.That(rangedAttack, Is.Not.Null);

        SerializedObject serializedRangedAttack = new SerializedObject(rangedAttack);
        Assert.That(
            serializedRangedAttack.FindProperty("wizardProjectileVisualPrefab").objectReferenceValue,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(WizardProjectilePrefabPath)));
        Assert.That(
            serializedRangedAttack.FindProperty("wizardExplosionVisualPrefab").objectReferenceValue,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(WizardExplosionPrefabPath)));
        Assert.That(
            serializedRangedAttack.FindProperty("archerProjectileVisualPrefab").objectReferenceValue,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(ArcherProjectilePrefabPath)));
        Assert.That(serializedRangedAttack.FindProperty("archerImpactLingerDuration"), Is.Null);
        Assert.That(serializedRangedAttack.FindProperty("archerCloseHitVisualFlightDistance"), Is.Null);

        PlayerPresentationComponent presentation =
            runtimePrefab.GetComponent<PlayerPresentationComponent>();
        Assert.That(presentation, Is.Not.Null);
        SerializedObject serializedPresentation = new SerializedObject(presentation);
        Assert.That(serializedPresentation.FindProperty("simpleMovementDampTime").floatValue, Is.EqualTo(0.1f));
        Assert.That(serializedPresentation.FindProperty("simpleActionTransitionDuration").floatValue, Is.EqualTo(0.1f));
        Assert.That(serializedPresentation.FindProperty("simpleActionLayerBlendDuration").floatValue, Is.EqualTo(0.1f));
    }

    [TestCase(2, "Assets/Resources/Characters/Wizard.prefab")]
    [TestCase(3, "Assets/Resources/Characters/Archer.prefab")]
    public void LegacyProjectileAnimationReceiver_IsDisabledAndCannotDuplicateShot(
        int classId,
        string visualPrefabPath)
    {
        GameObject root = new GameObject("LegacyProjectileGuardTest");
        PlayerPresentationComponent presentation =
            root.AddComponent<PlayerPresentationComponent>();
        GameObject visual = Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath),
            root.transform);

        try
        {
            presentation.BindVisual(visual, LoadDefine(classId));
            triggerProjectile legacyShooter =
                visual.GetComponentInChildren<triggerProjectile>(true);
            Assert.That(legacyShooter, Is.Not.Null);
            Assert.That(legacyShooter.enabled, Is.False);

            FieldInfo spawnedProjectileField = typeof(triggerProjectile).GetField(
                "magicMissile",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(spawnedProjectileField, Is.Not.Null);
            Assert.That(spawnedProjectileField.GetValue(legacyShooter), Is.Null);
            legacyShooter.shoot();
            Assert.That(
                spawnedProjectileField.GetValue(legacyShooter),
                Is.Null,
                "禁用的 Human Pack shoot 接收器仍生成了第二套演示投射物。");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SkillFireballPrefab_KeepsOriginalScaleAndMaterial()
    {
        GameObject skillFireball =
            AssetDatabase.LoadAssetAtPath<GameObject>(SkillFireballPrefabPath);
        GameObject basicAttackSource =
            AssetDatabase.LoadAssetAtPath<GameObject>(WizardProjectilePrefabPath);
        Assert.That(skillFireball, Is.Not.Null);
        Assert.That(basicAttackSource, Is.Not.Null);
        Assert.That(skillFireball.transform.localScale, Is.EqualTo(Vector3.one * 0.8f));

        MeshRenderer skillRenderer = skillFireball.GetComponentInChildren<MeshRenderer>(true);
        MeshRenderer basicAttackRenderer = basicAttackSource.GetComponentInChildren<MeshRenderer>(true);
        Assert.That(skillRenderer, Is.Not.Null);
        Assert.That(basicAttackRenderer, Is.Not.Null);
        Assert.That(skillRenderer.sharedMaterial, Is.SameAs(basicAttackRenderer.sharedMaterial));
    }

    [TestCase(2, "Assets/Resources/Characters/Wizard.prefab")]
    [TestCase(3, "Assets/Resources/Characters/Archer.prefab")]
    public void RangedPresentation_AttackEntersVisibleAttackState(int classId, string visualPrefabPath)
    {
        GameObject presentationRoot = new GameObject("RangedPresentationTestRoot");
        PlayerPresentationComponent presentation =
            presentationRoot.AddComponent<PlayerPresentationComponent>();
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
        GameObject visualInstance = Object.Instantiate(visualPrefab, presentationRoot.transform);

        try
        {
            Animator animator = visualInstance.GetComponentInChildren<Animator>(true);
            // EditMode 没有游戏首帧，先完成 Animator 初始化再绑定表现层。
            animator.Rebind();
            animator.Update(0f);
            presentation.BindVisual(visualInstance, LoadDefine(classId));
            int attackLayerIndex = animator.GetLayerIndex("Attack Layer");
            Assert.That(attackLayerIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(0f).Within(0.001f));

            presentation.SetCombo(1);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.Zero.Within(0.001f));
            InvokePrivateMethod(presentation, "AdvanceAttackLayerFadeIn", 0.05f);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(0.5f).Within(0.01f));
            InvokePrivateMethod(presentation, "AdvanceAttackLayerFadeIn", 0.05f);
            animator.Update(0.11f);

            int attackStateHash = Animator.StringToHash("Attack");
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(attackLayerIndex);
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(attackLayerIndex);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                currentState.shortNameHash == attackStateHash ||
                nextState.shortNameHash == attackStateHash,
                Is.True,
                "远程职业左键攻击后没有进入 Attack 状态。");
        }
        finally
        {
            Object.DestroyImmediate(presentationRoot);
        }
    }

    [TestCase(2, "Assets/Resources/Characters/Wizard.prefab")]
    [TestCase(3, "Assets/Resources/Characters/Archer.prefab")]
    public void RangedBasicAttack_FiresForwardDamagesAndReturnsToPool(
        int classId,
        string visualPrefabPath)
    {
        GameObject configObject = new GameObject("RangedAttackTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 300, 340 };
        GameConfig.instance = config;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        GameObject runtimeInstance = null;
        IGameplayInput previousInput = GameplayRuntime.Instance.CurrentInput;
        TestGameplayInput testInput = new TestGameplayInput();
        List<GameObject> targetObjects = new List<GameObject>();
        HashSet<int> existingFloatingTextIds = new HashSet<int>(
            Object.FindObjectsOfType<FloatingCombatText>(true)
                .Select(item => item.GetInstanceID()));

        try
        {
            CharacterDefine define = LoadDefine(classId);
            NCharacter save = new NCharacter
            {
                id = classId,
                slotIndex = 0,
                name = $"RangedAttackTest_{classId}",
                classId = classId,
                level = 1,
                exp = 0
            };

            GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(visualPrefabPath);
            runtimeInstance = Object.Instantiate(runtimePrefab);
            GameObject visualInstance = Object.Instantiate(visualPrefab, runtimeInstance.transform);
            PlayerRuntimeController runtime = runtimeInstance.GetComponent<PlayerRuntimeController>();
            // EditMode 实例化不会执行正常 PlayMode 的 Awake，手动完成运行时组件缓存后再复现生成器装配。
            InvokePrivateMethod(runtime, "CacheComponents");
            // 完全复现 GameplayCharacterSpawner：先绑定模型，再设置位置和应用角色数据。
            runtime.BindCharacterVisual(visualInstance, define);
            runtimeInstance.transform.SetPositionAndRotation(
                new Vector3(2f, 0f, -3f),
                Quaternion.Euler(0f, 35f, 0f));
            runtime.ApplyCharacterEntryData(save, define);

            PlayerPresentationComponent presentation =
                runtimeInstance.GetComponent<PlayerPresentationComponent>();
            PlayerCombatComponent combat = runtimeInstance.GetComponent<PlayerCombatComponent>();
            PlayerRangedAttackComponent rangedAttack =
                runtimeInstance.GetComponent<PlayerRangedAttackComponent>();
            presentation.BindVisual(visualInstance, define);
            triggerProjectile legacyShooter =
                visualInstance.GetComponentInChildren<triggerProjectile>(true);
            Assert.That(legacyShooter, Is.Not.Null);
            Assert.That(legacyShooter.enabled, Is.False);
            int availableBeforeAttack = rangedAttack.AvailableProjectileCount;
            Vector3 expectedStartPosition = legacyShooter.shootPoint.position;
            if (classId == 3)
            {
                // 弩口被实体占用时必须改用 PlayerRuntime 的通用安全出生点，避免箭矢出生即回池。
                GameObject spawnBlocker = new GameObject("ArcherShootPointBlocker");
                spawnBlocker.transform.position = expectedStartPosition;
                SphereCollider blockerCollider = spawnBlocker.AddComponent<SphereCollider>();
                blockerCollider.radius = 0.03f;
                targetObjects.Add(spawnBlocker);
                expectedStartPosition =
                    runtimeInstance.transform.position +
                    Vector3.up * 1.15f +
                    runtimeInstance.transform.forward * 0.7f;
                Physics.SyncTransforms();
            }

            // 从公共输入接口进入攻击，而不是直接调用 StartFirstAttack，防止测试绕过真实故障点。
            GameplayRuntime.Instance.RegisterInput(testInput);
            testInput.LeftMouseDownValue = true;
            testInput.LeftMouseHeldValue = true;
            InvokePrivateMethod(combat, "CheckAttackInput");
            testInput.LeftMouseDownValue = false;
            testInput.LeftMouseHeldValue = false;
            Assert.That(combat.IsAttacking, Is.True);
            Assert.That(combat.CurrentCombo, Is.EqualTo(1));

            Animator animator = presentation.Animator;
            int attackLayerIndex = animator.GetLayerIndex("Attack Layer");
            InvokePrivateMethod(presentation, "AdvanceAttackLayerFadeIn", 0.1f);
            animator.Update(0.11f);
            Assert.That(animator.GetLayerWeight(attackLayerIndex), Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                animator.GetCurrentAnimatorStateInfo(attackLayerIndex).shortNameHash,
                Is.EqualTo(Animator.StringToHash("Attack")),
                "真实左键输入没有让远程职业进入 Attack 状态。");

            // 弓箭手每次点击同帧发射；法师仍按动画配置的释放时间生成火球。
            float releaseDelay = define.basicAttackDuration * define.projectileReleaseRatio;
            if (classId == 3)
            {
                Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));
            }
            else
            {
                InvokePrivateMethod(combat, "TickEventlessAttackReleaseDelay", releaseDelay + 0.01f);
            }
            Assert.That(combat.TryReleaseRangedBasicAttack(), Is.False);
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));

            PlayerBasicAttackProjectile projectile =
                Object.FindObjectsOfType<PlayerBasicAttackProjectile>(true)
                    .FirstOrDefault(item => item.gameObject.activeSelf);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(Vector3.Distance(projectile.transform.position, expectedStartPosition), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(projectile.transform.forward, runtimeInstance.transform.forward), Is.LessThan(0.01f));
            Assert.That(
                projectile.GetComponentInChildren<MeshRenderer>(true),
                Is.Not.Null,
                "远程普攻仍在使用无职业外观的基础球体。");
            Assert.That(
                projectile.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled),
                Is.True,
                "对象池取出的投射物 Renderer 没有恢复启用。");
            Assert.That(
                projectile.GetComponentsInChildren<TrailRenderer>(true)
                    .All(trail => trail.enabled && trail.emitting),
                Is.True,
                "对象池取出的投射物 Trail 没有恢复播放。");

            if (classId == 3)
            {
                // 攻击间隔内快速连点不能绕过攻速限制。
                testInput.LeftMouseDownValue = true;
                testInput.LeftMouseHeldValue = true;
                InvokePrivateMethod(combat, "CheckAttackInput");
                testInput.LeftMouseDownValue = false;
                testInput.LeftMouseHeldValue = false;
                Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));

                // 冷却结束后即使没有新的按下帧，只要仍处于长按状态就会自动发射下一箭。
                InvokePrivateMethod(
                    combat,
                    "TickArcherBasicAttackCooldown",
                    define.basicAttackDuration + 0.01f);
                testInput.LeftMouseHeldValue = true;
                InvokePrivateMethod(combat, "CheckAttackInput");
                testInput.LeftMouseHeldValue = false;
                Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(2));
                PlayerBasicAttackProjectile secondClickProjectile =
                    Object.FindObjectsOfType<PlayerBasicAttackProjectile>(true)
                        .First(item => item.gameObject.activeSelf && item != projectile);
                Assert.That(combat.TryReleaseRangedBasicAttack(), Is.False,
                    "同一个点击令牌被动画事件重复发射。 ");
                secondClickProjectile.Release();
                Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));
            }

            Vector3 midpoint = (Vector3)InvokePrivateMethodWithResult(
                projectile,
                "EvaluateTrajectoryPosition",
                0.5f);
            Vector3 straightMidpoint =
                expectedStartPosition +
                runtimeInstance.transform.forward *
                (define.projectileSpeed * define.projectileLifetime * 0.5f);

            if (classId == 2)
            {
                Assert.That(projectile.name, Does.Contain("MagicMissile 1"));
                Assert.That(projectile.Trajectory, Is.EqualTo(CharacterProjectileTrajectory.Arc));
                Assert.That(projectile.ExplosionRadius, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(projectile.VisualScaleMultiplier, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(projectile.transform.localScale, Is.EqualTo(Vector3.one * 0.56f));
                Assert.That(midpoint.y, Is.EqualTo(straightMidpoint.y + 3f).Within(0.01f));

                GameObject directTargetObject = CreateTestTarget(
                    "WizardDirectTarget",
                    projectile.transform.position + Vector3.right * 0.2f,
                    targetObjects,
                    out CubeTest directTarget,
                    out BoxCollider directCollider);
                GameObject nearbyTargetObject = CreateTestTarget(
                    "WizardNearbyTarget",
                    projectile.transform.position + Vector3.left * 0.7f,
                    targetObjects,
                    out CubeTest nearbyTarget,
                    out BoxCollider ignoredNearbyCollider);
                GameObject extraColliderObject = new GameObject("SecondCollider");
                extraColliderObject.transform.SetParent(nearbyTargetObject.transform, false);
                extraColliderObject.AddComponent<SphereCollider>();

                CreateTestTarget(
                    "WizardOutsideTarget",
                    projectile.transform.position + Vector3.right * 2.5f,
                    targetObjects,
                    out CubeTest outsideTarget,
                    out BoxCollider ignoredOutsideCollider);
                Physics.SyncTransforms();
                InvokePrivateMethod(projectile, "OnTriggerEnter", directCollider);

                Assert.That(directTarget.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(
                    nearbyTarget.transform.localScale.x,
                    Is.EqualTo(0.9f).Within(0.001f),
                    "法师爆炸范围内多 Collider 目标没有按 FighterInterface 去重。");
                Assert.That(outsideTarget.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(directTargetObject, Is.Not.Null);
            }
            else
            {
                Assert.That(projectile.name, Does.Contain("Human Bolt"));
                Assert.That(projectile.Trajectory, Is.EqualTo(CharacterProjectileTrajectory.Straight));
                Assert.That(projectile.ExplosionRadius, Is.Zero);
                Assert.That(projectile.VisualScaleMultiplier, Is.EqualTo(1f).Within(0.001f));
                Assert.That(midpoint.y, Is.EqualTo(straightMidpoint.y).Within(0.01f));

                CreateTestTarget(
                    "ArcherDirectTarget",
                    projectile.transform.position + runtimeInstance.transform.forward * 0.18f,
                    targetObjects,
                    out CubeTest target,
                    out BoxCollider targetCollider);
                targetCollider.size = Vector3.one * 0.02f;
                CreateTestTarget(
                    "ArcherFarTarget",
                    projectile.transform.position + runtimeInstance.transform.forward * 0.22f,
                    targetObjects,
                    out CubeTest farTarget,
                    out BoxCollider farTargetCollider);
                farTargetCollider.size = Vector3.one * 0.02f;

                // 模拟 Slime2 的 shootPos/攻击范围：Trigger 虽然位于 FighterInterface 子级，
                // 也不能在箭碰到正式身体前提前消费这支箭。
                GameObject fighterTriggerObject = new GameObject("ArcherIgnoredFighterTrigger");
                fighterTriggerObject.transform.SetParent(target.transform, false);
                fighterTriggerObject.transform.position =
                    projectile.transform.position + runtimeInstance.transform.forward * 0.02f;
                SphereCollider fighterTrigger = fighterTriggerObject.AddComponent<SphereCollider>();
                fighterTrigger.isTrigger = true;
                fighterTrigger.radius = 0.01f;
                Physics.SyncTransforms();

                // 12m/s 的箭矢一帧移动约 0.24m；目标厚度仅 0.02m，只有连续扫掠才能稳定命中。
                InvokePrivateMethod(projectile, "FixedUpdate");
                Assert.That(target.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(farTarget.transform.localScale.x, Is.EqualTo(1f).Within(0.001f),
                    "同一支箭没有优先命中路径上最近的目标。 ");
                Assert.That(projectile.IsReleased, Is.True);
                Assert.That(rangedAttack.ActiveProjectileCount, Is.Zero);
                Assert.That(projectile.GetComponent<SphereCollider>().enabled, Is.False);

                // 回池后再次收到回调也不能重复伤害。
                InvokePrivateMethod(projectile, "OnTriggerEnter", targetCollider);
                Assert.That(target.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
            }

            Assert.That(projectile.IsReleased, Is.True);
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(0));
            Assert.That(rangedAttack.AvailableProjectileCount, Is.EqualTo(availableBeforeAttack));

            // 再攻击一次验证回池对象可重新显示，且每次仍然只取出一个投射物。
            // 第一支箭的测试目标必须先移出物理查询，弓箭手的出生点阻挡物则继续保留。
            for (int targetIndex = 0; targetIndex < targetObjects.Count; targetIndex++)
            {
                GameObject targetObject = targetObjects[targetIndex];
                if (targetObject != null && targetObject.name != "ArcherShootPointBlocker")
                {
                    targetObject.SetActive(false);
                }
            }
            Physics.SyncTransforms();

            combat.ResetCombo();
            InvokePrivateMethod(
                combat,
                "TickArcherBasicAttackCooldown",
                define.basicAttackDuration + 0.01f);
            testInput.LeftMouseDownValue = true;
            testInput.LeftMouseHeldValue = true;
            InvokePrivateMethod(combat, "CheckAttackInput");
            testInput.LeftMouseDownValue = false;
            testInput.LeftMouseHeldValue = false;
            if (classId == 2)
            {
                InvokePrivateMethod(combat, "TickEventlessAttackReleaseDelay", releaseDelay + 0.01f);
            }
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));
            PlayerBasicAttackProjectile reusedProjectile =
                Object.FindObjectsOfType<PlayerBasicAttackProjectile>(true)
                    .FirstOrDefault(item => item.gameObject.activeSelf);
            Assert.That(reusedProjectile, Is.Not.Null);
            Assert.That(
                reusedProjectile.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled),
                Is.True);
            Assert.That(
                reusedProjectile.GetComponentsInChildren<TrailRenderer>(true)
                    .All(trail => trail.enabled && trail.emitting),
                Is.True);
            Assert.That(
                reusedProjectile.GetComponent<SphereCollider>().enabled,
                Is.EqualTo(classId == 2));

            if (classId == 3)
            {
                // 使用项目真实 SlimeCo + CharacterController 验证公共 FighterInterface 伤害链。
                GameObject slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefabPath);
                Assert.That(slimePrefab, Is.Not.Null);
                GameObject slimeObject = Object.Instantiate(slimePrefab);
                slimeObject.transform.position = expectedStartPosition + runtimeInstance.transform.forward * 0.8f;
                SlimeCo slime = slimeObject.GetComponent<SlimeCo>();
                Assert.That(slime, Is.Not.Null);
                Assert.That(slimeObject.GetComponent<CharacterController>(), Is.Not.Null);
                slime.Hp = 1000;
                slime.HpMax = 1000;
                targetObjects.Add(slimeObject);
                Physics.SyncTransforms();

                for (int fixedStep = 0;
                     fixedStep < 5 &&
                     !reusedProjectile.IsReleased;
                     fixedStep++)
                {
                    InvokePrivateMethod(reusedProjectile, "FixedUpdate");
                }

                Assert.That(slime.Hp, Is.LessThan(1000), "箭矢没有对真实 SlimeCo 造成伤害。 ");
                Assert.That(reusedProjectile.IsReleased, Is.True);
            }
            else
            {
                reusedProjectile.Release();
            }
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(0));
            Assert.That(rangedAttack.AvailableProjectileCount, Is.EqualTo(availableBeforeAttack));
        }
        finally
        {
            GameplayRuntime.Instance.UnregisterInput(testInput);
            if (previousInput != null)
            {
                GameplayRuntime.Instance.RegisterInput(previousInput);
            }

            FloatingCombatText[] floatingTexts = Object.FindObjectsOfType<FloatingCombatText>(true);
            for (int i = 0; i < floatingTexts.Length; i++)
            {
                if (floatingTexts[i] != null &&
                    !existingFloatingTextIds.Contains(floatingTexts[i].GetInstanceID()))
                {
                    Object.DestroyImmediate(floatingTexts[i].gameObject);
                }
            }

            for (int i = 0; i < targetObjects.Count; i++)
            {
                if (targetObjects[i] != null)
                {
                    Object.DestroyImmediate(targetObjects[i]);
                }
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

    [Test]
    public void ArcherSpawnOverlap_IsIgnoredAndArrowOnlyReleasesOnLaterHitOrLifetime()
    {
        GameObject configObject = new GameObject("ArcherCloseRangeTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 300, 340 };
        GameConfig.instance = config;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        GameObject runtimeInstance = null;
        GameObject slimeObject = null;
        List<GameObject> clusteredTargetObjects = new List<GameObject>();
        HashSet<int> existingFloatingTextIds = new HashSet<int>(
            Object.FindObjectsOfType<FloatingCombatText>(true)
                .Select(item => item.GetInstanceID()));

        try
        {
            CharacterDefine archerDefine = LoadDefine(3);
            NCharacter save = new NCharacter
            {
                id = 3,
                slotIndex = 0,
                name = "ArcherCloseRangeTest",
                classId = 3,
                level = 1,
                exp = 0
            };

            GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
            GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Resources/Characters/Archer.prefab");
            runtimeInstance = Object.Instantiate(runtimePrefab);
            GameObject visualInstance = Object.Instantiate(visualPrefab, runtimeInstance.transform);
            PlayerRuntimeController runtime = runtimeInstance.GetComponent<PlayerRuntimeController>();
            PlayerRangedAttackComponent rangedAttack =
                runtimeInstance.GetComponent<PlayerRangedAttackComponent>();
            FieldInfo prewarmCountField = typeof(PlayerRangedAttackComponent).GetField(
                "prewarmCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(prewarmCountField, Is.Not.Null);
            prewarmCountField.SetValue(rangedAttack, 1);
            // EditMode 不走 PlayerRuntimeController.Awake，先补齐依赖再绑定职业表现。
            InvokePrivateMethod(runtime, "CacheComponents");
            runtime.BindCharacterVisual(visualInstance, archerDefine);
            runtimeInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            runtime.ApplyCharacterEntryData(save, archerDefine);

            triggerProjectile legacyShooter =
                visualInstance.GetComponentInChildren<triggerProjectile>(true);
            Assert.That(legacyShooter, Is.Not.Null);
            Assert.That(legacyShooter.shootPoint, Is.Not.Null);

            GameObject slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlimePrefabPath);
            slimeObject = Object.Instantiate(slimePrefab);
            SlimeCo slime = slimeObject.GetComponent<SlimeCo>();
            CharacterController slimeController = slimeObject.GetComponent<CharacterController>();
            Assert.That(slime, Is.Not.Null);
            Assert.That(slimeController, Is.Not.Null);
            slime.Hp = 1000;
            slime.HpMax = 1000;

            // 史莱姆在 Fire 之前就覆盖真实弩口，复现“有音效但箭同帧回池”的旧故障。
            Vector3 slimeCenterOffset = slimeObject.transform.TransformVector(slimeController.center);
            slimeObject.transform.position = legacyShooter.shootPoint.position - slimeCenterOffset;

            // 再放一个同样覆盖弩口的目标，验证出生时已经重叠的所有正式怪物身体都会被本支箭忽略。
            CreateTestTarget(
                "ArcherClusterSideTarget",
                legacyShooter.shootPoint.position +
                runtimeInstance.transform.right * 0.35f +
                runtimeInstance.transform.forward * 0.1f,
                clusteredTargetObjects,
                out CubeTest clusteredSideTarget,
                out BoxCollider clusteredSideCollider);

            // 弓箭手普通箭按本次需求穿过环境，只由正式怪物身体或寿命结束触发回收。
            GameObject wallObject = new GameObject("ArcherIgnoredEnvironmentWall");
            wallObject.transform.position =
                legacyShooter.shootPoint.position + runtimeInstance.transform.forward * 0.6f;
            BoxCollider wallCollider = wallObject.AddComponent<BoxCollider>();
            wallCollider.size = new Vector3(1f, 2f, 0.1f);
            clusteredTargetObjects.Add(wallObject);
            Physics.SyncTransforms();

            int availableBeforeFire = rangedAttack.AvailableProjectileCount;
            PlayerBasicAttackProjectile projectile = rangedAttack.Fire();

            Assert.That(projectile, Is.Not.Null);
            Assert.That(
                Vector3.Distance(projectile.transform.position, legacyShooter.shootPoint.position),
                Is.LessThan(0.01f),
                "贴脸目标被误当成墙后，箭矢没有从真实弩口生成。 ");
            Assert.That(slime.Hp, Is.EqualTo(1000), "出生时重叠的怪物仍让箭矢同帧命中。 ");
            Assert.That(clusteredSideTarget.transform.localScale.x, Is.EqualTo(1f).Within(0.001f),
                "怪群中出生时重叠的第二个目标被错误伤害。 ");
            Assert.That(projectile.IsReleased, Is.False);
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(rangedAttack.ActiveProjectileCount, Is.EqualTo(1));
            Assert.That(
                projectile.GetComponent<SphereCollider>().enabled,
                Is.False,
                "弓箭手箭矢应关闭 Trigger，只使用逐物理帧扫掠。 ");
            Assert.That(
                projectile.GetComponentsInChildren<Renderer>(true).All(renderer => renderer.enabled),
                Is.True);
            TrailRenderer[] impactTrails = projectile.GetComponentsInChildren<TrailRenderer>(true);
            Assert.That(impactTrails.Length, Is.GreaterThan(0));
            Assert.That(impactTrails.All(trail => trail.enabled && trail.emitting), Is.True);

            // 即使第三方资源或 Unity 手动派发 Trigger，弓箭手也只接受后续扫掠到的正式怪物身体。
            InvokePrivateMethod(projectile, "OnTriggerEnter", slimeController);
            InvokePrivateMethod(projectile, "OnTriggerEnter", clusteredSideCollider);
            InvokePrivateMethod(projectile, "OnTriggerEnter", wallCollider);
            Assert.That(projectile.IsReleased, Is.False);
            Assert.That(slime.Hp, Is.EqualTo(1000));
            Assert.That(clusteredSideTarget.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));

            Vector3 flightStart = projectile.transform.position;
            InvokePrivateMethod(projectile, "FixedUpdate");
            Assert.That(projectile.IsReleased, Is.False);
            Assert.That(projectile.gameObject.activeSelf, Is.True);
            Assert.That(
                Vector3.Distance(flightStart, projectile.transform.position),
                Is.GreaterThan(0.01f),
                "箭矢没有飞离出生时重叠的怪物和环境碰撞体。 ");

            int lifetimeSteps = Mathf.CeilToInt(
                archerDefine.projectileLifetime / Time.fixedDeltaTime);
            for (int step = 1; step < lifetimeSteps; step++)
            {
                InvokePrivateMethod(projectile, "FixedUpdate");
            }

            Assert.That(projectile.IsReleased, Is.False, "箭矢在寿命检查前被其他碰撞体回收。 ");
            InvokePrivateMethod(projectile, "Update");
            Assert.That(projectile.IsReleased, Is.True);
            Assert.That(
                Vector3.Distance(flightStart, projectile.transform.position),
                Is.EqualTo(archerDefine.projectileSpeed * archerDefine.projectileLifetime)
                    .Within(0.05f),
                "未命中后箭矢没有飞满配置寿命对应的距离。 ");
            Assert.That(slime.Hp, Is.EqualTo(1000));
            Assert.That(clusteredSideTarget.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(rangedAttack.ActiveProjectileCount, Is.Zero);
            Assert.That(rangedAttack.AvailableProjectileCount, Is.EqualTo(availableBeforeFire));

            // 再次从池中取出时仍关闭 Trigger，但 Renderer、Trail 和寿命状态必须完整恢复。
            slimeObject.SetActive(false);
            for (int i = 0; i < clusteredTargetObjects.Count; i++)
            {
                clusteredTargetObjects[i].SetActive(false);
            }
            Physics.SyncTransforms();
            PlayerBasicAttackProjectile reusedProjectile = rangedAttack.Fire();
            Assert.That(reusedProjectile, Is.SameAs(projectile));
            Assert.That(reusedProjectile.IsReleased, Is.False);
            Assert.That(reusedProjectile.GetComponent<SphereCollider>().enabled, Is.False);
            Assert.That(
                reusedProjectile.GetComponentsInChildren<TrailRenderer>(true)
                    .All(trail => trail.enabled && trail.emitting),
                Is.True);
            reusedProjectile.Release();
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

            if (slimeObject != null)
            {
                Object.DestroyImmediate(slimeObject);
            }
            for (int i = 0; i < clusteredTargetObjects.Count; i++)
            {
                if (clusteredTargetObjects[i] != null)
                {
                    Object.DestroyImmediate(clusteredTargetObjects[i]);
                }
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

    private static CharacterDefine LoadDefine(int classId)
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterDataPath);
        Assert.That(json, Is.Not.Null);
        CharacterDefineTable table = JsonUtility.FromJson<CharacterDefineTable>(json.text);
        CharacterDefine define = table.characters.FirstOrDefault(item => item.classId == classId);
        Assert.That(define, Is.Not.Null, $"职业配置不存在：classId={classId}");
        return define;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"测试目标缺少私有方法：{methodName}");
        method.Invoke(target, parameters);
    }

    private static object InvokePrivateMethodWithResult(
        object target,
        string methodName,
        params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"测试目标缺少私有方法：{methodName}");
        return method.Invoke(target, parameters);
    }

    private static GameObject CreateTestTarget(
        string objectName,
        Vector3 position,
        List<GameObject> cleanupList,
        out CubeTest target,
        out BoxCollider targetCollider)
    {
        GameObject targetObject = new GameObject(objectName);
        targetObject.transform.position = position;
        target = targetObject.AddComponent<CubeTest>();
        targetCollider = targetObject.AddComponent<BoxCollider>();
        cleanupList.Add(targetObject);
        return targetObject;
    }

    private static void AssertParameter(
        EditorAnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        AnimatorControllerParameter parameter = controller.parameters
            .FirstOrDefault(item => item.name == parameterName);
        Assert.That(parameter, Is.Not.Null, $"远程职业 Animator 缺少参数：{parameterName}");
        Assert.That(parameter.type, Is.EqualTo(expectedType));
    }

    private static void AssertSimpleTransitionsUseFixedTenthSecond(
        EditorAnimatorController controller)
    {
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            AnimatorStateMachine stateMachine = layer.stateMachine;
            IEnumerable<AnimatorStateTransition> transitions = stateMachine.anyStateTransitions
                .Concat(stateMachine.states.SelectMany(child => child.state.transitions));
            foreach (AnimatorStateTransition transition in transitions)
            {
                Assert.That(transition.hasFixedDuration, Is.True);
                Assert.That(transition.duration, Is.EqualTo(0.1f).Within(0.001f));
            }
        }
    }

    /// <summary>
    /// 测试输入源：只控制本次用到的左键，其余输入保持默认值。
    /// 用接口替身可以验证真实输入入口，而不依赖编辑器鼠标状态。
    /// </summary>
    private sealed class TestGameplayInput : IGameplayInput
    {
        public bool LeftMouseDownValue { get; set; }
        public bool LeftMouseHeldValue { get; set; }
        public bool LeftMouseUpValue { get; set; }

        public float XInput => 0f;
        public float YInput => 0f;
        public Vector3 MouseInput => Vector3.zero;
        public bool LeftMouseDown => LeftMouseDownValue;
        public bool LeftMouseHeld => LeftMouseHeldValue;
        public bool LeftMouseUp => LeftMouseUpValue;
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
