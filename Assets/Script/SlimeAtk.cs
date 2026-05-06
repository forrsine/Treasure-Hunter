using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 史莱姆近战攻击碰撞盒。
/// 
/// 新手理解：
/// 1. 这个脚本通常挂在史莱姆攻击用的 Trigger Collider 上。
/// 2. 攻击动画播放到“真正打中”的帧时，会启用这个碰撞盒。
/// 3. 碰到实现 FighterInterface 的对象，就把 slime.AtkPower 作为伤害打过去。
/// </summary>
public class SlimeAtk : MonoBehaviour
{
    // 拖入拥有攻击力数据的史莱姆本体。
    public SlimeCo slime;

    /// <summary>
    /// 预留初始化入口；当前攻击盒不需要开局处理。
    /// </summary>
    void Start()
    {
        // 目前不需要初始化，保留空方法方便以后扩展。
    }

    /// <summary>
    /// 预留每帧入口；伤害由 Trigger 回调驱动。
    /// </summary>
    void Update()
    {
        // 目前不需要每帧逻辑，真正的伤害发生在 OnTriggerEnter。
    }

    /// <summary>
    /// 攻击盒碰到可受击对象时，把史莱姆攻击力传给目标。
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // TryGetComponent 会尝试从碰到的物体上找 FighterInterface。
        // 找到了就返回 true，并把结果放进 fighter 变量。
        if (other.gameObject.TryGetComponent<FighterInterface>(out FighterInterface  fighter))
        {
            // 这里直接用史莱姆当前攻击力。怪物难度提升后，slime.AtkPower 也会变高。
            fighter.Hit(slime.AtkPower);
        }
    }
}
