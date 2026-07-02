using System.Net;
using System.Net.Sockets;

namespace Network;

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

    public void Stop()
    {
        _listenerSocket?.Close();
        _listenerSocket = null;
    }

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

        BeginAccept(e);
    }

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
