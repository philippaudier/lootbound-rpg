# Equipment & Loot Identity System (Slice 0.6 + 0.7)

This document describes the equipment and loot identity system implemented in Slice 0.6, with the condition system added in Slice 0.7.1, weapon wear in Slice 0.7.2, broken weapons in Slice 0.7.3, and repair fragments in Slice 0.7.4.

## Overview

The equipment system provides unique, persistent equipment identity with:
- GUID-based unique identification per equipment instance
- Separation between definitions (templates) and instances (unique items)
- Rarity system (Common, Uncommon, Rare for V1)
- Affix system with stat modifiers
- Generated names based on rarity
- History tracking (found location, enemies defeated)
- **Durability and condition system** (Slice 0.7.1)
- Equipment/unequipment from inventory
- Combat integration with resolved stats
- UI for equipment details, condition, and comparison

## Architecture

```
Equipment/
├── Definitions/
│   ├── WeaponDefinition      - ScriptableObject extending ItemDefinition
│   ├── AffixDefinition       - ScriptableObject for affix templates
│   └── EquipmentRegistry     - Registry of all definitions
├── Instances/
│   ├── EquipmentData         - Unique instance with GUID, affixes, durability
│   ├── AffixInstance         - Rolled affix with value
│   └── EquipmentHistory      - Tracks found location, kills, equips
├── Condition/
│   ├── EquipmentCondition    - Enum (Excellent, Good, Worn, Fragile, Broken)
│   └── EquipmentConditionHelper - Centralized thresholds, colors, tooltips
├── Stats/
│   ├── ResolvedWeaponStats   - Computed final stats
│   └── AffixModifierType     - Enum for stat modifiers
├── Generation/
│   ├── EquipmentGenerator    - Creates instances with random rolls
│   └── EquipmentNameGenerator- Generates names by rarity
└── Player/
    └── PlayerEquipment       - Manages equipped items
```

## Definition vs Instance

### Definition (Template)

ScriptableObjects that define base stats:

- `WeaponDefinition`: Extends ItemDefinition with weapon-specific stats
- Shared by all instances of that weapon type
- Immutable at runtime

```csharp
// WeaponDefinition fields
public float BaseDamage { get; }
public float BaseAttackSpeed { get; }
public float BaseRange { get; }
public float BaseStagger { get; }
public float BaseDurability { get; }  // Default: 100
public MeleeAttackConfig AttackConfig { get; }
```

### Instance (Unique Item)

Serializable classes for unique equipment pieces:

- `EquipmentData`: Attached to ItemInstance
- Contains unique GUID, rolled rarity, rolled affixes
- Mutable history tracking

```csharp
// EquipmentData fields
public string InstanceId { get; }      // GUID
public string DefinitionId { get; }    // Links to definition
public string CustomName { get; }      // Generated name
public ItemRarity Rarity { get; }      // Rolled rarity
public IReadOnlyList<AffixInstance> Affixes { get; }
public EquipmentHistory History { get; }
public bool IsEquipped { get; set; }

// Durability (Slice 0.7.1)
public float CurrentDurability { get; }
public float MaxDurability { get; }
public float NormalizedDurability { get; }  // 0-1
public EquipmentCondition Condition { get; }
```

## Affix System

### AffixDefinition

ScriptableObject defining an affix type:

| Parameter | Description |
|-----------|-------------|
| AffixId | Unique identifier |
| DisplayName | UI display name |
| ModifierType | Which stat to modify |
| MinValue / MaxValue | Roll range |
| IsNegative | Whether modifier is a penalty |
| Tier | Minor or Major |

### AffixModifierType

Supported modifier types:

| Type | Effect |
|------|--------|
| DamagePercent | Percentage bonus to damage |
| AttackSpeedPercent | Percentage bonus to attack speed |
| RangePercent | Percentage bonus to range |
| StaggerPercent | Percentage bonus to stagger |

### AffixInstance

A rolled affix on equipment:

```csharp
public class AffixInstance
{
    public string DefinitionId { get; }
    public float RolledValue { get; }

    public float GetEffectiveValue(IEquipmentRegistry registry);
    public AffixModifierType GetModifierType(IEquipmentRegistry registry);
}
```

### V1 Affixes

| Affix | Type | Tier | Range |
|-------|------|------|-------|
| Sharp | DamagePercent | Minor | +8% to +15% |
| Swift | AttackSpeedPercent | Minor | +5% to +12% |
| Balanced | RangePercent | Minor | +5% to +10% |
| Heavy | StaggerPercent | Major | +10% to +20% |

Note: Heavy was originally designed as a dual modifier (+damage, -speed) but was simplified for V1 to use a single stagger modifier. Compound affixes may be added in a future slice.

## Rarity System

### V1 Rarity Distribution

| Rarity | Probability | Affixes |
|--------|------------|---------|
| Common | 65% | 0 affixes |
| Uncommon | 28% | 1 minor affix |
| Rare | 7% | 1 major affix |

Epic and Legendary are not implemented in V1.

### Name Generation

Names are generated based on rarity:

- **Common**: Prefix + Base (e.g., "Old Blade", "Worn Blade")
- **Uncommon**: Base only (e.g., "Blade")
- **Rare**: Base + Location Suffix (e.g., "Blade of the Caverns")

## Stat Resolution

### ResolvedWeaponStats

Computed final stats after applying all modifiers:

```csharp
public readonly struct ResolvedWeaponStats
{
    public float Damage { get; }
    public float AttackSpeed { get; }
    public float DurationMultiplier { get; }
    public float Range { get; }
    public float Stagger { get; }
    public bool IsValid { get; }
}
```

### Resolution Process

1. Start with base stats from WeaponDefinition
2. Accumulate percentage bonuses from all affixes
3. Apply bonuses: `stat * (1 + bonus/100)`
4. Clamp to valid ranges

Stat clamp ranges:
- Damage: minimum 1
- AttackSpeed: 0.3 to 3.0
- Range: 0.5 to 4.0
- Stagger: 0.0 to 1.0

## Condition System (Slice 0.7.1)

### EquipmentCondition Enum

Equipment has a condition derived from durability:

| Condition | Durability Range | Color |
|-----------|-----------------|-------|
| Excellent | 80-100% | Bluish gray |
| Good | 60-79% | Soft green |
| Worn | 35-59% | Ochre |
| Fragile | 1-34% | Orange |
| Broken | 0% | Deep red |

### EquipmentConditionHelper

Centralized helper for condition calculations:

```csharp
public static class EquipmentConditionHelper
{
    // Calculate condition from normalized durability (0-1)
    public static EquipmentCondition GetCondition(float normalizedDurability);

    // Calculate from current and max values
    public static EquipmentCondition GetCondition(float current, float max);

    // Get display color for condition
    public static Color GetConditionColor(EquipmentCondition condition);

    // Get tooltip text for condition
    public static string GetConditionTooltip(EquipmentCondition condition);

    // Check if equipment can be used in combat
    public static bool CanUseInCombat(EquipmentCondition condition);
}
```

### Durability API

EquipmentData provides methods for durability manipulation:

```csharp
public class EquipmentData
{
    // Set durability to specific value (clamped to 0-MaxDurability)
    public void SetDurability(float value);

    // Restore durability by amount
    public void RestoreDurability(float amount);

    // Reduce durability by amount
    public void ReduceDurability(float amount);
}
```

### V1 Scope

The condition system in Slice 0.7.1 is **foundation only**:

✅ Implemented:
- Durability tracking (CurrentDurability, MaxDurability)
- Condition calculation from durability
- UI display (condition label, durability bar, colors, tooltips)
- Centralized thresholds
- EditMode tests for all thresholds

❌ Not implemented (deferred to future slices):
- ~~Durability loss from combat~~ → Implemented in Slice 0.7.2
- Repair mechanics
- Condition effects on stats
- Equipment breaking gameplay

## Weapon Wear System (Slice 0.7.2)

The wear system applies durability loss to weapons during combat.

### Architecture

```
Equipment/Wear/
├── WeaponWearCause.cs      - Enum for wear causes
├── WeaponWearConfig.cs     - ScriptableObject for tuning
├── WearContext.cs          - Input data for wear evaluation
├── WearResult.cs           - Output data from wear application
└── WeaponWearSystem.cs     - Pure C# logic (testable)

Equipment/
└── PlayerWeaponWear.cs     - MonoBehaviour connector

UI/Notifications/
└── ConditionNotificationUI.cs - Shows condition changes
```

### Wear Causes

| Cause | Description |
|-------|-------------|
| SuccessfulHit | Weapon hit a valid combat target |
| HeavyTargetHit | Weapon hit a target with high HP |
| PlayerDamagedWhileEquipped | Player took damage while weapon was equipped |
| WorldImpact | Reserved for future environmental hits |
| Debug | For testing/debugging |

### WeaponWearConfig

ScriptableObject defining wear parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| SuccessfulHitChance | 15% | Chance to apply wear per hit |
| SuccessfulHitAmount | 1 | Durability loss per hit |
| HeavyTargetHpThreshold | 100 | HP above which targets are "heavy" |
| HeavyTargetHitChance | 30% | Additional wear chance for heavy targets |
| HeavyTargetHitAmount | 2 | Additional durability loss |
| PlayerDamagedChance | 10% | Chance when player takes damage |
| PlayerDamagedAmount | 0.5 | Durability loss when damaged |

### WearContext and WearResult

```csharp
// Input: describes the wear event
public readonly struct WearContext
{
    public WeaponWearCause Cause { get; }
    public int AttackId { get; }           // For preventing double wear
    public float TargetMaxHp { get; }      // For heavy target check
}

// Output: result of wear attempt
public readonly struct WearResult
{
    public bool WearApplied { get; }
    public float DurabilityLost { get; }
    public EquipmentCondition ConditionBefore { get; }
    public EquipmentCondition ConditionAfter { get; }
    public bool ConditionChanged { get; }
    public bool NowBroken { get; }
}
```

### WeaponWearSystem

Pure C# class for testability:

```csharp
public class WeaponWearSystem
{
    public event Action<WearResult> OnWearApplied;
    public event Action<WearResult> OnConditionChanged;

    public WearResult TryApplyWear(EquipmentData equipment, WearContext context);
    public WearResult TryApplyHitWear(EquipmentData equipment, int attackId, float targetMaxHp);
    public WearResult ApplyDebugWear(EquipmentData equipment);
    public void ResetForNewAttack();
}
```

Key features:
- Probabilistic wear (not every attack causes wear)
- Attack ID tracking prevents multiple wear per swing
- Deterministic with optional seed for testing
- Events fire only when wear applied or condition changes

### PlayerWeaponWear

MonoBehaviour connecting to Unity events:

- Subscribes to `PlayerCombatController.OnAttack` for attack ID generation
- Subscribes to `PlayerCombatController.OnHitTarget` for hit wear
- Subscribes to `PlayerHealth.OnDamaged` for damage wear
- Fires events for UI notification binding

### Condition Notifications

ConditionNotificationUI shows messages when condition degrades:
- "Test Blade is showing signs of use" (Good)
- "Test Blade has seen many battles" (Worn)
- "Test Blade is becoming fragile!" (Fragile)
- "Test Blade has broken!" (Broken)

Notifications appear only on condition transitions, not on every durability loss.

### V1 Limitations

The wear system in Slice 0.7.2 does NOT include:
- Repair mechanics
- ~~Stat penalties for worn/broken equipment~~ → Implemented in Slice 0.7.3
- VFX/SFX for wear events (basic visual feedback added in 0.7.3)
- WorldImpact wear trigger (reserved)
- UI for durability in combat HUD

## Equipment Generation

### EquipmentGenerator

Creates equipment instances with random rolls:

```csharp
public class EquipmentGenerator
{
    // Generate with random rarity
    public ItemInstance GenerateWeapon(
        WeaponDefinition definition,
        string foundLocation,
        int? seed = null);

    // Generate with specific rarity
    public ItemInstance GenerateWeaponWithRarity(
        WeaponDefinition definition,
        ItemRarity rarity,
        string foundLocation,
        int? seed = null);

    // Create simple common weapon (for starting equipment)
    public ItemInstance CreateSimpleWeapon(
        WeaponDefinition definition,
        string customName,
        string foundLocation);
}
```

### Deterministic Generation

Pass an optional seed for deterministic results:

```csharp
var generator = new EquipmentGenerator(registry);
var instance = generator.GenerateWeapon(weaponDef, "Cave", seed: 12345);
// Same seed always produces same result
```

## Inventory Integration

### ItemInstance Extension

ItemInstance now optionally contains EquipmentData:

```csharp
public class ItemInstance
{
    public EquipmentData EquipmentData { get; }
    public bool HasEquipmentData { get; }
    public bool IsEquipped { get; }
}
```

### Non-Stackable Behavior

Equipment items cannot:
- Stack (Add always returns overflow)
- Merge with other equipment
- Split

### GUID Preservation

Equipment GUID is preserved through:
- Clone(): Same GUID reference
- Drop/pickup: Same instance stored in ItemWorldPickup
- Inventory operations: Data never duplicated

## Player Equipment

### PlayerEquipment Component

Manages equipped items:

```csharp
public class PlayerEquipment : MonoBehaviour
{
    public event Action<EquipmentSlot, EquipmentData> OnEquipmentChanged;
    public event Action<EquipmentData, ResolvedWeaponStats> OnWeaponEquipped;
    public event Action OnWeaponUnequipped;

    public bool TryEquip(int inventorySlotIndex);
    public bool TryUnequip();
    public void RecordKill();  // Increment kill counter

    public EquipmentData EquippedWeapon { get; }
    public ResolvedWeaponStats? CurrentWeaponStats { get; }
}
```

### Equip Flow

1. PlayerEquipment.TryEquip(slotIndex)
2. Validates slot contains weapon with equipment data
3. Unequips current weapon if any
4. Sets IsEquipped flag on new equipment
5. Resolves stats and notifies PlayerMeleeWeapon
6. Fires OnEquipmentChanged and OnWeaponEquipped events

### Combat Integration

PlayerMeleeWeapon uses equipment stats:

```csharp
public class PlayerMeleeWeapon
{
    public void SetEquipmentStats(ResolvedWeaponStats stats);

    // Returns equipment damage if set, else config damage
    public float GetEffectiveDamage();
    public float GetEffectiveRange();
    public float GetEffectiveStagger();
}
```

## UI

### Inventory Equipment Display

Extended InventoryUI shows:

- **Slot indicators**: Green left border for equipped items
- **Rarity coloring**: Bottom border color matches rarity
- **Details panel**: Name, description, stats, affixes
- **Condition display** (Slice 0.7.1): Label, durability bar, condition-colored
- **History**: Found location and enemies defeated

### Stats Display

Equipment stats shown when selected:
- Damage: Base value with affix modifier
- Speed: Attacks per second
- Range: Attack reach
- Stagger: Knockback force

### Comparison

When viewing unequipped weapon with weapon already equipped:
- Shows stat difference (green +, red -)
- Compares resolved stats

### Equip/Unequip Button

- Shows "Equip" for unequipped weapons
- Shows "Unequip" for equipped weapon
- Equipped items cannot be dropped

## Loot System

### Enemy Equipment Drops

EnemyConfig extended with:

| Parameter | Description |
|-----------|-------------|
| WeaponLoot | Array of WeaponDefinition |
| WeaponDropChance | 0-1 probability |

### Spawn Flow

1. EnemyHealth.HandleDied() calls SpawnLoot()
2. SpawnEquipmentLoot() rolls against WeaponDropChance
3. If successful, picks random WeaponDefinition
4. EquipmentGenerator creates instance with enemy name as location
5. ItemWorldPickup.SpawnPickup() places in world

### GUID Preservation on Pickup

ItemWorldPickup stores full ItemInstance:
- Equipment data preserved across drop/pickup
- No regeneration of GUID or stats

## Configuration

### EquipmentRegistry

ScriptableObject holding all definitions:

- WeaponDefinitions list
- AffixDefinitions list
- ItemDefinitions list

Provides lookup by ID:

```csharp
public interface IEquipmentRegistry
{
    WeaponDefinition GetWeaponDefinition(string id);
    AffixDefinition GetAffixDefinition(string id);
    ItemDefinition GetItemDefinition(string id);
}
```

### Required Assets

Create via Unity menu "Lootbound/Equipment/...":

1. WeaponDefinition assets
2. AffixDefinition assets
3. EquipmentRegistry asset (references all above)

## Tests

EditMode tests in `EquipmentTests.cs`:

### EquipmentData Tests
- GUID generation uniqueness
- Property initialization
- Clone preserves GUID
- CloneWithNewId generates new GUID
- IsValid validation

### EquipmentHistory Tests
- Found location recording
- Kill counter increment
- Equip counter increment
- Clone copies all data

### AffixInstance Tests
- Property initialization
- Definition caching
- Positive value calculation
- Negative value calculation

### ResolvedWeaponStats Tests
- Property initialization
- Duration multiplier calculation
- Default and Invalid values

### Stat Resolution Tests
- Base stats without affixes
- Damage bonus application
- Speed bonus application
- Multiple affix accumulation
- Value clamping

### ItemInstance Equipment Tests
- HasEquipmentData flag
- Cannot stack
- Cannot merge
- Cannot split
- Clone preserves GUID
- CloneAsNewEquipment generates new GUID

### EquipmentGenerator Tests
- Creates valid instances
- Deterministic with same seed
- Specific rarity generation
- Simple weapon creation

### EquipmentRegistry Tests
- Weapon lookup
- Affix lookup
- Tier filtering
- Validation

### Inventory Equipment Tests
- Single slot per equipment
- Multiple weapons use separate slots
- Full inventory rejection

### Equipment Condition Tests (Slice 0.7.1)
- Condition at 100% is Excellent
- Condition at 80% is Excellent (boundary)
- Condition at 79% is Good
- Condition at 60% is Good (boundary)
- Condition at 59% is Worn
- Condition at 35% is Worn (boundary)
- Condition at 34% is Fragile
- Condition at 1% is Fragile (boundary)
- Condition at 0% is Broken
- Negative durability is Broken
- Condition from current/max calculation
- Zero max durability is Broken
- Distinct colors for each condition
- Non-empty tooltip for each condition
- CanUseInCombat true for all conditions (including Broken - see 0.7.3)

### Equipment Durability Tests (Slice 0.7.1)
- New equipment has full durability
- Default durability is 100
- SetDurability clamps to bounds
- ReduceDurability decreases value
- ReduceDurability clamps to zero
- ReduceDurability ignores negative amount
- RestoreDurability increases value
- RestoreDurability clamps to max
- RestoreDurability ignores negative amount
- NormalizedDurability calculation
- Condition updates with durability changes
- Clone preserves durability
- CloneWithNewId starts at full durability
- Serialization preserves durability
- Negative max durability clamped to 1

### Weapon Wear Tests (Slice 0.7.2)

EditMode tests in `WeaponWearTests.cs`:

**WeaponWearConfig Tests**
- GetChance returns correct values per cause
- GetAmount returns correct values per cause
- IsHeavyTarget checks threshold correctly

**WearContext Tests**
- SuccessfulHit creates correct context
- PlayerDamaged creates correct context
- Debug creates correct context

**WearResult Tests**
- NoWear returns correct properties
- Applied returns correct properties
- ConditionChanged detects changes
- NowBroken detects broken state

**WeaponWearSystem Tests**
- TryApplyWear with null equipment returns NoWear
- TryApplyWear with broken equipment returns NoWear
- Deterministic with same seed
- Zero chance never applies
- 100% chance always applies
- Attack ID tracking prevents duplicate wear
- ResetForNewAttack clears tracking
- ApplyDebugWear always applies
- TryApplyHitWear applies both normal and heavy wear
- OnWearApplied event fires
- OnConditionChanged fires only when condition changes
- Condition transitions (Excellent→Good, Fragile→Broken)
- PlayerDamaged applies wear
- PlayerDamaged not affected by attack ID tracking
- Full combat scenario integration test

### Broken Weapons Tests (Slice 0.7.3)

EditMode tests in `EquipmentTests.cs`:

**BrokenWeaponConfig Tests**
- ApplyPenalties reduces all stats by multipliers
- GetPenaltyPercent returns negative percentages for UI

**Broken Stat Resolution Tests**
- Broken weapon with config applies penalties
- Non-broken weapon with config has no penalties
- Broken weapon without config has no penalties (backward compatibility)

**Resolution Order**
- Base stats → Affixes → Broken penalties → Clamp
- Sharp affix applied before broken multiplier
- Swift affix applied before broken multiplier

**Identity Conservation**
- GUID unchanged when broken
- Affixes unchanged when broken
- History unchanged when broken
- Kill count unchanged when broken

**Combat Integration**
- CanUseInCombat returns true for Broken
- IsBroken helper detects broken state
- Stats recalculate on condition change

## Scene Setup

### Required References

PlayerEquipment component needs:
- PlayerInventory reference
- PlayerMeleeWeapon reference
- EquipmentRegistry reference

PlayerWeaponWear component needs (Slice 0.7.2):
- PlayerEquipment reference
- PlayerCombatController reference
- PlayerHealth reference
- WeaponWearConfig reference

### Prefab Updates

```
PlayerCharacter
├── ... (existing)
├── PlayerInventory
├── PlayerEquipment
├── PlayerWeaponWear (Slice 0.7.2)
└── Camera
    └── PlayerMeleeWeapon (updated)
```

### ScriptableObject Assets

Create in Assets/Lootbound/ScriptableObjects/Equipment/:

```
Equipment/
├── Weapons/
│   ├── Weapon_OldBlade.asset
│   ├── Weapon_TravelerBlade.asset
│   └── Weapon_HeavyBlade.asset
├── Affixes/
│   ├── Affix_Sharp.asset
│   ├── Affix_Swift.asset
│   ├── Affix_Balanced.asset
│   └── Affix_Heavy.asset
├── WeaponWearConfig.asset (Slice 0.7.2)
├── BrokenWeaponConfig.asset (Slice 0.7.3)
└── EquipmentRegistry.asset
```

## Broken Weapons (Slice 0.7.3)

### Philosophy

A broken weapon is not destroyed. It remains:
- In the inventory
- Equipped if it was equipped
- Usable in combat
- The same instance (GUID preserved)

But it suffers severe penalties that encourage returning to the refuge.

### BrokenWeaponConfig

```csharp
[CreateAssetMenu(menuName = "Lootbound/Equipment/Broken Weapon Config")]
public class BrokenWeaponConfig : ScriptableObject
{
    float damageMultiplier;      // Default: 0.30 (70% reduction)
    float attackSpeedMultiplier; // Default: 0.55 (45% reduction)
    float rangeMultiplier;       // Default: 0.90 (10% reduction)
    float staggerMultiplier;     // Default: 0.20 (80% reduction)
}
```

### Stat Resolution Order

```
Base weapon stats (from WeaponDefinition)
    ↓
Affixes applied (Sharp, Swift, etc.)
    ↓
Broken penalties (if condition == Broken && config != null)
    ↓
Clamp to minimums (damage ≥ 1, speed 0.3-3, range 0.5-4)
    ↓
Final ResolvedWeaponStats
```

### UI Feedback

- **Broken badge**: Red warning badge in inventory details
- **Penalty percentages**: Stats displayed with penalty indicators
- **Break notification**: Distinctive notification when weapon breaks
- **Visual feedback**: Desaturation + tint on first-person weapon view

### Debug Tools

Combat Debug Overlay (F6) shows:
- Equipment section with condition and durability
- Debug buttons: "Apply Wear", "Break", "Restore"

## Repair Fragments System (Slice 0.7.4)

The repair system allows players to restore weapon durability using repair fragments.

### Philosophy

Repair fragments are a consumable resource that transforms equipment maintenance into a meaningful gameplay decision. Players must balance:
- When to repair (during expedition vs at refuge)
- How much to repair (partial vs full)
- Which equipment to prioritize

Equipment repair preserves identity - the same weapon with the same GUID, affixes, and history.

### Architecture

```
Equipment/Repair/
├── RepairConfig.cs         - ScriptableObject for tuning
├── RepairFailureReason.cs  - Enum for failure reasons
├── RepairPreview.cs        - Preview of repair operation
├── RepairRequest.cs        - Request data structure
├── RepairResult.cs         - Result of repair execution
├── RepairService.cs        - Pure C# repair logic
└── PlayerRepair.cs         - MonoBehaviour connector
```

### RepairConfig

ScriptableObject defining repair parameters:

| Parameter | Default | Description |
|-----------|---------|-------------|
| DurabilityPerFragment | 20 | Durability restored per fragment |
| CanRepairBroken | true | Whether broken equipment can be repaired |
| MaxRepairPercentage | 1.0 | Maximum durability % reachable (0.5-1.0) |

### RepairFailureReason

Reasons why repair cannot proceed:

| Reason | Description |
|--------|-------------|
| None | No failure - repair can proceed |
| NoEquipmentSelected | No equipment selected |
| AlreadyFullDurability | Equipment at max durability |
| NoFragmentsAvailable | No repair fragments in inventory |
| InsufficientFragments | Not enough fragments for request |
| BrokenRepairNotAllowed | Config prevents broken repair |
| InvalidConfig | Repair system misconfigured |
| InventoryTransactionFailed | Fragment removal failed |

### RepairPreview

Preview of a repair operation before committing:

```csharp
public readonly struct RepairPreview
{
    public bool CanRepair { get; }
    public RepairFailureReason FailureReason { get; }
    public float CurrentDurability { get; }
    public float DurabilityAfterRepair { get; }
    public EquipmentCondition ConditionBefore { get; }
    public EquipmentCondition ConditionAfter { get; }
    public int FragmentsAvailable { get; }
    public int FragmentsForFullRepair { get; }
    public int FragmentsToConsume { get; }
    public bool WillChangeCondition { get; }
    public bool IsFullRepair { get; }
}
```

### RepairResult

Result after executing repair:

```csharp
public readonly struct RepairResult
{
    public bool Success { get; }
    public RepairFailureReason FailureReason { get; }
    public string EquipmentName { get; }
    public float DurabilityBefore { get; }
    public float DurabilityAfter { get; }
    public EquipmentCondition ConditionBefore { get; }
    public EquipmentCondition ConditionAfter { get; }
    public int FragmentsConsumed { get; }
    public bool RestoredFromBroken { get; }
}
```

### RepairService

Pure C# class for repair logic:

```csharp
public class RepairService
{
    public event Action<RepairResult> OnRepairCompleted;

    public int GetAvailableFragments();
    public RepairPreview PreviewRepair(EquipmentData equipment, int fragmentCount = 0);
    public RepairResult ExecuteRepair(RepairRequest request);
    public RepairResult ExecuteFullRepair(EquipmentData equipment);
    public RepairResult ExecutePartialRepair(EquipmentData equipment, int fragmentCount);
    public bool CanRepair(EquipmentData equipment);
    public bool NeedsRepair(EquipmentData equipment);
}
```

Key features:
- Atomic transaction: fragments consumed only if durability restored
- Preview before commit
- Supports partial repair (use N fragments)
- Supports full repair (use minimum needed)
- Stats recalculated when condition changes

### PlayerRepair

MonoBehaviour connecting to Unity systems:
- References RepairConfig, PlayerInventory, PlayerEquipment
- Creates RepairService instance
- Exposes convenient repair methods
- Fires events for UI binding

### Repair UI

Repair panel in InventoryUI shows when equipment needs repair:
- Available repair fragments count
- Durability preview (before → after)
- Condition change preview (if applicable)
- Fragments to consume
- Repair button (Full Repair / Repair)

If repair not possible, shows failure reason.

### Fragment Calculation

Fragments needed = ceil((targetDurability - currentDurability) / durabilityPerFragment)

With default config (20 per fragment):
- 0 → 100 durability = 5 fragments
- 40 → 100 durability = 3 fragments
- 80 → 100 durability = 1 fragment

### Identity Preservation

Repair maintains equipment identity:
- Same GUID
- Same affixes
- Same history (kill count, found location)
- Same custom name
- Only durability changes

### Debug Tools

Combat Debug Overlay (F6) includes Repair section:
- Available fragments count
- Can repair status
- Repair preview (fragments needed, condition after)
- "+5 Fragments" and "+20 Fragments" buttons
- "Repair" button (when possible)

### Repair Tests (Slice 0.7.4)

EditMode tests in `RepairTests.cs`:

**RepairConfig Tests**
- CalculateFragmentsForFullRepair returns correct value
- CalculateFragmentsForFullRepair returns zero for full durability
- CalculateFragmentsForFullRepair respects max percentage
- CalculateDurabilityRestored returns correct value

**RepairPreview Tests**
- Successful preview has correct properties
- Failed preview has correct properties
- WillChangeCondition detects changes

**RepairResult Tests**
- Successful result has correct properties
- Failed result has correct properties
- RestoredFromBroken detects broken repair

**RepairService Tests**
- PreviewRepair with null equipment returns failure
- PreviewRepair with full durability returns already full
- PreviewRepair with no fragments returns no fragments
- PreviewRepair with broken and not allowed returns not allowed
- PreviewRepair with valid inputs returns correct preview
- PreviewRepair with partial fragments returns partial repair
- PreviewRepair with specific count uses specified count
- ExecuteRepair restores durability and consumes fragments
- ExecuteRepair with partial repair consumes correct fragments
- ExecuteRepair updates condition
- ExecuteRepair repairs broken equipment
- ExecuteRepair fires event
- CanRepair returns true when possible
- CanRepair returns false when not possible
- NeedsRepair returns true when damaged
- GetAvailableFragments returns correct count
- Full repair workflow integration test
- MaxRepairPercentage limits repair

### V1 Limitations

Not implemented in Slice 0.7.4:
- Repair Station (3D workbench at refuge) - deferred to Slice 0.7.5
- Repair animation/VFX
- Repair audio
- Fragment crafting
- Different fragment qualities
- Armor repair

## V1 Limitations

Not implemented:
- Epic and Legendary rarities
- Multiple affix slots
- ~~Equipment durability~~ → Implemented in Slice 0.7.1 (foundation only)
- ~~Equipment repair~~ → Implemented in Slice 0.7.4 (repair fragments)
- Equipment enhancement
- ~~Durability loss from combat~~ → Implemented in Slice 0.7.2
- Armor equipment
- Accessory equipment
- Equipment set bonuses
- Equipment comparison tooltip
- Equipment sorting
- Equipment favoriting
- Equipment locking

## Next Steps

Future slices may add:
- Equipment repair mechanics
- Enhancement system with risk/reward
- Armor and accessory slots
- Additional affix types
- Equipment sets with bonuses
- Named/unique equipment
