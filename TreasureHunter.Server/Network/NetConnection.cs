using System.Net;
using System.Net.Sockets;

namespace Network;

/// <summary>
/// 单个 TCP 客户端连接：封装异步接收、响应发送、断线通知和对应业务 Session。
/// 每条连接拥有独立 PackageHandler，因此不同客户端的半包数据不会互相污染。
/// </summary>
public sealed class NetConnection<T> where T : INetSession
{
    public delegate void DataReceivedCallback(NetConnection<T> sender, DataEventArgs e);
    public delegate void DisconnectedCallback(NetConnection<T> sender, SocketAsyncEventArgs e);

    private sealed class State
    {
        public required DataReceivedCallback DataReceived;
        public required DisconnectedCallback DisconnectedCallback;
        public required Socket Socket;
    }

    private readonly SocketAsyncEventArgs _eventArgs;

    public readonly PackageHandler<NetConnection<T>> packageHandler;

    public NetConnection(
        Socket socket,
        DataReceivedCallback dataReceived,
        DisconnectedCallback disconnectedCallback,
        T session)
    {
        packageHandler = new PackageHandler<NetConnection<T>>(this);

        _eventArgs = new SocketAsyncEventArgs();
        _eventArgs.AcceptSocket = socket;
        _eventArgs.Completed += ReceivedCompleted;
        _eventArgs.UserToken = new State
        {
            Socket = socket,
            DataReceived = dataReceived,
            DisconnectedCallback = disconnectedCallback
        };
        _eventArgs.SetBuffer(new byte[64 * 1024], 0, 64 * 1024);

        Session = session;
        BeginReceive(_eventArgs);
    }

    public bool Verified { get; set; }

    public T Session { get; }

    /// <summary>
    /// 主动断开当前连接。
    /// </summary>
    public void Disconnect()
    {
        CloseConnection(_eventArgs);
    }

    /// <summary>
    /// 从 Session 中取出待发送响应并发给客户端。
    /// </summary>
    public void SendResponse()
    {
        byte[]? data = Session.GetResponse();
        if (data == null || data.Length == 0)
        {
            return;
        }

        SendData(data, 0, data.Length);
    }

    /// <summary>
    /// 底层字节发送入口。
    /// </summary>
    private void SendData(byte[] data, int offset, int count)
    {
        State state = (State)_eventArgs.UserToken!;
        Socket socket = state.Socket;
        if (socket.Connected)
        {
            socket.BeginSend(data, offset, count, SocketFlags.None, SendCallback, socket);
        }
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            var client = (Socket)ar.AsyncState!;
            client.EndSend(ar);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    /// <summary>
    /// 投递下一次异步接收。
    /// </summary>
    private void BeginReceive(SocketAsyncEventArgs args)
    {
        State state = (State)args.UserToken!;
        Socket socket = state.Socket;
        if (!socket.Connected)
        {
            return;
        }

        bool pending = socket.ReceiveAsync(args);
        if (!pending)
        {
            ReceivedCompleted(socket, args);
        }
    }

    /// <summary>
    /// 一次异步接收完成后的回调。
    /// 收到 0 字节或 Socket 错误都视为连接不可继续使用，统一进入断线清理。
    /// </summary>
    private void ReceivedCompleted(object? sender, SocketAsyncEventArgs args)
    {
        // 0 字节或 SocketError 都表示连接已无法继续读取，需要统一进入断线清理。
        if (args.BytesTransferred == 0 || args.SocketError != SocketError.Success)
        {
            CloseConnection(args);
            return;
        }

        State state = (State)args.UserToken!;
        byte[] data = new byte[args.BytesTransferred];
        Array.Copy(args.Buffer!, args.Offset, data, 0, data.Length);
        state.DataReceived(this, new DataEventArgs
        {
            RemoteEndPoint = args.RemoteEndPoint as IPEndPoint,
            Data = data,
            Offset = 0,
            Length = data.Length
        });

        BeginReceive(args);
    }

    /// <summary>
    /// 关闭连接并回调上层做业务清理。
    /// </summary>
    private void CloseConnection(SocketAsyncEventArgs args)
    {
        State state = (State)args.UserToken!;
        Socket socket = state.Socket;
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        socket.Close();
        args.Completed -= ReceivedCompleted;
        state.DisconnectedCallback(this, args);
    }
}
