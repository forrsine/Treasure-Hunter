#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战士蓄力重击回归测试：保护配置数值、状态机、延迟攻击盒、动画固定和公共 UI 装配。
/// </summary>
public sealed class WarriorChargedAttackTests
{
    private const string CharacterDataPath = "Assets/Resources/Data/CharacterDefine.json";
    private const string PlayerRuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";
    private const string WarriorPrefabPath = "Assets/Resources/Characters/Warrior.prefab";
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";

    [Test]
    public void WarriorChargeConfiguration_UsesConfirmedValuesOnlyForWarrior()
    {
        CharacterDefineTable table = LoadCharacterTable();
        CharacterDefine warrior = table.characters.First(item => item.classId == 1);
        CharacterChargedAttackDefine charge = warrior.chargeAttack;

        Assert.That(charge, Is.Not.Null);
        Assert.That(charge.enabled, Is.True);
        Assert.That(charge.maxChargeDuration, Is.EqualTo(1.6f).Within(0.001f));
        Assert.That(charge.maxDamageMultiplier, Is.EqualTo(3f).Within(0.001f));
        Assert.That(charge.holdNormalizedTime, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(charge.releaseHitDelay, Is.EqualTo(0.08f).Within(0.001f));
        Assert.That(charge.movementSpeedLimit, Is.EqualTo(1.5f).Within(0.001f));
        Assert.That(charge.fullChargeDamageReduction, Is.EqualTo(0.15f).Within(0.001f));
        Assert.That(charge.fullChargeAreaRadius, Is.EqualTo(3f).Within(0.001f));
        Assert.That(charge.fullChargeSpinDuration, Is.EqualTo(0.6f).Within(0.001f));
        Assert.That(charge.fullChargeSpinDegrees, Is.EqualTo(360f).Within(0.001f));

        foreach (CharacterDefine otherClass in table.characters.Where(item => item.classId != 1))
        {
            Assert.That(
                otherClass.chargeAttack == null || !otherClass.chargeAttack.enabled,
                Is.True,
                $"{otherClass.classKey} 不应启用战士蓄力机制。");
        }
    }

    [Test]
    public void ShortClick_ReleasesOneTimesAttackAfterConfiguredDelay()
    {
        GameObject runtimeObject = CreateWarriorRuntime(out PlayerRuntimeController runtime, out _);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
        SphereCollider hitbox = runtimeObject.transform.Find("AttackHitbox").GetComponent<SphereCollider>();
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            Assert.That(chargedAttack.TryHandleBasicAttackInput(input), Is.True);
            Assert.That(combat.IsAttacking, Is.True);
            Assert.That(hitbox.enabled, Is.False, "蓄力前摇不能提前打开攻击盒。");

            input.LeftMouseDownValue = false;
            input.LeftMouseHeldValue = false;
            input.LeftMouseUpValue = true;
            Assert.That(chargedAttack.TryHandleBasicAttackInput(input), Is.True);
            Assert.That(combat.ActiveBasicAttackDamageMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(chargedAttack.IsFullChargeSpinActive, Is.False);

            InvokePrivateMethod(combat, "TickEventlessAttackReleaseDelay", 0.079f);
            Assert.That(hitbox.enabled, Is.False);
            InvokePrivateMethod(combat, "TickEventlessAttackReleaseDelay", 0.002f);
            Assert.That(hitbox.enabled, Is.True);
            Assert.That(combat.AttackHitWindowId, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void HoldingCharge_InterpolatesTwoAndThreeTimesAndCapsAtMaximum()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            Assert.That(chargedAttack.TryHandleBasicAttackInput(input), Is.True);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);

            Assert.That(chargedAttack.IsHoldingCharge, Is.True);
            Assert.That(chargedAttack.MovementSpeedLimit, Is.EqualTo(1.5f).Within(0.001f));
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 0.8f);
            Assert.That(chargedAttack.ChargeProgress, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(chargedAttack.CurrentDamageMultiplier, Is.EqualTo(2f).Within(0.001f));

            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 0.8f);
            Assert.That(chargedAttack.CurrentDamageMultiplier, Is.EqualTo(3f).Within(0.001f));
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 5f);
            Assert.That(chargedAttack.ChargeProgress, Is.EqualTo(1f).Within(0.001f));
            Assert.That(chargedAttack.CurrentDamageMultiplier, Is.EqualTo(3f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void FullChargeGuard_ActivatesAtMaximumPersistsThroughReleaseAndThenClears()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);

            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 1.59f);
            Assert.That(chargedAttack.IsFullChargeGuardActive, Is.False);
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 0.02f);
            Assert.That(chargedAttack.IsFullChargeGuardActive, Is.True);
            Assert.That(chargedAttack.FullChargeDamageReduction, Is.EqualTo(0.15f).Within(0.001f));

            input.LeftMouseHeldValue = false;
            input.LeftMouseUpValue = true;
            chargedAttack.TryHandleBasicAttackInput(input);
            Assert.That(chargedAttack.IsFullChargeGuardActive, Is.True, "释放动画期间应继续获得满蓄力减伤。");

            combat.CancelControlledBasicAttack();
            chargedAttack.TryHandleBasicAttackInput(input);
            Assert.That(chargedAttack.IsChargeAttackActive, Is.False);
            Assert.That(chargedAttack.IsFullChargeGuardActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void FullChargeRelease_RotatesByAbsoluteProgressAndRestoresInitialFacing()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            runtimeObject.transform.rotation = Quaternion.Euler(0f, 37f, 0f);
            Quaternion initialRotation = runtimeObject.transform.rotation;
            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 1.6f);

            input.LeftMouseHeldValue = false;
            input.LeftMouseUpValue = true;
            chargedAttack.TryHandleBasicAttackInput(input);
            Assert.That(chargedAttack.IsFullChargeSpinActive, Is.True);

            InvokePrivateMethod(chargedAttack, "TickFullChargeSpin", 0.3f);
            Assert.That(
                Quaternion.Angle(initialRotation, runtimeObject.transform.rotation),
                Is.EqualTo(180f).Within(0.5f),
                "旋转过半时应到达绝对180度位置，而不是依赖逐帧累加。 ");

            InvokePrivateMethod(chargedAttack, "TickFullChargeSpin", 0.3f);
            Assert.That(chargedAttack.IsFullChargeSpinActive, Is.False);
            Assert.That(
                Quaternion.Angle(initialRotation, runtimeObject.transform.rotation),
                Is.LessThan(0.1f),
                "360度旋转完成后必须恢复释放前朝向。 ");
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void CancelDuringFullChargeSpin_ImmediatelyRestoresInitialFacing()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            runtimeObject.transform.rotation = Quaternion.Euler(0f, 73f, 0f);
            Quaternion initialRotation = runtimeObject.transform.rotation;
            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 1.6f);
            input.LeftMouseHeldValue = false;
            input.LeftMouseUpValue = true;
            chargedAttack.TryHandleBasicAttackInput(input);
            InvokePrivateMethod(chargedAttack, "TickFullChargeSpin", 0.2f);

            chargedAttack.CancelCharge();

            Assert.That(chargedAttack.IsFullChargeSpinActive, Is.False);
            Assert.That(chargedAttack.IsChargeAttackActive, Is.False);
            Assert.That(
                Quaternion.Angle(initialRotation, runtimeObject.transform.rotation),
                Is.LessThan(0.1f));
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void HoldingCharge_PinsOnlyAttackLayerAndReleaseResumesAnimation()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        PlayerPresentationComponent presentation = runtime.Presentation;
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);
            int attackLayer = animator.GetLayerIndex("Attack Layer");

            animator.Update(0.25f);
            InvokePrivateMethod(presentation, "PinSimpleAttackPoseIfNeeded");
            float heldTime = animator.GetCurrentAnimatorStateInfo(attackLayer).normalizedTime;
            Assert.That(heldTime, Is.EqualTo(0.2f).Within(0.03f));
            Assert.That(animator.speed, Is.EqualTo(1f).Within(0.001f), "不能用全局 Animator.speed 冻结下半身。");

            input.LeftMouseHeldValue = false;
            input.LeftMouseUpValue = true;
            chargedAttack.TryHandleBasicAttackInput(input);
            animator.Update(0.15f);
            float releasedTime = animator.GetCurrentAnimatorStateInfo(attackLayer).normalizedTime;
            Assert.That(releasedTime, Is.GreaterThan(heldTime + 0.05f));
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void CancelCharge_DisablesHitboxRestoresAnimationAndClearsMultiplier()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
        SphereCollider hitbox = runtimeObject.transform.Find("AttackHitbox").GetComponent<SphereCollider>();
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };

        try
        {
            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 1.6f);

            chargedAttack.CancelCharge();

            Assert.That(chargedAttack.IsChargeAttackActive, Is.False);
            Assert.That(combat.IsAttacking, Is.False);
            Assert.That(combat.ActiveBasicAttackDamageMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hitbox.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    [Test]
    public void ChargedDamage_MultipliesExistingCriticalResultAndRoundsToInteger()
    {
        GameObject runtimeObject = CreateWarriorRuntime(out PlayerRuntimeController runtime, out _);
        PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
        IArchitecture architecture = TreasureHunterArchitecture.Interface;

        try
        {
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 1, level = 1 },
                LoadCharacterTable().characters.First(item => item.classId == 1)));
            Assert.That(combat.BeginControlledBasicAttack(), Is.True);
            Assert.That(combat.ReleaseControlledBasicAttack(3f, 0.08f), Is.True);

            int damage = combat.RollAttackDamage(out bool isCritical);
            Assert.That(isCritical, Is.False);
            Assert.That(damage, Is.EqualTo(102));
        }
        finally
        {
            Object.DestroyImmediate(runtimeObject);
            architecture.Deinit();
        }
    }

    [Test]
    public void FullChargeArea_HitsEveryDirectionOnceAndKeepsWeaponColliderDisabled()
    {
        GameObject runtimeObject = CreateWarriorRuntime(out PlayerRuntimeController runtime, out _);
        PlayerCombatComponent combat = runtimeObject.GetComponent<PlayerCombatComponent>();
        SphereCollider hitbox = runtimeObject.transform.Find("AttackHitbox").GetComponent<SphereCollider>();
        IArchitecture architecture = TreasureHunterArchitecture.Interface;
        List<GameObject> targetObjects = new List<GameObject>();
        SkillVisualPool visualPool = SkillVisualPool.Instance;
        GameObject ownedPoolObject = null;

        try
        {
            if (visualPool == null)
            {
                ownedPoolObject = new GameObject("WarriorChargedAttackTestVisualPool");
                visualPool = ownedPoolObject.AddComponent<SkillVisualPool>();
            }

            CharacterDefine warrior = LoadCharacterTable().characters.First(item => item.classId == 1);
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 1, level = 1 },
                warrior));

            CubeTest front = CreateTarget("SpinFront", runtimeObject.transform.position + Vector3.forward * 2.4f, targetObjects);
            CubeTest back = CreateTarget("SpinBack", runtimeObject.transform.position + Vector3.back * 2.4f, targetObjects);
            CubeTest left = CreateTarget("SpinLeft", runtimeObject.transform.position + Vector3.left * 2.4f, targetObjects);
            CubeTest right = CreateTarget("SpinRight", runtimeObject.transform.position + Vector3.right * 2.4f, targetObjects);
            GameObject duplicateCollider = new GameObject("DuplicateCollider");
            duplicateCollider.transform.SetParent(right.transform, false);
            duplicateCollider.AddComponent<SphereCollider>();
            CubeTest outside = CreateTarget("SpinOutside", runtimeObject.transform.position + Vector3.forward * 4.2f, targetObjects);

            Assert.That(combat.BeginControlledBasicAttack(), Is.True);
            Assert.That(combat.ReleaseControlledBasicAttack(3f, 0.08f, 3f), Is.True);
            Physics.SyncTransforms();
            InvokePrivateMethod(combat, "TickEventlessAttackReleaseDelay", 0.081f);

            Assert.That(front.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(back.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(left.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(right.transform.localScale.x, Is.EqualTo(0.9f).Within(0.001f),
                "同一目标拥有多个Collider时只能受到一次范围伤害。 ");
            Assert.That(outside.transform.localScale.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(hitbox.enabled, Is.False, "满蓄力范围扫描不能再开启普通武器攻击盒。 ");
            Assert.That(combat.AttackHitWindowId, Is.EqualTo(1));
            Assert.That(Object.FindObjectsOfType<PlayerChargedSpinEffect>(true).Any(effect => effect.Radius == 3f), Is.True);
        }
        finally
        {
            ReleaseTestVisuals();
            foreach (GameObject targetObject in targetObjects)
            {
                Object.DestroyImmediate(targetObject);
            }

            Object.DestroyImmediate(runtimeObject);
            if (ownedPoolObject != null)
            {
                Object.DestroyImmediate(ownedPoolObject);
            }
            architecture.Deinit();
        }
    }

    [Test]
    public void ChargedSpinEffect_UsesConfiguredRadiusAndReturnsToPool()
    {
        SkillVisualPool visualPool = SkillVisualPool.Instance;
        GameObject ownedPoolObject = null;

        try
        {
            if (visualPool == null)
            {
                ownedPoolObject = new GameObject("WarriorSpinEffectTestPool");
                visualPool = ownedPoolObject.AddComponent<SkillVisualPool>();
            }

            PlayerChargedSpinEffect first = PlayerChargedSpinEffect.Play(Vector3.zero, 3f);
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Radius, Is.EqualTo(3f).Within(0.001f));
            Assert.That(first.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(2));

            InvokePrivateMethod(first, "Release");
            Assert.That(first.gameObject.activeSelf, Is.False);

            PlayerChargedSpinEffect reused = PlayerChargedSpinEffect.Play(Vector3.one, 3f);
            Assert.That(reused, Is.SameAs(first), "圆环结束后应从对象池复用，而不是重复创建。 ");
            InvokePrivateMethod(reused, "Release");
        }
        finally
        {
            if (ownedPoolObject != null)
            {
                Object.DestroyImmediate(ownedPoolObject);
            }
        }
    }

    [Test]
    public void FullChargeDamageReduction_MultipliesProfessionReductionAndRoundsOnce()
    {
        GameObject configObject = new GameObject("FullChargeGuardTestGameConfig");
        GameConfig config = configObject.AddComponent<GameConfig>();
        config.Lv_NextExp = new[] { 50, 60 };
        config.Lv_Hpmax = new[] { 360, 400 };
        GameConfig.instance = config;
        IArchitecture architecture = TreasureHunterArchitecture.Interface;

        try
        {
            CharacterDefine warrior = LoadCharacterTable().characters.First(item => item.classId == 1);
            architecture.SendCommand(new InitializePlayerCommand(
                new NCharacter { classId = 1, level = 1 },
                warrior));

            PlayerDamageResult normalResult = architecture.SendCommand(
                new TakePlayerDamageCommand(100, false));
            architecture.SendCommand(new FullHealPlayerCommand());
            PlayerDamageResult guardedResult = architecture.SendCommand(
                new TakePlayerDamageCommand(100, false, warrior.chargeAttack.fullChargeDamageReduction));

            Assert.That(normalResult.ActualDamage, Is.EqualTo(80));
            Assert.That(guardedResult.ActualDamage, Is.EqualTo(68));
        }
        finally
        {
            architecture.Deinit();
            GameConfig.instance = null;
            Object.DestroyImmediate(configObject);
        }
    }

    [Test]
    public void FullChargeFeedback_UsesYellowWithoutDisablingRendererAndRestoresAfterHit()
    {
        GameObject runtimeObject = CreateWarriorRuntime(
            out PlayerRuntimeController runtime,
            out Animator animator);
        PlayerChargedAttackComponent chargedAttack = runtime.ChargedAttack;
        PlayerHealthComponent health = runtimeObject.GetComponent<PlayerHealthComponent>();
        TestGameplayInput input = new TestGameplayInput { LeftMouseDownValue = true, LeftMouseHeldValue = true };
        Material[] runtimeMaterials = null;

        try
        {
            health.Initialize(runtime);
            SkinnedMeshRenderer renderer = runtime.Presentation.PrimaryRenderer;
            Assert.That(renderer, Is.Not.Null);
            runtimeMaterials = renderer.materials;
            Color[] originalColors = runtimeMaterials.Select(material => material.color).ToArray();
            bool rendererEnabledBeforeFlash = renderer.enabled;

            chargedAttack.TryHandleBasicAttackInput(input);
            input.LeftMouseDownValue = false;
            AdvanceAnimatorUntilHolding(chargedAttack, animator, input);
            InvokePrivateMethod(chargedAttack, "AdvanceCharge", 1.6f);
            health.TickHitFlash();

            Color chargeColor = GetPrivateField<Color>(health, "fullChargeFlashColor");
            AssertMaterialColors(runtimeMaterials, chargeColor);
            Assert.That(renderer.enabled, Is.EqualTo(rendererEnabledBeforeFlash));

            InvokePrivateMethod(health, "StartHitFlash");
            AssertMaterialColors(runtimeMaterials, GetPrivateField<Color>(health, "hitFlashColor"));

            SetPrivateField(health, "hitFlashStartedAt", Time.time - 1f);
            health.TickHitFlash();
            AssertMaterialColors(runtimeMaterials, chargeColor, "受击闪红结束后应继续显示满蓄力黄色。");

            chargedAttack.CancelCharge();
            health.TickHitFlash();
            for (int i = 0; i < runtimeMaterials.Length; i++)
            {
                AssertColorsEqual(runtimeMaterials[i].color, originalColors[i], "蓄力结束后应恢复角色原始颜色。");
            }
        }
        finally
        {
            health.ResetRuntimeBuffers();
            Object.DestroyImmediate(runtimeObject);
            if (runtimeMaterials != null)
            {
                foreach (Material material in runtimeMaterials)
                {
                    if (material != null && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material)))
                    {
                        Object.DestroyImmediate(material);
                    }
                }
            }
        }
    }

    [Test]
    public void GameplayUiRoot_ContainsConfiguredChargeBarAboveSkillBar()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        GameplayUiRoot uiRoot = prefab != null ? prefab.GetComponent<GameplayUiRoot>() : null;
        PlayerChargeBarUi chargeBarUi = prefab != null ? prefab.GetComponent<PlayerChargeBarUi>() : null;
        Transform chargeBar = prefab != null ? prefab.transform.Find("ChargeBarRoot") : null;

        Assert.That(prefab, Is.Not.Null);
        Assert.That(uiRoot, Is.Not.Null);
        Assert.That(chargeBarUi, Is.Not.Null);
        Assert.That(uiRoot.ValidatePrefabReferences(false), Is.True);
        Assert.That(chargeBarUi.ValidatePrefabReferences(false), Is.True);
        Assert.That(chargeBar, Is.Not.Null);
        Assert.That(chargeBar.gameObject.activeSelf, Is.False);

        RectTransform rect = chargeBar.GetComponent<RectTransform>();
        Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(0f, -330f)));
        Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(360f, 32f)));
        Image fill = chargeBar.Find("ChargeFill").GetComponent<Image>();
        Assert.That(fill.type, Is.EqualTo(Image.Type.Simple));
        Assert.That(fill.rectTransform.pivot.x, Is.Zero.Within(0.001f));
        Assert.That(fill.raycastTarget, Is.False);
    }

    private static CubeTest CreateTarget(
        string targetName,
        Vector3 position,
        ICollection<GameObject> targetObjects)
    {
        GameObject targetObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        targetObject.name = targetName;
        targetObject.transform.position = position;
        CubeTest target = targetObject.AddComponent<CubeTest>();
        targetObjects.Add(targetObject);
        return target;
    }

    private static void ReleaseTestVisuals()
    {
        foreach (PlayerChargedSpinEffect effect in Object.FindObjectsOfType<PlayerChargedSpinEffect>(true))
        {
            if (effect != null && effect.gameObject.activeSelf)
            {
                InvokePrivateMethod(effect, "Release");
            }
        }

        foreach (FloatingCombatText floatingText in Object.FindObjectsOfType<FloatingCombatText>(true))
        {
            if (floatingText != null && floatingText.gameObject.activeSelf)
            {
                InvokePrivateMethod(floatingText, "ReleaseToPool");
            }
        }
    }

    private static GameObject CreateWarriorRuntime(
        out PlayerRuntimeController runtime,
        out Animator animator)
    {
        GameObject runtimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRuntimePrefabPath);
        GameObject visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarriorPrefabPath);
        Assert.That(runtimePrefab, Is.Not.Null);
        Assert.That(visualPrefab, Is.Not.Null);

        GameObject runtimeObject = Object.Instantiate(runtimePrefab);
        GameObject visualObject = Object.Instantiate(visualPrefab, runtimeObject.transform);
        runtime = runtimeObject.GetComponent<PlayerRuntimeController>();
        InvokePrivateMethod(runtime, "CacheComponents");
        animator = visualObject.GetComponentInChildren<Animator>(true);
        animator.Rebind();
        animator.Update(0f);
        CharacterDefine warrior = LoadCharacterTable().characters.First(item => item.classId == 1);
        SetPrivateField(runtime, "entryDefine", warrior);
        // EditMode 专项测试只装配蓄力所需组件，避免初始化受击材质等无关表现产生编辑器资源泄漏警告。
        runtime.Presentation.BindVisual(visualObject, warrior);
        runtimeObject.GetComponent<PlayerCombatComponent>().Initialize(runtime);
        runtime.ChargedAttack.Initialize(runtime);
        return runtimeObject;
    }

    private static void AdvanceAnimatorUntilHolding(
        PlayerChargedAttackComponent chargedAttack,
        Animator animator,
        TestGameplayInput input)
    {
        for (int i = 0; i < 20 && !chargedAttack.IsHoldingCharge; i++)
        {
            animator.Update(0.04f);
            chargedAttack.TryHandleBasicAttackInput(input);
        }

        Assert.That(chargedAttack.IsHoldingCharge, Is.True, "攻击动画没有在 0.20 位置进入 Holding 状态。");
    }

    private static CharacterDefineTable LoadCharacterTable()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterDataPath);
        Assert.That(json, Is.Not.Null);
        CharacterDefineTable table = JsonUtility.FromJson<CharacterDefineTable>(json.text);
        Assert.That(table, Is.Not.Null);
        return table;
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] parameters)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"测试目标缺少私有方法：{methodName}");
        method.Invoke(target, parameters);
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

    private static void AssertMaterialColors(Material[] materials, Color expected, string message = null)
    {
        foreach (Material material in materials)
        {
            Assert.That(material, Is.Not.Null);
            AssertColorsEqual(material.color, expected, message);
        }
    }

    private static void AssertColorsEqual(Color actual, Color expected, string message)
    {
        Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), message);
        Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), message);
        Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), message);
        Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.001f), message);
    }

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
        public bool DebugHighAttackToggleDown => false;
        public bool DebugInvincibilityToggleDown => false;
        public bool DebugAddGoldDown => false;
        public bool DebugCompleteVaultCycleDown => false;
        public bool DebugAddLevelDown => false;
        public bool DebugRestoreManaDown => false;
        public bool DebugZeroCooldownToggleDown => false;
        public bool InventoryToggleDown => false;
        public bool InteractDown => false;
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
