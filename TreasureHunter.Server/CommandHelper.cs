namespace GameServer;

public static class CommandHelper
{
    public static void Run()
    {
        while (true)
        {
            string? command = Console.ReadLine();
            if (string.Equals(command, "quit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "exit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "q", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
    }
}
