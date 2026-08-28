#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>任务资源装配测试：防止任务 ID、史莱姆身份或 Prefab 引用在后续手工编辑时丢失。</summary>
public sealed class QuestFeatureAssetTests
{
    [Test]
    public void Catalog_ContainsPlannedOneTimeQuests()
    {
        QuestCatalog catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(
            "Assets/Resources/Data/Quest/QuestCatalog.asset");

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Entries, Has.Length.EqualTo(2));
        AssertQuest(catalog, "hunt_red_slime", MonsterKind.RedSlime, 5, 50L);
        AssertQuest(catalog, "hunt_green_slime", MonsterKind.GreenSlime, 8, 80L);
    }

    [TestCase("Assets/Prefabs/Slime1.prefab", MonsterKind.RedSlime)]
    [TestCase("Assets/Prefabs/Slime2.prefab", MonsterKind.GreenSlime)]
    public void SlimePrefab_HasStableKindAndDeathReporter(string path, MonsterKind expectedKind)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null);
        SlimeCo slime = prefab.GetComponentInChildren<SlimeCo>(true);
        Assert.That(slime, Is.Not.Null);
        Assert.That(slime.MonsterKind, Is.EqualTo(expectedKind));
        Assert.That(slime.GetComponent<MonsterQuestProgressReporter>(), Is.Not.Null);
    }

    [Test]
    public void QuestPrefabs_HaveCompleteSerializedReferences()
    {
        GameObject npc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPC/QuestMushroom.prefab");
        GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/QuestListItem.prefab");
        GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");

        Assert.That(npc, Is.Not.Null);
        Assert.That(npc.GetComponent<QuestNpcController>(), Is.Not.Null);
        Assert.That(npc.GetComponent<SphereCollider>(), Is.Not.Null.And.Property("isTrigger").True);
        Assert.That(npc.GetComponent<Rigidbody>(), Is.Not.Null.And.Property("isKinematic").True);
        Assert.That(item.GetComponent<QuestListItemView>().ValidateReferences(false), Is.True);
        Assert.That(ui.GetComponent<QuestPanel>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponent<GameplayUiRoot>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponent<GameSessionUi>().ValidatePrefabReferences(false), Is.True);
    }

    [Test]
    public void QuestUiPrefab_KeepsRuntimeModalHiddenAndLayoutTargetsEditable()
    {
        GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/QuestListItem.prefab");
        Assert.That(ui, Is.Not.Null);
        Assert.That(item, Is.Not.Null);

        Transform feature = ui.GetComponentsInChildren<Transform>(true)
            .Single(transform => transform.name == "QuestFeature");
        Transform prompt = feature.Find("QuestPrompt");
        Transform modal = feature.Find("QuestModal");
        Transform panel = modal != null ? modal.Find("Panel") : null;

        Assert.That(prompt, Is.Not.Null);
        Assert.That(prompt.gameObject.activeSelf, Is.False);
        Assert.That(modal, Is.Not.Null);
        Assert.That(modal.gameObject.activeSelf, Is.False,
            "QuestModal 必须默认关闭，避免全屏任务层在初始化前遮住商店。需要排版时由编辑器菜单临时显示。 ");
        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.Find("Content"), Is.Not.Null);
        Assert.That(item.GetComponent<QuestListItemView>().ValidateReferences(false), Is.True);
    }

    [Test]
    public void QuestUiLayout_KeepsCardsAndControlsInsideTheirSafeAreas()
    {
        GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameplayUiRoot.prefab");
        GameObject item = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/QuestListItem.prefab");
        Assert.That(ui, Is.Not.Null);
        Assert.That(item, Is.Not.Null);

        RectTransform itemRect = item.GetComponent<RectTransform>();
        RectTransform objectiveIcon = RequireRect(item.transform, "ObjectiveIcon");
        RectTransform title = RequireRect(item.transform, "Title");
        RectTransform description = RequireRect(item.transform, "Description");
        RectTransform progress = RequireRect(item.transform, "ProgressTrack");
        RectTransform rewardCoin = RequireRect(item.transform, "RewardCoin");
        RectTransform rewardText = RequireRect(item.transform, "RewardText");
        RectTransform actionButton = RequireRect(item.transform, "ActionButton");

        AssertContained(itemRect, objectiveIcon);
        AssertContained(itemRect, title);
        AssertContained(itemRect, description);
        AssertContained(itemRect, progress);
        AssertContained(itemRect, rewardCoin);
        AssertContained(itemRect, rewardText);
        AssertContained(itemRect, actionButton);

        // Guild List 素材的左侧 0-135 是徽章、右侧 700-820 是奖杯，动态内容不能压住装饰。
        float safeLeft = itemRect.rect.xMin + 135f;
        float safeRight = itemRect.rect.xMin + 700f;
        Assert.That(GetRectInParent(itemRect, objectiveIcon).xMin, Is.GreaterThanOrEqualTo(safeLeft));
        Assert.That(GetRectInParent(itemRect, actionButton).xMax, Is.LessThanOrEqualTo(safeRight));
        Assert.That(GetRectInParent(itemRect, rewardText).xMax, Is.LessThanOrEqualTo(safeRight));
        AssertNoOverlap(itemRect, title, rewardCoin);
        AssertNoOverlap(itemRect, title, rewardText);
        AssertNoOverlap(itemRect, description, actionButton);
        AssertNoOverlap(itemRect, progress, actionButton);

        Transform feature = ui.transform.Find("QuestFeature");
        RectTransform panel = RequireRect(feature, "QuestModal/Panel");
        RectTransform panelTitle = RequireRect(panel, "Title");
        RectTransform closeButton = RequireRect(panel, "CloseButton");
        RectTransform content = RequireRect(panel, "Content");
        RectTransform feedback = RequireRect(panel, "Feedback");
        AssertContained(panel, panelTitle);
        AssertContained(panel, closeButton);
        AssertContained(panel, content);
        AssertContained(panel, feedback);
        AssertNoOverlap(panel, content, feedback);

        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        LayoutElement itemLayout = item.GetComponent<LayoutElement>();
        Assert.That(layout, Is.Not.Null);
        Assert.That(itemLayout, Is.Not.Null);
        float twoCardHeight = itemLayout.preferredHeight * 2f
                              + layout.spacing
                              + layout.padding.top
                              + layout.padding.bottom;
        Assert.That(content.rect.height, Is.GreaterThanOrEqualTo(twoCardHeight),
            "任务列表高度必须能同时容纳两张任务卡，避免 VerticalLayoutGroup 把卡片挤到一起。");
    }

    [Test]
    public void MainScene_ContainsSeparatedMushroomAndFungiInteractionAreas()
    {
        const string scenePath = "Assets/Scenes/MainScene.unity";
        Scene scene = SceneManager.GetSceneByPath(scenePath);
        bool openedByTest = !scene.IsValid() || !scene.isLoaded;
        if (openedByTest)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
        }

        try
        {
            QuestNpcController questNpc = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<QuestNpcController>(true))
                .Single();
            MerchantNpcController merchantNpc = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MerchantNpcController>(true))
                .Single();

            Assert.That(questNpc.name, Is.EqualTo("Mushroom"));
            Assert.That(
                Vector3.Distance(questNpc.transform.position, merchantNpc.transform.position),
                Is.GreaterThan(6f),
                "允许手动移动 NPC，但两个 3 米交互球必须完全分离，避免同一位置同时响应 E 键。");
        }
        finally
        {
            if (openedByTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static void AssertQuest(
        QuestCatalog catalog,
        string questId,
        MonsterKind monsterKind,
        int requiredCount,
        long reward)
    {
        Assert.That(catalog.TryGetQuest(questId, out QuestDefinition definition), Is.True);
        Assert.That(definition.TargetMonster, Is.EqualTo(monsterKind));
        Assert.That(definition.RequiredCount, Is.EqualTo(requiredCount));
        Assert.That(definition.GoldReward, Is.EqualTo(reward));
    }

    private static RectTransform RequireRect(Transform root, string path)
    {
        Assert.That(root, Is.Not.Null, $"查找 {path} 时父节点为空。");
        Transform target = root.Find(path);
        Assert.That(target, Is.Not.Null, $"任务 UI 缺少节点：{path}");
        RectTransform rect = target.GetComponent<RectTransform>();
        Assert.That(rect, Is.Not.Null, $"任务 UI 节点缺少 RectTransform：{path}");
        return rect;
    }

    private static void AssertContained(RectTransform parent, RectTransform child)
    {
        Rect childRect = GetRectInParent(parent, child);
        const float tolerance = 0.1f;
        Assert.That(childRect.xMin, Is.GreaterThanOrEqualTo(parent.rect.xMin - tolerance),
            $"{child.name} 超出了 {parent.name} 的左边界。");
        Assert.That(childRect.xMax, Is.LessThanOrEqualTo(parent.rect.xMax + tolerance),
            $"{child.name} 超出了 {parent.name} 的右边界。");
        Assert.That(childRect.yMin, Is.GreaterThanOrEqualTo(parent.rect.yMin - tolerance),
            $"{child.name} 超出了 {parent.name} 的下边界。");
        Assert.That(childRect.yMax, Is.LessThanOrEqualTo(parent.rect.yMax + tolerance),
            $"{child.name} 超出了 {parent.name} 的上边界。");
    }

    private static void AssertNoOverlap(RectTransform parent, RectTransform first, RectTransform second)
    {
        Rect firstRect = GetRectInParent(parent, first);
        Rect secondRect = GetRectInParent(parent, second);
        Assert.That(firstRect.Overlaps(secondRect), Is.False,
            $"{first.name} 与 {second.name} 的布局区域发生重叠。");
    }

    private static Rect GetRectInParent(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4];
        child.GetWorldCorners(corners);
        Vector3 bottomLeft = parent.InverseTransformPoint(corners[0]);
        Vector3 topRight = parent.InverseTransformPoint(corners[2]);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }
}
#endif
