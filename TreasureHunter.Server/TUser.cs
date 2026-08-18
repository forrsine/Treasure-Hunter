namespace GameServer;

/// <summary>
/// 数据库用户记录。
/// PasswordHash 只保存 BCrypt 哈希，不保存明文密码，这是服务端安全的基础要求之一。
/// </summary>
public sealed class TUser
{
    public long ID { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public TPlayer Player { get; set; } = new();
}
