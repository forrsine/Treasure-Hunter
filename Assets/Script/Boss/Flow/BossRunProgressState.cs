using UnityEngine;

/// <summary>
/// Boss 周回进度状态：保存“主场景宝箱进度”和“Boss 已挑战轮次”这类跨场景数据。
/// 注意：这里不保存场景里的 GameObject，只保存可恢复的数据，避免 LoadScene 后引用失效。
/// </summary>
public static class BossRunProgressState
{
    private const int DefaultVaultsPerBoss = 5;

    private static int vaultsPerBoss = DefaultVaultsPerBoss;
    private static int totalVaultDestroyedCount;
    private static int completedBossCount;
    private static int activeBossRound;
    private static bool bossFightInProgress;
    private static bool hasMainSceneReturnSpawn;
    private static Vector3 mainSceneReturnPosition;
    private static Quaternion mainSceneReturnRotation = Quaternion.identity;

    public static int VaultsPerBoss => vaultsPerBoss;
    public static int TotalVaultDestroyedCount => totalVaultDestroyedCount;
    public static int CompletedBossCount => completedBossCount;
    public static int CurrentBossRound => bossFightInProgress
        ? Mathf.Max(1, activeBossRound)
        : Mathf.Max(1, completedBossCount + 1);
    public static int NextBossUnlockVaultCount => Mathf.Max(1, completedBossCount + 1) * vaultsPerBoss;
    public static int VaultsUntilNextBoss => Mathf.Max(0, NextBossUnlockVaultCount - totalVaultDestroyedCount);
    public static bool IsBossEntranceReady => !bossFightInProgress && totalVaultDestroyedCount >= NextBossUnlockVaultCount;

    /// <summary>
    /// 每局可以由入口控制器配置一次“打几次宝箱开 Boss 门”，默认是 5。
    /// </summary>
    public static void ConfigureVaultsPerBoss(int requiredVaultCount)
    {
        vaultsPerBoss = Mathf.Max(1, requiredVaultCount);
    }

    /// <summary>
    /// 记录宝箱当前累计击破次数。以 BoxCo 的 DestroyedCount 为准，避免主场景重载后控制器本地计数归零。
    /// </summary>
    public static void RecordVaultDestroyed(BoxCo vault)
    {
        if (vault == null)
        {
            return;
        }

        totalVaultDestroyedCount = Mathf.Max(totalVaultDestroyedCount, vault.DestroyedCount);
    }

    /// <summary>
    /// 主场景重新加载后，把新生成的宝箱恢复到之前的累计击破层级。
    /// </summary>
    public static void RestoreVaultProgressIfNeeded(BoxCo vault)
    {
        if (vault == null || totalVaultDestroyedCount <= 0)
        {
            return;
        }

        if (vault.DestroyedCount == totalVaultDestroyedCount)
        {
            return;
        }

        vault.RestoreProgress(totalVaultDestroyedCount);
    }

    /// <summary>
    /// 玩家进入 Boss 传送门前调用：记录返回主场景的位置，并锁定本轮 Boss 轮次。
    /// </summary>
    public static void BeginBossChallenge(Vector3 returnPosition, Quaternion returnRotation)
    {
        mainSceneReturnPosition = returnPosition;
        mainSceneReturnRotation = returnRotation;
        hasMainSceneReturnSpawn = true;

        bossFightInProgress = true;
        activeBossRound = Mathf.Max(1, completedBossCount + 1);
    }

    /// <summary>
    /// Boss 死亡流程结束后调用：本轮 Boss 正式完成，下一次需要再打满 5 次宝箱才会开门。
    /// </summary>
    public static void MarkBossDefeated()
    {
        if (bossFightInProgress)
        {
            completedBossCount = Mathf.Max(completedBossCount, activeBossRound);
        }

        bossFightInProgress = false;
        activeBossRound = 0;
    }

    /// <summary>
    /// 主场景角色生成器读取一次返回位置。只消费一次，避免之后普通重开也被传送到旧位置。
    /// </summary>
    public static bool TryConsumeMainSceneReturnSpawn(out Vector3 position, out Quaternion rotation)
    {
        position = mainSceneReturnPosition;
        rotation = mainSceneReturnRotation;

        if (!hasMainSceneReturnSpawn)
        {
            return false;
        }

        hasMainSceneReturnSpawn = false;
        return true;
    }

    /// <summary>
    /// 开始新局/退出登录时清空 Boss 周回状态，避免编辑器同一次 Play 中残留上一局数据。
    /// </summary>
    public static void ResetRun()
    {
        totalVaultDestroyedCount = 0;
        completedBossCount = 0;
        activeBossRound = 0;
        bossFightInProgress = false;
        hasMainSceneReturnSpawn = false;
        mainSceneReturnPosition = Vector3.zero;
        mainSceneReturnRotation = Quaternion.identity;
        vaultsPerBoss = DefaultVaultsPerBoss;
    }
}
