using GameServer;
using GameServer.Entities;
using GameServer.Services;
using SkillBridge.Message;

namespace Network;

public sealed class NetSession : INetSession
{
    private NetMessage? _response;

    public TUser? User { get; set; }
    public Character? Character { get; set; }
    public IPostResponser? PostResponser { get; set; }

    public NetMessageResponse Response
    {
        get
        {
            _response ??= new NetMessage();
            _response.Response ??= new NetMessageResponse();
            return _response.Response;
        }
    }

    public byte[]? GetResponse()
    {
        if (_response == null)
        {
            return null;
        }

        PostResponser?.PostProcess(Response);

        byte[] data = PackageHandler.PackMessage(_response);
        _response = null;
        return data;
    }

    public void Disconnected()
    {
        PostResponser = null;
        if (Character != null)
        {
            UserService.Instance.CharacterLeave(Character);
        }
    }
}
