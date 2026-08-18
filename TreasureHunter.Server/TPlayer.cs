namespace GameServer;

/// <summary>
/// 数据库玩家聚合数据。
/// 可以理解成“一个账号在游戏维度下的总档案”，包含角色列表和最高分等信息。
/// </summary>
public sealed class TPlayer
{
    public long ID { get; set; }
    public long UserId { get; set; }
    public int HighScore { get; set; }
    public List<TCharacter> Characters { get; } = new();
}
