using SkillBridge.Message;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Network
{
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

        private void Awake()
        {
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

        public void Init(string serverIP, int port)
        {
            address = new IPEndPoint(IPAddress.Parse(serverIP), port);
        }

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
