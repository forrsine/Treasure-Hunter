#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;
using EditorAnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;

/// <summary>
/// 战士动画控制器生成工具：把 Human Pack 中已有的待机、跑步、攻击资源，
/// 组装为项目自己的 AnimatorController，避免直接修改第三方资源包文件。
/// </summary>
public static class WarriorAnimatorControllerSetupTool
{
    private const string ControllerPath = "Assets/Ani/Warrior.controller";
    private const string UpperBodyMaskPath = "Assets/Ani/WarriorUpperBody.mask";
    private const string WarriorPrefabPath = "Assets/Resources/Characters/Warrior.prefab";
    private const string WarriorPreviewPrefabPath = "Assets/Resources/Characters/WarriorPreview.prefab";

    private const string IdleClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human_Idle.fbx";
    private const string RunClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/human_run.FBX";
    private const string AttackClipPath = "Assets/AllResources/Human Pack/Humans/Human Models/Animations/Human_Attack_01.fbx";
    private const float SimpleTransitionDuration = 0.1f;

    [MenuItem("Tools/Treasure Hunter/Setup Warrior Animator")]
    public static void SetupFromMenu()
    {
        Setup(logSuccess: true);
    }

    /// <summary>
    /// 提供给命令行和自动化验证调用，确保生成过程仍由 Unity 的序列化 API 完成。
    /// </summary>
    public static void SetupFromCommandLine()
    {
        Setup(logSuccess: true);
    }

    private static void Setup(bool logSuccess)
    {
        AnimationClip idleClip = LoadModelClip(IdleClipPath, "Human Idle");
        AnimationClip runClip = LoadModelClip(RunClipPath, "Human Run");
        AnimationClip attackClip = LoadModelClip(AttackClipPath, "Human Attack Sword");
        if (idleClip == null || runClip == null || attackClip == null)
        {
            throw new InvalidOperationException("生成战士 Animator 失败：待机、跑步或剑击动画资源缺失，请检查 Human Pack 是否完整。 ");
        }

        AvatarMask upperBodyMask = CreateOrUpdateUpperBodyMask();
        EditorAnimatorController controller = RebuildController(idleClip, runClip, attackClip, upperBodyMask);
        AssignControllerToPrefab(WarriorPrefabPath, controller, configureGameplayTransform: true);
        AssignControllerToPrefab(WarriorPreviewPrefabPath, controller, configureGameplayTransform: false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (logSuccess)
        {
            Debug.Log("战士 Animator 已生成并绑定：走路使用 1 倍跑步动作，奔跑和冲刺使用 2 倍跑步动作，攻击为单段剑击。 ");
        }
    }

    private static EditorAnimatorController RebuildController(
        AnimationClip idleClip,
        AnimationClip runClip,
        AnimationClip attackClip,
        AvatarMask upperBodyMask)
    {
        // 这是显式菜单工具，重复执行时重建同一个项目资源，保证参数和状态机不会累积脏配置。
        if (AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        EditorAnimatorController controller = EditorAnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
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
        BuildAttackLayer(controller, attackClip, upperBodyMask);
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

        // Human Pack 没有单独的 Walk 动画：Speed=0.5 用原速跑步模拟走路，
        // Speed=1 把同一动画设为 2 倍速，对应奔跑和无翻滚动作时的快速冲刺。
        ChildMotion[] motions = locomotionTree.children;
        motions[1].timeScale = 1f;
        motions[2].timeScale = 2f;
        locomotionTree.children = motions;

        locomotionState.writeDefaultValues = true;
        stateMachine.defaultState = locomotionState;

        // 当前素材没有跳跃动作，离地阶段保持中立待机姿势；移动和重力仍由代码正常驱动。
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
        AnimationClip attackClip,
        AvatarMask upperBodyMask)
    {
        AnimatorStateMachine stateMachine = new AnimatorStateMachine
        {
            name = "Warrior Attack Layer"
        };
        AssetDatabase.AddObjectToAsset(stateMachine, controller);

        AnimatorState emptyState = stateMachine.AddState("Empty", new Vector3(100f, 80f, 0f));
        AnimatorState attackState = stateMachine.AddState("Attack", new Vector3(360f, 20f, 0f));
        AnimatorState skillState = stateMachine.AddState("Skill", new Vector3(360f, 150f, 0f));
        stateMachine.defaultState = emptyState;

        attackState.motion = attackClip;
        attackState.speed = 0.8f;
        attackState.writeDefaultValues = true;
        skillState.motion = attackClip;
        skillState.speed = 0.8f;
        skillState.writeDefaultValues = true;

        AnimatorStateTransition attackTransition = stateMachine.AddAnyStateTransition(attackState);
        ConfigureTriggerTransition(attackTransition, "Attack", SimpleTransitionDuration);
        AnimatorStateTransition skillTransition = stateMachine.AddAnyStateTransition(skillState);
        ConfigureTriggerTransition(skillTransition, "Skill", SimpleTransitionDuration);

        ConfigureExitTransition(attackState.AddTransition(emptyState));
        ConfigureExitTransition(skillState.AddTransition(emptyState));

        EditorAnimatorControllerLayer attackLayer = new EditorAnimatorControllerLayer
        {
            name = "Attack Layer",
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

    private static void AssignControllerToPrefab(
        string prefabPath,
        EditorAnimatorController controller,
        bool configureGameplayTransform)
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = prefabRoot.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                throw new InvalidOperationException($"战士 Prefab 缺少 Animator：{prefabPath}");
            }

            animator.runtimeAnimatorController = controller;
            if (configureGameplayTransform)
            {
                // 游戏内战士模型本身比通用玩家壳大，且资源根节点额外旋转了 180 度。
                // 这里只校正表现 Prefab，不缩放 CharacterController 和公共攻击盒。
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
