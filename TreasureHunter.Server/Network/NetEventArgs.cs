using System.Net;

namespace Network;

public sealed class DataEventArgs : EventArgs
{
    public IPEndPoint? RemoteEndPoint { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Offset { get; set; }
    public int Length { get; set; }
}
