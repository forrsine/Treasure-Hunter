using Common;
using SkillBridge.Message;

namespace Network;

/// <summary>不关心发送者类型时使用的便捷消息分发器。</summary>
public class MessageDistributer : MessageDistributer<object>
{
}

/// <summary>
/// 服务端消息队列与事件总线。
/// Socket 回调把完整 NetMessage 入队，后台工作线程再按具体协议类型调用业务处理器，
/// 从而避免在 IO 回调中执行数据库等耗时业务。
/// </summary>
public class MessageDistributer<T> : Singleton<MessageDistributer<T>>
{
    private sealed class MessageArgs
    {
        public required T Sender { get; init; }
        public required NetMessage Message { get; init; }
    }

    public delegate void MessageHandler<Tm>(T sender, Tm message);

    private readonly Queue<MessageArgs> _messageQueue = new();
    private readonly Dictionary<string, Delegate?> _messageHandlers = new();
    private readonly AutoResetEvent _threadEvent = new(true);
    private bool _running;

    public int ThreadCount { get; private set; }
    public int ActiveThreadCount;
    public bool ThrowException { get; set; }

    /// <summary>
    /// 订阅某一种具体协议消息的处理器。
    /// </summary>
    public void Subscribe<Tm>(MessageHandler<Tm> messageHandler)
    {
        string type = typeof(Tm).Name;
        if (!_messageHandlers.ContainsKey(type))
        {
            _messageHandlers[type] = null;
        }

        _messageHandlers[type] = (MessageHandler<Tm>?)_messageHandlers[type] + messageHandler;
    }

    /// <summary>
    /// 取消订阅，通常在生命周期结束时使用。
    /// </summary>
    public void Unsubscribe<Tm>(MessageHandler<Tm> messageHandler)
    {
        string type = typeof(Tm).Name;
        if (!_messageHandlers.ContainsKey(type))
        {
            _messageHandlers[type] = null;
        }

        _messageHandlers[type] = (MessageHandler<Tm>?)_messageHandlers[type] - messageHandler;
    }

    /// <summary>
    /// 根据消息真实类型触发对应订阅者。
    /// </summary>
    public void RaiseEvent<Tm>(T sender, Tm message)
    {
        if (message == null)
        {
            return;
        }

        string key = message.GetType().Name;
        if (!_messageHandlers.TryGetValue(key, out Delegate? handlerDelegate) || handlerDelegate == null)
        {
            Log.WarningFormat("No handler subscribed for {0}", key);
            return;
        }

        var handler = (MessageHandler<Tm>)handlerDelegate;
        try
        {
            handler(sender, message);
        }
        catch (Exception ex)
        {
            Log.ErrorFormat("Message handler exception: {0}", ex);
            if (ThrowException)
            {
                throw;
            }
        }
    }

    /// <summary>
    /// 收到完整协议消息后先入队，等待后台工作线程消费。
    /// </summary>
    public void ReceiveMessage(T sender, NetMessage message)
    {
        // Queue 不是线程安全容器，入队、出队和清空都使用同一把锁保护。
        lock (_messageQueue)
        {
            _messageQueue.Enqueue(new MessageArgs { Sender = sender, Message = message });
        }

        _threadEvent.Set();
    }

    /// <summary>
    /// 清空待处理消息队列，常用于停服或断线清理。
    /// </summary>
    public void Clear()
    {
        lock (_messageQueue)
        {
            _messageQueue.Clear();
        }
    }

    /// <summary>
    /// 立即在当前线程分发队列中的所有消息。
    /// 当前项目主要走后台线程消费，这个方法更多用于调试或扩展场景。
    /// </summary>
    public void Distribute()
    {
        while (TryDequeue(out MessageArgs? package) && package != null)
        {
            Dispatch(package);
        }
    }

    /// <summary>
    /// 启动后台消息处理线程池。
    /// </summary>
    public void Start(int threadCount)
    {
        // 限制线程数量，避免错误配置创建过多线程拖垮进程。
        ThreadCount = Math.Clamp(threadCount, 1, 1000);
        _running = true;

        for (int i = 0; i < ThreadCount; i++)
        {
            ThreadPool.QueueUserWorkItem(MessageDistribute);
        }

        while (ActiveThreadCount < ThreadCount)
        {
            Thread.Sleep(50);
        }
    }

    /// <summary>
    /// 停止消息处理线程，并等待所有工作线程退出。
    /// </summary>
    public void Stop()
    {
        _running = false;
        Clear();

        while (ActiveThreadCount > 0)
        {
            _threadEvent.Set();
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// 后台工作线程主循环。
    /// 没有消息时等待 AutoResetEvent 唤醒，避免空转占 CPU。
    /// </summary>
    private void MessageDistribute(object? stateInfo)
    {
        Log.Warning("MessageDistribute thread start");
        Interlocked.Increment(ref ActiveThreadCount);

        try
        {
            while (_running)
            {
                if (!TryDequeue(out MessageArgs? package) || package == null)
                {
                    _threadEvent.WaitOne();
                    continue;
                }

                Dispatch(package);
            }
        }
        finally
        {
            Interlocked.Decrement(ref ActiveThreadCount);
            Log.Warning("MessageDistribute thread end");
        }
    }

    private bool TryDequeue(out MessageArgs? package)
    {
        lock (_messageQueue)
        {
            if (_messageQueue.Count == 0)
            {
                package = null;
                return false;
            }

            package = _messageQueue.Dequeue();
            return true;
        }
    }

    private static void Dispatch(MessageArgs package)
    {
        if (package.Message.Request != null)
        {
            MessageDispatch<T>.Instance.Dispatch(package.Sender, package.Message.Request);
        }

        if (package.Message.Response != null)
        {
            MessageDispatch<T>.Instance.Dispatch(package.Sender, package.Message.Response);
        }
    }
}
