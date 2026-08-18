/// <summary>
/// 技能特效 Addressables 地址表。
/// 运行时代码和编辑器迁移工具共用同一份常量，避免地址字符串分散后出现拼写不一致。
/// </summary>
public static class SkillVfxAddresses
{
    public const string GroupName = "Local_SkillVFX";
    public const string Label = "skill-vfx";

    public const string FireballProjectile = "skill-vfx/fireball-projectile";
    public const string FireballExplosion = "skill-vfx/fireball-explosion";
    public const string PoisonArea = "skill-vfx/poison-area";
    public const string ScytheSpin = "skill-vfx/scythe-spin";
    public const string SpikyFireRed = "skill-vfx/shared/spiky-fire-red";

    // 对象池 Key 固定后，资源释放阶段才能精确清理对应实例，而不会影响其他表现对象。
    public const string FireballProjectilePoolKey = "FireballProjectileVfx";
    public const string FireballExplosionPoolKey = "FireballExplosionVfx";
    public const string PoisonAreaPoolKey = "PoisonAreaVfx";
    public const string ScytheSpinPoolKey = "ScytheSpinVfx";
}
