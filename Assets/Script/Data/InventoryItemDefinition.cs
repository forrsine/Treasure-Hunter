using UnityEngine;

/// <summary>
/// 物品分类：首版只用于背包详情展示，后续可作为“使用、装备、任务提交”等行为的分发依据。
/// </summary>
public enum InventoryItemCategory
{
    Consumable,
    Material,
    Quest
}

/// <summary>
/// 物品品质：数据层只保存枚举，具体显示颜色由 UI 决定，避免核心数据依赖表现样式。
/// </summary>
public enum InventoryItemRarity
{
    Common,
    Uncommon,
    Rare,
    Epic
}

/// <summary>
/// 物品使用效果：只描述消耗品要触发哪类规则，真正的玩家数值修改仍由对应 System 负责。
/// </summary>
public enum InventoryItemUseEffect
{
    None,
    RestoreHealth,
    RestoreMana
}

/// <summary>
/// 物品静态配置：描述“这个物品是什么”，不保存玩家当前拥有的数量。
/// 同一种物品的名称、图标和堆叠上限只配置一次，所有运行时格子共同引用该资源。
/// </summary>
[CreateAssetMenu(fileName = "InventoryItem", menuName = "Treasure Hunter/Inventory/Item Definition")]
public sealed class InventoryItemDefinition : ScriptableObject
{
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private InventoryItemCategory category;
    [SerializeField] private InventoryItemRarity rarity;
    [SerializeField] private Sprite icon;
    [SerializeField, TextArea(3, 6)] private string description;
    [SerializeField, Min(1)] private int maxStack = 1;
    [SerializeField] private InventoryItemUseEffect useEffect;
    [SerializeField, Range(0f, 1f)] private float restorePercent;
    [SerializeField] private Color displayTint = Color.white;

    public string ItemId => itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public InventoryItemCategory Category => category;
    public InventoryItemRarity Rarity => rarity;
    public Sprite Icon => icon;
    public string Description => description;
    public int MaxStack => Mathf.Max(1, maxStack);
    public InventoryItemUseEffect UseEffect => useEffect;
    public float RestorePercent => Mathf.Clamp01(restorePercent);
    // 兼容本字段加入前已经创建的物品资源：旧资源反序列化后可能得到透明黑色，
    // UI 与地面表现应退回白色，而不是把图标意外显示成完全透明。
    public Color DisplayTint => displayTint.a <= 0f ? Color.white : displayTint;
    public bool IsUsable => useEffect != InventoryItemUseEffect.None && RestorePercent > 0f;

    /// <summary>
    /// 运行时判断两个配置是否代表同一种物品。
    /// 正常情况会比较 ScriptableObject 引用；itemId 让重新加载资源后仍能保持稳定身份。
    /// </summary>
    public bool IsSameItem(InventoryItemDefinition other)
    {
        if (other == null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(itemId) && itemId == other.itemId;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxStack = Mathf.Max(1, maxStack);
        restorePercent = Mathf.Clamp01(restorePercent);
        if (string.IsNullOrWhiteSpace(itemId))
        {
            itemId = name.Trim().ToLowerInvariant().Replace(' ', '_');
        }
    }
#endif
}
