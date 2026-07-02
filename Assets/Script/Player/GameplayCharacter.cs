using UnityEngine;

public sealed class GameplayCharacter
{
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
