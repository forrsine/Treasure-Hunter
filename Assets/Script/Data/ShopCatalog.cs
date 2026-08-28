using System;
using UnityEngine;

/// <summary>商店静态目录：只描述“卖什么、多少钱”，不保存任何角色购买状态。</summary>
[CreateAssetMenu(fileName = "ShopCatalog", menuName = "Treasure Hunter/Shop/Catalog")]
public sealed class ShopCatalog : ScriptableObject
{
    public const string ResourcesPath = "Data/Shop/ShopCatalog";

    [SerializeField] private ShopCatalogEntry[] entries = Array.Empty<ShopCatalogEntry>();

    public ShopCatalogEntry[] Entries => entries;

    public bool Contains(ShopCatalogEntry entry)
    {
        if (entry == null || entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (ReferenceEquals(entries[i], entry))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetEntry(string entryId, out ShopCatalogEntry entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(entryId) || entries == null)
        {
            return false;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            ShopCatalogEntry candidate = entries[i];
            if (candidate != null && string.Equals(candidate.EntryId, entryId, StringComparison.Ordinal))
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }
}
