using Common;
using GameServer;
using System.Net;
using System.Net.Sockets;

namespace Network;

/// <summary>
/// 网络服务协调器：管理 TCP 监听器、连接会话和消息处理线程。
/// Socket 回调只交付字节数据，粘包拆包和业务分发分别由 PackageHandler、MessageDistributer 负责。
/// </summary>
public sealed class NetService
{
    private TcpSocketListener? _serverListener;
    private int _messageThreads = 4;

    /// <summary>
    /// 初始化监听器与线程配置。
    /// </summary>
    public bool Init(string host, int port, int backlog, int messageThreads)
    {
        _messageThreads = messageThreads;
        _serverListener = new TcpSocketListener(host, port, backlog);
        _serverListener.SocketConnected += OnSocketConnected;
        return true;
    }

    /// <summary>
    /// 启动 TCP 监听与消息分发线程池。
    /// </summary>
    public void Start()
    {
        if (_serverListener == null)
        {
            throw new InvalidOperationException("NetService has not been initialized.");
        }

        Log.Warning("Starting Listener...");
        _serverListener.Start();

        MessageDistributer<NetConnection<NetSession>>.Instance.Start(_messageThreads);
        Log.WarningFormat("NetService Started at {0}:{1}", Settings.ServerHost, Settings.ServerPort);
    }

    /// <summary>
    /// 停止监听器与消息分发线程。
    /// </summary>
    public void Stop()
    {
        Log.Warning("Stop NetService...");
        _serverListener?.Stop();

        Log.Warning("Stopping Message Handler...");
        MessageDistributer<NetConnection<NetSession>>.Instance.Stop();
    }

    /// <summary>
    /// 新连接建立后，为它创建独立 Session 与 NetConnection。
    /// 一条连接对应一个 Session，这样登录态和当前角色都能按连接隔离。
    /// </summary>
    private static void OnSocketConnected(object? sender, Socket e)
    {
        var clientIP = (IPEndPoint?)e.RemoteEndPoint;
        var session = new NetSession();

        _ = new NetConnection<NetSession>(
            e,
            DataReceived,
            Disconnected,
            session);

        Log.WarningFormat("Client[{0}] Connected", clientIP);
    }

    /// <summary>
    /// 连接断开后的统一回调。
    /// </summary>
    private static void Disconnected(NetConnection<NetSession> sender, SocketAsyncEventArgs e)
    {
        sender.Session.Disconnected();
        Log.WarningFormat("Client[{0}] Disconnected", e.RemoteEndPoint);
    }

    /// <summary>
    /// 收到字节流后的入口。
    /// 同一连接的拆包缓存需要串行访问，所以这里对 packageHandler 加锁。
    /// </summary>
    private static void DataReceived(NetConnection<NetSession> sender, DataEventArgs e)
    {
        Log.WarningFormat("Client[{0}] DataReceived Len:{1}", e.RemoteEndPoint, e.Length);
        // 同一连接的多次回调共享拆包缓冲区，因此必须串行写入和解析。
        lock (sender.packageHandler)
        {
            sender.packageHandler.ReceiveData(e.Data, 0, e.Data.Length);
        }
    }
}
