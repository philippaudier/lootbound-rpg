# World Progression (Slice 0.9.7)

## Promise

Every world position owns a unique progression context. Danger, rarity,
population, resources, landmarks, ambience — every future decision reads
this context; none writes it. The player never sees a number: the world
itself tells the story of depth.

## The seven rings, five feelings

The spatial partition stays the fine 7-ring `WorldRing` enum; the designer
reading of it is emotional:

| Feeling | Rings |
|---------|-------|
| Refuge — safety | `Refuge` |
| Near — first steps | `Nearlands` |
| Exploration — the wild begins | `Wildlands` |
| Distant — preparation required | `Farlands`, `Outerlands` |
| Deep — respect | `Edgelands` (+ `Void`, outside the playable disc by default) |

Balance never hangs on ring labels: the continuous `Depth01` (0 at the
Refuge, 1 at the logical WorldDisc edge, clamped beyond) is the backbone,
mapped through curves.

## Authority

```text
WorldRingEvaluator      geometry only: distance -> normalized radius -> ring
        ↓
WorldProgression        pure, one per validated layout, carried by WorldLayoutContext
        ↓
WorldRingContext        immutable snapshot per position
```

- `WorldProgression.GetContext(Vector3)` / `GetContextFromDistance(float)`.
- Constructed by `ProceduralTerrainGenerator` after layout validation from
  `RefugePosition + WorldDiscRadius + WorldRingConfig +
  WorldProgressionConfig`; reached through
  `generator.Context.LayoutContext.Progression` or explicit injection
  (`WorldContentPlanner`). No singleton, no scene lookup.
- Nobody computes `distance(refuge, position)` or depth locally anymore.
- **Rings never depend on the seed**: for a given distance and world radius
  the ring is always identical (the refuge moves with the seed; the rule
  does not).

`WorldRingContext` fields: `Ring`, `DistanceFromRefuge`, `Depth01`,
`IsInsideWorldDisc`, `Difficulty01`, `ExpectedLootTier`, `FogDensity01`,
`LightAttenuation01`, `Saturation01`, `Temperature01`.

## WorldProgressionConfig

ScriptableObject with `Depth01 ->` curves: difficulty, loot tier (scaled to
`MaxLootTier`), fog, light attenuation, saturation, temperature. Assigned on
`TerrainGenerationConfig`; built-in linear defaults apply when absent.
Ambience values are **parameters only** — no rendering is driven yet.

## Content definitions

Every definition (encounter, resource, landmark) declares:

- `MinimumRing` / `MaximumRing` — **inclusive** window (unlike the spatial
  ring thresholds, which stay min-inclusive / max-exclusive).
  `MaximumRing` defaults to `Edgelands`: **Void requires explicit opt-in**.
- `SelectionWeight` — relative weight among compatible definitions
  (0 excludes).
- `WeightByDepth` — curve multiplied with the weight, evaluated at the
  **global** `Depth01` of the reservation (not a per-ring local value).
  This is how "wood 100% near, 40% deep / crystals 0% near, 100% deep"
  is authored.
- `DifficultyRating` / `LootValue` — V1 metadata: shown in F7, prepares
  ambient population; not yet consumed by balance.

`WorldContentCompatibility` is the single shared rule (planner + F7): it
returns the final effective weight or a human-readable incompatibility
reason. The planner then draws deterministically (per-reservation FNV RNG,
one draw). Loot: `ExpectedLootTier` is exposed and displayed; wiring actual
drops to the tier is a future balance slice.

## Debug — F7

`WorldProgressionDebugPanel`: context at the player position and at the
camera-aimed point (ring, depth, difficulty, loot tier, ambience), plus for
every registered definition its **final weight here** or **why it cannot
appear here**. Ring circles are already drawn by `WorldLayoutGizmos`.

## Tests

`WorldProgressionTests` (EditMode, pure): context stability, ring/depth
independence from the seed at equal distance, monotonicity, clamping and
`IsInsideWorldDisc`, min-inclusive spatial boundaries preserved, inclusive
definition windows, Void opt-in, zero-weight exclusion, depth-curve
modulation, fully deterministic weighted selection with a non-fragile
tolerance, injected-vs-fallback depth equivalence.

## Explicitly future

Ambient population (0.9.8), weather, shaders/rendering driven by ambience
parameters, music, dynamic difficulty, loot drops consuming the tier.
