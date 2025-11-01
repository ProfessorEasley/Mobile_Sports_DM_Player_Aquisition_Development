using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Simplified Telemetry Logger (Phase 1)
/// Records one log per pack pull with emotional snapshot.
/// Stores data as JSON and exports frustration/satisfaction deltas to CSV.
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

    [Serializable]
    private class LogWrapper { public List<PackPullLog> logs = new(); }
    private LogWrapper cached = new();

    // -------------------------------------------------------------------------
    // Unity Lifecycle
    // -------------------------------------------------------------------------
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        logFilePath = Path.Combine(dir, "pull_history.json");

        csvDirPath = Path.Combine(dir, "csv_exports");
        if (!Directory.Exists(csvDirPath)) Directory.CreateDirectory(csvDirPath);

        LoadCachedFile();
    }

    private void LoadCachedFile()
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
        else
        {
            SaveFile();
        }
    }

    // -------------------------------------------------------------------------
    // MAIN LOGGING API
    // -------------------------------------------------------------------------
    public void LogPull(string packTypeKey, string packName, int packCostCoins, List<string> rarities)
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
                pack_type = packTypeKey,
                pack_name = packName,
                cost_coins = packCostCoins,
                pull_results = new List<string>(rarities)
            };

            // Emotional snapshot (after pull)
            if (EmotionalStateManager.Instance != null)
            {
                var (fr, sa) = EmotionalStateManager.Instance.Snapshot();
                ev.satisfaction_after = sa;
                ev.frustration_after = fr;
            }

            // Save to cache
            cached.logs.Add(ev);
            if (cached.logs.Count > MaxLogs)
                cached.logs.RemoveRange(0, cached.logs.Count - MaxLogs);

            SaveFile();
            ExportEmotionalStateCSV(ev);

            if (verboseLogging)
            {
                Debug.Log($"[Telemetry] Logged {packTypeKey} → [{string.Join(", ", rarities)}] | logs={cached.logs.Count}");
            }

            OnPullLogged?.Invoke(ev);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Telemetry] Error during LogPull: {e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    // HOOK LOGGING (optional, minimal)
    // -------------------------------------------------------------------------
    public void LogHookExecution(string hookId, bool fired, string reasonIfBlocked, string context = null)
    {
        if (verboseLogging)
            Debug.Log($"[Hook] {hookId} fired={fired} reason={reasonIfBlocked ?? "-"} ctx={context}");
    }

    // -------------------------------------------------------------------------
    // CSV EXPORT
    // -------------------------------------------------------------------------
    private void ExportEmotionalStateCSV(PackPullLog ev)
    {
        string csvPath = Path.Combine(csvDirPath, "PHASE_1_EMOTIONAL_STATE_LOG.csv");

        if (!File.Exists(csvPath))
        {
            string header = "log_id,timestamp,session_id,player_id,event_type,frustration_after,satisfaction_after,frustration_delta,satisfaction_delta";
            File.WriteAllText(csvPath, header + "\n");
        }

        var (frAfter, saAfter) = EmotionalStateManager.Instance?.Snapshot() ?? (0f, 0f);
        float frDelta = EmotionalStateManager.Instance?.GetLastFrustrationDelta() ?? 0f;
        float saDelta = EmotionalStateManager.Instance?.GetLastSatisfactionDelta() ?? 0f;

        var rowValues = new List<string>
        {
            ev.event_id,
            ev.timestamp.ToString(),
            ev.session_id,
            ev.player_id,
            "outcome",
            frAfter.ToString("F2"),
            saAfter.ToString("F2"),
            frDelta.ToString("F2"),
            saDelta.ToString("F2")
        };

        File.AppendAllText(csvPath, string.Join(",", rowValues) + "\n");
    }

    // -------------------------------------------------------------------------
    // FILE HANDLING
    // -------------------------------------------------------------------------
    private void SaveFile()
    {
        try
        {
            string json = JsonConvert.SerializeObject(cached, Formatting.Indented);

            if ((System.Text.Encoding.UTF8.GetByteCount(json) / 1024f) > MaxFileSizeKB && cached.logs.Count > 100)
                cached.logs.RemoveRange(0, 100);

            File.WriteAllText(logFilePath, json);
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
    // LOG STRUCTURE
    // -------------------------------------------------------------------------
    [Serializable]
    public class PackPullLog
    {
        public string event_id;
        public long timestamp;
        public string session_id;
        public string player_id;
        public int player_level;

        public string pack_type;
        public string pack_name;
        public int cost_coins;

        public List<string> pull_results; // ✅ simplified to flat rarity list

        public float satisfaction_after;
        public float frustration_after;
    }
}
