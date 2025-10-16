using System;
using System.Collections.Generic;
using UnityEngine;

/// Phase 1 Hook Orchestrator
/// Controls when emotional or feedback hooks can trigger,
/// with per-hook cooldowns, session caps, and a global quiet window.
public class HookOrchestrator : MonoBehaviour
{
    public static HookOrchestrator Instance;

    [Header("Global Quiet Window (seconds)")]
    [Tooltip("Range of time during which no new hooks can trigger globally.")]
    public Vector2 quietWindowRange = new Vector2(6f, 8f);

    // Internal state
    private float _globalQuietUntil;
    private readonly Dictionary<string, float> _cooldownsUntil = new();
    private readonly Dictionary<string, int> _sessionCaps = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Attempts to fire a hook with cooldown and session limits.
    /// </summary>
    public bool TryFireHook(string hookId, float cooldownSeconds, int sessionCap, Action payloadAction, out string blockReason)
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

        // Update internal tracking
        _cooldownsUntil[hookId] = now + cooldownSeconds + UnityEngine.Random.Range(0f, 0.5f);
        _sessionCaps[hookId] = _sessionCaps.TryGetValue(hookId, out var cnt) ? cnt + 1 : 1;
        _globalQuietUntil = now + UnityEngine.Random.Range(quietWindowRange.x, quietWindowRange.y);

        return true;
    }

    /// <summary>
    /// Called after a pack outcome is evaluated — logs success/failure for outcome-based hooks.
    /// </summary>
    public void TryTriggerOutcomeHooks(List<string> rarities)
    {
        bool fired = TryFireHook(
            hookId: "outcome_streak",
            cooldownSeconds: 5f,
            sessionCap: 5,
            payloadAction: () => TelemetryLogger.Instance?.LogHookExecution("outcome_streak", true, null, "outcome"),
            out string reason
        );

        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("outcome_streak", false, reason, "outcome");
    }
}
