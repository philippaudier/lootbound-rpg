# World RPG Progression

This document describes how the radial world structure supports RPG progression. The ring system creates a natural difficulty gradient that rewards preparation and exploration.

## Core Progression Loop

```
Refuge (safe)
    ↓
Prepare equipment
    ↓
Expedition into rings
    ↓
Encounters scale with ring
    ↓
Loot quality scales with ring
    ↓
Return to Refuge
    ↓
Repair, enhance, repeat
```

## Ring-Based Scaling

### Encounter Difficulty

Each ring multiplies base encounter difficulty:

| Ring | Difficulty Modifier | Typical Enemies |
|------|---------------------|-----------------|
| Refuge | 0.0x (safe) | None |
| Nearlands | 0.5x | Scouts, scavengers |
| Wildlands | 1.0x (baseline) | Packs, hunters |
| Farlands | 1.5x | Elite variants |
| Outerlands | 2.0x | Rare, dangerous |
| Edgelands | 2.5x+ | Unique threats |
| Void | Future content | Unknown |

### Loot Quality

Better rings yield better loot:

| Ring | Drop Tier | Enhancement Potential |
|------|-----------|----------------------|
| Refuge | None | N/A |
| Nearlands | Common | Low |
| Wildlands | Common-Uncommon | Medium |
| Farlands | Uncommon-Rare | High |
| Outerlands | Rare-Epic | Very High |
| Edgelands | Epic-Legendary | Maximum |

### Resource Density

Resource nodes appear more frequently and yield more in outer rings:

```
Nearlands:  Basic materials, common
Wildlands:  Standard materials
Farlands:   Quality materials, less common
Outerlands: Rare materials
Edgelands:  Exceptional materials, sparse
```

## RadialPath Progression

Each RadialPath represents a distinct expedition route:

### Path Identity

- Different paths may favor different enemy types
- Terrain along paths affects encounter setup
- OuterDestinations offer unique rewards

### Branch Exploration

Branches off primary paths provide:
- Optional challenges
- Hidden resources
- Alternate routes
- Rest points (Clearings)
- Scenic viewpoints

## Distance vs Ring

The system uses two progression measures:

### DistanceFromRefuge

- Continuous value in meters
- Used for precise threat scaling
- Affects encounter spawn rates
- Determines equipment degradation rate

### WorldRing

- Categorical zone classification
- Determines enemy types available
- Affects loot tables
- Gates content by player progression

## Equipment Degradation

Equipment condition interacts with distance:

```
Condition loss rate = BaseRate × (1 + DistanceFromRefuge / WorldRadius)
```

Further expeditions strain equipment more:
- Nearlands: Minimal wear
- Wildlands: Standard wear
- Farlands+: Accelerated wear

This creates natural expedition limits based on equipment quality.

## Attunement Opportunities

Attunement stones appear at significant locations:
- OuterDestinations (guaranteed)
- Viewpoints (chance)
- Deep branch terminals (chance)

Higher ring = higher attunement tier available.

## Return Journey Tension

The radial structure creates inherent tension:
- Distance to Refuge increases linearly
- Threat remains based on current position
- Equipment degrades throughout
- Resources must last round trip

This encourages:
- Careful resource management
- Strategic retreat decisions
- Gradual territory learning

## Progression Gates

Natural gates emerge from the system:

1. **Equipment Gate**: Better gear needed for outer rings
2. **Knowledge Gate**: Learn enemy patterns in inner rings
3. **Resource Gate**: Stock supplies for longer expeditions
4. **Skill Gate**: Combat proficiency for harder encounters

No artificial barriers - progression emerges from gameplay.

## Future Considerations

### Ring-Specific Content (Not Implemented)

Future slices may add:
- Biome variation per ring
- Weather intensity scaling
- Day/night danger changes
- Seasonal effects

### Mastery Indicators (Vision)

Track player mastery by ring:
- Times reaching each ring
- Enemies defeated per ring
- Resources gathered per ring
- Successful returns from ring

## Technical Notes

### Using Ring for Gameplay

```csharp
// Get ring at player position
WorldRingSample sample = worldDisc.EvaluateAt(playerPos, refugePos);

// Scale encounter difficulty
float difficultyMod = GetDifficultyModifier(sample.Ring);

// Determine loot table
LootTable table = GetLootTableForRing(sample.Ring);

// Check equipment requirements
bool canHandle = equipment.Rating >= GetMinRatingForRing(sample.Ring);
```

### Gradual Transition

Ring boundaries are hard transitions for game rules, but:
- Visual terrain changes should be gradual
- Enemy density should blend across boundaries
- Player should sense approaching boundary

## Related Documentation

- [WORLD_RINGS.md](WORLD_RINGS.md): Ring system implementation
- [WORLD_LAYOUT.md](WORLD_LAYOUT.md): Layout structure
- [EQUIPMENT.md](EQUIPMENT.md): Equipment system
