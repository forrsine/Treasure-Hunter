#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 玩家架构验收工具：检查通用玩家 Prefab 是否挂齐新组件，并阻止旧巨型控制脚本重新进入 Prefab。
/// 可从菜单运行，也可在命令行/持续集成中调用。
/// </summary>
public static class PlayerArchitectureValidator
{
    private const string RuntimePrefabPath = "Assets/Resources/Characters/PlayerRuntime.prefab";
    private const string GameplayUiPrefabPath = "Assets/Prefabs/UI/GameplayUiRoot.prefab";
    private const string MainScenePath = "Assets/Scenes/MainScene.unity";

    [MenuItem("Tools/Treasure Hunter/Validate Player Architecture")]
    public static void ValidateFromMenu()
    {
        Validate();
        Debug.Log("玩家架构验证通过。");
    }

    public static void ValidateFromCommandLine()
    {
        Validate();
        Debug.Log("PLAYER_ARCHITECTURE_VALIDATION_SUCCEEDED");
    }

    private static void Validate()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimePrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"找不到玩家运行时 Prefab：{RuntimePrefabPath}");
        }

        RequireComponent<PlayerRuntimeController>(prefab);
        RequireComponent<CharacterController>(prefab);
        RequireComponent<PlayerMovementComponent>(prefab);
        RequireComponent<PlayerCombatComponent>(prefab);
        RequireComponent<PlayerHealthComponent>(prefab);
        RequireComponent<PlayerProgressionComponent>(prefab);
        RequireComponent<PlayerPresentationComponent>(prefab);
        RequireComponent<PlayerAudioComponent>(prefab);
        RejectComponent<GameSessionUi>(prefab);
        RejectComponent<PlayerAttributePanel>(prefab);
        RejectComponent<PlayerLevelUpPanel>(prefab);
        RejectComponent<GameplayUiRoot>(prefab);

        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "PlayerCo")
            {
                throw new InvalidOperationException("PlayerRuntime.prefab 仍挂载旧玩家控制脚本。");
            }
        }

        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
        {
            throw new InvalidOperationException("PlayerRuntime.prefab 存在 Missing Script。");
        }

        Transform hitbox = prefab.transform.Find("AttackHitbox");
        if (hitbox == null || hitbox.GetComponent<SphereCollider>() == null || hitbox.GetComponent<WeaponCo>() == null)
        {
            throw new InvalidOperationException("AttackHitbox 缺少 SphereCollider 或 WeaponCo。");
        }

        ValidateCareerVisuals();
        ValidateGameplayUi();
    }

    /// <summary>
    /// 验证四个职业只提供表现资源，不把旧玩家逻辑重新带进通用运行时。
    /// </summary>
    private static void ValidateCareerVisuals()
    {
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Data/CharacterDefine.json");
        CharacterDefineTable table = json != null ? JsonUtility.FromJson<CharacterDefineTable>(json.text) : null;
        if (table == null || table.characters == null || table.characters.Count != 4)
        {
            throw new InvalidOperationException("职业配置应包含四个有效职业。");
        }

        for (int i = 0; i < table.characters.Count; i++)
        {
            CharacterDefine define = table.characters[i];
            string path = !string.IsNullOrWhiteSpace(define.visualPrefabPath)
                ? define.visualPrefabPath
                : define.gamePrefabPath;
            GameObject visual = Resources.Load<GameObject>(path);
            if (visual == null)
            {
                throw new InvalidOperationException($"职业 {define.classId} 缺少表现 Prefab：Resources/{path}");
            }

            MonoBehaviour[] behaviours = visual.GetComponentsInChildren<MonoBehaviour>(true);
            for (int j = 0; j < behaviours.Length; j++)
            {
                if (behaviours[j] != null && behaviours[j].GetType().Name == "PlayerCo")
                {
                    throw new InvalidOperationException($"职业表现 Prefab 不应挂载旧玩家逻辑：{path}");
                }
            }
        }
    }

    private static void RequireComponent<T>(GameObject prefab) where T : Component
    {
        if (prefab.GetComponent<T>() == null)
        {
            throw new InvalidOperationException($"PlayerRuntime.prefab 缺少组件：{typeof(T).Name}");
        }
    }

    private static void RejectComponent<T>(GameObject prefab) where T : Component
    {
        if (prefab.GetComponentInChildren<T>(true) != null)
        {
            throw new InvalidOperationException($"PlayerRuntime.prefab 不应包含场景 UI：{typeof(T).Name}");
        }
    }

    /// <summary>
    /// UI 独立验收：Prefab 必须显式拥有四个 View，MainScene 只能放一个玩法 UI 根。
    /// </summary>
    private static void ValidateGameplayUi()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayUiPrefabPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"找不到玩法 UI Prefab：{GameplayUiPrefabPath}");
        }

        if (prefab.GetComponent<Canvas>() == null ||
            prefab.GetComponent<GameplayUiRoot>() == null ||
            prefab.GetComponent<PlayerHudUi>() == null ||
            prefab.GetComponent<GameSessionUi>() == null ||
            prefab.GetComponent<PlayerAttributePanel>() == null ||
            prefab.GetComponent<PlayerLevelUpPanel>() == null)
        {
            throw new InvalidOperationException("GameplayUiRoot.prefab 缺少 Canvas 或玩法 UI 组件。");
        }

        GameplayUiRoot uiRoot = prefab.GetComponent<GameplayUiRoot>();
        PlayerHudUi playerHudUi = prefab.GetComponent<PlayerHudUi>();
        GameSessionUi sessionUi = prefab.GetComponent<GameSessionUi>();
        PlayerAttributePanel attributePanel = prefab.GetComponent<PlayerAttributePanel>();
        PlayerLevelUpPanel levelUpPanel = prefab.GetComponent<PlayerLevelUpPanel>();
        if (!uiRoot.ValidatePrefabReferences(false) ||
            !playerHudUi.ValidatePrefabReferences(false) ||
            !sessionUi.ValidatePrefabReferences(false) ||
            !attributePanel.ValidatePrefabReferences(false) ||
            !levelUpPanel.ValidatePrefabReferences(false))
        {
            throw new InvalidOperationException("GameplayUiRoot.prefab 存在未配置的序列化引用。");
        }

        PlayerAttributeRowView[] rows = prefab.GetComponentsInChildren<PlayerAttributeRowView>(true);
        if (rows.Length != 12)
        {
            throw new InvalidOperationException($"属性面板应包含 12 条静态属性行，当前数量：{rows.Length}。");
        }

        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab) > 0)
        {
            throw new InvalidOperationException("GameplayUiRoot.prefab 存在 Missing Script。");
        }

        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
        GameplayUiRoot[] roots = UnityEngine.Object.FindObjectsOfType<GameplayUiRoot>(true);
        if (roots.Length != 1 || roots[0].gameObject.scene != scene)
        {
            throw new InvalidOperationException($"MainScene 应且只能包含一个 GameplayUiRoot，当前数量：{roots.Length}。");
        }

        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);
        if (eventSystems.Length != 1 || eventSystems[0].gameObject.scene != scene)
        {
            throw new InvalidOperationException($"MainScene 应且只能包含一个 EventSystem，当前数量：{eventSystems.Length}。");
        }

        GameplayStartupGuidePopup[] guides = UnityEngine.Object.FindObjectsOfType<GameplayStartupGuidePopup>(true);
        if (guides.Length != 1 || !guides[0].ValidatePrefabReferences(false))
        {
            throw new InvalidOperationException("MainScene 的新手引导弹窗缺失或序列化引用不完整。");
        }
    }
}
#endif
