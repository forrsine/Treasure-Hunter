using QFramework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>金币 HUD：监听余额事件刷新，不在 Update 中轮询经济模型。</summary>
[DisallowMultipleComponent]
public sealed class GoldHudView : MonoBehaviour, IController
{
    [SerializeField] private Text goldText;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.RegisterEvent<GoldChangedEvent>(HandleGoldChanged);
        Refresh(this.SendQuery(new GetGoldQuery()));
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<GoldChangedEvent>(HandleGoldChanged);
    }

    public bool ValidateReferences(bool logError)
    {
        if (goldText != null)
        {
            return true;
        }

        if (logError)
        {
            Debug.LogError("GoldHudView 未配置 goldText。", this);
        }

        return false;
    }

    private void HandleGoldChanged(GoldChangedEvent evt) => Refresh(evt.CurrentGold);

    private void Refresh(long gold)
    {
        if (goldText != null)
        {
            goldText.text = $"金币  {gold:N0}";
        }
    }
}
