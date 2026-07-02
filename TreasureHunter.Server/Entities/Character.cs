using Network;
using SkillBridge.Message;

namespace GameServer.Entities;

public sealed class Character : IPostResponser
{
    public Character(CharacterType type, TCharacter data)
    {
        Data = data;
        Id = data.ID;
        Info = new NCharacterInfo
        {
            Id = checked((int)data.ID),
            ConfigId = data.TID,
            EntityId = checked((int)data.ID),
            Name = data.Name,
            Type = type,
            Class = (CharacterClass)data.Class,
            Level = data.Level,
            mapId = data.MapID,
            Gold = data.Gold,
            SlotIndex = data.SlotIndex
        };
    }

    public long Id { get; }
    public TCharacter Data { get; }
    public NCharacterInfo Info { get; }

    public void PostProcess(NetMessageResponse message)
    {
    }

    public void Clear()
    {
    }

    public NCharacterInfo GetBasicInfo()
    {
        return new NCharacterInfo
        {
            Id = Info.Id,
            ConfigId = Info.ConfigId,
            Name = Info.Name,
            Type = Info.Type,
            Class = Info.Class,
            Level = Info.Level,
            mapId = Info.mapId,
            Gold = Info.Gold,
            SlotIndex = Info.SlotIndex
        };
    }
}
