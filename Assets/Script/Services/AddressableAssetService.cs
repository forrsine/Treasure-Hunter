using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Addressables 资源服务：统一负责异步加载 Prefab 和释放加载句柄。
/// 第一阶段只封装技能特效需要的最小能力，后续可在这里扩展缓存、下载进度和远程资源更新。
/// </summary>
[DisallowMultipleComponent]
public sealed class AddressableAssetService : MonoBehaviour
{
    private static AddressableAssetService instance;

    // 延迟释放的句柄需要被记录，确保退出游戏时协程来不及执行也不会遗留引用计数。
    private readonly List<AsyncOperationHandle<GameObject>> pendingReleaseHandles =
        new List<AsyncOperationHandle<GameObject>>();

    public static AddressableAssetService GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<AddressableAssetService>();
        if (instance != null)
        {
            return instance;
        }

        GameObject serviceObject = new GameObject(nameof(AddressableAssetService));
        instance = serviceObject.AddComponent<AddressableAssetService>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 发起 Prefab 异步加载，并把句柄交给调用方持有。
    /// Addressables 使用引用计数，因此谁持有句柄，谁就必须在不再使用资源时释放它。
    /// </summary>
    public AsyncOperationHandle<GameObject> LoadPrefabAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            Debug.LogError("Addressables 加载失败：资源地址不能为空。");
            return default;
        }

        return Addressables.LoadAssetAsync<GameObject>(address);
    }

    /// <summary>
    /// 从已完成的句柄读取 Prefab，并集中输出可定位的失败日志。
    /// </summary>
    public bool TryGetLoadedPrefab(
        AsyncOperationHandle<GameObject> handle,
        string address,
        out GameObject prefab)
    {
        prefab = null;
        if (!handle.IsValid())
        {
            Debug.LogError($"Addressables 加载失败：{address} 的句柄无效。");
            return false;
        }

        if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
        {
            prefab = handle.Result;
            return true;
        }

        string reason = handle.OperationException != null
            ? handle.OperationException.Message
            : "未返回 Prefab 资源";
        Debug.LogError($"Addressables 加载失败：{address}，原因：{reason}");
        return false;
    }

    /// <summary>
    /// 下一帧释放 Prefab 句柄。
    /// 调用方会先 Destroy 对象池中的实例，而 Unity 的 Destroy 在帧末生效，因此延后一帧可保证实例先完成清理。
    /// </summary>
    public void ReleasePrefabAfterInstanceCleanup(AsyncOperationHandle<GameObject> handle)
    {
        if (!handle.IsValid())
        {
            return;
        }

        pendingReleaseHandles.Add(handle);
        StartCoroutine(ReleaseNextFrame(handle));
    }

    private IEnumerator ReleaseNextFrame(AsyncOperationHandle<GameObject> handle)
    {
        yield return null;

        if (handle.IsValid())
        {
            Addressables.Release(handle);
        }

        pendingReleaseHandles.Remove(handle);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < pendingReleaseHandles.Count; i++)
        {
            AsyncOperationHandle<GameObject> handle = pendingReleaseHandles[i];
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        pendingReleaseHandles.Clear();

        if (instance == this)
        {
            instance = null;
        }
    }
}
