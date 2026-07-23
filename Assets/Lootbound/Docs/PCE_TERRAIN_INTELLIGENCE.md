# PCE 0.2 — Terrain Intelligence Foundation

> **Status**: audit and integration contract (PCE 0.2). Goal: let the world
> understand its own geography before anything decides how to cross it. This
> milestone implements NO corridor, NO flow field, NO circulation algorithm —
> it maps what the World Engine already knows, names what is genuinely
> missing, and locks how the PCE will consume it. Vocabulary:
> `PCE_GLOSSARY.md`. Rules: `PCE_INVARIANTS.md`.

## 1. Knowledge matrix — what already exists

All fields below live in `Lootbound.World` (Unity-free), are pure
`IWorldField<T>` functions of `seed + coordinate`, and are deterministic.
"Infinite-ready" = evaluable at any world coordinate with no bounded domain;
"domain-bound" = computed over a `WorldDomain` grid (currently the
materialized region, 129² default).

| Knowledge | Field / API | Unit · range | Scope | PCE relevance |
|---|---|---|---|---|
| Height (base relief) | `HeightField` | normalized 0..1 | infinite-ready | foundation of everything |
| Final relief (base + refuge/paths/stamps) | `ProceduralTerrainGenerator.SampleHeight` (`IWorldHeightSampler`) | metres | in-region final, base beyond | see gap G4 |
| Slope | `SlopeField` | degrees | infinite-ready | primary cost input |
| Curvature | `CurvatureField` | signed, convex > 0 | infinite-ready | valley/ridge signal |
| Roughness | `RoughnessField` | metres (V1: includes slope trend) | infinite-ready | cost input |
| Elevation | `ElevationField` | normalized | infinite-ready | highland behaviour |
| Exposure / aspect | `ExposureField` + `Aspect` | bearing °, −1 flat | infinite-ready | later (weather, vegetation language) |
| Cliff | `CliffField` | bool (slope > 60°) | infinite-ready | hard cost |
| Flow direction | `FlowField` | D8 direction / cell | **domain-bound** | valley-axis proxy (8-quantized) |
| Catchment | `CatchmentField` | accumulation | **domain-bound** | river/valley importance |
| Water table | `WaterTableField` | wetness proxy | **domain-bound** | animal attractors later |
| River mask | `RiverMaskField` | bool | **domain-bound** | crossings, fords, cost |
| Landscape | `LandscapeField` → `LandscapeType` {Plain, Valley, Ridge, Mountain, Plateau, **Pass**, Basin, Cliff} | enum, point-wise | mixed (reads river) | reads the terrain the way the PCE needs — but point-wise only |
| Traversability | `TraversabilityField` | local cost ≥ 1 | mixed (reads river) | **the proto cost field** — exactly ONE hard-wired profile |
| Region/bounds | `WorldDomain`, `WorldBounds` | — | — | evaluation windows |
| Composition seam | `WorldKnowledgeComposer.Build(config, seed, region)` | — | Gameplay-side | the pattern PCE composition will mirror |

Determinism caveat that matters to the PCE: the four hydrology fields (and
therefore the river inputs of Landscape and Traversability) depend on the
`WorldDomain` they were computed over. Today that domain is the materialized
region. **Region-independent evaluation is the exact subject of PCE 0.4**
(Deterministic Regional Domains) — a tracked dependency, not a 0.2 gap to
solve.

## 2. Missing capabilities — genuine gaps only

Rule applied (from the mission): if it exists, reuse it; if it is missing,
justify it and put it in **Terrain Intelligence** (the World Knowledge
layer), never inside the PCE.

### G1 — Located, ranked passes
`LandscapeType.Pass` can say "this point is pass-like"; nothing can answer
"what are the passes of this area, where exactly, and how important is
each?". Macro corridors cross ranges at passes — they need passes as
**features** (position, saddle depth, connectivity), not as a per-point bool.
- **Home**: new analyzer in `Lootbound.World/Processing` (working name
  `PassDetector` / saddle analysis over a domain), same V1 discipline as T3.
- **When**: PCE 0.3 if cost-field prototyping confirms the need at that
  stage, else PCE 0.6 (macro flows are the first true consumer).

### G2 — Valley axis as a direction (vector)
Corridors follow valleys; that needs "the direction *along* the valley".
`FlowField` (D8) already gives per-cell downstream direction and
`CatchmentField` gives importance — the concept exists, but the direction is
8-quantized and domain-bound.
- **Home**: thin derived field in Processing (working name
  `FlowDirectionField`, interpolated vector from the D8 grid + accumulation
  weighting). Possibly unnecessary if D8 proves sufficient.
- **When**: decide by measurement during PCE 0.3/0.6 prototyping — not
  built speculatively now.

### G3 — Per-profile terrain cost
`TraversabilityField` hard-wires one parameter set
(`TraversalBaseCost 1, SlopeCostPerDegree 0.05, CliffCost 100,
RoughnessCostPerMetre 0.5, WaterCost 25`) — i.e. one implicit "generic
human" profile. The PCE needs the same mechanism with different weights per
`CirculationProfile` (a mountain trail tolerates slope; an animal fears open
ground more than gradient; an ancient trade road hates slope above all).
- **Home split (locked)**: the **mechanism** (a cost field parameterized by a
  `TraversalProfile`) belongs to Terrain Intelligence; the **named profiles**
  (one per circulation family) belong to the PCE. Knowledge computes; the
  PCE means.
- **When**: PCE 0.3 — this IS milestone 0.3 (Terrain Cost System).

### G4 — Knowledge over the FINAL relief
`WorldKnowledgeComposer` builds every analyzer on the **base** `HeightField`.
The authored deformations (refuge basin, corridor flattening, landmark
seats) are invisible to knowledge — yet circulation near the Refuge must see
the carved basin. The generator already exposes the final relief
(`IWorldHeightSampler.SampleHeight`, in-region final + base beyond).
- **Home**: a small adapter in the composition seam (an `IWorldField<float>`
  over `IWorldHeightSampler`) so knowledge — and therefore costs — can be
  built on the final ground. No new analyzer; a wiring choice.
- **When**: PCE 0.3, where costs make the difference observable.

### G5 — Water access points (for animal intents)
"Where can you drink" (river cells, standing-water shores) is derivable from
`RiverMask` + `WaterTable`; no dedicated feature extraction exists.
- **Home**: derived recipe first (documented query pattern); analyzer only if
  measurement shows the recipe is too weak.
- **When**: PCE 0.5 (attractors) — not before.

Explicitly **not** gaps (deliberate non-needs at this stage): ridge lines
(no early consumer — V1 discipline), erosion, soil types, vegetation density
(later consumers of the PCE, not inputs to 0.3–0.6).

## 3. Integration architecture (locked at 0.2)

```text
Lootbound.World  (existing, Unity-free)
 ├── Layers/Fields        HeightField …                ← unchanged
 └── Processing           Slope … Traversability       ← Terrain Intelligence
      └── (0.3+) cost mechanism, pass/flow-direction analyzers as justified

Lootbound.Circulation  (FUTURE assembly — created with the first PCE type, ~0.5/0.7)
 └── the PCE proper: intents, flows, corridors, families, queries
      references Lootbound.World — and nothing references it back

Lootbound.Gameplay  (existing)
 └── composition seams: WorldKnowledgeComposer today,
     a CirculationComposer later, mirroring the same pattern
```

**Decisions locked**:
1. Terrain Intelligence additions (G1–G4 mechanisms) go into
   `Lootbound.World/Processing` — they are world knowledge, consumable by any
   system, PCE included but not privileged.
2. The PCE proper gets its **own Unity-free assembly** `Lootbound.Circulation`
   referencing `Lootbound.World` one-way (the read-only dependency becomes
   compiler-enforced, in the spirit of `noEngineReferences` and
   `ChunkOwnershipTests`). It is created when the first PCE type exists —
   no empty assembly today.
3. The PCE consumes knowledge **only** through `IWorldField<T>` /
   `WorldKnowledge` contracts. Never raw noise, never
   `TerrainGenerationContext` grids, never Unity objects.
4. Profile semantics (families, weight sets, promises) never leak into
   `Lootbound.World`. Knowledge computes; the PCE means.

## 4. Proposed public contracts (documented — 0.3 implements)

```csharp
// Terrain Intelligence (Lootbound.World/Processing)

/// How ONE kind of mover PERCEIVES the terrain - deliberately not named
/// "weights": today it weighs, tomorrow it may carry fear of exposure,
/// preferred cover, avoidances. The terrain is neutral; cost only exists
/// through a TraversalProfile (invariant 16).
/// (Working shape - final members fixed at implementation.)
public sealed class TraversalProfile
{
    public float BaseCost;
    public float SlopeCostPerDegree;
    public float CliffCost;
    public float RoughnessCostPerMetre;
    public float WaterCost;
    // 0.3 candidates, added only if measured useful:
    // float SlopeComfortMax; float ExposurePenalty; float CoverBonus;
}

/// Traversal cost for ONE perception - the generalization of today's
/// TraversabilityField (which becomes "cost through the default profile").
public sealed class TerrainCostField : IWorldField<float>
{
    public TerrainCostField(WorldKnowledge knowledge, TraversalProfile profile);
    public float Evaluate(WorldCoordinate coordinate);
}
```

Guarantees: deterministic per (seed, profile, coordinate); infinite-ready
wherever its inputs are; unit documented (abstract cost ≥ ~BaseCost, additive
like today); no allocation per evaluation; V1-swappable. **No cost API ever
answers without a profile** (invariant 16).

The PCE-side contracts (`CirculationProfile` = the semantic bundle that OWNS
a `TraversalProfile`) stay conceptual until `Lootbound.Circulation` exists —
see `PCE_ARCHITECTURE.md` §9 for the query surface they will feed.

## 5. Plan for PCE 0.3 — Terrain Cost System

The name is deliberate: 0.3 builds a **mechanism**, not one field. The same
engine produces `TerrainCostField`s from any `TraversalProfile` — human,
animal, merchant, mountain, or a mover not yet imagined.

1. Implement `TraversalProfile` + `TerrainCostField` in Processing;
   re-express `TraversabilityField` as the default-profile instance (no
   behaviour change — golden-guarded).
2. Wire the **final-relief option** (G4 adapter) into the composition seam;
   measure the difference around the Refuge.
3. Define the three first `TraversalProfile`s — human-road, mountain,
   animal — as DATA on the Gameplay/PCE side (values are tuning, not
   architecture).
4. Extend the F9 overlay: one key per cost profile, side-by-side comparison.
5. Pure tests: determinism, huge/negative coordinates, and **profile
   divergence** (the same terrain must rank differently under different
   weights — that is the milestone's observable proof).
6. Decide G1 (passes) and G2 (flow direction) by prototyping need, not by
   anticipation.

Out of scope for 0.3, restated: corridors, flows, intents, solver choice,
any visible trace.

## Related Documentation

- `PCE_ARCHITECTURE.md` §2 (pipeline), §10 (anchoring) — this document is
  the detailed execution of that anchoring.
- `PCE_ROADMAP.md` — 0.2 deliverable (this), 0.3 plan above.
- `WORLD_ENGINE_ARCHITECTURE.md` §3 — the World Knowledge this audits.
