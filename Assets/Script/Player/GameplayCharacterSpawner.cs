using System.Collections.Generic;
using UnityEngine;

public class GameplayCharacterSpawner : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private CameraCo gameplayCamera;
    [SerializeField] private int fallbackClassId = 1;

    private readonly Dictionary<long, GameObject> spawnedCharacters = new Dictionary<long, GameObject>();

    public GameObject CurrentPlayer { get; private set; }

    private void OnEnable()
    {
        GameplayCharacterManager.Instance.CharacterEntered += OnCharacterEnter;
        GameplayCharacterManager.Instance.CharacterLeft += OnCharacterLeave;
    }

    private void OnDisable()
    {
        GameplayCharacterManager.Instance.CharacterEntered -= OnCharacterEnter;
        GameplayCharacterManager.Instance.CharacterLeft -= OnCharacterLeave;
    }

    private void Start()
    {
        EnterSelectedCharacter();
    }

    private void EnterSelectedCharacter()
    {
        Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        GameplayCharacterManager.Instance.EnterCurrentCharacter(
            SelectedCharacterState.CurrentCharacter,
            position,
            rotation,
            fallbackClassId);
    }

    private void OnCharacterEnter(GameplayCharacter character)
    {
        CreateCharacterObject(character);
    }

    private void OnCharacterLeave(GameplayCharacter character)
    {
        if (character == null)
        {
            return;
        }

        if (!spawnedCharacters.TryGetValue(character.EntityId, out GameObject characterObject))
        {
            return;
        }

        if (characterObject != null)
        {
            Destroy(characterObject);
        }

        spawnedCharacters.Remove(character.EntityId);

        if (CurrentPlayer == characterObject)
        {
            CurrentPlayer = null;
        }
    }

    private void CreateCharacterObject(GameplayCharacter character)
    {
        if (character == null || character.Define == null)
        {
            return;
        }

        if (!spawnedCharacters.TryGetValue(character.EntityId, out GameObject characterObject) || characterObject == null)
        {
            GameObject prefab = Resources.Load<GameObject>(character.Define.gamePrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"\u6ca1\u6709\u627e\u5230\u73a9\u5bb6\u6a21\u578b\uff1aResources/{character.Define.gamePrefabPath}");
                return;
            }

            characterObject = Instantiate(prefab, transform);
            characterObject.name = $"Character_{character.EntityId}_{character.Name}";
            spawnedCharacters[character.EntityId] = characterObject;
        }

        InitCharacterObject(characterObject, character);
    }

    private void InitCharacterObject(GameObject characterObject, GameplayCharacter character)
    {
        characterObject.transform.SetPositionAndRotation(character.Position, character.Rotation);

        PlayerCo player = characterObject.GetComponent<PlayerCo>();
        if (player != null)
        {
            player.ApplyCharacterEntryData(character.Save, character.Define);
        }

        if (character.IsCurrentPlayer)
        {
            CurrentPlayer = characterObject;

            if (gameplayCamera != null)
            {
                gameplayCamera.target = characterObject.transform;
            }
        }

        Debug.Log($"\u5df2\u751f\u6210\u89d2\u8272\uff1a{character.Name}");
    }
}
