using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TelemetryLogger : MonoBehaviour
{
    private string logFilePath;
    private const int MaxLogs = 1000;
    private const int MaxFileSizeKB = 256;

    [Serializable]
    public class PullLogEntry
    {
        public string timestamp;
        public string packType;
        public List<string> cards;
    }

    [Serializable]
    private class PullLogWrapper
    {
        public List<PullLogEntry> logs = new List<PullLogEntry>();
    }

    private PullLogWrapper cachedLogWrapper = new PullLogWrapper();

    void Awake()
    {
        if (FindObjectsOfType<TelemetryLogger>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        logFilePath = Path.Combine(Application.persistentDataPath, "pull_history.json");

        if (!File.Exists(logFilePath))
        {
            File.WriteAllText(logFilePath, JsonUtility.ToJson(cachedLogWrapper, true));
            Debug.Log("📝 Created new log file: " + logFilePath);
        }
        else
        {
            string raw = File.ReadAllText(logFilePath);
            try
            {
                cachedLogWrapper = JsonUtility.FromJson<PullLogWrapper>(raw);
                Debug.Log($"📂 Loaded {cachedLogWrapper.logs.Count} previous logs.");
            }
            catch
            {
                Debug.LogWarning("⚠️ Failed to parse existing log file. Starting fresh.");
                cachedLogWrapper = new PullLogWrapper();
            }
        }
    }

    public void LogPull(string packType, List<string> rarities)
    {
        var newEntry = new PullLogEntry
        {
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            packType = packType,
            cards = rarities
        };

        cachedLogWrapper.logs.Add(newEntry);

        if (cachedLogWrapper.logs.Count > MaxLogs)
            cachedLogWrapper.logs.RemoveRange(0, cachedLogWrapper.logs.Count - MaxLogs);

        string updatedJson = JsonUtility.ToJson(cachedLogWrapper, true); // pretty print

        if ((System.Text.Encoding.UTF8.GetByteCount(updatedJson) / 1024f) > MaxFileSizeKB)
        {
            Debug.LogWarning("⚠️ Log file exceeded 256KB. Trimming first 100 entries.");
            cachedLogWrapper.logs.RemoveRange(0, Math.Min(100, cachedLogWrapper.logs.Count));
            updatedJson = JsonUtility.ToJson(cachedLogWrapper, true);
        }

        File.WriteAllText(logFilePath, updatedJson);
        Debug.Log($"✅ Saved pull log ({cachedLogWrapper.logs.Count} total pulls) → {logFilePath}");
    }
    [ContextMenu("Clear Pull History")]
    public void ClearLogFile()
    {
        if (string.IsNullOrEmpty(logFilePath))
        {
            logFilePath = Path.Combine(Application.persistentDataPath, "pull_history.json");
        }

        cachedLogWrapper = new PullLogWrapper();

        try
        {
            File.WriteAllText(logFilePath, JsonUtility.ToJson(cachedLogWrapper, true));
            Debug.Log("🗑 Cleared pull_history.json at: " + logFilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Failed to clear pull history: " + e.Message);
        }
    }


}
