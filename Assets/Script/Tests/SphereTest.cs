using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单测试脚本：被攻击后向后退。
/// 
/// 新手理解：
/// 和 CubeTest 一样，它只是用来测试 FighterInterface。
/// 只要一个物体实现了 Hit 方法，武器脚本就能统一攻击它。
/// </summary>
public class SphereTest : MonoBehaviour, FighterInterface
{
    /// <summary>
    /// 被攻击时调用。参数 A 是攻击力，这里暂时没有用到。
    /// </summary>
    public void Hit(int A)
    {
        // transform.forward 是物体自己的“前方”方向。
        // 这里减去 forward，表示沿自己的后方移动 1 个单位。
        this.transform.position -= this.transform.forward;
    }
}
