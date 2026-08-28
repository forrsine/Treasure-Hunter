using QFramework;
using UnityEngine;

/// <summary>
/// 重要金币地面拾取物：负责悬浮表现、触发收取、生命周期和对象池状态重置。
/// 金币不进入背包，因此不会被背包容量阻挡。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class WorldGoldPickup : MonoBehaviour, IController
{
    [SerializeField] private Transform visualRoot;
    [SerializeField, Min(0f)] private float rotationSpeed = 110f;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.15f;
    [SerializeField, Min(0f)] private float bobFrequency = 2.5f;

    private WorldGoldPool ownerPool;
    private GameObject sourcePrefab;
    private long amount;
    private float remainingLifetime;
    private float bobPhase;
    private Vector3 visualBaseLocalPosition;
    private bool isConfigured;

    public long Amount => amount;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;

        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        visualBaseLocalPosition = visualRoot.localPosition;
    }

    private void Update()
    {
        if (!isConfigured)
        {
            return;
        }

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
        {
            ReleaseToPool();
            return;
        }

        bobPhase += Time.deltaTime * bobFrequency;
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * (Mathf.Sin(bobPhase) * bobAmplitude);
        visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    public void Configure(WorldGoldPool pool, GameObject prefab, long goldAmount, float lifetimeSeconds)
    {
        ownerPool = pool;
        sourcePrefab = prefab;
        amount = System.Math.Max(1L, goldAmount);
        remainingLifetime = Mathf.Max(1f, lifetimeSeconds);
        bobPhase = Random.value * Mathf.PI * 2f;
        isConfigured = true;
        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localRotation = Quaternion.identity;
    }

    public void PrepareRecycle()
    {
        isConfigured = false;
        amount = 0L;
        remainingLifetime = 0f;
        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.localRotation = Quaternion.identity;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isConfigured || other == null || other.GetComponentInParent<PlayerRuntimeController>() == null)
        {
            return;
        }

        long added = this.SendCommand(new AddGoldCommand(amount));
        if (added > 0L)
        {
            GameAudioService.Play2D(GameSfxId.GoldPickup);
            ReleaseToPool();
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
}
