# Emotion System Changelog

## Version 2.1 - Balanced Edition (Current)

### Major Changes

#### 1. Base Delta Formula - FIXED

- **Removed:** Centered formula `(quality01 - 0.5) * (2 * Smax)` that created dead zones
- **Added:** Asymmetric power curves:
  - Satisfaction: `quality01^0.7 * Smax` (easier to gain)
  - Frustration: `(1-quality01)^1.2 * Fmax` (harder to gain)
- **Impact:** Satisfaction can now build from 0, rare cards give meaningful rewards

#### 2. Rare Card Boost - NEW FEATURE

- **Added:** 50% satisfaction boost when rare/epic/legendary cards appear
- **Impact:** Rare cards feel immediately rewarding and exciting

#### 3. Quality Reset Logic - FIXED

- **Removed:** Bad pulls reducing satisfaction further (causing collapse)
- **Changed:**
  - Good pulls: Proportionally reduce frustration based on quality
  - Bad pulls: Reduce frustration GAIN by up to 30% (less punishment, not more)
- **Impact:** Satisfaction no longer collapses, bad pulls are less punishing

#### 4. Oppositional Dampening - REDUCED

- **Changed:** Impact reduced by 70% (k = 0.25 → effective k = 0.075)
- **Impact:** Prevents feedback loops, emotions coexist more naturally

#### 5. Streak System - BALANCED

- **Changed:**
  - Hot streaks: Full satisfaction boost, moderate frustration reduction
  - Cold streaks: Reduced punishment (50% less satisfaction loss, 30% less frustration gain)
  - Overall multipliers reduced by 50%
- **Impact:** Bad streaks are recoverable, good streaks feel rewarding

#### 6. Decay Order - FIXED

- **Changed:** Decay now happens BEFORE adding deltas (was after)
- **Changed:** Decay rates: 2% satisfaction, 3% frustration (was 0.5% and 1.5%)
- **Impact:** Satisfaction can accumulate over multiple good pulls

### Configuration Changes

- `quality_reset.R_S`: No longer used in bad pull path
- `quality_reset.R_F`: Now only reduces frustration on good pulls
- `oppositional.k`: Effective value is 30% of config value
- `streak.alpha/beta`: Effective values are 50% of config values

### Code Structure

The calculation pipeline now follows this order:

1. Raw score calculation
2. Normalization
3. Pack-type bias
4. Base deltas (asymmetric curves)
5. Rare card boost
6. Quality-driven reduction
7. Neutral-band recovery
8. Oppositional dampening (reduced)
9. Streak multiplier (balanced)
10. Decay application (before deltas)
11. Delta application and clamping
12. Rolling window update

---

## Version 2.0 - Initial Part 2

### Features Added

- Quality-driven reduction (dynamic reset)
- Neutral-band recovery
- Oppositional dampening
- Streak multiplier
- Rolling window for streak calculations

### Issues Found

- Base delta formula created dead zones
- Satisfaction couldn't build from 0
- Rare cards didn't feel rewarding
- Bad pulls were too punishing
- Oppositional created feedback loops
- Streaks were too aggressive
- Decay order prevented accumulation

---

## Version 1.0 - Part 1

### Initial Implementation

- Basic normalized quality scoring
- Simple satisfaction/frustration deltas
- Pack score ranges
- Rarity numeric values

---

## Migration Notes

### For Developers

- Update any code that assumes satisfaction can go negative
- Update any code that expects old base delta formula
- Rare boost is automatic, no configuration needed
- Oppositional and streak multipliers are reduced in code

### For Designers

- Test with new system to ensure balance feels right
- Adjust `S_max` and `F_max` if satisfaction/frustration build too fast/slow
- Adjust rare boost multiplier (currently 1.5) if needed
- Adjust decay rates (currently 0.98 and 0.97) if needed

### For QA

- Test edge cases: all commons, all rares, mixed pulls
- Test streak scenarios: hot streaks, cold streaks, recovery
- Verify satisfaction builds over multiple good pulls
- Verify frustration is recoverable with good pulls
