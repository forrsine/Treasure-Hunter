using ProtoBuf;
using System.Collections.Generic;
using System.Xml.Linq;

namespace SkillBridge.Message
{
    [ProtoContract]
    public class NetMessage
    {
        [ProtoMember(1)]
        public NetMessageRequest Request { get; set; }

        [ProtoMember(2)]
        public NetMessageResponse Response { get; set; }
    }

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

    [ProtoContract]
    public class UserRegisterRequest
    {
        [ProtoMember(1, Name = "user")]
        public string User { get; set; } = "";

        [ProtoMember(2, Name = "password")]
        public string Password { get; set; } = "";
    }

    [ProtoContract]
    public class UserRegisterResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";
    }

    [ProtoContract]
    public class UserLoginRequest
    {
        [ProtoMember(1, Name = "user")]
        public string User { get; set; } = "";

        [ProtoMember(2, Name = "password")]
        public string Password { get; set; } = "";
    }

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

    [ProtoContract]
    public class UserGameEnterRequest
    {
        [ProtoMember(1, Name = "characterIdx")]
        public int characterIdx { get; set; }
    }

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

    [ProtoContract]
    public class UserGameLeaveRequest
    {
    }

    [ProtoContract]
    public class UserGameLeaveResponse
    {
        [ProtoMember(1, Name = "result")]
        public Result Result { get; set; }

        [ProtoMember(2, Name = "errormsg")]
        public string Errormsg { get; set; } = "";
    }

    [ProtoContract]
    public class NUserInfo
    {
        [ProtoMember(1, Name = "id")]
        public int Id { get; set; }

        [ProtoMember(2, Name = "player")]
        public NPlayerInfo Player { get; set; }
    }

    [ProtoContract]
    public class NPlayerInfo
    {
        [ProtoMember(1, Name = "id")]
        public int Id { get; set; }

        [ProtoMember(2, Name = "characters")]
        public List<NCharacterInfo> Characters { get; } = new List<NCharacterInfo>();
    }

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

    [ProtoContract(Name = "RESULT")]
    public enum Result
    {
        [ProtoEnum(Name = "SUCCESS")]
        Success = 0,

        [ProtoEnum(Name = "FAILED")]
        Failed = 1
    }

    [ProtoContract(Name = "CHARACTER_TYPE")]
    public enum CharacterType
    {
        Player = 0,

        [ProtoEnum(Name = "NPC")]
        Npc = 1,

        Monster = 2
    }

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
