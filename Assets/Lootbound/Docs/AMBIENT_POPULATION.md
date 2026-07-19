# Ambient World Population (Slice 0.9.8)

## Promise

The world is inhabited before the player arrives. Creatures live anywhere
the terrain, ring, NavMesh and their own rules allow - off paths, off nodes,
off reservations. Leaving a trail, climbing behind a mound and finding a
presence that seems to have always lived there: that is the success case.

## Two content families, never confused

```text
Authored:  WorldLayout → Reservation → Recipe → Encounter   (unchanged)
Ambient :  WorldProgression + Terrain + Runtime NavMesh + Definitions
           → cells → intentions → creatures
```

Ambient reads the same foundations (WorldRingContext through
`WorldProgression.GetContext`, `WorldContentCompatibility` for windows and
weights, the runtime NavMesh through the same gate); it writes only its own
instances under `AmbientPopulation_Spawned`.

## Architecture

```text
AmbientPopulationController   orchestrates (waits for terrain + navigation,
        ↓                     activates cells, budgets, despawn, purge)
AmbientPopulationPlanner      what SHOULD live here (pure, deterministic)
        ↓
AmbientPopulationPlan         the intention (definition, group, candidates)
        ↓
AmbientSpawnValidator         is it possible here, NOW? (structural/transient)
        ↓
AmbientPopulationSpawner      materialization (instantiate, warp, identity)
        ↓
AmbientPopulationRegistry     what happened to this presence (local memory)
```

## Cells - the world's local memory

- `Vector2Int` coordinates **relative to the logical WorldDisc center**
  (the Refuge position) - an explicit dependency, never the world origin.
- Default 48 m, 1-2 anchors per cell, groups of 1-3 (sobriety first).
- Cell seed = FNV(`"Ambient"`, `PopulationGenerationVersion`, WorldSeed,
  cell) - same seed + same cell → same intention, always. The player only
  influences WHEN a cell is planned, never WHAT it contains. No
  `UnityEngine.Random`, no `GetInstanceID`, no `Time` in generation.
- Each anchor carries **several stable candidate positions**: one unlucky
  NavMesh point never condemns the intention.

## Structural vs transient rejections

| Kind | Examples | Effect |
|------|----------|--------|
| **Structural** | outside disc, ring window, slope, no NavMesh, refuge buffer, authored exclusion | candidate permanently invalid this generation; a plan dies only when EVERY candidate is structural |
| **Transient** | player too close, camera frustum, budgets, neighbor too close | "not now" - the intention stays pending, retried later, never re-rolled |

## Anti-pop rules

`MinimumDistanceFromPlayer` + camera frustum rejection (optional
line-of-sight raycast exists, off by default). The initial pass PLANS every
nearby cell but MATERIALIZES only what passes these rules: a nearby cell can
hold an intention without a creature popping next to the player.

## Streaming and memory

- Despawn: beyond `DespawnRadius` + grace (10 s) + peaceful state only
  (never Suspicious/Chasing/attacking/staggered) - a fought creature never
  disappears because the player stepped back.
- The despawn keeps the PLAN and a snapshot (identity + normalized health):
  returning brings back the **same presence, still wounded**, at its
  resolved anchor (never a mid-chase position). Streaming is never a free
  heal (`EnemyHealth.RestoreNormalizedHealth`, no damage events).
- Death is remembered for the whole session: a defeated presence never
  returns (`respawnAfterDeath` exists, off by default).

## Identity

One identity system: `WorldContentIdentity` extended with
`WorldContentOrigin {Authored, Ambient}` and the cell. Ambient ids are
namespaced (`ambient_v{version}_{x}_{y}_a{anchor}_m{member}`) - no logical
collision with authored reservation ids. `EnemyBrain` is reused unchanged:
per-instance RNG from (WorldSeed, id, member index), `HomePosition` captured
after the final Warp.

## Generation lifecycle

`OnGenerationComplete` is the earliest observable invalidation signal (the
generation pipeline is synchronous): the controller purges everything
immediately - instances, plans, snapshots, stats - then waits for the
matching `OnNavigationCompleted` through its own `NavigationContentGate`.
Stale results are rejected by `GenerationId`. `OnDied` subscriptions are
released on despawn, death and purge.

## Debug - F7 and gizmos

F7 (WorldProgressionDebugPanel): alive/budget, planned cells, spawned /
despawned / deaths, current cell (coordinates, seed, ring, depth), recent
rejections with their reason and kind. Controller gizmos (selected): cell
grid colored by state, spawn/despawn radii, anchors, homes, recent rejected
candidates (red = structural, yellow = transient).

## Scene setup

Add `AmbientPopulationController` to the scene: assign the terrain
generator, the `RuntimeNavigationBuilder`, the player transform and
`DefaultAmbientPopulationConfig` (definitions live inside the config asset).
Optionally assign the controller on the F7 panel.

## Explicitly future

Packs and coordinated groups, migrations, predators/prey, day-night cycles,
weather-driven spawns, ambient elites, persistent respawn across sessions,
off-camera ecosystem simulation, hundreds of simultaneous actives.
