#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

/// <summary>商店装配测试：保护淘宝 UI 引用、Fungi 商人和三类奖励 Prefab 不被后续生成工具覆盖。</summary>
public sealed class ShopFeatureAssetTests
{
    [Test]
    public void GameplayUiPrefab_HasCompleteShopGoldHudAndTaobaoSprites()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Assert.That(prefab, Is.Not.Null);
        MerchantShopPanel panel = prefab.GetComponent<MerchantShopPanel>();
        GoldHudView goldHud = prefab.GetComponent<GoldHudView>();
        Assert.That(panel, Is.Not.Null);
        Assert.That(goldHud, Is.Not.Null);
        Assert.That(panel.ValidatePrefabReferences(false), Is.True);
        Assert.That(goldHud.ValidateReferences(false), Is.True);

        Transform feature = prefab.GetComponentsInChildren<Transform>(true)
            .SingleOrDefault(item => item.name == "MerchantShopFeature");
        Assert.That(feature, Is.Not.Null);
        Assert.That(feature.GetComponentsInChildren<ShopItemCardView>(true), Has.Length.EqualTo(20));

        Image shopBackground = feature.GetComponentsInChildren<Image>(true)
            .Single(image => image.name == "ShopPanel");
        string spritePath = AssetDatabase.GetAssetPath(shopBackground.sprite);
        Assert.That(spritePath, Does.StartWith("Assets/AllResources/淘宝ui素材/RuntimeSprites/Shop/"));
    }

    [Test]
    public void GameplayUiPrefab_ModalRootsStartHiddenAndShareFeatureParents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Assert.That(prefab, Is.Not.Null);

        Transform merchantFeature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "MerchantShopFeature");
        Transform questFeature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "QuestFeature");
        Transform dialogue = merchantFeature.Find("FirstDialogue");
        Transform shop = merchantFeature.Find("ShopPanel");
        Transform questModal = questFeature.Find("QuestModal");

        Assert.That(dialogue, Is.Not.Null);
        Assert.That(shop, Is.Not.Null);
        Assert.That(questModal, Is.Not.Null);
        Assert.That(dialogue.gameObject.activeSelf, Is.False);
        Assert.That(shop.gameObject.activeSelf, Is.False);
        Assert.That(questModal.gameObject.activeSelf, Is.False,
            "任务全屏层默认激活时会盖住后打开的商店。 ");
        Assert.That(dialogue.parent, Is.SameAs(merchantFeature));
        Assert.That(shop.parent, Is.SameAs(merchantFeature));
        Assert.That(questModal.parent, Is.SameAs(questFeature));
        Assert.That(merchantFeature.parent, Is.SameAs(questFeature.parent),
            "两个功能根必须处于同一 Canvas 层级，打开时才能通过 SetAsLastSibling 可靠切换前后顺序。 ");
    }

    [Test]
    public void GameplayUiPrefab_ShopGoldAndProductScrollAreConfiguredForMouseWheel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Transform feature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "MerchantShopFeature");

        Text shopGoldText = feature.Find("ShopPanel/ShopGoldText").GetComponent<Text>();
        Assert.That(shopGoldText, Is.Not.Null);
        Assert.That(shopGoldText.text, Does.Contain("金币"));
        Assert.That(shopGoldText.rectTransform.anchorMin,
            Is.EqualTo(Vector2.one).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(shopGoldText.rectTransform.anchorMax,
            Is.EqualTo(Vector2.one).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(shopGoldText.rectTransform.pivot,
            Is.EqualTo(Vector2.one).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(shopGoldText.rectTransform.anchoredPosition,
            Is.EqualTo(new Vector2(-70f, -28f)).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(shopGoldText.rectTransform.sizeDelta,
            Is.EqualTo(new Vector2(420f, 72f)).Using(Vector2ComparerWithEqualsOperator.Instance));
        AssertReadableText(shopGoldText, new Color32(255, 181, 46, 255));
        Assert.That(shopGoldText.fontStyle, Is.EqualTo(FontStyle.Bold));
        Assert.That(shopGoldText.alignment, Is.EqualTo(TextAnchor.MiddleRight));

        Transform scrollTransform = feature.Find("ShopPanel/ProductScroll");
        ScrollRect scrollRect = scrollTransform.GetComponent<ScrollRect>();
        RectTransform viewport = scrollTransform.Find("Viewport").GetComponent<RectTransform>();
        RectTransform content = scrollTransform.Find("Viewport/Content").GetComponent<RectTransform>();

        Assert.That(scrollRect, Is.Not.Null);
        Assert.That(scrollRect.horizontal, Is.False);
        Assert.That(scrollRect.vertical, Is.True);
        Assert.That(scrollRect.scrollSensitivity, Is.EqualTo(60f).Within(0.01f));
        Assert.That(scrollRect.viewport, Is.SameAs(viewport));
        Assert.That(scrollRect.content, Is.SameAs(content));
    }

    [Test]
    public void MerchantInteractionPrompt_MatchesQuestInteractionPrompt()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Transform merchantFeature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "MerchantShopFeature");
        Transform questFeature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "QuestFeature");

        Image merchantPrompt = merchantFeature.Find("InteractionPrompt").GetComponent<Image>();
        Image questPrompt = questFeature.Find("QuestPrompt").GetComponent<Image>();
        Assert.That(merchantPrompt.sprite, Is.SameAs(questPrompt.sprite));
        Assert.That(AssetDatabase.GetAssetPath(merchantPrompt.sprite),
            Does.EndWith("RuntimeSprites/Progression/UI_Progression_Guild_List.png"));
        Assert.That(merchantPrompt.type, Is.EqualTo(questPrompt.type));
        Assert.That((Color32)merchantPrompt.color, Is.EqualTo((Color32)questPrompt.color));

        AssertRectTransformMatches(merchantPrompt.rectTransform, questPrompt.rectTransform);

        Text merchantLabel = merchantPrompt.transform.Find("PromptText").GetComponent<Text>();
        Text questLabel = questPrompt.transform.Find("Label").GetComponent<Text>();
        Assert.That((Color32)merchantLabel.color, Is.EqualTo((Color32)questLabel.color));
        Assert.That(merchantLabel.fontSize, Is.EqualTo(questLabel.fontSize));
        Assert.That(merchantLabel.fontStyle, Is.EqualTo(questLabel.fontStyle));
        Assert.That(merchantLabel.alignment, Is.EqualTo(questLabel.alignment));
        AssertRectTransformMatches(merchantLabel.rectTransform, questLabel.rectTransform);
    }

    [Test]
    public void MerchantDialogue_MatchesQuestWindowAndProductCardRegionsDoNotOverlap()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        Transform feature = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "MerchantShopFeature");

        Transform popup = feature.Find("FirstDialogue/Popup");
        Assert.That(popup, Is.Not.Null);
        Image popupImage = popup.GetComponent<Image>();
        Transform questPanel = prefab.GetComponentsInChildren<Transform>(true)
            .Single(item => item.name == "QuestFeature")
            .Find("QuestModal/Panel");
        Image questPanelImage = questPanel.GetComponent<Image>();
        Assert.That(popupImage.sprite, Is.SameAs(questPanelImage.sprite));
        Assert.That(AssetDatabase.GetAssetPath(popupImage.sprite),
            Does.EndWith("RuntimeSprites/Progression/UI_Progression_Guild_Background.png"));
        Assert.That(((RectTransform)popup).sizeDelta,
            Is.EqualTo(new Vector2(1050f, 660f)).Using(Vector2ComparerWithEqualsOperator.Instance));

        Text speaker = popup.Find("SpeakerText").GetComponent<Text>();
        Text body = popup.Find("BodyText").GetComponent<Text>();
        AssertReadableText(speaker, new Color32(255, 226, 151, 255));
        AssertReadableText(body, new Color32(241, 220, 169, 255));
        Assert.That(speaker.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
        Assert.That(speaker.fontStyle, Is.EqualTo(FontStyle.Bold));

        Image continueButton = popup.Find("ContinueButton").GetComponent<Image>();
        Assert.That(AssetDatabase.GetAssetPath(continueButton.sprite),
            Does.EndWith("RuntimeSprites/Progression/UI_Progression_Missions_List_ButtonGreen_Btn_Normal.png"));

        ShopItemCardView[] cards = feature.GetComponentsInChildren<ShopItemCardView>(true);
        Assert.That(cards, Has.Length.EqualTo(20));
        foreach (ShopItemCardView card in cards)
        {
            RectTransform icon = card.transform.Find("Icon").GetComponent<RectTransform>();
            Text name = card.transform.Find("NameText").GetComponent<Text>();
            Text description = card.transform.Find("DescriptionText").GetComponent<Text>();
            Text price = card.transform.Find("PriceText").GetComponent<Text>();
            RectTransform state = card.transform.Find("StateBackground").GetComponent<RectTransform>();

            AssertReadableText(name, new Color32(255, 214, 107, 255));
            AssertReadableText(description, new Color32(238, 233, 223, 255));
            AssertReadableText(price, new Color32(255, 181, 46, 255));

            Assert.That(icon.anchoredPosition.y, Is.EqualTo(-16f).Within(0.01f));
            Assert.That(icon.sizeDelta, Is.EqualTo(new Vector2(100f, 100f)).Using(Vector2ComparerWithEqualsOperator.Instance));
            AssertTopStretchRegion(name.rectTransform, 122f, 30f);
            AssertTopStretchRegion(description.rectTransform, 158f, 50f);
            AssertTopStretchRegion(price.rectTransform, 210f, 26f);
            Assert.That(state.anchoredPosition.y, Is.EqualTo(12f).Within(0.01f));
            Assert.That(state.sizeDelta, Is.EqualTo(new Vector2(130f, 36f)).Using(Vector2ComparerWithEqualsOperator.Instance));

            float iconBottom = icon.anchoredPosition.y - icon.sizeDelta.y;
            Assert.That(iconBottom, Is.GreaterThan(name.rectTransform.offsetMax.y), "图标与名称区域发生重叠。");
            Assert.That(name.rectTransform.offsetMin.y, Is.GreaterThan(description.rectTransform.offsetMax.y), "名称与说明区域发生重叠。");
            Assert.That(description.rectTransform.offsetMin.y, Is.GreaterThan(price.rectTransform.offsetMax.y), "说明与价格区域发生重叠。");
        }
    }

    [Test]
    public void WindleafBoots_UsesDedicatedBootIcon()
    {
        InventoryItemDefinition boots = AssetDatabase.LoadAssetAtPath<InventoryItemDefinition>(
            "Assets/Resources/Data/Inventory/BossWindleafBoots.asset");

        Assert.That(boots, Is.Not.Null);
        Assert.That(boots.Icon, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(boots.Icon),
            Is.EqualTo("Assets/AllResources/淘宝ui素材/RuntimeSprites/FunctionIcons/UI_FunctionIcon_CustumeBoots.png"));
    }

    [Test]
    public void MerchantPrefab_WrapsOriginalFungiAndUsesThreeMeterTrigger()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC/MerchantFungi.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<MerchantNpcController>(), Is.Not.Null);
        SphereCollider trigger = prefab.GetComponent<SphereCollider>();
        Rigidbody body = prefab.GetComponent<Rigidbody>();
        Assert.That(trigger, Is.Not.Null);
        Assert.That(trigger.isTrigger, Is.True);
        Assert.That(trigger.radius, Is.EqualTo(3f).Within(0.001f));
        Assert.That(body, Is.Not.Null);
        Assert.That(body.isKinematic, Is.True);
        Assert.That(prefab.GetComponentInChildren<Animator>(true), Is.Not.Null);
    }

    [Test]
    public void RewardPrefabs_HaveCorrectGoldComponentsWithoutReplacingLootComponents()
    {
        GameObject slimeOne = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slime1.prefab");
        GameObject slimeTwo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Slime2.prefab");
        GameObject box = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Box.prefab");
        GameObject gold = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/WorldGoldPickup.prefab");

        Assert.That(slimeOne.GetComponentInChildren<MonsterGoldRewardController>(true), Is.Not.Null);
        Assert.That(slimeTwo.GetComponentInChildren<MonsterGoldRewardController>(true), Is.Not.Null);
        Assert.That(slimeOne.GetComponentInChildren<MonsterLootDropController>(true), Is.Not.Null);
        Assert.That(slimeTwo.GetComponentInChildren<MonsterLootDropController>(true), Is.Not.Null);
        Assert.That(box.GetComponentInChildren<VaultGoldRewardController>(true), Is.Not.Null);
        Assert.That(gold.GetComponent<WorldGoldPickup>(), Is.Not.Null);
    }

    [Test]
    public void MainScene_FungiUsesProjectMerchantPrefabAtOriginalTransform()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Additive);
        try
        {
            MerchantNpcController merchant = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MerchantNpcController>(true))
                .SingleOrDefault();
            Assert.That(merchant, Is.Not.Null);
            Assert.That(merchant.name, Is.EqualTo("Fungi"));
            Assert.That(merchant.transform.position.x, Is.EqualTo(2.7f).Within(0.001f));
            Assert.That(merchant.transform.position.z, Is.EqualTo(1.29f).Within(0.001f));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(merchant.gameObject), Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void AssertReadableText(Text text, Color expectedColor)
    {
        Assert.That(text, Is.Not.Null);
        Assert.That(text.color.r, Is.EqualTo(expectedColor.r).Within(0.005f));
        Assert.That(text.color.g, Is.EqualTo(expectedColor.g).Within(0.005f));
        Assert.That(text.color.b, Is.EqualTo(expectedColor.b).Within(0.005f));
        Outline outline = text.GetComponent<Outline>();
        Assert.That(outline, Is.Not.Null, $"{text.name} 缺少可读性描边。");
        Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(1f, -1f)).Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    private static void AssertRectTransformMatches(RectTransform actual, RectTransform expected)
    {
        Assert.That(actual.anchorMin,
            Is.EqualTo(expected.anchorMin).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.anchorMax,
            Is.EqualTo(expected.anchorMax).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.pivot,
            Is.EqualTo(expected.pivot).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.anchoredPosition,
            Is.EqualTo(expected.anchoredPosition).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.sizeDelta,
            Is.EqualTo(expected.sizeDelta).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.offsetMin,
            Is.EqualTo(expected.offsetMin).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(actual.offsetMax,
            Is.EqualTo(expected.offsetMax).Using(Vector2ComparerWithEqualsOperator.Instance));
    }

    private static void AssertTopStretchRegion(RectTransform rect, float top, float height)
    {
        Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(1f, 1f)).Using(Vector2ComparerWithEqualsOperator.Instance));
        Assert.That(rect.offsetMax.y, Is.EqualTo(-top).Within(0.01f));
        Assert.That(rect.offsetMin.y, Is.EqualTo(-top - height).Within(0.01f));
    }
}
#endif
