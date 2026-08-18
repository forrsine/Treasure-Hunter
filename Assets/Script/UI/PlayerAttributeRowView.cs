using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家属性行 View：对应 Prefab 中一条固定属性，只负责显示名称、数值和变化高亮。
/// key 用来匹配 Query 返回的 PlayerAttributeEntry；本组件不读取或修改 PlayerModel。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerAttributeRowView : MonoBehaviour
{
    [SerializeField] private string key;
    [SerializeField] private Image background;
    [SerializeField] private Text labelText;
    [SerializeField] private Text valueText;

    private string lastValue;
    private float highlightTimer;

    public string Key => key;

    public bool ValidatePrefabReferences(bool logError)
    {
        string missing = null;
        if (string.IsNullOrWhiteSpace(key)) missing = nameof(key);
        else if (background == null) missing = nameof(background);
        else if (labelText == null) missing = nameof(labelText);
        else if (valueText == null) missing = nameof(valueText);

        if (missing == null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError($"PlayerAttributeRowView 的 Prefab 引用未配置：{missing}。", this);
        }

        return false;
    }

    public void SetContent(
        PlayerAttributeEntry entry,
        Color normalBackground,
        Color normalValue,
        Color highlightBackground,
        Color highlightValue,
        float highlightDuration)
    {
        if (!ValidatePrefabReferences(false))
        {
            return;
        }

        if (!string.IsNullOrEmpty(lastValue) && lastValue != entry.Value)
        {
            highlightTimer = Mathf.Max(0f, highlightDuration);
        }

        lastValue = entry.Value;
        labelText.text = entry.Label;
        valueText.text = entry.Value;
        gameObject.SetActive(true);
        ApplyHighlight(normalBackground, normalValue, highlightBackground, highlightValue);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void TickHighlight(
        float deltaTime,
        Color normalBackground,
        Color normalValue,
        Color highlightBackground,
        Color highlightValue)
    {
        if (highlightTimer > 0f)
        {
            highlightTimer = Mathf.Max(0f, highlightTimer - deltaTime);
        }

        ApplyHighlight(normalBackground, normalValue, highlightBackground, highlightValue);
    }

    private void ApplyHighlight(
        Color normalBackground,
        Color normalValue,
        Color highlightBackground,
        Color highlightValue)
    {
        if (background == null || valueText == null)
        {
            return;
        }

        bool highlighted = highlightTimer > 0f;
        background.color = highlighted ? highlightBackground : normalBackground;
        valueText.color = highlighted ? highlightValue : normalValue;
    }
}
