# World Rings

The WorldRing system defines concentric zones radiating outward from the Refuge. Each ring has distinct characteristics that affect gameplay, encounters, and resource availability.

## Ring Structure

The world is divided into 7 concentric rings:

| Ring | Normalized Radius | Description |
|------|-------------------|-------------|
| **Refuge** | 0.00 - 0.05 | The player's safe starting area |
| **Nearlands** | 0.05 - 0.15 | Familiar territory near the Refuge |
| **Wildlands** | 0.15 - 0.35 | The first true wilderness areas |
| **Farlands** | 0.35 - 0.55 | More dangerous, requiring preparation |
| **Outerlands** | 0.55 - 0.75 | Remote regions with rare rewards |
| **Edgelands** | 0.75 - 0.90 | The boundary between known and unknown |
| **Void** | 0.90+ | Future expansion territory |

## Technical Implementation

### Boundary Rule

All rings use the `[min, max)` rule:
- **Minimum** is inclusive: a position at exactly 0.05 normalized radius is in Nearlands
- **Maximum** is exclusive: a position at 0.149 normalized radius is still in Nearlands
- **Void** uses `[min, +∞]`: it extends infinitely beyond the world disc

### Core Types

#### WorldRing Enum
```csharp
public enum WorldRing
{
    Refuge,      // Safe starting area
    Nearlands,   // Near the Refuge
    Wildlands,   // First wilderness
    Farlands,    // Distant regions
    Outerlands,  // Remote territory
    Edgelands,   // World boundary
    Void         // Beyond the edge
}
```

#### WorldRingSample
```csharp
public readonly struct WorldRingSample
{
    public float DistanceFromRefuge { get; }
    public float NormalizedWorldRadius { get; }
    public WorldRing Ring { get; }
}
```

#### WorldRingConfig
A ScriptableObject that defines ring thresholds. It includes strict validation:
- Refuge must start at 0
- Void must be last
- Thresholds must be strictly ascending
- No duplicates allowed
- All rings must be present

### Evaluation

Use `WorldRingEvaluator` to evaluate positions:

```csharp
// Evaluate a world position
WorldRingSample sample = WorldRingEvaluator.Evaluate(
    position,
    refugePosition,
    worldDiscRadius,
    ringConfig
);

// Access the results
float distance = sample.DistanceFromRefuge;
float normalized = sample.NormalizedWorldRadius;
WorldRing ring = sample.Ring;
```

### WorldDiscDefinition

The `WorldDiscDefinition` encapsulates the logical world:

```csharp
// Full world definition
var worldDisc = new WorldDiscDefinition(worldRadius, ringConfig);

// Compressed preview mode (prototype terrain smaller than logical world)
var compressedWorldDisc = new WorldDiscDefinition(
    worldRadius: 2000f,
    previewTerrainRadius: 512f,
    ringConfig
);
```

## Key Concepts

### NormalizedWorldRadius

`NormalizedWorldRadius` is calculated against the **logical WorldDisc radius**, NOT the local terrain size. This is critical for:
- Ring determination
- Threat level scaling
- Resource distribution
- Encounter difficulty

```csharp
// CORRECT: Use WorldDiscDefinition.WorldRadius
float normalized = distance / worldDiscDefinition.WorldRadius;

// WRONG: Do NOT use terrain size
float normalized = distance / terrain.size.x; // INCORRECT
```

### Distance vs Ring

Two ways to measure position:
- **DistanceFromRefuge**: Absolute distance in meters
- **WorldRing**: Categorical zone classification

Use Ring for gameplay rules (encounter types, loot tables).
Use DistanceFromRefuge for precise calculations (threat gradient).

## Gameplay Intent

The ring system supports the core expedition loop:
- **Refuge**: Preparation, repair, enhancement
- **Nearlands**: Learning, safe exploration
- **Wildlands → Outerlands**: Progressive challenge and reward
- **Edgelands**: High-risk expeditions for the prepared
- **Void**: Future content, currently represents the world boundary

Each ring further from Refuge increases:
- Encounter difficulty
- Resource quality
- Equipment requirements
- Return journey tension

## Configuration

Create a WorldRingConfig asset:
1. Right-click in `Assets/Lootbound/ScriptableObjects/`
2. Create > Lootbound > World Ring Config
3. Configure thresholds in the Inspector

The default configuration provides balanced ring sizes:
```
Refuge:     0.00 - 0.05 (5% of radius)
Nearlands:  0.05 - 0.15 (10% of radius)
Wildlands:  0.15 - 0.35 (20% of radius)
Farlands:   0.35 - 0.55 (20% of radius)
Outerlands: 0.55 - 0.75 (20% of radius)
Edgelands:  0.75 - 0.90 (15% of radius)
Void:       0.90+       (10%+ of radius)
```

## Validation

WorldRingConfig validates strictly:
- Invalid configs throw exceptions on use
- Editor validation logs errors immediately
- No silent fallbacks or default behavior

This ensures ring boundaries are always well-defined and predictable.
