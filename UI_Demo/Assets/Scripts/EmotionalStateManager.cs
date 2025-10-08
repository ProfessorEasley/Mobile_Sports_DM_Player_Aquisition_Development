using System;
using System.Collections.Generic;
using UnityEngine;

/// Phase 1: session-scoped emotional state + streak math + guardrails.
public class EmotionalStateManager : MonoBehaviour
{
    public static EmotionalStateManager Instance;

    [Header("Phase 1 Params")]
    [Range(0.5f, 2.0f)] public float EF = 1.2f;        // Escalation Factor
    [Range(0.5f, 1.0f)] public float softCapPct = 0.85f;
    [Range(1.0f, 1.5f)] public float rareBoostCap = 1.20f; // +20% max

    [Header("Emotions (0..10)")]
    [Range(0,10)] public float frustration;
    [Range(0,10)] public float satisfaction;
    [Range(0,10)] public float envy;

    // session-scoped counters / timers
    int _streakCommons;       // for Outcome Streak (fail/neutral)
    int _streakRarePlus;      // for Outcome Streak (success)
    int _droughtCounter;      // increments on repeated drought ticks
    int _socialStreak;        // consecutive “behind/ahead” checks

    // last activity/time markers (for drought pacing if needed)
    float _tsLastProgress;    // seconds since last "progress" (Rare+)
    float _tsLastAny;         // last update time

    // decay gatekeepers (simple phase-1 toggles we can set from outside)
    int _sessionsWithoutShareOrRare = 0;
    int _sessionsWithoutSocial = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetSession();
    }

    public void ResetSession()
    {
        frustration = satisfaction = envy = 0f;
        _streakCommons = _streakRarePlus = 0;
        _droughtCounter = _socialStreak = 0;
        _tsLastProgress = Time.time;
        _tsLastAny = Time.time;
        _sessionsWithoutShareOrRare = 0;
        _sessionsWithoutSocial = 0;
    }

    // ------------ PUBLIC API (events) ------------

    /// Call after a pack is opened with its rarities.
    public EmotionDeltaResult HandleOutcomeEvent(List<string> rarities, bool pity, string pityType)

    {
        _tsLastAny = Time.time;

        // simple "progress" detection: Rare+
        bool hasRarePlus = rarities.Exists(r =>
        {
            var k = (r ?? "common").ToLowerInvariant();
            return k == "rare" || k == "epic" || k == "legendary";
        });

        // Update streaks
        if (hasRarePlus) { _streakRarePlus++; _streakCommons = 0; _tsLastProgress = Time.time; }
        else             { _streakCommons++;  _streakRarePlus = 0; }

        // Phase-1 decay triggers (session-level toggles you can set from game flow)
        if (hasRarePlus) _sessionsWithoutShareOrRare = 0; else _sessionsWithoutShareOrRare++;
        // (Social exposure is tracked in OnSocialUpdate below)

        // Compute base deltas by hook rules
        var deltas = new EmotionDeltaResult();

        // Outcome Streak – COMMON streak → frustration +5 scaled; RARE+ → satisfaction +2 scaled (+20% cap)
        if (_streakCommons >= 2)
        {
            float baseF = 5f;
            float efPow = Mathf.Pow(EF, _streakCommons - 1);
            deltas.frustration += baseF * efPow;
        }
        if (_streakRarePlus >= 1)
        {
            float baseS = 2f;
            float efPow = Mathf.Pow(EF, Mathf.Max(0, _streakRarePlus - 1));
            float delta = baseS * efPow;
            delta = ApplyRarePlusCap(delta); // +20% cap
            deltas.satisfaction += delta;
        }

        // Apply decay per Phase-1 rules
        ApplyPhase1Decay(hasRarePlus, false, socialExposed:false);

        // Clamp & soft-cap (single update)
        ApplyDeltasWithSoftCap(deltas);

        return deltas;
    }

    /// Call on time/tick checks to model drought (e.g., every 10s when time_since_progress >= 60s)
    public EmotionDeltaResult OnDroughtTick()
    {
        _tsLastAny = Time.time;
        _droughtCounter++;

        var deltas = new EmotionDeltaResult();
        float baseF = 3f;
        float efPow = Mathf.Pow(EF, Mathf.Max(0, _droughtCounter - 1));
        deltas.frustration += baseF * efPow;

        ApplyPhase1Decay(hasRarePlus:false, shared:false, socialExposed:false);
        ApplyDeltasWithSoftCap(deltas);
        return deltas;
    }

    /// Call when social/leaderboard exposure occurs.
    /// peerDelta: "behind" → envy +1.5 (streak-scaled) | "ahead" → satisfaction +1.0 (no envy)
    public EmotionDeltaResult OnSocialUpdate(string peerDelta)
    {
        _tsLastAny = Time.time;
        _sessionsWithoutSocial = 0; // reset session counter (we saw social)
        _socialStreak++;

        var deltas = new EmotionDeltaResult();
        if (peerDelta == "behind")
        {
            float baseE = 1.5f;
            float efPow = Mathf.Pow(EF, Mathf.Max(0, _socialStreak - 1));
            deltas.envy += baseE * efPow;
        }
        else if (peerDelta == "ahead")
        {
            _socialStreak = 0; // break envy streak
            deltas.satisfaction += 1.0f; // small positive
        }

        ApplyPhase1Decay(hasRarePlus:false, shared:false, socialExposed:true);
        ApplyDeltasWithSoftCap(deltas);
        return deltas;
    }

    // ------------ helpers ------------

    float ApplyRarePlusCap(float deltaPositive)
    {
        // +20% cap on net positive reward delta
        if (deltaPositive <= 0f) return deltaPositive;
        return Mathf.Min(deltaPositive * rareBoostCap, deltaPositive * rareBoostCap); // kept explicit for clarity/extension
    }

    void ApplyPhase1Decay(bool hasRarePlus, bool shared, bool socialExposed)
    {
        // frustration: −1.5 when XP multiplier/dupe-bonus fires (wire this externally; call DecayFrustrationBonus() then)
        // satisfaction: −2.0 if no share AND no Rare+ for two sessions
        // envy: −1.0 after two sessions without social exposure

        if (!(hasRarePlus || shared))
        {
            if (_sessionsWithoutShareOrRare >= 2) satisfaction = Mathf.Max(0f, satisfaction - 2.0f);
        }

        if (!socialExposed)
        {
            _sessionsWithoutSocial++;
            if (_sessionsWithoutSocial >= 2) envy = Mathf.Max(0f, envy - 1.0f);
        }
    }

    public void DecayFrustrationBonus()
    {
        frustration = Mathf.Max(0f, frustration - 1.5f);
    }

    void ApplyDeltasWithSoftCap(EmotionDeltaResult d)
    {
        // target after applying deltas
        float tgtF = Mathf.Clamp(frustration + d.frustration, 0f, 10f);
        float tgtS = Mathf.Clamp(satisfaction + d.satisfaction, 0f, 10f);
        float tgtE = Mathf.Clamp(envy + d.envy, 0f, 10f);

        // soft-cap: don’t allow a single update to exceed 85% of target peak from the current value
        frustration = SoftCap(frustration, tgtF, softCapPct);
        satisfaction = SoftCap(satisfaction, tgtS, softCapPct);
        envy = SoftCap(envy, tgtE, softCapPct);
        
        // 🟢 Debug output for Phase-1 verification
        Debug.Log($"[Emotion Update]  Frustration={frustration:F2}  Satisfaction={satisfaction:F2}  Envy={envy:F2}");
    }

    float SoftCap(float current, float target, float pct)
    {
        if (target <= current) return target; // decays are fine
        float maxStep = current + (target - current) * pct;
        return Mathf.Min(target, maxStep);
    }

    // Expose read-only state to other systems
    public (float fr, float sa, float ev) Snapshot() => (frustration, satisfaction, envy);

    // Reset streaks from outside if needed (e.g., when user exits flow, etc.)
    public void BreakOutcomeStreaks() { _streakCommons = 0; _streakRarePlus = 0; }
}

public struct EmotionDeltaResult
{
    public float frustration;
    public float satisfaction;
    public float envy;
}
