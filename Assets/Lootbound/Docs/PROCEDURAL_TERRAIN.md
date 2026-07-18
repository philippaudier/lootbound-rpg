# Procedural Terrain V1

This document describes the procedural terrain generation system implemented in Slice 0.3.

## Overview

The terrain system generates a finite, explorable world from a seed. The same seed always produces the same terrain, enabling reproducible worlds.

The V1 implementation uses Unity's built-in Terrain system with procedurally generated heightmaps and surface painting.

## Components

### TerrainGenerationConfig

ScriptableObject containing all authoring parameters for terrain generation.

Location: `Assets/Lootbound/ScriptableObjects/World/DefaultTerrainGenerationConfig.asset`

Key parameters:

| Category | Parameter | Description |
|----------|-----------|-------------|
| Seed | defaultSeed | Starting seed for generation |
| Dimensions | worldSize | Terrain size in meters (1024m default) |
| Dimensions | terrainHeight | Maximum height in meters (260m default) |
| Dimensions | heightmapResolution | Resolution of heightmap (513 default) |
| Macro | macroScale | Scale of main terrain features |
| Macro | macroOctaves | Number of noise octaves |
| Ridge | ridgeScale, ridgeStrength | High point features |
| Valley | valleyScale, valleyStrength | Low corridor features |
| Detail | detailScale, detailStrength | Fine surface variation |
| Spawn | spawnSafeRadius | Flat zone around spawn |
| Spawn | spawnBlendRadius | Transition to natural terrain |
| Surface | lowlandThreshold | Height threshold for grass |
| Surface | highlandThreshold | Height threshold for highland |
| Surface | steepSlopeThreshold | Slope angle for rock |

### TerrainGenerationContext

Runtime class holding all generated data for a single terrain generation pass.

Contains:
- HeightMap (raw values)
- NormalizedHeightMap (0-1 range)
- SlopeMap (degrees)
- MacroMap (for future biome classification)
- SpawnPosition
- Generation timing metrics

### TerrainHeightGenerator

Static class that generates the heightmap using layered noise.

Pipeline:
1. Compute deterministic offsets from seed
2. Apply light domain warping for organic shapes
3. Generate macro terrain (FBM noise)
4. Add valley features (inverted noise)
5. Add ridge features (ridged noise)
6. Add detail noise
7. Apply height remap curve
8. Normalize heightmap
9. Compute slope map

### TerrainSpawnPlanner

Static class that finds a valid spawn location and prepares the spawn zone.

Spawn selection criteria:
- Slope under maxSpawnSlope
- Height in mid-range (10-60% normalized)
- Accessible surrounding area
- Preference for positions closer to center

After selection, applies progressive flattening:
- Core zone: nearly flat with micro-variation
- Blend zone: smooth transition to natural terrain

### TerrainSurfacePainter

Static class that paints terrain texture layers based on terrain data.

Layer order:
1. Grass (index 0) - Low areas, gentle slopes
2. DryGround (index 1) - Mid elevations
3. Rock (index 2) - Steep slopes
4. Highland (index 3) - High elevations

Painting considers:
- Normalized height
- Slope angle
- Noise variation for natural transitions

### ProceduralTerrainGenerator

MonoBehaviour orchestrator that coordinates all generation steps.

Methods:
- `Generate(int seed)` - Generate with specific seed
- `GenerateDefault()` - Use config default seed
- `GenerateRandom()` - Random seed
- `Regenerate()` - Same seed again
- `ClearTerrain()` - Reset terrain

### TerrainGenerationDebug

MonoBehaviour providing debug overlay (toggle with F5).

Displays:
- Generation status
- Current seed
- Terrain dimensions
- Height statistics
- Spawn position and slope
- Generation timing

Map visualization (press 1-4):
1. Off
2. Height map
3. Slope map
4. Macro map

## Generation Pipeline

```
Seed
  ↓
Deterministic Offsets
  ↓
Macro Shape (FBM)
  ↓
Valley Features
  ↓
Ridge Features
  ↓
Detail Noise
  ↓
Height Remap Curve
  ↓
Normalization (optional; disabled in the default preset — min-max
normalization amplifies slopes by the inverse of the seed's raw range,
making final slopes seed-dependent and layout generation unreliable)
  ↓
Spawn Search
  ↓
Spawn Flattening
  ↓
Slope Map Calculation
  ↓
Apply to Unity Terrain
  ↓
Surface Painting
  ↓
Player Positioning
```

## Determinism

The system ensures deterministic generation:

- Uses `System.Random` initialized with seed for offsets
- Each noise channel uses separate offsets derived from seed
- No dependency on `UnityEngine.Random` global state
- Same seed + same config = same terrain

## Sandbox Scene

Scene: `Assets/Lootbound/Scenes/12_ProceduralTerrainSandbox.unity`

Structure:
```
12_ProceduralTerrainSandbox
├── Directional Light
├── ProceduralWorld
│   ├── Terrain (Unity Terrain)
│   └── TerrainGenerator (ProceduralTerrainGenerator)
├── Debug
│   ├── DebugOverlay
│   └── TerrainGenerationDebug
└── Player (PlayerCharacter prefab)
```

## Generation from Inspector

The ProceduralTerrainGenerator has a custom editor providing:
- Generate Default button
- Generate Random button
- Regenerate Current Seed button
- Clear Terrain button
- Custom seed input field
- Generation status display
- Detailed metrics when generated

## Performance

Typical generation times on development hardware (1024m, 513 resolution):
- Heightmap generation: ~50-100ms
- Terrain application: ~10-30ms
- Surface painting: ~20-50ms
- Total: ~100-200ms

## Controls

| Key | Action |
|-----|--------|
| F5 | Toggle terrain debug overlay |
| 1 | Hide map preview |
| 2 | Show height map |
| 3 | Show slope map |
| 4 | Show macro map |

## Limitations (V1)

Not implemented:
- Infinite terrain / streaming
- Runtime chunk loading
- Multithreading / Jobs
- GPU-based generation
- Caves / tunnels
- Rivers / water bodies
- Erosion simulation
- Full biome system
- Vegetation
- Points of interest
- Threat regions

## Future Considerations

The following data is prepared for future systems:
- MacroMap can inform biome/region classification
- SlopeMap can guide vegetation placement
- NormalizedHeightMap can define threat region boundaries
- SpawnPosition establishes future refuge location

## Tuning Guidelines

For better terrain:

**Macro terrain** (large shapes):
- Increase macroScale for gentler, larger features
- Reduce macroOctaves for simpler shapes
- Adjust macroPersistence for detail falloff

**Valleys** (exploration corridors):
- Increase valleyStrength for more pronounced valleys
- Adjust valleyScale for wider/narrower corridors

**Ridges** (landmarks):
- Keep ridgeStrength moderate (0.2-0.3)
- Ridges appear mainly in higher macro areas

**Details** (surface texture):
- Keep detailStrength very low (0.05-0.1)
- Details should not affect traversability

**Spawn zone**:
- spawnSafeRadius: enough for refuge (20-30m)
- spawnBlendRadius: smooth transition (50-70m)
- maxSpawnSlope: compatible with controller (8-12°)

## Validation Checklist

When testing a seed:
- [ ] Terrain generates without errors
- [ ] Player spawns on valid flat area
- [ ] Spawn zone feels natural, not artificial
- [ ] Terrain is traversable with Character Controller
- [ ] Slopes don't exceed controller limits in main areas
- [ ] Relief is readable from player height
- [ ] Some landmarks visible for orientation
- [ ] Same seed produces identical terrain
