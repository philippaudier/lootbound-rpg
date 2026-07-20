# World Layout Generation V1 (Radial Architecture)

## Overview

The world layout system generates a radial graph-based expedition structure from terrain data. Multiple RadialPaths emanate from a central Refuge toward OuterDestinations at the world's edge.

**Key Principle**: The layout is terrain-aware during generation. It queries terrain noise functions to find traversable routes with natural curvature, then terrain is gently adapted to support the chosen paths.

**Critical Invariant**: All primary path edges must ALWAYS be traversable. Generation fails explicitly if no valid paths exist.

## Core Concepts

### WorldDisc and Rings

The world is organized as a circular disc with concentric rings. See [WORLD_RINGS.md](WORLD_RINGS.md) for details.

- **WorldDiscDefinition**: Defines the logical world (radius, ring config)
- **WorldRing**: Categorical zone (Refuge, Nearlands, Wildlands, etc.)
- **NormalizedWorldRadius**: Position normalized to WorldDisc radius (0-1)

### Nodes

Nodes represent significant locations in the world:

| Type | Description |
|------|-------------|
| **Refuge** | Player's safe starting area (only one, at world center) |
| **Junction** | Connection point along paths |
| **Clearing** | Open area suitable for encounters or rest |
| **Viewpoint** | Elevated position with good sightlines |
| **Landmark** | Distinctive terrain feature for navigation |
| **OuterDestination** | Terminal node of a RadialPath |
| **DeadEnd** | Terminal node of a branch |

Each node has:
- Deterministic ID: `node_{seed}_{type}_{index}`
- World position with terrain height
- Radius defining area of influence
- **DistanceFromRefuge**: Absolute distance in meters
- **NormalizedWorldRadius**: Distance normalized to WorldDisc (0-1)
- **Ring**: WorldRing zone classification
- **RadialPathId**: Which RadialPath this node belongs to (null for Refuge)
- **PathStepIndex**: Position along primary path (-1 for branches)

### RadialPaths

RadialPaths are terrain-aware routes radiating outward from the Refuge:

- **StartAngle**: Initial angular direction from Refuge (degrees)
- **NodeIds**: Ordered list of primary path nodes
- **EdgeIds**: Ordered list of primary path edges
- **OuterDestinationNodeId**: Terminal node of this path

Paths curve naturally following terrain - they are NOT rigid spokes. Curvature emerges from scoring (outward progression + terrain slope + curvature penalty).

### Edges

Edges connect nodes and represent traversable paths:

| Property | Description |
|----------|-------------|
| **EdgeId** | Deterministic ID: `edge_{seed}_{nodeAIndex}_{nodeBIndex}` |
| **RadialPathId** | Which RadialPath this edge belongs to |
| **IsPrimaryPathEdge** | True for main radial paths, false for branches |
| **ControlPoints** | Sampled points along the path for terrain evaluation |
| **AverageSlope** | Average slope along control points |
| **MaxSlope** | Maximum slope encountered |
| **IsTraversable** | Whether the edge is walkable |

### Reservations

Reservations mark positions for future content spawning:

| Type | Description |
|------|-------------|
| **EncounterReservation** | Position for enemy encounters |
| **ResourceReservation** | Position for resource nodes |

> There is no landmark reservation. Since slice 0.9.10 landmarks are derived
> directly from elevated/terminal layout **nodes** (Viewpoint, Landmark,
> OuterDestination) by the `LandmarkPlanner` - see `LANDMARKS.md`.

All reservations inherit radial properties from their host node:
- DistanceFromRefuge
- NormalizedWorldRadius
- Ring
- RadialPathId

## Generation Flow

```
WorldSeed
  ↓
TerrainHeightGenerator.Generate() (heightmap + normalized map + slope map)
  ↓
WorldDiscDefinition created (WorldRadius, RingConfig)
  ↓
TerrainContextSampler created (reads the context's NormalizedHeightMap/SlopeMap —
the same height space applied to the Unity Terrain)
  ↓
WorldLayoutGenerator.Generate()
  ├── Find Refuge position (near world center with bounded offset)
  ├── Create Refuge node
  ├── Determine RadialPath count and distribute angles
  ├── For each RadialPath:
  │   ├── Generate nodes outward with emergent curvature
  │   ├── Score candidates: outward progression + slope + curvature
  │   │   (if no candidate fits — e.g. near the world margin — retry once
  │   │   with shorter steps and a ±90° spread to curve along the edge)
  │   └── Terminal node becomes OuterDestination
  ├── Generate branches from primary path nodes
  │   └── Branches inherit RadialPathId from anchor
  ├── Create reservations attached to nodes
  └── Validate structure and traversability
  ↓
TerrainHeightGenerator.ApplyLayoutFlattening()
  ├── Gentle corridors along primary paths
  └── Flattened clearings
  ↓
WorldLayoutGenerator.ReprojectReservationHeights()
  └── Reservation Y re-sampled so stored positions match the final terrain
  ↓
WorldLayoutValidator.ValidateAgainstTerrain()
  ↓
Done
```

## Determinism

Same seed always produces:
- Same node IDs and positions
- Same RadialPath structure
- Same edge connections
- Same ring assignments

Generation uses deterministic retry with salt:
```csharp
for (int attempt = 0; attempt < maxAttempts; attempt++)
{
    int effectiveSeed = HashCombine(worldSeed, attempt);
    result = TryGenerate(effectiveSeed, ...);
    if (result.Success) break;
}
```

The resulting layout stores:
- `WorldSeed`: Original seed
- `GenerationAttempt`: Which retry succeeded
- `EffectiveLayoutSeed`: Actual seed used

## Configuration

`WorldLayoutConfig` ScriptableObject controls:

### Generation Retries
- `maxGenerationAttempts`: Maximum attempts before failing

### RadialPath Generation
- `minimumRadialPathCount`: Minimum paths from Refuge
- `maximumRadialPathCount`: Maximum paths from Refuge
- `nodesPerRadialPath`: Nodes per path (excluding Refuge)
- `radialStepMin/Max`: Distance between nodes in meters
- `primaryPathMaxSlope`: Maximum traversable slope in degrees

### Path Distribution
- `minimumAngularSeparation`: Minimum degrees between adjacent paths
- `maxAngularGap`: Maximum degrees gap between paths

### Candidate Scoring
- `outwardProgressionWeight`: Weight for outward movement
- `terrainSlopeWeight`: Weight for low slope preference
- `curvaturePenaltyWeight`: Penalty for sharp turns

### Branches
- `branchCount`: Number of secondary branches
- `branchMaxNodes`: Maximum nodes per branch
- `branchChance`: Probability each node spawns a branch

### Node Radii
- `refugeRadius`, `junctionRadius`, `clearingRadius`, etc.

### Terrain Correction
- `corridorWidth`: Path corridor width
- `maxCorrectionStrength`: Maximum vertical correction

## Radial Properties

### DistanceFromRefuge
- Absolute distance in meters (XZ plane)
- Refuge = 0, increases outward
- Used for continuous calculations

### NormalizedWorldRadius
- DistanceFromRefuge / WorldDiscDefinition.WorldRadius
- Always 0-1 within the world disc
- **Calculated from logical world radius, NOT terrain size**

### WorldRing
- Categorical zone based on NormalizedWorldRadius
- Determined by WorldRingConfig thresholds
- Used for gameplay rules (difficulty, loot, etc.)

### PathStepIndex
- Primary path nodes: 0, 1, 2, ... (index along path)
- Refuge: -1
- Branch nodes: -1

## Validation

### Structural Validation
- Refuge node exists at center
- At least one RadialPath exists
- Each path has OuterDestination
- All nodes reachable from Refuge
- All IDs unique

### Radial Property Validation
- Refuge has DistanceFromRefuge = 0
- Refuge has NormalizedWorldRadius = 0
- Refuge is in WorldRing.Refuge
- PathStepIndex matches position along paths

### Traversability Validation
- All primary path edges have `IsTraversable = true`
- All OuterDestinations reachable via primary paths

## Debug Visualization

Add `WorldLayoutGizmos` component to view in Scene:

- **Rings**: Concentric circles showing zone boundaries
- **Nodes**: Color-coded spheres by type
- **Edges**: Green lines for primary paths, yellow for branches
- **Labels**: Node type and ring
- **Reservations**: Cubes for E/R (encounter/resource)

## Files

```
Assets/Lootbound/Gameplay/World/Layout/
├── ITerrainSampler.cs           # Interface for terrain queries
├── WorldRing.cs                 # Ring enum
├── WorldRingSample.cs           # Ring evaluation result
├── WorldRingConfig.cs           # Ring threshold configuration
├── WorldRingEvaluator.cs        # Ring evaluation functions
├── WorldDiscDefinition.cs       # Logical world definition
├── RadialPath.cs                # Path structure
├── WorldNodeType.cs             # Node type enum
├── WorldNode.cs                 # Node data structure
├── WorldEdge.cs                 # Edge data structure
├── WorldReservations.cs         # Reservation classes
├── WorldLayoutContext.cs        # Complete layout data
├── WorldLayoutConfig.cs         # ScriptableObject configuration
├── WorldLayoutGenerator.cs      # Generation algorithms
├── WorldLayoutResult.cs         # Generation result wrapper
├── WorldLayoutValidator.cs      # Validation functions
└── WorldLayoutGizmos.cs         # Scene view debug visualization

Assets/Lootbound/Tests/EditMode/
└── WorldLayoutTests.cs          # Unit tests
```

## V1 Scope

### Included
- Central Refuge with bounded offset
- Multiple RadialPaths with terrain-aware curvature
- OuterDestinations at path terminals
- Branches inheriting RadialPathId
- WorldRing assignment for all nodes
- Encounter/Resource reservations
- Deterministic generation with bounded retries
- Ring visualization in Scene view

### Excluded from V1
- Threat levels (future slice)
- Enemy/resource spawning
- Ring-specific biomes
- Terrain streaming

## Related Documentation

- [WORLD_RINGS.md](WORLD_RINGS.md): Ring system details
- [WORLD_DISC_AND_STREAMING_VISION.md](WORLD_DISC_AND_STREAMING_VISION.md): Future terrain streaming
- [WORLD_RPG_PROGRESSION.md](WORLD_RPG_PROGRESSION.md): Progression design
