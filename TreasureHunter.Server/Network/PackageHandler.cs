using SkillBridge.Message;
using System.IO;

namespace Network;

public class PackageHandler : PackageHandler<object>
{
    public PackageHandler(object sender) : base(sender)
    {
    }
}

public class PackageHandler<T>
{
    private readonly MemoryStream _stream = new(64 * 1024);
    private readonly T _sender;
    private int _readOffset;

    public PackageHandler(T sender)
    {
        _sender = sender;
    }

    public void ReceiveData(byte[] data, int offset, int count)
    {
        if (_stream.Position + count > _stream.Capacity)
        {
            throw new InvalidOperationException("PackageHandler write buffer overflow.");
        }

        _stream.Write(data, offset, count);
        ParsePackage();
    }

    public static byte[] PackMessage(NetMessage message)
    {
        using var messageStream = new MemoryStream();
        ProtoBuf.Serializer.Serialize(messageStream, message);

        byte[] package = new byte[messageStream.Length + 4];
        Buffer.BlockCopy(BitConverter.GetBytes((int)messageStream.Length), 0, package, 0, 4);
        Buffer.BlockCopy(messageStream.GetBuffer(), 0, package, 4, (int)messageStream.Length);

        return package;
    }

    public static NetMessage UnpackMessage(byte[] packet, int offset, int length)
    {
        using var messageStream = new MemoryStream(packet, offset, length);
        return ProtoBuf.Serializer.Deserialize<NetMessage>(messageStream);
    }

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
