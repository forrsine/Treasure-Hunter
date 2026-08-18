using SkillBridge.Message;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Network
{
    /// <summary>
    /// 不关心消息发送者类型时使用的便捷消息分发器。
    /// </summary>
    public class MessageDistributer : MessageDistributer<object>
    {
    }

    /// <summary>
    /// 类型安全的消息事件总线。
    /// Socket 接收线程只把完整消息压入队列，Unity 主线程在 Update 中调用 Distribute，
    /// 避免网络线程直接操作 Unity 对象。
    /// </summary>
    public class MessageDistributer<T> where T : class
    {
        private class MessageArgs
        {
            public T sender;
            public NetMessage message;
        }

        private static MessageDistributer<T> instance;

        public static MessageDistributer<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MessageDistributer<T>();
                }

                return instance;
            }
        }

        public delegate void MessageHandler<Tm>(T sender, Tm message);

        private readonly Queue<MessageArgs> messageQueue = new Queue<MessageArgs>();
        private readonly Dictionary<string, Delegate> messageHandlers = new Dictionary<string, Delegate>();

        public bool ThrowException { get; set; }

        /// <summary>
        /// 订阅某一种具体协议消息。
        /// </summary>
        public void Subscribe<Tm>(MessageHandler<Tm> messageHandler)
        {
            string type = typeof(Tm).Name;
            if (!messageHandlers.ContainsKey(type))
            {
                messageHandlers[type] = null;
            }

            messageHandlers[type] = (MessageHandler<Tm>)messageHandlers[type] + messageHandler;
        }

        /// <summary>
        /// 取消订阅，通常在对象销毁或场景切换时调用。
        /// </summary>
        public void Unsubscribe<Tm>(MessageHandler<Tm> messageHandler)
        {
            string type = typeof(Tm).Name;
            if (!messageHandlers.ContainsKey(type))
            {
                messageHandlers[type] = null;
            }

            messageHandlers[type] = (MessageHandler<Tm>)messageHandlers[type] - messageHandler;
        }

        /// <summary>
        /// 根据消息真实类型触发对应订阅者。
        /// </summary>
        public void RaiseEvent<Tm>(T sender, Tm msg)
        {
            string key = msg.GetType().Name;
            if (!messageHandlers.ContainsKey(key))
            {
                Debug.LogWarning("No handler subscribed for " + key);
                return;
            }

            MessageHandler<Tm> handler = (MessageHandler<Tm>)messageHandlers[key];
            if (handler == null)
            {
                Debug.LogWarning("No handler subscribed for " + key);
                return;
            }

            try
            {
                handler(sender, msg);
            }
            catch (Exception ex)
            {
                Debug.LogError("Message handler exception: " + ex);
                if (ThrowException)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 网络层收到完整消息后，先压入队列，等待 Unity 主线程消费。
        /// </summary>
        public void ReceiveMessage(T sender, NetMessage message)
        {
            messageQueue.Enqueue(new MessageArgs
            {
                sender = sender,
                message = message
            });
        }

        /// <summary>
        /// 清空待分发队列，常用于断线重连时丢弃旧消息。
        /// </summary>
        public void Clear()
        {
            messageQueue.Clear();
        }

        /// <summary>
        /// 在 Unity 主线程逐条派发消息。
        /// 这样业务层就可以安全操作 UI、场景对象和 MonoBehaviour。
        /// </summary>
        public void Distribute()
        {
            while (messageQueue.Count > 0)
            {
                MessageArgs package = messageQueue.Dequeue();
                if (package.message.Request != null)
                {
                    MessageDispatch<T>.Instance.Dispatch(package.sender, package.message.Request);
                }

                if (package.message.Response != null)
                {
                    MessageDispatch<T>.Instance.Dispatch(package.sender, package.message.Response);
                }
            }
        }
    }
}
