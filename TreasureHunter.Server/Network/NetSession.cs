using GameServer;
using GameServer.Entities;
using GameServer.Services;
using SkillBridge.Message;

namespace Network;

/// <summary>
/// 单个客户端连接的业务会话：保存已登录用户、当前在线角色和待发送响应。
/// 每次 SendResponse 取走响应后会清空缓冲，避免旧字段混入下一条消息。
/// </summary>
public sealed class NetSession : INetSession
{
    private NetMessage? _response;

    public TUser? User { get; set; }
    public Character? Character { get; set; }
    public IPostResponser? PostResponser { get; set; }

    /// <summary>
    /// 取得当前可写响应对象。
    /// 懒加载的好处是：只有确实要回复客户端时，才创建这一层包装对象。
    /// </summary>
    public NetMessageResponse Response
    {
        get
        {
            _response ??= new NetMessage();
            _response.Response ??= new NetMessageResponse();
            return _response.Response;
        }
    }

    /// <summary>
    /// 取出本次待发送响应并序列化成字节数组。
    /// 发送完后会把缓存置空，避免上一条消息的字段混进下一条响应。
    /// </summary>
    public byte[]? GetResponse()
    {
        if (_response == null)
        {
            return null;
        }

        // 在序列化前给当前游戏实体最后一次追加同步数据的机会。
        PostResponser?.PostProcess(Response);

        byte[] data = PackageHandler.PackMessage(_response);
        _response = null;
        return data;
    }

    /// <summary>
    /// 连接断开时的业务清理。
    /// 如果该连接上还有在线角色，要同步触发离场逻辑。
    /// </summary>
    public void Disconnected()
    {
        PostResponser = null;
        if (Character != null)
        {
            UserService.Instance.CharacterLeave(Character);
        }
    }
}
