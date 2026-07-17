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
- Definitions declare an innermost allowed ring; a reservation only receives
  a definition whose `MinimumRing` is at or inside the reservation's ring.
- Placement validation: terrain bounds, slope ≤ `MaxPlacementSlope` (default
  matches the layout generator's 40°).
- Failure tolerance: a reservation without compatible definition, a missing
  prefab/item, an invalid placement, or a disabled category produces a typed
  `SpawnRejection` and never blocks world generation.

## Spawning sequence

`WorldContentSpawner` consumes **only** the published, validated layout: it
runs from `OnGenerationComplete`, which the generator fires after terrain
application and final layout validation. Generation attempts that were not
published are never visible to the spawner. Editor-mode generation (inspector
buttons) is ignored (`Application.isPlaying` guard).

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

## Known limitation: NavMesh

The NavMesh is baked statically in `12_ProceduralTerrainSandbox` against the
terrain produced by the **default seed**. Nothing rebuilds it at runtime, and
this slice deliberately does not address runtime NavMesh building.

Consequences:
- With the default seed (and a NavMesh re-baked after generation), encounters
  spawn normally.
- With any other seed, spawn positions without nearby baked mesh are rejected
  and reported as `NavMeshUnavailable` — visible in the debug panel, never
  masked.

Proposed future minimal slice: an `OnGenerationComplete` handler calling
`NavMeshSurface.BuildNavMesh()` (AI Navigation 2.0.10) so the mesh follows the
generated terrain.

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
