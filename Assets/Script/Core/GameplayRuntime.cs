using System;
using UnityEngine;

/// <summary>
/// 当前游戏局的运行时上下文。
/// 它只保存“当前玩家、金库和输入源”这类跨系统引用，并通过事件通知引用变化，
/// 避免摄像机、UI、敌人频繁使用 FindObjectOfType 查找场景对象。
/// </summary>
public sealed class GameplayRuntime
{
    private static readonly GameplayRuntime instance = new GameplayRuntime();

    private GameplayRuntime()
    {
    }

    public static GameplayRuntime Instance => instance;

    public PlayerRuntimeController CurrentPlayer { get; private set; }
    public BoxCo CurrentVault { get; private set; }
    public IGameplayInput CurrentInput { get; private set; }

    private int cachedScore;
    private int cachedVaultDestroyedCount;
    private int bonusScore;

    public event Action<PlayerRuntimeController> CurrentPlayerChanged;
    public event Action<BoxCo> CurrentVaultChanged;
    public event Action<IGameplayInput> CurrentInputChanged;

    public int CurrentScore => (CurrentVault != null ? CurrentVault.Score : cachedScore) + bonusScore;
    public int CurrentVaultDestroyedCount => CurrentVault != null ? CurrentVault.DestroyedCount : cachedVaultDestroyedCount;

    /// <summary>
    /// 注册当前玩家。
    /// 这样摄像机、UI、掉落奖励等系统都能通过统一入口找到玩家，而不是各自去场景里查找。
    /// </summary>
    public void RegisterPlayer(PlayerRuntimeController player)
    {
        if (player == null || CurrentPlayer == player)
        {
            return;
        }

        CurrentPlayer = player;
        CurrentPlayerChanged?.Invoke(CurrentPlayer);
    }

    /// <summary>
    /// 玩家销毁或离场时注销引用，避免保留失效对象。
    /// </summary>
    public void UnregisterPlayer(PlayerRuntimeController player)
    {
        if (player == null || CurrentPlayer != player)
        {
            return;
        }

        CurrentPlayer = null;
        CurrentPlayerChanged?.Invoke(null);
    }

    /// <summary>
    /// 兼容未挂载的旧脚本调用。只有新的 PlayerRuntimeController 能成为权威玩家引用。
    /// </summary>
    public void RegisterPlayer(MonoBehaviour obsoletePlayer)
    {
        if (obsoletePlayer is PlayerRuntimeController runtimeController)
        {
            RegisterPlayer(runtimeController);
        }
    }

    public void UnregisterPlayer(MonoBehaviour obsoletePlayer)
    {
        if (obsoletePlayer is PlayerRuntimeController runtimeController)
        {
            UnregisterPlayer(runtimeController);
        }
    }

    /// <summary>
    /// 注册本局金库目标，供分数 UI 和玩法逻辑统一访问。
    /// </summary>
    public void RegisterVault(BoxCo vault)
    {
        if (vault == null || CurrentVault == vault)
        {
            return;
        }

        CurrentVault = vault;
        CacheVaultProgress(vault);
        CurrentVaultChanged?.Invoke(CurrentVault);
    }

    /// <summary>
    /// 金库离场后清理引用。
    /// </summary>
    public void UnregisterVault(BoxCo vault)
    {
        if (vault == null || CurrentVault != vault)
        {
            return;
        }

        CacheVaultProgress(vault);
        CurrentVault = null;
        CurrentVaultChanged?.Invoke(null);
    }

    /// <summary>
    /// 切换到 Boss 房间前主动缓存当前金库进度。
    /// Boss 场景没有 BoxCo，但会复用主场景 GameplayUiRoot，因此 UI 需要能读取上一段主场景的分数和宝箱次数。
    /// </summary>
    public void CacheCurrentVaultProgress()
    {
        if (CurrentVault != null)
        {
            CacheVaultProgress(CurrentVault);
        }
    }

    /// <summary>
    /// 开始新局或退出登录时清理局内进度缓存，避免下一局短暂显示上一局数据。
    /// </summary>
    public void ClearVaultProgressCache()
    {
        cachedScore = 0;
        cachedVaultDestroyedCount = 0;
        bonusScore = 0;
    }

    /// <summary>
    /// 增加宝箱伤害分以外的局内奖励分，例如 Boss 击杀分。
    /// 独立保存可以避免主场景与 Boss 场景切换时覆盖 BoxCo 自己计算的伤害分。
    /// </summary>
    public void AddScoreBonus(int amount)
    {
        if (amount > 0)
        {
            bonusScore += amount;
        }
    }

    private void CacheVaultProgress(BoxCo vault)
    {
        if (vault == null)
        {
            return;
        }

        cachedScore = vault.Score;
        cachedVaultDestroyedCount = vault.DestroyedCount;
    }

    /// <summary>
    /// 注册当前输入源。
    /// 玩家逻辑依赖接口而不是具体输入脚本，这样以后替换新输入系统也更容易。
    /// </summary>
    public void RegisterInput(IGameplayInput input)
    {
        if (input == null || CurrentInput == input)
        {
            return;
        }

        CurrentInput = input;
        CurrentInputChanged?.Invoke(CurrentInput);
    }

    /// <summary>
    /// 注销输入源，通常发生在切场景或对象销毁时。
    /// </summary>
    public void UnregisterInput(IGameplayInput input)
    {
        if (input == null || CurrentInput != input)
        {
            return;
        }

        CurrentInput = null;
        CurrentInputChanged?.Invoke(null);
    }

    /// <summary>
    /// 供其他系统快速获取玩家 Transform，例如摄像机跟随或怪物索敌。
    /// </summary>
    public bool TryGetPlayerTransform(out Transform playerTransform)
    {
        playerTransform = CurrentPlayer != null ? CurrentPlayer.transform : null;
        return playerTransform != null;
    }

    /// <summary>
    /// 给当前玩家发放经验的便捷入口。
    /// 这里只负责转发，真正的升级结算仍由成长系统处理。
    /// </summary>
    public void AddExpToCurrentPlayer(int exp)
    {
        if (CurrentPlayer == null || exp <= 0)
        {
            return;
        }

        CurrentPlayer.AddExp(exp);
    }

    /// <summary>
    /// 让当前玩家直接回满血量，常用于测试或局内补给。
    /// </summary>
    public void FullHealCurrentPlayer()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        CurrentPlayer.FullHeal();
    }

    /// <summary>
    /// 按最大生命百分比治疗当前玩家，金库奖励通过这里走正式治疗事件与 HUD 刷新流程。
    /// </summary>
    public void HealCurrentPlayerByMaxHpPercent(float percent)
    {
        if (CurrentPlayer == null || percent <= 0f)
        {
            return;
        }

        int amount = Mathf.CeilToInt(CurrentPlayer.Stats.MaxHp * Mathf.Clamp01(percent));
        CurrentPlayer.Heal(amount, false);
    }
}
