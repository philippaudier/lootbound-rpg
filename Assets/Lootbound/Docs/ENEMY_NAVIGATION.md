# Enemy Navigation Behaviours (Slice 0.9.6)

## Promise

An enemy occupies a territory: it rests, wanders with a simple intention,
notices the player progressively, chases readably, gives up when the player
leaves its territory, walks back home and resumes its routine. No
omniscience, no infinite pursuit, no teleport in normal operation.

```text
Idle → Wandering/Patrolling → Suspicious → Chasing → ReturningHome → Idle
```

## Single state machine

One `EnemyState` authority (no parallel navigation enum, no state booleans):
`Idle, Wandering, Patrolling, Suspicious, Chasing, AttackWindup,
AttackActive, AttackRecovery, ReturningHome, Stagger, Stuck, Dead`.

Every transition is centralized in `EnemyStateMachine` (pure C#, injected
time) and carries `PreviousState`, `CurrentState`, `TransitionReason`,
`StateEnteredAt`. Transitions are published exactly once; `Dead` is
absorbing. Combat keeps its existing contract (`EnemyCombat` listens to
`OnStateChanged` and strikes during `AttackActive`).

## Separation of responsibilities

| Piece | Role | Testability |
|-------|------|-------------|
| `EnemyPerception` | Decides what is SEEN: distance + FOV + line of sight (masked, triggers ignored) + omnidirectional `ImmediateDetectionRange`; throttled (`PerceptionInterval`, staggered per instance) | Unity raycast — PlayMode |
| `EnemyStateMachine` + `EnemyPursuitRules` | Decide the STATE: transitions, suspicion, leash + hysteresis, reacquire cooldown, arrival | Pure — EditMode |
| `IEnemyRoamingBehaviour` (`EnemyWanderBehaviour`, `EnemyPatrolBehaviour`) | Decide WHERE/WHEN to roam | Pure (injected sampler/RNG/time) — EditMode |
| `EnemyBrain` | Orchestrates and applies through the `NavMeshAgent` (the agent IS the motor — no extra layer) | PlayMode |

Future slices (0.9.8 ambient population) add roaming behaviours
(pack/guard/camp...) by implementing `IEnemyRoamingBehaviour` — the brain
does not change.

## Territory

`HomePosition` is captured by the brain once the agent stands on the final
NavMesh (after the spawner's `Warp`) — never the reservation position, the
host node or the raw prefab position. No node/path assumption: any navigable
start position works. The territory is `HomePosition` + `WanderRadius` +
`MaxChaseDistanceFromHome` + `ReturnCompletionDistance`, all visible as
gizmos when the enemy is selected (green = wander, orange = leash).

## Wandering

Rest → weighted move choice → bounded NavMesh-validated destination around
**HomePosition** (not the current position, so the enemy drifts back
naturally) → move → rest. Distance weights make enemies live more than they
walk: 40 % stay put, 30 % short (0.15–0.35 R), 20 % medium (0.35–0.7 R),
10 % far (0.7–1 R). Pacing is desynchronized per instance through a
`System.Random` seeded from **WorldSeed + ReservationId + entry index**
(stable placement data for hand-placed enemies) — `UnityEngine.Random` is
never touched, world determinism is untouched.

## Patrol (optional)

`EnemyPatrolRoute` (local points authored on the instance, loop or
ping-pong, dwell per point). Selected by `EnemyNavigationProfile.RoamingMode
= Patrol`; falls back to wander when no route exists. Invalid points are
skipped cleanly (bounded, never an infinite loop). No automatic
WorldLayout-driven patrols in V1.

## Perception → pursuit pipeline

```text
out of range                        → ignored
in range, outside FOV, far         → ignored
inside ImmediateDetectionRange     → noticed even from behind (LOS still applies)
in range + FOV + line of sight     → Suspicious (stops, turns, hesitates — never moves)
still visible after SuspicionDuration
  and player within leash-hysteresis → Chasing
```

Chasing repaths at most every `ChaseRepathInterval` and only if the target
moved more than `ChaseRepathDistance`. Abandon when the **player is farther
than `MaxChaseDistanceFromHome` from HomePosition** (leash is territorial,
not enemy-relative) or when sight is lost longer than `LoseSightDelay`.
`LeashHysteresis` + `ReacquireCooldown` (after arriving home) prevent
Chasing↔ReturningHome oscillation at the boundary.

After `AttackRecovery` or `Stagger`, the enemy re-evaluates the world
(visible → Chasing, otherwise Suspicious) instead of blindly chasing.

## Return and recovery

`ReturningHome` walks back with **long-range passive reacquisition
disabled** — but it is not a free-hit window:

- a short omnidirectional `AwarenessRadius` (LOS still applies) notices a
  point-blank player → `Suspicious`;
- **taking damage always interrupts the return**
  (`TransitionReason.AttackedWhileReturning`), bypassing the reacquire
  cooldown. Within the territorial leash the response is a **bounded
  defensive chase** (`DefensiveChaseDuration`): no line of sight required
  during the window, leash always enforced, successive hits never extend an
  active window (no infinite pursuit by poking), return resumes as soon as
  the attacker retreats or the window expires. An attacker beyond the leash
  is faced from `Suspicious` (stand ground — no boundary ping-pong). The
  same damage response applies to all peaceful states (Idle, Wandering,
  Patrolling, Suspicious).

Arrival is tolerant, then the routine resumes. No teleport in the nominal
path.
Stuck detection (near-zero velocity + no displacement for `StuckTimeout`)
escalates: recompute destination → sample navigable ground ahead → `Stuck`
state → sample near home → **emergency warp only as a logged, countable,
disable-able last resort** (`AllowEmergencyWarp`).

## Configuration

`EnemyNavigationProfile` (ScriptableObject, shareable between enemy types)
referenced by `EnemyConfig.NavigationProfile`. Speeds are multipliers of
`EnemyConfig.MoveSpeed` (no contradictory duplicates); `DetectionRange`,
`FieldOfView` and `AttackRange` stay on `EnemyConfig`. A missing profile
falls back to built-in defaults (never crashes).

## Debug

- F6 combat overlay, Enemy section: state, previous, reason, time in state,
  mode (Wander/Patrol), home distance, path status, sees-player + last seen,
  recovery and emergency-warp counters.
- Gizmos on selection: territory circles, FOV cone, immediate range,
  current destination, patrol route.
- `LootboundLog`, category `EnemyBrain` (per transition, never per frame).

## V1 limits / explicitly future

Ambient population, ring difficulty, packs/aggro sharing, sound
investigation, last-known-position search, flee/cover, Behavior Tree /
Utility AI, pooling, saved enemy state, OffMeshLinks.
