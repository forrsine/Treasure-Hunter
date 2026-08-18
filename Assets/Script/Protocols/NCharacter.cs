using System;

/// <summary>
/// 客户端使用的轻量角色存档模型。
/// 它由网络 NCharacterInfo 转换而来，供角色选择、跨场景暂存和游戏角色生成使用。
/// </summary>
[Serializable]
public class NCharacter
{
    public long id;
    public int slotIndex;
    public string name;
    public int classId;
    public int level;
    public int exp;
}
