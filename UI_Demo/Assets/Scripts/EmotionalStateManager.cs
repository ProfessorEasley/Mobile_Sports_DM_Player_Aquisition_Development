using System;
using System.Collections.Generic;
using UnityEngine;

/// Phase 1: Basic emotional state tracking for frustration and satisfaction.
/// add envy later after implement leaderboard features.
public class EmotionalStateManager : MonoBehaviour
{
    public static EmotionalStateManager Instance;

    [Header("Phase 1 Parameters")]
    [Range(0.5f, 2.0f)] public float EF = 1.2f;        // Escalation Factor
    [Range(0.5f, 1.0f)] public float softCapPct = 0.85f;
    [Range(1.0f, 1.5f)] public float rareBoostCap = 1.20f; // +20% cap

    [Header("Emotions (0–10)")]
    [Range(0, 10)] public float frustration;
    [Range(0, 10)] public float satisfaction;

    // Session-scoped counters / timers
    int _streakCommons;       // Consecutive pulls with no Rare+
    int _streakRarePlus;      // Consecutive pulls with Rare+
    int _droughtCounter;      // Used if time since last progress grows large

    // Last activity/time markers
    float _tsLastProgress;
    float _tsLastAny;

    // Decay gatekeepers
    int _sessionsWithoutShareOrRare = 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetSession();
    }

    public void ResetSession()
    {
        frustration = satisfaction = 0f;
        _streakCommons = _streakRarePlus = 0;
        _droughtCounter = 0;
        _tsLastProgress = Time.time;
        _tsLastAny = Time.time;
        _sessionsWithoutShareOrRare = 0;
    }

    // ------------ PUBLIC API ------------

    /// Called after a pack is opened with its rarities.
    public EmotionDeltaResult HandleOutcomeEvent(List<string> rarities, bool pity, string pityType)
    {
        _tsLastAny = Time.time;

        // "Progress" = got a Rare or higher
        bool hasRarePlus = rarities.Exists(r =>
        {
            var k = (r ?? "common").ToLowerInvariant();
            return k == "rare" || k == "epic" || k == "legendary";
        });

        // Update streaks
        if (hasRarePlus)
        {
            _streakRarePlus++;
            _streakCommons = 0;
            _tsLastProgress = Time.time;
        }
        else
        {
            _streakCommons++;
            _streakRarePlus = 0;
        }

        // Reset or increment decay counters
        if (hasRarePlus)
            _sessionsWithoutShareOrRare = 0;
        else
            _sessionsWithoutShareOrRare++;

        var deltas = new EmotionDeltaResult();

        // COMMON streak → frustration
        if (_streakCommons >= 2)
        {
            float baseF = 5f;
            float efPow = Mathf.Pow(EF, _streakCommons - 1);
            deltas.frustration += baseF * efPow;
        }

        // RARE+ streak → satisfaction
        if (_streakRarePlus >= 1)
        {
            float baseS = 2f;
            float efPow = Mathf.Pow(EF, Mathf.Max(0, _streakRarePlus - 1));
            float delta = baseS * efPow;
            delta = ApplyRarePlusCap(delta);
            deltas.satisfaction += delta;
        }

        // Apply decay and update
        ApplyPhase1Decay(hasRarePlus);
        ApplyDeltasWithSoftCap(deltas);

        return deltas;
    }

    /// Called periodically to simulate frustration increase from long gaps (drought).
    public EmotionDeltaResult OnDroughtTick()
    {
        _tsLastAny = Time.time;
        _droughtCounter++;

        var deltas = new EmotionDeltaResult();
        float baseF = 3f;
        float efPow = Mathf.Pow(EF, Mathf.Max(0, _droughtCounter - 1));
        deltas.frustration += baseF * efPow;

        ApplyPhase1Decay(hasRarePlus: false);
        ApplyDeltasWithSoftCap(deltas);
        return deltas;
    }

    // ------------ HELPERS ------------

    float ApplyRarePlusCap(float deltaPositive)
    {
        if (deltaPositive <= 0f) return deltaPositive;
        return Mathf.Min(deltaPositive * rareBoostCap, 10f);
    }

    void ApplyPhase1Decay(bool hasRarePlus)
    {
        // Frustration: −1.5 when dupe-bonus or XP multiplier fires (call externally)
        // Satisfaction: −2.0 if no Rare+ for two sessions
        if (!hasRarePlus && _sessionsWithoutShareOrRare >= 2)
            satisfaction = Mathf.Max(0f, satisfaction - 2.0f);
    }

    public void DecayFrustrationBonus()
    {
        frustration = Mathf.Max(0f, frustration - 1.5f);
    }

    void ApplyDeltasWithSoftCap(EmotionDeltaResult d)
    {
        float tgtF = Mathf.Clamp(frustration + d.frustration, 0f, 10f);
        float tgtS = Mathf.Clamp(satisfaction + d.satisfaction, 0f, 10f);

        frustration = SoftCap(frustration, tgtF, softCapPct);
        satisfaction = SoftCap(satisfaction, tgtS, softCapPct);

        Debug.Log($"[Emotion Update] Frustration={frustration:F2} Satisfaction={satisfaction:F2}");
    }

    float SoftCap(float current, float target, float pct)
    {
        if (target <= current) return target; // Decays are fine
        float maxStep = current + (target - current) * pct;
        return Mathf.Min(target, maxStep);
    }

    // Read-only snapshot
    public (float fr, float sa) Snapshot() => (frustration, satisfaction);

    public void BreakOutcomeStreaks()
    {
        _streakCommons = 0;
        _streakRarePlus = 0;
    }
}

public struct EmotionDeltaResult
{
    public float frustration;
    public float satisfaction;
}
