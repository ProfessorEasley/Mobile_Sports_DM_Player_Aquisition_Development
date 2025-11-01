using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Phase 1 (Simplified) Emotional State Manager
/// Implements normalized pack-quality → emotion deltas:
///   - quality01 = (rawScore - minScore[pack]) / (maxScore[pack] - minScore[pack])
///   - ΔSatisfaction = quality01 * S_max
///   - ΔFrustration  = (1 - quality01) * F_max
///
/// Notes:
/// - Emotion meters are 0–100
/// - No EF / streak / decay logic; intentionally removed for clarity
/// - Rarity values and pack bounds are hardcoded for Phase 1
/// - Exposes S_max / F_max in Inspector for quick tuning
/// - Provides last-delta getters for TelemetryLogger CSV
/// </summary>
public class EmotionalStateManager : MonoBehaviour
{
    public static EmotionalStateManager Instance;

    [Header("Emotion Meters (0–100)")]
    [Range(0, 100)] public float frustration;
    [Range(0, 100)] public float satisfaction;

    [Header("Emotion Formula Settings")]
    [Tooltip("Satisfaction gain at quality01 = 1.0")]
    public float S_max = 3f;
    [Tooltip("Frustration gain at quality01 = 0.0")]
    public float F_max = 3f;

    [Header("Debug")]
    public bool verbose = true;

    // --- Logging variables for Telemetry ---
    private float _lastFrustrationDelta;
    private float _lastSatisfactionDelta;
    private string _lastHookTriggered = "none"; // kept for CSV compatibility
    private int _lastStreakLength = 0;          // not used in simplified model
    private bool _lastRareBoostApplied = false; // not used in simplified model

    // --- Hardcoded rarity values (Phase 1) ---
    // Common=1, Uncommon=2, Rare=3, Epic=4, Legendary=5
    private readonly Dictionary<string, int> _rarityValue = new(StringComparer.OrdinalIgnoreCase)
    {
        { "common", 1 },
        { "uncommon", 2 },
        { "rare", 3 },
        { "epic", 4 },
        { "legendary", 5 }
    };

    // --- Hardcoded per-pack score bounds (from Phase 1 spec) ---
    // Bronze: min 3 (1+1+1), max 7 (2+2+3)
    // Silver: min 6 (2+2+2), max 12 (3+4+5)
    // Gold:   min 9 (3+3+3), max 13 (4+4+5)
    private struct Bounds { public int min; public int max; public Bounds(int mi, int ma){ min=mi; max=ma; } }
    private readonly Bounds _bronzeBounds = new(3, 7);
    private readonly Bounds _silverBounds = new(6, 12);
    private readonly Bounds _goldBounds   = new(9, 13);

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetSession();
    }

    /// <summary>
    /// Reset both meters to 0.
    /// </summary>
    public void ResetSession()
    {
        frustration = 0f;
        satisfaction = 0f;
        _lastFrustrationDelta = 0f;
        _lastSatisfactionDelta = 0f;
        _lastHookTriggered = "none";
        _lastStreakLength = 0;
        _lastRareBoostApplied = false;
    }

    // ------------------------------------------------------------------------
    // PUBLIC API
    // ------------------------------------------------------------------------

    /// <summary>
    /// Apply the normalized emotion update for a pack outcome.
    /// </summary>
    /// <param name="packTypeKey">e.g., 'bronze_pack', 'silver_pack', 'gold_pack'</param>
    /// <param name="rarities">List of rarity strings for the 3 pulls</param>
    /// <returns>EmotionDeltaResult with applied deltas</returns>
    public EmotionDeltaResult ApplyPackOutcome(string packTypeKey, List<string> rarities)
    {
        // 1) Compute raw score from rarities
        int rawScore = 0;
        if (rarities != null)
        {
            foreach (var r in rarities)
            {
                string key = string.IsNullOrEmpty(r) ? "common" : r.ToLowerInvariant();
                rawScore += _rarityValue.TryGetValue(key, out var v) ? v : 1;
            }
        }

        // 2) Get pack bounds
        var b = GetBoundsForPack(packTypeKey);

        // 3) Normalize to 0..1
        float denom = Mathf.Max(1, b.max - b.min);
        float quality01 = Mathf.Clamp01((rawScore - b.min) / denom);

        // 4) Convert to emotion deltas
        float dSat = quality01 * S_max;
        float dFr  = (1f - quality01) * F_max;

        // 5) Apply and clamp to 0..100
        float prevS = satisfaction;
        float prevF = frustration;

        satisfaction = Mathf.Clamp(satisfaction + dSat, 0f, 100f);
        frustration  = Mathf.Clamp(frustration + dFr,  0f, 100f);

        // 6) Record deltas for logging/CSV
        _lastSatisfactionDelta = satisfaction - prevS;
        _lastFrustrationDelta  = frustration  - prevF;
        _lastHookTriggered = "none";
        _lastStreakLength = 0;
        _lastRareBoostApplied = false;

        if (verbose)
            Debug.Log($"[Emotion] pack={packTypeKey} | bounds=[{b.min},{b.max}] | raw={rawScore} | q={quality01:F3} | S={satisfaction:F1} | F={frustration:F1}");

        return new EmotionDeltaResult { satisfaction = _lastSatisfactionDelta, frustration = _lastFrustrationDelta };
    }

    /// <summary>
    /// Back-compat wrapper in case any code still calls the old method.
    /// Uses a best-effort pack key guess (defaults to bronze bounds if unknown).
    /// </summary>
    [Obsolete("Use ApplyPackOutcome(packTypeKey, rarities) instead.")]
    public EmotionDeltaResult HandleOutcomeEvent(List<string> rarities)
    {
        // Fallback guess: try to read the last-used pack from PackOpeningController if available
        string packKey = "bronze_pack";
        try
        {
            var opener = FindObjectOfType<PackOpeningController>();
            if (opener != null && !string.IsNullOrEmpty(opener.packType))
                packKey = opener.packType;
        }
        catch { /* ignore */ }

        return ApplyPackOutcome(packKey, rarities);
    }

    // ------------------------------------------------------------------------
    // INTERNAL HELPERS
    // ------------------------------------------------------------------------
    private Bounds GetBoundsForPack(string packTypeKey)
    {
        string k = (packTypeKey ?? string.Empty).ToLowerInvariant();

        // Flexible string-matching so we don't depend on exact keys
        if (k.Contains("bronze")) return _bronzeBounds;
        if (k.Contains("silver")) return _silverBounds;
        if (k.Contains("gold"))   return _goldBounds;

        // If keys don't include names, try reading current config pack name (optional)
        // Otherwise, default to Silver as a middle ground
        return _silverBounds;
    }

    // Snapshot for UI
    public (float fr, float sa) Snapshot() => (frustration, satisfaction);

    // Telemetry getters (CSV export depends on these)
    public float GetLastFrustrationDelta() => _lastFrustrationDelta;
    public float GetLastSatisfactionDelta() => _lastSatisfactionDelta;
    public string GetLastHookTriggered() => _lastHookTriggered;
    public int GetLastStreakLength() => _lastStreakLength;
    public bool GetLastRareBoostApplied() => _lastRareBoostApplied;
}

public struct EmotionDeltaResult
{
    public float frustration;
    public float satisfaction;
}
