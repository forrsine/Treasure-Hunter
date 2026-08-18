using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏角色集合管理器。
/// 负责角色进入/离开以及唯一实体编号，具体 Prefab 创建交给 GameplayCharacterSpawner 监听事件完成。
/// </summary>
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

    /// <summary>
    /// 让当前选中的角色进入玩法场景。
    /// 这个方法只负责准备角色描述数据并发出“进入”事件，
    /// 真正的 Prefab 创建交给 GameplayCharacterSpawner 去完成。
    /// </summary>
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

    /// <summary>
    /// 把角色加入当前场景角色集合。
    /// 如果同一个实体编号已经存在，先移除旧的，再登记新的。
    /// </summary>
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

    /// <summary>
    /// 按实体编号移除一个角色，并通知监听者做销毁清理。
    /// </summary>
    public void RemoveCharacter(long entityId)
    {
        if (!characters.TryGetValue(entityId, out GameplayCharacter character))
        {
            return;
        }

        CharacterLeft?.Invoke(character);
        characters.Remove(entityId);
    }

    /// <summary>
    /// 清空当前场景内全部角色。
    /// 切换角色或重新进入玩法场景前，通常会先走这里，保证旧角色不会残留。
    /// </summary>
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

    /// <summary>
    /// 根据职业编号查找职业配置。
    /// 如果当前职业找不到，并且不是回退职业，就尝试使用保底职业配置。
    /// </summary>
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

    /// <summary>
    /// 没有合法存档时，创建一份仅用于进入场景的临时角色数据。
    /// 这能保证开发阶段即使没登录、没建角，也能直接进主场景测试。
    /// </summary>
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

    /// <summary>
    /// 生成当前角色的实体编号。
    /// 正式存档角色优先使用服务端真实 ID；临时角色则使用负数自减 ID，避免和正式数据冲突。
    /// </summary>
    private long CreateEntityId(NCharacter save)
    {
        if (save != null && save.id > 0)
        {
            return save.id;
        }

        return nextTransientEntityId--;
    }
}
