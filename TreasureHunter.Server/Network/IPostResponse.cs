using SkillBridge.Message;

namespace Network;

/// <summary>响应发送前的扩展点，游戏实体可以在封包前追加需要同步的数据。</summary>
public interface IPostResponser
{
    void PostProcess(NetMessageResponse message);
}
