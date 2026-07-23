# Procedural Circulation Engine — Invariants

> **Status**: non-negotiable rules (PCE 0.1). Every future PCE milestone is
> reviewed against this list. Each invariant comes with examples of
> violations — if a change resembles a violation example, it is one.
> Vocabulary: see `PCE_GLOSSARY.md`.

## 1. Absolute player freedom
The player can leave any corridor at any time. The PCE never creates rails,
invisible walls, forced progression or punitive off-path design.
- **Violation**: off-corridor movement artificially slowed "to encourage road
  use"; content that only ever spawns on corridors; a quest that requires
  staying on a path.

## 2. The terrain stays sovereign
The PCE reads the terrain first and adapts to it. It may *request* subtle
local influence; it never reshapes the world to rescue a route. A bad
crossing may remain bad.
- **Violation**: flattening a mountainside so a MajorRoad fits; carving an
  implicit tunnel; filling a deep valley; any influence applied without going
  through an Influence Request.

## 3. Corridors precede traces
World logic never depends on rendering. A texture exists because a corridor
justifies it — never the reverse.
- **Violation**: gameplay querying "is there brown paint here?"; a trace
  painted with no underlying corridor; corridor data derived from splat maps.

## 4. Infinite world — no global graph
No finite world-wide graph is ever required. Any region must be evaluable
locally from `World Seed + world coordinates + configuration`.
- **Violation**: a solver that must see the whole disc before answering; a
  serialized "world road network" asset; any query whose cost grows with
  world size.

## 5. Determinism
Same seed, same configuration, same coordinates: same flows, corridors,
branches, families, ages and history states. Always.
- **Violation**: `UnityEngine.Random` anywhere in the PCE; results depending
  on frame timing, player behaviour, or floating state left from a previous
  query.

## 6. Chunk-load-order independence
The order in which chunks are loaded, unloaded or reloaded never changes the
network.
- **Violation**: a corridor that bends differently because its neighbour
  chunk happened to be resident; growth algorithms seeded by "whichever chunk
  asked first".

## 7. Exact cross-chunk continuity
A corridor crossing a chunk border continues exactly on the other side, and
the neighbour does NOT need to be loaded for the result to be correct.
- **Violation**: hairline breaks at borders; a trail that dead-ends at a
  chunk edge until the neighbour streams in; border stitching that needs both
  chunks in memory.

## 8. Hierarchical rarity
The world must never become a road grid.
```text
MajorRoad        very rare
SecondaryRoad    rare
MountainTrail    moderate
AnimalTrail      frequent but discreet
ForgottenPath    rare and fragmentary
```
- **Violation**: every valley getting a road; corridors of the same family
  running parallel without a reason; density tuned until the wild disappears.

## 9. A promise is never a guarantee
Some paths lead to a panorama, a clearing, a collapsed passage, a silence —
or nothing notable at all. Silences and false mysteries are mandatory
outcomes, not failures.
- **Violation**: a reward table attached to path endings; "dead end
  compensation" loot; tuning that makes every ForgottenPath pay out.

## 10. Every branch has a reason
A fork exists because of a landmark, a pass, water, the Refuge, a plausible
shortcut, an old usage, a topology change or an animal need — a spatial,
historical or ecological cause.
- **Violation**: decorative T-junctions; branches placed by pure noise to
  "look organic"; a fork whose two arms serve the same purpose the same way.

## 11. Natural readability
The player learns to read circulation from the world itself — width, surface,
curvature, upkeep, vegetation, sound, traffic, built remnants — never from
mandatory UI labels.
- **Violation**: a minimap legend required to distinguish families; families
  that are functionally different but visually identical; UI-first signage.

## 12. Structure survives streaming
Structural circulation data (existence, family, intensity, direction, width,
age, stable identity, local connections) is deterministic and recomputable.
Presentation (paint, meshes, decals, vegetation edits, props) belongs to
active chunks only. Unloading a chunk may destroy its presentation — never
its procedural identity.
- **Violation**: corridor identity stored only in a chunk's baked assets; a
  reload that changes a corridor's id, family or axis; presentation objects
  used as the source of truth for queries.

## 13. The PCE stays in its lane
It models circulation. It does not absorb world layout, landmark planning,
content spawning or narrative. Some places are deliberately out of network.
- **Violation**: the PCE deciding where villages are; every landmark forced
  onto a corridor; hidden places made reachable "for completeness".

## 14. The PCE never knows the player
The network exists independently of the player. It never generates corridors
toward the player, around the player or for the player. It existed before
them; it will continue after them. The player only ever **discovers** it.
- **Violation**: player position as any solver input; corridor density that
  follows the player; a trail spawned so the player finds something; any PCE
  API taking "the player" as a parameter (queries take world positions —
  whose positions they are is none of the PCE's business).

## 15. The PCE never optimizes for player enjoyment
It seeks territorial coherence, logic and history. Enjoyment emerges from
that honesty; it is never engineered into generation.
- **Violation**: `if player unrewarded for 30 min → create a ruin`;
  pacing-driven corridor or trace placement; any feedback loop from play
  metrics, session length or telemetry into generation. The world stays
  honest even when it is quiet.

## 16. Cost is never absolute
The terrain is neutral. A 35° slope has no canonical cost — it has a cost
FOR someone: Cost(human), Cost(animal), Cost(merchant), Cost(goat), Cost(a
mover not yet imagined). Every cost query goes through a `TraversalProfile`;
no world-wide "difficulty" truth exists.
- **Violation**: a single global cost/difficulty map shared by every mover;
  comparing costs produced by different profiles as if commensurable; UI or
  gameplay presenting one absolute "terrain difficulty"; any API returning a
  cost without a profile.

## 17. The Terrain Cost System knows no game entity
It knows no Human, Wolf, Deer, Merchant, Bandit or Player — only
`TraversalProfile`s. Gameplay maps entities to profiles
(`Merchant → CirculationProfile → TraversalProfile`); the engine only ever
receives a profile. The real success of this rule: no engine algorithm ever
needs to know what an animal IS. Profiles are composable data — a new mover
is a new instance, never a new class or a new algorithm.
- **Violation**: `switch (entityType)` anywhere in Terrain Intelligence or
  the PCE; a `HumanTraversalProfile` subclass; a `HumanTerrainCost` type;
  entity names appearing in `Lootbound.World`.

## 18. The world holds no opinion (Neutrality Principle)
Terrain Intelligence measures and classifies — `Slope = 37°`,
`Landscape = Valley` — it never judges. "Bad slope", "hostile ground", "ugly
detour" do not exist in the World Engine; judgment is exactly what a
`TraversalProfile` (and tomorrow, any perception) adds. **The world is
objective; its actors are subjective.** The engine states 37°; only a
perception may call it steep.
- **Violation**: a knowledge field encoding a verdict (a profile-less
  "difficulty" or "badness" field); thresholds inside Terrain Intelligence
  tuned "because roads shouldn't go there"; World-layer code whose naming or
  comments speak for a mover.

## 19. Territorial Intelligence measures, never names
The Neutrality Principle (18) applied to territories: this layer produces
measures — Accessibility 0.71, Isolation 0.23 — never labels. "High valley"
or "mountain basin" are judgments a consumer may derive later. No enum, no
`TerritoryType`, no name ever lives in the engine.
- **Violation**: `enum TerritoryType { Valley, Mountain, Forest }`; a field
  returning a territory name; thresholds in the World layer converting
  measures into labels.

## 20. A territory never has an exact boundary
Influences always decay. A point always belongs to several territorial
logics at once, by degree — never to exactly one.
- **Violation**: hard territory borders; region polygons; classifying a
  point into a single territory; any API answering "which territory is
  this?" with one label instead of measures.

## Related Documentation

- `PCE_GLOSSARY.md` — the vocabulary these rules are written in.
- `PCE_ARCHITECTURE.md` — the structures that enforce them.
- `WORLD_ENGINE_ARCHITECTURE.md` §6.1 — the ownership style this follows
  (machine-enforced where possible, as with `ChunkOwnershipTests`).
