using QFramework;
using System;
using System.Collections;
using UnityEngine;

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
    private bool clearUpgradesRequested;
    private float nextSaveRealtime;
    private int changeVersion;
    private int savedVersion;
    private string lastError = "";

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
        BossRunProgressState.PersistentProgressChanged += HandlePersistentProgressChanged;
    }

    private void OnDisable()
    {
        this.UnRegisterEvent<PlayerExperienceGainedEvent>(HandleExperienceGained);
        this.UnRegisterEvent<PlayerAttributeUpgradedEvent>(HandleAttributeUpgraded);
        this.UnRegisterEvent<PlayerDiedEvent>(HandlePlayerDied);
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
        clearUpgradesRequested = false;
        changeVersion = 0;
        savedVersion = 0;
        lastError = "";
    }

    public void EndSession()
    {
        sessionActive = false;
        currentCharacter = null;
        dirty = false;
        clearUpgradesRequested = false;
        lastError = "";

        if (saveRoutine != null)
        {
            StopCoroutine(saveRoutine);
            saveRoutine = null;
        }
    }

    /// <summary>请求一次防抖自动保存。连续经验或多个进度事件会合并成一个网络请求。</summary>
    public void RequestAutoSave()
    {
        RequestSave(false, false);
    }

    /// <summary>
    /// 强制把最新数据保存完成。clearUpgrades 为 true 时同时清空强化和待选择次数，供死亡/重开使用。
    /// </summary>
    public IEnumerator FlushNow(bool clearUpgrades, Action<bool, string, NCharacter> onDone)
    {
        if (!sessionActive)
        {
            onDone?.Invoke(false, "当前没有已进入的角色，无法保存。", null);
            yield break;
        }

        RequestSave(true, clearUpgrades);
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
    public IEnumerator FlushAndLeave(bool clearUpgrades, Action<bool, string> onDone)
    {
        bool saveSuccess = false;
        string message = "";
        yield return FlushNow(clearUpgrades, (success, resultMessage, _) =>
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

    private void HandlePlayerDied(PlayerDiedEvent _)
    {
        // 死亡发生时立即排队“清强化”存档；结算界面的重开按钮还会强制等待它完成。
        RequestSave(true, true);
    }

    private void RequestSave(bool immediate, bool clearUpgrades)
    {
        if (!sessionActive || apiClient == null || !apiClient.IsLoggedIn)
        {
            return;
        }

        dirty = true;
        clearUpgradesRequested |= clearUpgrades;
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
            bool clearUpgradesForAttempt = clearUpgradesRequested;
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

            if (clearUpgradesForAttempt)
            {
                progress.ClearRunUpgrades();
            }

            yield return apiClient.SaveCharacterProgress(
                progress,
                BossRunProgressState.TotalVaultDestroyedCount,
                BossRunProgressState.CompletedBossCount,
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

            if (clearUpgradesForAttempt)
            {
                clearUpgradesRequested = false;
                // 数据源已确认清零后再同步本地计数，防止随后“正常退出保存”把死亡前旧次数写回存档。
                TreasureHunterArchitecture.Interface.SendCommand(new ClearPlayerRunUpgradeProgressCommand());
            }

            dirty = changeVersion > attemptVersion;
        }

        saveRoutine = null;
    }
}
