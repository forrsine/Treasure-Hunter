using UnityEngine;

public class SelectedCharacterTest : MonoBehaviour
{
    private void Start()
    {
        NCharacter save = SelectedCharacterState.CurrentCharacter;

        if (save == null)
        {
            Debug.LogWarning("没有选择角色");
            return;
        }

        Debug.Log($"当前进入游戏的角色：{save.name}, classId={save.classId}, level={save.level}");
    }
}