using SkillBridge.Message;
using System;
using System.IO;

namespace Network
{
    /// <summary>
    /// 默认发送者类型的封包处理器。
    /// </summary>
    public class PackageHandler : PackageHandler<object>
    {
        public PackageHandler(object sender) : base(sender)
        {
        }
    }

    /// <summary>
    /// TCP 粘包/拆包处理器。
    /// 协议格式为“4 字节消息长度 + Protobuf 消息体”，可以从连续字节流中解析多条完整消息，
    /// 并保留不足一包的尾部数据等待下一次 ReceiveData。
    /// </summary>
    public class PackageHandler<T> where T : class
    {
        private readonly MemoryStream stream = new MemoryStream(64 * 1024);
        private readonly T sender;
        private int readOffset;

        public PackageHandler(T sender)
        {
            this.sender = sender;
        }

        /// <summary>
        /// 把本次收到的字节流写入缓存，并尝试继续拆包。
        /// 一次收到半包、多包都属于 TCP 的正常现象。
        /// </summary>
        public void ReceiveData(byte[] data, int offset, int count)
        {
            if (stream.Position + count > stream.Capacity)
            {
                throw new Exception("PackageHandler write buffer overflow.");
            }

            stream.Write(data, offset, count);
            ParsePackage();
        }

        /// <summary>
        /// 把一条业务消息打包成“长度头 + 消息体”的网络包。
        /// </summary>
        public static byte[] PackMessage(NetMessage message)
        {
            // 长度头解决 TCP 没有消息边界的问题，接收端据此判断一条消息是否完整。
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, message);

                byte[] package = new byte[ms.Length + 4];
                Buffer.BlockCopy(BitConverter.GetBytes((int)ms.Length), 0, package, 0, 4);
                Buffer.BlockCopy(ms.GetBuffer(), 0, package, 4, (int)ms.Length);

                return package;
            }
        }

        /// <summary>
        /// 从指定字节区间中反序列化出一条 NetMessage。
        /// </summary>
        public static NetMessage UnpackMessage(byte[] packet, int offset, int length)
        {
            using (MemoryStream ms = new MemoryStream(packet, offset, length))
            {
                return ProtoBuf.Serializer.Deserialize<NetMessage>(ms);
            }
        }

        /// <summary>
        /// 解析缓存区中的完整消息。
        /// 如果数据不完整，就把剩余字节移到头部等待下一次接收。
        /// </summary>
        private bool ParsePackage()
        {
            if (readOffset + 4 < stream.Position)
            {
                int packageSize = BitConverter.ToInt32(stream.GetBuffer(), readOffset);
                if (packageSize + readOffset + 4 <= stream.Position)
                {
                    NetMessage message = UnpackMessage(stream.GetBuffer(), readOffset + 4, packageSize);
                    if (message == null)
                    {
                        throw new Exception("PackageHandler ParsePackage failed, invalid package.");
                    }

                    MessageDistributer<T>.Instance.ReceiveMessage(sender, message);
                    readOffset += packageSize + 4;
                    return ParsePackage();
                }
            }

            if (readOffset > 0)
            {
                long size = stream.Position - readOffset;
                if (readOffset < stream.Position)
                {
                    Array.Copy(stream.GetBuffer(), readOffset, stream.GetBuffer(), 0, size);
                }

                readOffset = 0;
                stream.Position = size;
                stream.SetLength(size);
            }

            return true;
        }
    }
}
