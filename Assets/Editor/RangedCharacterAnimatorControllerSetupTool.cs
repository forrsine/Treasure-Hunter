#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;
using EditorAnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;

/// <summary>
/// 弓箭手和法师动画控制器生成工具。
/// 复用 Human Pack 的待机、跑步和职业攻击动作，但把状态机保存在项目自己的 Assets/Ani 下，
/// 这样不会修改第三方资源，也便于后续替换单个职业动画。
/// </summary>
public static class RangedCharacterAnimatorControllerSetupTool
{
    private const string ArcherControllerPath = "Assets/Ani/Archer.controller";
    private const string WizardControllerPath = "Assets/Ani/Wizard.controller";
    private const string UpperBodyMaskPath = "Assets/Ani/RangedUpperBody.mask";
    private const string CharacterDataPath = "Assets/Resources/Data/CharacterDefine.json";
    private const string PlayerRuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";

    private const string ArcherPrefabPath = "Assets/Resources/Characters/Archer.prefab";
    private const string ArcherPreviewPrefabPath = "Assets/Resources/Characters/ArcherPreview.prefab";
    private const string WizardPrefabPath = "Assets/Resources/Characters/Wizard.prefab";
    private const string WizardPreviewPrefabPath = "Assets/Resources/Characters/WizardPreview.prefab";
    private const string WizardProjectilePrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/MagicMissile 1.prefab";
    private const string WizardExplosionPrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/Magic Explosion 1.prefab";
    private const string ArcherProjectilePrefabPath = "Assets/AllResources/Human Pack/Humans/Human Models/Projectiles/Human Bolt.prefab";

    private const string IdleClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human_Idle.fbx";
    private const string RunClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/human_run.FBX";
    private const string ArcherAttackClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human Shoot Crossbow 2_shootTrigger.anim";
    private const string WizardAttackClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human_atkStaff.anim";
    private const float SimpleTransitionDuration = 0.1f;
    private const float ArcherSkillAnimationDuration = 0.75f;

    /// <summary>
    /// 当前项目若正被 Unity Editor 打开，新增脚本完成编译后自动补齐首次资源生成。
    /// 仅在控制器缺失时运行一次，已有资源不会在普通脚本重载时被反复覆盖。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ScheduleFirstSetupWhenAssetsAreMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ArcherControllerPath) != null &&
                AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(WizardControllerPath) != null)
            {
                return;
            }

            Setup(logSuccess: false);
        };
    }

    [MenuItem("Tools/Treasure Hunter/Setup Ranged Character Animators")]
    public static void SetupFromMenu()
    {
        Setup(logSuccess: true);
    }

    /// <summary>
    /// 提供给命令行验证调用。资源创建仍通过 Unity 的序列化 API 完成，避免手写 controller YAML。
    /// </summary>
    public static void SetupFromCommandLine()
    {
        Setup(logSuccess: true);
    }

    private static void Setup(bool logSuccess)
    {
        AnimationClip idleClip = LoadModelClip(IdleClipPath, "Human Idle");
        AnimationClip runClip = LoadModelClip(RunClipPath, "Human Run");
        AnimationClip archerAttackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArcherAttackClipPath);
        AnimationClip wizardAttackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(WizardAttackClipPath);
        if (idleClip == null || runClip == null || archerAttackClip == null || wizardAttackClip == null)
        {
            throw new InvalidOperationException("生成远程职业 Animator 失败：待机、跑步、弓箭攻击或法杖攻击动画资源缺失。");
        }

        CharacterDefine archerDefine = LoadCharacterDefine("Archer");
        CharacterDefine wizardDefine = LoadCharacterDefine("Wizard");
        AvatarMask upperBodyMask = CreateOrUpdateUpperBodyMask();

        EditorAnimatorController archerController = RebuildController(
            ArcherControllerPath,
            "Archer Attack Layer",
            idleClip,
            runClip,
            archerAttackClip,
            archerDefine.basicAttackDuration,
            ArcherSkillAnimationDuration,
            upperBodyMask);
        EditorAnimatorController wizardController = RebuildController(
            WizardControllerPath,
            "Wizard Attack Layer",
            idleClip,
            runClip,
            wizardAttackClip,
            wizardDefine.basicAttackDuration,
            wizardDefine.basicAttackDuration,
            upperBodyMask);

        AssignControllerToPrefab(
            ArcherPrefabPath,
            archerController,
            "弓箭手",
            configureGameplayTransform: true);
        AssignControllerToPrefab(
            ArcherPreviewPrefabPath,
            archerController,
            "弓箭手预览",
            configureGameplayTransform: false);
        AssignControllerToPrefab(
            WizardPrefabPath,
            wizardController,
            "法师",
            configureGameplayTransform: true);
        AssignControllerToPrefab(
            WizardPreviewPrefabPath,
            wizardController,
            "法师预览",
            configureGameplayTransform: false);
        EnsureRuntimeRangedAttackComponent();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (logSuccess)
        {
            Debug.Log("弓箭手和法师 Animator 已生成并绑定：法师普攻使用紫色抛物线火球，弓箭手普攻使用直线箭矢。");
        }
    }

    private static EditorAnimatorController RebuildController(
        string controllerPath,
        string attackLayerStateMachineName,
        AnimationClip idleClip,
        AnimationClip runClip,
        AnimationClip attackClip,
        float basicAttackDuration,
        float skillAnimationDuration,
        AvatarMask upperBodyMask)
    {
        // 工具可重复执行：只重建项目自有的两个控制器，不触碰 Human Pack 原控制器。
        if (AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(controllerPath) != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        EditorAnimatorController controller = EditorAnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter(new AnimatorControllerParameter
        {
            name = "IsGrounded",
            type = AnimatorControllerParameterType.Bool,
            defaultBool = true
        });
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Skill", AnimatorControllerParameterType.Trigger);

        BuildLocomotionLayer(controller, idleClip, runClip);
        BuildAttackLayer(
            controller,
            attackLayerStateMachineName,
            attackClip,
            basicAttackDuration,
            skillAnimationDuration,
            upperBodyMask);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void BuildLocomotionLayer(
        EditorAnimatorController controller,
        AnimationClip idleClip,
        AnimationClip runClip)
    {
        EditorAnimatorControllerLayer baseLayer = controller.layers[0];
        baseLayer.name = "Base Layer";
        AnimatorStateMachine stateMachine = baseLayer.stateMachine;

        AnimatorState locomotionState = controller.CreateBlendTreeInController(
            "Locomotion",
            out BlendTree locomotionTree,
            0);
        locomotionTree.blendType = BlendTreeType.Simple1D;
        locomotionTree.blendParameter = "Speed";
        locomotionTree.useAutomaticThresholds = false;
        locomotionTree.AddChild(idleClip, 0f);
        locomotionTree.AddChild(runClip, 0.5f);
        locomotionTree.AddChild(runClip, 1f);

        // 素材没有独立 Walk/Run/Roll：Speed=0.5 播放原速动作模拟走路，
        // Speed=1 将相同动作设为两倍速，既表现奔跑，也作为快速冲刺时的翻滚替代。
        ChildMotion[] motions = locomotionTree.children;
        motions[1].timeScale = 1f;
        motions[2].timeScale = 2f;
        locomotionTree.children = motions;

        locomotionState.writeDefaultValues = true;
        stateMachine.defaultState = locomotionState;

        // 当前远程职业素材没有合适的跳跃动作，离地时保持待机姿势；移动和重力仍由代码处理。
        AnimatorState airborneState = stateMachine.AddState("Airborne", new Vector3(360f, 10f, 0f));
        airborneState.motion = idleClip;
        airborneState.writeDefaultValues = true;

        AnimatorStateTransition enterAirborne = locomotionState.AddTransition(airborneState);
        ConfigureConditionTransition(enterAirborne, "IsGrounded", false, SimpleTransitionDuration);
        AnimatorStateTransition leaveAirborne = airborneState.AddTransition(locomotionState);
        ConfigureConditionTransition(leaveAirborne, "IsGrounded", true, SimpleTransitionDuration);

        EditorAnimatorControllerLayer[] layers = controller.layers;
        layers[0] = baseLayer;
        controller.layers = layers;
    }

    private static void BuildAttackLayer(
        EditorAnimatorController controller,
        string stateMachineName,
        AnimationClip attackClip,
        float basicAttackDuration,
        float skillAnimationDuration,
        AvatarMask upperBodyMask)
    {
        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = stateMachineName
        };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        AnimatorState emptyState = stateMachine.AddState("Empty", new Vector3(100f, 80f, 0f));
        AnimatorState attackState = stateMachine.AddState("Attack", new Vector3(360f, 20f, 0f));
        AnimatorState skillState = stateMachine.AddState("Skill", new Vector3(360f, 150f, 0f));
        stateMachine.defaultState = emptyState;

        // 普攻和技能虽然暂时复用同一素材，但各自按目标时长换算速度。
        // 弓箭手只把普攻加速两倍，技能仍保持原来的 0.75 秒，避免技能表现被连带加速。
        float attackStateSpeed = Mathf.Max(0.01f, attackClip.length / Mathf.Max(0.1f, basicAttackDuration));
        float skillStateSpeed = Mathf.Max(0.01f, attackClip.length / Mathf.Max(0.1f, skillAnimationDuration));
        attackState.motion = attackClip;
        attackState.speed = attackStateSpeed;
        attackState.writeDefaultValues = true;
        skillState.motion = attackClip;
        skillState.speed = skillStateSpeed;
        skillState.writeDefaultValues = true;

        AnimatorStateTransition attackTransition = stateMachine.AddAnyStateTransition(attackState);
        ConfigureTriggerTransition(attackTransition, "Attack", SimpleTransitionDuration);
        attackTransition.canTransitionToSelf = true;
        AnimatorStateTransition skillTransition = stateMachine.AddAnyStateTransition(skillState);
        ConfigureTriggerTransition(skillTransition, "Skill", SimpleTransitionDuration);
        skillTransition.canTransitionToSelf = true;
        ConfigureExitTransition(attackState.AddTransition(emptyState));
        ConfigureExitTransition(skillState.AddTransition(emptyState));

        EditorAnimatorControllerLayer attackLayer = new EditorAnimatorControllerLayer
        {
            name = "Attack Layer",
            // 游戏绑定模型时攻击层默认关闭，左键攻击再由表现组件显式恢复为 1。
            defaultWeight = 0f,
            avatarMask = upperBodyMask,
            blendingMode = AnimatorLayerBlendingMode.Override,
            stateMachine = stateMachine
        };
        controller.AddLayer(attackLayer);
    }

    private static AvatarMask CreateOrUpdateUpperBodyMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
        }

        for (int bodyPartValue = 0;
             bodyPartValue < (int)AvatarMaskBodyPart.LastBodyPart;
             bodyPartValue++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)bodyPartValue, false);
        }

        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static CharacterDefine LoadCharacterDefine(string classKey)
    {
        TextAsset characterJson = AssetDatabase.LoadAssetAtPath<TextAsset>(CharacterDataPath);
        CharacterDefineTable table = characterJson != null
            ? JsonUtility.FromJson<CharacterDefineTable>(characterJson.text)
            : null;
        CharacterDefine define = table?.characters?.FirstOrDefault(item => item.classKey == classKey);
        if (define == null)
        {
            throw new InvalidOperationException($"生成远程职业 Animator 失败：CharacterDefine.json 中找不到 {classKey} 配置。");
        }

        return define;
    }

    private static void AssignControllerToPrefab(
        string prefabPath,
        EditorAnimatorController controller,
        string characterLabel,
        bool configureGameplayTransform)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                throw new InvalidOperationException($"{characterLabel} Prefab 缺少 Animator：{prefabPath}");
            }

            animator.runtimeAnimatorController = controller;
            if (configureGameplayTransform)
            {
                // 游戏模型使用 PlayerRuntime 的正前方移动和发射小球，表现根节点必须与其坐标系一致。
                // 这里只调整游戏 Prefab；选角预览继续保留原来的构图和 Transform。
                prefabRoot.transform.localPosition = Vector3.zero;
                prefabRoot.transform.localRotation = Quaternion.identity;
                prefabRoot.transform.localScale = Vector3.one * 0.7f;
                EditorUtility.SetDirty(prefabRoot.transform);
            }

            EditorUtility.SetDirty(animator);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void EnsureRuntimeRangedAttackComponent()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerRuntimePrefabPath);
        try
        {
            PlayerRuntimeController runtime = prefabRoot.GetComponent<PlayerRuntimeController>();
            if (runtime == null)
            {
                throw new InvalidOperationException("PlayerRuntime Prefab 缺少 PlayerRuntimeController。");
            }

            PlayerRangedAttackComponent rangedAttack =
                prefabRoot.GetComponent<PlayerRangedAttackComponent>() ??
                prefabRoot.AddComponent<PlayerRangedAttackComponent>();

            GameObject wizardProjectilePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(WizardProjectilePrefabPath);
            GameObject wizardExplosionPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(WizardExplosionPrefabPath);
            GameObject archerProjectilePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(ArcherProjectilePrefabPath);
            if (wizardProjectilePrefab == null ||
                wizardExplosionPrefab == null ||
                archerProjectilePrefab == null)
            {
                throw new InvalidOperationException(
                    "配置远程普攻失败：MagicMissile、Magic Explosion 或 Human Bolt Prefab 缺失。");
            }

            SerializedObject runtimeSerialized = new SerializedObject(runtime);
            SerializedProperty rangedAttackProperty = runtimeSerialized.FindProperty("rangedAttack");
            if (rangedAttackProperty == null)
            {
                throw new InvalidOperationException("PlayerRuntimeController 缺少 rangedAttack 序列化字段。");
            }

            rangedAttackProperty.objectReferenceValue = rangedAttack;
            runtimeSerialized.ApplyModifiedPropertiesWithoutUndo();

            // 生成工具同时固化三个资源引用，避免以后重建 Animator 时 PlayerRuntime 丢失职业投射物。
            SerializedObject rangedAttackSerialized = new SerializedObject(rangedAttack);
            rangedAttackSerialized.FindProperty("wizardProjectileVisualPrefab").objectReferenceValue =
                wizardProjectilePrefab;
            rangedAttackSerialized.FindProperty("wizardExplosionVisualPrefab").objectReferenceValue =
                wizardExplosionPrefab;
            rangedAttackSerialized.FindProperty("archerProjectileVisualPrefab").objectReferenceValue =
                archerProjectilePrefab;
            rangedAttackSerialized.FindProperty("wizardExplosionVfxLifetime").floatValue = 0.8f;
            rangedAttackSerialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(runtime);
            EditorUtility.SetDirty(rangedAttack);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerRuntimePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static AnimationClip LoadModelClip(string path, string preferredName)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return clips.FirstOrDefault(clip => clip.name == preferredName)
            ?? clips.FirstOrDefault();
    }

    private static void ConfigureConditionTransition(
        AnimatorStateTransition transition,
        string parameterName,
        bool expectedValue,
        float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.AddCondition(
            expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameterName);
    }

    private static void ConfigureTriggerTransition(
        AnimatorStateTransition transition,
        string triggerName,
        float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.canTransitionToSelf = false;
        transition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static void ConfigureExitTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = true;
        transition.exitTime = 0.95f;
        transition.hasFixedDuration = true;
        transition.duration = SimpleTransitionDuration;
        transition.canTransitionToSelf = false;
    }
}
#endif
