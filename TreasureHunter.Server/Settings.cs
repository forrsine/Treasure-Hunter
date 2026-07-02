using Microsoft.Extensions.Configuration;

namespace GameServer;

public static class Settings
{
    public static string ServerHost { get; private set; } = "127.0.0.1";
    public static int ServerPort { get; private set; } = 8000;
    public static int ConnectionBacklog { get; private set; } = 10;
    public static int MessageThreads { get; private set; } = 4;
    public static string ConnectionString { get; private set; } = "";

    public static void Load()
    {
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
