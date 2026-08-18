using Common;
using GameServer.Entities;
using SkillBridge.Message;

namespace GameServer.Managers;

/// <summary>
/// 在线角色管理器：用角色实体 ID 维护已进入游戏的角色集合。
/// 当前是单进程内存版本，后续多人场景可在这里扩展地图分区和广播查询。
/// </summary>
public sealed class CharacterManager : Singleton<CharacterManager>
{
    public Dictionary<long, Character> Characters { get; } = new();

    public void Init()
    {
    }

    /// <summary>
    /// 清空当前所有在线角色，通常在停服或重置测试环境时使用。
    /// </summary>
    public void Clear()
    {
        Characters.Clear();
    }

    /// <summary>
    /// 把数据库角色记录转换成在线角色实体并登记到在线表。
    /// </summary>
    public Character AddCharacter(TCharacter data)
    {
        Character character = new Character(CharacterType.Player, data);
        Characters[character.Id] = character;
        return character;
    }

    /// <summary>
    /// 移除一个在线角色。
    /// </summary>
    public void RemoveCharacter(long characterId)
    {
        Characters.Remove(characterId);
    }

    /// <summary>
    /// 根据角色 ID 查询当前在线角色。
    /// </summary>
    public Character? GetCharacter(long characterId)
    {
        Characters.TryGetValue(characterId, out Character? character);
        return character;
    }
}
