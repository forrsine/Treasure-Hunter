using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterPreviewController : MonoBehaviour, IDragHandler
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float rotateSpeed = 0.5f;

    private GameObject currentModel;

    public void ShowCharacter(CharacterDefine define)
    {
        if (define == null)
        {
            return;
        }

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        GameObject prefab = Resources.Load<GameObject>(define.previewPrefabPath);

        if (prefab == null)
        {
            Debug.LogError($"没有找到角色预览模型：Resources/{define.previewPrefabPath}");
            return;
        }

        currentModel = Instantiate(prefab, modelRoot);
        currentModel.transform.localPosition = Vector3.zero;
        currentModel.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
       

        modelRoot.localRotation = Quaternion.identity;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (modelRoot == null)
        {
            return;
        }

        float rotateY = -eventData.delta.x * rotateSpeed;
        modelRoot.Rotate(0f, rotateY, 0f, Space.World);
    }

    public void ClearCharacter()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
    }

}