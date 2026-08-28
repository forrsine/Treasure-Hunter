/// <summary>
/// 游戏中可播放的核心音效标识。
/// 业务脚本只引用语义 ID，不直接持有 AudioClip，后续更换素材时无需修改玩法代码。
/// </summary>
public enum GameSfxId
{
    None = 0,
    UiClick = 1,
    UiError = 2,
    QuestAccepted = 3,
    QuestRewarded = 4,
    ShopPurchase = 5,
    GoldPickup = 6,
    ItemPickup = 7,
    PlayerFootstepWalk = 8,
    PlayerFootstepRun = 9,
    PlayerJump = 10,
    PlayerRoll = 11,
    PlayerAttackWarrior1 = 12,
    PlayerAttackWarrior2 = 13,
    PlayerAttackWarrior3 = 14,
    PlayerAttackAssassin1 = 15,
    PlayerAttackAssassin2 = 16,
    PlayerAttackAssassin3 = 17,
    PlayerAttackArcher = 18,
    PlayerAttackWizard = 19,
    PlayerHit = 20,
    PlayerDeath = 21,
    SkillFireball = 22,
    SkillPoison = 23,
    SkillSpin = 24,
    VaultHit = 25,
    VaultBreak = 26,
    PortalEnter = 27,
    SlimeMelee = 28,
    SlimeRanged = 29,
    SlimeHit = 30,
    SlimeDeath = 31,
    BossBite = 32,
    BossClaw = 33,
    BossSpell = 34,
    BossHit = 35,
    BossDeath = 36
}
