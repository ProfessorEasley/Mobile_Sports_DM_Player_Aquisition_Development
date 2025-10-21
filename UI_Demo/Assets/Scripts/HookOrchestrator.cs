using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Phase 1 Hook Orchestrator
/// Controls when emotional or feedback hooks can trigger,
/// with per-hook cooldowns, session caps, and a global quiet window.
/// Loads optional tuning values from phase1_config.json (in Resources).
/// </summary>
public class HookOrchestrator : MonoBehaviour
{
    public static HookOrchestrator Instance;

    [Header("Global Quiet Window (seconds)")]
    [Tooltip("Range of time during which no new hooks can trigger globally.")]
    public Vector2 quietWindowRange = new Vector2(6f, 8f);

    // --- Internal State ---
    private float _globalQuietUntil;
    private readonly Dictionary<string, float> _cooldownsUntil = new();
    private readonly Dictionary<string, int> _sessionCaps = new();

    // --- Optional Config Data ---
    [Serializable]
    private class HookConfig
    {
        public float cooldown = 5f;
        public int sessionCap = 5;
    }

    [Serializable]
    private class Phase1Config
    {
        public Dictionary<string, HookConfig> hooks = new();
    }

    private Phase1Config _phase1Config = new();

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
        LoadPhase1Config();
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

        // Check global quiet window
        if (now < _globalQuietUntil)
        {
            blockReason = "global_quiet";
            return false;
        }

        // Check per-session cap
        if (_sessionCaps.TryGetValue(hookId, out var used) && used >= sessionCap)
        {
            blockReason = "session_cap";
            return false;
        }

        // Check per-hook cooldown
        if (_cooldownsUntil.TryGetValue(hookId, out var cdUntil) && now < cdUntil)
        {
            blockReason = "cooldown";
            return false;
        }

        // ✅ Fire the hook payload
        payloadAction?.Invoke();

        // Update tracking
        _cooldownsUntil[hookId] = now + cooldownSeconds + UnityEngine.Random.Range(0f, 0.5f);
        _sessionCaps[hookId] = _sessionCaps.TryGetValue(hookId, out var cnt) ? cnt + 1 : 1;

        // Clamp quiet window to safe positive values
        float minQuiet = Mathf.Max(0.5f, quietWindowRange.x);
        float maxQuiet = Mathf.Max(minQuiet, quietWindowRange.y);
        _globalQuietUntil = now + UnityEngine.Random.Range(minQuiet, maxQuiet);

        Debug.Log($"[HookOrchestrator] Fired hook '{hookId}' | Quiet until: {_globalQuietUntil:F2}s");
        return true;
    }

    /// <summary>
    /// Triggered after a pack outcome — manages outcome-based hooks.
    /// </summary>
    public void TryTriggerOutcomeHooks(List<string> rarities)
    {
        float cd = GetCooldownFor("outcome_streak", 5f);
        int cap = GetSessionCapFor("outcome_streak", 5);

        bool fired = TryFireHook(
            hookId: "outcome_streak",
            cooldownSeconds: cd,
            sessionCap: cap,
            payloadAction: () =>
                TelemetryLogger.Instance?.LogHookExecution("outcome_streak", true, null, "outcome"),
            out string reason
        );

        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("outcome_streak", false, reason, "outcome");
    }

    /// <summary>
    /// (For future phases) Example placeholder for frustration-triggered hooks.
    /// </summary>
    public void TryTriggerFrustrationHook()
    {
        float cd = GetCooldownFor("frustration_spike", 6f);
        int cap = GetSessionCapFor("frustration_spike", 3);

        bool fired = TryFireHook(
            hookId: "frustration_spike",
            cooldownSeconds: cd,
            sessionCap: cap,
            payloadAction: () =>
                TelemetryLogger.Instance?.LogHookExecution("frustration_spike", true, null, "emotion"),
            out string reason
        );

        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("frustration_spike", false, reason, "emotion");
    }

    /// <summary>
    /// Resets all cooldowns and caps (called at session restart).
    /// </summary>
    public void ResetHooks()
    {
        _cooldownsUntil.Clear();
        _sessionCaps.Clear();
        _globalQuietUntil = 0f;
        Debug.Log("[HookOrchestrator] Hook state reset.");
    }

    // ------------------------------------------------------------
    // Config Helpers
    // ------------------------------------------------------------
    private void LoadPhase1Config()
    {
        try
        {
            TextAsset cfg = Resources.Load<TextAsset>("phase1_config");
            if (cfg != null)
            {
                _phase1Config = JsonConvert.DeserializeObject<Phase1Config>(cfg.text);

                int count = _phase1Config?.hooks?.Count ?? 0;
                string keys = count > 0 ? string.Join(", ", _phase1Config.hooks.Keys) : "none";
                Debug.Log($"[HookOrchestrator] ✅ Loaded hook config ({count} entries): {keys}");
            }
            else
            {
                Debug.Log("[HookOrchestrator] No phase1_config.json found in Resources – using defaults.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[HookOrchestrator] ❌ Failed to load config: {e.Message}");
        }
    }

    private float GetCooldownFor(string hookId, float fallback)
    {
        if (_phase1Config.hooks != null &&
            _phase1Config.hooks.TryGetValue(hookId, out var hc))
            return Mathf.Max(0.5f, hc.cooldown);
        return fallback;
    }

    private int GetSessionCapFor(string hookId, int fallback)
    {
        if (_phase1Config.hooks != null &&
            _phase1Config.hooks.TryGetValue(hookId, out var hc))
            return Mathf.Max(1, hc.sessionCap);
        return fallback;
    }
}
