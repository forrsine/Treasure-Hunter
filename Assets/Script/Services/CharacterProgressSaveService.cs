using QFramework;
using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 角色进度保存意图。数值顺序同时代表合并优先级，保证死亡重置不会被普通保存覆盖。
/// </summary>
public enum CharacterProgressSaveMode
{
    Normal = 0,
    ClearRunUpgrades = 1,
    ResetAfterDeath = 2
}

/// <summary>
/// 角色成长自动存档协调器：监听领域事件、合并频繁保存，并保证存档请求按顺序执行。
/// 它不计算成长数值，属性数据来自 PlayerModel，关卡数据来自 BossRunProgressState。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterProgressSaveService : MonoBehaviour, IController
{
    private const float SaveDebounceSeconds = 1f;

    public static CharacterProgressSaveService Instance { get; private set; }
    public bool IsSessionActive => sessionActive;

    private GameApiClient apiClient;
    private NCharacter currentCharacter;
    private Coroutine saveRoutine;
    private bool sessionActive;
    private bool dirty;
    private CharacterProgressSaveMode requestedSaveMode;
    private float nextSaveRealtime;
    private int changeVersion;
    private int savedVersion;
    private string lastError = "";
    private bool isApplyingAuthoritativeInventory;

    public IArchitecture GetArchitecture() => TreasureHunterArchitecture.Interface;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        apiClient = GetComponent<GameApiClient>();
    }

    private void OnEnable()
    {
        this.RegisterEvent<PlayerExperienceGainedEvent>(HandleExperienceGained);
        this.RegisterEvent<PlayerAttributeUpgradedEvent>(HandleAttributeUpgraded);
        this.RegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
        this.RegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        BossRunProgressState.PersistentProgressChanged += HandlePersistentProgressChanged;
    }

    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerExperienceGainedEvent>(HandleExperienceGained);
        this.UnRegisterEvent<PlayerAttributeUpgradedEvent>(HandleAttributeUpgraded);
        this.UnRegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
        this.UnRegisterEvent<InventoryChangedEvent>(HandleInventoryChanged);
        BossRunProgressState.PersistentProgressChanged -= HandlePersistentProgressChanged;

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>角色成功通过当前数据源校验后，开始本次可保存会话。</summary>
    public void BeginSession(NCharacter character)
    {
        currentCharacter = character != null ? character.Clone() : null;
        sessionActive = currentCharacter != null && currentCharacter.id > 0;
        dirty = false;
        requestedSaveMode = CharacterProgressSaveMode.Normal;
        changeVersion = 0;
        savedVersion = 0;
        lastError = "";
        isApplyingAuthoritativeInventory = false;
    }

    public void EndSession()
    {
        sessionActive = false;
        currentCharacter = null;
        dirty = false;
        requestedSaveMode = CharacterProgressSaveMode.Normal;
        lastError = "";
        isApplyingAuthoritativeInventory = false;

        if (saveRoutine != null)
        {
            StopCoroutine(saveRoutine);
            saveRoutine = null;
        }
    }

    /// <summary>请求一次防抖自动保存。连续经验或多个进度事件会合并成一个网络请求。</summary>
    public void RequestAutoSave()
    {
        RequestSave(false, CharacterProgressSaveMode.Normal);
    }

    /// <summary>
    /// 强制把最新数据保存完成。保存模式由调用场景明确传入，避免布尔值无法表达死亡全重置。
    /// </summary>
    public IEnumerator FlushNow(
        CharacterProgressSaveMode saveMode,
        Action<bool, string, NCharacter> onDone)
    {
        if (!sessionActive)
        {
            onDone?.Invoke(false, "当前没有已进入的角色，无法保存。", null);
            yield break;
        }

        RequestSave(true, saveMode);
        int targetVersion = changeVersion;

        while (saveRoutine != null)
        {
            yield return null;
        }

        bool success = !dirty && savedVersion >= targetVersion;
        onDone?.Invoke(
            success,
            success ? "角色进度已保存。" : string.IsNullOrEmpty(lastError) ? "角色进度保存失败。" : lastError,
            success && currentCharacter != null ? currentCharacter.Clone() : null);
    }

    /// <summary>保存后结束当前角色会话；任一步失败都不清理本地会话。</summary>
    public IEnumerator FlushAndLeave(
        CharacterProgressSaveMode saveMode,
        Action<bool, string> onDone)
    {
        bool saveSuccess = false;
        string message = "";
        yield return FlushNow(saveMode, (success, resultMessage, _) =>
        {
            saveSuccess = success;
            message = resultMessage;
        });

        if (!saveSuccess)
        {
            onDone?.Invoke(false, message);
            yield break;
        }

        bool leaveSuccess = false;
        yield return apiClient.LeaveCharacter((success, resultMessage) =>
        {
            leaveSuccess = success;
            message = resultMessage;
        });

        if (leaveSuccess)
        {
            EndSession();
        }

        onDone?.Invoke(leaveSuccess, message);
    }

    private void HandleExperienceGained(PlayerExperienceGainedEvent _)
    {
        RequestAutoSave();
    }

    private void HandleAttributeUpgraded(PlayerAttributeUpgradedEvent _)
    {
        RequestAutoSave();
    }

    private void HandlePersistentProgressChanged()
    {
        RequestAutoSave();
    }

    private void HandleInventoryChanged(InventoryChangedEvent _)
    {
        // 服务器确认后的批量恢复也会刷新 UI，但它不代表玩家又修改了一次背包。
        if (!isApplyingAuthoritativeInventory)
        {
            RequestAutoSave();
        }
    }

    private void HandlePlayerDied(PlayerDiedEvent _)
    {
        // 使用 realtime 驱动的立即保存，即使死亡界面把 Time.timeScale 设为 0 也能继续执行。
        RequestSave(true, CharacterProgressSaveMode.ResetAfterDeath);
    }

    private void RequestSave(bool immediate, CharacterProgressSaveMode saveMode)
    {
        if (!sessionActive || apiClient == null || !apiClient.IsLoggedIn)
        {
            return;
        }

        dirty = true;
        if (saveMode > requestedSaveMode)
        {
            requestedSaveMode = saveMode;
        }
        changeVersion++;
        nextSaveRealtime = immediate
            ? Time.realtimeSinceStartup
            : Time.realtimeSinceStartup + SaveDebounceSeconds;

        if (saveRoutine == null)
        {
            saveRoutine = StartCoroutine(SaveLoop());
        }
    }

    private IEnumerator SaveLoop()
    {
        while (dirty)
        {
            while (Time.realtimeSinceStartup < nextSaveRealtime)
            {
                yield return null;
            }

            int attemptVersion = changeVersion;
            CharacterProgressSaveMode attemptMode = requestedSaveMode;
            bool success = false;
            string message = "";
            NCharacter savedCharacter = null;
            PlayerProgressSaveData progress;

            try
            {
                progress = TreasureHunterArchitecture.Interface.SendQuery(new GetPlayerProgressSaveDataQuery());
            }
            catch (Exception exception)
            {
                lastError = $"读取玩家存档快照失败：{exception.Message}";
                break;
            }

            if (attemptMode == CharacterProgressSaveMode.ClearRunUpgrades)
            {
                progress.ClearRunUpgrades();
            }
            else if (attemptMode == CharacterProgressSaveMode.ResetAfterDeath)
            {
                InventoryDatabase inventoryDatabase =
                    TreasureHunterArchitecture.Interface.GetSystem<InventorySystem>().Database;
                progress.ResetAfterDeath(inventoryDatabase);
            }

            bool resetAfterDeath = attemptMode == CharacterProgressSaveMode.ResetAfterDeath;
            int vaultDestroyedCount = resetAfterDeath
                ? 0
                : BossRunProgressState.TotalVaultDestroyedCount;
            int completedBossCount = resetAfterDeath
                ? 0
                : BossRunProgressState.CompletedBossCount;

            yield return apiClient.SaveCharacterProgress(
                progress,
                vaultDestroyedCount,
                completedBossCount,
                resetAfterDeath,
                (result, resultMessage, character) =>
                {
                    success = result;
                    message = resultMessage;
                    savedCharacter = character;
                });

            if (!success || savedCharacter == null)
            {
                lastError = string.IsNullOrEmpty(message) ? "角色进度保存失败。" : message;
                Debug.LogWarning(lastError);
                break;
            }

            currentCharacter = savedCharacter.Clone();
            SelectedCharacterState.SetCharacter(currentCharacter);
            savedVersion = attemptVersion;
            lastError = "";

            // 只有本次保存期间没有新请求时才能清掉模式；若又发生了变化，保留最高优先级再保存一次。
            if (changeVersion == attemptVersion)
            {
                requestedSaveMode = CharacterProgressSaveMode.Normal;
            }

            if (attemptMode == CharacterProgressSaveMode.ResetAfterDeath)
            {
                ApplyConfirmedDeathReset(savedCharacter);
            }
            else if (attemptMode == CharacterProgressSaveMode.ClearRunUpgrades)
            {
                // 数据源已确认清零后再同步本地计数，防止随后“正常退出保存”把死亡前旧次数写回存档。
                TreasureHunterArchitecture.Interface.SendCommand(new ClearPlayerRunUpgradeProgressCommand());
            }

            dirty = changeVersion > attemptVersion;
        }

        saveRoutine = null;
    }

    /// <summary>
    /// 死亡重置只有在数据源写入成功后才落到本地，避免网络失败时客户端和数据库状态分叉。
    /// 运行时玩家保持 0 生命；背包同步为“清药水、留材料”，技能、场景传递和关卡累计则立即重置。
    /// </summary>
    private void ApplyConfirmedDeathReset(NCharacter savedCharacter)
    {
        TreasureHunterArchitecture.Interface.SendCommand(
            new ResetPlayerProgressAfterDeathCommand(savedCharacter.Clone()));

        try
        {
            isApplyingAuthoritativeInventory = true;
            TreasureHunterArchitecture.Interface.SendCommand(
                new RestoreInventoryCommand(savedCharacter.inventoryItems));
        }
        finally
        {
            isApplyingAuthoritativeInventory = false;
        }

        GameplayRuntime.Instance.ClearVaultProgressCache();
        BossRunProgressState.ResetRun();
        PlayerSceneTransferState.Clear();
        GameplayStartupGuideState.ResetSession();
    }
}
