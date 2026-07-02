using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单测试脚本：被攻击后缩小。
/// 
/// 新手理解：
/// 这个类实现 FighterInterface，所以玩家武器可以打到它。
/// 它不是正式敌人，只是用来测试“攻击命中接口是否能正常调用”。
/// </summary>
public class CubeTest : MonoBehaviour, FighterInterface
{
    /// <summary>
    /// 被攻击时调用。参数 A 是传进来的攻击力，但这里暂时没有用它。
    /// </summary>
    public void Hit(int A)
    {
        // localScale 是物体自身缩放。每被打一次，XYZ 三个方向都缩小 0.1。
        this.transform.localScale -= new Vector3(0.1f,0.1f,0.1f);
    }
}
