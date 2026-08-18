using GameServer;

// 服务端程序入口：初始化并启动 GameServer，主线程等待控制台退出命令。
// 真正的网络、数据库和业务初始化顺序封装在 GameServer 中。
Console.WriteLine("Game Server Init");

var server = new GameServer.GameServer();
server.Init();
server.Start();

Console.WriteLine("Game Server Running......");
Console.WriteLine("Input quit, exit, or q to stop server.");

while (true)
{
    // 这里保持最简单的控制台循环，方便本地联调时手动停止服务端。
    string? command = Console.ReadLine();
    if (string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "q", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }
}

Console.WriteLine("Game Server Exiting...");
server.Stop();
Console.WriteLine("Game Server Exited");
