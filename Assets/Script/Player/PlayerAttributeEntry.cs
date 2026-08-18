/// <summary>
/// 玩家属性面板的一行只读数据。
/// 该结构不依赖具体玩家组件或 UI，因此 Query 可以在数据层生成，任意界面都能复用。
/// </summary>
public readonly struct PlayerAttributeEntry
{
    public PlayerAttributeEntry(string groupName, string key, string label, string value)
    {
        GroupName = groupName;
        Key = key;
        Label = label;
        Value = value;
    }

    public string GroupName { get; }
    public string Key { get; }
    public string Label { get; }
    public string Value { get; }
}
