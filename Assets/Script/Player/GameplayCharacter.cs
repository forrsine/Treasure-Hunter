using UnityEngine;

/// <summary>
/// 游戏场景中的角色运行时描述。
/// 它把服务端存档、职业静态配置和当前场景位置组合在一起，但不负责生成 GameObject。
/// </summary>
public sealed class GameplayCharacter
{
    /// <summary>
    /// 构造一份“当前场景里的角色描述数据”。
    /// 注意这里还不是 GameObject，只是把存档、职业配置和出生点先打包好。
    /// </summary>
    public GameplayCharacter(
        long entityId,
        NCharacter save,
        CharacterDefine define,
        bool isCurrentPlayer,
        Vector3 position,
        Quaternion rotation)
    {
        EntityId = entityId;
        Save = save;
        Define = define;
        IsCurrentPlayer = isCurrentPlayer;
        Position = position;
        Rotation = rotation;
    }

    public long EntityId { get; }
    public NCharacter Save { get; }
    public CharacterDefine Define { get; }
    public bool IsCurrentPlayer { get; }
    public Vector3 Position { get; set; }
    public Quaternion Rotation { get; set; }

    public long Id => Save != null ? Save.id : EntityId;
    public int ClassId => Save != null ? Save.classId : Define != null ? Define.classId : 0;
    public int Level => Save != null ? Mathf.Max(1, Save.level) : Define != null ? Mathf.Max(1, Define.initLevel) : 1;
    public int Exp => Save != null ? Mathf.Max(0, Save.exp) : 0;

    /// <summary>
    /// 统一获取角色显示名。
    /// 优先使用存档名；没有存档时退回职业名；再没有就用默认名字。
    /// </summary>
    public string Name
    {
        get
        {
            if (Save != null && !string.IsNullOrWhiteSpace(Save.name))
            {
                return Save.name;
            }

            if (Define != null && !string.IsNullOrWhiteSpace(Define.name))
            {
                return Define.name;
            }

            return "Player";
        }
    }
}
