using Common;
using GameServer.Entities;
using SkillBridge.Message;

namespace GameServer.Managers;

public sealed class CharacterManager : Singleton<CharacterManager>
{
    public Dictionary<long, Character> Characters { get; } = new();

    public void Init()
    {
    }

    public void Clear()
    {
        Characters.Clear();
    }

    public Character AddCharacter(TCharacter data)
    {
        Character character = new Character(CharacterType.Player, data);
        Characters[character.Id] = character;
        return character;
    }

    public void RemoveCharacter(long characterId)
    {
        Characters.Remove(characterId);
    }

    public Character? GetCharacter(long characterId)
    {
        Characters.TryGetValue(characterId, out Character? character);
        return character;
    }
}
