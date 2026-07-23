# Procedural Circulation Engine — Architecture Contract

> **Status**: conceptual architecture (PCE 0.1). No PCE code exists yet; this
> document binds the code that will. Vocabulary: `PCE_GLOSSARY.md`. Rules:
> `PCE_INVARIANTS.md`. Nothing here contradicts `WORLD_ENGINE_ARCHITECTURE.md`
> — the PCE is a peer subsystem that READS the World Engine.

## 1. Position in Lootbound

```text
World Engine   = the physical world     (fields, knowledge, chunks)
PCE            = the circulatory system (intents, flows, corridors, traces)
```

The PCE consumes the World Engine's sampling and knowledge contracts and adds
one thing the world does not have yet: the memory of movement. It is built in
the same style: deterministic pure functions of `seed + coordinates + config`,
Unity-free logic, presentation as a disposable view.

## 2. Conceptual pipeline

```text
World Seed
    │
    ▼
Terrain Sampling            (existing: IWorldHeightSampler, HeightField)
    │
    ▼
Terrain Analysis            (existing: World Knowledge — Slope, Elevation,
    │                        Curvature, Flow/Catchment/RiverMask, Cliff,
    │                        Landscape incl. Valley/Ridge/Pass, Traversability)
    ▼
Terrain Cost Fields         (per CirculationProfile, derived from knowledge)
    │
    ▼
Circulation Intents         (from attractors, geography, history)
    │
    ▼
Flow Generation             (invisible pressure of movement)
    │
    ▼
Corridor Resolution
    ├── Macro Corridors     (regional axes)
    ├── Regional Corridors  (places ↔ axes)
    └── Local Corridors     (the walked experience)
    │
    ▼
Historical Evolution        (Active → Declining → Abandoned → Forgotten)
    │
    ▼
Visible Trace Generation
    ├── Terrain Influence Requests
    ├── Surface Painting
    ├── Vegetation Influence
    ├── Props
    └── Decals / Tracks
    │
    ▼
Gameplay Queries            (Sample*, isolation, curiosity…)
```

Two hard cuts in this pipeline:

- **Corridor Resolution never touches the terrain.** It emits *Influence
  Requests*; the World Engine's sculpting systems (the Structure Stamp
  pattern already proven by landmark seating) decide and apply. Allowed at
  term: micro-relief softening, small flats, local easing, gentle camber,
  switchback clearance, vegetation clearing, surface wear. Forbidden by
  default: cutting a mountain, implicit tunnels, filling deep valleys,
  long-distance artificial flattening, macro-relief edits to rescue a bad
  route. If a passage is impossible the network goes around, finds a pass,
  degrades into a technical trail, becomes an interrupted old road — or gives
  up.
- **Visible Trace Generation never defines logic.** Traces render corridors;
  queries never read traces (invariant 3).

## 3. The three scales

| Scale | Answers | Produces | Must NOT do |
|---|---|---|---|
| **Macro** | "Where does this territory circulate at large?" — great valleys, major passes, basins, gaps between ranges, regional directions, old trade axes | directional axes with intensity; no visible detail | contain path detail; require the whole world (it approximates from local + seeded regional data) |
| **Regional** | "How do places join the axes?" — Refuge, villages, mines, major ruins, lakes, sanctuaries, resource areas | secondary corridors, branches, connections, competing historical routes | invent places (it reads attractors, it does not create them) |
| **Local** | "What does walking it feel like?" — switchbacks, boulder detours, gaps between trees, shortcuts, animal braids, width variation, small panorama detours | the walked corridor and its micro-decisions | contradict the structural direction set above it |

Lower scales refine, never override.

## 4. Circulation families (initial set)

Fixed as *families* at PCE 0.1; numeric values deliberately NOT fixed.

| Family | Exists because | Terrain behaviour | Continuity / visibility |
|---|---|---|---|
| `MajorRoad` | major regional connection | low slope, ample curves, wide, durable surface | strong continuity, high visibility |
| `SecondaryRoad` | a place worth reaching off the axis | narrower, accepts more relief, legible forks | good continuity, moderate visibility |
| `MountainTrail` | crossing heights is sometimes worth it | follows contours, uses passes, switchbacks, refuses frontal slopes, can narrow or expose | continuous but modest; reads as effort |
| `AnimalTrail` | animals need water, food, cover | very narrow, irregular, prefers cover, avoids active human zones, may braid into parallel traces | weak individual traces, frequent presence |
| `ForgottenPath` | someone once needed this | built remnants, collapsed segments, reclaimed vegetation; destination may no longer exist | **corridor stays continuous even when traces are intermittent** |

## 5. History model

Three independent axes per corridor — **Existence**, **Traffic**,
**Visibility** — moved through conceptual states:

```text
Active → Declining → Abandoned → Forgotten
```

This is what later allows replaced routes, collapsed bridges, old forks,
severed paths, vegetation reclaiming a roadbed, and isolated roadside ruins —
without ever breaking structural continuity or determinism (age and state are
functions of seed + place, not of playtime).

## 6. Streaming contract

The PCE follows the split the chunk pipeline already practices
(`TerrainChunkData` vs `TerrainChunk`):

| Structural (deterministic, recomputable anywhere, never stored per-chunk as truth) | Presentation (active chunks only, disposable) |
|---|---|
| corridor presence, family, intensity, direction, width, age, stable id, local connections | ground painting, meshes, decals, removed vegetation, props, prints, historical objects |

Cross-chunk continuity without a global graph works the way terrain already
does — **shared deterministic inputs, not shared state**:

```text
Chunk asks about its area
    ↓
Deterministic regional domains (seeded from world coords, chunk-independent)
    ↓
Corridor evaluation inside the domain (same answer for any asker)
    ↓
Border values agree because both sides evaluate the same function
```

A chunk never "grows" a road toward a neighbour; both chunks independently
evaluate the same seeded corridor and obtain the same crossing point — the
same principle that makes terrain heights bit-identical at chunk edges today.
Unloading destroys presentation only; identity survives (invariant 12).

## 7. Relation to landmarks and layout

The relation is **bidirectional and partial** — never `circulation ⇒
landmark` as a rule:

```text
Terrain → natural & historical attractors  ⇄  Circulation → secondary landmarks
```

- Places that create circulation: villages, mines, sanctuaries, water.
- Places that exist because circulation exists: inns, bridges, guard posts,
  memorials, road ruins.
- Places with no connection at all: hidden caves, wild places, rare isolated
  resources — deliberately out of network.

The PCE does not absorb the world layout, landmark planning or content
systems. **Deferred question (tracked in `PCE_ROADMAP.md`)**: how the current
`WorldLayoutGenerator` radial paths and corridor flattening reconcile with
PCE corridors once those exist.

## 8. Consumers

The PCE is infrastructure. Future consumers include: wildlife movement and
spawning, merchants and NPC travel, bandit ambush placement, ruins and
roadside structures, quests and rumours, resource distribution, weather
exposure along routes, vegetation suppression and regrowth, ambient sound
(distant bells on a road, silence off it), and difficulty/isolation tuning.

## 9. Conceptual public API (documented, NOT implemented)

All queries are deterministic, world-space, allocation-conscious, and answer
from structural data only. Signatures are directional, not final.

```csharp
CirculationSample Sample(Vector2 worldPosition);
// The complete local answer: strongest corridor(s), family, intensity,
// traffic, age, visibility, direction. The one-stop query.

float SampleInfluence(Vector2 worldPosition, CirculationFamily family);
// 0..1: how strongly this family shapes this exact spot (for painting,
// vegetation, audio blending).

float SampleDistanceToNearest(Vector2 worldPosition, CirculationQuery query);
// Distance in metres to the nearest corridor matching the query
// (family/traffic/age filters). Bounded search radius by contract.

bool TryFindNearestCorridor(Vector2 worldPosition, CirculationQuery query,
                            out CorridorReference corridor);
// Stable reference (id + local segment info) for systems that follow or
// reason about a specific corridor.

Vector2 SamplePreferredDirection(Vector2 worldPosition, CirculationProfile profile);
// Where would this profile naturally go from here — usable by animals and
// NPCs WITHOUT pathfinding on a graph.

float SampleTraffic(Vector2 worldPosition);
// Current frequentation pressure at a point (ambience, encounters).

float SampleIsolation(Vector2 worldPosition);
float SampleCuriosity(Vector2 worldPosition);
// DERIVED metrics (see glossary): signals for other systems, never absolute
// artistic truths, never hand-authored per location.
```

Guarantees common to all: same seed + config + position ⇒ same answer; no
dependency on loaded chunks; no global precomputation; cost independent of
world size.

## 10. Anchoring in the existing codebase (for PCE 0.2+)

- **Terrain Analysis is already built**: the World Knowledge layer
  (`Lootbound.World/Processing`) provides Slope, Curvature, Elevation,
  hydrology (Flow → Catchment → RiverMask), Cliff, Landscape (Valley, Ridge,
  Pass, Basin…) and a first `TraversabilityField` explicitly created for this
  future consumer. PCE 0.2 reuses and extends it; it does not rebuild it.
- **Coordinates & determinism**: Refuge = world (0,0), signed
  `WorldCoordinate` doubles, seeded offsets, no global `Random` — the PCE
  inherits all of it.
- **Home**: PCE logic belongs in a Unity-free layer in the World Engine
  style; the exact assembly/namespace is decided at PCE 0.2 (deferred).

## Related Documentation

- `PCE_DESIGN_BIBLE.md` · `PCE_GLOSSARY.md` · `PCE_INVARIANTS.md` ·
  `PCE_ROADMAP.md`
- `WORLD_ENGINE_ARCHITECTURE.md` — the world this engine reads (esp. §3
  World Knowledge, §6.1 ownership style).
