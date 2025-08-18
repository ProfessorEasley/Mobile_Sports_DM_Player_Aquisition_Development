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

        // Keep emotional / MVP bits permissive so schema tweaks don't break us
        public JToken  emotional_acquisition_loop;
        public JObject emotional_trigger_mapping;
        public JToken  emotional_state_tracker;
        public JToken  emotional_hooks;

        // New in Archita’s drop config
        public JToken  emotional_hook_mvp_integration;  // tolerant capture of the MVP block
    }

    [Serializable]
    public class PackType {
        public string pack_id;
        public string name;
        public Cost cost;
        public int   guaranteed_cards;
        public DropRates drop_rates;
        public PityRules pity_rules;
        public EmotionalModifiers emotional_modifiers;
        public List<string> special_modifiers;          // e.g. ["guaranteed_rare_minimum"]
        public HookIntegration hook_integration;        // NEW (from ccas_drop_config.json)
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

    // NEW: per-pack hook eligibility/settings
    [Serializable] public class HookIntegration {
        public int    frustration_trigger_threshold;   // e.g., 3
        public bool   satisfaction_hook_eligible;      // e.g., true/false
        public string feedback_priority;               // "low|medium|high|premium" etc.
        public bool   celebration_enhanced;            // optional
        public bool   music_crescendo_eligible;        // optional
    }

    [Serializable]
    public class RarityTier {
        public string tier_id;
        public string display_name;
        public int base_xp, dust_value, dupe_xp, dupe_dust;
        public Dictionary<string, float> emotion_impact;   // regret/satisfaction/etc.

        // NEW: hook trigger helpers present in JSON (fields are optional)
        public RarityHookTriggers hook_triggers;
    }

    // Covers all keys we’ve seen across tiers; missing ones just default false/null
    [Serializable]
    public class RarityHookTriggers {
        public bool streak_frustration;
        public bool safety_cushion_eligible;

        public bool orientation_ping;
        public bool feedback_eligible;

        public bool npc_audio_feedback;
        public bool music_crescendo;
        public bool impact_accent;

        public bool celebration_sequence;
        public bool social_share_prompt;

        public string feedback_priority; // some tiers specify this as a string
    }

    [Serializable] public class XpTier {
        public int max_level, xp_per_level, total_xp_required, upgrade_cost_coins;
        public Dictionary<string, float> emotional_rewards;
    }
}
