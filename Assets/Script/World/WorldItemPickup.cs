using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// 地面物品拾取体：负责悬浮旋转、45 秒生命周期、玩家触发拾取与对象池状态重置。
/// 背包是否能接收以及如何堆叠仍由 InventorySystem 决定。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class WorldItemPickup : MonoBehaviour, IController
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Transform visualRoot;
    [SerializeField] private Renderer[] tintedRenderers;
    [SerializeField] private Light[] tintedLights;
    [SerializeField] private bool createFallbackSphereVisual;
    [SerializeField] private Vector3 fallbackVisualLocalOffset = new Vector3(0f, 0.75f, 0f);
    [SerializeField, Min(0.1f)] private float fallbackSphereScale = 0.55f;
    [SerializeField, Min(0f)] private float emissionIntensity = 2.2f;
    [SerializeField, Min(0f)] private float pointLightIntensity = 2.8f;
    [SerializeField, Min(0f)] private float pointLightRange = 4.5f;
    [SerializeField, Min(0f)] private float rotationSpeed = 80f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float bobFrequency = 2.2f;
    [SerializeField, Min(0.05f)] private float retryInterval = 0.3f;

    private readonly HashSet<int> overlappingPlayerColliders = new HashSet<int>();
    private MaterialPropertyBlock propertyBlock;

    private WorldLootPool ownerPool;
    private GameObject sourcePrefab;
    private InventoryItemDefinition item;
    private int amount;
    private float remainingLifetime;
    private float nextRetryTime;
    private float bobPhase;
    private Vector3 visualBaseLocalPosition;
    private bool isConfigured;
    private bool fullWarningSent;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        // UnityEngine.Object 派生资源不能在 MonoBehaviour 字段初始化器中创建，
        // 否则 Prefab 反序列化会报构造阶段异常。
        propertyBlock = new MaterialPropertyBlock();
        EnsurePhysicsSetup();
        CreateFallbackSphereVisualIfNeeded();
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        if (tintedRenderers == null || tintedRenderers.Length == 0)
        {
            tintedRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (tintedLights == null || tintedLights.Length == 0)
        {
            tintedLights = GetComponentsInChildren<Light>(true);
        }

        visualBaseLocalPosition = visualRoot.localPosition;
    }

    private void Update()
    {
        if (!isConfigured)
        {
            return;
        }

        // 使用缩放时间：打开背包或暂停菜单时，掉落物寿命和表现一起暂停。
        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            ReleaseToPool();
            return;
        }

        bobPhase += Time.deltaTime * bobFrequency;
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition +
                Vector3.up * (Mathf.Sin(bobPhase) * bobAmplitude);
            visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
        }

        if (overlappingPlayerColliders.Count > 0 && Time.time >= nextRetryTime)
        {
            nextRetryTime = Time.time + retryInterval;
            if (this.GetSystem<InventorySystem>().GetAddableAmount(item, amount) > 0)
            {
                TryPickup(false);
            }
        }
    }

    public void Configure(
        WorldLootPool pool,
        GameObject prefab,
        InventoryItemDefinition newItem,
        int newAmount,
        float lifetimeSeconds)
    {
        ownerPool = pool;
        sourcePrefab = prefab;
        item = newItem;
        amount = Mathf.Max(1, newAmount);
        remainingLifetime = Mathf.Max(1f, lifetimeSeconds);
        nextRetryTime = 0f;
        bobPhase = Random.value * Mathf.PI * 2f;
        overlappingPlayerColliders.Clear();
        fullWarningSent = false;
        isConfigured = item != null;

        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localRotation = Quaternion.identity;
        }

        ApplyDisplayTint(item != null ? item.DisplayTint : Color.white);
    }

    public void PrepareRecycle()
    {
        isConfigured = false;
        item = null;
        amount = 0;
        remainingLifetime = 0f;
        overlappingPlayerColliders.Clear();
        fullWarningSent = false;
        if (visualRoot != null)
        {
            visualRoot.localPosition = visualBaseLocalPosition;
            visualRoot.localRotation = Quaternion.identity;
        }

        ApplyDisplayTint(Color.white);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryRegisterPlayerCollider(other))
        {
            return;
        }

        TryPickup(true);
    }

    private void OnTriggerStay(Collider other)
    {
        TryRegisterPlayerCollider(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            overlappingPlayerColliders.Remove(other.GetInstanceID());
        }
    }

    private bool TryRegisterPlayerCollider(Collider other)
    {
        if (!isConfigured || other == null ||
            other.GetComponentInParent<PlayerRuntimeController>() == null)
        {
            return false;
        }

        overlappingPlayerColliders.Add(other.GetInstanceID());
        return true;
    }

    private void TryPickup(bool notifyWhenFull)
    {
        if (!isConfigured || item == null || amount <= 0)
        {
            return;
        }

        InventorySystem inventorySystem = this.GetSystem<InventorySystem>();
        if (inventorySystem.GetAddableAmount(item, amount) <= 0)
        {
            if (notifyWhenFull && !fullWarningSent)
            {
                fullWarningSent = true;
                this.SendCommand(new AddInventoryItemCommand(item, amount));
            }

            return;
        }

        InventoryAddResult result = this.SendCommand(new AddInventoryItemCommand(item, amount));
        amount = result.RemainingAmount;
        if (amount <= 0)
        {
            ReleaseToPool();
        }
        else
        {
            fullWarningSent = true;
        }
    }

    private void ReleaseToPool()
    {
        if (!isConfigured)
        {
            return;
        }

        isConfigured = false;
        if (ownerPool != null)
        {
            ownerPool.Release(sourcePrefab, this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void EnsurePhysicsSetup()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void ApplyDisplayTint(Color tint)
    {
        if (tintedRenderers == null || propertyBlock == null)
        {
            return;
        }

        for (int i = 0; i < tintedRenderers.Length; i++)
        {
            Renderer targetRenderer = tintedRenderers[i];
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            if (targetRenderer.sharedMaterial.HasProperty(BaseColorProperty))
            {
                propertyBlock.SetColor(BaseColorProperty, tint);
            }

            if (targetRenderer.sharedMaterial.HasProperty(ColorProperty))
            {
                propertyBlock.SetColor(ColorProperty, tint);
            }

            if (targetRenderer.sharedMaterial.HasProperty(EmissionColorProperty))
            {
                propertyBlock.SetColor(EmissionColorProperty, tint * emissionIntensity);
            }

            targetRenderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        if (tintedLights == null)
        {
            return;
        }

        for (int i = 0; i < tintedLights.Length; i++)
        {
            Light light = tintedLights[i];
            if (light == null)
            {
                continue;
            }

            light.color = tint;
            light.intensity = pointLightIntensity;
            light.range = pointLightRange;
        }
    }

    /// <summary>
    /// Boss 掉落物使用同一套拾取逻辑，但视觉上是发光球。
    /// 如果 Prefab 没有手动放模型，这里运行时生成一个小球和点光源，避免再复制一套 Boss 专用拾取脚本。
    /// </summary>
    private void CreateFallbackSphereVisualIfNeeded()
    {
        if (!createFallbackSphereVisual || visualRoot != null)
        {
            return;
        }

        GameObject visualRootObject = new GameObject("VisualRoot");
        visualRootObject.transform.SetParent(transform, false);
        visualRootObject.transform.localPosition = fallbackVisualLocalOffset;
        visualRoot = visualRootObject.transform;

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "OrbVisual";
        sphere.transform.SetParent(visualRoot, false);
        sphere.transform.localPosition = Vector3.zero;
        sphere.transform.localRotation = Quaternion.identity;
        sphere.transform.localScale = Vector3.one * fallbackSphereScale;

        Collider visualCollider = sphere.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.EnableKeyword("_EMISSION");
            material.SetColor(EmissionColorProperty, Color.white * emissionIntensity);
            renderer.material = material;
            tintedRenderers = new[] { renderer };
        }

        Light light = visualRootObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.intensity = pointLightIntensity;
        light.range = pointLightRange;
        tintedLights = new[] { light };
    }
}
