using System;
using System.Collections.Generic;
using UnityEngine;

/// Minimal MVP orchestrator: one hook at a time, per-hook cooldowns, global quiet window 6–8s.
public class HookOrchestrator : MonoBehaviour
{
    public static HookOrchestrator Instance;

    [Header("Quiet Window (seconds)")]
    public Vector2 quietWindowRange = new Vector2(6f, 8f);

    float _globalQuietUntil;
    readonly Dictionary<string, float> _cooldownsUntil = new Dictionary<string, float>();
    readonly Dictionary<string, int> _sessionCaps = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TryFireHook(string hookId, float cooldownSeconds, int sessionCap, Action payloadAction, out string blockReason)
    {
        float now = Time.time;
        blockReason = null;

        if (now < _globalQuietUntil) { blockReason = "global_quiet"; return false; }

        if (_sessionCaps.TryGetValue(hookId, out var used) && used >= sessionCap) { blockReason = "session_cap"; return false; }

        if (_cooldownsUntil.TryGetValue(hookId, out var cd) && now < cd) { blockReason = "cooldown"; return false; }

        // fire!
        payloadAction?.Invoke();

        // set cooldown + caps + quiet window
        _cooldownsUntil[hookId] = now + cooldownSeconds + UnityEngine.Random.Range(0f, 0.5f);
        _sessionCaps[hookId] = (_sessionCaps.TryGetValue(hookId, out var cnt) ? cnt + 1 : 1);
        _globalQuietUntil = now + UnityEngine.Random.Range(quietWindowRange.x, quietWindowRange.y);

        return true;
    }
    public void TryTriggerOutcomeHooks(List<string> rarities)
    {
        bool fired = TryFireHook("outcome_streak", 5f, 5, 
            () => TelemetryLogger.Instance?.LogHookExecution("outcome_streak", true, null, "outcome"),
            out string reason);
        if (!fired)
            TelemetryLogger.Instance?.LogHookExecution("outcome_streak", false, reason, "outcome");
    }
}

