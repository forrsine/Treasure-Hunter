using Network;
using SkillBridge.Message;

namespace GameServer.Entities;

/// <summary>
/// 已进入游戏的角色实体：关联数据库记录与网络角色信息。
/// 数据库存档负责持久化，Info 负责发送给客户端，两者职责保持分离。
/// </summary>
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

    /// <summary>
    /// 在发送响应前给当前角色补充同步数据。
    /// 现在还是空实现，后续如果扩位置、血量、地图状态同步，可以从这里追加。
    /// </summary>
    public void PostProcess(NetMessageResponse message)
    {
    }

    /// <summary>
    /// 清理角色运行时资源。
    /// 当前版本角色运行时状态较少，所以暂时为空实现。
    /// </summary>
    public void Clear()
    {
    }

    /// <summary>
    /// 返回一份新的基础信息 DTO，避免外部直接修改 Character 内部持有的 Info。
    /// </summary>
    public NCharacterInfo GetBasicInfo()
    {
        // 返回新的 DTO，避免调用方直接修改实体内部持有的 Info。
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
