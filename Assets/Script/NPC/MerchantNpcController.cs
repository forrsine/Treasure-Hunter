using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// Fungi 商人交互体：只检测玩家是否靠近并把 E 键转成领域事件。
/// 钱包、首次对话和购买规则由对应 System 处理，避免场景 NPC 直接修改存档。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class MerchantNpcController : MonoBehaviour, IController
{
    [SerializeField, Min(0.5f)] private float interactionRadius = 3f;

    private readonly HashSet<Collider> overlappingPlayerColliders = new HashSet<Collider>();
    private readonly List<Collider> invalidPlayerColliders = new List<Collider>();
    private bool isNearby;

    public bool IsPlayerNearby => isNearby;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = GetLocalTriggerRadius();

        Rigidbody body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.5f, interactionRadius);
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = GetLocalTriggerRadius();
        }
    }

    private void Update()
    {
        // OnTriggerExit 在 Collider 被禁用、销毁或角色瞬移时可能不触发，主动清理可避免“按 E”提示残留。
        RefreshNearbyState();
        if (!isNearby || Time.timeScale <= 0f)
        {
            return;
        }

        GameSessionUi sessionUi = GameSessionUi.Instance;
        if (sessionUi != null && sessionUi.IsGameplayInputBlocked)
        {
            return;
        }

        IGameplayInput input = GameplayRuntime.Instance.CurrentInput;
        if (input == null || !input.InteractDown)
        {
            return;
        }

        bool introCompleted = this.SendQuery(new IsMerchantIntroCompletedQuery());
        if (!introCompleted && this.SendCommand(new CompleteMerchantIntroCommand()))
        {
            GetArchitecture().SendEvent(new MerchantDialogueRequestedEvent());
            return;
        }

        GetArchitecture().SendEvent(new ShopOpenRequestedEvent());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            overlappingPlayerColliders.Add(other);
            RefreshNearbyState();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayerCollider(other))
        {
            overlappingPlayerColliders.Add(other);
            RefreshNearbyState();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null)
        {
            overlappingPlayerColliders.Remove(other);
            RefreshNearbyState();
        }
    }

    private void OnDisable()
    {
        overlappingPlayerColliders.Clear();
        SetNearby(false);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        return other != null && other.GetComponentInParent<PlayerRuntimeController>() != null;
    }

    private void RefreshNearbyState()
    {
        invalidPlayerColliders.Clear();
        foreach (Collider playerCollider in overlappingPlayerColliders)
        {
            if (IsInvalidOrOutsideInteractionRange(playerCollider))
            {
                invalidPlayerColliders.Add(playerCollider);
            }
        }

        for (int i = 0; i < invalidPlayerColliders.Count; i++)
        {
            overlappingPlayerColliders.Remove(invalidPlayerColliders[i]);
        }

        SetNearby(overlappingPlayerColliders.Count > 0);
    }

    private bool IsInvalidOrOutsideInteractionRange(Collider playerCollider)
    {
        if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy ||
            playerCollider.GetComponentInParent<PlayerRuntimeController>() == null)
        {
            return true;
        }

        // 使用 Collider 最近点而不是角色中心，兼容角色拥有多个或尺寸不同的碰撞体。
        Vector3 closestPoint = playerCollider.ClosestPoint(transform.position);
        float radius = Mathf.Max(0.5f, interactionRadius);
        return (closestPoint - transform.position).sqrMagnitude > radius * radius;
    }

    private void SetNearby(bool nearby)
    {
        if (isNearby == nearby)
        {
            return;
        }

        isNearby = nearby;
        GetArchitecture().SendEvent(new MerchantProximityChangedEvent(nearby));
    }

    /// <summary>
    /// MainScene 里沿用原 Fungi 的 1.5 倍外观缩放，因此把世界 3 米换算为本地半径，
    /// 避免 Collider 随 Transform 再放大一次变成 4.5 米。
    /// </summary>
    private float GetLocalTriggerRadius()
    {
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(0.5f, interactionRadius) / Mathf.Max(0.0001f, maxScale);
    }
}
