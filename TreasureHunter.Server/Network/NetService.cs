using Common;
using GameServer;
using System.Net;
using System.Net.Sockets;

namespace Network;

public sealed class NetService
{
    private TcpSocketListener? _serverListener;
    private int _messageThreads = 4;

    public bool Init(string host, int port, int backlog, int messageThreads)
    {
        _messageThreads = messageThreads;
        _serverListener = new TcpSocketListener(host, port, backlog);
        _serverListener.SocketConnected += OnSocketConnected;
        return true;
    }

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

    public void Stop()
    {
        Log.Warning("Stop NetService...");
        _serverListener?.Stop();

        Log.Warning("Stopping Message Handler...");
        MessageDistributer<NetConnection<NetSession>>.Instance.Stop();
    }

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

    private static void Disconnected(NetConnection<NetSession> sender, SocketAsyncEventArgs e)
    {
        sender.Session.Disconnected();
        Log.WarningFormat("Client[{0}] Disconnected", e.RemoteEndPoint);
    }

    private static void DataReceived(NetConnection<NetSession> sender, DataEventArgs e)
    {
        Log.WarningFormat("Client[{0}] DataReceived Len:{1}", e.RemoteEndPoint, e.Length);
        lock (sender.packageHandler)
        {
            sender.packageHandler.ReceiveData(e.Data, 0, e.Data.Length);
        }
    }
}
