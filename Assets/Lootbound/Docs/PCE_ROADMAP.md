# Procedural Circulation Engine — Roadmap

> **Status**: milestone plan locked at PCE 0.1. The first VISIBLE result is
> deliberately not before PCE 0.9: language and spatial coherence come first.
> Each milestone lists its dependency and an observable result — nothing ships
> without one. The V1 discipline of the World Engine applies: smallest
> complete version, simple swappable algorithms, tests, no speculative code.

## Milestones

### PCE 0.1 — Design Bible & Architecture Contract *(this)*
- **Result**: the five PCE documents; vocabulary, invariants, architecture
  and roadmap frozen. No code.

### PCE 0.2 — Terrain Analysis Foundation
- **Depends on**: 0.1; existing World Knowledge (T3).
- **Work**: audit World Knowledge against PCE needs (valleys, passes, water,
  surface difficulty); fill only measured gaps; decide the PCE's assembly /
  namespace home (deferred question #1).
- **Observable**: a written mapping "PCE need → existing field / new field",
  plus F-key overlay views for any new field. No circulation yet.

### PCE 0.3 — Terrain Cost System
- **Depends on**: 0.2 (see `PCE_TERRAIN_INTELLIGENCE.md` §5 for the detailed
  plan).
- **Work**: the cost MECHANISM — `TraversalProfile` (how a mover perceives
  the terrain) → `TerrainCostField`, in Terrain Intelligence; the first
  three named profiles (human-road, mountain, animal) as PCE-side data;
  the final-relief adapter (G4). Cost is never absolute (invariant 16).
- **Observable**: cost overlays per profile; pure tests (determinism, huge
  coordinates, and profile divergence — the same terrain must rank
  differently under different profiles).

### PCE 0.4 — Territorial Intelligence
- **Depends on**: 0.3. (Renamed from "Deterministic Regional Domains" —
  knowledge, not objects; see `PCE_TERRITORIAL_INTELLIGENCE.md`.)
- **Work**: the first mid-scale kernel — `TerritorialIdentity`
  (Accessibility, Isolation, Connectivity) as grid-free ray statistics over a
  cost view; invariants 19 (measures, never names) and 20 (no exact
  boundaries); F10/F11/F12 overlays. Deliberately three measures, no more.
- **Observable**: territorial heat overlays; property tests (plain vs pocket
  vs corridor rankings, determinism, continuity, 0..1).
- **Note**: the grid-free design made the "deterministic regional domains"
  machinery unnecessary at this stage; that need (and the edge-of-world
  decay policy, deferred #4) moves to the corridor milestones 0.6–0.8,
  where domain-bound hydrology and macro flows genuinely require it.

### PCE 0.5 — Circulation Intents & Attractors
- **Depends on**: 0.4; existing layout/landmark data as attractor sources.
- **Work**: intent derivation from attractors + geography (no routes yet).
- **Observable**: intent lists per region, deterministic; overlay of
  attractors and intent directions.

### PCE 0.6 — Macro Flow Prototype
- **Depends on**: 0.5.
- **Work**: flows from intents; macro axes with intensity (still invisible).
- **Observable**: macro flow overlay across a large area; determinism tests.

### PCE 0.7 — Corridor Representation
- **Depends on**: 0.6.
- **Work**: the `CirculationCorridor` structural type — band, axis, width,
  intensity, family, age, stable id; queries `Sample` / `TryFindNearestCorridor`
  in structural form.
- **Observable**: corridor overlay (bands, not lines); id stability tests.

### PCE 0.8 — Cross-Chunk Continuity
- **Depends on**: 0.7 + 0.4.
- **Work**: prove and test the continuity invariant at real chunk borders.
- **Observable**: automated border tests (any load order, unload/reload);
  overlay across seams.

### PCE 0.9 — First Visible Mountain Trail
- **Depends on**: 0.8.
- **Work**: ONE family (`MountainTrail`) rendered end-to-end: corridor →
  visible trace (surface painting via the existing splat pipeline). One
  polished example over many shallow variants.
- **Observable**: walkable trail in the sandbox crossing several chunks with
  no seam; the first Lootbound circulation screenshot.

### PCE 0.10 — Branches & Secondary Routes
- **Depends on**: 0.9.
- **Work**: motivated forks (invariant 10), `SecondaryRoad`; begin the
  reconciliation with `WorldLayoutGenerator` radial paths (deferred #2).
- **Observable**: forks with visible reasons; layout reconciliation decision
  recorded.

### PCE 0.11 — Forgotten Paths
- **Depends on**: 0.10.
- **Work**: history states (Active → … → Forgotten); intermittent traces over
  continuous corridors; built remnants as trace props.
- **Observable**: an old path whose pavement surfaces then vanishes while the
  corridor continues.

### PCE 0.12 — Animal Trails
- **Depends on**: 0.9 (not 0.11).
- **Work**: animal profile (cover, water, braiding, human avoidance).
- **Observable**: braided discreet trails converging on water; wildlife
  systems can query `SamplePreferredDirection`.

### PCE 0.13 — Terrain Influence
- **Depends on**: 0.9.
- **Work**: Influence Requests consumed by the stamp/sculpt pipeline
  (micro-softening, wear, camber, switchback clearance) within the forbidden
  limits of `PCE_ARCHITECTURE.md` §2.
- **Observable**: a trail that subtly seats into the slope; before/after
  comparison; invariant-2 tests (refused influences stay refused).

### PCE 0.14 — Vegetation & Surface Language
- **Depends on**: 0.11-0.13.
- **Work**: the readability vocabulary (invariant 11): width, wear, moss,
  thinning vegetation, per-family visual language.
- **Observable**: blind family identification by playtest — no UI labels.

### PCE 0.15 — Landmark Integration
- **Depends on**: 0.10.
- **Work**: bidirectional relation — circulation-born landmarks (inns,
  bridges, memorials) and attractor-born corridors; keep out-of-network
  places out (invariant 13).
- **Observable**: a bridge that exists because a corridor crosses water; a
  hidden place that remains roadless.

### PCE 1.0 — Playable Circulation Ecosystem
- **Depends on**: everything above.
- **Work**: all families live together at world scale under hierarchical
  rarity; consumers wired (wildlife, ambience, encounters at minimum);
  performance within the streaming budgets.
- **Observable**: an expedition where choosing between road, trail and wild
  is a constant, legible, rewarding decision — the Moments of the Design
  Bible reproduce naturally.

## Named early: Territory Memory

The history model (`Active → Declining → Abandoned → Forgotten`) is the seed
of a larger future layer, already named: **Territory Memory** (see
`PCE_GLOSSARY.md`) — the territory's own story, where corridors accumulate
chained events (a road used for centuries → a bridge → a village → decline →
a forgotten trail). Expected to emerge naturally around PCE 0.11–0.15 and to
mature after 1.0: history as a deterministic layer on top of geography, so
the world can evolve without ever breaking determinism.

## Named early: PerceptionProfile

`TraversalProfile` may prove to be the first of a family. If other systems
one day need subjective readings of the objective world — a `VisionProfile`,
a `SoundProfile`, a `ResourcePreferenceProfile` — they will follow the same
philosophy (the Neutrality Principle, invariant 18: the world is objective,
systems perceive it differently), and `TraversalProfile` would become one
specialization of a general `PerceptionProfile` idea. Named now; deliberately
NOT designed and NOT implemented.

## Deferred questions (tracked, deliberately unanswered at 0.1)

1. **Assembly home** of PCE logic (Unity-free layer in the World Engine
   style) — decide at 0.2.
2. **Reconciliation with `WorldLayoutGenerator`** radial paths and corridor
   flattening — direction: the PCE progressively becomes the circulation
   authority, layout keeps node/POI planning; decide during 0.10.
3. **Solver algorithm** for corridor resolution — explicitly NOT chosen at
   0.1 (no A* commitment); choose at 0.6-0.7 from measured candidates.
4. **Edge-of-world decay** (rings, The Void) — how circulation density fades
   outward; decide at 0.6 with `WORLD_RINGS.md` in hand (moved from 0.4,
   which went grid-free and did not need it).
5. **Hydrology interplay** (fords, bridges, valley-following vs river
   crossing) — after 0.9.
6. **Doc refresh**: `WORLD_DISC_AND_STREAMING_VISION.md` predates the
   implemented chunk streaming (T3.1A) and needs a status update — separate
   housekeeping task, not a PCE milestone.

## Related Documentation

- `PCE_DESIGN_BIBLE.md` · `PCE_ARCHITECTURE.md` · `PCE_GLOSSARY.md` ·
  `PCE_INVARIANTS.md`
