using ProtoBuf;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SkillBridge.Message
{
    /// <summary>网络传输的最外层消息，同一包只使用 Request 或 Response。</summary>
    [ProtoContract]
    public class NetMessage
    {
        [ProtoMember(1)]
        public NetMessageRequest Request { get; set; }

        [ProtoMember(2)]
        public NetMessageResponse Response { get; set; }
    }

    /// <summary>客户端发往服务端的请求集合，具体业务由非空字段决定。</summary>
    [ProtoContract]
    public class NetMessageRequest
    {
        [ProtoMember(1)]
        public UserRegisterRequest userRegister { get; set; }

        [ProtoMember(2)]
        public UserLoginRequest userLogin { get; set; }

        [ProtoMember(3)]
        public UserCreateCharacterRequest createChar { get; set; }

        [ProtoMember(4)]
        public UserGameEnterRequest gameEnter { get; set; }

        [ProtoMember(5)]
        public UserGameLeaveRequest gameLeave { get; set; }
    }

    /// <summary>服务端发往客户端的响应集合，字段编号必须与服务端协议保持一致。</summary>
    [ProtoContract]
    public class NetMessageResponse
    {
        [ProtoMember(1)]
        public UserRegisterResponse userRegister { get; set; }

        [ProtoMember(2)]
        public UserLoginResponse userLogin { get; set; }

        [ProtoMember(3)]
        public UserCreateCharacterResponse createChar { get; set; }

        [ProtoMember(4)]
        public UserGameEnterResponse gameEnter { get; set; }

        [ProtoMember(5)]
        public UserGameLeaveResponse gameLeave { get; set; }
    }

    /// <summary>注册账号请求。密码只用于本次传输，不应写入客户端日志。</summary>
    [ProtoContract]
    public class UserRegisterRequest
    {
        [ProtoMember(1, Name = "user")]
        public string User { get; set; } = "";

        [ProtoMember(2, Name = "password")]
        public string Password { get; set; } = "";
    }

    /// <summary>注册结果。</summary>
    [ProtoContract]
    public class UserRegisterResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";
    }

    /// <summary>账号密码登录请求。</summary>
    [ProtoContract]
    public class UserLoginRequest
    {
        [ProtoMember(1, Name = "user")]
        public string User { get; set; } = "";

        [ProtoMember(2, Name = "password")]
        public string Password { get; set; } = "";
    }

    /// <summary>登录结果及玩家角色列表。</summary>
    [ProtoContract]
    public class UserLoginResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";

        [ProtoMember(3, Name = "userinfo")]
        public NUserInfo Userinfo { get; set; }
    }

    /// <summary>在指定存档槽创建职业角色。</summary>
    [ProtoContract]
    public class UserCreateCharacterRequest
    {
        [ProtoMember(1, Name = "name")]
        public string Name { get; set; } = "";

        [ProtoMember(2, Name = "class")]
        public CharacterClass Class { get; set; }

        [ProtoMember(3, Name = "slot")]
        public int SlotIndex { get; set; }
    }

    /// <summary>创建角色结果，并返回最新角色列表。</summary>
    [ProtoContract]
    public class UserCreateCharacterResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";

        [ProtoMember(3, Name = "characters")]
        public List<NCharacterInfo> Characters { get; } = new List<NCharacterInfo>();
    }

    /// <summary>请求让角色列表中指定下标的角色进入游戏。</summary>
    [ProtoContract]
    public class UserGameEnterRequest
    {
        [ProtoMember(1, Name = "characterIdx")]
        public int characterIdx { get; set; }
    }

    /// <summary>进入游戏结果和当前角色网络数据。</summary>
    [ProtoContract]
    public class UserGameEnterResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";

        [ProtoMember(3, Name = "character")]
        public NCharacterInfo Character { get; set; }
    }

    /// <summary>离开当前游戏角色的请求。</summary>
    [ProtoContract]
    public class UserGameLeaveRequest
    {
    }

    /// <summary>离开游戏结果。</summary>
    [ProtoContract]
    public class UserGameLeaveResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";
    }

    /// <summary>登录用户的网络传输数据。</summary>
    [ProtoContract]
    public class NUserInfo
    {
        [ProtoMember(1, Name = "id")]
        public int Id { get; set; }

        [ProtoMember(2, Name = "player")]
        public NPlayerInfo Player { get; set; }
    }

    /// <summary>玩家账号下的角色集合。</summary>
    [ProtoContract]
    public class NPlayerInfo
    {
        [ProtoMember(1, Name = "id")]
        public int Id { get; set; }

        [ProtoMember(2, Name = "characters")]
        public List<NCharacterInfo> Characters { get; } = new List<NCharacterInfo>();
    }

    /// <summary>单个角色在客户端与服务端之间传输的基础数据。</summary>
    [ProtoContract]
    public class NCharacterInfo
    {
        [ProtoMember(1, Name = "id")]
        public int Id { get; set; }

        [ProtoMember(2, Name = "config_id")]
        public int ConfigId { get; set; }

        [ProtoMember(3, Name = "entity_id")]
        public int EntityId { get; set; }

        [ProtoMember(4, Name = "name")]
        public string Name { get; set; } = "";

        [ProtoMember(5, Name = "type")]
        public CharacterType Type { get; set; }

        [ProtoMember(6, Name = "class")]
        public CharacterClass Class { get; set; }

        [ProtoMember(7, Name = "level")]
        public int Level { get; set; }

        [ProtoMember(8)]
        public int mapId { get; set; }

        [ProtoMember(9, Name = "gold")]
        public long Gold { get; set; }

        [ProtoMember(10, Name = "slot_index")]
        public int SlotIndex { get; set; }
    }

    /// <summary>通用业务处理结果。</summary>
    [ProtoContract(Name = "RESULT")]
    public enum Result
    {
        [ProtoEnum(Name = "SUCCESS")]
        Success = 0,

        [ProtoEnum(Name = "FAILED")]
        Failed = 1
    }

    /// <summary>网络实体类型。</summary>
    [ProtoContract(Name = "CHARACTER_TYPE")]
    public enum CharacterType
    {
        Player = 0,

        [ProtoEnum(Name = "NPC")]
        Npc = 1,

        Monster = 2
    }

    /// <summary>可创建的职业编号；数值同时用于数据库和 CharacterDefine 配置。</summary>
    [ProtoContract(Name = "CHARACTER_CLASS")]
    public enum CharacterClass
    {
        [ProtoEnum(Name = "NONE")]
        None = 0,

        [ProtoEnum(Name = "WARRIOR")]
        Warrior = 1,

        [ProtoEnum(Name = "WIZARD")]
        Wizard = 2,

        [ProtoEnum(Name = "ARCHER")]
        Archer = 3,

        [ProtoEnum(Name = "ASSASSIN")]
        Assassin = 4
    }
}
