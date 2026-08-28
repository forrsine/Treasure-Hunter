using QFramework;
using UnityEngine;

/// <summary>金库金币奖励：每次正式击破生成一个可拾取金币对象。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCo))]
public sealed class VaultGoldRewardController : MonoBehaviour, IController
{
    private const float DefaultPickupRadius = 0.7f;

    [SerializeField] private EconomyConfig economyConfig;
    [SerializeField] private Transform rewardSpawnPoint;

    [Header("自动掉落点")]
    [Tooltip("没有手动掉落点时，金币与宝箱表面之间额外保留的距离。金币触发球半径会另外计入。")]
    [SerializeField, Min(0f)] private float surfacePadding = 0.2f;

    [Tooltip("金币根节点高于宝箱碰撞体底部的距离。视觉模型自身还会继续向上悬浮。")]
    [SerializeField, Min(0f)] private float heightAboveColliderBottom = 0.2f;

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

    private void OnValidate()
    {
        surfacePadding = Mathf.Max(0f, surfacePadding);
        heightAboveColliderBottom = Mathf.Max(0f, heightAboveColliderBottom);
    }

    private void HandleVaultDestroyed(BoxCo destroyedVault)
    {
        if (destroyedVault == null || destroyedVault != vault)
        {
            return;
        }

        EconomyConfig config = economyConfig != null ? economyConfig : this.GetSystem<EconomySystem>().Config;
        if (config == null || config.WorldGoldPickupPrefab == null)
        {
            Debug.LogWarning("金库金币掉落缺少 EconomyConfig 或金币 Prefab。", this);
            return;
        }

        int gold = config.CalculateVaultGold(destroyedVault.DestroyedCount);
        Vector3 position = ResolveRewardPosition(destroyedVault, config.WorldGoldPickupPrefab);
        WorldGoldPool.Instance.Get(config.WorldGoldPickupPrefab, gold, position, config.ImportantPickupLifetimeSeconds);
    }

    /// <summary>
    /// 手动掉落点优先；未配置时把金币放在宝箱朝向玩家的一侧。
    /// 安全距离包含金币 Trigger 半径，避免 Trigger 仍有一部分埋在实体碰撞体中。
    /// </summary>
    private Vector3 ResolveRewardPosition(BoxCo destroyedVault, GameObject pickupPrefab)
    {
        if (rewardSpawnPoint != null)
        {
            return rewardSpawnPoint.position;
        }

        Collider vaultCollider = destroyedVault.GetComponent<Collider>();
        Vector3 targetDirection = destroyedVault.transform.forward;
        PlayerRuntimeController player = GameplayRuntime.Instance.CurrentPlayer;
        if (player != null)
        {
            Vector3 directionToPlayer = player.transform.position - destroyedVault.transform.position;
            if (Vector3.ProjectOnPlane(directionToPlayer, Vector3.up).sqrMagnitude > 0.0001f)
            {
                targetDirection = directionToPlayer;
            }
        }

        float pickupRadius = GetPickupHorizontalRadius(pickupPrefab);
        return WorldPickupSpawnUtility.CalculateOutsidePosition(
            vaultCollider,
            destroyedVault.transform.position,
            targetDirection,
            pickupRadius + surfacePadding,
            heightAboveColliderBottom);
    }

    private static float GetPickupHorizontalRadius(GameObject pickupPrefab)
    {
        SphereCollider pickupTrigger = pickupPrefab != null
            ? pickupPrefab.GetComponent<SphereCollider>()
            : null;
        if (pickupTrigger == null)
        {
            return DefaultPickupRadius;
        }

        Vector3 scale = pickupTrigger.transform.lossyScale;
        float horizontalScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        return Mathf.Max(0f, pickupTrigger.radius * horizontalScale);
    }
}
