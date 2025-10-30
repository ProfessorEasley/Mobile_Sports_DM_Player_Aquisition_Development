using System;
using System.Collections.Generic;

namespace CCAS.Config
{
    /// <summary>
    /// Matches phase1_simplified_config.json
    /// This is the ONLY data shape we care about in Phase 1 runtime.
    /// </summary>
    [Serializable]
    public class Phase1ConfigRoot
    {
        public string schema_version;
        public string methodology;
        public Phase1Configuration phase_1_configuration;
        public Dictionary<string, RarityValue> rarity_values;
        public Dictionary<string, PackType> pack_types;
    }

    [Serializable]
    public class Phase1Configuration
    {
        public List<string> tracked_emotions;
        public EmotionParameters emotion_parameters;
        public string session_persistence;
    }

    [Serializable]
    public class EmotionParameters
    {
        public float S_max;
        public float F_max;
    }

    [Serializable]
    public class RarityValue
    {
        public int numeric_value;
        public string display_name;
    }

    [Serializable]
    public class PackType
    {
        public string name;
        public int cost; // coins only
        public int guaranteed_cards;
        public DropRates drop_rates;
        public ScoreRange score_range;
    }

    [Serializable]
    public class DropRates
    {
        // any rarity not present in JSON will just default to 0f
        public float common;
        public float uncommon;
        public float rare;
        public float epic;
        public float legendary;
    }

    [Serializable]
    public class ScoreRange
    {
        public int min_score;
        public int max_score;
    }
}
