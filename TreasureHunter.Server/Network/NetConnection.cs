using System.Net;
using System.Net.Sockets;

namespace Network;

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

    public void Disconnect()
    {
        CloseConnection(_eventArgs);
    }

    public void SendResponse()
    {
        byte[]? data = Session.GetResponse();
        if (data == null || data.Length == 0)
        {
            return;
        }

        SendData(data, 0, data.Length);
    }

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

    private void ReceivedCompleted(object? sender, SocketAsyncEventArgs args)
    {
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
