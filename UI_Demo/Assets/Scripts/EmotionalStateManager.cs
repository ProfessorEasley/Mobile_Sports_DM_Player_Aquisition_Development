using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Phase 1 Emotional State Manager
/// Tracks Frustration & Satisfaction dynamically using the new JSON suite:
///   - phase1_config.json: global constants (EF, soft cap, etc.)
///   - phase1_edge_cases.json: hook + decay formulas
///   - phase1_persona.json: per-persona sensitivity and thresholds
/// </summary>
public class EmotionalStateManager : MonoBehaviour
{
    public static EmotionalStateManager Instance;

    [Header("Global Config (Loaded JSON)")]
    public float EF = 1.2f;
    public float softCapPct = 0.85f;
    public float rareBoostCap = 1.20f;
    public float baseFrustration = 5f;
    public float baseSatisfaction = 2f;
    public float baseDrought = 3f;
    public float frustrationDecay = 1.5f;
    public float satisfactionDecay = 2.0f;

    [Header("Persona Tuning (Optional)")]
    public string activePersona = "f2p_casual";
    public float personaFrustrationSensitivity = 1.0f;
    public float personaSatisfactionSensitivity = 1.0f;
    public int personaStreakThreshold = 2;

    [Header("Emotions (0–10)")]
    [Range(0, 10)] public float frustration;
    [Range(0, 10)] public float satisfaction;

    // Internal counters
    private int _streakCommons;
    private int _streakRarePlus;
    private int _droughtCounter;
    private float _tsLastProgress;
    private float _tsLastAny;
    private int _sessionsWithoutShareOrRare = 0;

    // Paths
    private readonly string pathConfig = Path.Combine(Application.streamingAssetsPath, "phase1_config.json");
    private readonly string pathEdge = Path.Combine(Application.streamingAssetsPath, "phase1_edge_cases.json");
    private readonly string pathPersona = Path.Combine(Application.streamingAssetsPath, "phase1_persona.json");

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadPhase1Config();
        LoadPersonaConfig(activePersona);
        ResetSession();
    }

    // ----------------------------------------------------
    // JSON LOADERS
    // ----------------------------------------------------
    void LoadPhase1Config()
    {
        try
        {
            if (!File.Exists(pathConfig))
            {
                Debug.LogError($"[Phase1Config] Missing: {pathConfig}");
                return;
            }

            string json = File.ReadAllText(pathConfig);
            var root = JsonConvert.DeserializeObject<Phase1ConfigRoot>(json);

            if (root?.phase_1_configuration != null)
            {
                var cfg = root.phase_1_configuration;
                EF = cfg.escalation_factor;
                softCapPct = cfg.soft_cap_percentage;
                rareBoostCap = cfg.rare_boost_cap;

                Debug.Log($"[Phase1Config] ✅ Loaded global config: EF={EF}, softCap={softCapPct}, rareBoost={rareBoostCap}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Phase1Config] ❌ Load failed: {e.Message}");
        }
    }

    void LoadPersonaConfig(string personaKey)
    {
        try
        {
            if (!File.Exists(pathPersona))
            {
                Debug.LogWarning($"[PersonaConfig] Missing: {pathPersona}");
                return;
            }

            string json = File.ReadAllText(pathPersona);
            var personaRoot = JsonConvert.DeserializeObject<Phase1PersonaRoot>(json);
            if (personaRoot?.phase_1_personas != null && personaRoot.phase_1_personas.ContainsKey(personaKey))
            {
                var persona = personaRoot.phase_1_personas[personaKey];
                personaFrustrationSensitivity = persona.phase_1_emotional_profile.frustration.base_sensitivity;
                personaSatisfactionSensitivity = persona.phase_1_emotional_profile.satisfaction.base_sensitivity;
                personaStreakThreshold = persona.phase_1_emotional_profile.frustration.streak_threshold;

                Debug.Log($"[PersonaConfig] ✅ Loaded persona '{persona.display_name}' — FrSens={personaFrustrationSensitivity}, SaSens={personaSatisfactionSensitivity}, Threshold={personaStreakThreshold}");
            }
            else
            {
                Debug.LogWarning($"[PersonaConfig] Persona not found: {personaKey}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PersonaConfig] ❌ Load failed: {e.Message}");
        }
    }

    // ----------------------------------------------------
    // SESSION CONTROL
    // ----------------------------------------------------
    public void ResetSession()
    {
        frustration = satisfaction = 0f;
        _streakCommons = _streakRarePlus = 0;
        _droughtCounter = 0;
        _tsLastProgress = _tsLastAny = Time.time;
        _sessionsWithoutShareOrRare = 0;
    }

    // ----------------------------------------------------
    // EVENT HANDLERS
    // ----------------------------------------------------
    public EmotionDeltaResult HandleOutcomeEvent(List<string> rarities, bool pity, string pityType)
    {
        _tsLastAny = Time.time;

        bool hasRarePlus = rarities.Exists(r =>
        {
            string k = (r ?? "common").ToLowerInvariant();
            return k == "rare" || k == "epic" || k == "legendary";
        });

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

        // Frustration increase for common streaks
        if (_streakCommons >= personaStreakThreshold)
        {
            float efPow = Mathf.Pow(EF, _streakCommons - 1);
            deltas.frustration += baseFrustration * personaFrustrationSensitivity * efPow;
        }

        // Satisfaction increase for Rare+ pulls
        if (hasRarePlus)
        {
            float efPow = Mathf.Pow(EF, Mathf.Max(0, _streakRarePlus - 1));
            float delta = baseSatisfaction * personaSatisfactionSensitivity * efPow;
            deltas.satisfaction += ApplyRarePlusCap(delta);
        }

        ApplyPhase1Decay(hasRarePlus);
        ApplyDeltasWithSoftCap(deltas);

        return deltas;
    }

    public EmotionDeltaResult OnDroughtTick()
    {
        _tsLastAny = Time.time;
        _droughtCounter++;

        var deltas = new EmotionDeltaResult();
        float efPow = Mathf.Pow(EF, Mathf.Max(0, _droughtCounter - 1));
        deltas.frustration += baseDrought * personaFrustrationSensitivity * efPow;

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

        frustration = Mathf.Clamp(frustration, 0f, 10f);
        satisfaction = Mathf.Clamp(satisfaction, 0f, 10f);

        Debug.Log($"[Emotion Update] Frustration={frustration:F2}  Satisfaction={satisfaction:F2}");
    }

    float SoftCap(float current, float target, float pct)
    {
        if (target <= current) return target;
        float maxStep = current + (target - current) * pct;
        return Mathf.Min(target, maxStep);
    }

    public (float fr, float sa) Snapshot() => (frustration, satisfaction);

    public void BreakOutcomeStreaks() => _streakCommons = _streakRarePlus = 0;
}

public struct EmotionDeltaResult
{
    public float frustration;
    public float satisfaction;
}

/// <summary>
/// Persona schema partial (subset of phase1_persona.json)
/// </summary>
[Serializable]
public class Phase1PersonaRoot
{
    public Dictionary<string, Phase1Persona> phase_1_personas;
}

[Serializable]
public class Phase1Persona
{
    public string display_name;
    public PersonaEmotionalProfile phase_1_emotional_profile;
}

[Serializable]
public class PersonaEmotionalProfile
{
    public PersonaEmotion frustration;
    public PersonaEmotion satisfaction;
}

[Serializable]
public class PersonaEmotion
{
    public float base_sensitivity;
    public int streak_threshold;
    public float max_tolerance;
}
