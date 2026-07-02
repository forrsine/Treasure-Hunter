using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色选择场景的总控制器。
///
/// 这个脚本主要负责以下几件事：
/// 1. 在“存档选择面板”和“创建角色面板”之间切换；
/// 2. 从服务器读取当前账号的角色存档，并把数据填入四个存档槽位；
/// 3. 记录玩家当前选中的存档槽位和职业；
/// 4. 请求服务器创建角色；
/// 5. 根据当前选择刷新角色预览、职业说明和存档高亮；
/// 6. 将最终选中的角色交给 SceneFlowService，进入游戏场景。
///
/// 注意：界面引用都通过 SerializeField 在 Inspector 中赋值，因此在 Unity
/// 场景里必须正确绑定按钮、文本、面板和存档槽位，否则运行时可能出现空引用。
/// </summary>
public class CharacterSelectPanelController : MonoBehaviour
{
    // ---------------------------- 服务引用 ----------------------------

    [Header("Services")]
    // 与游戏服务器通信的客户端。若 Inspector 没有指定，会在 Awake 中自动获取。
    [SerializeField] private GameApiClient apiClient;

    // ---------------------------- 面板引用 ----------------------------

    [Header("Panels")]
    // 显示四个角色存档、开始游戏和创建角色入口的面板。
    [SerializeField] private GameObject savePanel;

    // 输入角色名、选择职业并最终创建角色的面板。
    [SerializeField] private GameObject createCharacterPanel;

    [Header("Save Panel Buttons")]
    // 从存档选择面板进入创建角色面板的按钮。
    [SerializeField] private Button createCharacterButton;

    // 使用当前选中的已有角色进入游戏。
    [SerializeField] private Button startGameButton;

    // 注销当前账号并返回登录场景。
    [SerializeField] private Button backToLoginButton;

    [Header("Create Panel Buttons")]
    // 确认角色名和职业后，向服务器提交创建请求。
    [SerializeField] private Button createButton;

    // 放弃当前创建界面并返回存档选择面板。
    [SerializeField] private Button backToSaveButton;

    [Header("Class Buttons")]
    // 四个职业按钮分别对应职业配置中的 classId 1、2、3、4。
    [SerializeField] private Button warriorButton;
    [SerializeField] private Button wizardButton;
    [SerializeField] private Button archerButton;
    [SerializeField] private Button assassinButton;

    [Header("Class Info")]
    // 显示当前所选职业的名称。
    [SerializeField] private Text classNameText;

    // 显示当前所选职业的详细说明。
    [SerializeField] private Text classDescriptionText;

    // 显示加载、创建、校验失败等操作结果；同时也会把消息输出到 Console。
    [SerializeField] private Text messageText;

    [Header("Preview")]
    // 负责加载、显示、清除和旋转角色预览模型。
    [SerializeField] private CharacterPreviewController previewController;

    [Header("Create Character")]
    // 创建角色时输入角色名称的输入框。
    [SerializeField] private InputField characterNameInput;

    [Header("Save Slots")]
    // 场景中按顺序排列的存档槽位。数组下标就是提交给服务器的 slotIndex。
    [SerializeField] private CharacterSaveSlot[] saveSlots;

    // 存档槽位内部用于表示“当前选中”的子物体名称。
    // 默认会寻找名为 HighLight 的对象，并通过 SetActive 控制其显示状态。
    [SerializeField] private string slotHighlightObjectName = "HighLight";

    // 本地保存的四个角色数据。数组位置与服务器的 slotIndex 一一对应；
    // 某一项为 null 表示该槽位目前没有角色。
    private NCharacter[] saves = new NCharacter[4];

    // 当前在创建界面选择的职业 ID，默认选择战士（classId = 1）。
    private int selectedClassId = 1;

    // 当前在存档面板选中的槽位下标；-1 表示尚未选择任何槽位。
    private int selectedSlotIndex = -1;

    // 当前准备创建角色的目标槽位；-1 表示还没有确定创建位置。
    // 它和 selectedSlotIndex 分开保存，是为了区分“正在浏览存档”和“正在创建角色”。
    private int creatingSlotIndex = -1;

    /// <summary>
    /// Unity 在对象初始化时调用。
    /// 此处只处理本地初始化：获取服务、注册按钮事件，以及显示初始面板。
    /// 服务器请求放在 Start 中执行，避免 Awake 阶段其他单例尚未初始化完成。
    /// </summary>
    private void Awake()
    {
        // Inspector 没有绑定 ApiClient 时，尝试从全局场景流程服务中获取或创建。
        EnsureApiClient();

        // 存档面板按钮事件。
        createCharacterButton.onClick.AddListener(OnClickCreateCharacterSlot);
        startGameButton.onClick.AddListener(OnClickStartGame);
        backToLoginButton.onClick.AddListener(OnClickBackToLogin);

        // 创建角色面板按钮事件。
        createButton.onClick.AddListener(OnClickCreateCharacter);
        backToSaveButton.onClick.AddListener(ShowSavePanel);

        // 职业 ID 必须与 CharacterDefine 配置表及服务端职业枚举保持一致。
        warriorButton.onClick.AddListener(() => SelectClass(1));
        wizardButton.onClick.AddListener(() => SelectClass(2));
        archerButton.onClick.AddListener(() => SelectClass(3));
        assassinButton.onClick.AddListener(() => SelectClass(4));

        // 打开场景时先显示存档面板。此时服务器数据可能还没返回，
        // 因此先按空数组刷新一次，等请求完成后会再次刷新。
        ShowSavePanel();
        RefreshSaveSlots();
    }

    /// <summary>
    /// Unity 在第一次帧更新前调用，从服务器异步加载当前账号的角色列表。
    /// </summary>
    private void Start()
    {
        StartCoroutine(LoadCharactersFromServer());
    }

    /// <summary>
    /// 确保控制器持有可用的 GameApiClient。
    /// 场景里手动绑定时直接使用绑定对象，否则使用全局实例。
    /// </summary>
    private void EnsureApiClient()
    {
        if (apiClient == null)
        {
            apiClient = SceneFlowService.GetOrCreateApiClient();
        }
    }

    /// <summary>
    /// 显示存档选择面板，并根据当前选择决定预览内容。
    /// 如果选中的是已有角色，则显示该职业模型；否则清空预览区域。
    /// </summary>
    private void ShowSavePanel()
    {
        savePanel.SetActive(true);
        createCharacterPanel.SetActive(false);

        // selectedSlotIndex 必须有效，而且该槽位确实有角色，才能显示角色预览。
        if (selectedSlotIndex >= 0 && saves[selectedSlotIndex] != null)
        {
            CharacterDefine define = CharacterDataManager.Instance.GetCharacter(saves[selectedSlotIndex].classId);
            previewController?.ShowCharacter(define);
            return;
        }

        previewController?.ClearCharacter();
    }

    /// <summary>
    /// 显示创建角色面板。
    /// 再次调用 SelectClass 可以同步职业名称、介绍和预览模型，
    /// 即使玩家从创建面板返回后重新进入，也能保持上一次选择的职业。
    /// </summary>
    private void ShowCreatePanel()
    {
        savePanel.SetActive(false);
        createCharacterPanel.SetActive(true);
        SelectClass(selectedClassId);
    }

    /// <summary>
    /// “开始游戏”按钮事件。
    /// 只有当前选中了真实存在的角色存档时才允许进入游戏。
    /// </summary>
    private void OnClickStartGame()
    {
        // 没选槽位，或者选中的是空槽位，都不能开始游戏。
        if (selectedSlotIndex < 0 || saves[selectedSlotIndex] == null)
        {
            SetMessage("Select a character save first.");
            return;
        }

        // 将完整角色数据交给场景流程服务，由它保存当前角色并加载游戏场景。
        NCharacter save = saves[selectedSlotIndex];
        SceneFlowService.StartGameplay(save);
    }

    /// <summary>
    /// 创建面板中的“确认创建”按钮事件。
    /// 先校验目标槽位和角色名，然后启动协程向服务器提交创建请求。
    /// </summary>
    private void OnClickCreateCharacter()
    {
        if (creatingSlotIndex < 0)
        {
            SetMessage("请先选择一个存档位。");
            return;
        }

        // Trim 会移除名称首尾空格，避免把纯空格当成有效角色名。
        string characterName = characterNameInput != null ? characterNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(characterName))
        {
            SetMessage("Enter a character name.");
            return;
        }

        // 创建请求需要同时携带目标槽位、角色名和职业 ID。
        StartCoroutine(CreateCharacterOnServer(creatingSlotIndex, characterName, selectedClassId));
    }

    /// <summary>
    /// 从服务器读取当前登录账号的所有角色。
    /// GameApiClient 通过回调返回结果；yield return 会让协程等待请求结束。
    /// </summary>
    private IEnumerator LoadCharactersFromServer()
    {
        // 没有网络服务时立即结束，防止后续调用产生空引用异常。
        if (apiClient == null)
        {
            SetMessage("ApiClient is missing.");
            yield break;
        }

        SetMessage("Loading character saves...");

        yield return apiClient.GetCharacters((success, message, characters) =>
        {
            // 请求失败时保留当前本地数据，只刷新界面并显示错误消息。
            if (!success)
            {
                SetMessage(string.IsNullOrEmpty(message) ? "Failed to load character saves." : message);
                RefreshSaveSlots();
                ShowSavePanel();
                return;
            }

            // 请求成功后，先按 slotIndex 将服务器角色放入本地数组，
            // 然后刷新每一个存档槽位及其选中状态。
            ApplyServerCharacters(characters);
            SetMessage(string.IsNullOrEmpty(message) ? "Character saves loaded." : message);
            RefreshSaveSlots();
            ShowSavePanel();
        });
    }

    /// <summary>
    /// 向服务器发送创建角色请求。
    ///
    /// slotIndex：角色要保存到的槽位下标；
    /// characterName：经过本地校验的角色名称；
    /// classId：所选职业 ID。
    /// </summary>
    private IEnumerator CreateCharacterOnServer(int slotIndex, string characterName, int classId)
    {
        if (apiClient == null)
        {
            SetMessage("ApiClient is missing.");
            yield break;
        }

        SetMessage("Creating character...");

        // 请求期间禁用相关按钮，防止玩家连续点击造成重复请求。
        SetCreateInteractable(false);

        yield return apiClient.CreateCharacter(slotIndex, characterName, classId, (success, message, createdCharacter) =>
        {
            // 无论成功还是失败，请求结束后都要恢复按钮操作。
            SetCreateInteractable(true);

            // success 为 false 表示服务端拒绝请求；角色为空则表示返回数据不完整。
            if (!success || createdCharacter == null)
            {
                SetMessage(string.IsNullOrEmpty(message) ? "Failed to create character." : message);
                return;
            }

            // 服务端返回的 slotIndex 才是最终可信的保存位置。
            // 确认下标合法后更新本地缓存，并自动选中新创建的角色。
            if (createdCharacter.slotIndex >= 0 && createdCharacter.slotIndex < saves.Length)
            {
                saves[createdCharacter.slotIndex] = createdCharacter;
                selectedSlotIndex = createdCharacter.slotIndex;
            }

            // 创建流程结束，清除“正在创建的槽位”状态。
            creatingSlotIndex = -1;

            // 清空输入框，避免下一次创建时残留上一次的角色名。
            if (characterNameInput != null)
            {
                characterNameInput.text = "";
            }

            // 返回存档面板前刷新数据，这样新角色会立即显示在对应槽位中。
            SetMessage(string.IsNullOrEmpty(message) ? "Character created." : message);
            RefreshSaveSlots();
            ShowSavePanel();
        });
    }

    /// <summary>
    /// 把服务器返回的角色列表转换为固定四格的本地存档数组。
    /// 服务器列表的顺序不一定等于槽位顺序，因此必须使用 character.slotIndex 放置。
    /// </summary>
    private void ApplyServerCharacters(NCharacter[] characters)
    {
        // 每次使用服务器的最新完整结果重建数组，避免残留已经不存在的旧角色。
        saves = new NCharacter[4];

        if (characters != null)
        {
            foreach (NCharacter character in characters)
            {
                // 忽略服务器列表中的空项。
                if (character == null)
                {
                    continue;
                }

                // 忽略异常槽位数据，避免数组越界导致整个角色选择界面失效。
                if (character.slotIndex < 0 || character.slotIndex >= saves.Length)
                {
                    continue;
                }

                saves[character.slotIndex] = character;
            }
        }

        // 如果刷新前选中的槽位仍然存在角色，就继续保留玩家的选择。
        if (selectedSlotIndex >= 0 && selectedSlotIndex < saves.Length && saves[selectedSlotIndex] != null)
        {
            return;
        }

        // 原选中角色已不存在或还没有选择时，重置选择和创建状态。
        selectedSlotIndex = -1;
        creatingSlotIndex = -1;
    }

    /// <summary>
    /// 注销当前登录状态，并返回登录场景。
    /// </summary>
    private void OnClickBackToLogin()
    {
        SceneFlowService.LogoutToLogin(apiClient);
    }

    /// <summary>
    /// 选择创建角色时使用的职业，并刷新职业文字和预览模型。
    /// classId 必须能在 CharacterDataManager 的配置表中找到对应 CharacterDefine。
    /// </summary>
    private void SelectClass(int classId)
    {
        // 即使配置读取失败，也先记录玩家最后一次点击的职业 ID。
        selectedClassId = classId;

        SetClassHighlight(warriorButton, classId == 1);
        SetClassHighlight(wizardButton, classId == 2);
        SetClassHighlight(archerButton, classId == 3);
        SetClassHighlight(assassinButton, classId == 4);

        CharacterDefine define = CharacterDataManager.Instance.GetCharacter(classId);
        // 找不到配置时不能继续读取名称、介绍或预览模型路径。
        if (define == null)
        {
            return;
        }

        // 文本引用允许为空，这样某些精简界面没有对应 Text 时也不会报错。
        if (classNameText != null)
        {
            classNameText.text = define.name;
        }

        if (classDescriptionText != null)
        {
            classDescriptionText.text = define.description;
        }

        // 根据职业配置中的 previewPrefabPath 加载并显示预览模型。
        previewController?.ShowCharacter(define);
    }


    private void SetClassHighlight(Button button, bool selected)
    {
        if (button == null)
        {
            return;
        }

        Transform highlight = button.transform.Find("HighLight");
        if (highlight != null)
        {
            highlight.gameObject.SetActive(selected);
        }
    }

    /// <summary>
    /// 使用 saves 中的数据刷新全部存档槽位。
    /// 该方法同时负责槽位内容、按钮回调、选中边框和 HighLight 图片。
    /// </summary>
    private void RefreshSaveSlots()
    {
        // 防止 Inspector 配置的槽位数量和代码中的存档数量不一致。
        int slotCount = Mathf.Min(saveSlots.Length, saves.Length);

        for (int i = 0; i < saveSlots.Length; i++)
        {
            // Inspector 数组中可能存在未绑定元素，遇到空引用时直接跳过。
            if (saveSlots[i] == null)
            {
                continue;
            }

            bool isValidSaveIndex = i < slotCount;
            bool isSelected = isValidSaveIndex && i == selectedSlotIndex;

            // 多出来的 UI 槽位没有对应存档数据，必须关闭其所有选中效果。
            if (!isValidSaveIndex)
            {
                saveSlots[i].SetSelected(false);
                SetSlotHighlight(saveSlots[i], false);
                continue;
            }

            NCharacter save = saves[i];
            // 空槽位显示“创建角色”提示；有数据的槽位显示名称、职业和等级。
            // 两种状态都会重新绑定点击回调，并把当前数组下标作为 slotIndex 传出。
            if (save == null)
            {
                saveSlots[i].SetEmpty(i, OnClickSaveSlot);
            }
            else
            {
                saveSlots[i].SetData(i, save, OnClickSaveSlot);
            }

            // 根据 selectedSlotIndex 统一更新选中框和额外 HighLight 图片。
            saveSlots[i].SetSelected(isSelected);
            SetSlotHighlight(saveSlots[i], isSelected);
        }
    }

    /// <summary>
    /// 打开或关闭指定存档槽位中的 HighLight 子物体。
    /// </summary>
    private void SetSlotHighlight(CharacterSaveSlot slot, bool selected)
    {
        Transform highlight = FindSlotHighlight(slot.transform);
        if (highlight != null)
        {
            highlight.gameObject.SetActive(selected);
        }
    }

    /// <summary>
    /// 在存档槽位下查找用于表示选中状态的对象。
    /// 优先寻找直接子物体；若 UI 层级发生变化，再递归搜索所有后代节点。
    /// 搜索时包含未激活对象，否则初始为隐藏状态的 HighLight 无法被找到。
    /// </summary>
    private Transform FindSlotHighlight(Transform slotTransform)
    {
        if (slotTransform == null || string.IsNullOrEmpty(slotHighlightObjectName))
        {
            return null;
        }

        // 常规场景结构下 HighLight 是槽位的直接子物体，直接 Find 开销更小。
        Transform directChild = slotTransform.Find(slotHighlightObjectName);
        if (directChild != null)
        {
            return directChild;
        }

        // 兼容 HighLight 被放入其他容器中的情况。参数 true 表示也搜索未激活节点。
        Transform[] children = slotTransform.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == slotHighlightObjectName)
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// 玩家点击某个存档槽位时调用。
    /// 负责记录选择、刷新高亮，并根据槽位是否有角色更新预览和创建状态。
    /// </summary>
    private void OnClickSaveSlot(int slotIndex)
    {
        // 所有来自 UI 的下标都先做范围检查，防止错误绑定导致数组越界。
        if (slotIndex < 0 || slotIndex >= saves.Length)
        {
            return;
        }

        // 先记录当前槽位，再统一刷新四个槽位的高亮状态。
        selectedSlotIndex = slotIndex;
        RefreshSaveSlots();

        // 空槽位可以作为新角色的创建位置，同时清除已有角色预览。
        if (saves[slotIndex] == null)
        {
            creatingSlotIndex = slotIndex;
            previewController?.ClearCharacter();
            return;
        }

        // 已有角色槽位用于进入游戏，不属于当前的“创建角色目标”，因此清除该状态。
        creatingSlotIndex = -1;

        // 读取已有角色的职业配置并显示对应预览模型。
        CharacterDefine define = CharacterDataManager.Instance.GetCharacter(saves[slotIndex].classId);
        previewController?.ShowCharacter(define);
    }

    /// <summary>
    /// 存档面板中的“创建角色”按钮事件。
    ///
    /// 当前逻辑：
    /// 1. 如果玩家选中了空槽位，就直接在该槽位创建；
    /// 2. 如果没有选中空槽位，就从上到下寻找第一个空槽位；
    /// 3. 四个槽位都已有角色时，显示存档已满的提示。
    ///
    /// 如果以后要支持“严格使用当前选中的槽位，并允许覆盖已有存档”，
    /// 主要修改的就是这个方法，同时服务端数据库写入也必须允许更新已有槽位。
    /// </summary>
    private void OnClickCreateCharacterSlot()
    {
        if (selectedSlotIndex < 0 || selectedSlotIndex >= saves.Length)
        {
            SetMessage("请先选择一个存档位。");
            return;
        }

        // 无论槽位为空还是已有角色，都使用当前选中的位置
        creatingSlotIndex = selectedSlotIndex;
        ShowCreatePanel();
    }

    /// <summary>
    /// 统一显示界面提示，并把非空消息写入 Unity Console，方便调试网络和 UI 流程。
    /// </summary>
    private void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log(message);
        }
    }

    /// <summary>
    /// 设置创建流程相关按钮是否可操作。
    /// 网络请求开始时传 false，请求完成后传 true，防止重复提交或中途切换界面。
    /// </summary>
    private void SetCreateInteractable(bool interactable)
    {
        if (createButton != null)
        {
            createButton.interactable = interactable;
        }

        if (createCharacterButton != null)
        {
            createCharacterButton.interactable = interactable;
        }

        if (backToSaveButton != null)
        {
            backToSaveButton.interactable = interactable;
        }
    }
}
