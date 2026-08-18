namespace Common;

/// <summary>
/// 延迟创建的进程内单例。适合当前单进程原型中的无参服务，
/// 项目扩大后可替换为依赖注入容器以改善生命周期和测试能力。
/// </summary>
public class Singleton<T> where T : new()
{
    private static T? _instance;

    /// <summary>
    /// 获取单例实例。
    /// 第一次访问时才真正 new 出对象，这就是“延迟创建”的含义。
    /// </summary>
    public static T Instance
    {
        get
        {
            _instance ??= new T();
            return _instance;
        }
    }
}
