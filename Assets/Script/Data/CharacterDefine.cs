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
/// 职业普通攻击类型。
/// 近战由公共攻击盒判定；投射物攻击会在动画释放帧生成对应职业的可复用投射物。
/// </summary>
public enum CharacterBasicAttackType
{
    Melee = 0,
    Projectile = 1
}

/// <summary>
/// 远程普攻投射物的飞行轨迹。
/// Straight 用于弓箭，Arc 用于需要明显抛物线表现的法术火球。
/// </summary>
public enum CharacterProjectileTrajectory
{
    Straight = 0,
    Arc = 1
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
/// 战士蓄力普攻配置。
/// 配置只描述数值规则，输入状态、动画固定和伤害结算分别由对应运行时组件处理。
/// </summary>
[Serializable]
public class CharacterChargedAttackDefine
{
    public bool enabled;
    public float maxChargeDuration;
    public float maxDamageMultiplier;
    public float holdNormalizedTime;
    public float releaseHitDelay;
    public float movementSpeedLimit;
    // 满蓄力后的额外乘算减伤，只在本次蓄力攻击结束前生效，不写入玩家常驻属性。
    public float fullChargeDamageReduction;
    // 只有满蓄力释放才读取以下旋转重斩配置；未满蓄力仍使用原近战攻击盒。
    public float fullChargeAreaRadius;
    public float fullChargeSpinDuration;
    public float fullChargeSpinDegrees;
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
    public CharacterBasicAttackType basicAttackType;
    public float basicAttackDuration;
    // 只有配置此节点并启用的职业才会接管左键蓄力，其余职业保持原攻击流程。
    public CharacterChargedAttackDefine chargeAttack;
    // 远程攻击配置。近战职业会忽略这些字段。
    public float projectileReleaseRatio;
    public float projectileSpeed;
    public float projectileLifetime;
    public float projectileRadius;
    public string projectileColorHex;
    public CharacterProjectileTrajectory projectileTrajectory;
    public float projectileArcHeight;
    public float projectileVisualScale;
    public bool projectileApplyTint;
    public float projectileExplosionRadius;
    public int initLevel;
    public float hp;
    public float mp;
    public float attack;
    // 职业基础伤害减免百分比。例如 20 表示进入战斗时拥有 20% 伤害减免。
    // 使用百分比而不是直接参与减法，可以继续复用现有 DamageReduction 成长和伤害公式。
    public float defense;
    public float moveSpeed;
}
