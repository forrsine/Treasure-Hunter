using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 单个角色存档槽的 UI 视图。
/// 它只显示空槽/角色数据并回传点击的槽位编号，创建和选择角色的业务逻辑由上层面板处理。
/// </summary>
public class CharacterSaveSlot : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text classText;
    [SerializeField] private Text levelText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button button;

    private int slotIndex;
    private Action<int> onClick;

    /// <summary>
    /// 把当前槽位设置成“空存档”显示。
    /// 这里不直接创建角色，只把点击回调交还给上层面板决定下一步逻辑。
    /// </summary>
    public void SetEmpty(int index, Action<int> clickCallback)
    {
        slotIndex = index;
        onClick = clickCallback;

        nameText.text = "空存档";
        classText.text = "点击创建角色";
        levelText.text = "";

        SetSelected(false);

        // 槽位会被反复复用，先清理旧回调可避免一次点击触发多次。
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onClick?.Invoke(slotIndex);
        });
    }

    /// <summary>
    /// 把当前槽位填充为已有角色数据。
    /// </summary>
    public void SetData(int index, NCharacter save, Action<int> clickCallback)
    {
        slotIndex = index;
        onClick = clickCallback;

        CharacterDefine define = CharacterDataManager.Instance.GetCharacter(save.classId);

        nameText.text = save.name;
        classText.text = define != null ? define.name : "未知职业";
        levelText.text = $"Lv.{save.level}";

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onClick?.Invoke(slotIndex);
        });
    }

    /// <summary>
    /// 控制槽位高亮框显隐，用于表示当前被选中的存档位。
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }
    }
}
