using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config;

/// <summary>
/// Phase 1 Telemetry Logger
/// Records pack pulls, pity state, emotional state, and hook executions.
/// Persists to JSON across sessions and can export to CSV using schema from phase1_logging.json.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance;

    private string logFilePath;
    private string csvDirPath;
    private const int MaxLogs = 1000;
    private const int MaxFileSizeKB = 512;

    [Header("Debug")]
    public bool verboseLogging = true;

    public event Action<PackPullLog> OnPullLogged;

    // -------------------------------------------------------------------------
    // Schema + Runtime Cache
    // -------------------------------------------------------------------------
    private Phase1LoggingRoot _loggingSchema;
    private readonly string loggingSchemaPath = Path.Combine(Application.streamingAssetsPath, "phase1_logging.json");

    [Serializable]
    private class LogWrapper { public List<PackPullLog> logs = new(); }
    private LogWrapper cached = new();

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        logFilePath = Path.Combine(dir, "pull_history.json");
        csvDirPath = Path.Combine(dir, "csv_exports");
        if (!Directory.Exists(csvDirPath)) Directory.CreateDirectory(csvDirPath);

        LoadLoggingSchema();
        LoadCachedFile();
    }

    void LoadLoggingSchema()
    {
        try
        {
            if (!File.Exists(loggingSchemaPath))
            {
                Debug.LogWarning($"[Telemetry] No logging schema found at {loggingSchemaPath}");
                return;
            }

            string json = File.ReadAllText(loggingSchemaPath);
            _loggingSchema = JsonConvert.DeserializeObject<Phase1LoggingRoot>(json);
            Debug.Log($"[Telemetry] ✅ Loaded logging schema: {_loggingSchema?.phase_1_log_definitions?.Count ?? 0} definitions");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] ❌ Failed to load schema: {e.Message}");
        }
    }

    void LoadCachedFile()
    {
        if (File.Exists(logFilePath))
        {
            try
            {
                cached = JsonConvert.DeserializeObject<LogWrapper>(File.ReadAllText(logFilePath)) ?? new LogWrapper();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Telemetry] Failed to read log file: {e.Message}");
                cached = new LogWrapper();
            }
        }
        else SaveFile();
    }

    // -------------------------------------------------------------------------
    // MAIN LOGGING API
    // -------------------------------------------------------------------------
    public void LogPull(
        string packTypeKey,
        string packId,
        Cost costFromConfig,
        List<string> rarities,
        int pullsSinceRare,
        int pullsSinceEpic,
        int pullsSinceLegendary,
        bool pityTriggered,
        string pityType)
    {
        try
        {
            if (rarities == null) rarities = new();

            var ev = new PackPullLog
            {
                event_id = $"pull_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}".Substring(0, 30),
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                session_id = PlayerPrefs.GetString("session_id", $"session_{SystemInfo.deviceUniqueIdentifier}_{DateTime.UtcNow:yyyyMMdd}"),
                player_id = PlayerPrefs.GetString("player_id", SystemInfo.deviceUniqueIdentifier),
                player_level = PlayerPrefs.GetInt("player_level", 1),
                pack_info = new PackInfo
                {
                    pack_type = packTypeKey,
                    pack_id = packId,
                    cost_paid = new CostPaid { coins = costFromConfig?.coins ?? 0, gems = costFromConfig?.gems ?? 0 },
                    purchase_method = (costFromConfig?.coins ?? 0) > 0 ? "coins"
                        : (costFromConfig?.gems ?? 0) > 0 ? "gems" : "free"
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

            // Add emotion impact per rarity
            var tiers = DropConfigManager.Instance?.config?.rarity_tiers;
            foreach (var r in rarities)
            {
                int xp = 0;
                EmotionImpact impact = null;

                if (tiers != null && tiers.TryGetValue((r ?? "common").ToLowerInvariant(), out var tier))
                {
                    xp = tier.base_xp;
                    if (tier.emotion_impact != null)
                    {
                        impact = new EmotionImpact
                        {
                            satisfaction = tier.emotion_impact.TryGetValue("satisfaction", out var sa) ? sa : 0f,
                            frustration = tier.emotion_impact.TryGetValue("frustration", out var fr) ? fr : 0f
                        };
                    }
                }

                ev.pull_results.Add(new PullResult
                {
                    rarity = r,
                    xp_gained = xp,
                    emotion_impact = impact
                });
            }

            // Capture emotional snapshot
            if (EmotionalStateManager.Instance != null)
            {
                var (fr, sa) = EmotionalStateManager.Instance.Snapshot();
                ev.emotional_state.satisfaction = sa;
                ev.emotional_state.frustration = fr;
                ev.emotional_state.cumulative_score = sa - fr;
                ev.emotional_state.negative_streaks = fr > sa ? 1 : 0;
            }

            cached.logs.Add(ev);
            if (cached.logs.Count > MaxLogs)
                cached.logs.RemoveRange(0, cached.logs.Count - MaxLogs);

            SaveFile();
            ExportCSVIfSchema(ev);

            if (verboseLogging)
            {
                string rarStr = rarities.Count > 0 ? string.Join(", ", rarities) : "(none)";
                Debug.Log($"[Telemetry] Logged {packTypeKey} → [{rarStr}] | pity={pityTriggered}:{pityType ?? "-"} | logs={cached.logs.Count}");
            }

            OnPullLogged?.Invoke(ev);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Error during LogPull: {e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // HOOK LOGGING
    // -------------------------------------------------------------------------
    private readonly HookExecutionLog hookExecLog = new();

    public void LogHookExecution(string hookId, bool fired, string reasonIfBlocked, string context = null)
    {
        hookExecLog.items.Add(new HookExec
        {
            hook_id = hookId,
            fired = fired,
            reason_if_blocked = reasonIfBlocked,
            context = context,
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        if (verboseLogging)
            Debug.Log($"[Hook] {hookId} fired={fired} reason={reasonIfBlocked ?? "-"} ctx={context}");
    }

    // -------------------------------------------------------------------------
    // CSV EXPORT (new)
    // -------------------------------------------------------------------------
    private void ExportCSVIfSchema(PackPullLog ev)
    {
        if (_loggingSchema?.phase_1_log_definitions == null) return;

        try
        {
            foreach (var def in _loggingSchema.phase_1_log_definitions)
            {
                string csvName = def.csv_name ?? "PHASE_1_GENERIC.csv";
                string csvPath = Path.Combine(csvDirPath, csvName);

                if (!File.Exists(csvPath))
                    File.WriteAllText(csvPath, string.Join(",", def.columns) + "\n");

                var rowValues = new List<string>();
                foreach (var col in def.columns)
                {
                    string val = GetValueForColumn(ev, col);
                    rowValues.Add(val ?? "");
                }

                File.AppendAllText(csvPath, string.Join(",", rowValues) + "\n");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Telemetry CSV] Export failed: {e.Message}");
        }
    }

    private string GetValueForColumn(PackPullLog ev, string column)
    {
        // Minimal mapping subset; expand as needed
        switch (column)
        {
            case "timestamp": return ev.timestamp.ToString();
            case "session_id": return ev.session_id;
            case "player_id": return ev.player_id;
            case "event_type": return "pull";
            case "pack_type": return ev.pack_info?.pack_type;
            case "frustration_after": return ev.emotional_state.frustration.ToString("F2");
            case "satisfaction_after": return ev.emotional_state.satisfaction.ToString("F2");
            default: return "";
        }
    }

    // -------------------------------------------------------------------------
    // FILE SAVE
    // -------------------------------------------------------------------------
    private void SaveFile()
    {
        try
        {
            var json = JsonConvert.SerializeObject(cached, Formatting.Indented);
            if ((System.Text.Encoding.UTF8.GetByteCount(json) / 1024f) > MaxFileSizeKB && cached.logs.Count > 100)
                cached.logs.RemoveRange(0, 100);

            File.WriteAllText(logFilePath, JsonConvert.SerializeObject(cached, Formatting.Indented));
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Write failed: {e.Message}");
        }
    }

    [ContextMenu("Clear Pull History")]
    public void ClearLogFile()
    {
        cached = new LogWrapper();
        SaveFile();
        Debug.Log("🗑 [Telemetry] Cleared pull_history.json");
    }

    public List<PackPullLog> GetRecent(int count)
    {
        if (cached?.logs == null || cached.logs.Count == 0)
            return new List<PackPullLog>();

        count = Mathf.Max(1, count);
        int start = Mathf.Max(0, cached.logs.Count - count);
        return cached.logs.GetRange(start, cached.logs.Count - start);
    }

    // -------------------------------------------------------------------------
    // SUPPORT CLASSES FOR SCHEMA
    // -------------------------------------------------------------------------
    [Serializable]
    public class Phase1LoggingRoot
    {
        public Dictionary<string, LogDefinition> phase_1_logging;
        public List<LogDefinition> phase_1_log_definitions;
    }

    [Serializable]
    public class LogDefinition
    {
        public string csv_name;
        public List<string> columns;
    }

    // -------------------------------------------------------------------------
    // EXISTING DATA CLASSES (unchanged)
    // -------------------------------------------------------------------------
    [Serializable] public class CostPaid { public int coins; public int gems; }

    [Serializable]
    public class PackInfo
    {
        public string pack_type;
        public string pack_id;
        public CostPaid cost_paid;
        public string purchase_method;
    }

    [Serializable]
    public class EmotionImpact { public float satisfaction; public float frustration; }

    [Serializable]
    public class PullResult
    {
        public string rarity;
        public bool is_duplicate;
        public int xp_gained;
        public EmotionImpact emotion_impact;
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

    [Serializable]
    public class HookExec
    {
        public string hook_id;
        public long timestamp;
        public string reason_if_blocked;
        public bool fired;
        public string context;
    }

    [Serializable] public class HookExecutionLog { public List<HookExec> items = new(); }

    [Serializable]
    public class EmotionalState
    {
        public float satisfaction;
        public float frustration;
        public float cumulative_score;
        public int negative_streaks;
    }

    [Serializable]
    public class AcquisitionLoopState
    {
        public string current_phase = "phase_1";
        public float emotional_pacing_score = 0f;
        public string[] trigger_conditions_met = Array.Empty<string>();
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
        public EmotionalState emotional_state = new();
        public AcquisitionLoopState acquisition_loop_state = new();
        public HookExecutionLog hook_execution = new();
    }
}
