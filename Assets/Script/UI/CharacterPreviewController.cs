using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 角色预览控制器：根据职业配置加载预览模型，并允许玩家拖动鼠标旋转观察。
/// 这里只负责选角界面的模型表现，不参与游戏场景角色生成。
/// </summary>
public class CharacterPreviewController : MonoBehaviour, IDragHandler
{
    [SerializeField] private Transform modelRoot;
    [SerializeField] private float rotateSpeed = 0.5f;

    private GameObject currentModel;

    /// <summary>
    /// 按职业配置加载预览模型。
    /// 选角界面切换职业时会先销毁旧模型，再实例化新的预览模型。
    /// </summary>
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

    /// <summary>
    /// 拖动鼠标时旋转预览根节点，让玩家可以从不同角度观察角色外观。
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        // 使用拖动增量而不是绝对鼠标位置，使不同分辨率下旋转手感保持一致。
        if (modelRoot == null)
        {
            return;
        }

        float rotateY = -eventData.delta.x * rotateSpeed;
        modelRoot.Rotate(0f, rotateY, 0f, Space.World);
    }

    /// <summary>
    /// 清空当前预览模型。
    /// 当没有选中有效角色，或者切回空槽位时会调用这里。
    /// </summary>
    public void ClearCharacter()
    {
        if (currentModel != null)
        {
            Destroy(currentModel);
            currentModel = null;
        }
    }

}
