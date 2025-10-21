using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// Phase 1 (Offline, Single Player)
/// Handles basic emotional state tracking for Frustration and Satisfaction.
/// Dynamically loads tuning constants from phase1_config.json in Resources or StreamingAssets.
public class EmotionalStateManager : MonoBehaviour
{
    public static EmotionalStateManager Instance;

    [Header("Phase 1 Parameters (Loaded from JSON)")]
    [Range(0.5f, 2.0f)] public float EF = 1.2f;               // Escalation factor
    [Range(0.5f, 1.0f)] public float softCapPct = 0.85f;       // Soft-cap % of target
    [Range(1.0f, 1.5f)] public float rareBoostCap = 1.20f;     // +20% cap for Rare+
    public float baseFrustration = 5f;
    public float baseSatisfaction = 2f;
    public float baseDrought = 3f;
    public float frustrationDecay = 1.5f;
    public float satisfactionDecay = 2.0f;

    [Header("Emotions (0–10)")]
    [Range(0, 10)] public float frustration;
    [Range(0, 10)] public float satisfaction;

    // --- Session-scoped counters / timers ---
    int _streakCommons;
    int _streakRarePlus;
    int _droughtCounter;

    float _tsLastProgress;
    float _tsLastAny;

    int _sessionsWithoutShareOrRare = 0;

    private const string ConfigFileName = "phase1_config.json";

    [Serializable]
    private class Phase1Config
    {
        public float EF = 1.2f;
        public float softCapPct = 0.85f;
        public float rareBoostCap = 1.2f;
        public float baseFrustration = 5f;
        public float baseSatisfaction = 2f;
        public float baseDrought = 3f;
        public float frustrationDecay = 1.5f;
        public float satisfactionDecay = 2.0f;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPhase1Config();
        ResetSession();
    }

    /// <summary>Load parameters from phase1_config.json (Resources or StreamingAssets).</summary>
    void LoadPhase1Config()
    {
        try
        {
            // 1️⃣ Try Resources first
            TextAsset configText = Resources.Load<TextAsset>("phase1_config");

            // 2️⃣ Fallback to StreamingAssets (useful for standalone builds)
            if (configText == null)
            {
                string altPath = Path.Combine(Application.streamingAssetsPath, ConfigFileName);
                if (File.Exists(altPath))
                    configText = new TextAsset(File.ReadAllText(altPath));
            }

            if (configText == null)
            {
                Debug.LogWarning("[Phase1Config] No config file found. Using defaults.");
                return;
            }

            var cfg = JsonConvert.DeserializeObject<Phase1Config>(configText.text);
            if (cfg != null)
            {
                EF = cfg.EF;
                softCapPct = cfg.softCapPct;
                rareBoostCap = cfg.rareBoostCap;
                baseFrustration = cfg.baseFrustration;
                baseSatisfaction = cfg.baseSatisfaction;
                baseDrought = cfg.baseDrought;
                frustrationDecay = cfg.frustrationDecay;
                satisfactionDecay = cfg.satisfactionDecay;

                Debug.Log($"[Phase1Config] ✅ Loaded: EF={EF}, softCap={softCapPct}, rareBoost={rareBoostCap}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Phase1Config] ❌ Failed to load config: {e.Message}");
        }
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

    // ----------------------------------------------------
    // PUBLIC API
    // ----------------------------------------------------

    /// <summary>Called after a pack is opened with its rarities.</summary>
    public EmotionDeltaResult HandleOutcomeEvent(List<string> rarities, bool pity, string pityType)
    {
        _tsLastAny = Time.time;

        // "Progress" = got a Rare or higher
        bool hasRarePlus = rarities.Exists(r =>
        {
            string k = (r ?? "common").ToLowerInvariant();
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

        _sessionsWithoutShareOrRare = hasRarePlus ? 0 : _sessionsWithoutShareOrRare + 1;

        var deltas = new EmotionDeltaResult();

        // Common streak → frustration
        if (_streakCommons >= 2)
        {
            float efPow = Mathf.Pow(EF, _streakCommons - 1);
            deltas.frustration += baseFrustration * efPow;
        }

        // Rare+ streak → satisfaction
        if (_streakRarePlus >= 1)
        {
            float efPow = Mathf.Pow(EF, Mathf.Max(0, _streakRarePlus - 1));
            float delta = baseSatisfaction * efPow;
            deltas.satisfaction += ApplyRarePlusCap(delta);
        }

        ApplyPhase1Decay(hasRarePlus);
        ApplyDeltasWithSoftCap(deltas);

        return deltas;
    }

    /// <summary>Increases frustration if player has long gaps without progress.</summary>
    public EmotionDeltaResult OnDroughtTick()
    {
        _tsLastAny = Time.time;
        _droughtCounter++;

        var deltas = new EmotionDeltaResult();
        float efPow = Mathf.Pow(EF, Mathf.Max(0, _droughtCounter - 1));
        deltas.frustration += baseDrought * efPow;

        ApplyPhase1Decay(false);
        ApplyDeltasWithSoftCap(deltas);
        return deltas;
    }

    // ----------------------------------------------------
    // INTERNAL HELPERS
    // ----------------------------------------------------

    float ApplyRarePlusCap(float deltaPositive)
    {
        return deltaPositive <= 0f ? deltaPositive : Mathf.Min(deltaPositive * rareBoostCap, 10f);
    }

    void ApplyPhase1Decay(bool hasRarePlus)
    {
        if (!hasRarePlus && _sessionsWithoutShareOrRare >= 2)
            satisfaction = Mathf.Max(0f, satisfaction - satisfactionDecay);
    }

    public void DecayFrustrationBonus()
    {
        frustration = Mathf.Max(0f, frustration - frustrationDecay);
    }

    void ApplyDeltasWithSoftCap(EmotionDeltaResult d)
    {
        float tgtF = Mathf.Clamp(frustration + d.frustration, 0f, 10f);
        float tgtS = Mathf.Clamp(satisfaction + d.satisfaction, 0f, 10f);

        frustration = SoftCap(frustration, tgtF, softCapPct);
        satisfaction = SoftCap(satisfaction, tgtS, softCapPct);

        // Final clamp to safe range
        frustration = Mathf.Clamp(frustration, 0f, 10f);
        satisfaction = Mathf.Clamp(satisfaction, 0f, 10f);

        Debug.Log($"[Emotion Update] Frustration={frustration:F2}  Satisfaction={satisfaction:F2}");
    }

    float SoftCap(float current, float target, float pct)
    {
        if (target <= current) return target; // Decays are fine
        float maxStep = current + (target - current) * pct;
        return Mathf.Min(target, maxStep);
    }

    public (float fr, float sa) Snapshot() => (frustration, satisfaction);

    public void BreakOutcomeStreaks()
    {
        _streakCommons = _streakRarePlus = 0;
    }
}

public struct EmotionDeltaResult
{
    public float frustration;
    public float satisfaction;
}
