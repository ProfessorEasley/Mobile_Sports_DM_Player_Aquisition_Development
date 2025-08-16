using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config; // for Cost + access to DropConfigManager.config.rarity_tiers

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
    [Serializable] public class PackInfo {
        public string pack_type;      // e.g., "bronze_pack" (config key)
        public string pack_id;        // e.g., "BRZ01" (from JSON)
        public CostPaid cost_paid;    // what the player actually paid
        public string purchase_method; // "coins" | "gems" | "free"
    }

    [Serializable] public class PullResult
    {
        public string card_id  = "unknown"; // placeholder until you wire a real card DB
        public string card_name = "unknown";
        public string rarity;               // common/uncommon/rare/epic/legendary
        public bool   is_duplicate = false; // placeholder
        public int    xp_gained;            // from rarity_tiers.base_xp
        public int    dust_gained = 0;      // placeholder
    }

    [Serializable] public class PityStateLog {
        public int pulls_since_rare;
        public int pulls_since_epic;
        public int pulls_since_legendary;
        public bool pity_triggered;
        public string pity_type; // "rare" | "epic" | "legendary" | null
    }

    [Serializable] public class EmotionalState {
        // schema placeholders (kept for shape compatibility)
        public float regret, satisfaction, frustration, fatigue, social_envy, curiosity, hope, disappointment, motivation, excitement, fomo_anxiety, social_validation;
        public float cumulative_emotion_score; public int negative_streak_duration; public float churn_risk_predictor;
    }

    [Serializable] public class AcquisitionLoopState {
        public string current_phase = "early_pulls";
        public int    phase_duration = 0;
        public string phase_emotional_curve = "tension_build";
        public string[] trigger_conditions_met = Array.Empty<string>();
        public string[] system_responses_applied = Array.Empty<string>();
        public float emotional_pacing_score = 0.0f;
        public int   time_since_last_peak = 0;
    }

    [Serializable]
    public class PackPullLog
    {
        public string event_id;     // unique id for the pull
        public long   timestamp;    // unix ms
        public string session_id;
        public string player_id;
        public int    player_level;

        public PackInfo pack_info;
        public List<PullResult> pull_results;

        public PityStateLog pity_state;
        public EmotionalState emotional_state = new EmotionalState();
        public AcquisitionLoopState acquisition_loop_state = new AcquisitionLoopState();
    }

    [Serializable]
    private class LogWrapper
    {
        public List<PackPullLog> logs = new List<PackPullLog>();
    }

    private LogWrapper cached = new LogWrapper();

    // ---------- Lifecycle ----------
    void Awake()
    {
        // singleton
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // avoid duplicates across scenes
        if (FindObjectsOfType<TelemetryLogger>().Length > 1) { Destroy(gameObject); return; }
        DontDestroyOnLoad(gameObject);

        logFilePath = Path.Combine(Application.persistentDataPath, "pull_history.json");

        // load existing
        if (File.Exists(logFilePath))
        {
            try
            {
                cached = JsonConvert.DeserializeObject<LogWrapper>(File.ReadAllText(logFilePath)) ?? new LogWrapper();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] Failed to parse existing log file: {e.Message}. Starting fresh.");
                cached = new LogWrapper();
            }
        }
        else
        {
            SaveFile(); // create empty file
        }
    }

    // ---------- Public API ----------
    /// <summary>Append a new pull to the log.</summary>
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
            event_id   = $"pull_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0,6)}",
            timestamp  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            session_id = PlayerPrefs.GetString("session_id", $"session_{SystemInfo.deviceUniqueIdentifier}_{DateTime.UtcNow:yyyyMMdd}"),
            player_id  = PlayerPrefs.GetString("player_id", SystemInfo.deviceUniqueIdentifier),
            player_level = PlayerPrefs.GetInt("player_level", 1),

            pack_info = new PackInfo
            {
                pack_type = packTypeKey,
                pack_id   = packId,
                cost_paid = new CostPaid { coins = costFromConfig?.coins ?? 0, gems = costFromConfig?.gems ?? 0 },
                purchase_method = (costFromConfig != null && costFromConfig.coins > 0) ? "coins"
                                  : (costFromConfig != null && costFromConfig.gems  > 0) ? "gems"
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

        // rarity -> base_xp mapping from config
        var tiers = DropConfigManager.Instance?.config?.rarity_tiers;
        foreach (var r in rarities)
        {
            int xp = 0;
            if (tiers != null && tiers.TryGetValue((r ?? "common").ToLowerInvariant(), out var tier))
                xp = tier.base_xp;

            ev.pull_results.Add(new PullResult { rarity = r, xp_gained = xp });
        }

        cached.logs.Add(ev);
        if (cached.logs.Count > MaxLogs)
            cached.logs.RemoveRange(0, cached.logs.Count - MaxLogs);

        SaveFile();

        if (verboseLogging)
        {
            string rarStr = (rarities.Count > 0) ? string.Join(", ", rarities) : "(none)";
            Debug.Log($"[Telemetry] {packTypeKey} → [{rarStr}] | pity={pityTriggered}:{pityType ?? "-"} | paid: coins={ev.pack_info.cost_paid.coins}, gems={ev.pack_info.cost_paid.gems} | total logs={cached.logs.Count}");
        }

        OnPullLogged?.Invoke(ev);
    }

    /// <summary>Most recent logged pull, or null.</summary>
    public PackPullLog GetMostRecent()
    {
        return (cached.logs.Count > 0) ? cached.logs[cached.logs.Count - 1] : null;
    }

    /// <summary>Last N pulls (N >= 1). Returns empty list if none.</summary>
    public List<PackPullLog> GetRecent(int count)
    {
        count = Mathf.Max(1, count);
        int start = Mathf.Max(0, cached.logs.Count - count);
        return cached.logs.GetRange(start, cached.logs.Count - start);
    }

    /// <summary>All pulls (read-only).</summary>
    public IReadOnlyList<PackPullLog> GetAll() => cached.logs;

    // ---------- Helpers ----------
    private void SaveFile()
    {
        var json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        // size guard
        if ((System.Text.Encoding.UTF8.GetByteCount(json) / 1024f) > MaxFileSizeKB)
        {
            // trim oldest 100 entries and recompute
            if (cached.logs.Count > 100)
                cached.logs.RemoveRange(0, 100);
            json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        }

        try
        {
            File.WriteAllText(logFilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Failed to write log file: {e.Message}");
        }
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
}
