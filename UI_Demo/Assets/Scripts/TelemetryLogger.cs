using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config;

public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance;

    private string logFilePath;
    private const int MaxLogs = 1000;
    private const int MaxFileSizeKB = 512;

    [Header("Debug")]
    public bool verboseLogging = true;

    public event Action<PackPullLog> OnPullLogged;

    // ---------- Data Shapes ----------
    [Serializable] public class CostPaid { public int coins; public int gems; }

    [Serializable]
    public class PackInfo
    {
        public string pack_type;        // e.g., "gold_pack"
        public string pack_id;          // e.g., "gold_001"
        public CostPaid cost_paid;      // what was actually paid
        public string purchase_method;  // "coins" | "gems" | "free" | "reward"
    }

    [Serializable]
    public class EmotionImpact
    {
        public float satisfaction;
        public float frustration;
    }

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

    [Serializable]
    public class HookExecutionLog
    {
        public List<HookExec> items = new List<HookExec>();
    }

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
        public EmotionalState emotional_state = new EmotionalState();
        public AcquisitionLoopState acquisition_loop_state = new AcquisitionLoopState();
        public HookExecutionLog hook_execution = new HookExecutionLog();
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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        DontDestroyOnLoad(gameObject);
        logFilePath = Path.Combine(Application.persistentDataPath, "pull_history.json");

        if (File.Exists(logFilePath))
        {
            try
            {
                cached = JsonConvert.DeserializeObject<LogWrapper>(File.ReadAllText(logFilePath)) ?? new LogWrapper();
            }
            catch
            {
                cached = new LogWrapper();
            }
        }
        else SaveFile();
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
        string pityType)
    {
        if (rarities == null) rarities = new List<string>();

        var ev = new PackPullLog
        {
            event_id = $"pull_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..6]}",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            session_id = PlayerPrefs.GetString("session_id", $"session_{SystemInfo.deviceUniqueIdentifier}_{DateTime.UtcNow:yyyyMMdd}"),
            player_id = PlayerPrefs.GetString("player_id", SystemInfo.deviceUniqueIdentifier),
            player_level = PlayerPrefs.GetInt("player_level", 1),
            pack_info = new PackInfo
            {
                pack_type = packTypeKey,
                pack_id = packId,
                cost_paid = new CostPaid { coins = costFromConfig?.coins ?? 0, gems = costFromConfig?.gems ?? 0 },
                purchase_method = (costFromConfig?.coins ?? 0) > 0 ? "coins" :
                                  (costFromConfig?.gems ?? 0) > 0 ? "gems" : "free"
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




        // Map emotion impact from config
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

            ev.pull_results.Add(new PullResult { rarity = r, xp_gained = xp, emotion_impact = impact });
        }

        cached.logs.Add(ev);
        if (cached.logs.Count > MaxLogs)
            cached.logs.RemoveRange(0, cached.logs.Count - MaxLogs);

        SaveFile();

        if (verboseLogging)
        {
            string rarStr = rarities.Count > 0 ? string.Join(", ", rarities) : "(none)";
            Debug.Log($"[Telemetry] {packTypeKey} → [{rarStr}] | pity={pityTriggered}:{pityType ?? "-"} | coins={ev.pack_info.cost_paid.coins} gems={ev.pack_info.cost_paid.gems}");
        }

        OnPullLogged?.Invoke(ev);
    }

    // ---------- Helpers ----------
    private void SaveFile()
    {
        var json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        if ((System.Text.Encoding.UTF8.GetByteCount(json) / 1024f) > MaxFileSizeKB)
        {
            if (cached.logs.Count > 100)
                cached.logs.RemoveRange(0, 100);
            json = JsonConvert.SerializeObject(cached, Formatting.Indented);
        }

        try { File.WriteAllText(logFilePath, json); }
        catch (Exception e) { Debug.LogError($"[Telemetry] Write failed: {e.Message}"); }
    }

    [ContextMenu("Clear Pull History")]
    public void ClearLogFile()
    {
        cached = new LogWrapper();
        SaveFile();
        Debug.Log("🗑 [Telemetry] Cleared pull_history.json");
    }

    // ---------- Hook Logging ----------
    private readonly HookExecutionLog hookExecLog = new HookExecutionLog();

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
    public List<PackPullLog> GetRecent(int count)
    {
        if (cached == null || cached.logs == null || cached.logs.Count == 0)
            return new List<PackPullLog>();

        count = Mathf.Max(1, count);
        int start = Mathf.Max(0, cached.logs.Count - count);
        return cached.logs.GetRange(start, cached.logs.Count - start);
    }
}
