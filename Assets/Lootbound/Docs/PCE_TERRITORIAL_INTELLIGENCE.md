# PCE 0.4 — Territorial Intelligence

> **Status**: implemented (first kernel). Vocabulary: `PCE_GLOSSARY.md`.
> Rules: `PCE_INVARIANTS.md` (esp. 18–20).

## The ladder of abstraction

> **The terrain describes the shape of the world.**
> **Terrain Intelligence understands the relief.**
> **Territorial Intelligence understands territories.**
> **The PCE understands movement.**

Each layer raises the level of abstraction — and each one only ever READS the
layer below. Territorial Intelligence answers a new question:

> *"What geographic logic governs this place?"*

Not "which biome". Not "which texture". Not even "which name".

## Principles

1. **A territory is never painted. It emerges.**
2. **Boundaries are always fuzzy** — influences decay, without exception
   (invariant 20).
3. **A point always belongs to several territorial logics at once**, by
   degree — never to exactly one.
4. **The territory is a consequence, never an input.**
5. **It measures; it never names** (invariant 19 — the Neutrality Principle
   applied to territories). There is no `enum TerritoryType` and there never
   will be. "Mountain basin" is a judgment a consumer may derive later from
   the measures.

## The first kernel — three measures, deliberately no more

`TerritorialIdentityField : IWorldField<TerritorialIdentity>` consumes ONE
already-composed cost view (built from a `TraversalProfile`), marches K
deterministic rays over a mid-scale radius, and returns:

| Measure | 0..1 meaning | V1 definition |
|---|---|---|
| `Accessibility` | how cheaply this perception moves AROUND here | ideal ÷ mean ray cost |
| `Isolation` | how costly the EASIEST way out is (enclosure) | 1 − ideal ÷ best ray cost |
| `Connectivity` | how many distinct easy directions exist | open rays ÷ K |

Perception-relative by construction (a goat's isolation is not a merchant's),
grid-free (cross-chunk continuity is structural, not stitched), pure and
deterministic, zero allocation per evaluation, engine-free (compiler-proven).

**Restraint is the design**: Centrality, Exposure, Relief Energy, Natural
Boundaries and the rest of the candidate vocabulary stay UNBUILT until a real
consumer demonstrates the need. One milestone, one capability.

## Observable proof

In the sandbox (world generated): **F10** Accessibility · **F11** Isolation ·
**F12** Connectivity (heat overlays; territorial redraw is deliberately slow —
it is a debug proof, not a runtime path). Plains glow accessible and
connected; walled basins glow isolated; corridors show few open directions.

## Future consumers (why this layer is generic, not PCE-private)

| System | Use |
|---|---|
| PCE | where circulation is natural (flows seek accessible, connected land) |
| World Layout | plausible placement for refuges and settlements |
| Wildlife | habitat plausibility (enclosure, access to water later) |
| Resources | emergence by territorial character |
| Weather | where fog, wind and humidity persist (basins, exposure later) |
| Ambient audio | soundscape by openness and enclosure |
| Future quests | natural descriptions of the world derived from measures |

## Related Documentation

- `PCE_TERRAIN_INTELLIGENCE.md` — the layer below (0.2/0.3).
- `PCE_ARCHITECTURE.md` — the pipeline this slots into.
- `PCE_ROADMAP.md` — what 0.4 deliberately did not do, and where the
  regional-domain machinery moved.
