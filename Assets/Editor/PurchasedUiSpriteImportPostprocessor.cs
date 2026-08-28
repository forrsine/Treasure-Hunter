#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 淘宝 UI 素材导入器：只处理专用素材目录，统一运行时 Sprite 的导入参数。
/// 原始 PSD/JPG 只作为设计参考导入，避免误当成运行时 Sprite 使用。
/// </summary>
public sealed class PurchasedUiSpriteImportPostprocessor : AssetPostprocessor
{
    public const string SourceRoot = "Assets/AllResources/淘宝ui素材/";
    public const string RuntimeSpriteRoot = SourceRoot + "RuntimeSprites/";
    public const string ImportRulesPath = "Assets/Editor/PurchasedUiSpriteImportRules.json";

    private const int RuntimeMaxTextureSize = 2048;
    private const int SourcePreviewMaxTextureSize = 512;
    private const float PixelsPerUnit = 100f;
    private static readonly string[] RequiredCategories =
    {
        "Common", "FunctionIcons", "Auth", "Popups", "Home",
        "Character", "Equipment", "Progression", "Shop", "Gameplay"
    };

    private static PurchasedUiImportRules cachedRules;
    private static DateTime cachedRulesWriteTimeUtc;
    private static Dictionary<string, PurchasedUiSpriteRule> cachedRulesByPath;

    /// <summary>
    /// Unity 导入贴图前执行。路径判断放在最前面，确保不会改变项目其他美术资源。
    /// </summary>
    private void OnPreprocessTexture()
    {
        string normalizedPath = NormalizeAssetPath(assetPath);
        TextureImporter importer = (TextureImporter)assetImporter;

        if (normalizedPath.StartsWith(RuntimeSpriteRoot, StringComparison.Ordinal))
        {
            ConfigureRuntimeSprite(importer, normalizedPath);
            return;
        }

        if (IsSourceReferenceTexture(normalizedPath))
        {
            ConfigureSourceReference(importer);
        }
    }

    /// <summary>
    /// 手动重导所有已切 PNG，适合规则或 Unity 版本变化后统一刷新设置。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/UI Assets/Apply Import Settings")]
    public static void ApplyImportSettings()
    {
        string runtimeDirectory = ToAbsolutePath(RuntimeSpriteRoot);
        if (!Directory.Exists(runtimeDirectory))
        {
            Debug.LogError($"找不到淘宝 UI 运行时目录：{RuntimeSpriteRoot}");
            return;
        }

        string[] pngFiles = Directory.GetFiles(runtimeDirectory, "*.png", SearchOption.AllDirectories);
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string pngFile in pngFiles)
            {
                string pngAssetPath = ToAssetPath(pngFile);
                AssetDatabase.ImportAsset(
                    pngAssetPath,
                    ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"淘宝 UI 导入设置已应用，共处理 {pngFiles.Length} 个 Sprite。 ");
    }

    /// <summary>
    /// 命令行入口：用于批处理导入，不弹出编辑器对话框。
    /// </summary>
    public static void ApplyImportSettingsFromCommandLine()
    {
        ApplyImportSettings();
        Debug.Log("PURCHASED_UI_IMPORT_SETTINGS_APPLIED");
    }

    /// <summary>
    /// 验证素材文件、清单和 TextureImporter 是否一致。
    /// 返回 false 时会逐项输出原因，方便定位漏导、重名或九宫格配置错误。
    /// </summary>
    public static bool ValidateLibrary()
    {
        List<string> errors = CollectValidationErrors();
        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError("淘宝 UI 素材校验失败：" + error);
            }

            Debug.LogError($"淘宝 UI 素材库共有 {errors.Count} 个问题。 ");
            return false;
        }

        PurchasedUiImportRules rules = LoadRules(true);
        Debug.Log($"淘宝 UI 素材库校验通过，共 {rules.sprites.Length} 个运行时 Sprite。 ");
        return true;
    }

    /// <summary>
    /// Unity 菜单要求入口方法返回 void；实际校验结果仍由 ValidateLibrary 返回，供测试和批处理复用。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/UI Assets/Validate Library")]
    private static void ValidateLibraryFromMenu()
    {
        ValidateLibrary();
    }

    /// <summary>
    /// 命令行验证入口：失败时抛出异常，让 Unity 批处理返回非零状态。
    /// </summary>
    public static void ValidateLibraryFromCommandLine()
    {
        if (!ValidateLibrary())
        {
            throw new InvalidOperationException("淘宝 UI 素材库验证失败，请检查 Unity 日志。 ");
        }

        Debug.Log("PURCHASED_UI_LIBRARY_VALIDATED");
    }

    /// <summary>
    /// 测试与菜单共用的验证逻辑，避免测试只验证另一套重复规则。
    /// </summary>
    public static List<string> CollectValidationErrors()
    {
        List<string> errors = new List<string>();
        PurchasedUiImportRules rules = LoadRules(false);
        if (rules == null || rules.sprites == null)
        {
            errors.Add($"无法读取导入规则：{ImportRulesPath}");
            return errors;
        }

        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> spriteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string category in RequiredCategories)
        {
            string categoryPath = ToAbsolutePath(RuntimeSpriteRoot + category);
            if (!Directory.Exists(categoryPath))
            {
                errors.Add("缺少运行时分类目录：" + category);
            }
        }

        foreach (PurchasedUiSpriteRule rule in rules.sprites)
        {
            string path = NormalizeAssetPath(rule.path);
            if (!paths.Add(path))
            {
                errors.Add("规则路径重复：" + path);
                continue;
            }

            string spriteName = Path.GetFileNameWithoutExtension(path);
            if (!spriteNames.Add(spriteName))
            {
                errors.Add("Sprite 名称大小写重复：" + spriteName);
            }

            string absolutePath = ToAbsolutePath(path);
            if (!File.Exists(absolutePath))
            {
                errors.Add("文件不存在：" + path);
                continue;
            }
            if (new FileInfo(absolutePath).Length == 0)
            {
                errors.Add("图片文件为空：" + path);
                continue;
            }
            if (rule.border == null || rule.border.Length != 4 || rule.border.Any(value => value < 0))
            {
                errors.Add("九宫格 Border 必须是四个非负整数：" + path);
                continue;
            }

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                errors.Add("尚未由 Unity 导入：" + path);
                continue;
            }

            if (importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single)
            {
                errors.Add("不是 Single Sprite：" + path);
            }
            if (importer.mipmapEnabled)
            {
                errors.Add("UI Sprite 不应开启 Mipmap：" + path);
            }
            if (!importer.alphaIsTransparency)
            {
                errors.Add("没有启用 Alpha Is Transparency：" + path);
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                errors.Add("Wrap Mode 不是 Clamp：" + path);
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                errors.Add("Filter Mode 不是 Bilinear：" + path);
            }
            if (Mathf.Abs(importer.spritePixelsPerUnit - PixelsPerUnit) > 0.01f)
            {
                errors.Add("Pixels Per Unit 不是 100：" + path);
            }
            if (importer.maxTextureSize != RuntimeMaxTextureSize)
            {
                errors.Add("Max Size 不是 2048：" + path);
            }
            TextureImporterSettings importerSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(importerSettings);
            if (importerSettings.spriteMeshType != SpriteMeshType.FullRect)
            {
                errors.Add("Mesh Type 不是 Full Rect：" + path);
            }

            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                errors.Add("无法加载 Sprite 子资源：" + path);
                continue;
            }

            Vector4 expectedBorder = RuleBorder(rule);
            if (sprite.border != expectedBorder)
            {
                errors.Add($"九宫格 Border 不一致：{path}，预期 {expectedBorder}，实际 {sprite.border}");
            }
            if (expectedBorder.x + expectedBorder.z >= sprite.rect.width ||
                expectedBorder.y + expectedBorder.w >= sprite.rect.height)
            {
                errors.Add("九宫格 Border 超过 Sprite 尺寸：" + path);
            }
        }

        string runtimeDirectory = ToAbsolutePath(RuntimeSpriteRoot);
        int actualPngCount = Directory.Exists(runtimeDirectory)
            ? Directory.GetFiles(runtimeDirectory, "*.png", SearchOption.AllDirectories).Length
            : 0;
        if (actualPngCount != rules.sprites.Length)
        {
            errors.Add($"PNG 数量与规则不一致：文件 {actualPngCount}，规则 {rules.sprites.Length}。 ");
        }

        return errors;
    }

    private static void ConfigureRuntimeSprite(TextureImporter importer, string path)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
        importer.maxTextureSize = RuntimeMaxTextureSize;
        importer.textureCompression = TextureImporterCompression.Compressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        PurchasedUiSpriteRule rule = FindRule(path);
        importer.spriteBorder = rule != null ? RuleBorder(rule) : Vector4.zero;
    }

    private static void ConfigureSourceReference(TextureImporter importer)
    {
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
        importer.maxTextureSize = SourcePreviewMaxTextureSize;
        importer.textureCompression = TextureImporterCompression.Compressed;
    }

    private static bool IsSourceReferenceTexture(string path)
    {
        if (!path.StartsWith(SourceRoot, StringComparison.Ordinal) ||
            path.StartsWith(RuntimeSpriteRoot, StringComparison.Ordinal))
        {
            return false;
        }

        string extension = Path.GetExtension(path);
        return extension.Equals(".psd", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private static PurchasedUiSpriteRule FindRule(string path)
    {
        EnsureRuleCache();
        cachedRulesByPath.TryGetValue(NormalizeAssetPath(path), out PurchasedUiSpriteRule rule);
        return rule;
    }

    private static void EnsureRuleCache()
    {
        string absoluteRulesPath = ToAbsolutePath(ImportRulesPath);
        DateTime writeTimeUtc = File.Exists(absoluteRulesPath)
            ? File.GetLastWriteTimeUtc(absoluteRulesPath)
            : DateTime.MinValue;
        if (cachedRulesByPath != null && writeTimeUtc == cachedRulesWriteTimeUtc)
        {
            return;
        }

        cachedRules = LoadRules(false);
        cachedRulesWriteTimeUtc = writeTimeUtc;
        cachedRulesByPath = cachedRules?.sprites != null
            ? cachedRules.sprites
                .Where(rule => rule != null && !string.IsNullOrWhiteSpace(rule.path))
                .GroupBy(rule => NormalizeAssetPath(rule.path), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, PurchasedUiSpriteRule>(StringComparer.OrdinalIgnoreCase);
    }

    private static PurchasedUiImportRules LoadRules(bool throwOnFailure)
    {
        string absoluteRulesPath = ToAbsolutePath(ImportRulesPath);
        if (!File.Exists(absoluteRulesPath))
        {
            if (throwOnFailure)
            {
                throw new FileNotFoundException("找不到淘宝 UI 导入规则。", absoluteRulesPath);
            }
            return null;
        }

        try
        {
            string json = File.ReadAllText(absoluteRulesPath);
            return JsonUtility.FromJson<PurchasedUiImportRules>(json);
        }
        catch (Exception exception)
        {
            if (throwOnFailure)
            {
                throw new InvalidOperationException("无法解析淘宝 UI 导入规则。", exception);
            }
            Debug.LogException(exception);
            return null;
        }
    }

    private static Vector4 RuleBorder(PurchasedUiSpriteRule rule)
    {
        if (rule?.border == null || rule.border.Length != 4)
        {
            return Vector4.zero;
        }
        return new Vector4(rule.border[0], rule.border[1], rule.border[2], rule.border[3]);
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static string ToAbsolutePath(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.GetFullPath(Path.Combine(projectRoot, NormalizeAssetPath(assetPath)));
    }

    private static string ToAssetPath(string absolutePath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName
            .Replace('\\', '/')
            .TrimEnd('/');
        string normalizedAbsolutePath = Path.GetFullPath(absolutePath).Replace('\\', '/');
        if (!normalizedAbsolutePath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("文件不在当前 Unity 项目内：" + absolutePath);
        }
        return normalizedAbsolutePath.Substring(projectRoot.Length + 1);
    }
}

[Serializable]
public sealed class PurchasedUiImportRules
{
    public int version;
    public string runtimeSpriteRoot;
    public string sourceRoot;
    public PurchasedUiSpriteRule[] sprites;
}

[Serializable]
public sealed class PurchasedUiSpriteRule
{
    public string path;
    public int[] border;
    public string role;
}
#endif
