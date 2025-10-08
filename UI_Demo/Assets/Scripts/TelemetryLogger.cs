using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config;

public class TelemetryLogger : MonoBehaviour
{
    // ---------- Singleton ----------
    public static TelemetryLogger Instance;

    // ---------- File / limits ----------
    private string logFilePath;
    private const int MaxLogs = 1000;
    private const int MaxFileSizeKB = 512;

    // ---------- Inspector ----------
    [Header("Debug")]
    public bool verboseLogging = true;

    // ---------- Event ----------
    public event Action<PackPullLog> OnPullLogged;

    // ---------- Data shapes ----------
    [Serializable] public class CostPaid { public int coins; public int gems; }

    [Serializable]
    public class PackInfo
    {
        public string pack_type;      // config key, e.g., "gold_pack"
        public string pack_id;        // e.g., "gold_001"
        public CostPaid cost_paid;    // what was actually paid
        public string purchase_method; // "coins" | "gems" | "free" | "reward"
    }

    [Serializable]
    public class EmotionImpact
    {
        public float regret, satisfaction, frustration, excitement;
    }

    [Serializable]
    public class PullResult
    {
        public string card_id = "unknown";
        public string card_name = "unknown";
        public string rarity;
        public bool is_duplicate = false;
        public int xp_gained;
        public int dust_gained = 0;
        public EmotionImpact emotion_impact;    // NEW
    }

    [Serializable]
    public class PityStateLog
    {
        public int pulls_since_rare;
        public int pulls_since_epic;
        public int pulls_since_legendary;
        public bool pity_triggered;
        public string pity_type;
    }
    [Serializable] public class HookExecutionLog { public List<HookExec> items = new List<HookExec>(); }
    [Serializable] public class HookExec
    {
        public string hook_id;
        public long   timestamp;
        public string reason_if_blocked; // null if fired
        public bool   fired;
        public string context;           // optional: "outcome|drought|social"
    }

    // --- MVP hook integration (light stub to match schema, safe to leave empty) ---
    [Serializable] public class HookExecutionRef { public string hook_id; public long timestamp; public string result; }
    [Serializable] public class SessionCaps { public int orientation_ping, safety_cushion, npc_audio_feedback, music_crescendo, impact_accent; }
    [Serializable] public class HookState { public long global_quiet_until; public SessionCaps session_caps_remaining = new SessionCaps(); }
    [Serializable]
    public class HookIntegrationLog
    {
        public List<HookExecutionRef> hooks_triggered_this_session = new List<HookExecutionRef>();
        public HookState current_hook_state = new HookState();
    }
    // ------------------------------------------------------------------------------

    [Serializable]
    public class EmotionalState
    {
        public float regret, satisfaction, frustration, fatigue, social_envy, curiosity, hope, disappointment, motivation, excitement, fomo_anxiety, social_validation;
        public float cumulative_emotion_score; public int negative_streak_duration; public float churn_risk_predictor;
    }

    [Serializable]
    public class AcquisitionLoopState
    {
        public string current_phase = "early_pulls";
        public int phase_duration = 0;
        public string phase_emotional_curve = "tension_build";
        public string[] trigger_conditions_met = Array.Empty<string>();
        public string[] system_responses_applied = Array.Empty<string>();
        public float emotional_pacing_score = 0.0f;
        public int time_since_last_peak = 0;
    }

    [Serializable]
    public class PackPullLog
    {
        public string event_id;
        public long timestamp;
        public string session_id;
        public string player_id;
        public int player_level;

        public PackInfo pack_info;
        public List<PullResult> pull_results;

        public PityStateLog pity_state;
        public EmotionalState emotional_state = new EmotionalState();
        public AcquisitionLoopState acquisition_loop_state = new AcquisitionLoopState();

        public HookIntegrationLog hook_integration = new HookIntegrationLog(); // NEW
    }

    [Serializable] private class LogWrapper { public List<PackPullLog> logs = new List<PackPullLog>(); }
    private LogWrapper cached = new LogWrapper();

    // ---------- Lifecycle ----------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (FindObjectsOfType<TelemetryLogger>().Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        logFilePath = Path.Combine(Application.persistentDataPath, "pull_history.json");

        if (File.Exists(logFilePath))
        {
            try
            {
                cached = JsonConvert.DeserializeObject<LogWrapper>(File.ReadAllText(logFilePath)) ?? new LogWrapper();
            }
            catch { cached = new LogWrapper(); }
        }
        else
        {
            SaveFile();
        }
    }

    // ---------- Public API ----------
    public void LogPull(
        string packTypeKey,
        string packId,
        Cost costFromConfig,
        List<string> rarities,
        int pullsSinceRare,
        int pullsSinceEpic,
        int pullsSinceLegendary,
        bool pityTriggered,
        string pityType
    )
    {
        if (rarities == null) rarities = new List<string>();

        var ev = new PackPullLog
        {
            event_id = $"pull_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            session_id = PlayerPrefs.GetString("session_id", $"session_{SystemInfo.deviceUniqueIdentifier}_{DateTime.UtcNow:yyyyMMdd}"),
            player_id = PlayerPrefs.GetString("player_id", SystemInfo.deviceUniqueIdentifier),
            player_level = PlayerPrefs.GetInt("player_level", 1),

            pack_info = new PackInfo
            {
                pack_type = packTypeKey,
                pack_id = packId,
                cost_paid = new CostPaid { coins = costFromConfig?.coins ?? 0, gems = costFromConfig?.gems ?? 0 },
                purchase_method = (costFromConfig != null && costFromConfig.coins > 0) ? "coins"
                                  : (costFromConfig != null && costFromConfig.gems > 0) ? "gems"
                                  : "free"
            },

            pull_results = new List<PullResult>(),
            pity_state = new PityStateLog
            {
                pulls_since_rare = pullsSinceRare,
                pulls_since_epic = pullsSinceEpic,
                pulls_since_legendary = pullsSinceLegendary,
                pity_triggered = pityTriggered,
                pity_type = pityType
            }
        };

        // rarity -> base_xp and emotion impact from config
        var tiers = DropConfigManager.Instance?.config?.rarity_tiers;
        foreach (var r in rarities)
        {
            int xp = 0;
            EmotionImpact impact = null;

            if (tiers != null && tiers.TryGetValue((r ?? "common").ToLowerInvariant(), out var tier))
            {
                xp = tier.base_xp;

                // map just the four MVP emotions if present
                if (tier.emotion_impact != null)
                {
                    impact = new EmotionImpact
                    {
                        regret = tier.emotion_impact.TryGetValue("regret", out var rg) ? rg : 0f,
                        satisfaction = tier.emotion_impact.TryGetValue("satisfaction", out var sa) ? sa : 0f,
                        frustration = tier.emotion_impact.TryGetValue("frustration", out var fr) ? fr : 0f,
                        excitement = tier.emotion_impact.TryGetValue("excitement", out var ex) ? ex : 0f
                    };
                }
            }

            ev.pull_results.Add(new PullResult { rarity = r, xp_gained = xp, emotion_impact = impact });
        }

        cached.logs.Add(ev);
        if (cached.logs.Count > MaxLogs) cached.logs.RemoveRange(0, cached.logs.Count - MaxLogs);

        SaveFile();

        if (verboseLogging)
        {
            string rarStr = (rarities.Count > 0) ? string.Join(", ", rarities) : "(none)";
            // Debug.Log($"[Telemetry] {packTypeKey} → [{rarStr}] | pity={pityTriggered}:{pityType ?? "-"} | paid c={ev.pack_info.cost_paid.coins} g={ev.pack_info.cost_paid.gems} | logs={cached.logs.Count}");
        }

        OnPullLogged?.Invoke(ev);
    }

    public PackPullLog GetMostRecent()
        => (cached.logs.Count > 0) ? cached.logs[cached.logs.Count - 1] : null;

    public List<PackPullLog> GetRecent(int count)
    {
        count = Mathf.Max(1, count);
        int start = Mathf.Max(0, cached.logs.Count - count);
        return cached.logs.GetRange(start, cached.logs.Count - start);
    }

    public IReadOnlyList<PackPullLog> GetAll() => cached.logs;

    // ---------- Helpers ----------
    private void SaveFile()
    {
        var json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        if ((System.Text.Encoding.UTF8.GetByteCount(json) / 1024f) > MaxFileSizeKB)
        {
            if (cached.logs.Count > 100) cached.logs.RemoveRange(0, 100);
            json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        }
        try { File.WriteAllText(logFilePath, json); }
        catch (Exception e) { Debug.LogError($"[Telemetry] Write failed: {e.Message}"); }
    }

    // ---------- Debug / Tools ----------
    [ContextMenu("Clear Pull History")]
    public void ClearLogFile()
    {
        cached = new LogWrapper();
        SaveFile();
        Debug.Log("🗑 [Telemetry] Cleared pull_history.json");
    }

    [ContextMenu("Dump Most Recent Pull")]
    public void DumpMostRecentToConsole()
    {
        var last = GetMostRecent();
        if (last == null) { Debug.Log("[Telemetry] No pulls logged yet."); return; }
        var rar = new List<string>();
        foreach (var pr in last.pull_results) rar.Add(pr.rarity);
        Debug.Log($"[Telemetry] Last pull → {last.pack_info.pack_type} [{string.Join(", ", rar)}] pity={last.pity_state.pity_triggered}:{last.pity_state.pity_type}");
    }

    

    // Add to PackPullLog if you want per-pull, or keep a session-level log. For simplicity, add session-level:
    private HookExecutionLog hookExecLog = new HookExecutionLog();

    public void LogHookExecution(string hookId, bool fired, string reasonIfBlocked, string context = null)
    {
        hookExecLog.items.Add(new HookExec {
            hook_id = hookId,
            fired   = fired,
            reason_if_blocked = reasonIfBlocked,
            context = context,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        if (verboseLogging)
            Debug.Log($"[Hook] {hookId} fired={fired} reason={reasonIfBlocked ?? "-"} ctx={context}");
    }

}
