namespace GameServer;

public sealed class TUser
{
    public long ID { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public TPlayer Player { get; set; } = new();
}
