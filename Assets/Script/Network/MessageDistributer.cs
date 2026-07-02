using SkillBridge.Message;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Network
{
    public class MessageDistributer : MessageDistributer<object>
    {
    }

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

        public void Subscribe<Tm>(MessageHandler<Tm> messageHandler)
        {
            string type = typeof(Tm).Name;
            if (!messageHandlers.ContainsKey(type))
            {
                messageHandlers[type] = null;
            }

            messageHandlers[type] = (MessageHandler<Tm>)messageHandlers[type] + messageHandler;
        }

        public void Unsubscribe<Tm>(MessageHandler<Tm> messageHandler)
        {
            string type = typeof(Tm).Name;
            if (!messageHandlers.ContainsKey(type))
            {
                messageHandlers[type] = null;
            }

            messageHandlers[type] = (MessageHandler<Tm>)messageHandlers[type] - messageHandler;
        }

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

        public void ReceiveMessage(T sender, NetMessage message)
        {
            messageQueue.Enqueue(new MessageArgs
            {
                sender = sender,
                message = message
            });
        }

        public void Clear()
        {
            messageQueue.Clear();
        }

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
