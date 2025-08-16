// PlayerWallet.cs
using System;
using UnityEngine;
using CCAS.Config;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance;
    public int coins = 500;         // temp default
    public int gems  = 0;

    public event Action OnChanged;

    void Awake() {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);
    }

    public bool CanAfford(PackType p) {
        if (p?.cost == null) return true;
        if (p.cost.coins > 0) return coins >= p.cost.coins;
        if (p.cost.gems  > 0) return gems  >= p.cost.gems;
        return true;
    }

    public bool SpendForPack(PackType p) {
        if (!CanAfford(p)) return false;
        if (p.cost.coins > 0) coins -= p.cost.coins;
        else if (p.cost.gems > 0)   gems  -= p.cost.gems;
        OnChanged?.Invoke();
        return true;
    }

    // Optional helpers for testing
    public void AddCoins(int a){ coins += a; OnChanged?.Invoke(); }
    public void AddGems (int a){ gems  += a; OnChanged?.Invoke(); }
}
