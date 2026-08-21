#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 操作说明关闭按钮配置测试：确保按钮使用图片显示“×”，不会再叠加旧版文字。
/// </summary>
public sealed class StartupGuideCloseButtonConfigurationTests
{
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";

    [Test]
    public void CloseButton_UsesImageWithoutTextChild()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        Assert.That(prefab, Is.Not.Null, $"找不到玩法 UI Prefab：{GameplayUiPrefabPath}");

        Transform closeButton = prefab.transform.Find("StartupGuidePopup/Panel/CloseButton");
        Assert.That(closeButton, Is.Not.Null, "操作说明面板缺少 CloseButton。");
        Assert.That(closeButton.GetComponent<Button>(), Is.Not.Null, "CloseButton 缺少 Button 组件。");

        Image closeButtonImage = closeButton.GetComponent<Image>();
        Assert.That(closeButtonImage, Is.Not.Null, "CloseButton 缺少 Image 组件。");
        Assert.That(closeButtonImage.sprite, Is.Not.Null, "CloseButton 没有配置关闭图标。");
        Assert.That(
            closeButton.GetComponentsInChildren<Text>(true),
            Is.Empty,
            "CloseButton 已使用关闭图标，不应再包含文字子对象。");

        GameplayStartupGuidePopup popup = prefab.GetComponent<GameplayStartupGuidePopup>();
        Assert.That(popup, Is.Not.Null);
        Assert.That(popup.ValidatePrefabReferences(false), Is.True);
    }
}
#endif
