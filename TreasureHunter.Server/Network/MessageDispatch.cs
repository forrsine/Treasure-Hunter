using Common;
using SkillBridge.Message;

namespace Network;

/// <summary>把外层 NetMessageRequest/Response 拆成具体业务消息并交给订阅者。</summary>
public class MessageDispatch<T> : Singleton<MessageDispatch<T>>
{
    /// <summary>
    /// 拆分客户端请求消息。
    /// </summary>
    public void Dispatch(T sender, NetMessageRequest message)
    {
        if (message.userRegister != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userRegister); }
        if (message.userLogin != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userLogin); }
        if (message.createChar != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.createChar); }
        if (message.gameEnter != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameEnter); }
        if (message.gameLeave != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameLeave); }
    }

    /// <summary>
    /// 拆分服务端响应消息。
    /// 当前服务端主要处理请求，但保留响应拆分能让测试与回环调试更方便。
    /// </summary>
    public void Dispatch(T sender, NetMessageResponse message)
    {
        if (message.userRegister != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userRegister); }
        if (message.userLogin != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.userLogin); }
        if (message.createChar != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.createChar); }
        if (message.gameEnter != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameEnter); }
        if (message.gameLeave != null) { MessageDistributer<T>.Instance.RaiseEvent(sender, message.gameLeave); }
    }
}
