#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 淘宝 UI 素材库回归测试：保证源 PSD、显式规则、PNG 文件和 Unity 导入结果保持一致。
/// 这些测试只检查资源管线，不依赖场景和运行时业务逻辑。
/// </summary>
public sealed class PurchasedUiAssetLibraryTests
{
    private const int ExpectedPsdCount = 49;
    private const int ExpectedFunctionIconCount = 360;
    private static readonly Regex SpriteNamePattern = new Regex(
        @"^UI_[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    [Test]
    public void SourceLibrary_ContainsExpectedPsdFilesAndKeepsIncompletePreviewUntouched()
    {
        string sourceDirectory = ToAbsolutePath(PurchasedUiSpriteImportPostprocessor.SourceRoot);
        string[] psdFiles = Directory.GetFiles(sourceDirectory, "*.psd", SearchOption.TopDirectoryOnly);

        Assert.That(psdFiles, Has.Length.EqualTo(ExpectedPsdCount));
        Assert.That(
            File.Exists(Path.Combine(sourceDirectory, "Language_预览图.jpg.baiduyun.p.downloading")),
            Is.True,
            "未完成的下载文件只应忽略，不应由导入工具删除。 ");
    }

    [Test]
    public void ImportRules_HaveUniquePathsNamesAndMatchGeneratedPngFiles()
    {
        PurchasedUiImportRules rules = LoadRules();
        string runtimeDirectory = ToAbsolutePath(PurchasedUiSpriteImportPostprocessor.RuntimeSpriteRoot);
        string[] pngFiles = Directory.GetFiles(runtimeDirectory, "*.png", SearchOption.AllDirectories);
        HashSet<string> rulePaths = new HashSet<string>(
            rules.sprites.Select(rule => Normalize(rule.path)),
            StringComparer.OrdinalIgnoreCase);

        Assert.That(rulePaths.Count, Is.EqualTo(rules.sprites.Length), "规则路径不能重复。 ");
        Assert.That(pngFiles, Has.Length.EqualTo(rules.sprites.Length));

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (PurchasedUiSpriteRule rule in rules.sprites)
        {
            string name = Path.GetFileNameWithoutExtension(rule.path);
            Assert.That(SpriteNamePattern.IsMatch(name), Is.True, "命名不规范：" + name);
            Assert.That(names.Add(name), Is.True, "Sprite 名称大小写重复：" + name);
            Assert.That(File.Exists(ToAbsolutePath(rule.path)), Is.True, "规则文件不存在：" + rule.path);
        }

        foreach (string pngFile in pngFiles)
        {
            Assert.That(rulePaths, Has.Member(ToAssetPath(pngFile)));
        }
    }

    [Test]
    public void FunctionIcons_ExportExactlyOneNamedSpritePerSourceIcon()
    {
        string iconDirectory = ToAbsolutePath(
            PurchasedUiSpriteImportPostprocessor.RuntimeSpriteRoot + "FunctionIcons/");
        string[] iconFiles = Directory.GetFiles(iconDirectory, "*.png", SearchOption.TopDirectoryOnly);

        Assert.That(iconFiles, Has.Length.EqualTo(ExpectedFunctionIconCount));
        Assert.That(
            iconFiles.All(path => Path.GetFileNameWithoutExtension(path).StartsWith(
                "UI_FunctionIcon_",
                StringComparison.Ordinal)),
            Is.True);
    }

    [Test]
    public void RuntimeSprites_HaveRequiredUnityImportSettings()
    {
        List<string> errors = PurchasedUiSpriteImportPostprocessor.CollectValidationErrors();
        Assert.That(errors, Is.Empty, string.Join(Environment.NewLine, errors));
    }

    [Test]
    public void NineSliceRules_AreValidAndUsedOnlyBySlicedComponents()
    {
        PurchasedUiImportRules rules = LoadRules();
        PurchasedUiSpriteRule[] slicedRules = rules.sprites
            .Where(rule => rule.border != null && rule.border.Any(value => value > 0))
            .ToArray();

        Assert.That(slicedRules.Length, Is.GreaterThanOrEqualTo(10));
        foreach (PurchasedUiSpriteRule rule in slicedRules)
        {
            Assert.That(rule.role, Is.EqualTo("Sliced"));
            Assert.That(rule.border, Has.Length.EqualTo(4));
            Assert.That(rule.border.All(value => value >= 0), Is.True);
        }
    }

    [Test]
    public void OriginalPsd_IsReferenceTextureRatherThanRuntimeSprite()
    {
        const string sourcePsd = "Assets/AllResources/淘宝ui素材/Buttons.psd";
        TextureImporter importer = AssetImporter.GetAtPath(sourcePsd) as TextureImporter;

        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.maxTextureSize, Is.EqualTo(512));
    }

    private static PurchasedUiImportRules LoadRules()
    {
        string json = File.ReadAllText(
            ToAbsolutePath(PurchasedUiSpriteImportPostprocessor.ImportRulesPath));
        PurchasedUiImportRules rules = JsonUtility.FromJson<PurchasedUiImportRules>(json);
        Assert.That(rules, Is.Not.Null);
        Assert.That(rules.version, Is.EqualTo(1));
        Assert.That(rules.sprites, Is.Not.Null.And.Not.Empty);
        return rules;
    }

    private static string Normalize(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, Normalize(assetPath)));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName
            .Replace('\\', '/')
            .TrimEnd('/');
        string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
        return normalized.Substring(projectRoot.Length + 1);
    }
}
#endif
