using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config;

/// <summary>
/// Loads Phase 1 JSON-based configuration from StreamingAssets and handles randomized card pulls with pity logic.
/// Now uses phase1_config.json as the single source of truth.
/// </summary>
public class DropConfigManager : MonoBehaviour
{
    public static DropConfigManager Instance;

    [Header("Runtime Config (Loaded JSON)")]
    public Phase1ConfigRoot config;   // maps directly to phase1_config.json

    // Per-pack pity counters (session-scoped)
    private class PityState { public int sinceRare, sinceEpic, sinceLegendary; }
    private readonly Dictionary<string, PityState> _pityByPack = new();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfig();
    }

    /// <summary>
    /// Loads phase1_config.json from StreamingAssets.
    /// </summary>
    void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "phase1_config.json");
        if (!File.Exists(path))
        {
            Debug.LogError($"[DropConfig] ❌ Config not found at {path}");
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                FloatParseHandling = FloatParseHandling.Double
            };

            config = JsonConvert.DeserializeObject<Phase1ConfigRoot>(json, settings);
            if (config?.pack_types != null && config.rarity_tiers != null)
            {
                Debug.Log($"[DropConfig] ✅ Loaded {config.pack_types.Count} pack types and {config.rarity_tiers.Count} rarity tiers from phase1_config.json");
            }
            else
            {
                Debug.LogError("[DropConfig] ❌ Missing pack_types or rarity_tiers in phase1_config.json");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[DropConfig] ❌ Failed to load config: {e.Message}");
        }
    }

    /// <summary>
    /// Simulates card pulls for a given pack type.
    /// Handles pity guarantees and logs results to Telemetry.
    /// </summary>
    public List<string> PullCardRarities(string packKey, out bool pityTriggered, out string pityType)
    {
        pityTriggered = false; pityType = null;
        var results = new List<string>();

        if (config == null || config.pack_types == null || !config.pack_types.ContainsKey(packKey))
        {
            Debug.LogError($"[DropConfig] Pack type not found: {packKey}");
            return results;
        }

        var pack = config.pack_types[packKey];
        var rates = pack.drop_rates;
        int pulls = Mathf.Max(1, pack.guaranteed_cards);

        if (!_pityByPack.TryGetValue(packKey, out var pity))
            _pityByPack[packKey] = pity = new PityState();

        for (int i = 0; i < pulls; i++)
        {
            string rarity = RollRarityWithPity(pack, rates, pity, out bool thisPity, out string thisType);
            if (thisPity) { pityTriggered = true; pityType = thisType; }
            results.Add(rarity);
            IncrementPityCounters(pity, rarity);
        }

        // --- Log to Telemetry ---
        TelemetryLogger.Instance?.LogPull(
            packKey,
            pack.pack_id,
            pack.cost,
            results,
            pity.sinceRare,
            pity.sinceEpic,
            pity.sinceLegendary,
            pityTriggered,
            pityType
        );

        return results;
    }

    // ---------- Helper Methods ----------

    string RollRarityWithPity(PackType pack, DropRates r, PityState pity,
                              out bool pityTriggered, out string pityType)
    {
        pityTriggered = false; pityType = null;

        var rules = pack.pity_rules;
        if (rules != null && rules.enabled)
        {
            if (pity.sinceLegendary >= rules.legendary_guarantee_after)
            {
                pityTriggered = true; pityType = "legendary"; return "legendary";
            }
            if (pity.sinceEpic >= rules.epic_guarantee_after)
            {
                pityTriggered = true; pityType = "epic"; return "epic";
            }
            if (pity.sinceRare >= rules.rare_guarantee_after)
            {
                pityTriggered = true; pityType = "rare"; return "rare";
            }
        }

        // Optional: special modifiers
        if (pack.special_modifiers != null && pack.special_modifiers.Contains("guaranteed_rare_minimum"))
        {
            var rolled = WeightedRoll(r);
            if (Order(rolled) < Order("rare")) return "rare";
            return rolled;
        }

        return WeightedRoll(r);
    }

    void IncrementPityCounters(PityState pity, string rarity)
    {
        string t = rarity.ToLowerInvariant();
        pity.sinceRare      = (t == "rare" || t == "epic" || t == "legendary") ? 0 : pity.sinceRare + 1;
        pity.sinceEpic      = (t == "epic" || t == "legendary") ? 0 : pity.sinceEpic + 1;
        pity.sinceLegendary = (t == "legendary") ? 0 : pity.sinceLegendary + 1;
    }

    string WeightedRoll(DropRates rates)
    {
        float c = Mathf.Max(0f, rates.common);
        float uc = Mathf.Max(0f, rates.uncommon);
        float r = Mathf.Max(0f, rates.rare);
        float e = Mathf.Max(0f, rates.epic);
        float l = Mathf.Max(0f, rates.legendary);

        float total = c + uc + r + e + l;
        if (total <= 0f) return "common";

        float roll = UnityEngine.Random.value * total;
        if ((roll -= c) < 0f) return "common";
        if ((roll -= uc) < 0f) return "uncommon";
        if ((roll -= r) < 0f) return "rare";
        if ((roll -= e) < 0f) return "epic";
        return "legendary";
    }

    int Order(string rarity)
    {
        return rarity switch
        {
            "common" => 0,
            "uncommon" => 1,
            "rare" => 2,
            "epic" => 3,
            "legendary" => 4,
            _ => 0
        };
    }
}

/// <summary>
/// Schema root for phase1_config.json
/// </summary>
[Serializable]
public class Phase1ConfigRoot
{
    public string schema_version;
    public Phase1Configuration phase_1_configuration;
    public Dictionary<string, PackType> pack_types;
    public Dictionary<string, RarityTier> rarity_tiers;
}

/// <summary>phase_1_configuration block</summary>
[Serializable]
public class Phase1Configuration
{
    public List<string> tracked_emotions;
    public string session_persistence;
    public float rare_boost_cap;
    public float soft_cap_percentage;
    public float escalation_factor;
    public List<float> global_quiet_window_seconds;
}
