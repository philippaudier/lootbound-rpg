# Lootbound — World Engine Architecture (Slice 0.9.T1, locked)

> **Status**: this is the **target architecture** for Lootbound's procedural
> world — a design lock, not a description of what exists. See
> [§13 Implementation status](#13-implementation-status) for the honest split
> between today and the target. Nothing here is "already built".
>
> **This is the final conception lock.** From here, every evolution is earned by
> code, not theory. The next real learning comes from building T2.

## The one philosophy

> **The world is a deterministic function.**
> **Layers** describe it. **Processing** derives from it. **Presentation** shows
> a momentary slice of it.

> **Mantra — The player never explores chunks. They explore a world. Chunks
> exist only for the engine.**

The terrain is the *first gameplay* of Lootbound: the player must feel, by
walking alone, that they drift farther from the Refuge. That sensation is born
of geography — relief, water, climate, danger — before any enemy, loot or
visual effect. Every layer below serves that sentence.

## The four layers

```text
WORLD          a deterministic function of a seed
  ↓
LAYERS         what the world IS
  ├── Fields        continuous analytic properties (Height · Climate · Danger · Region · Moisture)
  ├── Structures    rare elements, a deterministic graph (Layout · Roads · Villages · Landmarks · Shrines)
  └── Simulation    time-varying, stateful (Weather · Seasons · Wind · Ambient Life)
  ↓
PROCESSING     what the world DERIVES  (domain-agnostic — see §3)
  ├── Hydrology         (flow → rivers emerge)
  ├── Erosion           (a consequence of hydrology)
  ├── Landscape Recognition   (valleys/forests/passes are recognized, not placed)
  ├── Vegetation
  └── Future Systems
  ↓
PRESENTATION   a momentary view around the player
  └── World Chunks · Terrain tiles · Mesh · Vegetation · LOD · Streaming · Audio
```

A complementary lens — three kinds of truth: the **Logical** world (Fields +
Processing outputs), the **Structural** world (the graph + Landscapes), the
**Presentation** world (chunks). None of them is the terrain.

The world exists even when no chunk is loaded:

```text
World (seed → Layers → Processing)
  ↓  chunk requested   (player approaches a landscape)
  ↓  chunk generated    (fields + derived data sampled, structures stamped)
  ↓  chunk presented     (terrain / mesh / vegetation / nav / audio)
  ↓  chunk discarded      (player leaves; only player edits persist)
```

Chunks are **never the truth**. A chunk unloaded then reloaded is identical,
except for the sparse overlay of player edits.

## 1. Coordinates (locked)

The World Engine is mathematics; it must never depend on Unity. Locked types:

```csharp
// World Engine — never Unity
public readonly struct WorldCoordinate { public readonly double X; public readonly double Z; }
public readonly struct ChunkCoordinate { public readonly int X;    public readonly int Y; }   // 2D chunk grid (X,Y → world X,Z)
```

- **`WorldCoordinate` is `double`** — the world is a math function of hundreds of
  km; maps, minimaps, saves, replay, (multiplayer) all reason in world space with
  no precision loss.
- **`ChunkCoordinate` is `int`** — a technical grid index.
- **No `Vector3` anywhere in the World Engine.** `Vector3` belongs to Unity.
  Fields, Structures, Processing and Landscapes speak only `WorldCoordinate`.
- **Floating origin lives ONLY on the Unity side.** The single conversion
  authority sits at the World↔Presentation seam:

```text
WorldCoordinate (double)  ──floating origin──►  Unity local position (float Vector3)
```

The engine always knows the true position; Unity only ever knows a local one.

### World origin — the Refuge is (0,0)

The world origin is the **Refuge: world XZ = (0,0)**. Coordinates are signed
(negative west/south, positive east/north), because the logical world is
unbounded and radial around the Refuge. There is **no `+ WorldSize/2` offset**
anywhere: the only origin is the `Min` of the finite region currently generated
(a `WorldBounds` descriptor carried by the generation context), never a per-call
translation of a coordinate.

> **The migration to a Refuge-centred world (slice T3.1A, step M2) changes the
> portion of the procedural field that is sampled. This difference is intentional
> and constitutes the new reference terrain.** Before the migration the region was
> the corner box `[0, WorldSize]` with the Refuge at its centre; a fixed seed
> therefore renders a *different* — but equally valid and fully deterministic —
> relief afterwards. This is not a regression: the golden snapshot test
> (`GeneratorGoldenSnapshotTests`) is deliberately re-baselined at that point, and
> the new values become the reference for detecting future *unintentional* drift.

## 2. Layers — what the world IS

### 2a. Fields (analytic)

Continuous properties, `O(1)` per point, evaluable anywhere with no state —
directly chunkable, no precompute: **Height** (`TerrainNoiseCore`), **Climate**,
**Danger** (`WorldProgression` — the field that carries the sensation of
distance), **Region** (biome), **Moisture**. `HeightField` is simply one member;
naming the family `Fields` lets us later add wind/fog/snow/ambient-sound without
changing the model. *Derived* properties (hydrology, erosion) are **not** here —
they are outputs of Processing (§3).

### 2b. Structures (a graph, emergent)

The Structural World is a **deterministic graph**, not a list:

```text
Refuge ──road── Bridge ──road── Village ──path── Forest Shrine ──pass── Mountain Pass
```

Nodes (Refuge, Villages, Landmarks, Shrines) and edges (Roads, Bridges, Fords,
Paths). All structures are **rare · deterministic · generated once ·
chunk-independent · consumed by chunks**. They increasingly **emerge** rather
than being placed: a village is *recognized* where a Landscape + road network +
water + accessibility make it plausible, then committed as a `Village`
structure. The graph is the connective tissue for future routing, quests and
emergent stories.

### 2c. Simulation (living, stateful)

The one layer with **time and mutable runtime state**: **Weather · Seasons ·
Wind · Ambient Life**. Seasons feed `ClimateField`; weather modulates
paint/shader/audio; ambient life is creatures streamed around the player — the
natural home of the existing `AmbientPopulationController`.

## 3. Processing — Domain Processing (domain-agnostic)

Some truths are not `O(1)` local: a river's flow integrates its whole upstream
catchment. Rather than a "global precompute" (a word that becomes a ceiling), we
process a **domain**.

> A **Domain** is a bounded region to process. **Domain Processing** transforms
> fields over that domain into **derived fields** and **recognitions**. It does
> not know whether the world is 2 km, 200 km or infinite. Today the domain is
> the whole world; tomorrow it may be a region, a tile grid, a hierarchy, a
> clipmap, or a distributed computation — **the architecture is identical**.

```text
Analytic Fields  →  Domain Processing  →  Derived Fields / Recognitions  →  Structures
```

Processing steps:
- **Hydrology** — flow accumulation over `HeightField` yields
  `Flow · Catchment · WaterTable · RiverMask`. **Rivers are discovered, not
  drawn**: the River Network graph *emerges* from the `RiverMask`; only its
  crossings and banks become structures (bridges/fords are graph edges where a
  road meets the water).
- **Erosion** — a *consequence* of hydrology (needs a neighbourhood / margin).
- **Landscape Recognition** — see §4.
- **Vegetation** — density/type masks from slope/biome/moisture.

### World Knowledge (T3 — implemented, `Lootbound.World/Processing/`)

Domain Processing produces **World Knowledge** — the derived fields the engine
deduces about its geography. **Analyzers produce Fields**; one Analyzer per
Field, each an interchangeable module behind `IWorldField<T>` (swap the V1
algorithm — D8, finite differences — for a better one later, nobody notices).

> **Every World Field is a deterministic function of the world: no mutable
> state, never modifies other fields, evaluable independently of the
> presentation layer.**

> **Every World Field answers a useful question the engine can ask the world**
> (how steep? does water pass here? is this a valley?). A field with no consumer
> should not exist ("No Orphan Fields").

The dependency graph is a strict **DAG** — a chain of *reasoning*, not just
calculations. **Golden rule: a Field only reads fields strictly UPSTREAM of it;
never a sibling, never downstream, no cycles.**

```text
                         HeightField
        ┌───────────┬───────────┬───────────┬───────────┐
        ▼           ▼           ▼           ▼           ▼
     Slope      Curvature   Elevation   Exposure   Roughness
        │
        ▼
      Cliff
        │
   HeightField ─► Flow ─► Catchment ─► WaterTable ─► RiverMask   (Domain: WorldDomain{Bounds,Resolution,CellSize})
                                            │
                    ┌───────────────────────┴───────────┐
                    ▼                                    ▼
             Traversability                          Landscape
   (Slope,Cliff,Roughness,RiverMask)   (Elevation,Slope,Curvature,Cliff,RiverMask)
```

- Analytic fields (`O(1)`, finite differences on HeightField): **Slope**°,
  **Curvature** (convex/concave), **Roughness** (local height variance, distinct
  from curvature), **Elevation** (0..1), **Exposure** (aspect°), **Cliff** (a
  concept, V1 = slope > threshold).
- Hydrology (Domain Processing, D8 + topological accumulation): **Flow**,
  **Catchment**, **WaterTable** (V1 proxy, no pit-filling), **RiverMask**
  (rivers are *discovered*, not drawn).
- Interpretation: **Traversability** (LOCAL cost only — distance is the path
  planner's job) and **Landscape** (`{Plain,Valley,Ridge,Mountain,Plateau,Pass,
  Basin,Cliff}`, PURELY geomorphological — never world position, danger or
  gameplay; never reads Traversability).

Each analyzer documents its **assumptions and limits** in its header (e.g. Flow:
D8, no pit-filling; Landscape: Ridge/Pass are rough approximations). Debug: the
**F9** overlay (Gameplay-side, `WorldKnowledgeDebugOverlay`) colours any field
over the world extent.

### The "4096 bug", requalified

The layout solver does not fail because 4096 m is "too big" — **it fails because
it has no geographic knowledge**: fixed-length greedy radial paths (≤720 m,
never scaled to the disc radius) whose traversability is checked *after* the
fact, with no notion of valleys, passes or contour lines. T3 builds exactly that
knowledge (Slope, Traversability, Flow, Landscape); **T3.1** (a path planner)
will consume the `TraversabilityField` to route naturally through valleys and
passes, and scale path length to the disc. T3 draws no route.

## 4. Landscapes — recognized, not generated

Between the world and the chunk sits the unit the player actually experiences —
a **valley, forest, mountain, pass, plain**. A `Landscape` is not placed; it is
**recognized** by Domain Processing:

```text
Height + Slope + Climate + Hydrology  →  Landscape Recognition  →  "this zone IS a valley"
```

- Emergent, like rivers: *detected*, never authored. A valley is a catchment
  basin; a pass is a saddle; a forest is a biome zone.
- **Naming**: `RegionField` (biome, §2a) ≠ `Landscape` (the region the player
  explores). Distinct on purpose.
- **A Landscape is never a chunk.** A chunk is a technical subdivision; a
  Landscape spans **0.5–4 chunks**, never 1:1. Gameplay, ambience, music and
  memory attach to **Landscapes** — because that is what the player remembers.
  Downstream gameplay reads them naturally: wolves prefer valleys, mist forms in
  valleys, certain landmarks appear in valleys.

## 5. Structure Stamps — one integration pass

No system writes the terrain directly. Structures *describe* constraints; a
single **Terrain Integration** pass applies them.

```text
LandmarkStamp ┐
RiverStamp    │   (from the RiverMask: carve the bed)
RoadStamp     ├──►  Terrain Integration  ──►  chunk height grid
BridgeStamp   │
VillageStamp  │
FutureStamp…  ┘
```

A stamp is **descriptive data, never an instruction** (centre, shape, radii,
target profile, limits, priority); the applier decides HOW on the current
backend (heightmap today; mesh/GPU/voxel later) — the description never changes.
This is the `LandmarkTerrainStamp` contract promoted to the whole structures
layer. Additive: a new structure is a new stamp producer, not a new terrain
writer.

## 6. World Chunks (locked)

A chunk is **not** a terrain — terrain is one component. A World Chunk holds
relief, water, vegetation, local weather, resources, enemies, birds, sounds,
roads, villages, landmarks. It is a **temporary presentation cache**,
reconstructible from (seed → Layers → Processing), therefore never the source of
truth and never persisted — except the sparse player-edit overlay.

- **Size: 128 m.** Chosen from gameplay density (a clearing's worth of content —
  a few enemies, resources, a landmark, a piece of road/river — fits in
  ~100–150 m), not for being a round number. **A chunk represents a CPU/GPU
  budget, never a gameplay unit.**
- **Resolution: 129 × 129** vertices (128 quads) for LOD0 — Unity Terrain's
  natural `2^n+1` grid.
- **Future LOD: 129 → 65 → 33 → 17.** (129² = 16 641 vertices vs 257² = 66 049 —
  ~4× lighter; prefer many light chunks over few heavy ones for streaming.)
- `ChunkData` (immutable once built): height grid **+ margin** (1–2 cells so
  slope/erosion read neighbours without seams), slope grid, splat weights,
  vegetation mask, intersecting structures + landscape, bounds.
- **Landscapes never depend on chunk boundaries.**

### 6.1 Chunk streaming ownership (T3.1 — locked)

The chunk system is a strict ownership chain: each component owns exactly one
concern and is blind to the others. This is what keeps the generator Unity-free,
and lets the menu, the Refuge, dungeons and tests each run their own streamer
with no hidden singleton.

```text
              Player
                 │
                 ▼
      TerrainChunkStreamer        owns: player pos → which chunks exist → the pool
                 │  requests / cancels          (budgets: activations, ms, queue cap)
                 ▼
   TerrainChunkBuildScheduler     owns: the queue — a request is only
                 │                Queued → Running → Finished; nearest-first,
                 │                deterministic ties, ONE build in flight
                 ▼
      TerrainChunkBuilder         creates the build; knows only the sampler
                 │
                 ▼
     TerrainChunkBuildState       row-sliced state machine (NOT a Unity Job):
                 │                SamplingHeights ▸ BuildingSurface (derived
                 │  samples       from the height buffer + 1-cell margin)
                 ▼
ProceduralTerrainGenerator        the ONE generator — Sample(x,z): Height / Masks
                 │
                 ▼
        TerrainChunkData          pure data (heights + alphamaps), zero Unity
                 │
                 ▼
    TerrainChunk.Apply(data)      owns a Unity Terrain — displays, never generates
                 │
                 ▼
          Unity Terrain
```

Sibling CLIENTS of the same sampling contracts (the generator owns no Unity
Terrain at all since M4): `TerrainPreviewGenerator` fills the Editor Terrain
Preview, `WorldPlayerSpawner` places the player, the chunk pipeline streams the
runtime world. A minimap tomorrow is just one more client.

| Component | Owns | Does NOT know about |
|---|---|---|
| `ProceduralTerrainGenerator` | `Sample(x,z)` — Height today; Masks / Biomes later — at any world coordinate | chunks · Unity Terrain · the streamer · the pool · the preview |
| `TerrainChunkBuildScheduler` | the request queue: priority, dedup, cancel, per-frame ms/activation budgets | the INTERNAL build steps · the pool · Unity |
| `TerrainChunkBuilder` / `TerrainChunkBuildState` | producing a `TerrainChunkData` (row-sliced, reusable lent buffers) | the scheduler · the streamer · the player · Unity objects |
| `TerrainChunkData` | the built data (height grid + alphamaps) | anything Unity |
| `TerrainChunk` | one Unity Terrain — `Apply(TerrainChunkData)`, neighbours, recycle hygiene | generation · the streamer |
| `TerrainChunkStreamer` | player position → required chunks → activation → the pool | how a chunk is built or displayed |

Rules that must stay true:

- The generator answers `Height(x,z)` for **any** coordinate; the finite region
  currently generated is only where authored deformations exist. It never learns
  that chunks exist.
- The scheduler never knows the internal build steps — to it a request is only
  `Queued → Running → Finished`. Dependencies point one way:
  scheduler → builder, never the reverse.
- `TerrainChunk` **displays**, never generates — so save / network / import /
  debug can feed it a `TerrainChunkData` from any source later, untouched.
- No hidden singleton: a streamer is an instance with injected dependencies;
  several can coexist across scenes.
- These guarantees are enforced by reflection tests (`ChunkOwnershipTests`).

## 7. Data ownership

- **World / immutable / infinite** — analytic fields (functions), definitions
  (SO), the seed. Never fully materialized.
- **Processing / derived / domain-cached** — hydrology, erosion, landscape
  recognition (processed over a domain, cached, sampled per chunk).
- **Structures / global-sparse / computed once** — the graph and landscapes,
  independent of loaded chunks.
- **Simulation / mutable runtime state** — weather clock, seasons, ambient life.
- **World Chunk / transient cache** — `ChunkData`.
- **World Chunk / persistent** — only the player-modification overlay.

## 8. Generation pipeline (by layer)

```text
LAYERS·Fields (analytic)    NoiseField → Macro Relief → Mountain Structure → Large Forms
   ↓
PROCESSING (domain)         Hydrology (flow → RiverMask → rivers emerge)
                            Erosion (margin)
                            Landscape Recognition
                            Vegetation masks
   ↓
LAYERS·Structures           recognize/commit graph (layout, roads, bridges, villages) → Structure Stamps
   ↓
CHUNK build                 sample fields+derived → Terrain Integration (stamps) → Paint → Vegetation → World Content
   ↓
PRESENTATION                Mesh/Heightmap · Collider · Nav · Vegetation · Audio
```

Every pass is **pure, deterministic, testable** — the standard set by the
landmark seat solver / stamp planner / stamp applier.

## 9. Streaming

Fields are infinite, so streaming is **materializing a sliding window** of
chunks around the player. `ChunkStreamer`: a load ring (prioritized by distance
/ landscape), active chunks (LOD by distance), an unload queue (player overlay
preserved). Deterministic: reload = identical. Fully streamed from the start;
the Refuge is a *pinned* chunk, not a special central terrain.

## 10. Backend recommendation — Unity Terrain tiles for V1

**Keep Unity Terrain for V1, as a stitched GRID OF TILES behind a
chunk-presentation interface; defer mesh chunks until caves/overhangs force
them.** Unity Terrain gives, for free, what the project already uses (heightmap
collider, splat painting, detail/tree instancing, native LOD, NavMesh baking);
`Terrain.SetNeighbors` stitches tiles for streaming + LOD. The real investment is
the layers/processing/chunk decoupling — once done, swapping tile ↔ mesh is a
Presentation-only change. Unity Terrain's 2.5D ceiling (no caves/overhangs)
concerns *later* features; the next slices are all heightmap-expressible.

## 11. Where future features live

| Feature | Home | Nature |
|---|---|---|
| Rivers | Processing / Hydrology → RiverMask; RiverStamp carves the bed | derived + stamp |
| Bridges / Fords | Structure graph edges where a Road crosses the RiverMask | structure |
| Roads | Structure graph edges (low-slope corridors) → RoadStamp | structure |
| Villages | Structure graph nodes (emergent from landscape+water+access) → VillageStamp | structure |
| Biomes | `RegionField` (Fields) | analytic field |
| Landscapes | Processing / Landscape Recognition | recognition |
| Snow / seasons / weather | Simulation over `ClimateField(x,z,time)` | simulation |
| Wind / fog / ambience / birds / music | Fields / Simulation | field / simulation |
| Ambient wildlife | Simulation / Ambient Life (already streaming) | simulation |
| Caves / overhangs | `SubsurfaceStructure` + mesh presentation — impossible in 2.5D | ⚠️ needs mesh backend |

## 12. Roadmap (locked)

```text
T2  World Foundations   coordinate system (WorldCoordinate/ChunkCoordinate, floating origin, no Vector3),
                        the Layers scaffold, analytic Fields (Height/Climate/Danger/Region), sampling rules,
                        world/presentation separation. Kill global normalization (fixed remap); absolute warp.
T3  Domain Processing   the domain abstraction; Hydrology (rivers emerge) + Landscape Recognition first.
T4  World Structures    the structure graph; Structure Stamps; scale layout params to disc radius (fixes the 4096 bug).
T5  World Chunks        WorldCoordinate→ChunkData chunk passes producing ONE chunk identical to today (safety net).
T6  Presentation        Unity Terrain tile grid, stitched, margins & LOD.
T7  Streaming           ChunkStreamer + floating origin around the player.
T8  Migration           content / nav / population fully per-chunk; retire the monolith.
```

Build the world model (Foundations → Processing → Structures → Chunks) before its
presentation and streaming. Erosion, Vegetation and the full Simulation layer are
additive and layer in later.

## 13. Implementation status

**Implemented — T2 World Foundations** (assembly `Lootbound.World`, `noEngineReferences`):
- `WorldCoordinate` (double) and `ChunkCoordinate` (int) — no `Vector3` in the
  World layer, enforced by the compiler.
- `IWorldField<T>` — the one contract; `HeightField : IWorldField<float>` is the
  first concrete World Field (pure, Unity-free), plus `RegionField` and a
  placeholder `ClimateField`.
- `WorldSampler` — the official entry point (Height / Danger / Region).
- Injected Providers at the seam: `INoiseSource` (Unity Perlin), `IHeightRemap`
  (AnimationCurve), `WorldProgressionDangerProvider`. The World layer never sees
  Unity.
- Global min/max normalization **removed** (`NormalizeToFullRange` and the
  `normalizeHeightmap` flag are gone). Relief is defined only by noise params +
  the HeightRemap curve; the monolithic generator now SAMPLES the HeightField.
- Earlier seeds of the model, still monolithic: `WorldProgression` (DangerField),
  the Landmark stamps (Structure Stamps), `AmbientPopulationController`
  (Simulation / Ambient Life).

**Implemented — T3 Domain Processing / World Knowledge** (`Lootbound.World/Processing/`):
- Analytic fields: `SlopeField`, `CurvatureField`, `RoughnessField`,
  `ElevationField`, `ExposureField`, `CliffField` (+ `Aspect`).
- `WorldDomain` {Bounds, Resolution, CellSize}; hydrology chain
  `FlowAnalyzer→FlowField`, `CatchmentAnalyzer→CatchmentField`,
  `WaterTableAnalyzer→WaterTableField`, `RiverMaskAnalyzer→RiverMaskField`.
- `TraversabilityField` (local cost), `LandscapeField` (`LandscapeType`).
- One Analyzer per Field, strict DAG, each documenting assumptions/limits.
- Gameplay-side composition: `WorldKnowledgeComposer` + `WorldKnowledge`; F9
  `WorldKnowledgeDebugOverlay`. Pure-World tests per field.
- No gameplay consumer yet — this slice computes, validates and visualizes the
  knowledge. T3.1 (path planner) will consume `TraversabilityField`.

**Not implemented** (this document's target): the Structures graph, Erosion,
Vegetation, real Landscapes-as-regions, World Chunks / ChunkData, the Simulation
layer, streaming, floating origin, async generation, the tile grid. The terrain
is still generated monolithically over one Unity Terrain — but now by SAMPLING
the World Engine, and the engine now UNDERSTANDS its geography.

## Related documentation

- `WORLD_DISC_AND_STREAMING_VISION.md` — the earlier vision this formalizes.
- `LANDMARKS.md` — the Structure Stamp pattern this generalizes.
- `WORLD_PROGRESSION.md` — the `DangerField`.
- `PROCEDURAL_TERRAIN.md` — the current monolithic implementation.
