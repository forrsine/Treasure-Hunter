using SkillBridge.Message;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// Unity TCP 客户端：管理连接、重试、收发缓冲区和主线程消息分发。
    /// 网络线程只处理字节流，业务响应最终通过 MessageDistributer 交给 GameApiClient。
    /// </summary>
    public class NetClient : MonoBehaviour
    {
        private const int DefaultTryConnectTimes = 3;
        private const int ReceiveBufferSize = 64 * 1024;
        private const int NetConnectTimeout = 10000;

        public const int NetErrorFailToConnect = 1005;
        public const int NetErrorSendException = 1000;
        public const int NetErrorIllegalPackage = 1001;
        public const int NetErrorZeroByte = 1002;
        public const int NetErrorOnDestroy = 1007;

        public delegate void ConnectEventHandler(int result, string reason);

        public static NetClient Instance { get; private set; }

        public event ConnectEventHandler OnConnect;
        public event ConnectEventHandler OnDisconnect;

        private IPEndPoint address;
        private Socket clientSocket;
        private readonly MemoryStream sendBuffer = new MemoryStream();
        private readonly MemoryStream receiveBuffer = new MemoryStream(ReceiveBufferSize);
        private readonly Queue<NetMessage> sendQueue = new Queue<NetMessage>();
        private readonly PackageHandler packageHandler = new PackageHandler(null);

        private bool connecting;
        private int retryTimes;
        private int retryTimesTotal = DefaultTryConnectTimes;
        private int sendOffset;

        public bool running { get; set; }

        public bool Connected
        {
            get { return clientSocket != null && clientSocket.Connected; }
        }

        /// <summary>
        /// 初始化单例客户端，并确保它跨场景复用。
        /// 重复创建网络客户端最危险的问题不是报错，而是同一条消息可能被处理两次。
        /// </summary>
        private void Awake()
        {
            // 网络连接跨场景复用；重复客户端会造成消息被接收两次，因此只保留一个实例。
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            running = true;
            MessageDistributer.Instance.ThrowException = true;
        }

        /// <summary>
        /// 设置服务端地址。
        /// </summary>
        public void Init(string serverIP, int port)
        {
            address = new IPEndPoint(IPAddress.Parse(serverIP), port);
        }

        /// <summary>
        /// 主动发起连接。
        /// 如果当前已经处于连接过程中，则直接忽略，避免重复创建 Socket。
        /// </summary>
        public void Connect(int times = DefaultTryConnectTimes)
        {
            if (connecting)
            {
                return;
            }

            retryTimesTotal = times;

            if (clientSocket != null)
            {
                clientSocket.Close();
            }

            if (address == null)
            {
                throw new Exception("Please call NetClient.Init first.");
            }

            connecting = true;
            DoConnect();
        }

        /// <summary>
        /// 关闭连接并清理本地网络状态。
        /// 断线后如果不清队列和缓冲，旧消息可能污染下一次连接。
        /// </summary>
        public void CloseConnection(int errorCode)
        {
            Debug.LogWarning("CloseConnection(), errorCode: " + errorCode);
            connecting = false;

            if (clientSocket != null)
            {
                clientSocket.Close();
                clientSocket = null;
            }

            MessageDistributer.Instance.Clear();
            sendQueue.Clear();

            receiveBuffer.Position = 0;
            sendBuffer.Position = 0;
            sendOffset = 0;

            RaiseDisconnected(errorCode, "");
        }

        /// <summary>
        /// 发送一条协议消息。
        /// 未连接时会先触发连接，并把消息暂存到待发送队列。
        /// </summary>
        public void SendMessage(NetMessage message)
        {
            if (!running)
            {
                return;
            }

            if (!Connected)
            {
                receiveBuffer.Position = 0;
                sendBuffer.Position = 0;
                sendOffset = 0;
                Connect();
                Debug.Log("Connect server before send message.");
                sendQueue.Enqueue(message);
                return;
            }

            sendQueue.Enqueue(message);
        }

        /// <summary>
        /// 执行一次真正的 Socket 连接。
        /// 这里使用超时等待，是为了在 Unity 环境中尽快拿到明确结果，而不是无限卡住。
        /// </summary>
        private void DoConnect()
        {
            try
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                clientSocket.Blocking = true;

                Debug.LogFormat("Connect[{0}] to server {1}", retryTimes, address);
                IAsyncResult result = clientSocket.BeginConnect(address, null, null);
                bool success = result.AsyncWaitHandle.WaitOne(NetConnectTimeout);
                if (success)
                {
                    clientSocket.EndConnect(result);
                }
            }
            catch (SocketException ex)
            {
                Debug.LogErrorFormat("DoConnect SocketException: {0}", ex);
                CloseConnection(NetErrorFailToConnect);
            }
            catch (Exception ex)
            {
                Debug.LogError("DoConnect Exception: " + ex);
            }

            if (clientSocket != null && clientSocket.Connected)
            {
                clientSocket.Blocking = false;
                retryTimes = 0;
                RaiseConnected(0, "Success");
            }
            else
            {
                retryTimes++;
                if (retryTimes >= retryTimesTotal)
                {
                    RaiseConnected(1, "Cannot connect to server");
                }
            }

            connecting = false;
        }

        /// <summary>
        /// 保持连接可用。
        /// 已连接则直接返回；未连接且允许重试时，会尝试重新连接。
        /// </summary>
        private bool KeepConnect()
        {
            if (connecting || address == null)
            {
                return false;
            }

            if (Connected)
            {
                return true;
            }

            if (retryTimes < retryTimesTotal)
            {
                Connect();
            }

            return false;
        }

        /// <summary>
        /// 处理接收缓冲。
        /// Socket 层只负责拿到字节流，拆包和业务分发交给 PackageHandler 与 MessageDistributer。
        /// </summary>
        private bool ProcessRecv()
        {
            try
            {
                if (clientSocket.Poll(0, SelectMode.SelectError))
                {
                    CloseConnection(NetErrorSendException);
                    return false;
                }

                if (clientSocket.Poll(0, SelectMode.SelectRead))
                {
                    int count = clientSocket.Receive(receiveBuffer.GetBuffer(), 0, receiveBuffer.Capacity, SocketFlags.None);
                    if (count <= 0)
                    {
                        CloseConnection(NetErrorZeroByte);
                        return false;
                    }

                    packageHandler.ReceiveData(receiveBuffer.GetBuffer(), 0, count);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("ProcessRecv exception: " + ex);
                CloseConnection(NetErrorIllegalPackage);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 处理发送缓冲。
        /// 显式维护 sendOffset，是为了兼容一次 Send 只发出部分字节的情况。
        /// </summary>
        private bool ProcessSend()
        {
            try
            {
                if (clientSocket.Poll(0, SelectMode.SelectError))
                {
                    CloseConnection(NetErrorSendException);
                    return false;
                }

                if (clientSocket.Poll(0, SelectMode.SelectWrite))
                {
                    if (sendBuffer.Position > sendOffset)
                    {
                        int bufferSize = (int)(sendBuffer.Position - sendOffset);
                        int count = clientSocket.Send(sendBuffer.GetBuffer(), sendOffset, bufferSize, SocketFlags.None);
                        if (count <= 0)
                        {
                            CloseConnection(NetErrorZeroByte);
                            return false;
                        }

                        sendOffset += count;
                        if (sendOffset >= sendBuffer.Position)
                        {
                            sendOffset = 0;
                            sendBuffer.Position = 0;
                            if (sendQueue.Count > 0)
                            {
                                sendQueue.Dequeue();
                            }
                        }
                    }
                    else if (sendQueue.Count > 0)
                    {
                        byte[] package = PackageHandler.PackMessage(sendQueue.Peek());
                        sendBuffer.Write(package, 0, package.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("ProcessSend exception: " + ex);
                CloseConnection(NetErrorSendException);
                return false;
            }

            return true;
        }

        private void Update()
        {
            // Unity API 必须在主线程调用，这里每帧处理已完成的网络消息和待发送队列。
            if (!running)
            {
                return;
            }

            if (KeepConnect() && ProcessRecv() && Connected)
            {
                ProcessSend();
                MessageDistributer.Instance.Distribute();
            }
        }

        private void OnDestroy()
        {
            CloseConnection(NetErrorOnDestroy);
        }

        private void RaiseConnected(int result, string reason)
        {
            if (OnConnect != null)
            {
                OnConnect(result, reason);
            }
        }

        private void RaiseDisconnected(int result, string reason)
        {
            if (OnDisconnect != null)
            {
                OnDisconnect(result, reason);
            }
        }
    }
}
