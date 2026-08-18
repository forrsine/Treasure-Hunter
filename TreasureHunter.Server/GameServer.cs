using GameServer.Managers;
using GameServer.Services;
using Network;

namespace GameServer;

/// <summary>
/// 服务端总生命周期：按顺序初始化数据库、角色管理、业务服务和网络层，
/// 并持有后台主循环，保证停止时先结束循环再关闭网络资源。
/// </summary>
internal sealed class GameServer
{
    private NetService? _network;
    private Thread? _thread;
    private bool _running;

    /// <summary>
    /// 按顺序初始化服务端依赖。
    /// 先准备配置和业务处理器，再启动网络层，可以避免首个请求到达时还没注册好处理器。
    /// </summary>
    public bool Init()
    {
        // 业务订阅必须在网络开始接收消息前完成，否则首个请求可能找不到处理器。
        DBService.Instance.Init();
        CharacterManager.Instance.Init();
        UserService.Instance.Init();

        _network = new NetService();
        _network.Init(Settings.ServerHost, Settings.ServerPort, Settings.ConnectionBacklog, Settings.MessageThreads);
        _thread = new Thread(Update)
        {
            IsBackground = true
        };

        return true;
    }

    /// <summary>
    /// 启动网络监听与后台主循环线程。
    /// </summary>
    public void Start()
    {
        if (_network == null || _thread == null)
        {
            throw new InvalidOperationException("GameServer has not been initialized.");
        }

        _network.Start();
        _running = true;
        _thread.Start();
    }

    /// <summary>
    /// 停止主循环并关闭网络服务。
    /// 先停线程再停网络，是为了让后台逻辑先停止接收新工作。
    /// </summary>
    public void Stop()
    {
        _running = false;
        _thread?.Join();
        _network?.Stop();
    }

    /// <summary>
    /// 当前版本的后台循环占位。
    /// 以后如果要加定时存盘、世界广播或 AI Tick，可以从这里扩展。
    /// </summary>
    private void Update()
    {
        while (_running)
        {
            Thread.Sleep(100);
        }
    }
}
