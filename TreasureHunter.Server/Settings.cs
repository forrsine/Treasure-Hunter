using Microsoft.Extensions.Configuration;

namespace GameServer;

/// <summary>
/// 服务端配置入口：从输出目录的 appsettings.json 读取监听地址、线程数和数据库连接串。
/// 配置集中管理可以避免网络层和数据层各自读取文件。
/// </summary>
public static class Settings
{
    public static string ServerHost { get; private set; } = "127.0.0.1";
    public static int ServerPort { get; private set; } = 8000;
    public static int ConnectionBacklog { get; private set; } = 10;
    public static int MessageThreads { get; private set; } = 4;
    public static string ConnectionString { get; private set; } = "";

    /// <summary>
    /// 从 appsettings.json 加载服务端配置。
    /// 这样监听地址、线程数和数据库连接串都能通过配置修改，而不用重新编译代码。
    /// </summary>
    public static void Load()
    {
        // 连接串属于敏感配置，业务日志和异常提示中不应输出其完整内容。
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        ServerHost = configuration["Server:Host"] ?? ServerHost;
        ServerPort = int.TryParse(configuration["Server:Port"], out int port) ? port : ServerPort;
        ConnectionBacklog = int.TryParse(configuration["Server:Backlog"], out int backlog) ? backlog : ConnectionBacklog;
        MessageThreads = int.TryParse(configuration["Server:MessageThreads"], out int threads) ? threads : MessageThreads;
        ConnectionString = configuration.GetConnectionString("TreasureHunterDb") ?? "";
    }
}
