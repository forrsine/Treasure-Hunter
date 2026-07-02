using System;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSaveSlot : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text classText;
    [SerializeField] private Text levelText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button button;

    private int slotIndex;
    private Action<int> onClick;

    public void SetEmpty(int index, Action<int> clickCallback)
    {
        slotIndex = index;
        onClick = clickCallback;

        nameText.text = "空存档";
        classText.text = "点击创建角色";
        levelText.text = "";

        SetSelected(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            onClick?.Invoke(slotIndex);
        });
    }

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

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
        {
            selectedFrame.SetActive(selected);
        }
    }
}