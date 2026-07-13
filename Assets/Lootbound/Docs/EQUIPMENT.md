# Equipment & Loot Identity System (Slice 0.6)

This document describes the equipment and loot identity system implemented in Slice 0.6.

## Overview

The equipment system provides unique, persistent equipment identity with:
- GUID-based unique identification per equipment instance
- Separation between definitions (templates) and instances (unique items)
- Rarity system (Common, Uncommon, Rare for V1)
- Affix system with stat modifiers
- Generated names based on rarity
- History tracking (found location, enemies defeated)
- Equipment/unequipment from inventory
- Combat integration with resolved stats
- UI for equipment details and comparison

## Architecture

```
Equipment/
├── Definitions/
│   ├── WeaponDefinition      - ScriptableObject extending ItemDefinition
│   ├── AffixDefinition       - ScriptableObject for affix templates
│   └── EquipmentRegistry     - Registry of all definitions
├── Instances/
│   ├── EquipmentData         - Unique instance with GUID and affixes
│   ├── AffixInstance         - Rolled affix with value
│   └── EquipmentHistory      - Tracks found location, kills, equips
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

## Scene Setup

### Required References

PlayerEquipment component needs:
- PlayerInventory reference
- PlayerMeleeWeapon reference
- EquipmentRegistry reference

### Prefab Updates

```
PlayerCharacter
├── ... (existing)
├── PlayerInventory
├── PlayerEquipment (new)
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
└── EquipmentRegistry.asset
```

## V1 Limitations

Not implemented:
- Epic and Legendary rarities
- Multiple affix slots
- Equipment durability
- Equipment repair
- Equipment enhancement
- Armor equipment
- Accessory equipment
- Equipment set bonuses
- Equipment comparison tooltip
- Equipment sorting
- Equipment favoriting
- Equipment locking

## Next Steps

Future slices may add:
- Equipment durability and repair
- Enhancement system with risk/reward
- Armor and accessory slots
- Additional affix types
- Equipment sets with bonuses
- Named/unique equipment
