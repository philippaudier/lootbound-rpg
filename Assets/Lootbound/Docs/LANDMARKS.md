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

## Terrain Integration (slice 0.9.10.2)

A landmark is a **generation constraint**, not an object dropped on finished
terrain: "there will be an old cabin here, so the hill naturally forms a small
level shelf." The terrain adapts to the landmark - never the reverse. The
building keeps X=0/Z=0 (only Y rotation), and the presenter never compensates
pivots.

### Breaking the circular dependency

Seating the ground needs the landmark's height; the landmark's height needs the
seated ground. The planner is split so the loop breaks cleanly:

```text
PlanPlacements  → LandmarkPlacement (XZ, ring/depth, definition; NO ground Y)
      ↓            (ring/depth/selection are horizontal → stable under stamping)
StampPlanner    → LandmarkTerrainStamp   (seat described from the pre-stamp relief)
      ↓
StampApplier    → writes NormalizedHeightMap + recomputes slope
      ↓
Finalize        → LandmarkIdentity grounded on the STAMPED terrain (never floats/sinks)
```

`ProceduralTerrainGenerator.IntegrateLandmarks` runs this between
`ApplyLayoutFlattening` and `ApplyHeightmapToTerrain`, so the Unity Terrain mesh,
collider and the runtime NavMesh all inherit the seats for free. A missing
registry attaches empty landmark AND stamp sets (one warning), never blocks.

### The stamp is a description, not an instruction

`LandmarkTerrainStamp` only *describes* the seat (centre, shape, radii, seat
height, mode, cut/fill limits, residual, priority). The applier decides HOW to
realize it. A future GPU / voxel / adaptive-terrain backend is a different
applier over the same data - the description never changes.

### Seat, cut/fill, transition, residual

- **Robust seat**: the reference height is the MEDIAN of a ring of samples plus
  the centre - a lone boulder or dip never drags the seat. The XZ is frozen: the
  planner already chose the place; the terrain never relocates a landmark.
- **Partial seating**: the per-cell correction is clamped to
  `MaxCutDepth`/`MaxFillHeight`. Ground too steep to fully seat keeps a residual
  intersection rather than forming a cliff - never skipped, downgraded or None.
- **Transition**: full seat inside `FoundationRadius`, `smoothstep` falloff over
  `TransitionRadius` back to the natural terrain. No "here begins the platform".
- **Residual relief**: `ResidualRoughness` keeps a fraction of the ORIGINAL
  terrain inside the foundation (no new noise) - the lived-in, "it was here
  before" feel. `NaturalIntegration` keeps most of it; `SoftFoundation` less.

### Foundation shape and overlaps

- `FoundationShape` is authored now; **only `Circle` is realized in V1**. Any
  other value falls back to a circle with a **one-time warning** - no silent or
  half-implemented behaviour.
- Overlaps are arbitrated deterministically and order-independently. At each
  cell: drop contributions below an absolute floor; among the rest, a stamp must
  reach a **relative influence floor** (half the strongest present) to compete -
  so a high-priority foundation that merely grazes can never override a
  neighbour that genuinely covers the cell. Among genuine competitors, higher
  `FoundationPriority` wins (the big landmark, not the first one), ties broken by
  effective influence then `LandmarkId`. *Known V1 limit: two genuinely
  overlapping seats of very different height can still meet in a step at their
  win boundary; landmarks are far enough apart that this is effectively never
  hit, and it will be smoothed if it ever matters.*

## Debug

The F7 panel (`WorldProgressionDebugPanel`) is unchanged: it still previews
landmark eligibility through `LandmarkRegistry` + `WorldContentCompatibility`.
The director exposes `ActiveLandmarkCount` for inspection and tests.

`LandmarkTerrainStampGizmos` (Editor-only, attach to any scene object) draws
each seat's foundation radius, transition radius, seat height and centre from
`layout.TerrainStamps`, so it is immediately clear WHY a patch was seated.

## Tests

- `Tests/EditMode/LandmarkPlannerTests.cs` - determinism, ordinal ordering,
  empty/null registry, ring incompatibility exclusion, weighted selection
  stability, id format, discovery radius, terrain grounding, progression
  match, host eligibility, null-prefab still planned.
- `Tests/PlayMode/LandmarkPlayModeTests.cs` - director publish, queries,
  release on disable, republish on re-enable, empty set; presenter prefab
  instantiation, no-prefab-no-fallback, fallback silhouette, no residue on
  disable, no duplicate on re-enable, material cleanup on destroy.
- `Tests/EditMode/LandmarkTerrainStampTests.cs` - seat solver (robust median,
  vertical offset), planner (None → no stamp, ordinal order), applier
  (determinism, untouched beyond outer radius, cut/fill limits, no-cliff
  transition, residual relief, order-independent overlap, priority arbitration,
  relative-floor guardrail, numerical stability), and grounding (no float/sink).
- `Tests/PlayMode/LandmarkTerrainPlayModeTests.cs` - full pipeline against a
  real Unity Terrain: context matches terrain, no relief above the seat inside
  the foundation, landmark grounds on the real stamped terrain.

## Manual Unity Editor setup

1. Create landmark definition assets (menu
   `Lootbound/World Content/Landmark Definition`): set the ring window, the
   selection weight, `DiscoveryRadius`, and optionally a prefab. Under
   **Terrain Integration**, set the conforming mode (None keeps the terrain
   untouched) and, when seating, the foundation/transition radii, cut/fill
   limits, residual roughness, vertical offset and priority. Author the prefab
   with its pivot at the base - the seat plane is where the origin lands.
2. Create a `LandmarkRegistry` asset and add the definitions.
3. On the `ProceduralTerrainGenerator`, assign the `LandmarkRegistry` field.
4. Add a `LandmarkDirector` GameObject and assign the generator.
5. Add a `LandmarkPresenter` GameObject; assign the director and the registry.
   Leave the development fallback OFF unless you want silhouettes for
   prefab-less definitions.
6. Optionally add `LandmarkTerrainStampGizmos` to inspect the seats in the
   Scene view.

## Deliberately future (just new definitions or new observers)

Discovery / journal / map mechanics keyed on `DiscoveryRadius`; several
landmarks per host (`slot > 0`); merchants, campfires, lore, mini-dungeons,
chests and events anchored to a landmark; named landmarks; landmark-driven
navigation aids. None of these require touching the planner, the director or
the identity - they are new definitions or new observers.
