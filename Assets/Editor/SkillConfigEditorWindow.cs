using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 技能配置编辑器工具：用于在 Unity 编辑器里查看、修改并保存 SkillDefine.json。
/// 注意：这个脚本只能放在 Assets/Editor 目录下，不能进入游戏运行时代码。
/// </summary>
public class SkillConfigEditorWindow : EditorWindow
{
    private const string ConfigPath = "Assets/Resources/Data/SkillDefine.json";

    private SkillDefineTable table;
    private int selectedIndex = -1;
    private Vector2 leftScroll;
    private Vector2 rightScroll;

    /// <summary>
    /// 在 Unity 顶部菜单创建入口。
    /// </summary>
    [MenuItem("Tools/Treasure Hunter/Skill Config")]
    private static void OpenWindow()
    {
        SkillConfigEditorWindow window = GetWindow<SkillConfigEditorWindow>("Skill Config");
        window.minSize = new Vector2(900f, 520f);
        window.LoadConfig();
    }

    private void OnEnable()
    {
        LoadConfig();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();

        DrawSkillList();
        DrawSkillDetail();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        DrawBottomButtons();
    }

    /// <summary>
    /// 读取 SkillDefine.json。
    /// </summary>
    private void LoadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            Debug.LogError($"找不到技能配置表：{ConfigPath}");
            table = new SkillDefineTable { skills = new List<SkillDefine>() };
            return;
        }

        string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
        table = JsonUtility.FromJson<SkillDefineTable>(json);

        if (table == null)
        {
            table = new SkillDefineTable();
        }

        if (table.skills == null)
        {
            table.skills = new List<SkillDefine>();
        }

        if (selectedIndex >= table.skills.Count)
        {
            selectedIndex = table.skills.Count - 1;
        }
    }

    /// <summary>
    /// 左侧技能列表。
    /// </summary>
    private void DrawSkillList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(260));
        EditorGUILayout.LabelField("技能列表", EditorStyles.boldLabel);

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, "box");

        for (int i = 0; i < table.skills.Count; i++)
        {
            SkillDefine skill = table.skills[i];
            string label = skill != null
                ? $"{skill.skillId}  {skill.name}"
                : "空技能";

            if (GUILayout.Toggle(selectedIndex == i, label, "Button"))
            {
                selectedIndex = i;
            }
        }

        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("新增技能"))
        {
            AddSkill();
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 右侧技能详情编辑区。
    /// </summary>
    private void DrawSkillDetail()
    {
        EditorGUILayout.BeginVertical();

        if (selectedIndex < 0 || selectedIndex >= table.skills.Count)
        {
            EditorGUILayout.HelpBox("请选择一个技能。", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        SkillDefine skill = table.skills[selectedIndex];

        rightScroll = EditorGUILayout.BeginScrollView(rightScroll, "box");

        EditorGUILayout.LabelField("基础信息", EditorStyles.boldLabel);
        skill.skillId = EditorGUILayout.IntField("技能 ID", skill.skillId);
        skill.skillKey = EditorGUILayout.TextField("英文 Key", skill.skillKey);
        skill.name = EditorGUILayout.TextField("技能名", skill.name);
        skill.description = EditorGUILayout.TextField("描述", skill.description);
        skill.skillType = EditorGUILayout.TextField("技能类型", skill.skillType);
        skill.isCommon = EditorGUILayout.Toggle("是否通用", skill.isCommon);
        skill.maxLevel = EditorGUILayout.IntField("最高等级", skill.maxLevel);
        skill.unlockLevel = EditorGUILayout.IntField("解锁等级", skill.unlockLevel);

        DrawAllowedClassIds(skill);

        EditorGUILayout.Space(10);
        DrawLevelList(skill);

        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// 编辑允许职业 ID。
    /// 第一版用逗号字符串，简单直观。
    /// </summary>
    private void DrawAllowedClassIds(SkillDefine skill)
    {
        if (skill.allowedClassIds == null)
        {
            skill.allowedClassIds = new List<int>();
        }

        string text = string.Join(",", skill.allowedClassIds);
        string newText = EditorGUILayout.TextField("允许职业 ID", text);

        if (newText != text)
        {
            skill.allowedClassIds.Clear();

            string[] parts = newText.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (int.TryParse(parts[i].Trim(), out int classId))
                {
                    skill.allowedClassIds.Add(classId);
                }
            }
        }
    }

    /// <summary>
    /// 编辑技能等级数据。
    /// </summary>
    private void DrawLevelList(SkillDefine skill)
    {
        EditorGUILayout.LabelField("等级数值", EditorStyles.boldLabel);

        if (skill.levels == null)
        {
            skill.levels = new List<SkillLevelDefine>();
        }

        for (int i = 0; i < skill.levels.Count; i++)
        {
            SkillLevelDefine level = skill.levels[i];

            EditorGUILayout.BeginVertical("box");

            level.level = EditorGUILayout.IntField("等级", level.level);
            level.mpCost = EditorGUILayout.IntField("蓝耗", level.mpCost);
            level.cooldown = EditorGUILayout.FloatField("冷却", level.cooldown);
            level.damageRate = EditorGUILayout.FloatField("伤害倍率", level.damageRate);
            level.radius = EditorGUILayout.FloatField("范围", level.radius);
            level.duration = EditorGUILayout.FloatField("持续时间", level.duration);
            level.tickInterval = EditorGUILayout.FloatField("Tick 间隔", level.tickInterval);
            level.slowRate = EditorGUILayout.Slider("减速比例", level.slowRate, 0f, 1f);

            if (GUILayout.Button("删除该等级"))
            {
                skill.levels.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndVertical();
        }

        if (GUILayout.Button("新增等级"))
        {
            skill.levels.Add(new SkillLevelDefine
            {
                level = skill.levels.Count + 1,
                mpCost = 10,
                cooldown = 1f,
                damageRate = 1f,
                radius = 3f
            });
        }
    }

    private void DrawBottomButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("重新加载"))
        {
            LoadConfig();
        }

        if (GUILayout.Button("保存到 JSON"))
        {
            SaveConfig();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void AddSkill()
    {
        table.skills.Add(new SkillDefine
        {
            skillId = 9999,
            skillKey = "NewSkill",
            name = "新技能",
            description = "技能描述",
            skillType = "SelfAoe",
            isCommon = true,
            allowedClassIds = new List<int>(),
            maxLevel = 1,
            unlockLevel = 5,
            levels = new List<SkillLevelDefine>
            {
                new SkillLevelDefine
                {
                    level = 1,
                    mpCost = 10,
                    cooldown = 1f,
                    damageRate = 1f,
                    radius = 3f
                }
            }
        });

        selectedIndex = table.skills.Count - 1;
    }

    /// <summary>
    /// 保存回 SkillDefine.json。
    /// </summary>
    private void SaveConfig()
    {
        string json = JsonUtility.ToJson(table, true);
        File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));

        AssetDatabase.Refresh();
        Debug.Log($"技能配置已保存：{ConfigPath}");
    }
}