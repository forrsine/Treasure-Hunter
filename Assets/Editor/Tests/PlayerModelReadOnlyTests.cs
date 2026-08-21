#if UNITY_EDITOR
using System.Linq;
using System.IO;
using NUnit.Framework;
using QFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 玩家领域层 EditMode 测试：不启动场景、不依赖 Animator，直接验证 Command -> System -> Model。
/// 这些测试重点保护只读边界和关键数值公式，防止后续重构把 UI 或组件重新变成数据写入口。
/// </summary>
public sealed class PlayerModelReadOnlyTests
{
    private GameObject configObject;
    private GameConfig config;
    private IArchitecture architecture;

    [SetUp]
    public void SetUp()
    {
        configObject = new GameObject("TestGameConfig");
        config = configObject.AddComponent<GameConfig>();
        // EditMode 创建普通 MonoBehaviour 时不会像进入 PlayMode 那样保证执行 Awake，
        // 因此测试必须显式建立配置单例和经验表，避免误用生产代码的无配置兜底值。
        config.Lv_NextExp = new[] { 50, 60, 75 };
        config.Lv_Hpmax = new[] { 150, 180, 200 };
        GameConfig.instance = config;
        architecture = TreasureHunterArchitecture.Interface;
    }

    [TearDown]
    public void TearDown()
    {
        architecture?.Deinit();
        architecture = null;
        GameConfig.instance = null;
        Object.DestroyImmediate(configObject);
    }

    [Test]
    public void ReadOnlyContractAndSnapshot_DoNotExposePublicSetters()
    {
        Assert.That(
            typeof(IPlayerStatsReadOnly).GetProperties().All(property => property.SetMethod == null),
            Is.True,
            "只读契约不应暴露 setter。");
        Assert.That(
            typeof(PlayerStatsSnapshot).GetProperties().All(property => property.SetMethod == null),
            Is.True,
            "Query 快照不应暴露 setter。");
    }

    [Test]
    public void QuerySnapshot_DoesNotChangeAfterLaterCommands()
    {
        InitializePlayer();
        PlayerStatsSnapshot before = QueryStats();

        architecture.SendCommand(new TakePlayerDamageCommand(30, false));
        PlayerStatsSnapshot after = QueryStats();

        Assert.That(before.CurrentHp, Is.EqualTo(before.MaxHp));
        Assert.That(after.CurrentHp, Is.EqualTo(before.MaxHp - 30));
        Assert.That(before.CurrentHp, Is.Not.EqualTo(after.CurrentHp), "旧快照必须保持原值，而不是引用 Model 内部对象。");
    }

    [Test]
    public void TakeDamage_AppliesProfessionReductionAndClampsHealth()
    {
        InitializePlayer(25f);

        PlayerDamageResult result = architecture.SendCommand(new TakePlayerDamageCommand(40, false));
        PlayerStatsSnapshot stats = QueryStats();

        Assert.That(result.ActualDamage, Is.EqualTo(30));
        Assert.That(stats.CurrentHp, Is.EqualTo(stats.MaxHp - 30));
    }

    [Test]
    public void Heal_DoesNotExceedMaxHealth()
    {
        InitializePlayer();
        architecture.SendCommand(new TakePlayerDamageCommand(50, false));

        int actualHeal = architecture.SendCommand(new HealPlayerCommand(999, false));
        PlayerStatsSnapshot stats = QueryStats();

        Assert.That(actualHeal, Is.EqualTo(50));
        Assert.That(stats.CurrentHp, Is.EqualTo(stats.MaxHp));
    }

    [Test]
    public void LethalDamage_ClampsHealthToZero()
    {
        InitializePlayer();

        PlayerDamageResult result = architecture.SendCommand(new TakePlayerDamageCommand(9999, false));

        Assert.That(result.Died, Is.True);
        Assert.That(QueryStats().CurrentHp, Is.Zero);
    }

    [Test]
    public void GuaranteedCriticalHit_UsesConfiguredMultiplier()
    {
        config.playerBaseCritChance = 1f;
        config.playerCritDamageMultiplier = 2f;
        InitializePlayer();

        PlayerAttackRoll roll = architecture.SendCommand(new RollPlayerAttackCommand());

        Assert.That(roll.IsCritical, Is.True);
        Assert.That(roll.Damage, Is.EqualTo(80));
    }

    [Test]
    public void ExactRequiredExperience_LevelsUpAndQueuesOneChoice()
    {
        InitializePlayer();
        int requiredExperience = QueryStats().ExpToNextLevel;

        architecture.SendCommand(new AddPlayerExpCommand(requiredExperience));
        PlayerStatsSnapshot stats = QueryStats();

        Assert.That(stats.Level, Is.EqualTo(2));
        Assert.That(stats.CurrentExp, Is.Zero);
        Assert.That(stats.PendingUpgradeSelectionCount, Is.EqualTo(1));
    }

    [Test]
    public void AttackUpgrade_IsAppliedOnlyThroughCommand()
    {
        config.playerAttackUpgradePercent = 0.3f;
        InitializePlayer();

        bool applied = architecture.SendCommand(new ApplyPlayerUpgradeCommand(PlayerAttributeType.AttackPower));

        Assert.That(applied, Is.True);
        Assert.That(QueryStats().AttackPower, Is.EqualTo(52));
    }

    private void InitializePlayer(float professionDefense = 0f)
    {
        NCharacter save = new NCharacter
        {
            id = 1,
            slotIndex = 0,
            name = "TestPlayer",
            classId = 1,
            level = 1,
            exp = 0
        };
        CharacterDefine define = new CharacterDefine
        {
            classId = 1,
            initLevel = 1,
            hp = 200f,
            attack = 40f,
            defense = professionDefense,
            moveSpeed = 3f
        };
        architecture.SendCommand(new InitializePlayerCommand(save, define));
    }

    private PlayerStatsSnapshot QueryStats()
    {
        return architecture.SendQuery(new GetPlayerStatsQuery());
    }
}

/// <summary>
/// Prefab/场景结构测试：保护“UI 是场景资产、玩家只承载玩法组件”这一装配边界。
/// </summary>
public sealed class PlayerUiStructureTests
{
    [Test]
    public void PlayerRuntimePrefab_DoesNotOwnGameplayUi()
    {
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Characters/PlayerRuntime.prefab");

        Assert.That(player, Is.Not.Null);
        Assert.That(player.GetComponentInChildren<GameplayUiRoot>(true), Is.Null);
        Assert.That(player.GetComponentInChildren<GameSessionUi>(true), Is.Null);
        Assert.That(player.GetComponentInChildren<PlayerAttributePanel>(true), Is.Null);
        Assert.That(player.GetComponentInChildren<PlayerLevelUpPanel>(true), Is.Null);
    }

    [Test]
    public void GameplayUiPrefab_ExplicitlyOwnsAllViews()
    {
        GameObject ui = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/GameplayUiRoot.prefab");

        Assert.That(ui, Is.Not.Null);
        Assert.That(ui.GetComponent<Canvas>(), Is.Not.Null);
        Assert.That(ui.GetComponent<GameplayUiRoot>(), Is.Not.Null);
        Assert.That(ui.GetComponent<GameSessionUi>(), Is.Not.Null);
        Assert.That(ui.GetComponent<PlayerAttributePanel>(), Is.Not.Null);
        Assert.That(ui.GetComponent<PlayerLevelUpPanel>(), Is.Not.Null);
        Assert.That(ui.GetComponent<GameplayUiRoot>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponent<GameSessionUi>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponent<PlayerAttributePanel>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponent<PlayerLevelUpPanel>().ValidatePrefabReferences(false), Is.True);
        Assert.That(ui.GetComponentsInChildren<PlayerAttributeRowView>(true).Length, Is.EqualTo(12));
    }

    [Test]
    public void MainScene_ContainsExactlyOneGameplayUiRoot()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
        GameplayUiRoot[] roots = Object.FindObjectsOfType<GameplayUiRoot>(true);

        Assert.That(roots.Count(root => root.gameObject.scene == scene), Is.EqualTo(1));
        Assert.That(Object.FindObjectsOfType<EventSystem>(true).Count(item => item.gameObject.scene == scene), Is.EqualTo(1));
        GameplayStartupGuidePopup guide = Object.FindObjectOfType<GameplayStartupGuidePopup>(true);
        Assert.That(guide, Is.Not.Null);
        Assert.That(guide.ValidatePrefabReferences(false), Is.True);
    }

    [Test]
    public void RuntimeGameplayUiScripts_DoNotContainDynamicConstructionFallbacks()
    {
        string[] paths =
        {
            "Assets/Script/UI/GameSessionUi.cs",
            "Assets/Script/UI/PlayerAttributePanel.cs",
            "Assets/Script/UI/PlayerLevelUpPanel.cs",
            "Assets/Script/UI/GameplayUiRoot.cs",
            "Assets/Script/UI/GameplayStartupGuidePopup.cs"
        };

        string[] forbiddenTokens =
        {
            "new GameObject",
            "RuntimeUiCanvasProvider",
            "FindObjectOfType",
            "GameObject.Find",
            "Destroy("
        };

        foreach (string path in paths)
        {
            string source = File.ReadAllText(path);
            foreach (string token in forbiddenTokens)
            {
                Assert.That(source.Contains(token), Is.False, $"{path} 不应包含运行时 UI 兜底：{token}");
            }
        }
    }
}
#endif
