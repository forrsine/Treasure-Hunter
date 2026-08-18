namespace Network;

/// <summary>网络会话最小接口，约定如何取出响应数据以及断线时如何清理业务状态。</summary>
public interface INetSession
{
    byte[]? GetResponse();
    void Disconnected();
}
