#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

/// <summary>
/// 技能特效 Addressables 迁移工具。
/// 工具可重复执行：已经移动和配置完成的资源会被校验，不会再次复制出重复文件。
/// </summary>
public static class SkillVfxAddressablesMigration
{
    private const string SourceFolder = "Assets/Resources/SkillVFX";
    private const string DestinationParentFolder = "Assets/AddressableAssets";
    private const string DestinationFolder = DestinationParentFolder + "/SkillVFX";

    private sealed class VfxMigrationEntry
    {
        public readonly string FileName;
        public readonly string Address;

        public VfxMigrationEntry(string fileName, string address)
        {
            FileName = fileName;
            Address = address;
        }
    }

    private static readonly VfxMigrationEntry[] Entries =
    {
        new VfxMigrationEntry("FireballProjectileVfx.prefab", SkillVfxAddresses.FireballProjectile),
        new VfxMigrationEntry("FireballExplosionVfx.prefab", SkillVfxAddresses.FireballExplosion),
        new VfxMigrationEntry("PoisonAreaVfx.prefab", SkillVfxAddresses.PoisonArea),
        new VfxMigrationEntry("ScytheSpinVfx.prefab", SkillVfxAddresses.ScytheSpin),
        new VfxMigrationEntry("SpikyFireAdditiveRed.prefab", SkillVfxAddresses.SpikyFireRed)
    };

    [MenuItem("Tools/Treasure Hunter/Addressables/Migrate Skill VFX")]
    public static void MigrateSkillVfx()
    {
        EnsureDestinationFolder();
        MovePrefabsPreservingGuids();

        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        AddressableAssetGroup group = GetOrCreateLocalSkillVfxGroup(settings);
        ConfigureLocalGroup(settings, group);
        ConfigureEntries(settings, group);

        settings.SetDirty(
            AddressableAssetSettings.ModificationEvent.BatchModification,
            null,
            true,
            true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"技能特效 Addressables 迁移完成：{Entries.Length} 个 Prefab 已配置到 {SkillVfxAddresses.GroupName}。");
    }

    [MenuItem("Tools/Treasure Hunter/Addressables/Migrate And Build Skill VFX")]
    public static void MigrateAndBuildSkillVfx()
    {
        MigrateSkillVfx();

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        if (!string.IsNullOrEmpty(result.Error))
        {
            throw new InvalidOperationException($"Addressables 内容构建失败：{result.Error}");
        }

        Debug.Log("技能特效 Addressables 内容构建完成。");
    }

    /// <summary>
    /// 提供给 Unity 批处理模式的稳定入口，便于 CI 或本地自动验证迁移和构建结果。
    /// </summary>
    public static void MigrateAndBuildFromCommandLine()
    {
        MigrateAndBuildSkillVfx();
    }

    private static void EnsureDestinationFolder()
    {
        if (!AssetDatabase.IsValidFolder(DestinationParentFolder))
        {
            AssetDatabase.CreateFolder("Assets", "AddressableAssets");
        }

        if (!AssetDatabase.IsValidFolder(DestinationFolder))
        {
            AssetDatabase.CreateFolder(DestinationParentFolder, "SkillVFX");
        }
    }

    private static void MovePrefabsPreservingGuids()
    {
        for (int i = 0; i < Entries.Length; i++)
        {
            VfxMigrationEntry entry = Entries[i];
            string sourcePath = SourceFolder + "/" + entry.FileName;
            string destinationPath = DestinationFolder + "/" + entry.FileName;
            bool sourceExists = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) != null;
            bool destinationExists = AssetDatabase.LoadAssetAtPath<GameObject>(destinationPath) != null;

            if (sourceExists && destinationExists)
            {
                throw new InvalidOperationException(
                    $"迁移中止：源目录和目标目录同时存在 {entry.FileName}，请先确认哪一份才是正确资源。");
            }

            if (!sourceExists && !destinationExists)
            {
                throw new InvalidOperationException($"迁移中止：找不到技能特效 {entry.FileName}。");
            }

            if (sourceExists)
            {
                string moveError = AssetDatabase.MoveAsset(sourcePath, destinationPath);
                if (!string.IsNullOrEmpty(moveError))
                {
                    throw new InvalidOperationException($"移动 {entry.FileName} 失败：{moveError}");
                }
            }
        }
    }

    private static AddressableAssetGroup GetOrCreateLocalSkillVfxGroup(AddressableAssetSettings settings)
    {
        AddressableAssetGroup group = settings.FindGroup(SkillVfxAddresses.GroupName);
        if (group != null)
        {
            return group;
        }

        return settings.CreateGroup(
            SkillVfxAddresses.GroupName,
            false,
            false,
            true,
            null,
            typeof(BundledAssetGroupSchema),
            typeof(ContentUpdateGroupSchema));
    }

    private static void ConfigureLocalGroup(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        BundledAssetGroupSchema bundledSchema = group.GetSchema<BundledAssetGroupSchema>();
        if (bundledSchema == null)
        {
            bundledSchema = group.AddSchema<BundledAssetGroupSchema>();
        }

        bundledSchema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
        bundledSchema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
        bundledSchema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
        bundledSchema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
        bundledSchema.IncludeInBuild = true;

        ContentUpdateGroupSchema contentUpdateSchema = group.GetSchema<ContentUpdateGroupSchema>();
        if (contentUpdateSchema == null)
        {
            contentUpdateSchema = group.AddSchema<ContentUpdateGroupSchema>();
        }

        // 第一阶段只做随包本地资源，不参与远程内容更新。
        contentUpdateSchema.StaticContent = true;
    }

    private static void ConfigureEntries(AddressableAssetSettings settings, AddressableAssetGroup group)
    {
        settings.AddLabel(SkillVfxAddresses.Label, false);

        for (int i = 0; i < Entries.Length; i++)
        {
            VfxMigrationEntry migrationEntry = Entries[i];
            string assetPath = DestinationFolder + "/" + migrationEntry.FileName;
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException($"配置失败：无法获取 {assetPath} 的 GUID。");
            }

            AddressableAssetEntry addressableEntry = settings.CreateOrMoveEntry(guid, group, false, false);
            addressableEntry.address = migrationEntry.Address;
            addressableEntry.SetLabel(SkillVfxAddresses.Label, true, false, false);
        }
    }
}
#endif
