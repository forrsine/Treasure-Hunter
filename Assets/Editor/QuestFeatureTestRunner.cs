#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// 任务及共享玩法模态 UI 的编辑器测试入口。
/// 任务和商店共用玩法 UI 根，因此每次回归都同时验证商店资源、经济规则、持久化和光标状态。
/// </summary>
public static class QuestFeatureTestRunner
{
    private const string RequestPath = "Temp/QuestFeatureTests.request";
    private const string ResultPath = "Temp/QuestFeatureTests.result.txt";
    private const string SharedModalSmokeRequestPath = "Temp/SharedModalSmokeTests.request";
    private const string SharedModalSmokeResultPath = "Temp/SharedModalSmokeTests.result.txt";
    private static TestRunnerApi runner;
    private static QuestTestCallbacks callbacks;

    [MenuItem("Tools/Treasure Hunter/Run Quest Feature Tests %#q")]
    public static void RunFromMenu()
    {
        RunTests();
    }

    [MenuItem("Tools/Treasure Hunter/Validate Shared Modal Assets (Keeps Dirty Scene)")]
    public static void RunSharedModalSmokeTestsFromMenu()
    {
        RunSharedModalSmokeTests();
    }

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedRun()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(RequestPath))
            {
                return;
            }

            File.Delete(RequestPath);
            RunTests();
        };
    }

    /// <summary>
    /// Test Runner 在场景未保存时会弹出保存确认。这个轻量入口直接执行纯资源断言，
    /// 不切换场景，也不会替用户保存或丢弃正在编辑的 MainScene。
    /// </summary>
    [InitializeOnLoadMethod]
    private static void ScheduleRequestedSharedModalSmokeTests()
    {
        if (!File.Exists(SharedModalSmokeRequestPath))
        {
            return;
        }

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(SharedModalSmokeRequestPath))
            {
                return;
            }

            File.Delete(SharedModalSmokeRequestPath);
            RunSharedModalSmokeTests();
        };
    }

    private static void RunSharedModalSmokeTests()
    {
        var report = new StringBuilder();
        try
        {
            var questTests = new QuestFeatureAssetTests();
            questTests.Catalog_ContainsPlannedOneTimeQuests();
            questTests.SlimePrefab_HasStableKindAndDeathReporter(
                "Assets/Prefabs/Slime1.prefab", MonsterKind.RedSlime);
            questTests.SlimePrefab_HasStableKindAndDeathReporter(
                "Assets/Prefabs/Slime2.prefab", MonsterKind.GreenSlime);
            questTests.QuestPrefabs_HaveCompleteSerializedReferences();
            questTests.QuestUiPrefab_KeepsRuntimeModalHiddenAndLayoutTargetsEditable();
            questTests.QuestUiLayout_KeepsCardsAndControlsInsideTheirSafeAreas();

            var shopTests = new ShopFeatureAssetTests();
            shopTests.GameplayUiPrefab_HasCompleteShopGoldHudAndTaobaoSprites();
            shopTests.GameplayUiPrefab_ModalRootsStartHiddenAndShareFeatureParents();
            shopTests.GameplayUiPrefab_ShopGoldAndProductScrollAreConfiguredForMouseWheel();
            shopTests.MerchantInteractionPrompt_MatchesQuestInteractionPrompt();
            shopTests.MerchantDialogue_MatchesQuestWindowAndProductCardRegionsDoNotOverlap();
            shopTests.MerchantPrefab_WrapsOriginalFungiAndUsesThreeMeterTrigger();

            report.AppendLine("Result: Passed");
            report.AppendLine("Passed: 12");
            report.AppendLine("Failed: 0");
            Debug.Log($"SHARED_MODAL_SMOKE_TESTS_SUCCEEDED\n{report}");
        }
        catch (Exception exception)
        {
            report.AppendLine("Result: Failed");
            report.AppendLine("Passed: 0");
            report.AppendLine("Failed: 1");
            report.AppendLine(exception.ToString());
            Debug.LogError($"SHARED_MODAL_SMOKE_TESTS_FAILED\n{report}");
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(SharedModalSmokeResultPath, report.ToString());
    }

    private static void RunTests()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("任务 EditMode 测试需要先退出 Play Mode。");
            return;
        }

        callbacks = new QuestTestCallbacks();
        runner = ScriptableObject.CreateInstance<TestRunnerApi>();
        runner.RegisterCallbacks(callbacks);
        var filter = new Filter
        {
            testMode = TestMode.EditMode,
            testNames = new[]
            {
                "QuestSystemTests",
                "QuestFeatureAssetTests",
                "ShopFeatureAssetTests",
                "EconomyShopSystemTests",
                "UiCursorStateTests",
                "CharacterProgressPersistenceTests",
                "GuestModePersistenceTests"
            }
        };
        runner.Execute(new ExecutionSettings(filter) { runSynchronously = true });
    }

    private sealed class QuestTestCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var report = new StringBuilder();
            report.AppendLine($"Result: {result.ResultState}");
            report.AppendLine($"Passed: {result.PassCount}");
            report.AppendLine($"Failed: {result.FailCount}");
            report.AppendLine($"Skipped: {result.SkipCount}");
            AppendFailures(result, report);
            File.WriteAllText(ResultPath, report.ToString());

            if (result.FailCount == 0)
            {
                Debug.Log($"QUEST_FEATURE_TESTS_SUCCEEDED\n{report}");
            }
            else
            {
                Debug.LogError($"QUEST_FEATURE_TESTS_FAILED\n{report}");
            }
        }

        private static void AppendFailures(ITestResultAdaptor result, StringBuilder report)
        {
            if (!result.HasChildren)
            {
                if (result.FailCount > 0)
                {
                    report.AppendLine($"FAIL: {result.FullName}");
                    report.AppendLine(result.Message);
                    report.AppendLine(result.StackTrace);
                }
                return;
            }

            foreach (ITestResultAdaptor child in result.Children)
            {
                AppendFailures(child, report);
            }
        }
    }
}
#endif
