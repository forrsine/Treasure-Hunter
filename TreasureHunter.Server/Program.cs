using GameServer;

Console.WriteLine("Game Server Init");

var server = new GameServer.GameServer();
server.Init();
server.Start();

Console.WriteLine("Game Server Running......");
Console.WriteLine("Input quit, exit, or q to stop server.");

while (true)
{
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
