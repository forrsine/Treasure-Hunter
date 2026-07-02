using System.Collections.Generic;
using UnityEngine;

public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance { get; private set; }

    public List<CharacterDefine> Characters { get; private set; } = new List<CharacterDefine>();

    private Dictionary<int, CharacterDefine> characterMap = new Dictionary<int, CharacterDefine>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCharacterDefine();
    }

    private void LoadCharacterDefine()
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>("Data/CharacterDefine");

        if (jsonAsset == null)
        {
            Debug.LogError("没有找到职业配置表：Resources/Data/CharacterDefine.json");
            return;
        }

        CharacterDefineTable table = JsonUtility.FromJson<CharacterDefineTable>(jsonAsset.text);

        if (table == null || table.characters == null)
        {
            Debug.LogError("职业配置表格式错误");
            return;
        }

        Characters = table.characters;
        characterMap.Clear();

        foreach (CharacterDefine define in Characters)
        {
            characterMap[define.classId] = define;
        }

        Debug.Log($"职业配置表加载完成，共 {Characters.Count} 个职业");
    }

    public CharacterDefine GetCharacter(int classId)
    {
        if (characterMap.TryGetValue(classId, out CharacterDefine define))
        {
            return define;
        }

        Debug.LogError($"没有找到职业配置：classId = {classId}");
        return null;
    }
}