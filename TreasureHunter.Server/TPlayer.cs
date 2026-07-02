namespace GameServer;

public sealed class TPlayer
{
    public long ID { get; set; }
    public long UserId { get; set; }
    public int HighScore { get; set; }
    public List<TCharacter> Characters { get; } = new();
}
