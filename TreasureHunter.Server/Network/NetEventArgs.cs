using System.Net;

namespace Network;

/// <summary>一次 Socket 接收完成后的只读数据描述，携带远端地址和有效字节范围。</summary>
public sealed class DataEventArgs : EventArgs
{
    public IPEndPoint? RemoteEndPoint { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public int Offset { get; set; }
    public int Length { get; set; }
}
