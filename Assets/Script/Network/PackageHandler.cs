using SkillBridge.Message;
using System;
using System.IO;

namespace Network
{
    public class PackageHandler : PackageHandler<object>
    {
        public PackageHandler(object sender) : base(sender)
        {
        }
    }

    public class PackageHandler<T> where T : class
    {
        private readonly MemoryStream stream = new MemoryStream(64 * 1024);
        private readonly T sender;
        private int readOffset;

        public PackageHandler(T sender)
        {
            this.sender = sender;
        }

        public void ReceiveData(byte[] data, int offset, int count)
        {
            if (stream.Position + count > stream.Capacity)
            {
                throw new Exception("PackageHandler write buffer overflow.");
            }

            stream.Write(data, offset, count);
            ParsePackage();
        }

        public static byte[] PackMessage(NetMessage message)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ProtoBuf.Serializer.Serialize(ms, message);

                byte[] package = new byte[ms.Length + 4];
                Buffer.BlockCopy(BitConverter.GetBytes((int)ms.Length), 0, package, 0, 4);
                Buffer.BlockCopy(ms.GetBuffer(), 0, package, 4, (int)ms.Length);

                return package;
            }
        }

        public static NetMessage UnpackMessage(byte[] packet, int offset, int length)
        {
            using (MemoryStream ms = new MemoryStream(packet, offset, length))
            {
                return ProtoBuf.Serializer.Deserialize<NetMessage>(ms);
            }
        }

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
