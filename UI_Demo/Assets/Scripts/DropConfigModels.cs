using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CCAS.Config
{
    [Serializable]
    public class DropConfigRoot
    {
        public Dictionary<string, PackType> pack_types;
        public Dictionary<string, RarityTier> rarity_tiers;
        public Dictionary<string, XpTier>    xp_progression_system;

        // Permissive nodes we don't consume yet
        public JToken  emotional_acquisition_loop;
        public JObject emotional_trigger_mapping;
        public JToken  emotional_state_tracker;
        public JToken  emotional_hooks;
    }

    [Serializable] public class PackType {
        public string pack_id;
        public string name;
        public Cost cost;
        public int   guaranteed_cards;
        public DropRates drop_rates;
        public PityRules pity_rules;
        public EmotionalModifiers emotional_modifiers;
        public List<string> special_modifiers;
    }

    [Serializable] public class Cost { public int coins; public int gems; }

    [Serializable] public class DropRates {
        public float common, uncommon, rare, epic, legendary;
    }

    [Serializable] public class PityRules {
        public bool enabled;
        public int rare_guarantee_after, epic_guarantee_after, legendary_guarantee_after;
    }

    [Serializable] public class EmotionalModifiers {
        public float anticipation_boost, satisfaction_multiplier, regret_accumulation_rate;
        public int frustration_threshold;
    }

    [Serializable] public class RarityTier {
        public string tier_id;
        public string display_name;
        public int base_xp, dust_value, dupe_xp, dupe_dust;
        public Dictionary<string, float> emotion_impact;
    }

    [Serializable] public class XpTier {
        public int max_level, xp_per_level, total_xp_required, upgrade_cost_coins;
        public Dictionary<string, float> emotional_rewards;
    }
}
