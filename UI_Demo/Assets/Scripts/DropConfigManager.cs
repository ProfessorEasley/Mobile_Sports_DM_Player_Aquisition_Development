using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class DropConfigManager : MonoBehaviour
{
    public static DropConfigManager Instance;

    public DropConfigWrapper config;
    public List<string> latestPull = new List<string>();
    public List<List<string>> allPullResults = new List<List<string>>();


    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
        LoadConfig();
    }

    void LoadConfig()
    {
        TextAsset json = Resources.Load<TextAsset>("drop_config");
        if (json == null)
        {
            Debug.LogError("Drop config JSON not found!");
            return;
        }

        config = JsonConvert.DeserializeObject<DropConfigWrapper>(json.text);

        // Example: print basic pack info
        if (config.pack_types.TryGetValue("basic_pack", out PackType basicPack))
        {
            Debug.Log("Basic Pack:");
            Debug.Log($"Cost: {basicPack.cost.coins} Coins");
            Debug.Log($"Drop Rates: C={basicPack.drop_rates.common}, R={basicPack.drop_rates.rare}, E={basicPack.drop_rates.epic}, L={basicPack.drop_rates.legendary}");
            Debug.Log($"Pity Rule - Rare after: {basicPack.pity_rules.rare_guarantee_after}");
        }

    }
    public List<string> PullCardRarities(string packKey)
    {
        List<string> results = new List<string>();

        if (!config.pack_types.ContainsKey(packKey))
        {
            Debug.LogError("Pack type not found: " + packKey);
            return results;
        }

        PackType pack = config.pack_types[packKey];
        DropRates rates = pack.drop_rates;
        int pulls = pack.guaranteed_cards;

        for (int i = 0; i < pulls; i++)
        {
            string rarity = GetWeightedRandomRarity(rates);
            results.Add(rarity);
        }

        // Save latest pull
        latestPull = results;

        // Optional: still keep full history if needed
        allPullResults.Add(results);

        return results;
    }


    private string GetWeightedRandomRarity(DropRates rates)
    {
        float roll = UnityEngine.Random.value; // 0.0 to 1.0
        float cumulative = 0f;

        cumulative += rates.common;
        if (roll < cumulative) return "common";

        cumulative += rates.rare;
        if (roll < cumulative) return "rare";

        cumulative += rates.epic;
        if (roll < cumulative) return "epic";

        // fallback
        return "legendary";
    }

}
