using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;


// Data models for ccas_drop_config.json (subset used at runtime)
[Serializable]
public class DropConfigRoot
{
    public Dictionary<string, PackType> pack_types;
    public Dictionary<string, RarityTier> rarity_tiers;
    public Dictionary<string, XpTier>    xp_progression_system;

    // Make emotional stuff permissive so strings like "maintain" don't break deserialization
    public JToken  emotional_acquisition_loop;       // was EmotionalAcquisitionLoop
    public JObject emotional_trigger_mapping;        // was Dictionary<string, EmotionalTrigger>
    public JToken  emotional_state_tracker;          // was EmotionalStateTracker
    public JToken  emotional_hooks;                  // was Dictionary<string, EmotionalHook>
}


[Serializable] public class PackType {
    public string pack_id;
    public string name;
    public Cost cost;
    public int   guaranteed_cards;
    public DropRates drop_rates;
    public PityRules pity_rules;
    public EmotionalModifiers emotional_modifiers;
    public List<string> special_modifiers; // optional
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

// Light shapes for future emotional integrations
[Serializable] public class EmotionalAcquisitionLoop {
    public Dictionary<string, AcquisitionPhase> phases;
    public EmotionalPacingGoals emotional_pacing_goals;
}
[Serializable] public class AcquisitionPhase {
    public string emotional_goal, system_feedback, emotional_curve;
    public int duration_target_seconds;
    public Dictionary<string, float> target_emotions;
}
[Serializable] public class EmotionalPacingGoals {
    public float target_positive_ratio, target_negative_ratio;
    public int time_between_peaks_minutes, pity_trigger_negative_phases;
}
[Serializable] public class EmotionalTrigger {
    public string detection_condition, system_response;
    public Dictionary<string, float> emotions_affected;
    public Dictionary<string, object> response_parameters;
}
[Serializable] public class EmotionalStateTracker {
    public Dictionary<string, EmotionTracked> tracked_emotions;
    public CesCalc ces_calculation;
    public CrpCalc crp_integration;
}
[Serializable] public class EmotionTracked {
    public float[] range; public string decay_condition; public float decay_rate;
}
[Serializable] public class CesCalc {
    public string formula; public Dictionary<string, float> intervention_thresholds;
}
[Serializable] public class CrpCalc {
    public string formula; public Dictionary<string, float> intervention_thresholds;
}
[Serializable] public class EmotionalHook {
    public string game_response, description;
}
