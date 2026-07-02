namespace Common;

public static class Log
{
    public static void Info(object message)
    {
        Console.WriteLine(message);
    }

    public static void InfoFormat(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }

    public static void Warning(object message)
    {
        Console.WriteLine(message);
    }

    public static void WarningFormat(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }

    public static void Error(object message)
    {
        Console.Error.WriteLine(message);
    }

    public static void ErrorFormat(string format, params object?[] args)
    {
        Console.Error.WriteLine(format, args);
    }
}
