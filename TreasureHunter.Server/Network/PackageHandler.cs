using SkillBridge.Message;
using System.IO;

namespace Network;

/// <summary>默认发送者类型的封包处理器。</summary>
public class PackageHandler : PackageHandler<object>
{
    public PackageHandler(object sender) : base(sender)
    {
    }
}

/// <summary>
/// TCP 粘包/拆包处理器，协议格式为“4 字节长度头 + Protobuf 消息体”。
/// 一次 ReceiveData 可以解析多包；不足一包的数据会移动到缓冲区头部等待下次接收。
/// </summary>
public class PackageHandler<T>
{
    private readonly MemoryStream _stream = new(64 * 1024);
    private readonly T _sender;
    private int _readOffset;

    public PackageHandler(T sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// 把收到的字节流写入缓存，并尝试继续拆包。
    /// </summary>
    public void ReceiveData(byte[] data, int offset, int count)
    {
        if (_stream.Position + count > _stream.Capacity)
        {
            throw new InvalidOperationException("PackageHandler write buffer overflow.");
        }

        _stream.Write(data, offset, count);
        ParsePackage();
    }

    /// <summary>
    /// 把一条业务消息打包成“长度头 + Protobuf 消息体”。
    /// </summary>
    public static byte[] PackMessage(NetMessage message)
    {
        // 长度头为接收端恢复消息边界，TCP 本身不会保留 Send 调用的边界。
        using var messageStream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(messageStream, message);

        byte[] package = new byte[messageStream.Length + 4];
        Buffer.BlockCopy(BitConverter.GetBytes((int)messageStream.Length), 0, package, 0, 4);
        Buffer.BlockCopy(messageStream.GetBuffer(), 0, package, 4, (int)messageStream.Length);

        return package;
    }

    /// <summary>
    /// 从指定字节区间中反序列化出一条 NetMessage。
    /// </summary>
    public static NetMessage UnpackMessage(byte[] packet, int offset, int length)
    {
        using var messageStream = new MemoryStream(packet, offset, length);
        return ProtoBuf.Serializer.Deserialize<NetMessage>(messageStream);
    }

    /// <summary>
    /// 解析缓冲区中的完整消息。
    /// 如果末尾数据不够凑成一整包，就先保留下来等待下一次接收。
    /// </summary>
    private bool ParsePackage()
    {
        if (_readOffset + 4 < _stream.Position)
        {
            int packageSize = BitConverter.ToInt32(_stream.GetBuffer(), _readOffset);
            if (packageSize + _readOffset + 4 <= _stream.Position)
            {
                NetMessage message = UnpackMessage(_stream.GetBuffer(), _readOffset + 4, packageSize);
                MessageDistributer<T>.Instance.ReceiveMessage(_sender, message);
                _readOffset += packageSize + 4;
                return ParsePackage();
            }
        }

        if (_readOffset > 0)
        {
            long size = _stream.Position - _readOffset;
            if (_readOffset < _stream.Position)
            {
                Array.Copy(_stream.GetBuffer(), _readOffset, _stream.GetBuffer(), 0, size);
            }

            _readOffset = 0;
            _stream.Position = size;
            _stream.SetLength(size);
        }

        return true;
    }
}
