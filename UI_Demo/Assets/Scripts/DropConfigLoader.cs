using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

/// <summary>
/// Data models for ccas_drop_config.json / phase1_config.json.
/// Structured for safe deserialization with backward compatibility.
/// </summary>
[Serializable]
public class DropConfigRoot
{
    // --- Primary Game Data ---
    public Dictionary<string, PackType> pack_types;
    public Dictionary<string, RarityTier> rarity_tiers;
    public Dictionary<string, XpTier> xp_progression_system;

    // --- Flexible Emotional Data (kept permissive) ---
    public JToken emotional_acquisition_loop;     // Placeholder for full emotion pipeline
    public JObject emotional_trigger_mapping;     // Permissive (Phase 2+)
    public JToken emotional_state_tracker;        // Placeholder (Phase 2+)
    public JToken emotional_hooks;                // Placeholder (Phase 2+)
}

/// <summary>
/// Defines a pack’s content rules, costs, drop rates, and modifiers.
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
    public List<string> special_modifiers; // Optional
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
/// Emotional modifiers used for outcome-based updates in Phase 1.
/// </summary>
[Serializable]
public class EmotionalModifiers
{
    public float anticipation_boost;
    public float satisfaction_multiplier;
    public float regret_accumulation_rate;
    public int frustration_threshold;
}

/// <summary>
/// Defines per-rarity properties including emotion impact values.
/// Phase 1 only uses satisfaction and frustration.
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

    // Emotion impact dictionary (e.g., {"satisfaction": +2, "frustration": -1})
    public Dictionary<string, float> emotion_impact;
}

/// <summary>
/// Experience and upgrade progression per tier.
/// </summary>
[Serializable]
public class XpTier
{
    public int max_level;
    public int xp_per_level;
    public int total_xp_required;
    public int upgrade_cost_coins;

    // Emotional rewards (Phase 2 use)
    public Dictionary<string, float> emotional_rewards;
}

#region --- Optional Emotional Loop Structures (kept for Phase 2 expansion) ---

[Serializable]
public class EmotionalAcquisitionLoop
{
    public Dictionary<string, AcquisitionPhase> phases;
    public EmotionalPacingGoals emotional_pacing_goals;
}

[Serializable]
public class AcquisitionPhase
{
    public string emotional_goal;
    public string system_feedback;
    public string emotional_curve;
    public int duration_target_seconds;
    public Dictionary<string, float> target_emotions;
}

[Serializable]
public class EmotionalPacingGoals
{
    public float target_positive_ratio;
    public float target_negative_ratio;
    public int time_between_peaks_minutes;
    public int pity_trigger_negative_phases;
}

[Serializable]
public class EmotionalTrigger
{
    public string detection_condition;
    public string system_response;
    public Dictionary<string, float> emotions_affected;
    public Dictionary<string, object> response_parameters;
}

[Serializable]
public class EmotionalStateTracker
{
    public Dictionary<string, EmotionTracked> tracked_emotions;
    public CesCalc ces_calculation;
    public CrpCalc crp_integration;
}

[Serializable]
public class EmotionTracked
{
    public float[] range;
    public string decay_condition;
    public float decay_rate;
}

[Serializable]
public class CesCalc
{
    public string formula;
    public Dictionary<string, float> intervention_thresholds;
}

[Serializable]
public class CrpCalc
{
    public string formula;
    public Dictionary<string, float> intervention_thresholds;
}

[Serializable]
public class EmotionalHook
{
    public string game_response;
    public string description;
}

#endregion
