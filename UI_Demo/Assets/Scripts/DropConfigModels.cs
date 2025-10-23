using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace CCAS.Config
{
    /// <summary>
    /// Root configuration structure for pack, rarity, XP, and emotion settings.
    /// Compatible with both ccas_drop_config.json and phase1_config.json.
    /// </summary>
    [Serializable]
    public class DropConfigRoot
    {
        public Dictionary<string, PackType> pack_types;
        public Dictionary<string, RarityTier> rarity_tiers;
        public Dictionary<string, XpTier> xp_progression_system;

        // Flexible emotional / MVP fields (kept permissive for safety)
        public JToken emotional_acquisition_loop;
        public JObject emotional_trigger_mapping;
        public JToken emotional_state_tracker;
        public JToken emotional_hooks;
        public JToken emotional_hook_mvp_integration; // MVP block (optional)
    }

    /// <summary>
    /// Defines a pack's structure, cost, and drop behavior.
    /// </summary>
    [Serializable]
    public class PackType
    {
        public string pack_id;
        public string name;
        public Cost cost;
        public int guaranteed_cards;
        public DropRates drop_rates;
        public PityRules pity_rules;
        public EmotionalModifiers emotional_modifiers;
        public List<string> special_modifiers; // e.g. ["guaranteed_rare_minimum"]
        public HookIntegration hook_integration; // Optional Phase-1 hooks
    }

    [Serializable]
    public class Cost
    {
        public int coins;
        public int gems;
    }

    [Serializable]
    public class DropRates
    {
        public float common, uncommon, rare, epic, legendary;
    }

    [Serializable]
    public class PityRules
    {
        public bool enabled;
        public int rare_guarantee_after;
        public int epic_guarantee_after;
        public int legendary_guarantee_after;
    }

    /// <summary>
    /// Emotional modifiers for Phase-1 adjustments.
    /// </summary>
    [Serializable]
    public class EmotionalModifiers
    {
        public float anticipation_boost = 0f;
        public float satisfaction_multiplier = 1f;
        public float regret_accumulation_rate = 1f;
        public int frustration_threshold = 3;
    }

    /// <summary>
    /// Optional per-pack hook eligibility and feedback settings.
    /// Used for MVP integration and emotion-driven responses.
    /// </summary>
    [Serializable]
    public class HookIntegration
    {
        public int frustration_trigger_threshold = 3;
        public bool satisfaction_hook_eligible = false;
        public string feedback_priority = "medium";
        public bool celebration_enhanced = false;
        public bool music_crescendo_eligible = false;
    }

    /// <summary>
    /// Rarity tiers define the card properties and emotional impact.
    /// Phase-1 only uses satisfaction and frustration impacts.
    /// </summary>
    [Serializable]
    public class RarityTier
    {
        public string tier_id;
        public string display_name;
        public int base_xp;
        public int dust_value;
        public int dupe_xp;
        public int dupe_dust;

        // Emotion impact (e.g. { "satisfaction": 2, "frustration": -1 })
        public Dictionary<string, float> emotion_impact;

        // Optional rarity-level hook triggers (future expansion)
        public RarityHookTriggers hook_triggers;
    }

    [Serializable]
    public class RarityHookTriggers
    {
        public bool streak_frustration;
        public bool safety_cushion_eligible;
        public bool orientation_ping;
        public bool feedback_eligible;
        public bool npc_audio_feedback;
        public bool music_crescendo;
        public bool impact_accent;
        public bool celebration_sequence;
        public bool social_share_prompt;
        public string feedback_priority;
    }

    /// <summary>
    /// Experience and upgrade cost definitions.
    /// </summary>
    [Serializable]
    public class XpTier
    {
        public int max_level;
        public int xp_per_level;
        public int total_xp_required;
        public int upgrade_cost_coins;
        public Dictionary<string, float> emotional_rewards;
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
}
