# World Disc and Streaming Vision

This document describes the architectural vision for the WorldDisc system and future terrain streaming. The current implementation uses a compressed preview mode; this vision guides future expansion.

## Core Concept: The WorldDisc

The game world is a large circular disc centered on the Refuge. The player explores outward along RadialPaths toward OuterDestinations at the disc's edge.

### Logical vs Physical World

The system distinguishes between:

- **Logical WorldDisc**: The full conceptual game world (e.g., 2km radius)
- **Physical Terrain**: The currently loaded terrain chunks (subset of logical world)
- **Preview Terrain**: Prototype compressed view (current implementation)

### WorldDiscDefinition

The `WorldDiscDefinition` class encapsulates this distinction:

```csharp
public sealed class WorldDiscDefinition
{
    // Logical world size (authoritative for NormalizedWorldRadius)
    public float WorldRadius { get; }

    // Ring configuration
    public WorldRingConfig RingConfig { get; }

    // Preview compression
    public bool IsCompressedPreview { get; }
    public float PreviewTerrainRadius { get; }
    public float CompressionRatio { get; }
}
```

## Current Implementation: Compressed Preview

The prototype uses a compressed preview mode where a small terrain (512m radius) represents a larger logical world (e.g., 500m logical radius). This allows:

- Full radial architecture testing
- Complete ring structure validation
- Gameplay loop verification
- Performance profiling at target scale

### How Compression Works

```
Logical World: 500m radius
Preview Terrain: 512m x 512m Unity Terrain
Compression Ratio: 1.0 (nearly 1:1 for prototype)

All radial calculations use logical WorldRadius.
Ring boundaries apply to normalized logical radius.
Preview terrain displays the compressed view.
```

## Future Vision: Terrain Streaming

### Hybrid Terrain Strategy

The production system will use a hybrid approach:

1. **Central Fixed Terrain**
   - ~500m radius around Refuge
   - Always loaded, high detail
   - Contains Refuge structures
   - Immutable landmarks

2. **Streamed Outer Terrain**
   - Procedurally generated chunks
   - Loaded/unloaded based on player position
   - Lower LOD at distance
   - Memory-managed lifecycle

3. **Transition Zone**
   - Seamless blending between fixed and streamed
   - Visual continuity across boundaries
   - No visible loading seams

### Streaming Architecture (Vision)

```
Player Position
      ↓
TerrainStreamingManager
  ├── Fixed Central Terrain (always loaded)
  ├── Streaming Ring (active chunks around player)
  └── Unload Queue (distant chunks)

WorldLayoutContext
  ├── All nodes remain in memory
  └── Per-chunk terrain data streamed on demand
```

### Key Design Principles

1. **NormalizedWorldRadius is Absolute**
   - Always calculated from logical WorldRadius
   - Never affected by loaded terrain extent
   - Ring assignment is deterministic

2. **Layout is Global, Terrain is Local**
   - WorldLayoutContext knows all nodes/edges
   - Terrain chunks load as needed
   - No structural dependency on loaded chunks

3. **Seamless Expansion**
   - Adding terrain chunks doesn't change logical world
   - Ring boundaries remain stable
   - RadialPaths exist regardless of loaded terrain

## Implementation Notes

### NOT Implemented in Current Slice

This document describes **vision**, not current implementation:
- Terrain streaming is NOT implemented
- Chunk loading/unloading is NOT implemented
- LOD system is NOT implemented
- Memory management is NOT implemented

### What IS Implemented

- WorldDiscDefinition with compression support
- NormalizedWorldRadius from logical radius
- Radial layout generation
- Ring evaluation system
- Preview terrain mode

### Migration Path

When implementing streaming:
1. WorldDiscDefinition already supports logical vs preview size
2. WorldLayoutContext already stores all nodes/edges
3. Add TerrainStreamingManager (new system)
4. Add chunk generation from layout data
5. Existing layout code requires minimal changes

## Technical Guarantees

1. **NormalizedWorldRadius Stability**
   - Value depends only on position and WorldRadius
   - Unaffected by loaded terrain
   - Deterministic across sessions

2. **Ring Assignment Consistency**
   - Same position = same ring (always)
   - Ring boundaries never shift
   - No runtime recalculation needed

3. **Layout Independence**
   - WorldLayoutContext is complete at generation time
   - No lazy loading of structure
   - All paths known before terrain loads

## Related Documentation

- [WORLD_RINGS.md](WORLD_RINGS.md): Ring system details
- [WORLD_LAYOUT.md](WORLD_LAYOUT.md): Layout generation
- [WORLD_RPG_PROGRESSION.md](WORLD_RPG_PROGRESSION.md): Progression design
