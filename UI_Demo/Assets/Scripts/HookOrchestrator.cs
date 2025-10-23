using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Phase 1 Hook Orchestrator (StreamingAssets version)
/// Controls when emotional or feedback hooks can trigger,
/// with per-hook cooldowns, session caps, and a global quiet window.
/// Reads tuning values from:
///   - phase1_edge_cases.json  (hook + quiet-window config)
///   - phase1_config.json      (optional fallback)
/// </summary>
public class HookOrchestrator : MonoBehaviour
{
    public static HookOrchestrator Instance;

    [Header("Global Quiet Window (seconds)")]
    public Vector2 quietWindowRange = new Vector2(6f, 8f);

    // --- Internal State ---
    private float _globalQuietUntil;
    private readonly Dictionary<string, float> _cooldownsUntil = new();
    private readonly Dictionary<string, int> _sessionCaps = new();

    // --- Config Models ---
    private Phase1EdgeCasesRoot _edgeCases;

    private readonly string edgeCasesPath = Path.Combine(Application.streamingAssetsPath, "phase1_edge_cases.json");
    private readonly string configPath = Path.Combine(Application.streamingAssetsPath, "phase1_config.json");

    // ------------------------------------------------------------
    // Lifecycle
    // ------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadPhase1EdgeCases();
    }

    // ------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------

    /// <summary>
    /// Attempts to fire a hook with cooldown, session cap, and quiet window checks.
    /// </summary>
    public bool TryFireHook(
        string hookId,
        float cooldownSeconds,
        int sessionCap,
        Action payloadAction,
        out string blockReason)
    {
        float now = Time.time;
        blockReason = null;

        // Global quiet window check
        if (now < _globalQuietUntil)
        {
            blockReason = "global_quiet";
            return false;
        }

        // Per-session cap
        if (_sessionCaps.TryGetValue(hookId, out var used) && used >= sessionCap)
        {
            blockReason = "session_cap";
            return false;
        }

        // Per-hook cooldown
        if (_cooldownsUntil.TryGetValue(hookId, out var cdUntil) && now < cdUntil)
        {
            blockReason = "cooldown";
            return false;
        }

        // ✅ Fire payload
        payloadAction?.Invoke();

        // Track usage
        _cooldownsUntil[hookId] = now + cooldownSeconds + UnityEngine.Random.Range(0f, 0.5f);
        _sessionCaps[hookId] = _sessionCaps.TryGetValue(hookId, out var cnt) ? cnt + 1 : 1;

        // Update quiet window
        float minQuiet = Mathf.Max(0.5f, quietWindowRange.x);
        float maxQuiet = Mathf.Max(minQuiet, quietWindowRange.y);
        _globalQuietUntil = now + UnityEngine.Random.Range(minQuiet, maxQuiet);

        Debug.Log($"[HookOrchestrator] Fired '{hookId}' | Quiet until {_globalQuietUntil:F2}");
        return true;
    }

    public void TryTriggerOutcomeHooks(List<string> rarities)
    {
        float cd = GetCooldownFor("outcome_streak", 5f);
        int cap = GetSessionCapFor("outcome_streak", 5);

        bool fired = TryFireHook(
            "outcome_streak",
            cd,
            cap,
            () => TelemetryLogger.Instance?.LogHookExecution("outcome_streak", true, null, "outcome"),
            out string reason
        );

        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("outcome_streak", false, reason, "outcome");
    }

    public void TryTriggerFrustrationHook()
    {
        float cd = GetCooldownFor("progress_drought", 10f);
        int cap = GetSessionCapFor("progress_drought", 3);

        bool fired = TryFireHook(
            "progress_drought",
            cd,
            cap,
            () => TelemetryLogger.Instance?.LogHookExecution("progress_drought", true, null, "drought"),
            out string reason
        );

        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("progress_drought", false, reason, "drought");
    }

    public void ResetHooks()
    {
        _cooldownsUntil.Clear();
        _sessionCaps.Clear();
        _globalQuietUntil = 0f;
        Debug.Log("[HookOrchestrator] Hook state reset.");
    }

    // ------------------------------------------------------------
    // Config Handling
    // ------------------------------------------------------------
    private void LoadPhase1EdgeCases()
    {
        try
        {
            if (!File.Exists(edgeCasesPath))
            {
                Debug.LogWarning($"[HookOrchestrator] No edge-cases config found at {edgeCasesPath}");
                return;
            }

            string json = File.ReadAllText(edgeCasesPath);
            _edgeCases = JsonConvert.DeserializeObject<Phase1EdgeCasesRoot>(json);

            // Pull quiet-window and escalation data
            if (_edgeCases?.phase_1_hooks?.global_configuration != null)
            {
                var g = _edgeCases.phase_1_hooks.global_configuration;
                if (g.quiet_window_seconds != null && g.quiet_window_seconds.Count == 2)
                    quietWindowRange = new Vector2(g.quiet_window_seconds[0], g.quiet_window_seconds[1]);
            }

            Debug.Log($"[HookOrchestrator] ✅ Loaded edge-case hooks and quiet window: {quietWindowRange.x}-{quietWindowRange.y}s");
        }
        catch (Exception e)
        {
            Debug.LogError($"[HookOrchestrator] ❌ Failed to load: {e.Message}");
        }
    }

    private float GetCooldownFor(string hookId, float fallback)
    {
        if (_edgeCases?.phase_1_hooks != null)
        {
            if (hookId.Contains("outcome") && _edgeCases.phase_1_hooks.outcome_streak_hook != null)
                return 5f; // could read from hook definition if cooldown added later
            if (hookId.Contains("progress") && _edgeCases.phase_1_hooks.progress_drought_hook != null)
                return 10f;
        }
        return fallback;
    }

    private int GetSessionCapFor(string hookId, int fallback)
    {
        // No explicit cap in JSON; keep fallback
        return fallback;
    }
}

// ------------------------------------------------------------
// Helper data models (subset of phase1_edge_cases.json)
// ------------------------------------------------------------
[Serializable]
public class Phase1EdgeCasesRoot
{
    public string schema_version;
    public Phase1Hooks phase_1_hooks;
    public Phase1Guardrails phase_1_guardrails;
}

[Serializable]
public class Phase1Hooks
{
    public Phase1GlobalConfig global_configuration;
    public OutcomeStreakHook outcome_streak_hook;
    public ProgressDroughtHook progress_drought_hook;
}

[Serializable]
public class Phase1GlobalConfig
{
    public float escalation_factor;
    public float soft_cap_percentage;
    public float rare_multiplier_cap;
    public List<float> quiet_window_seconds;
    public bool session_persistence_only;
}

[Serializable]
public class OutcomeStreakHook
{
    public string hook_id;
    public string name;
    public List<string> trigger_events;
}

[Serializable]
public class ProgressDroughtHook
{
    public string hook_id;
    public string name;
    public List<string> trigger_events;
}

[Serializable]
public class Phase1Guardrails
{
    public EmotionBounds emotion_bounds;
}

[Serializable]
public class EmotionBounds
{
    public float min;
    public float max;
}
