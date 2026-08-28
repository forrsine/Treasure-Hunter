using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 宝箱金币安全掉落点测试：验证不同攻击方向下，金币触发范围都不会埋进宝箱碰撞体。
/// </summary>
public sealed class WorldPickupSpawnUtilityTests
{
    private const string SmokeRequestPath = "Temp/VaultGoldSpawnTests.request";
    private const string SmokeResultPath = "Temp/VaultGoldSpawnTests.result.txt";
    private const float PickupRadius = 0.7f;
    private const float SurfacePadding = 0.2f;
    private const float HeightAboveBottom = 0.2f;

    [MenuItem("Tools/Treasure Hunter/Validate Vault Gold Spawn")]
    public static void RunSmokeTestsFromMenu()
    {
        RunSmokeTests();
    }

    [InitializeOnLoadMethod]
    private static void ScheduleRequestedSmokeTests()
    {
        if (!File.Exists(SmokeRequestPath))
        {
            return;
        }

        File.Delete(SmokeRequestPath);
        EditorApplication.delayCall += RunSmokeTests;
    }

    /// <summary>
    /// 直接执行纯几何测试，不切换场景，避免覆盖用户正在编辑但尚未保存的 MainScene。
    /// </summary>
    private static void RunSmokeTests()
    {
        try
        {
            WorldPickupSpawnUtilityTests tests = new WorldPickupSpawnUtilityTests();
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(-5f, 0f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(5f, 0f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(0f, -5f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(0f, 5f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(5f, 5f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(1f, 5f);
            tests.CalculateOutsidePosition_PlacesWholePickupOutsideVault(5f, 1f);
            tests.CalculateOutsidePosition_UsesSafeFallbackWithoutCollider();
            tests.VaultAndGoldPrefabs_UseAutomaticSafeSpawnConfiguration();

            const string result = "Result: Passed\nPassed: 9\nFailed: 0";
            File.WriteAllText(SmokeResultPath, result);
            Debug.Log($"VAULT_GOLD_SPAWN_TESTS_SUCCEEDED\n{result}");
        }
        catch (Exception exception)
        {
            string result = $"Result: Failed\nPassed: 0\nFailed: 1\n{exception}";
            File.WriteAllText(SmokeResultPath, result);
            Debug.LogError($"VAULT_GOLD_SPAWN_TESTS_FAILED\n{result}");
        }
    }

    [TestCase(-5f, 0f)]
    [TestCase(5f, 0f)]
    [TestCase(0f, -5f)]
    [TestCase(0f, 5f)]
    [TestCase(5f, 5f)]
    [TestCase(1f, 5f)]
    [TestCase(5f, 1f)]
    public void CalculateOutsidePosition_PlacesWholePickupOutsideVault(float playerX, float playerZ)
    {
        GameObject vaultObject = new GameObject("VaultSpawnPositionTest");
        try
        {
            vaultObject.transform.position = new Vector3(34.5f, 0.2f, -9.8f);
            BoxCollider vaultCollider = vaultObject.AddComponent<BoxCollider>();
            // 使用 MainScene 当前覆盖后的碰撞尺寸，直接保护这次实际出现问题的资产数据。
            vaultCollider.size = new Vector3(1.72f, 3.4177427f, 2.67f);
            vaultCollider.center = new Vector3(0f, 1.7138715f, 0f);
            Physics.SyncTransforms();

            Vector3 playerPosition = vaultObject.transform.position + new Vector3(playerX, 0f, playerZ);
            Vector3 directionToPlayer = playerPosition - vaultObject.transform.position;
            float expectedClearance = PickupRadius + SurfacePadding;
            Vector3 spawnPosition = WorldPickupSpawnUtility.CalculateOutsidePosition(
                vaultCollider,
                vaultObject.transform.position,
                directionToPlayer,
                expectedClearance,
                HeightAboveBottom);

            Vector3 closestPoint = vaultCollider.ClosestPoint(spawnPosition);
            float distanceFromVault = Vector3.Distance(spawnPosition, closestPoint);
            Vector3 horizontalSpawnDirection = Vector3.ProjectOnPlane(
                spawnPosition - vaultCollider.bounds.center,
                Vector3.up);

            Assert.That(distanceFromVault, Is.GreaterThanOrEqualTo(expectedClearance - 0.001f),
                "金币根节点到宝箱表面的距离不足，金币 Trigger 仍可能与宝箱重合。");
            Assert.That(Vector3.Dot(horizontalSpawnDirection, directionToPlayer), Is.GreaterThan(0f),
                "金币应生成在朝向玩家的一侧，保证玩家能够接近。");
            Assert.That(spawnPosition.y, Is.EqualTo(vaultCollider.bounds.min.y + HeightAboveBottom).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(vaultObject);
        }
    }

    [Test]
    public void CalculateOutsidePosition_UsesSafeFallbackWithoutCollider()
    {
        Vector3 origin = new Vector3(2f, 1f, 3f);
        Vector3 result = WorldPickupSpawnUtility.CalculateOutsidePosition(
            null,
            origin,
            Vector3.right,
            PickupRadius + SurfacePadding,
            HeightAboveBottom);

        Assert.That(result, Is.EqualTo(origin + Vector3.right * 0.9f + Vector3.up * 0.2f));
    }

    [Test]
    public void VaultAndGoldPrefabs_UseAutomaticSafeSpawnConfiguration()
    {
        GameObject vaultPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Box.prefab");
        GameObject goldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/WorldGoldPickup.prefab");
        Assert.That(vaultPrefab, Is.Not.Null);
        Assert.That(goldPrefab, Is.Not.Null);

        VaultGoldRewardController rewardController = vaultPrefab.GetComponent<VaultGoldRewardController>();
        SphereCollider pickupTrigger = goldPrefab.GetComponent<SphereCollider>();
        Assert.That(rewardController, Is.Not.Null, "Box.prefab 缺少金库金币奖励组件。");
        Assert.That(pickupTrigger, Is.Not.Null.And.Property("isTrigger").True,
            "金币 Prefab 必须使用 SphereCollider Trigger 才能自动拾取。");

        SerializedObject serializedController = new SerializedObject(rewardController);
        Assert.That(serializedController.FindProperty("rewardSpawnPoint").objectReferenceValue, Is.Null,
            "当前 Box.prefab 使用自动安全掉落点；若以后指定手动点，应同步更新此测试。");
        Assert.That(serializedController.FindProperty("surfacePadding").floatValue,
            Is.GreaterThanOrEqualTo(SurfacePadding));
        Assert.That(serializedController.FindProperty("heightAboveColliderBottom").floatValue,
            Is.GreaterThanOrEqualTo(HeightAboveBottom));
        Assert.That(pickupTrigger.radius, Is.EqualTo(PickupRadius).Within(0.001f));
    }
}
