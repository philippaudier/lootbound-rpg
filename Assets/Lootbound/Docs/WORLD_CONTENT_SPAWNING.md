# World Content Spawning (V1)

Reservation-driven content spawning: the first runtime consumer of the
Encounter/Resource/Landmark reservations produced by `WorldLayoutGenerator`.

```text
WorldLayout reservation
→ definition resolution (per-family registry)
→ placement validation (terrain bounds, slope, ring rules)
→ SpawnRecipe (deterministic plan, pure data)
→ runtime instantiation (WorldContentSpawner)
→ playable content
```

## Architecture

All runtime code lives in `Assets/Lootbound/Gameplay/World/Spawning/`
(namespace `Lootbound.Gameplay.World.Spawning`, assembly `Lootbound.Gameplay`).

| Layer | Types | Responsibility |
|-------|-------|----------------|
| Definitions | `EncounterDefinition`, `ResourceSpawnDefinition`, `LandmarkDefinition` | Independent ScriptableObjects per category. Stable string IDs (fallback to asset name). No shared base class. |
| Registries | `EncounterRegistry`, `ResourceSpawnRegistry`, `LandmarkRegistry` | One registry per content family (EquipmentRegistry pattern). |
| Planning | `WorldContentPlanner` (static, pure C#) | Reservations in, `WorldContentPlan` out: `SpawnRecipe` list + typed `SpawnRejection` list. No Unity objects created. |
| Recipes | `SpawnRecipe`, `SpawnRecipeEntry` | Fully resolved plan for one reservation: definition, anchor, one entry per instance with a `Role` (prepares richer encounter compositions later). |
| Instantiation | `WorldContentSpawner` (MonoBehaviour) | Subscribes to `ProceduralTerrainGenerator.OnGenerationComplete`, instantiates recipes, records a `WorldContentSpawnReport`. |
| Identity | `WorldContentIdentity` (MonoBehaviour) | Attached to every spawned instance: `ReservationId`, `HostNodeId`, `DefinitionId`, `Ring`, `RadialPathId`, `Role`. |
| Terrain access | `TerrainContextSampler` (in `Lootbound.Gameplay.World`) | Adapts `TerrainGenerationContext` to `ITerrainSampler`. Shared with layout generation: the single conversion authority for the pipeline height space. |

## Determinism

- Every decision for a reservation is drawn from a `System.Random` seeded by
  FNV-1a over `(worldSeed, ReservationId)`. Results are therefore independent
  of collection iteration order and stable across processes.
- `UnityEngine.Random` is never used for gameplay decisions. The only visual
  variation (enemy yaw) is a deterministic hash, clearly separated.
- Same seed + same definitions → same selections, quantities, compositions,
  and placement offsets.

## Rules

- **The Refuge ring never hosts encounters.** This is a hard guard in the
  planner, applied before and regardless of any definition's `MinimumRing`.
- Since slice 0.9.7, definitions declare an inclusive ring window
  (`MinimumRing`..`MaximumRing`, Void opt-in only) plus a `SelectionWeight`
  and a `WeightByDepth` curve evaluated at the global `Depth01`; the planner
  draws among compatible definitions with a deterministic weighted pick
  (`WorldContentCompatibility` is the shared rule, also displayed by the F7
  panel). The progression authority is injected from
  `WorldLayoutContext.Progression` (see `WORLD_PROGRESSION.md`).
- Placement validation: terrain bounds, slope ≤ `MaxPlacementSlope` (default
  matches the layout generator's 40°).
- Failure tolerance: a reservation without compatible definition, a missing
  prefab/item, an invalid placement, or a disabled category produces a typed
  `SpawnRejection` and never blocks world generation.

## Spawning sequence

`WorldContentSpawner` consumes **only** the published, validated layout.
Since slice 0.9.5 it is gated by runtime navigation (see
`RUNTIME_NAVIGATION.md`): on `OnGenerationComplete` it clears the previous
content immediately (so nothing stale contributes to the new NavMesh build)
and waits; the plan is computed and instantiated when
`RuntimeNavigationBuilder.OnNavigationCompleted` publishes the result for
that same `GenerationId` (`NavigationContentGate`, pure C#). Generation
attempts that were not published are never visible to the spawner.
Editor-mode generation (inspector buttons) is ignored
(`Application.isPlaying` guard).

Spawned objects live under `WorldContent_Spawned/{Encounters,Resources,Landmarks}`;
the hierarchy is destroyed and rebuilt on regeneration.

- **Encounters**: enemy prefab instantiated per recipe entry (group of 1–3),
  gated by `NavMesh.SamplePosition` (see limitation below), agent warped onto
  the mesh.
- **Resources**: `ItemWorldPickup.SpawnPickup(item, position, quantity)` —
  the existing pickup path (requires the `Interactable` layer and a scene
  `PlayerInventory`).
- **Landmarks**: prefab when assigned, otherwise a clearly named placeholder
  primitive (`Landmark_PLACEHOLDER_...`), encapsulated in one method and easy
  to replace with real assets.

## NavMesh (since slice 0.9.5)

The NavMesh is rebuilt at runtime for every generation by
`RuntimeNavigationBuilder` (`RUNTIME_NAVIGATION.md`). Navigation is global
for building the mesh but **local for validation**: each encounter entry
resolves its own navigable position via `NavMesh.SamplePosition` bounded by
`navMeshSampleDistance`; a failed entry is rejected (`NavMeshUnavailable`)
without blocking the others, and per-entry diagnostics keep the requested
position, resolved position and distance (`EntryPlacement`). If the whole
navigation build failed, resources and landmarks still spawn and encounters
are rejected with an explicit reason.

## Debug

- `WorldContentDebugPanel` (assembly `Lootbound.Debugging`), toggle **F6**:
  reservations received / recipes planned / spawns succeeded / rejected, and
  per entry: category, definition, reservation, host node, ring, radial path,
  failure reason.
- Logging through `LootboundLog`, category `WorldContent`.

## Manual Unity Editor setup

1. Create definition assets (menu `Lootbound/World Content/...`):
   at least one `Encounter_*` (assign `Enemy.prefab`), one `ResourceSpawn_*`
   (assign an `Item_*` definition), one `Landmark_*` (prefab optional).
2. Create the three registry assets and add the definitions to them.
3. In `12_ProceduralTerrainSandbox`: add a `WorldContentSpawner` GameObject,
   assign the `ProceduralTerrainGenerator` and the three registries.
4. Optionally add `WorldContentDebugPanel` and assign the spawner.
5. Re-bake the NavMesh after generating with the default seed so encounters
   have a mesh to stand on.

## Tests

`Assets/Lootbound/Tests/EditMode/WorldContentPlannerTests.cs` covers:
determinism (same seed, different seeds, `UnityEngine.Random` immunity,
iteration-order independence), ring compatibility, Refuge exclusion, typed
rejections (no definition, missing prefab, steep slope, out of bounds,
disabled category), radial data propagation, composition/quantity bounds,
terrain-sampled heights, and resolution of all three categories.

## Deliberately out of scope (V1)

Ring-based difficulty scaling, loot tables per ring, encounter packs/waves/
leaders, persistence and respawn, runtime NavMesh building, streaming,
definitive landmark assets.
