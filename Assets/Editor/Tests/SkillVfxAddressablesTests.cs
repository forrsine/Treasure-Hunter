#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// 技能特效 Addressables 配置测试：防止资源被误放回 Resources、地址被改错或本地分组配置丢失。
/// </summary>
public sealed class SkillVfxAddressablesTests
{
    private const string DestinationFolder = "Assets/AddressableAssets/SkillVFX";
    private const string SourceFolder = "Assets/Resources/SkillVFX";

    private static readonly Dictionary<string, string> ExpectedEntries = new Dictionary<string, string>
    {
        { "FireballProjectileVfx.prefab", SkillVfxAddresses.FireballProjectile },
        { "FireballExplosionVfx.prefab", SkillVfxAddresses.FireballExplosion },
        { "PoisonAreaVfx.prefab", SkillVfxAddresses.PoisonArea },
        { "ScytheSpinVfx.prefab", SkillVfxAddresses.ScytheSpin },
        { "SpikyFireAdditiveRed.prefab", SkillVfxAddresses.SpikyFireRed }
    };

    [Test]
    public void SkillVfxGroup_UsesLocalLz4PackTogetherSettings()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        Assert.IsNotNull(settings, "项目应存在 Addressables Settings。");

        AddressableAssetGroup group = settings.FindGroup(SkillVfxAddresses.GroupName);
        Assert.IsNotNull(group, $"应存在分组 {SkillVfxAddresses.GroupName}。");

        BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
        Assert.IsNotNull(bundledSchema, "技能特效分组应包含 BundledAssetGroupSchema。");
        Assert.AreEqual(BundledAssetGroupSchema.BundlePackingMode.PackTogether, bundledSchema.BundleMode);
        Assert.AreEqual(BundledAssetGroupSchema.BundleCompressionMode.LZ4, bundledSchema.Compression);
        Assert.IsTrue(bundledSchema.IncludeInBuild);
        Assert.AreEqual(AddressableAssetSettings.kLocalBuildPath, bundledSchema.BuildPath.GetName(settings));
        Assert.AreEqual(AddressableAssetSettings.kLocalLoadPath, bundledSchema.LoadPath.GetName(settings));

        ContentUpdateGroupSchema contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
        Assert.IsNotNull(contentUpdateSchema, "技能特效分组应包含 ContentUpdateGroupSchema。");
        Assert.IsTrue(contentUpdateSchema.StaticContent, "第一阶段的本地技能特效应标记为静态内容。");
    }

    [Test]
    public void SkillVfxPrefabs_AreAddressableAndOutsideResources()
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        Assert.IsNotNull(settings, "项目应存在 Addressables Settings。");

        AddressableAssetGroup group = settings.FindGroup(SkillVfxAddresses.GroupName);
        Assert.IsNotNull(group, $"应存在分组 {SkillVfxAddresses.GroupName}。");
        Assert.AreEqual(ExpectedEntries.Count, group.entries.Count, "技能特效分组不应混入其他资源。");

        foreach (KeyValuePair<string, string> expectedEntry in ExpectedEntries)
        {
            string sourcePath = SourceFolder + "/" + expectedEntry.Key;
            string destinationPath = DestinationFolder + "/" + expectedEntry.Key;
            Assert.IsNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath),
                $"{expectedEntry.Key} 已迁移，不应继续保留在 Resources 中。");
            Assert.IsNotNull(
                AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath),
                $"目标目录缺少 {expectedEntry.Key}。");

            string guid = AssetDatabase.AssetPathToGUID(destinationPath);
            AddressableAssetEntry entry = settings.FindAssetEntry(guid);
            Assert.IsNotNull(entry, $"{expectedEntry.Key} 尚未配置为 Addressable。");
            Assert.AreSame(group, entry.parentGroup);
            Assert.AreEqual(expectedEntry.Value, entry.address);
            CollectionAssert.Contains(entry.labels, SkillVfxAddresses.Label);
        }
    }
}
#endif
