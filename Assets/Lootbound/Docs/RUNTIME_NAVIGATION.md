# Runtime Navigation (Slice 0.9.5)

## Role

Turns the generated terrain into a navigable surface for every seed, at
runtime, without any per-seed manual bake. Navigation is a **derived
representation** of the physical terrain: it consumes the published
`TerrainGenerationContext`; it is never a source of truth for the WorldDisc,
rings, WorldLayout, reservations or logical content positions.

Navigation is **global for building the mesh, local for validation**: one
NavMesh covers the terrain, but each encounter entry resolves its own
navigable position individually — one failed entry never blocks another.

## Pipeline order

```text
Heights generated (TerrainHeightGenerator)
  ↓
WorldLayout generated + validated (context-backed sampler)
  ↓
Corridor/clearing flattening + reservation height reprojection
  ↓
Final heightmap applied to TerrainData, painting, player positioning
  ↓
TerrainGenerationContext published (OnGenerationComplete, carries GenerationId)
  ↓
WorldContentSpawner clears previous content immediately
RuntimeNavigationBuilder rebuilds the dedicated NavMeshSurface
  ↓
OnNavigationCompleted(RuntimeNavigationBuildResult) published
  ↓
WorldContentSpawner (NavigationContentGate) plans + instantiates content
```

## Components

| Type | Role |
|------|------|
| `RuntimeNavigationBuilder` (MonoBehaviour) | Knows ONLY Terrain → NavMeshSurface → NavMesh. Subscribes to `OnGenerationComplete`, derives volume bounds from the actual `TerrainData` (+ margin, no preset constants), runs `NavMeshSurface.BuildNavMesh()` synchronously, publishes `OnNavigationCompleted`. Never references any spawning or content type. |
| `RuntimeNavigationBuildResult` | Success/failure, `GenerationId`, seed, duration, bounds, surface count, triangle count, failure reason. |
| `RuntimeNavigationStats` | Build count, last/average/worst duration, triangles, bounds — developer diagnostics (F6). |
| `NavigationContentGate` (pure C#) | Decides WHEN a generation may spawn: keyed on the monotone `GenerationId` (never the seed), releases each generation exactly once, ignores stale results. |

## Generation identity

`ProceduralTerrainGenerator` increments a per-session counter; each
`TerrainGenerationContext` carries it as `GenerationId`. Two successive
generations with the same seed have different ids. A navigation result whose
id does not match the pending generation is ignored.

## Old NavMesh cleanup

`NavMeshSurface.BuildNavMesh()` internally removes the previous
`NavMeshDataInstance` before registering the new one, so successive
generations never accumulate data. The static baked `NavMesh-Terrain.asset`
is removed from the scene surface (see scene setup) — no navigation data
from any previous configuration can influence a new world.

## Failure behaviour

- Navigation **unavailable** (build failed, no surface, no terrain):
  resources and landmarks still spawn; encounters are rejected with an
  explicit `NavMeshUnavailable (...)` reason. Terrain, layout and manual
  exploration are untouched.
- Navigation **incomplete** (one reservation position not navigable):
  only that entry is rejected (`SamplePosition` bounded by
  `navMeshSampleDistance`); other entries and encounters spawn normally.
  The NavMesh never repairs a wrong height and never teleports an enemy
  away from its reservation.

Diagnostics keep, per encounter entry: requested position, resolved
position, distance (see `EntryPlacement` in the spawn report, F6 panel).

## Scene setup (12_ProceduralTerrainSandbox)

1. The dedicated `NavMeshSurface` object: `Agent Type = Humanoid`,
   `Collect Objects` is driven at runtime (Volume, bounds from TerrainData),
   `Use Geometry = Physics Colliders`, `Include Layers` = terrain/static
   environment only (exclude Player, enemies, pickups, debug objects),
   **NavMeshData reference cleared** (`None`); `NavMesh-Terrain.asset` can be
   deleted.
2. `RuntimeNavigationBuilder` component (same object or a sibling):
   assign the `ProceduralTerrainGenerator`, the `Terrain`, and the surface.
3. `WorldContentSpawner`: assign the `RuntimeNavigationBuilder` reference.
4. `WorldContentDebugPanel` (F6): assign the `RuntimeNavigationBuilder`.

## Agent parameters (V1 authority)

The surface builds for `agentTypeID 0` (Humanoid), matching
`Enemy.prefab`'s `NavMeshAgent` (radius 0.5, height 2). The Humanoid agent
settings in the Navigation window are the single authoritative source for
bake parameters in V1; no per-category agents yet.

## Measured build cost (development machine, seed 42, runtime preset)

| Preset | Build time | Triangles | NavMeshData |
|--------|-----------|-----------|-------------|
| 512 m  | ~0.75 s   | 288       | 12 KB       |
| 1024 m | ~3.0 s    | 1152      | 45 KB       |
| 2048 m | ~12.5 s   | 4608      | 180 KB      |

`RenderMeshes` and `PhysicsColliders` measured strictly equivalent (time,
triangles, memory): the Terrain is collected as a dedicated source in both
modes. `PhysicsColliders` is chosen on semantics — the walkable ground is
what has colliders; decorative meshes without collision must not become
navigable. Re-run anytime: menu `Lootbound/Diagnostics/Navigation Build
Report`.

The synchronous build adds ~3 s to a generation on the 1024 preset —
accepted for V1 (generation is already a loading moment). If this becomes
a problem, the documented path is `UpdateNavMesh()` (async) guarded by
`GenerationId`, not a custom task framework.

## V1 limitations / explicitly future

- Single terrain, single surface, whole-world synchronous build (see
  measured cost above).
- Future (not blocked by this design): multiple terrains/surfaces, builds
  per sector, local NavMeshData activation, streaming, async builds guarded
  by `GenerationId`, multiple agent types, navigation links.
