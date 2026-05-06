/// <summary>
/// 玩家升级时可以选择强化的属性类型。
/// 
/// 新手理解：
/// 1. enum 是“固定选项列表”，比直接写字符串更安全。
/// 2. PlayerCo 负责真正改数值，GameConfig 负责配置每种属性的显示文本和成长幅度，
///    PlayerLevelUpPanel 负责把这些选项显示成按钮。
/// 3. 每个枚举值后面的数字不是必须写，但写出来可以让存档或调试时更稳定。
/// </summary>
public enum PlayerAttributeType
{
    // None 表示“没有选择任何属性”，常用作默认值。
    None = 0,

    // 增加玩家攻击力，影响 WeaponCo 打出去的伤害。
    AttackPower = 1,

    // 增加最大生命值，同时通常会恢复一部分生命。
    MaxHp = 2,

    // 增加移动速度，包含走路速度和按 Shift 跑步的速度。
    MoveSpeed = 3,

    // 增加暴击概率，暴击会按暴击伤害倍率放大攻击。
    CritChance = 4,

    // 增加闪避概率，玩家被怪物攻击时有机会完全免伤。
    DodgeChance = 5,

    // 增加每秒自动回血。
    HealthRegen = 6,

    // 增加伤害减免比例，受到伤害时按比例降低。
    DamageReduction = 7,

    // 增加吸血比例，玩家造成伤害后按比例回血。
    LifeSteal = 8
}
