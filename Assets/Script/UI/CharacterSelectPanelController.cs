using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPanelController : MonoBehaviour
{
    [Header("Services")]
    [SerializeField] private GameApiClient apiClient;

    [Header("Panels")]
    [SerializeField] private GameObject savePanel;
    [SerializeField] private GameObject createCharacterPanel;

    [Header("Save Panel Buttons")]
    [SerializeField] private Button createCharacterButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button backToLoginButton;

    [Header("Create Panel Buttons")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button backToSaveButton;

    [Header("Class Buttons")]
    [SerializeField] private Button warriorButton;
    [SerializeField] private Button wizardButton;
    [SerializeField] private Button archerButton;
    [SerializeField] private Button assassinButton;

    [Header("Class Info")]
    [SerializeField] private Text classNameText;
    [SerializeField] private Text classDescriptionText;
    [SerializeField] private Text messageText;

    [Header("Preview")]
    [SerializeField] private CharacterPreviewController previewController;

    [Header("Create Character")]
    [SerializeField] private InputField characterNameInput;

    [Header("Save Slots")]
    [SerializeField] private CharacterSaveSlot[] saveSlots;
    [SerializeField] private string slotHighlightObjectName = "HighLight";

    private NCharacter[] saves = new NCharacter[4];
    private int selectedClassId = 1;
    private int selectedSlotIndex = -1;
    private int creatingSlotIndex = -1;

    private void Awake()
    {
        EnsureApiClient();

        createCharacterButton.onClick.AddListener(OnClickCreateCharacterSlot);
        startGameButton.onClick.AddListener(OnClickStartGame);
        backToLoginButton.onClick.AddListener(OnClickBackToLogin);
        createButton.onClick.AddListener(OnClickCreateCharacter);
        backToSaveButton.onClick.AddListener(ShowSavePanel);

        warriorButton.onClick.AddListener(() => SelectClass(1));
        wizardButton.onClick.AddListener(() => SelectClass(2));
        archerButton.onClick.AddListener(() => SelectClass(3));
        assassinButton.onClick.AddListener(() => SelectClass(4));

        ShowSavePanel();
        RefreshSaveSlots();
    }

    private void Start()
    {
        StartCoroutine(LoadCharactersFromServer());
    }

    private void EnsureApiClient()
    {
        if (apiClient == null)
        {
            apiClient = SceneFlowService.GetOrCreateApiClient();
        }
    }

    private void ShowSavePanel()
    {
        savePanel.SetActive(true);
        createCharacterPanel.SetActive(false);

        if (selectedSlotIndex >= 0 && saves[selectedSlotIndex] != null)
        {
            CharacterDefine define = CharacterDataManager.Instance.GetCharacter(saves[selectedSlotIndex].classId);
            previewController?.ShowCharacter(define);
            return;
        }

        previewController?.ClearCharacter();
    }

    private void ShowCreatePanel()
    {
        savePanel.SetActive(false);
        createCharacterPanel.SetActive(true);
        SelectClass(selectedClassId);
    }

    private void OnClickStartGame()
    {
        if (selectedSlotIndex < 0 || saves[selectedSlotIndex] == null)
        {
            SetMessage("Select a character save first.");
            return;
        }

        NCharacter save = saves[selectedSlotIndex];
        SceneFlowService.StartGameplay(save);
    }

    private void OnClickCreateCharacter()
    {
        if (creatingSlotIndex < 0)
        {
            SetMessage("Select an empty save slot first.");
            return;
        }

        string characterName = characterNameInput != null ? characterNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(characterName))
        {
            SetMessage("Enter a character name.");
            return;
        }

        StartCoroutine(CreateCharacterOnServer(creatingSlotIndex, characterName, selectedClassId));
    }

    private IEnumerator LoadCharactersFromServer()
    {
        if (apiClient == null)
        {
            SetMessage("ApiClient is missing.");
            yield break;
        }

        SetMessage("Loading character saves...");

        yield return apiClient.GetCharacters((success, message, characters) =>
        {
            if (!success)
            {
                SetMessage(string.IsNullOrEmpty(message) ? "Failed to load character saves." : message);
                RefreshSaveSlots();
                ShowSavePanel();
                return;
            }

            ApplyServerCharacters(characters);
            SetMessage(string.IsNullOrEmpty(message) ? "Character saves loaded." : message);
            RefreshSaveSlots();
            ShowSavePanel();
        });
    }

    private IEnumerator CreateCharacterOnServer(int slotIndex, string characterName, int classId)
    {
        if (apiClient == null)
        {
            SetMessage("ApiClient is missing.");
            yield break;
        }

        SetMessage("Creating character...");
        SetCreateInteractable(false);

        yield return apiClient.CreateCharacter(slotIndex, characterName, classId, (success, message, createdCharacter) =>
        {
            SetCreateInteractable(true);

            if (!success || createdCharacter == null)
            {
                SetMessage(string.IsNullOrEmpty(message) ? "Failed to create character." : message);
                return;
            }

            if (createdCharacter.slotIndex >= 0 && createdCharacter.slotIndex < saves.Length)
            {
                saves[createdCharacter.slotIndex] = createdCharacter;
                selectedSlotIndex = createdCharacter.slotIndex;
            }

            creatingSlotIndex = -1;

            if (characterNameInput != null)
            {
                characterNameInput.text = "";
            }

            SetMessage(string.IsNullOrEmpty(message) ? "Character created." : message);
            RefreshSaveSlots();
            ShowSavePanel();
        });
    }

    private void ApplyServerCharacters(NCharacter[] characters)
    {
        saves = new NCharacter[4];

        if (characters != null)
        {
            foreach (NCharacter character in characters)
            {
                if (character == null)
                {
                    continue;
                }

                if (character.slotIndex < 0 || character.slotIndex >= saves.Length)
                {
                    continue;
                }

                saves[character.slotIndex] = character;
            }
        }

        if (selectedSlotIndex >= 0 && selectedSlotIndex < saves.Length && saves[selectedSlotIndex] != null)
        {
            return;
        }

        selectedSlotIndex = -1;
        creatingSlotIndex = -1;
    }

    private void OnClickBackToLogin()
    {
        SceneFlowService.LogoutToLogin(apiClient);
    }

    private void SelectClass(int classId)
    {
        selectedClassId = classId;

        CharacterDefine define = CharacterDataManager.Instance.GetCharacter(classId);
        if (define == null)
        {
            return;
        }

        if (classNameText != null)
        {
            classNameText.text = define.name;
        }

        if (classDescriptionText != null)
        {
            classDescriptionText.text = define.description;
        }

        previewController?.ShowCharacter(define);
    }

    private void RefreshSaveSlots()
    {
        int slotCount = Mathf.Min(saveSlots.Length, saves.Length);

        for (int i = 0; i < saveSlots.Length; i++)
        {
            if (saveSlots[i] == null)
            {
                continue;
            }

            bool isValidSaveIndex = i < slotCount;
            bool isSelected = isValidSaveIndex && i == selectedSlotIndex;

            if (!isValidSaveIndex)
            {
                saveSlots[i].SetSelected(false);
                SetSlotHighlight(saveSlots[i], false);
                continue;
            }

            NCharacter save = saves[i];
            if (save == null)
            {
                saveSlots[i].SetEmpty(i, OnClickSaveSlot);
            }
            else
            {
                saveSlots[i].SetData(i, save, OnClickSaveSlot);
            }

            saveSlots[i].SetSelected(isSelected);
            SetSlotHighlight(saveSlots[i], isSelected);
        }
    }

    private void SetSlotHighlight(CharacterSaveSlot slot, bool selected)
    {
        Transform highlight = FindSlotHighlight(slot.transform);
        if (highlight != null)
        {
            highlight.gameObject.SetActive(selected);
        }
    }

    private Transform FindSlotHighlight(Transform slotTransform)
    {
        if (slotTransform == null || string.IsNullOrEmpty(slotHighlightObjectName))
        {
            return null;
        }

        Transform directChild = slotTransform.Find(slotHighlightObjectName);
        if (directChild != null)
        {
            return directChild;
        }

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

    private void OnClickSaveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= saves.Length)
        {
            return;
        }

        selectedSlotIndex = slotIndex;
        RefreshSaveSlots();

        if (saves[slotIndex] == null)
        {
            creatingSlotIndex = slotIndex;
            previewController?.ClearCharacter();
            return;
        }

        creatingSlotIndex = -1;
        CharacterDefine define = CharacterDataManager.Instance.GetCharacter(saves[slotIndex].classId);
        previewController?.ShowCharacter(define);
    }

    private void OnClickCreateCharacterSlot()
    {
        if (selectedSlotIndex >= 0 && saves[selectedSlotIndex] == null)
        {
            creatingSlotIndex = selectedSlotIndex;
            ShowCreatePanel();
            return;
        }

        for (int i = 0; i < saves.Length; i++)
        {
            if (saves[i] == null)
            {
                creatingSlotIndex = i;
                selectedSlotIndex = i;
                RefreshSaveSlots();
                ShowCreatePanel();
                return;
            }
        }

        SetMessage("All save slots are full.");
    }

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
