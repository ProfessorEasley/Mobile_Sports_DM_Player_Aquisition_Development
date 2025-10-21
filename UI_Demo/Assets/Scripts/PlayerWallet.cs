// PlayerWallet.cs
using System;
using UnityEngine;
using CCAS.Config;

/// <summary>
/// Simple player currency manager for Phase 1.
/// Tracks coins and gems, handles affordability checks,
/// and raises OnChanged events for UI refresh.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance;

    [Header("Starting Balances")]
    public int coins = 500;
    public int gems = 0;

    public event Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadWallet();
    }

    // ----------------------------------------------------------
    // Core Logic
    // ----------------------------------------------------------

    public bool CanAfford(PackType p)
    {
        if (p?.cost == null) return true;
        if (p.cost.coins > 0) return coins >= p.cost.coins;
        if (p.cost.gems > 0) return gems >= p.cost.gems;
        return true;
    }

    public bool SpendForPack(PackType p)
    {
        if (!CanAfford(p))
            return false;

        if (p.cost.coins > 0) coins = Mathf.Max(0, coins - p.cost.coins);
        else if (p.cost.gems > 0) gems = Mathf.Max(0, gems - p.cost.gems);

        SaveWallet();
        OnChanged?.Invoke();
        return true;
    }

    // ----------------------------------------------------------
    // Optional Helpers
    // ----------------------------------------------------------

    public void AddCoins(int amount)
    {
        coins = Mathf.Max(0, coins + amount);
        SaveWallet();
        OnChanged?.Invoke();
    }

    public void AddGems(int amount)
    {
        gems = Mathf.Max(0, gems + amount);
        SaveWallet();
        OnChanged?.Invoke();
    }

    // ----------------------------------------------------------
    // Persistence (Optional)
    // ----------------------------------------------------------

    private void SaveWallet()
    {
        PlayerPrefs.SetInt("wallet_coins", coins);
        PlayerPrefs.SetInt("wallet_gems", gems);
        PlayerPrefs.Save();
    }

    private void LoadWallet()
    {
        coins = PlayerPrefs.GetInt("wallet_coins", coins);
        gems = PlayerPrefs.GetInt("wallet_gems", gems);
    }
}
