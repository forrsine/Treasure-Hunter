namespace GameServer;

/// <summary>控制台命令循环，收到 quit、exit 或 q 时结束等待。</summary>
public static class CommandHelper
{
    /// <summary>
    /// 阻塞等待控制台退出命令。
    /// 这个类目前比较简单，但保留出来后，后续想扩服务端热命令会更方便。
    /// </summary>
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
