namespace Common;

/// <summary>
/// 服务端最小日志门面，目前输出到控制台。
/// 业务层通过统一入口记录信息，后续可替换为带级别、文件滚动和结构化字段的日志框架。
/// </summary>
public static class Log
{
    /// <summary>
    /// 记录普通信息日志。
    /// </summary>
    public static void Info(object message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// 使用格式化字符串记录普通信息日志。
    /// </summary>
    public static void InfoFormat(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }

    /// <summary>
    /// 记录警告日志。
    /// 当前仍输出到标准输出，后续如果接专业日志框架可以单独区分级别。
    /// </summary>
    public static void Warning(object message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// 使用格式化字符串记录警告日志。
    /// </summary>
    public static void WarningFormat(string format, params object?[] args)
    {
        Console.WriteLine(format, args);
    }

    /// <summary>
    /// 记录错误日志。
    /// 错误日志写到标准错误输出，方便后续做日志采集或进程监控。
    /// </summary>
    public static void Error(object message)
    {
        Console.Error.WriteLine(message);
    }

    /// <summary>
    /// 使用格式化字符串记录错误日志。
    /// </summary>
    public static void ErrorFormat(string format, params object?[] args)
    {
        Console.Error.WriteLine(format, args);
    }
}
