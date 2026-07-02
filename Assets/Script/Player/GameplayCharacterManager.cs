using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GameplayCharacterManager
{
    private static readonly GameplayCharacterManager instance = new GameplayCharacterManager();

    private readonly Dictionary<long, GameplayCharacter> characters = new Dictionary<long, GameplayCharacter>();
    private long nextTransientEntityId = -1;

    private GameplayCharacterManager()
    {
    }

    public static GameplayCharacterManager Instance => instance;
    public IReadOnlyDictionary<long, GameplayCharacter> Characters => characters;

    public event Action<GameplayCharacter> CharacterEntered;
    public event Action<GameplayCharacter> CharacterLeft;

    public GameplayCharacter EnterCurrentCharacter(
        NCharacter selectedCharacter,
        Vector3 position,
        Quaternion rotation,
        int fallbackClassId)
    {
        Clear();

        int classId = selectedCharacter != null ? selectedCharacter.classId : fallbackClassId;
        CharacterDefine define = ResolveCharacterDefine(classId, fallbackClassId);
        if (define == null)
        {
            return null;
        }

        NCharacter save = selectedCharacter ?? CreateFallbackCharacter(define);
        long entityId = CreateEntityId(save);
        GameplayCharacter character = new GameplayCharacter(
            entityId,
            save,
            define,
            true,
            position,
            rotation);

        AddCharacter(character);
        return character;
    }

    public void AddCharacter(GameplayCharacter character)
    {
        if (character == null)
        {
            return;
        }

        if (characters.ContainsKey(character.EntityId))
        {
            RemoveCharacter(character.EntityId);
        }

        characters[character.EntityId] = character;
        CharacterEntered?.Invoke(character);
    }

    public void RemoveCharacter(long entityId)
    {
        if (!characters.TryGetValue(entityId, out GameplayCharacter character))
        {
            return;
        }

        CharacterLeft?.Invoke(character);
        characters.Remove(entityId);
    }

    public void Clear()
    {
        if (characters.Count == 0)
        {
            return;
        }

        List<GameplayCharacter> snapshot = new List<GameplayCharacter>(characters.Values);
        characters.Clear();

        foreach (GameplayCharacter character in snapshot)
        {
            CharacterLeft?.Invoke(character);
        }
    }

    private CharacterDefine ResolveCharacterDefine(int classId, int fallbackClassId)
    {
        CharacterDataManager dataManager = CharacterDataManager.Instance;
        if (dataManager == null)
        {
            Debug.LogError("CharacterDataManager is missing. Cannot enter gameplay scene.");
            return null;
        }

        CharacterDefine define = dataManager.GetCharacter(classId);
        if (define != null || classId == fallbackClassId)
        {
            return define;
        }

        Debug.LogWarning($"Character class {classId} is unavailable. Falling back to class {fallbackClassId}.");
        return dataManager.GetCharacter(fallbackClassId);
    }

    private NCharacter CreateFallbackCharacter(CharacterDefine define)
    {
        return new NCharacter
        {
            id = 0,
            slotIndex = -1,
            name = define != null ? define.name : "Player",
            classId = define != null ? define.classId : 1,
            level = define != null ? Mathf.Max(1, define.initLevel) : 1,
            exp = 0
        };
    }

    private long CreateEntityId(NCharacter save)
    {
        if (save != null && save.id > 0)
        {
            return save.id;
        }

        return nextTransientEntityId--;
    }
}
