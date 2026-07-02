using Common;
using SkillBridge.Message;

namespace Network;

public class MessageDistributer : MessageDistributer<object>
{
}

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

    public void Subscribe<Tm>(MessageHandler<Tm> messageHandler)
    {
        string type = typeof(Tm).Name;
        if (!_messageHandlers.ContainsKey(type))
        {
            _messageHandlers[type] = null;
        }

        _messageHandlers[type] = (MessageHandler<Tm>?)_messageHandlers[type] + messageHandler;
    }

    public void Unsubscribe<Tm>(MessageHandler<Tm> messageHandler)
    {
        string type = typeof(Tm).Name;
        if (!_messageHandlers.ContainsKey(type))
        {
            _messageHandlers[type] = null;
        }

        _messageHandlers[type] = (MessageHandler<Tm>?)_messageHandlers[type] - messageHandler;
    }

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

    public void ReceiveMessage(T sender, NetMessage message)
    {
        lock (_messageQueue)
        {
            _messageQueue.Enqueue(new MessageArgs { Sender = sender, Message = message });
        }

        _threadEvent.Set();
    }

    public void Clear()
    {
        lock (_messageQueue)
        {
            _messageQueue.Clear();
        }
    }

    public void Distribute()
    {
        while (TryDequeue(out MessageArgs? package) && package != null)
        {
            Dispatch(package);
        }
    }

    public void Start(int threadCount)
    {
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
