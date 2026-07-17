# Attunement System (Slices 0.8.1 - 0.8.6)

This document describes the attunement system implemented in:
- Slice 0.8.1 (Data Foundation)
- Slice 0.8.2 (Core Stats)
- Slice 0.8.3 (Attunement Stones V1)
- Slice 0.8.4 (Attunement Table V1)
- Slice 0.8.5 (Failure, Pity & Protection V1)
- Slice 0.8.6 (Attunement History V1)

## Overview

The attunement system provides equipment progression through attunement levels:
- Each equipment instance has an attunement level (0 to 10)
- Attunement level belongs to the specific equipment instance, not the definition
- Display format: "Traveler Blade +0" to "Traveler Blade +10"
- **Attunement affects combat stats** - higher levels provide damage bonuses
- Attunement preserved through equip/unequip, drop/pickup, repair, condition changes
- New equipment spawned via CloneWithNewId resets to level 0

## Stat Resolution Pipeline

Attunement bonuses are applied in the stat resolution pipeline:

```
Base → Affixes → ATTUNEMENT → Broken Penalties → Clamp
```

Example calculation:
```
Base:       100 damage
Affix:      +20% (Sharp)  → 100 × 1.20 = 120 damage
Attunement: +2 (×1.08)    → 120 × 1.08 = 129.6 damage
Broken:     ×0.30         → 129.6 × 0.30 = 38.88 damage
Clamp:      min 1         → 38.88 damage (final)
```

## Attunement Multipliers

| Level | Damage | Speed | Range | Stagger |
|-------|--------|-------|-------|---------|
| +0 | ×1.00 | ×1.00 | ×1.00 | ×1.00 |
| +1 | ×1.04 | ×1.00 | ×1.00 | ×1.00 |
| +2 | ×1.08 | ×1.00 | ×1.00 | ×1.00 |
| +3 | ×1.13 | ×1.00 | ×1.00 | ×1.00 |
| +4 | ×1.18 | ×1.00 | ×1.00 | ×1.00 |
| +5 | ×1.24 | ×1.00 | ×1.00 | ×1.00 |
| +6 | ×1.30 | ×1.00 | ×1.00 | ×1.00 |
| +7 | ×1.37 | ×1.00 | ×1.00 | ×1.00 |
| +8 | ×1.44 | ×1.00 | ×1.00 | ×1.00 |
| +9 | ×1.52 | ×1.00 | ×1.00 | ×1.00 |
| +10 | ×1.60 | ×1.00 | ×1.00 | ×1.00 |

Only Damage differs for V1. Structure supports all stats for future (per-weapon personality).

## V1 Scope

✅ Implemented:
- AttunementLevel field on EquipmentData (int, 0-10)
- AttunementState enum (Unattuned, Attuned, Maximum)
- AttunementTier struct for per-level multipliers
- AttunementCoreConfig ScriptableObject for stat bonuses
- AttunementAttemptResult struct for attempt results
- TryIncreaseAttunement method on EquipmentData
- Stat bonuses applied in ResolveStats pipeline
- GetAttunedDisplayName() for formatted name display
- SetAttunementLevel() API for debug and future mechanics
- AttunementFoundationConfig ScriptableObject for max level
- UI display (badge, details panel, comparison, damage bonus)
- Debug tools (F6 overlay with attunement buttons)
- Comprehensive EditMode tests
- **Attunement Stones** (Slice 0.8.3):
  - AttunementService for stone-based attunement
  - AttunementCostConfig ScriptableObject for cost settings
  - AttunementAttemptPreview for UI preview
  - AttunementFailureReason enum for explicit failure states
  - Stone consumption with atomic transactions
  - Debug UI for adding/using stones
- **Attunement Table** (Slice 0.8.4):
  - AttunementTable world object (IInteractable)
  - AttunementTableUI controller (UI Toolkit)
  - Equipment selection and preview
  - Physical place in game world for attunement ritual
  - See [ATTUNEMENT_TABLE.md](ATTUNEMENT_TABLE.md) for details
- **Failure & Protection System** (Slice 0.8.5):
  - Success/failure chances (100% at +0, 18% at +9→+10)
  - Protection (pity) system (+5% per failure, capped at 30%)
  - Guarantee after 6 consecutive failures
  - ConsecutiveAttunementFailures tracking on EquipmentData
  - AttunementChanceConfig ScriptableObject for chance settings
  - IAttunementRandomSource for testable randomness
  - ForceNextOutcome debug API
  - UI display with "Accumulated Resonance" terminology
  - Debug buttons for Force Win/Lose

❌ Not implemented (deferred to future slices):
- Protection items (prevent level loss)
- Animation/VFX
- Sound effects
- Variable stone costs per level

## Architecture

```
Equipment/Attunement/
├── AttunementState.cs              - Enum (Unattuned, Attuned, Maximum)
├── AttunementTier.cs               - Struct for per-level multipliers
├── AttunementCoreConfig.cs         - ScriptableObject for stat bonuses
├── AttunementAttemptResult.cs      - Struct for attempt results
├── AttunementLevelChangeResult.cs  - Struct for modification results
├── AttunementFoundationConfig.cs   - ScriptableObject for max level
├── AttunementHelper.cs             - Static helper methods
├── AttunementFailureReason.cs      - Enum for failure states (0.8.3)
├── AttunementCostConfig.cs         - ScriptableObject for costs (0.8.3)
├── AttunementAttemptPreview.cs     - Struct for UI preview (0.8.3)
├── AttunementService.cs            - Service for stone-based attunement (0.8.3)
├── AttunementChanceConfig.cs       - ScriptableObject for chances (0.8.5)
├── IAttunementRandomSource.cs      - Interface for random source (0.8.5)
└── SystemRandomSource.cs           - Random implementations (0.8.5)

Equipment/
└── EquipmentData.cs                - Extended with attunement fields
```

## Vocabulary

| Term | Definition |
|------|------------|
| Attunement | The bond between equipment and player |
| Attunement Level | Current level (0 to MaximumAttunementLevel) |
| Attunement State | Derived state (Unattuned, Attuned, Maximum) |
| Maximum Attunement Level | Cap for levels (default 10) |
| Attunement Tier | Stat multipliers for a specific level |
| Accumulated Resonance | Player-facing name for protection bonus from failures |
| Protection | Bonus success chance gained from consecutive failures |
| Guarantee | Automatic success after reaching failure threshold |
| Consecutive Failures | Number of failed attempts since last success |

## AttunementState Enum

```csharp
public enum AttunementState
{
    Unattuned,  // Level 0
    Attuned,    // Level 1 to (Maximum-1)
    Maximum     // Level == MaximumAttunementLevel
}
```

The state is derived from level, not serialized separately.

## AttunementTier Struct

Defines stat multipliers for a specific attunement level:

```csharp
[Serializable]
public struct AttunementTier
{
    public float DamageMultiplier;
    public float AttackSpeedMultiplier;
    public float RangeMultiplier;
    public float StaggerMultiplier;

    public static AttunementTier Default { get; }  // All 1.0
    public bool HasDamageBonus { get; }
    public bool HasAnyBonus { get; }
    public float DamageBonusPercent { get; }

    public (float, float, float, float) ApplyMultipliers(
        float damage, float attackSpeed, float range, float stagger);
}
```

## AttunementCoreConfig

ScriptableObject defining stat bonuses for each attunement level:

```csharp
[CreateAssetMenu(menuName = "Lootbound/Equipment/Attunement Core Config")]
public class AttunementCoreConfig : ScriptableObject
{
    [SerializeField] private AttunementTier[] tiers;  // 11 tiers for 0-10
    [SerializeField] private AttunementFoundationConfig foundationConfig;

    public int MaximumLevel { get; }       // From foundation or array length
    public int TierCount { get; }

    public AttunementTier GetTier(int level);
    public float GetDamageMultiplier(int level);
    public float GetDamageBonusPercent(int level);
    public bool HasBonusAtLevel(int level);

    public (float, float, float, float) ApplyMultipliers(
        int level, float damage, float attackSpeed, float range, float stagger);
}
```

## AttunementAttemptResult

Struct for attunement attempt results:

```csharp
public readonly struct AttunementAttemptResult
{
    public bool Success { get; }
    public int PreviousLevel { get; }
    public int CurrentLevel { get; }
    public bool WasAtMaximum { get; }
    public int MaximumLevel { get; }
    public bool LevelIncreased { get; }
    public bool IsNowAtMaximum { get; }

    public static AttunementAttemptResult AlreadyMaximum(int level, int maxLevel);
    public static AttunementAttemptResult Succeeded(int prevLevel, int newLevel, int maxLevel);
    public static AttunementAttemptResult Failed(int level, int maxLevel);  // Future use

    // Stone-based attunement (0.8.3)
    public int StonesRequired { get; }
    public int StonesConsumed { get; }
    public AttunementFailureReason FailureReason { get; }
    public bool StonesWereConsumed { get; }

    public static AttunementAttemptResult SucceededWithStones(
        int prevLevel, int newLevel, int maxLevel, int stonesRequired, int stonesConsumed);
    public static AttunementAttemptResult CannotAttempt(
        int level, int maxLevel, int stonesRequired, AttunementFailureReason reason);
}
```

## Attunement Stones (Slice 0.8.3)

Attunement attempts require consumable Attunement Stones.

### Core Concepts

- **Attunement Stone** - Stackable consumable item required for attunement attempts
- **Stones per attempt** - Configurable cost (default: 1 stone per attempt)
- **Atomic transactions** - Stones are consumed BEFORE level increase is applied
- **V1 success rate** - Always 100% (no failure chance)

### AttunementFailureReason

Enum for explicit failure states:

```csharp
public enum AttunementFailureReason
{
    None = 0,
    InvalidEquipment,
    MissingInventory,
    MissingStoneDefinition,
    NoAttunementStones,
    InsufficientAttunementStones,
    AlreadyAtMaximum,
    InvalidConfiguration,
    TransactionFailed,
    NotEquipment,
    AttemptInProgress
}
```

### AttunementCostConfig

ScriptableObject for stone cost configuration:

```csharp
[CreateAssetMenu(menuName = "Lootbound/Equipment/Attunement Cost Config")]
public class AttunementCostConfig : ScriptableObject
{
    public ItemDefinition AttunementStoneDefinition { get; }
    public int StonesPerAttempt { get; }  // Default: 1
    public bool ConsumeStoneOnSuccess { get; }  // Default: true
    public bool ConsumeStoneOnFailure { get; }  // Default: true (future use)
    public bool AllowDebugFreeAttempts { get; }
    public bool EnableDebugLogs { get; }
    public bool IsValid { get; }  // True when stone definition is set
}
```

### AttunementAttemptPreview

Readonly struct for previewing attempts without mutation:

```csharp
public readonly struct AttunementAttemptPreview
{
    public bool CanAttempt { get; init; }
    public int CurrentLevel { get; init; }
    public int ResultingLevelOnSuccess { get; init; }
    public int MaximumLevel { get; init; }
    public int RequiredStones { get; init; }
    public int AvailableStones { get; init; }
    public float SuccessChance { get; init; }
    public AttunementFailureReason FailureReason { get; init; }
    public string EquipmentName { get; init; }

    // Protection system (0.8.5)
    public float BaseChance { get; init; }
    public float ProtectionBonus { get; init; }
    public int ConsecutiveFailures { get; init; }
    public bool IsGuaranteed { get; init; }
    public bool HasProtection { get; }              // True if ProtectionBonus > 0
    public float ProtectionBonusPercent { get; }    // For UI display

    public bool HasEnoughStones { get; }
    public bool IsAlreadyAtMaximum { get; }
    public int AttemptsWithCurrentStones { get; }

    public static AttunementAttemptPreview CannotAttempt(AttunementFailureReason reason, ...);
    public static AttunementAttemptPreview CanProceed(...);
    public static AttunementAttemptPreview CanProceedWithProtection(...);  // 0.8.5
}
```

### AttunementService

Service for managing stone-based attunement:

```csharp
public class AttunementService
{
    public event Action<AttunementAttemptResult> OnAttunementCompleted;

    public AttunementService(
        AttunementCostConfig costConfig,
        AttunementCoreConfig coreConfig,
        Inventory inventory,
        AttunementChanceConfig chanceConfig = null,     // 0.8.5
        IAttunementRandomSource randomSource = null);  // 0.8.5

    public int MaximumLevel { get; }
    public int StonesPerAttempt { get; }
    public ItemDefinition StoneDefinition { get; }
    public bool HasChanceSystem { get; }  // 0.8.5: True if chanceConfig provided

    // Get available stones in inventory
    public int GetAvailableStones();

    // Preview without mutation
    public AttunementAttemptPreview PreviewAttempt(EquipmentData equipment);

    // Attempt with stone consumption
    public AttunementAttemptResult TryAttune(EquipmentData equipment, bool bypassCost = false);

    // Debug bypass (if enabled in config)
    public AttunementAttemptResult TryAttuneDebugWithoutCost(EquipmentData equipment);

    // Force next outcome (0.8.5 debug)
    public void ForceNextOutcome(bool? success);  // null clears override
}
```

### Transaction Flow

```
1. Validate equipment (null, IsValid)
2. Check already at maximum
3. Check cost config validity
4. Check inventory available
5. Check stones available (>= required)
6. ATOMIC: Remove stones from inventory
7. Roll for success (0.8.5) - chance based on level + protection
8. If success: Apply level increase, reset consecutive failures
9. If failure: Increment consecutive failures (0.8.5)
10. Fire OnAttunementCompleted event
11. Return result
```

If any step fails before stone consumption, the attempt is rejected with appropriate `AttunementFailureReason`.
**Stones are consumed BEFORE the roll** (atomic transaction). Failed attempts still consume stones.

## Failure & Protection System (Slice 0.8.5)

Attunement attempts can now fail based on the current level. Higher levels have lower base success chances, but protection accumulates from failures.

### Success Chances

| Level Transition | Base Chance |
|-----------------|-------------|
| +0 → +1 | 100% |
| +1 → +2 | 90% |
| +2 → +3 | 80% |
| +3 → +4 | 70% |
| +4 → +5 | 60% |
| +5 → +6 | 50% |
| +6 → +7 | 40% |
| +7 → +8 | 30% |
| +8 → +9 | 24% |
| +9 → +10 | 18% |

### Protection (Pity) System

After each failed attempt:
- Equipment gains **+5%** protection bonus (capped at **30%**)
- Protection is **per-weapon instance**, not global
- Protection **resets to 0** on successful attunement
- Protection is **preserved** through repair, condition changes, equip/unequip

Effective chance = Base Chance + Protection Bonus (capped at 100%)

Example:
```
Level 5 attempt: Base 50%
After 3 failures: 50% + 15% = 65% effective chance
After 6 failures: GUARANTEED (see below)
```

### Guarantee Threshold

After **6 consecutive failures** on the same weapon, the next attempt is **guaranteed** to succeed (100% chance), regardless of the base chance.

### Player-Facing Terminology

- **Accumulated Resonance** - The protection bonus displayed in UI
- Shows as "Resonance: +X%" or "GUARANTEED" when threshold reached

### AttunementChanceConfig

ScriptableObject for chance and protection settings:

```csharp
[CreateAssetMenu(menuName = "Lootbound/Equipment/Attunement Chance Config")]
public class AttunementChanceConfig : ScriptableObject
{
    [SerializeField] private float[] baseChances;          // Per-level (100%, 90%, 80%, ...)
    [SerializeField] private float protectionBonusPerFailure;  // Default: 0.05 (+5%)
    [SerializeField] private float protectionCap;              // Default: 0.30 (30%)
    [SerializeField] private int guaranteeAfterFailures;       // Default: 6

    public float GetBaseChance(int currentLevel);
    public float GetProtectionBonus(int consecutiveFailures);
    public float GetEffectiveChance(int currentLevel, int consecutiveFailures);
    public bool IsGuaranteed(int consecutiveFailures);

    public string FormatProtectionBonus(int consecutiveFailures);  // "+X%"
    public string FormatEffectiveChance(int currentLevel, int consecutiveFailures);  // "X%"
}
```

### IAttunementRandomSource

Interface for testable random number generation:

```csharp
public interface IAttunementRandomSource
{
    float NextFloat();                    // Returns 0.0 to 1.0
    bool Roll(float successChance);       // Returns true if roll < successChance
}
```

### Random Source Implementations

```csharp
// Production implementation
public class SystemRandomSource : IAttunementRandomSource { }

// Test implementation - always succeeds or fails
public class DeterministicRandomSource : IAttunementRandomSource
{
    public static DeterministicRandomSource AlwaysSucceed { get; }
    public static DeterministicRandomSource AlwaysFail { get; }
}

// Test implementation - predetermined sequence
public class SequenceRandomSource : IAttunementRandomSource
{
    public SequenceRandomSource(params bool[] sequence);
    public void Reset();
}
```

### EquipmentData Extensions (0.8.5)

```csharp
public class EquipmentData
{
    [SerializeField] private int consecutiveAttunementFailures;

    public int ConsecutiveAttunementFailures { get; }
    public bool HasAccumulatedResonance { get; }  // True if failures > 0

    public void IncrementAttunementFailures();
    public void ResetAttunementFailures();
    public void SetAttunementFailures(int count);  // Debug
}
```

Clone behavior:
- `Clone()` - Preserves consecutive failures
- `CloneWithNewId()` - Resets consecutive failures to 0

### AttunementAttemptResult Extensions (0.8.5)

```csharp
public readonly struct AttunementAttemptResult
{
    // New properties for failure handling
    public bool WasRngFailure { get; }         // True if failed due to RNG (not validation)
    public float AttemptedChance { get; }      // Success chance that was rolled against
    public float ProtectionGained { get; }     // Protection bonus from this failure

    public static AttunementAttemptResult FailedWithStones(
        int level, int maximumLevel, int stonesRequired, int stonesConsumed,
        float attemptedChance, float protectionGained);
}
```

### Debug Tools (0.8.5)

Combat Debug Overlay (F6) new buttons:
- **Force Win** - Force next attempt to succeed
- **Force Lose** - Force next attempt to fail
- **Clear Force** - Clear forced outcome
- **R=0** - Reset resonance (consecutive failures) to 0
- **R+1** - Add 1 consecutive failure
- **R=5** - Set to 5 consecutive failures
- **R=6** - Set to 6 consecutive failures (guarantee threshold)

Display shows:
- Success chance with breakdown: "65% (50% + 15%)"
- "GUARANTEED" when at guarantee threshold

### UI Display (AttunementTableUI)

Success chance display:
- Shows "100%" at level 0
- Shows "50%" when no protection
- Shows "65% (50% + 15%)" with protection breakdown
- Shows "GUARANTEED" when at 6+ failures

Result feedback:
- Success: "ATTUNED!" (green)
- RNG Failure: "The resonance dissipated..." (red)
- Other failures: Specific error message

### PlayerEquipment Integration (0.8.3)

```csharp
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private AttunementCostConfig attunementCostConfig;

    public AttunementCostConfig AttunementCostConfig { get; }
    public ItemDefinition AttunementStoneDefinition { get; }

    // Get or create the attunement service (lazy initialization)
    public AttunementService GetAttunementService();

    // Stone-based attunement
    public AttunementAttemptResult? TryAttuneEquippedWeaponWithStones();
    public AttunementAttemptPreview PreviewAttuneEquippedWeapon();
    public int GetAvailableAttunementStones();
}
```

### Debug UI (F6 Combat Overlay)

Equipment section shows:
- Attunement Stones: N (need X/attempt)

Debug buttons:
- "Attune (Stone)" - Consume stone and attune (disabled if no stones or at max)
- "+5 Stones" - Add 5 stones to inventory
- "+20 Stones" - Add 20 stones to inventory
- "Free +1" - Debug attunement without stones
- "Reset", "Set Max", "Random", "+0/+3/+5/+10" - Level manipulation

## AttunementFoundationConfig

ScriptableObject centralizing maximum attunement level:

```csharp
[CreateAssetMenu(menuName = "Lootbound/Equipment/Attunement Foundation Config")]
public class AttunementFoundationConfig : ScriptableObject
{
    public const int DefaultMaximumAttunementLevel = 10;

    [SerializeField, Range(1, 15)]
    private int maximumAttunementLevel = 10;

    public int MaximumAttunementLevel { get; }

    public AttunementState GetState(int level);
    public bool IsMaximumLevel(int level);
    public int ClampLevel(int level);
}
```

## AttunementHelper

Static helper for attunement calculations:

```csharp
public static class AttunementHelper
{
    // Calculate state from level
    public static AttunementState GetState(int level, int maximumLevel);
    public static AttunementState GetState(int level); // Uses default max (10)

    // State checks
    public static bool IsAttuned(int level);
    public static bool IsAtMaximum(int level, int maximumLevel);

    // Level clamping
    public static int ClampLevel(int level, int maximumLevel);
    public static int ClampLevel(int level); // Uses default max (10)

    // Display name formatting
    public static string FormatDisplayName(string baseName, int level);
}
```

### FormatDisplayName Behavior

| Base Name | Level | Result |
|-----------|-------|--------|
| "Traveler Blade" | 0 | "Traveler Blade" |
| "Traveler Blade" | 1 | "Traveler Blade +1" |
| "Traveler Blade" | 10 | "Traveler Blade +10" |
| "" | 0 | "" |
| "" | 3 | "+3" |

## EquipmentData Extensions

### New Fields

```csharp
public class EquipmentData
{
    [SerializeField] private int attunementLevel;

    // Properties
    public int AttunementLevel { get; }
    public int MaximumAttunementLevel { get; }  // Returns 10
    public AttunementState AttunementState { get; }
    public bool IsAttuned { get; }
    public bool IsAtMaximumAttunement { get; }
}
```

### New Methods

```csharp
// Get display name with attunement suffix
public string GetAttunedDisplayName(IEquipmentRegistry registry);

// Set attunement level (for debug and future mechanics)
public AttunementLevelChangeResult SetAttunementLevel(int newLevel, int maximumLevel = -1);

// Try to increase attunement by 1 (for debug and attunement mechanics)
public AttunementAttemptResult TryIncreaseAttunement(int maximumLevel = -1);

// Resolve stats with attunement bonuses
public ResolvedWeaponStats ResolveStats(
    IEquipmentRegistry registry,
    BrokenWeaponConfig brokenConfig,
    AttunementCoreConfig attunementConfig);
```

### Clone Behavior

| Method | Attunement Level |
|--------|-----------------|
| Clone() | Preserved (same as original) |
| CloneWithNewId() | Reset to 0 |

## UI

### Inventory Slot Badge

When equipment has attunement level > 0:
- Small "+N" badge in top-right corner of slot
- Gold color for visibility

### Details Panel

Attunement section shows:
- Label: "Attunement"
- Value: "+N / +10" format
- Color based on state:
  - Unattuned (level 0): Gray (#808080)
  - Attuned (level 1-9): Blue (#4488FF)
  - Maximum (level 10): Gold (#FFD700)

### Damage Bonus Display

When equipment is attuned (level > 0):
- Shows "Damage Bonus: +X%" row in details panel
- Calculated from AttunementCoreConfig

### Equipment Name Display

When equipment is attuned (level > 0):
- Name displays as "Traveler Blade +3"
- Used in inventory details, repair station, comparison panel

### Comparison Display

When comparing two weapons:
- Shows attunement level comparison
- Shows actual resolved damage values (includes attunement bonus)

## Debug Tools

### Combat Debug Overlay (F6)

Equipment section shows:
- Attunement: +N / +10 (State)
- Damage Bonus: +X% (when attuned)
- Success Chance: X% (base + protection) or GUARANTEED
- Resonance: N consecutive failures (0.8.5)

Debug buttons for attunement:
- "Attune +1" - Increase level by 1 (debug, no cost)
- "Reset" - Reset to level 0
- "Set Max" - Set to maximum (10)
- "Random" - Set random level 0-10
- "+0", "+3", "+5", "+10" - Set specific levels

Debug buttons for failure/protection (0.8.5):
- "Force Win" - Force next attempt to succeed
- "Force Lose" - Force next attempt to fail
- "Clear" - Clear forced outcome
- "R=0" - Reset resonance to 0
- "R+1" - Increment resonance by 1
- "R=5" - Set resonance to 5
- "R=6" - Set resonance to 6 (guarantee threshold)

## PlayerEquipment Integration

PlayerEquipment injects configuration for stat resolution and attunement:

```csharp
public class PlayerEquipment : MonoBehaviour
{
    [SerializeField] private AttunementCoreConfig attunementConfig;
    [SerializeField] private AttunementCostConfig attunementCostConfig;
    [SerializeField] private AttunementChanceConfig attunementChanceConfig;  // 0.8.5

    public AttunementCoreConfig AttunementConfig { get; }
    public AttunementCostConfig AttunementCostConfig { get; }
    public AttunementChanceConfig AttunementChanceConfig { get; }  // 0.8.5

    // Get or create the attunement service
    public AttunementService GetAttunementService();

    // Try to attune the equipped weapon
    public AttunementAttemptResult? TryAttuneEquippedWeapon();

    // Reset attunement to 0
    public void ResetEquippedWeaponAttunement();

    // Set specific attunement level
    public void SetEquippedWeaponAttunement(int level);
}
```

## Tests

EditMode tests in `AttunementTests.cs`:

### AttunementState Tests
- HasExpectedValues (Unattuned=0, Attuned=1, Maximum=2)

### AttunementHelper Tests
- GetState at level 0 returns Unattuned
- GetState at level 1-9 returns Attuned
- GetState at level 5 is NOT Maximum (max is 10)
- GetState at level 10 returns Maximum
- GetState with default maximum uses 10
- ClampLevel clamps to 0-10 range
- FormatDisplayName formats correctly

### AttunementFoundationConfig Tests
- DefaultMaximumLevel is 10
- GetState returns correct state
- IsMaximumLevel true at 10, false at 5
- ClampLevel clamps to 10

### AttunementCoreConfig Tests
- HasCorrectTierCount (11 tiers)
- MaximumLevel is 10
- GetTier at level 0 has no bonus
- GetTier at level 5 returns ×1.24 damage
- GetTier at level 10 returns ×1.60 damage
- GetTier out of bounds clamps correctly
- GetDamageBonusPercent returns correct values
- HasBonusAtLevel correct for all levels

### AttunementTier Tests
- Default has no bonus
- ApplyMultipliers calculates correctly
- DamageBonusPercent correct

### Stat Modification Tests
- ResolveStats level 0 no bonus
- ResolveStats level 1 +4% damage
- ResolveStats level 5 +24% damage
- ResolveStats level 10 +60% damage
- ResolveStats without config has no bonus
- ResolveStats attunement applied after affixes
- ResolveStats attunement applied before broken penalty
- ResolveStats all multipliers correct order

### TryIncreaseAttunement Tests
- FromLevel0 succeeds
- FromLevel9 succeeds (reaches maximum)
- AtLevel10 fails (already maximum)
- Multiple increases work
- Uses default max if not provided

### AttunementAttemptResult Tests
- AlreadyMaximum has correct properties
- Succeeded has correct properties
- Succeeded to maximum sets IsNowAtMaximum
- Failed has correct properties
- ToString formats correctly

### Attunement Preservation Tests
- Durability change preserves attunement
- Repair preserves attunement
- Condition changes preserve attunement

### AttunementService Tests (0.8.3)
- AttunementCostConfig validity checks
- GetAvailableStones returns 0 without inventory
- MaximumLevel returns config value
- StonesPerAttempt returns cost config value
- PreviewAttempt with invalid equipment returns CannotAttempt
- PreviewAttempt without inventory returns MissingInventory
- PreviewAttempt at maximum returns AlreadyAtMaximum
- PreviewAttempt without stones returns NoAttunementStones
- PreviewAttempt with insufficient stones returns InsufficientAttunementStones
- PreviewAttempt with valid conditions returns CanProceed
- TryAttune success consumes stones
- TryAttune fails without stones
- TryAttune fails at maximum (no stones consumed)
- TryAttuneDebugWithoutCost succeeds without stones
- Multiple attempts consume correct stone counts
- AttunementAttemptResult SucceededWithStones properties
- AttunementAttemptResult CannotAttempt properties
- AttunementFailureReason enum values

### Attunement Failure & Protection Tests (0.8.5)
- AttunementChanceConfig GetBaseChance level 0 returns 100%
- AttunementChanceConfig GetBaseChance level 9 returns 18%
- AttunementChanceConfig GetProtectionBonus 0 failures returns 0
- AttunementChanceConfig GetProtectionBonus 3 failures returns 15%
- AttunementChanceConfig GetProtectionBonus 10 failures caps at 30%
- AttunementChanceConfig IsGuaranteed 5 failures returns false
- AttunementChanceConfig IsGuaranteed 6 failures returns true
- AttunementChanceConfig GetEffectiveChance adds protection to base
- AttunementChanceConfig GetEffectiveChance guaranteed returns 100%
- EquipmentData ConsecutiveAttunementFailures starts at 0
- EquipmentData IncrementAttunementFailures increments count
- EquipmentData ResetAttunementFailures sets to 0
- EquipmentData SetAttunementFailures sets specific value
- EquipmentData Clone preserves consecutive failures
- EquipmentData CloneWithNewId resets consecutive failures
- DeterministicRandomSource AlwaysSucceed returns success
- DeterministicRandomSource AlwaysFail returns failure
- DeterministicRandomSource Roll with guaranteed chance always succeeds
- SequenceRandomSource returns sequence in order
- AttunementService with chance config has chance system
- AttunementService without chance config no chance system
- TryAttune with deterministic fail consumes stones
- TryAttune with deterministic success succeeds
- TryAttune success resets failure count
- TryAttune failure increments failure count
- TryAttune guarantee after 6 failures
- ForceNextOutcome forces success
- ForceNextOutcome forces failure
- ForceNextOutcome only affects one attempt
- AttunementAttemptPreview with chance config shows protection info
- AttunementAttemptPreview with guarantee shows guaranteed
- AttunementAttemptResult FailedWithStones has correct properties
- Protection preserved through repair
- Protection preserved through condition change

## Scene Setup

### Required: AttunementCoreConfig Asset

Create via Unity menu "Lootbound/Equipment/Attunement Core Config":
1. Right-click in `Assets/Lootbound/ScriptableObjects/Equipment/`
2. Create > Lootbound > Equipment > Attunement Core Config
3. Name: `DefaultAttunementCoreConfig`
4. Configure 11 tiers with spec multipliers

### Assign to PlayerEquipment

1. Open PlayerCharacter prefab
2. Find PlayerEquipment component
3. Assign the AttunementCoreConfig asset

## Files Created (0.8.2)

```
Equipment/Attunement/
├── AttunementTier.cs
├── AttunementCoreConfig.cs
└── AttunementAttemptResult.cs
```

## Files Created (0.8.3)

```
Equipment/Attunement/
├── AttunementFailureReason.cs
├── AttunementCostConfig.cs
├── AttunementAttemptPreview.cs
└── AttunementService.cs
```

## Files Modified (0.8.2)

- `AttunementFoundationConfig.cs` - DefaultMaximumAttunementLevel changed to 10
- `EquipmentData.cs` - Added ResolveStats overload with attunementConfig, TryIncreaseAttunement
- `PlayerEquipment.cs` - Added AttunementCoreConfig injection, TryAttuneEquippedWeapon
- `InventoryUI.cs` - Added damage bonus display, updated max level handling
- `CombatDebugOverlay.cs` - Added new attunement debug buttons
- `AttunementTests.cs` - Comprehensive test updates for max level 10 and stat modifications

## Files Modified (0.8.3)

- `AttunementAttemptResult.cs` - Added StonesRequired, StonesConsumed, FailureReason, SucceededWithStones, CannotAttempt
- `PlayerEquipment.cs` - Added AttunementCostConfig, AttunementService, TryAttuneEquippedWeaponWithStones, PreviewAttuneEquippedWeapon, GetAvailableAttunementStones
- `CombatDebugOverlay.cs` - Added stone display, Attune (Stone) button, +5/+20 Stones buttons
- `AttunementTests.cs` - Added comprehensive AttunementService tests

## Files Created (0.8.4)

```
Gameplay/World/
└── AttunementTable.cs

UI/AttunementTable/
├── AttunementTableUI.cs
├── AttunementTable.uxml
└── AttunementTable.uss

Tests/EditMode/
└── AttunementTableTests.cs

Docs/
└── ATTUNEMENT_TABLE.md
```

## Files Created (0.8.5)

```
Equipment/Attunement/
├── AttunementChanceConfig.cs      - ScriptableObject for chance settings
├── IAttunementRandomSource.cs     - Interface for random source abstraction
└── SystemRandomSource.cs          - Includes SystemRandomSource, DeterministicRandomSource, SequenceRandomSource
```

## Files Modified (0.8.5)

- `EquipmentData.cs` - Added consecutiveAttunementFailures field and methods
- `AttunementAttemptPreview.cs` - Added protection info (BaseChance, ProtectionBonus, etc.)
- `AttunementAttemptResult.cs` - Added FailedWithStones factory, WasRngFailure, ProtectionGained
- `AttunementService.cs` - Rewritten with failure/protection logic and ForceNextOutcome
- `PlayerEquipment.cs` - Added AttunementChanceConfig injection
- `AttunementTableUI.cs` - Success chance display with protection breakdown
- `CombatDebugOverlay.cs` - Force Win/Lose buttons, resonance manipulation
- `AttunementTests.cs` - Comprehensive failure/protection tests (~30 new tests)

## Scene Setup (0.8.3)

### Required: AttunementCostConfig Asset

Create via Unity menu "Lootbound/Equipment/Attunement Cost Config":
1. Right-click in `Assets/Lootbound/ScriptableObjects/Equipment/`
2. Create > Lootbound > Equipment > Attunement Cost Config
3. Name: `DefaultAttunementCostConfig`
4. Assign the Attunement Stone ItemDefinition

### Required: Item_AttunementStone Asset

Create via Unity menu "Lootbound/Items/Item Definition":
1. Right-click in `Assets/Lootbound/ScriptableObjects/Items/`
2. Create > Lootbound > Items > Item Definition
3. Name: `Item_AttunementStone`
4. Configure:
   - Item ID: `item_attunement_stone`
   - Display Name: `Attunement Stone`
   - Is Stackable: `true`
   - Max Stack Size: `20` (or 50)
   - Item Category: `Material` (or `Consumable`)

### Assign AttunementCostConfig to PlayerEquipment

1. Open PlayerCharacter prefab
2. Find PlayerEquipment component
3. Assign the AttunementCostConfig asset to the new field

### Add Attunement Stones to Enemy Loot (optional)

1. Open enemy EnemyConfig assets
2. Add Item_AttunementStone to the `lootItems` array
3. Configure drop rate and quantity

## Scene Setup (0.8.4)

### Adding AttunementTableUI to a Scene

1. Create empty GameObject named "AttunementTableUI"
2. Add UIDocument component
3. Assign AttunementTable.uxml as Source Asset
4. Set Sort Order: 110
5. Add AttunementTableUI script
6. Assign player references (auto-find works in editor)

### Placing the Table

1. Drag P_AttunementTable prefab into scene
2. Add AttunementTable component to prefab if not present
3. Position in refuge/test area
4. Ensure collider is on interactable layer

## Scene Setup (0.8.5)

### Required: AttunementChanceConfig Asset

Create via Unity menu "Lootbound/Equipment/Attunement Chance Config":
1. Right-click in `Assets/Lootbound/ScriptableObjects/Equipment/`
2. Create > Lootbound > Equipment > Attunement Chance Config
3. Name: `DefaultAttunementChanceConfig`
4. Default values are already configured:
   - Base chances: 100%, 90%, 80%, 70%, 60%, 50%, 40%, 30%, 24%, 18%
   - Protection per failure: 5%
   - Protection cap: 30%
   - Guarantee after: 6 failures

### Assign AttunementChanceConfig to PlayerEquipment

1. Open PlayerCharacter prefab
2. Find PlayerEquipment component
3. Assign the AttunementChanceConfig asset to the new field

**Note:** If AttunementChanceConfig is not assigned, the system falls back to 100% success chance (pre-0.8.5 behavior).

## Attunement History (Slice 0.8.6)

Attunement attempts leave a permanent trace on equipment. Each piece remembers:
- Total attempts, successes, and failures
- Stones consumed across all attempts
- Highest attunement level ever reached
- Longest failure streak experienced
- Details of the last attempt (location, timestamp, result)

### AttunementHistory Class

Located in `EquipmentHistory.Attunement`:

```csharp
public sealed class AttunementHistory
{
    // Counters
    public int TotalAttempts { get; }
    public int SuccessfulAttempts { get; }
    public int FailedAttempts { get; }
    public int TotalStonesConsumed { get; }

    // Progress tracking
    public int HighestAttunementLevelReached { get; }
    public int LongestFailureStreak { get; }

    // Last attempt details
    public long LastAttemptTimestamp { get; }
    public string LastAttemptLocation { get; }
    public bool LastAttemptSuccess { get; }
    public bool LastAttemptWasGuaranteed { get; }

    // Methods
    public void RecordAttempt(AttunementAttemptResult result, string location, int currentStreak, bool wasGuaranteed);
    public void Reset();
    public string GetSummary();
    public static string FormatTimestamp(long timestamp);
}
```

### Recording Authority

`AttunementService` is the single authority for recording attunement history. After each **resolved** attempt (where RNG actually rolled), it calls `RecordAttempt()`.

Technical refusals (maximum level, insufficient stones, broken equipment) are **not** recorded.

### WasAttemptResolved Property

```csharp
// In AttunementAttemptResult
public bool WasAttemptResolved => Success || WasRngFailure;
```

### Location Tracking

`AttunementTable.LocationName` provides the location string for history recording:

```csharp
// AttunementTable inspector field
[SerializeField] private string locationName = "Refuge Attunement Table";
public string LocationName => locationName;
```

### UI Display

- **Inventory Details Panel**: Shows attempt summary and last attempt details
- **Attunement Table UI**: Displays attempt statistics
- **F6 Debug Overlay**: Shows counters, streaks, and reset button

### Files Created (0.8.6)

```
Equipment/Attunement/
└── AttunementHistory.cs        - History data structure

Docs/
└── ATTUNEMENT_HISTORY.md       - Detailed documentation
```

### Files Modified (0.8.6)

- `EquipmentHistory.cs` - Added Attunement property
- `AttunementAttemptResult.cs` - Added WasAttemptResolved property
- `AttunementService.cs` - Added history recording and location parameter
- `AttunementTable.cs` - Added LocationName field
- `AttunementTableUI.cs` - Updated to pass location and display history
- `InventoryUI.cs` - Added attunement history display
- `CombatDebugOverlay.cs` - Added history section and reset button
- `AttunementTests.cs` - Added ~30 history tests

See [ATTUNEMENT_HISTORY.md](ATTUNEMENT_HISTORY.md) for complete documentation.

## Next Steps

Future slices may add:
- Variable stone costs per level
- Protection items to prevent level loss
- VFX/SFX for attunement success/failure
- Per-weapon personality (different stats bonuses per weapon type)
