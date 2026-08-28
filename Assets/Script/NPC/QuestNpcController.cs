using System.Collections.Generic;
using QFramework;
using UnityEngine;

/// <summary>
/// Mushroom 任务 NPC：只处理玩家接近、E 键交互和头顶任务标记。
/// 接取、计数、奖励与存档规则全部由 QuestSystem 负责，NPC 不直接改数据。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public sealed class QuestNpcController : MonoBehaviour, IController
{
    [SerializeField, Min(0.5f)] private float interactionRadius = 3f;
    [SerializeField] private GameObject questMarkerRoot;

    private readonly HashSet<Collider> overlappingPlayerColliders = new HashSet<Collider>();
    private readonly List<Collider> invalidPlayerColliders = new List<Collider>();
    private bool isNearby;

    public bool IsPlayerNearby => isNearby;
    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        ConfigurePhysics();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.RegisterEvent<QuestAcceptedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestProgressChangedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestRewardClaimedEvent>(HandleQuestChanged);
        this.RegisterEvent<QuestProgressRestoredEvent>(HandleQuestRestored);
        RefreshQuestMarker();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        this.UnRegisterEvent<QuestAcceptedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestProgressChangedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestRewardClaimedEvent>(HandleQuestChanged);
        this.UnRegisterEvent<QuestProgressRestoredEvent>(HandleQuestRestored);
        overlappingPlayerColliders.Clear();
        SetNearby(false);
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.5f, interactionRadius);
        ConfigurePhysics();
    }

    private void Update()
    {
        // 不能只依赖 OnTriggerExit：角色 Collider 被禁用、销毁或瞬移时，Unity 可能不会补发退出事件。
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
        if (input != null && input.InteractDown)
        {
            GetArchitecture().SendEvent(new QuestPanelOpenRequestedEvent());
        }
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

    private void ConfigurePhysics()
    {
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
            trigger.radius = GetLocalTriggerRadius();
        }

        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        }
    }

    private float GetLocalTriggerRadius()
    {
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return interactionRadius / Mathf.Max(0.0001f, maxScale);
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
        GetArchitecture().SendEvent(new QuestNpcProximityChangedEvent(nearby));
    }

    private void HandleQuestChanged(QuestAcceptedEvent _) => RefreshQuestMarker();
    private void HandleQuestChanged(QuestProgressChangedEvent _) => RefreshQuestMarker();
    private void HandleQuestChanged(QuestRewardClaimedEvent _) => RefreshQuestMarker();
    private void HandleQuestRestored(QuestProgressRestoredEvent _) => RefreshQuestMarker();

    private void RefreshQuestMarker()
    {
        if (questMarkerRoot != null)
        {
            questMarkerRoot.SetActive(!this.SendQuery(new AreAllQuestsClaimedQuery()));
        }
    }
}
