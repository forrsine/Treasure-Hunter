using SkillBridge.Message;

namespace Network
{
    /// <summary>
    /// 网络消息拆分器：检查 NetMessage 中实际携带的请求/响应类型，
    /// 再交给 MessageDistributer 按具体消息类型通知业务订阅者。
    /// </summary>
    public class MessageDispatch<T> where T : class
    {
        private static MessageDispatch<T> instance;

        public static MessageDispatch<T> Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MessageDispatch<T>();
                }

                return instance;
            }
        }

        /// <summary>
        /// 拆分服务端响应消息。
        /// 外层响应壳里可能包含不同业务字段，这里只派发当前真正存在的那一项。
        /// </summary>
        public void Dispatch(T sender, NetMessageResponse message)
        {
            // Protobuf 外层消息可能包含不同业务响应，这里只派发本次真正存在的字段。
            if (message.userRegister != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userRegister); }
            if (message.userLogin != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userLogin); }
            if (message.createChar != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.createChar); }
            if (message.gameEnter != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameEnter); }
            if (message.gameLeave != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameLeave); }
        }

        /// <summary>
        /// 拆分客户端请求消息。
        /// 服务端收到协议壳后，再把具体请求投递给对应业务处理器。
        /// </summary>
        public void Dispatch(T sender, NetMessageRequest message)
        {
            if (message.userRegister != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userRegister); }
            if (message.userLogin != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userLogin); }
            if (message.createChar != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.createChar); }
            if (message.gameEnter != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameEnter); }
            if (message.gameLeave != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameLeave); }
        }
    }
}
