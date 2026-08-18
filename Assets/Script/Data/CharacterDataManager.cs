using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 职业配置管理器：启动时读取 CharacterDefine.json，并建立 classId 索引。
/// 静态职业数据在这里统一查询，角色选择和生成代码不需要各自重复解析 JSON。
/// </summary>
public class CharacterDataManager : MonoBehaviour
{
    public static CharacterDataManager Instance { get; private set; }

    public List<CharacterDefine> Characters { get; private set; } = new List<CharacterDefine>();

    private Dictionary<int, CharacterDefine> characterMap = new Dictionary<int, CharacterDefine>();

    /// <summary>
    /// 初始化职业配置管理器。
    /// 它会跨场景常驻，因为登录、选角和主场景都会用到同一份职业配置。
    /// </summary>
    private void Awake()
    {
        // 配置管理器需要跨登录、选角和主场景存在；重复实例必须销毁，避免读取到两份配置。
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadCharacterDefine();
    }

    /// <summary>
    /// 从 Resources/Data/CharacterDefine.json 读取职业配置，并建立 classId 索引。
    /// </summary>
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

    /// <summary>
    /// 按职业编号获取对应配置。
    /// 角色选择、角色生成和表现适配都会通过这个入口查静态职业数据。
    /// </summary>
    public CharacterDefine GetCharacter(int classId)
    {
        // 字典查询为 O(1)，比每次遍历职业列表更适合频繁按职业编号取配置。
        if (characterMap.TryGetValue(classId, out CharacterDefine define))
        {
            return define;
        }

        Debug.LogError($"没有找到职业配置：classId = {classId}");
        return null;
    }
}
