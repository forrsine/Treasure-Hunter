using System;
using UnityEngine;

public sealed class GameplayRuntime
{
    private static readonly GameplayRuntime instance = new GameplayRuntime();

    private GameplayRuntime()
    {
    }

    public static GameplayRuntime Instance => instance;

    public PlayerCo CurrentPlayer { get; private set; }
    public BoxCo CurrentVault { get; private set; }
    public IGameplayInput CurrentInput { get; private set; }

    public event Action<PlayerCo> CurrentPlayerChanged;
    public event Action<BoxCo> CurrentVaultChanged;
    public event Action<IGameplayInput> CurrentInputChanged;

    public int CurrentScore => CurrentVault != null ? CurrentVault.Score : 0;
    public int CurrentVaultDestroyedCount => CurrentVault != null ? CurrentVault.DestroyedCount : 0;

    public void RegisterPlayer(PlayerCo player)
    {
        if (player == null || CurrentPlayer == player)
        {
            return;
        }

        CurrentPlayer = player;
        CurrentPlayerChanged?.Invoke(CurrentPlayer);
    }

    public void UnregisterPlayer(PlayerCo player)
    {
        if (player == null || CurrentPlayer != player)
        {
            return;
        }

        CurrentPlayer = null;
        CurrentPlayerChanged?.Invoke(null);
    }

    public void RegisterVault(BoxCo vault)
    {
        if (vault == null || CurrentVault == vault)
        {
            return;
        }

        CurrentVault = vault;
        CurrentVaultChanged?.Invoke(CurrentVault);
    }

    public void UnregisterVault(BoxCo vault)
    {
        if (vault == null || CurrentVault != vault)
        {
            return;
        }

        CurrentVault = null;
        CurrentVaultChanged?.Invoke(null);
    }

    public void RegisterInput(IGameplayInput input)
    {
        if (input == null || CurrentInput == input)
        {
            return;
        }

        CurrentInput = input;
        CurrentInputChanged?.Invoke(CurrentInput);
    }

    public void UnregisterInput(IGameplayInput input)
    {
        if (input == null || CurrentInput != input)
        {
            return;
        }

        CurrentInput = null;
        CurrentInputChanged?.Invoke(null);
    }

    public bool TryGetPlayerTransform(out Transform playerTransform)
    {
        playerTransform = CurrentPlayer != null ? CurrentPlayer.transform : null;
        return playerTransform != null;
    }

    public void AddExpToCurrentPlayer(int exp)
    {
        if (CurrentPlayer == null || exp <= 0)
        {
            return;
        }

        CurrentPlayer.AddExp(exp);
    }

    public void FullHealCurrentPlayer()
    {
        if (CurrentPlayer == null)
        {
            return;
        }

        CurrentPlayer.FullHeal();
    }
}
