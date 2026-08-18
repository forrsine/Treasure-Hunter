using System;
using System.Collections.Generic;

/// <summary>
/// Boss 行为树节点返回值。
/// Success 表示当前节点完成，Failure 表示当前节点条件不满足或执行失败，Running 表示动作还在持续。
/// </summary>
public enum BossBehaviorState
{
    Success,
    Failure,
    Running
}

/// <summary>
/// Boss 行为树基础节点。
/// 这里做成纯 C# 类，不继承 MonoBehaviour，目的是让 AI 决策逻辑不依赖 Unity 生命周期，后续也方便单元测试。
/// </summary>
public abstract class BossBehaviorNode
{
    public abstract BossBehaviorState Tick();
}

/// <summary>
/// 选择节点：从左到右尝试子节点，只要有一个成功或运行中，就停止继续往后判断。
/// Boss 常用它表达“优先近战，其次远程，再追击，最后待机”这种优先级逻辑。
/// </summary>
public sealed class BossSelectorNode : BossBehaviorNode
{
    private readonly List<BossBehaviorNode> children;

    public BossSelectorNode(params BossBehaviorNode[] children)
    {
        this.children = new List<BossBehaviorNode>(children);
    }

    public override BossBehaviorState Tick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            BossBehaviorState state = children[i].Tick();
            if (state != BossBehaviorState.Failure)
            {
                return state;
            }
        }

        return BossBehaviorState.Failure;
    }
}

/// <summary>
/// 顺序节点：从左到右执行子节点，只要有一个失败，就认为整条分支失败。
/// 通常用它表达“如果条件满足，就执行动作”。
/// </summary>
public sealed class BossSequenceNode : BossBehaviorNode
{
    private readonly List<BossBehaviorNode> children;

    public BossSequenceNode(params BossBehaviorNode[] children)
    {
        this.children = new List<BossBehaviorNode>(children);
    }

    public override BossBehaviorState Tick()
    {
        for (int i = 0; i < children.Count; i++)
        {
            BossBehaviorState state = children[i].Tick();
            if (state != BossBehaviorState.Success)
            {
                return state;
            }
        }

        return BossBehaviorState.Success;
    }
}

/// <summary>
/// 条件节点：把一个 bool 判断包装成行为树节点。
/// 这样 BossController 只需要提供“能否近战”“玩家是否在检测范围内”等条件函数。
/// </summary>
public sealed class BossConditionNode : BossBehaviorNode
{
    private readonly Func<bool> condition;

    public BossConditionNode(Func<bool> condition)
    {
        this.condition = condition;
    }

    public override BossBehaviorState Tick()
    {
        return condition != null && condition()
            ? BossBehaviorState.Success
            : BossBehaviorState.Failure;
    }
}

/// <summary>
/// 行为节点：把一个具体动作包装成行为树节点。
/// 具体动作仍由 BossController 实现，行为树只负责任务编排，避免 AI 框架反过来依赖某个 Boss。
/// </summary>
public sealed class BossActionNode : BossBehaviorNode
{
    private readonly Func<BossBehaviorState> action;

    public BossActionNode(Func<BossBehaviorState> action)
    {
        this.action = action;
    }

    public override BossBehaviorState Tick()
    {
        return action != null
            ? action()
            : BossBehaviorState.Failure;
    }
}
