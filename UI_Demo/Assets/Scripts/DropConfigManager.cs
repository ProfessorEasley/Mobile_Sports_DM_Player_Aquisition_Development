using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using CCAS.Config;


public class DropConfigManager : MonoBehaviour
{
    public static DropConfigManager Instance;

    [Header("Runtime Config (new JSON)")]
    public DropConfigRoot config;  // ← defined in your models file

    // Basic pity state per-pack (kept in-memory per session)
    private class PityState { public int sinceRare, sinceEpic, sinceLegendary; }
    private readonly Dictionary<string, PityState> _pityByPack = new Dictionary<string, PityState>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        LoadConfig();
    }

    void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "ccas_drop_config.json");
        string jsonText = null;

        try
        {
            if (File.Exists(path)) jsonText = File.ReadAllText(path);
            else
            {
                // Fallback: Resources/drop_config (TextAsset)
                var ta = Resources.Load<TextAsset>("drop_config");
                if (ta != null) jsonText = ta.text;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed reading drop config: " + e.Message);
        }

        if (string.IsNullOrEmpty(jsonText))
        {
            Debug.LogError("Drop config JSON not found. Expected StreamingAssets/ccas_drop_config.json");
            return;
        }

        var settings = new JsonSerializerSettings {
            MissingMemberHandling    = MissingMemberHandling.Ignore,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling        = DateParseHandling.None,
            FloatParseHandling       = FloatParseHandling.Double,
            Error = (sender, args) => {
                Debug.LogWarning($"JSON parse warning @ {args.ErrorContext.Path}: {args.ErrorContext.Error.Message}");
                args.ErrorContext.Handled = true; // skip bad nodes instead of crashing
            }
        };

        try {
            config = JsonConvert.DeserializeObject<DropConfigRoot>(jsonText, settings);
        } catch (Exception e) {
            Debug.LogError("Drop config JSON failed to load: " + e.Message);
            return;
        }

        if (config == null || config.pack_types == null || config.rarity_tiers == null)
        {
            Debug.LogError("Drop config JSON is invalid or missing required fields.");
        }
        else
        {
            Debug.Log($"[DropConfig] Loaded {config.pack_types.Count} pack types and {config.rarity_tiers.Count} rarity tiers.");
        }
    }

    public List<string> PullCardRarities(string packKey, out bool pityTriggered, out string pityType)
    {
        pityTriggered = false; pityType = null;

        var results = new List<string>();
        if (config == null || !config.pack_types.ContainsKey(packKey))
        {
            Debug.LogError("Pack type not found: " + packKey);
            return results;
        }

        var pack  = config.pack_types[packKey];
        var rates = pack.drop_rates;
        int pulls = Mathf.Max(1, pack.guaranteed_cards);

        // Prepare pity counters for this pack
        if (!_pityByPack.TryGetValue(packKey, out var pity))
        {
            pity = new PityState();
            _pityByPack[packKey] = pity;
        }

        for (int i = 0; i < pulls; i++)
        {
            string rarity = RollRarityWithPity(pack, rates, pity, out bool thisPullWasPity, out string thisPityType);

            if (thisPullWasPity) { pityTriggered = true; pityType = thisPityType; }
            results.Add(rarity);

            // update pity counters after each result
            IncrementPityCounters(pity, rarity);
        }

        // Log to telemetry
        var logger = FindObjectOfType<TelemetryLogger>();
        if (logger != null)
        {
            logger.LogPull(
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
        }
        else
        {
            Debug.LogWarning("TelemetryLogger not found in scene!");
        }

        return results;
    }

    // ---------- helpers ----------

    string RollRarityWithPity(PackType pack, DropRates r, PityState pity,
                              out bool pityTriggered, out string pityType)
    {
        pityTriggered = false; pityType = null;

        if (pack.pity_rules != null && pack.pity_rules.enabled)
        {
            if (pity.sinceLegendary >= pack.pity_rules.legendary_guarantee_after)
            {
                pityTriggered = true; pityType = "legendary";
                return "legendary";
            }
            if (pity.sinceEpic >= pack.pity_rules.epic_guarantee_after)
            {
                pityTriggered = true; pityType = "epic";
                return "epic";
            }
            if (pity.sinceRare >= pack.pity_rules.rare_guarantee_after)
            {
                pityTriggered = true; pityType = "rare";
                return "rare";
            }
        }

        // Special modifier: guaranteed_rare_minimum
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
        pity.sinceRare      = t == "rare"      || t == "epic" || t == "legendary" ? 0 : pity.sinceRare + 1;
        pity.sinceEpic      = t == "epic"      || t == "legendary" ? 0 : pity.sinceEpic + 1;
        pity.sinceLegendary = t == "legendary" ? 0 : pity.sinceLegendary + 1;
    }

    string WeightedRoll(DropRates rates)
    {
        float c  = Mathf.Max(0f, rates.common);
        float uc = Mathf.Max(0f, rates.uncommon);
        float r  = Mathf.Max(0f, rates.rare);
        float e  = Mathf.Max(0f, rates.epic);
        float l  = Mathf.Max(0f, rates.legendary);

        float total = c + uc + r + e + l;
        if (total <= 0f) return "common";

        float roll = UnityEngine.Random.value * total;
        if ((roll -= c)  < 0f) return "common";
        if ((roll -= uc) < 0f) return "uncommon";
        if ((roll -= r)  < 0f) return "rare";
        if ((roll -= e)  < 0f) return "epic";
        return "legendary";
    }

    int Order(string rarity)
    {
        switch (rarity)
        {
            case "common": return 0;
            case "uncommon": return 1;
            case "rare": return 2;
            case "epic": return 3;
            case "legendary": return 4;
            default: return 0;
        }
    }
}
