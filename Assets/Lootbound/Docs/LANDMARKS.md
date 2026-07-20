# Landmarks (Slice 0.9.10)

## Promise

A landmark is not a prefab. It is a **permanent place with an identity** - the
geographic memory of the world. The tower you see on the horizon, walk toward
for two expeditions, and finally reach is *the same place* every time you load
that seed. Future systems (wildlife, merchants, encounters, campfires, lore,
mini-dungeons, chests, events) will anchor to landmarks. Adding a new landmark
later is adding a new **definition**, not a new system.

## What is permanent

The identity, not any representation of it. `LandmarkIdentity` is pure,
immutable, serializable data - strings, a `Vector3`, an enum, floats - and
carries **no ScriptableObject reference**. The presentation layer resolves the
`DefinitionId` through the `LandmarkRegistry` to obtain a prefab; gameplay never
needs the asset.

```text
LandmarkId       landmark_{worldSeed}_{hostNodeId}_{slot}   (stable identity)
DefinitionId     which archetype was selected here
Position         grounded on the final terrain
Ring / Depth01 / Difficulty01     from WorldProgression
RadialPathId / HostNodeId / Slot  where it was born (slot 0 in V1)
DiscoveryRadius  noticed-from distance (carried from the definition)
```

## Landmarks are a World system, not spawn content

Before 0.9.10 a landmark was a transient `LandmarkReservation` handled by the
content spawner. That reservation is **gone**. Landmarks are now derived
directly from the world layout and live under
`Assets/Lootbound/Gameplay/World/Landmarks/`.

```text
WorldLayout nodes ──► LandmarkPlanner (pure) ──► IReadOnlyList<LandmarkIdentity>
                                                      │  attached ONCE to
                                                      ▼  WorldLayoutContext
                          ┌───────────────────────────┴───────────────────────┐
                          ▼                                                     ▼
               LandmarkDirector (Gameplay)                     AmbientPopulationController
               observable registry + events                   (reads the same set to avoid
                          │                                     spawning on top of a landmark)
                          ▼
               LandmarkPresenter (Presentation.Landmarks)
               observes, resolves prefab, renders
```

The set is computed **once** and attached to the layout, then consumed by
independent readers. No reader recomputes it; no reader depends on another's
order.

## LandmarkPlanner - pure derivation

`LandmarkPlanner.Plan(layout, registry, progression, sampler)` turns a
validated layout into the deterministic landmark set. Pure C#: no scene, no
instantiation, no `UnityEngine.Random`, no mutable global state.

- **Hosts** = the world's elevated / terminal nodes: `Viewpoint`, `Landmark`
  and `OuterDestination`. These are the places a traveller notices on the
  horizon (reusing existing layout points, not a new reservation pass).
- **One landmark per host** in V1 (`slot 0`). The slot is part of the identity
  so a host can carry several landmarks later without an id change.
- **Position** grounded via the shared `ITerrainSampler` on the published
  terrain, exactly like every other placement.
- **Ring / Depth01 / Difficulty01** come from the `WorldProgression` authority
  (`WORLD_PROGRESSION.md`), with a fallback to the host's own radial data.
- **Definition selection** is a weighted draw among ring/depth-compatible
  definitions, using the shared `WorldContentCompatibility` rule (the same one
  the content planner and the F7 panel use). A host with no compatible
  definition stays an anonymous node - no landmark, never an error.
- **Determinism**: the per-landmark `System.Random` is seeded by FNV-1a over
  `(worldSeed, LandmarkId)` - the identical recipe to the content planner, so
  selection is stable across processes and iteration order. The final list is
  sorted by `LandmarkId` (ordinal): one canonical order for every reader.

## Attach point and guardrails

`ProceduralTerrainGenerator` gains a `LandmarkRegistry` field. During
`Generate()`, right after reservation heights are reprojected and before
`OnGenerationComplete` fires, it calls the planner and attaches the result:
`WorldLayoutContext.AttachLandmarks(...)`.

- **Single-assignment, read-only, empty-not-null**: `AttachLandmarks` accepts
  the set once and exposes `Landmarks` as a read-only list, defaulting to an
  empty (never null) collection. Readers can iterate unconditionally.
- **No registry, no blocking**: when the `LandmarkRegistry` field is unassigned,
  the generator attaches an empty set and logs a single warning. Old scenes,
  previews and tests keep working - a missing registry is a content gap, not a
  generation failure.

## LandmarkDirector - the observable registry (Gameplay)

`LandmarkDirector` subscribes to `ProceduralTerrainGenerator.OnGenerationComplete`,
reads `context.LayoutContext.Landmarks`, and republishes them as a live,
queryable registry - the same observable shape as `AmbientEventDirector`. It
references no prefab, no `Renderer`, no presentation type.

- `ActiveLandmarks` / `ActiveLandmarkCount`
- `event OnLandmarkRegistered(LandmarkIdentity)` / `OnLandmarkReleased(...)`
- `GetNearest(Vector3)`, `GetByRing(WorldRing)` - the first anchoring queries
- On regeneration it releases the previous set before republishing; on disable
  it releases everything; on enable it catches up if generation already ran.

## LandmarkPresenter - visuals (Presentation.Landmarks)

A new assembly `Lootbound.Presentation.Landmarks` (references `Lootbound.Core`
and `Lootbound.Gameplay`, never the reverse) holds `LandmarkPresenter`. It
observes the director, resolves each `DefinitionId` through the `LandmarkRegistry`
to obtain the prefab, and instantiates it under `Landmarks_Presented`.

- A landmark whose definition has **no prefab shows nothing**.
- A **development fallback** (OFF by default) can instead show a tall silhouette
  sharing one presenter-owned material - a dev tool, never a silent data fix.
- Register/release keep the visuals in sync; disable and destroy release
  everything and the presenter destroys its own runtime material.

## Dependency direction

```text
Presentation.Landmarks ──observes──► Gameplay (LandmarkDirector, LandmarkIdentity)
Gameplay ──never references──► Presentation
```

Gameplay produces runtime data; presentation observes it. The identity carries
no asset, so the boundary is total.

## Debug

The F7 panel (`WorldProgressionDebugPanel`) is unchanged: it still previews
landmark eligibility through `LandmarkRegistry` + `WorldContentCompatibility`.
The director exposes `ActiveLandmarkCount` for inspection and tests.

## Tests

- `Tests/EditMode/LandmarkPlannerTests.cs` - determinism, ordinal ordering,
  empty/null registry, ring incompatibility exclusion, weighted selection
  stability, id format, discovery radius, terrain grounding, progression
  match, host eligibility, null-prefab still planned.
- `Tests/PlayMode/LandmarkPlayModeTests.cs` - director publish, queries,
  release on disable, republish on re-enable, empty set; presenter prefab
  instantiation, no-prefab-no-fallback, fallback silhouette, no residue on
  disable, no duplicate on re-enable, material cleanup on destroy.

## Manual Unity Editor setup

1. Create landmark definition assets (menu
   `Lootbound/World Content/Landmark Definition`): set the ring window, the
   selection weight, `DiscoveryRadius`, and optionally a prefab.
2. Create a `LandmarkRegistry` asset and add the definitions.
3. On the `ProceduralTerrainGenerator`, assign the `LandmarkRegistry` field.
4. Add a `LandmarkDirector` GameObject and assign the generator.
5. Add a `LandmarkPresenter` GameObject; assign the director and the registry.
   Leave the development fallback OFF unless you want silhouettes for
   prefab-less definitions.

## Deliberately future (just new definitions or new observers)

Discovery / journal / map mechanics keyed on `DiscoveryRadius`; several
landmarks per host (`slot > 0`); merchants, campfires, lore, mini-dungeons,
chests and events anchored to a landmark; named landmarks; landmark-driven
navigation aids. None of these require touching the planner, the director or
the identity - they are new definitions or new observers.
