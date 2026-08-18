using System;
using System.Collections.Generic;

/// <summary>
/// 职业动画适配类型。
/// DirectionalCombo 对应刺客的四方向移动和三段连击控制器；
/// SimpleSpeedAttack 对应战士、法师、弓箭手资源自带的 Speed/Attack 控制器。
/// </summary>
public enum CharacterAnimationStyle
{
    DirectionalCombo = 0,
    SimpleSpeedAttack = 1
}

/// <summary>
/// CharacterDefine.json 的根节点，仅用于让 JsonUtility 读取职业列表。
/// </summary>
[Serializable]
public class CharacterDefineTable
{
    public List<CharacterDefine> characters;
}

/// <summary>
/// 职业静态配置：描述一个职业使用什么模型、初始数值和动画适配方式。
/// 这里只保存不会在一局游戏中变化的数据；当前血量、经验等运行时状态不应写回本配置。
/// </summary>
[Serializable]
public class CharacterDefine
{
    public int classId;
    public string classKey;
    public string name;
    public string description;
    public string previewPrefabPath;
    // visualPrefabPath 只负责角色外观；玩家逻辑统一来自 PlayerRuntime Prefab。
    public string visualPrefabPath;
    // 保留旧字段兼容现有数据和工具，新生成流程会优先读取 visualPrefabPath。
    public string gamePrefabPath;
    public CharacterAnimationStyle animationStyle;
    public float basicAttackDuration;
    public int initLevel;
    public float hp;
    public float mp;
    public float attack;
    public float defense;
    public float moveSpeed;
}
