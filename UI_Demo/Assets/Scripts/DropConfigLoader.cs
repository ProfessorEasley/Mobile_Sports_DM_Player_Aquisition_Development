using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DropConfigWrapper
{
    public Dictionary<string, PackType> pack_types;
    public Dictionary<string, RarityTier> rarity_tiers;
    public DuplicateConversion duplicate_conversion;
}

[Serializable]
public class PackType
{
    public string pack_id;
    public string name;
    public PackCost cost;
    public int guaranteed_cards;
    public DropRates drop_rates;
    public PityRules pity_rules;
    public List<string> special_modifiers;
}

[Serializable]
public class PackCost
{
    public int coins;
    public int gems;
}

[Serializable]
public class DropRates
{
    public float common;
    public float rare;
    public float epic;
    public float legendary;
}

[Serializable]
public class PityRules
{
    public bool enabled;
    public int rare_guarantee_after;
    public int epic_guarantee_after;
    public int legendary_guarantee_after;
}

[Serializable]
public class RarityTier
{
    public string tier_id;
    public string display_name;
    public int base_xp;
    public int dust_value;
    public float upgrade_cost_multiplier;
}

[Serializable]
public class DuplicateConversion
{
    public Dictionary<string, int> xp_conversion_rates;
    public Dictionary<string, int> dust_conversion_rates;
    public int max_card_level;
    public OverflowConversion overflow_conversion;
}

[Serializable]
public class OverflowConversion
{
    public float xp_to_dust_ratio;
    public int dust_cap_per_session;
}
