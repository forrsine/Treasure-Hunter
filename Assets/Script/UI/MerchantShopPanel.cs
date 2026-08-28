using System.Collections;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fungi 商店表现层：管理交互提示、首次对话、全屏商店和购买提示。
/// 钱包、限购和背包容量都由 System 校验，UI 只发送命令并显示结果。
/// </summary>
[DisallowMultipleComponent]
public sealed class MerchantShopPanel : MonoBehaviour, IController
{
    private const float GoldSpendFeedbackDuration = 0.45f;
    private static readonly Color GoldSpendFeedbackColor = new Color32(255, 132, 74, 255);

    [SerializeField, HideInInspector] private int visualLayoutVersion;

    [Header("Shared References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private GameSessionUi sessionUi;
    [SerializeField] private InventoryPanel inventoryPanel;

    [Header("Interaction Prompt")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Text promptText;

    [Header("First Dialogue")]
    [SerializeField] private GameObject dialogueRoot;
    [SerializeField] private Text dialogueSpeakerText;
    [SerializeField] private Text dialogueBodyText;
    [SerializeField] private Button dialogueCloseButton;

    [Header("Shop")]
    [SerializeField] private GameObject shopRoot;
    [SerializeField] private Button shopCloseButton;
    [SerializeField] private Text shopGoldText;
    [SerializeField] private ScrollRect productScrollRect;
    [SerializeField] private Button allCategoryButton;
    [SerializeField] private Button consumableCategoryButton;
    [SerializeField] private Button equipmentCategoryButton;
    [SerializeField] private Button materialCategoryButton;
    [SerializeField] private ShopItemCardView[] itemCards;

    [Header("Toast")]
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private Text toastText;
    [SerializeField, Min(0.1f)] private float toastDuration = 2.2f;

    private bool isMerchantNearby;
    private bool isDialogueOpen;
    private bool isShopOpen;
    private ShopCategory selectedCategory = ShopCategory.All;
    private bool hasCapturedGameplayState;
    private float cachedTimeScale = 1f;
    private Coroutine toastRoutine;
    private Coroutine goldSpendFeedbackRoutine;
    private Color defaultShopGoldColor = Color.white;
    private bool hasCapturedDefaultShopGoldColor;

    public bool IsDialogueOpen => isDialogueOpen;
    public bool IsShopOpen => isShopOpen;
    public bool IsModalOpen => isDialogueOpen || isShopOpen;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        // Prefab 编辑时可能为了排版临时显示模态窗口。运行时必须尽早关闭，
        // 避免透明全屏 Graphic 在 Start 校验前挡住其它 NPC 和 UI。
        if (promptRoot != null) promptRoot.SetActive(false);
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (shopRoot != null) shopRoot.SetActive(false);
        if (toastRoot != null) toastRoot.SetActive(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.RegisterEvent<MerchantProximityChangedEvent>(HandleMerchantProximityChanged);
        this.RegisterEvent<MerchantDialogueRequestedEvent>(HandleDialogueRequested);
        this.RegisterEvent<ShopOpenRequestedEvent>(HandleShopOpenRequested);
        this.RegisterEvent<GoldChangedEvent>(HandleGoldChanged);
        this.RegisterEvent<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompleted);
        this.RegisterEvent<ShopProgressRestoredEvent>(HandleShopProgressRestored);
        this.RegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        BindButtons();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<MerchantProximityChangedEvent>(HandleMerchantProximityChanged);
        this.UnRegisterEvent<MerchantDialogueRequestedEvent>(HandleDialogueRequested);
        this.UnRegisterEvent<ShopOpenRequestedEvent>(HandleShopOpenRequested);
        this.UnRegisterEvent<GoldChangedEvent>(HandleGoldChanged);
        this.UnRegisterEvent<ShopPurchaseCompletedEvent>(HandleShopPurchaseCompleted);
        this.UnRegisterEvent<ShopProgressRestoredEvent>(HandleShopProgressRestored);
        this.UnRegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        UnbindButtons();
        ResetGoldSpendFeedback();
        CloseImmediateAndRestoreState();
    }

    private void Start()
    {
        if (!ValidatePrefabReferences(true))
        {
            enabled = false;
            return;
        }

        for (int i = 0; i < itemCards.Length; i++)
        {
            itemCards[i].Initialize(HandleProductClicked);
        }

        CaptureDefaultShopGoldColor();
        promptRoot.SetActive(false);
        dialogueRoot.SetActive(false);
        shopRoot.SetActive(false);
        toastRoot.SetActive(false);
        RefreshGold(this.SendQuery(new GetGoldQuery()));
    }

    public bool TryCloseTopModal()
    {
        if (isDialogueOpen)
        {
            CloseDialogue();
            return true;
        }

        if (isShopOpen)
        {
            CloseShop();
            return true;
        }

        return false;
    }

    public void RefreshPromptVisibility()
    {
        if (promptRoot == null)
        {
            return;
        }

        bool blockedByOtherUi = sessionUi != null && sessionUi.HasBlockingOverlayExcludingMerchant;
        bool visible = isMerchantNearby && !IsModalOpen && !blockedByOtherUi &&
                       (inventoryPanel == null || !inventoryPanel.IsOpen);
        promptRoot.SetActive(visible);
        if (visible)
        {
            bool introCompleted = this.SendQuery(new IsMerchantIntroCompletedQuery());
            promptText.text = introCompleted ? "按 E 打开商店" : "按 E 与 Fungi 交谈";
        }
    }

    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (targetCanvas == null) missing = nameof(targetCanvas);
        else if (sessionUi == null) missing = nameof(sessionUi);
        else if (inventoryPanel == null) missing = nameof(inventoryPanel);
        else if (promptRoot == null) missing = nameof(promptRoot);
        else if (promptText == null) missing = nameof(promptText);
        else if (dialogueRoot == null) missing = nameof(dialogueRoot);
        else if (dialogueSpeakerText == null) missing = nameof(dialogueSpeakerText);
        else if (dialogueBodyText == null) missing = nameof(dialogueBodyText);
        else if (dialogueCloseButton == null) missing = nameof(dialogueCloseButton);
        else if (shopRoot == null) missing = nameof(shopRoot);
        else if (shopCloseButton == null) missing = nameof(shopCloseButton);
        else if (shopGoldText == null) missing = nameof(shopGoldText);
        else if (productScrollRect == null) missing = nameof(productScrollRect);
        else if (allCategoryButton == null) missing = nameof(allCategoryButton);
        else if (consumableCategoryButton == null) missing = nameof(consumableCategoryButton);
        else if (equipmentCategoryButton == null) missing = nameof(equipmentCategoryButton);
        else if (materialCategoryButton == null) missing = nameof(materialCategoryButton);
        else if (itemCards == null || itemCards.Length == 0) missing = nameof(itemCards);
        else if (toastRoot == null) missing = nameof(toastRoot);
        else if (toastText == null) missing = nameof(toastText);

        if (missing == null)
        {
            for (int i = 0; i < itemCards.Length; i++)
            {
                if (itemCards[i] == null || !itemCards[i].ValidateReferences(false))
                {
                    missing = $"{nameof(itemCards)}[{i}]";
                    break;
                }
            }
        }

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"MerchantShopPanel 的 Prefab 引用未配置：{missing}。", this);
        }

        return false;
    }

    private void BindButtons()
    {
        if (dialogueCloseButton != null) dialogueCloseButton.onClick.AddListener(CloseDialogue);
        if (shopCloseButton != null) shopCloseButton.onClick.AddListener(CloseShop);
        if (allCategoryButton != null) allCategoryButton.onClick.AddListener(() => SelectCategory(ShopCategory.All));
        if (consumableCategoryButton != null) consumableCategoryButton.onClick.AddListener(() => SelectCategory(ShopCategory.Consumable));
        if (equipmentCategoryButton != null) equipmentCategoryButton.onClick.AddListener(() => SelectCategory(ShopCategory.Equipment));
        if (materialCategoryButton != null) materialCategoryButton.onClick.AddListener(() => SelectCategory(ShopCategory.Material));
    }

    private void UnbindButtons()
    {
        if (dialogueCloseButton != null) dialogueCloseButton.onClick.RemoveListener(CloseDialogue);
        if (shopCloseButton != null) shopCloseButton.onClick.RemoveListener(CloseShop);
        if (allCategoryButton != null) allCategoryButton.onClick.RemoveAllListeners();
        if (consumableCategoryButton != null) consumableCategoryButton.onClick.RemoveAllListeners();
        if (equipmentCategoryButton != null) equipmentCategoryButton.onClick.RemoveAllListeners();
        if (materialCategoryButton != null) materialCategoryButton.onClick.RemoveAllListeners();
    }

    private void HandleMerchantProximityChanged(MerchantProximityChangedEvent evt)
    {
        isMerchantNearby = evt.IsNearby;
        RefreshPromptVisibility();
    }

    private void HandleDialogueRequested(MerchantDialogueRequestedEvent evt)
    {
        if (sessionUi != null && sessionUi.HasBlockingOverlayExcludingMerchant)
        {
            return;
        }

        CaptureGameplayStateIfNeeded();
        isDialogueOpen = true;
        promptRoot.SetActive(false);
        dialogueSpeakerText.text = "Fungi";
        dialogueBodyText.text = "去右边战斗赚些金币，再来找我买东西吧。";
        BringMerchantFeatureToFront(dialogueRoot);
        dialogueRoot.SetActive(true);
        dialogueRoot.transform.SetAsLastSibling();
        sessionUi?.NotifyModalStateChanged();
    }

    private void HandleShopOpenRequested(ShopOpenRequestedEvent evt)
    {
        if ((inventoryPanel != null && inventoryPanel.IsOpen) ||
            (sessionUi != null && sessionUi.HasBlockingOverlayExcludingMerchant))
        {
            return;
        }

        CaptureGameplayStateIfNeeded();
        isShopOpen = true;
        promptRoot.SetActive(false);
        BringMerchantFeatureToFront(shopRoot);
        shopRoot.SetActive(true);
        shopRoot.transform.SetAsLastSibling();
        // 打开面板时主动读取一次钱包，避免角色数据恢复时机变化导致商店显示旧余额。
        RefreshGold(this.SendQuery(new GetGoldQuery()));
        SelectCategory(ShopCategory.All);
        sessionUi?.NotifyModalStateChanged();
    }

    private void CloseDialogue()
    {
        if (!isDialogueOpen)
        {
            return;
        }

        isDialogueOpen = false;
        dialogueRoot.SetActive(false);
        RestoreGameplayStateIfNoModal();
        RefreshPromptVisibility();
        sessionUi?.NotifyModalStateChanged();
    }

    private void CloseShop()
    {
        if (!isShopOpen)
        {
            return;
        }

        isShopOpen = false;
        ResetGoldSpendFeedback();
        shopRoot.SetActive(false);
        RestoreGameplayStateIfNoModal();
        RefreshPromptVisibility();
        sessionUi?.NotifyModalStateChanged();
    }

    private void SelectCategory(ShopCategory category)
    {
        selectedCategory = category;
        RefreshCatalog();
        ResetProductScrollPosition();
    }

    private void RefreshCatalog()
    {
        ShopCatalog catalog = this.GetSystem<ShopSystem>().Catalog;
        ShopCatalogEntry[] entries = catalog != null ? catalog.Entries : null;
        int cardIndex = 0;
        for (int i = 0; entries != null && i < entries.Length && cardIndex < itemCards.Length; i++)
        {
            ShopCatalogEntry entry = entries[i];
            if (entry == null || (selectedCategory != ShopCategory.All && entry.Category != selectedCategory))
            {
                continue;
            }

            bool soldOut = entry.LimitedOncePerCharacter &&
                           this.SendQuery(new IsLimitedShopItemPurchasedQuery(entry.Item != null ? entry.Item.ItemId : string.Empty));
            itemCards[cardIndex++].Bind(entry, soldOut);
        }

        while (cardIndex < itemCards.Length)
        {
            itemCards[cardIndex++].Bind(null, false);
        }
    }

    private void HandleProductClicked(ShopCatalogEntry entry)
    {
        ShopPurchaseResult result = this.SendCommand(new PurchaseShopItemCommand(entry));
        if (!result.Success)
        {
            ShowToast(GetFailureMessage(result.Failure));
        }
    }

    private void HandleGoldChanged(GoldChangedEvent evt) => RefreshGold(evt.CurrentGold);
    private void HandleInventoryChanged(InventoryChangedEvent evt) { if (isShopOpen) RefreshCatalog(); }
    private void HandleShopProgressRestored(ShopProgressRestoredEvent evt) { RefreshPromptVisibility(); if (isShopOpen) RefreshCatalog(); }

    private void HandleShopPurchaseCompleted(ShopPurchaseCompletedEvent evt)
    {
        RefreshCatalog();
        string itemName = evt.Result.Entry?.Item != null ? evt.Result.Entry.Item.DisplayName : "商品";
        long spentGold = evt.Result.Entry != null ? evt.Result.Entry.Price : 0L;
        ShowToast($"购买成功：{itemName}｜金币 -{spentGold:N0}");
        PlayGoldSpendFeedback();
    }

    private void RefreshGold(long gold)
    {
        if (shopGoldText != null)
        {
            shopGoldText.text = $"当前金币：{gold:N0}";
        }
    }

    /// <summary>
    /// 分类切换和重新打开商店时回到商品列表顶部，避免玩家看到上一个分类遗留的滚动位置。
    /// 布局只在交互发生时重建，不会产生每帧刷新开销。
    /// </summary>
    private void ResetProductScrollPosition()
    {
        if (productScrollRect == null || productScrollRect.content == null)
        {
            return;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(productScrollRect.content);
        productScrollRect.StopMovement();
        productScrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// 扣款成功后短暂改变余额颜色。商店会暂停 Time.timeScale，因此恢复计时必须使用真实时间。
    /// </summary>
    private void PlayGoldSpendFeedback()
    {
        if (shopGoldText == null)
        {
            return;
        }

        CaptureDefaultShopGoldColor();
        if (goldSpendFeedbackRoutine != null)
        {
            StopCoroutine(goldSpendFeedbackRoutine);
        }

        shopGoldText.color = GoldSpendFeedbackColor;
        goldSpendFeedbackRoutine = StartCoroutine(RestoreGoldColorRoutine());
    }

    private IEnumerator RestoreGoldColorRoutine()
    {
        yield return new WaitForSecondsRealtime(GoldSpendFeedbackDuration);
        if (shopGoldText != null && hasCapturedDefaultShopGoldColor)
        {
            shopGoldText.color = defaultShopGoldColor;
        }

        goldSpendFeedbackRoutine = null;
    }

    private void CaptureDefaultShopGoldColor()
    {
        if (!hasCapturedDefaultShopGoldColor && shopGoldText != null)
        {
            defaultShopGoldColor = shopGoldText.color;
            hasCapturedDefaultShopGoldColor = true;
        }
    }

    private void ResetGoldSpendFeedback()
    {
        if (goldSpendFeedbackRoutine != null)
        {
            StopCoroutine(goldSpendFeedbackRoutine);
            goldSpendFeedbackRoutine = null;
        }

        if (shopGoldText != null && hasCapturedDefaultShopGoldColor)
        {
            shopGoldText.color = defaultShopGoldColor;
        }
    }

    private void CaptureGameplayStateIfNeeded()
    {
        if (!hasCapturedGameplayState)
        {
            hasCapturedGameplayState = true;
            cachedTimeScale = Time.timeScale;
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreGameplayStateIfNoModal()
    {
        if (!hasCapturedGameplayState || IsModalOpen)
        {
            return;
        }

        Time.timeScale = cachedTimeScale;
        // 商人交互只允许从正常玩法状态进入。关闭最后一个商人模态后应确定性地恢复镜头控制，
        // 不能恢复一个可能因编辑器焦点或其它 UI 遗留而处于解锁状态的缓存值。
        if (sessionUi != null)
        {
            sessionUi.RequestGameplayCursorRestore();
        }
        else
        {
            UiCursorStateUtility.EnsureHiddenAndLocked();
        }
        hasCapturedGameplayState = false;
    }

    private void CloseImmediateAndRestoreState()
    {
        isDialogueOpen = false;
        isShopOpen = false;
        if (dialogueRoot != null) dialogueRoot.SetActive(false);
        if (shopRoot != null) shopRoot.SetActive(false);
        RestoreGameplayStateIfNoModal();
    }

    private void ShowToast(string message)
    {
        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(ShowToastRoutine(message));
    }

    /// <summary>
    /// MerchantShopFeature 与 QuestFeature 是 Canvas 下的兄弟节点。
    /// 只提升内部 ShopPanel 无法越过整个 QuestFeature，因此打开商店时要提升功能根节点。
    /// </summary>
    private static void BringMerchantFeatureToFront(GameObject visibleRoot)
    {
        Transform featureRoot = visibleRoot != null ? visibleRoot.transform.parent : null;
        featureRoot?.SetAsLastSibling();
    }

    private IEnumerator ShowToastRoutine(string message)
    {
        toastText.text = message;
        toastRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(toastDuration);
        toastRoot.SetActive(false);
        toastRoutine = null;
    }

    private static string GetFailureMessage(ShopPurchaseFailure failure)
    {
        switch (failure)
        {
            case ShopPurchaseFailure.InsufficientGold: return "金币不足";
            case ShopPurchaseFailure.InventoryFull: return "背包已满";
            case ShopPurchaseFailure.SoldOut: return "该装备已经售罄";
            case ShopPurchaseFailure.InvalidEntry: return "商品配置无效";
            default: return "购买失败，请重试";
        }
    }
}
