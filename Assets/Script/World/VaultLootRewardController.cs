using QFramework;
using UnityEngine;

/// <summary>
/// 旧版宝箱背包奖励桥接器：监听当前宝箱的正式击破事件，并把抽取结果交给背包 Command。
/// 当前版本需求改为“箱子不掉落物品”，所以这个脚本保留但不再挂到 Box.prefab 上。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCo))]
public sealed class VaultLootRewardController : MonoBehaviour, IController
{
    [SerializeField] private InventoryDatabase inventoryDatabase;

    private BoxCo vault;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        vault = GetComponent<BoxCo>();
    }

    private void OnEnable()
    {
        BoxCo.OnVaultDestroyed += HandleVaultDestroyed;
    }

    private void OnDisable()
    {
        BoxCo.OnVaultDestroyed -= HandleVaultDestroyed;
    }

    /// <summary>
    /// 静态事件会通知场景中的所有监听者，因此先过滤为自己负责的宝箱，再进行一次掉落。
    /// 开发者快捷键仍然调用 BoxCo 的正式击破流程，所以也会自然进入这里。
    /// </summary>
    private void HandleVaultDestroyed(BoxCo destroyedVault)
    {
        if (destroyedVault == null || destroyedVault != vault)
        {
            return;
        }

        InventoryDatabase database = inventoryDatabase != null
            ? inventoryDatabase
            : this.GetSystem<InventorySystem>().Database;
        if (database == null)
        {
            Debug.LogWarning("宝箱没有配置 InventoryDatabase，已跳过背包掉落。", this);
            return;
        }

        if (!database.TryRollVaultLoot(Random.value, out InventoryItemDefinition item, out int amount))
        {
            Debug.LogWarning("InventoryDatabase 中没有可用的宝箱掉落项。", database);
            return;
        }

        InventoryAddResult result = this.SendCommand(new AddInventoryItemCommand(item, amount));
        if (result.RemainingAmount > 0)
        {
            Debug.LogWarning(
                $"背包空间不足：{item.DisplayName} 加入 {result.AddedAmount} 个，剩余 {result.RemainingAmount} 个未加入。",
                this);
        }
    }
}
