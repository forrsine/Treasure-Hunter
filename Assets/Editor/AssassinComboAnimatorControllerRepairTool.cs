using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using EditorAnimatorController = UnityEditor.Animations.AnimatorController;
using EditorAnimatorControllerLayer = UnityEditor.Animations.AnimatorControllerLayer;

/// <summary>
/// 刺客连击动画控制器修复工具。
/// 作用：用 UnityEditor API 直接修改 AnimatorController，避免手改 YAML 后 Unity 当前会话没有刷新到正确状态。
/// </summary>
[InitializeOnLoad]
public static class AssassinComboAnimatorControllerRepairTool
{
    private const string ControllerPath = "Assets/Ani/Player.controller";
    private const string AttackLayerName = "Attack Layer";
    private const string ComboIndexParameter = "ComboIndex";
    private const string SkillTriggerParameter = "Skill";

    private const string Atk4ClipPath = "Assets/AllResources/游戏原美术素材/Suriyun/Faye/Animations_Faye/ATK4.anim";
    private const string Atk1ClipPath = "Assets/AllResources/游戏原美术素材/Suriyun/Faye/Animations_Faye/Atk1.anim";
    private const string Atk2ClipPath = "Assets/AllResources/游戏原美术素材/Suriyun/Faye/Animations_Faye/Atk2.anim";
    private const string Atk3ClipPath = "Assets/AllResources/游戏原美术素材/Suriyun/Faye/Animations_Faye/Atk3.anim";

    static AssassinComboAnimatorControllerRepairTool()
    {
        EditorApplication.delayCall += RepairIfNeededAfterCompile;
    }

    [MenuItem("Tools/Treasure Hunter/Repair Assassin Combo Animator")]
    public static void RepairIfNeeded()
    {
        Repair(logWhenUnchanged: true);
    }

    private static void RepairIfNeededAfterCompile()
    {
        Repair(logWhenUnchanged: false);
    }

    private static void Repair(bool logWhenUnchanged)
    {
        EditorAnimatorController controller = AssetDatabase.LoadAssetAtPath<EditorAnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogWarning($"找不到玩家动画控制器：{ControllerPath}");
            return;
        }

        AnimationClip atk4Clip = LoadClip(Atk4ClipPath);
        AnimationClip atk1Clip = LoadClip(Atk1ClipPath);
        AnimationClip atk2Clip = LoadClip(Atk2ClipPath);
        AnimationClip atk3Clip = LoadClip(Atk3ClipPath);
        if (atk4Clip == null || atk1Clip == null || atk2Clip == null || atk3Clip == null)
        {
            Debug.LogWarning("刺客连击修复失败：缺少 ATK4 / Atk1 / Atk2 / Atk3 动画资源。");
            return;
        }

        if (!TryGetAttackLayer(controller, out EditorAnimatorControllerLayer attackLayer))
        {
            Debug.LogWarning($"刺客连击修复失败：{ControllerPath} 缺少 {AttackLayerName}。");
            return;
        }

        AnimatorStateMachine stateMachine = attackLayer.stateMachine;
        AnimatorState combo1State = FindAnyStateDestination(stateMachine, 1)
            ?? FindStateByName(stateMachine, "Atk4")
            ?? FindStateByName(stateMachine, "Atk1");
        AnimatorState combo2State = FindDestinationFromState(combo1State, 2)
            ?? FindAnyStateDestination(stateMachine, 2)
            ?? FindStateByNameExcluding(stateMachine, "Atk1", combo1State)
            ?? FindStateByNameExcluding(stateMachine, "Atk2", combo1State);
        AnimatorState combo3State = FindDestinationFromState(combo2State, 3)
            ?? FindAnyStateDestination(stateMachine, 3)
            ?? FindStateByNameExcluding(stateMachine, "Atk2", combo1State, combo2State)
            ?? FindStateByNameExcluding(stateMachine, "Atk3", combo1State, combo2State);

        if (combo1State == null || combo2State == null || combo3State == null)
        {
            Debug.LogWarning("刺客连击修复失败：无法从 ComboIndex 过渡中定位三段攻击状态。");
            return;
        }

        bool changed = false;
        changed |= EnsureParameter(controller, ComboIndexParameter, AnimatorControllerParameterType.Int);
        changed |= EnsureParameter(controller, SkillTriggerParameter, AnimatorControllerParameterType.Trigger);

        changed |= ConfigureState(combo1State, "Atk4", atk4Clip, 1.5f);
        changed |= ConfigureState(combo2State, "Atk1", atk1Clip, 2f);
        changed |= ConfigureState(combo3State, "Atk2", atk2Clip, 1.5f);

        AnimatorState skillState = FindTriggerDestination(stateMachine, SkillTriggerParameter)
            ?? FindStateByName(stateMachine, "Skill");
        if (skillState == null)
        {
            skillState = stateMachine.AddState("Skill", new Vector3(460f, 610f, 0f));
            changed = true;
        }

        changed |= ConfigureState(skillState, "Skill", atk3Clip, 1f);
        changed |= EnsureComboStateTransition(combo1State, combo2State, 2, 0.06f);
        changed |= EnsureComboStateTransition(combo2State, combo3State, 3, 0.06f);
        changed |= EnsureAnyStateComboTransition(stateMachine, combo1State, 1, 0.08f);
        changed |= EnsureAnyStateComboTransition(stateMachine, combo2State, 2, 0.08f);
        changed |= EnsureAnyStateComboTransition(stateMachine, combo3State, 3, 0.08f);
        changed |= EnsureAnyStateTriggerTransition(stateMachine, skillState, SkillTriggerParameter, 0.06f);

        AnimatorState emptyState = FindStateByName(stateMachine, "Empty");
        if (emptyState != null)
        {
            changed |= EnsureExitTransition(combo1State, emptyState, 0.92f, 0.08f, requireComboIndexZero: true);
            changed |= EnsureExitTransition(combo2State, emptyState, 0.92f, 0.08f, requireComboIndexZero: true);
            changed |= EnsureExitTransition(combo3State, emptyState, 0.92f, 0.08f, requireComboIndexZero: true);
            changed |= EnsureExitTransition(skillState, emptyState, 0.92f, 0.08f, requireComboIndexZero: false);
        }
        else
        {
            Debug.LogWarning("刺客连击修复提醒：Attack Layer 中没有找到 Empty 状态，攻击结束回空状态的过渡没有自动补齐。");
        }

        if (!changed)
        {
            if (logWhenUnchanged)
            {
                Debug.Log("刺客连击 Animator 已经是正确配置：Combo 1=Atk4(ATK4)，Combo 2=Atk1，Combo 3=Atk2，Skill=Atk3。");
            }

            return;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("已修复刺客连击 Animator：Combo 1=Atk4(ATK4)，Combo 2=Atk1，Combo 3=Atk2，Skill=Atk3。");
    }

    private static AnimationClip LoadClip(string path)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
    }

    private static bool TryGetAttackLayer(EditorAnimatorController controller, out EditorAnimatorControllerLayer layer)
    {
        for (int i = 0; i < controller.layers.Length; i++)
        {
            if (controller.layers[i].name == AttackLayerName)
            {
                layer = controller.layers[i];
                return true;
            }
        }

        layer = null;
        return false;
    }

    private static bool EnsureParameter(
        EditorAnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == parameterName)
            {
                return false;
            }
        }

        controller.AddParameter(parameterName, parameterType);
        return true;
    }

    private static AnimatorState FindAnyStateDestination(AnimatorStateMachine stateMachine, int comboIndex)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (HasComboCondition(transition, comboIndex))
            {
                return transition.destinationState;
            }
        }

        return null;
    }

    private static AnimatorState FindDestinationFromState(AnimatorState state, int comboIndex)
    {
        if (state == null)
        {
            return null;
        }

        foreach (AnimatorStateTransition transition in state.transitions)
        {
            if (HasComboCondition(transition, comboIndex))
            {
                return transition.destinationState;
            }
        }

        return null;
    }

    private static AnimatorState FindTriggerDestination(AnimatorStateMachine stateMachine, string triggerName)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == triggerName)
                {
                    return transition.destinationState;
                }
            }
        }

        return null;
    }

    private static AnimatorState FindStateByName(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        return null;
    }

    private static AnimatorState FindStateByNameExcluding(
        AnimatorStateMachine stateMachine,
        string stateName,
        params AnimatorState[] excludedStates)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            if (state == null || state.name != stateName || ContainsState(excludedStates, state))
            {
                continue;
            }

            return state;
        }

        return null;
    }

    private static bool ContainsState(AnimatorState[] states, AnimatorState target)
    {
        foreach (AnimatorState state in states)
        {
            if (state == target)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasComboCondition(AnimatorStateTransition transition, int comboIndex)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == ComboIndexParameter && Mathf.Approximately(condition.threshold, comboIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ConfigureState(AnimatorState state, string stateName, Motion motion, float speed)
    {
        bool changed = false;
        if (state.name != stateName)
        {
            state.name = stateName;
            changed = true;
        }

        if (state.motion != motion)
        {
            state.motion = motion;
            changed = true;
        }

        if (!Mathf.Approximately(state.speed, speed))
        {
            state.speed = speed;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureAnyStateComboTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState destination,
        int comboIndex,
        float duration)
    {
        bool changed = false;
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            if (!HasComboCondition(transition, comboIndex))
            {
                continue;
            }

            changed |= ConfigureTransition(transition, destination, duration, canTransitionToSelf: false);
            return changed;
        }

        AnimatorStateTransition newTransition = stateMachine.AddAnyStateTransition(destination);
        newTransition.AddCondition(AnimatorConditionMode.Equals, comboIndex, ComboIndexParameter);
        ConfigureTransition(newTransition, destination, duration, canTransitionToSelf: false);
        return true;
    }

    private static bool EnsureComboStateTransition(
        AnimatorState source,
        AnimatorState destination,
        int comboIndex,
        float duration)
    {
        bool changed = false;
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (!HasComboCondition(transition, comboIndex))
            {
                continue;
            }

            changed |= ConfigureTransition(transition, destination, duration, canTransitionToSelf: false);
            return changed;
        }

        AnimatorStateTransition newTransition = source.AddTransition(destination);
        newTransition.AddCondition(AnimatorConditionMode.Equals, comboIndex, ComboIndexParameter);
        ConfigureTransition(newTransition, destination, duration, canTransitionToSelf: false);
        return true;
    }

    private static bool EnsureAnyStateTriggerTransition(
        AnimatorStateMachine stateMachine,
        AnimatorState destination,
        string triggerName,
        float duration)
    {
        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == triggerName)
                {
                    return ConfigureTransition(transition, destination, duration, canTransitionToSelf: false);
                }
            }
        }

        AnimatorStateTransition newTransition = stateMachine.AddAnyStateTransition(destination);
        newTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
        ConfigureTransition(newTransition, destination, duration, canTransitionToSelf: false);
        return true;
    }

    private static bool ConfigureTransition(
        AnimatorStateTransition transition,
        AnimatorState destination,
        float duration,
        bool canTransitionToSelf)
    {
        bool changed = false;
        if (transition.destinationState != destination)
        {
            transition.destinationState = destination;
            changed = true;
        }

        if (transition.hasExitTime)
        {
            transition.hasExitTime = false;
            changed = true;
        }

        if (!transition.hasFixedDuration)
        {
            transition.hasFixedDuration = true;
            changed = true;
        }

        if (!Mathf.Approximately(transition.duration, duration))
        {
            transition.duration = duration;
            changed = true;
        }

        if (transition.canTransitionToSelf != canTransitionToSelf)
        {
            transition.canTransitionToSelf = canTransitionToSelf;
            changed = true;
        }

        return changed;
    }

    private static bool EnsureExitTransition(
        AnimatorState source,
        AnimatorState destination,
        float exitTime,
        float duration,
        bool requireComboIndexZero)
    {
        bool changed = false;
        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState != destination)
            {
                continue;
            }

            if (requireComboIndexZero && HasNonComboIndexZeroCondition(transition))
            {
                continue;
            }

            if (!requireComboIndexZero && transition.conditions.Length > 0)
            {
                continue;
            }

            changed |= ConfigureExitTransition(transition, destination, exitTime, duration, requireComboIndexZero);
            return changed;
        }

        AnimatorStateTransition newTransition = source.AddTransition(destination);
        ConfigureExitTransition(newTransition, destination, exitTime, duration, requireComboIndexZero);
        return true;
    }

    private static bool ConfigureExitTransition(
        AnimatorStateTransition transition,
        AnimatorState destination,
        float exitTime,
        float duration,
        bool requireComboIndexZero)
    {
        bool changed = false;
        if (transition.destinationState != destination)
        {
            transition.destinationState = destination;
            changed = true;
        }

        if (!transition.hasExitTime)
        {
            transition.hasExitTime = true;
            changed = true;
        }

        if (!transition.hasFixedDuration)
        {
            transition.hasFixedDuration = true;
            changed = true;
        }

        if (!Mathf.Approximately(transition.exitTime, exitTime))
        {
            transition.exitTime = exitTime;
            changed = true;
        }

        if (!Mathf.Approximately(transition.duration, duration))
        {
            transition.duration = duration;
            changed = true;
        }

        if (transition.canTransitionToSelf)
        {
            transition.canTransitionToSelf = false;
            changed = true;
        }

        if (requireComboIndexZero)
        {
            changed |= EnsureComboIndexZeroCondition(transition);
        }

        return changed;
    }

    private static bool HasNonComboIndexZeroCondition(AnimatorStateTransition transition)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == ComboIndexParameter &&
                condition.mode == AnimatorConditionMode.Equals &&
                Mathf.Approximately(condition.threshold, 0f))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool EnsureComboIndexZeroCondition(AnimatorStateTransition transition)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == ComboIndexParameter &&
                condition.mode == AnimatorConditionMode.Equals &&
                Mathf.Approximately(condition.threshold, 0f))
            {
                return false;
            }
        }

        transition.AddCondition(AnimatorConditionMode.Equals, 0f, ComboIndexParameter);
        return true;
    }
}
