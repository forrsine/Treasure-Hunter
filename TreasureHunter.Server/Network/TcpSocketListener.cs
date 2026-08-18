using System.Net;
using System.Net.Sockets;

namespace Network;

/// <summary>
/// TCP 异步监听器：只负责 Bind、Listen 和连续 Accept，
/// 新连接通过 SocketConnected 事件交给 NetService 创建会话，不在监听器中处理业务。
/// </summary>
public sealed class TcpSocketListener : IDisposable
{
    private readonly IPEndPoint _endPoint;
    private readonly int _connectionBacklog;
    private readonly SocketAsyncEventArgs _args;
    private Socket? _listenerSocket;
    private bool _disposed;

    public TcpSocketListener(string address, int port, int connectionBacklog)
        : this(new IPEndPoint(IPAddress.Parse(address), port), connectionBacklog)
    {
    }

    public TcpSocketListener(IPEndPoint endPoint, int connectionBacklog)
    {
        _endPoint = endPoint;
        _connectionBacklog = connectionBacklog;
        _args = new SocketAsyncEventArgs();
        _args.Completed += OnSocketAccepted;
    }

    public event EventHandler<Socket>? SocketConnected;

    public bool IsRunning => _listenerSocket != null;

    /// <summary>
    /// 启动监听器。
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("The server is already running.");
        }

        _listenerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenerSocket.Bind(_endPoint);
        _listenerSocket.Listen(_connectionBacklog);
        BeginAccept(_args);
    }

    /// <summary>
    /// 停止监听器。
    /// </summary>
    public void Stop()
    {
        _listenerSocket?.Close();
        _listenerSocket = null;
    }

    /// <summary>
    /// 投递下一次异步 Accept。
    /// </summary>
    private void BeginAccept(SocketAsyncEventArgs args)
    {
        if (_listenerSocket == null)
        {
            return;
        }

        args.AcceptSocket = null;
        bool pending = _listenerSocket.AcceptAsync(args);
        if (!pending)
        {
            OnSocketAccepted(_listenerSocket, args);
        }
    }

    /// <summary>
    /// 处理一次 Accept 完成事件。
    /// 每接入一个新客户端后，都会立刻继续投递下一次 Accept，保持持续监听。
    /// </summary>
    private void OnSocketAccepted(object? sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.OperationAborted)
        {
            return;
        }

        if (e.SocketError == SocketError.Success && e.AcceptSocket != null)
        {
            SocketConnected?.Invoke(this, e.AcceptSocket);
        }

        // 每完成一次 Accept 都立即投递下一次，保证监听器持续接收新客户端。
        BeginAccept(e);
    }

    /// <summary>
    /// 释放监听器资源。
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _args.Dispose();
        _disposed = true;
    }
}
