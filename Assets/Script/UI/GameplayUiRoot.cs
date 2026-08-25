using UnityEngine;

/// <summary>
/// 玩法 UI 组合根：明确持有本场景的玩家 HUD、会话、属性、升级和背包界面。
/// 所有引用都由 GameplayUiRoot Prefab 序列化保存；运行时不 GetComponent、不查找，也不自动补组件。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplayUiRoot : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private PlayerHudUi playerHudUi;
    [SerializeField] private GameSessionUi sessionUi;
    [SerializeField] private PlayerAttributePanel attributePanel;
    [SerializeField] private PlayerLevelUpPanel levelUpPanel;
    [SerializeField] private InventoryPanel inventoryPanel;
    [SerializeField] private PlayerChargeBarUi chargeBarUi;

    /// <summary>
    /// 运行时只做一次引用校验。
    /// 纯 Prefab 引用模式下，如果根引用都没配对，就应该立刻报错，而不是悄悄兜底。
    /// </summary>
    private void Awake()
    {
        if (Application.isPlaying && !ValidatePrefabReferences(true))
        {
            enabled = false;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器下只做静态校验，方便你在 Inspector 一改引用就能立刻发现问题。
    /// </summary>
    private void OnValidate()
    {
        ValidatePrefabReferences(false);
    }
#endif

    /// <summary>
    /// 检查 UI 根节点的关键引用是否完整。
    /// 这个类相当于 Gameplay UI 的“装配清单”，少任何一个都可能导致局内界面失效。
    /// </summary>
    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (playerHudUi == null) missing = nameof(playerHudUi);
        else if (sessionUi == null) missing = nameof(sessionUi);
        else if (attributePanel == null) missing = nameof(attributePanel);
        else if (levelUpPanel == null) missing = nameof(levelUpPanel);
        else if (inventoryPanel == null) missing = nameof(inventoryPanel);
        else if (chargeBarUi == null || !chargeBarUi.ValidatePrefabReferences(false)) missing = nameof(chargeBarUi);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"GameplayUiRoot 的 Prefab 引用未配置：{missing}。", this);
        }

        return false;
    }
}
