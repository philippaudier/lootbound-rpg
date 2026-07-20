# World Ambience (slices 0.9.9 / 0.9.9.1 / 0.9.9.2 / 0.9.9.3 / 0.9.9.4)

## Purpose

The world already *knows* how oppressive each position should feel: since
slice 0.9.7, `WorldRingContext` carries normalized ambience intents
(`FogDensity01`, `LightAttenuation01`, `Saturation01`, `Temperature01`).
This slice makes those intents **visible**: fog thickens, light dims, color
desaturates and cools as the player walks away from the Refuge — and the
Refuge itself keeps *exactly* the visual identity it had before this system
existed.

Danger is communicated by the atmosphere, not by UI labels.

## Architecture

```text
WorldProgression.GetContext(player position)        (authority, slice 0.9.7)
        │  intents: FogDensity01 / LightAttenuation01 / Saturation01 / Temperature01
        ▼
WorldAmbienceEvaluator (pure static)
        │  + WorldAmbienceConfig (translation scalars, SO)
        │  + WorldAmbienceBaseline (captured scene values)
        ▼
WorldAmbienceState (immutable engine values)
        │  smoothed per frame: factor = 1 - exp(-speed * dt)
        ▼
WorldAmbienceController (MonoBehaviour, Lootbound.Gameplay)
        ▼
WorldAmbienceApplierBase (abstract boundary, Lootbound.Gameplay)
        ▼
PBSkyWorldAmbienceApplier (Lootbound.Rendering.PBSky assembly)
```

- **Intent vs translation**: `WorldProgressionConfig` stays the *intent*
  (curves by depth). `WorldAmbienceConfig` only maps normalized intents to
  engine ranges; it is never a competing progression curve.
- **Assembly boundary**: `Lootbound.Gameplay` never references the
  third-party sky package. The concrete applier lives in a small
  `Lootbound.Rendering.PBSky` assembly (references `PBSkyURP` + URP).
- **Pure evaluator**: `Evaluate(context, config, baseline)` has no time, no
  scene, no rendering — fully covered by EditMode tests.

## Rendering integration (PBSky)

The scene fog is PBSkyURP's volumetric `Fog` VolumeComponent on the existing
Global Volume (priority 0); native Unity fog is off. The applier:

- **reads** the fog baseline from `globalVolume.sharedProfile` (never writes
  the shared asset);
- creates one runtime Volume `WorldAmbience_RuntimeVolume` (global,
  priority 10, in-memory profile) owning **only** the parameters this system
  drives:
  - PBSky `Fog`: `meanFreePath` + `tint` (`maxFogDistance` only when
    `controlMaxFogDistance` is enabled — off by default in V1);
  - URP `ColorAdjustments`: `saturation` + `contrast`;
  - URP `WhiteBalance`: `temperature`.
  `enabled`, `colorMode`, `baseHeight`, the sky and the clouds stay inherited
  from the Global Volume.
- drives the directional light and `RenderSettings.ambientIntensity` as
  **baseline × multiplier** (day/night friendly);
- `Restore()` destroys the runtime volume and writes the captured light
  values back exactly (also on disable / destroy).

`RefreshLightingBaseline()` is the documented hook for a future day/night
cycle: re-capture (or inject) the base light before applying multipliers.

## Default translation (composition intent × translation)

The default intent curves peak below 1 at the disc edge (attenuation 0.5,
saturation 0.6, temperature 0.25), so the *visual* endpoints at Edgelands
come from composing both layers:

| Value | Refuge (depth 0) | Edgelands (depth 1) | Available extreme |
|---|---|---|---|
| Fog mean free path | captured baseline (~400 m) | ~160 m | 140 m (config floor via inspector) |
| Directional light | ×1.00 | ×0.85 | ×0.70 (full intent) |
| Ambient intensity | ×1.00 | ×0.80 | ×0.60 (full intent) |
| Saturation | 0 | −12 | −30 (full intent) |
| Temperature | ≈ −0.7 (imperceptible) | ≈ −4.75 | −7 / +2 |
| Contrast | 0 | +4 | toggleable off |
| Fog tint | exact baseline tint | 50 % drift toward cool grey gradient | influence 0..1 |

Beyond the disc, `Depth01` is already clamped to 1 by `WorldProgression`:
the Void inherits the Edgelands maximum without extrapolation.

## Sky extension (slice 0.9.9.1)

The same runtime volume also carries a `PhysicallyBasedSky` block driving
ONLY the component's artistic overrides — never a parameter included in
`GetPrecomputationHashCode`, so no atmosphere LUT rebuild is ever
triggered:

| Value | Refuge (depth 0) | Edgelands (depth 1) |
|---|---|---|
| `zenithTint` | exact baseline | 35 % drift toward (0.94, 0.96, 1.00) |
| `horizonTint` | exact baseline | 50 % drift toward (0.90, 0.92, 0.95) |
| `colorSaturation` | baseline | toward 0.90 floor (via `Saturation01`), never above baseline |
| `exposure` | **not driven** (default) | opt-in: baseline −0.20 EV via `LightAttenuation01` |

- When `controlSkyExposure` is off (default), the exposure override is
  **absent** from the runtime volume — the global profile stays fully in
  charge. The toggle exists because sky dimming composes with the ambient
  multiplier.
- If the global profile has no `PhysicallyBasedSky`, the sky is simply not
  driven (clean fallback, same as the fog).
- Clouds, weather, sun rotation and all physical/precomputation sky
  parameters stay strictly untouched.
- F7 shows the driven sky values (baseline / current → target, and
  "Exposure: off (global profile)" when uncontrolled).

## Ambient event foundation (slice 0.9.9.2)

> Sound belongs to the world. It never follows the player's head.

`WorldAmbienceState` carries four activity intents (0..1), evaluated by
depth like every other value and smoothed identically:

| Activity | Refuge (depth 0) | Edgelands (depth 1) |
|---|---|---|
| `BirdActivity` | 1.00 | 0.05 |
| `InsectActivity` | 1.00 | 0.00 |
| `WindActivity` | 0.20 | 0.85 |
| `RareEventActivity` | 0.02 | 0.30 |

An activity drives event **frequency and eligibility** — never a final
audio gain. Real loudness will come from 3D sources, distance and rolloff
in a future audio slice.

`AmbientEventDirector` (`Gameplay/World/Ambience/Events/`) turns these
intents into temporary, world-anchored events around the player:

- **Profiles** (`AmbientEventProfile` SO): id, category (Birds / Insects /
  Wind / Environmental / Rare — Environmental shares the rare intent),
  weight, activity response curve, cooldown / lifetime / distance /
  height-offset ranges, max concurrent, avoid-player-view flag.
- **Selection formula** (applied exactly once, documented in
  `AmbientEventSelector`): `effectiveWeight = weight ×
  clamp01(activityResponse(activity))`; one bounded attempt roll per
  evaluation (`baseChance × min(1, Σ effectiveWeights)`), then a
  proportional weighted pick.
- **Placement** (`AmbientEventPlacement`): area-uniform radius
  (`sqrt(lerp(min², max², u))`), uniform angle (or the arc outside a ±35°
  frontal cone), ground via `SampleHeightAtWorld` (fallback: player
  height) plus the profile's height offset.
- **Cadence**: configurable interval (never per frame), at most one spawn
  per tick, missed time discarded (no catch-up bursts), global spawn
  spacing, per-profile cooldowns, per-profile and global concurrency caps.
  No coroutines — expiration is a time comparison in `Update`.
- **Lifecycle**: a spawned instance is a bare marker GameObject under
  `AmbientEvents_Active` plus a pure `AmbientEventInstance` record.
  `OnEventSpawned` / `OnEventReleased` expose instances to future
  presentation layers (audio, visuals). Disable releases everything —
  no orphans, no dead registry entries.
- **Debug**: F7 "Ambient Events" section (activities, active instances,
  last spawn, next evaluation, cooldowns, rejection reasons) and optional
  gizmos (rings, per-category colored spheres, lines to the player,
  remaining lifetime) — editor-only.

The Gameplay assembly references no AudioSource, AudioClip, Shader or
PBSky type anywhere in this system.

## Spatial bird audio (slice 0.9.9.3)

`Lootbound.Presentation.Audio` (`Assets/Lootbound/Presentation/Audio/`) is
the first presentation layer consuming the director's events — same
decoupling pattern as `Lootbound.Rendering.PBSky`; Gameplay never
references it.

- `AmbientAudioPresenter` subscribes to `OnEventSpawned` /
  `OnEventReleased`. For each **Birds** event it creates a runtime child
  `BirdAudioPresentation` under the marker, carrying a 3D `AudioSource`.
  Ownership: the director owns the marker; the presenter owns only its
  audio child.
- `BirdAudioLibrary` (SO): clips (null entries ignored), pitch 0.95–1.05,
  volume 0.90–1.00, minDistance 8, maxDistance 45, logarithmic rolloff,
  spatialBlend 1, priority 128 — all defensively bounded. Source defaults:
  loop off, playOnAwake off, doppler 0, spread 0.
- Randomness: a private seedable `System.Random` (clip, pitch, volume) —
  the global `UnityEngine.Random` is never touched. The same blackbird
  never sings exactly the same.
- No valid clip (null/empty/all-null library) → no AudioSource is created.
- Release → stop + destroy the audio child; presenter disable → all audio
  children destroyed, markers intact; re-enable → missing presentations
  recreated exactly once for still-active instances.
- Distance attenuation, panning and multiple simultaneous birds come free
  from Unity's 3D audio — no code follows the player's head.

Asset: `ScriptableObjects/Audio/DefaultBirdAudioLibrary.asset` ships with
an **empty clip list** — import bird recordings and fill it to hear the
world.

## Ambient wildlife (slice 0.9.9.4)

`Lootbound.Presentation.Wildlife` (`Assets/Lootbound/Presentation/Wildlife/`)
is the first VISUAL presentation of the ambient events - same decoupling
as audio and PBSky: Gameplay never references it; both presenters observe
the same Birds events independently (no reference between the Audio and
Wildlife assemblies; the shared event and seed keep them roughly in sync).

- `AmbientWildlifePresenter` subscribes to the director. Each Birds event
  produces at most ONE small flock (2-6 birds, configurable) flying a
  quadratic-Bezier path around the event position: **Crossing**, **Rising**
  or **Circling**. Flock roots live under `AmbientWildlife_Active` on the
  presenter (never under the director's markers).
- **Determinism**: `BirdFlockPlanner` is pure. The seed is FNV-1a over the
  event id, quantized position and spawn time; the same seed reproduces
  the variant, bird count, trajectory, duration, formation offsets,
  phases and scales. `UnityEngine.Random` is never touched. Camera
  avoidance is a global path translation applied AFTER planning.
- **Lifecycle**: event released -> flock released. Flight finished before
  the event -> flock released early (the birds flew away) and the
  instance is tombstoned so rescans and re-enables never replay it; the
  tombstone is cleared when the event is released. No duplicates, no
  leaks; `Release()` is idempotent.
- **Movement**: one central `Tick` per presenter (no per-bird Update),
  progression = elapsed/duration per bird (framerate independent), loose
  formation (irregular offsets, staggered starts, near-but-different
  speeds), vertical bob, orientation along the flight direction.
- **Library**: `BirdVisualLibrary` (variants: prefab, weight, scale and
  flap ranges; flock: group size, duration, radius, heights, bob). Null
  prefabs and invalid weights are ignored. `DefaultBirdVisualLibrary.asset`
  (`ScriptableObjects/Wildlife/`) ships EMPTY: add a light bird prefab to
  see flights. With no valid variant the presenter shows nothing - unless
  `EnableDevelopmentFallback` (OFF by default) is checked, which allows a
  tiny quad silhouette sharing one presenter-owned material (a dev tool,
  never a silent data fix; abandoned cleanly if no compatible shader).
- **Pooling decision**: instantiate/destroy in V1 - spawn frequency is a
  handful of short flights; `Release()` is the single seam where a pool
  could later be inserted.
- Deliberate V1 limits: birds only, no other species, no NavMesh, no
  interactions, no persistence, no LoD/occlusion, flights do not loop.

Scene setup: add `AmbientWildlifePresenter`, assign `eventDirector`,
`birdLibrary` (+ optional `terrainGenerator` for minimum height and
`cameraTransform` for spawn avoidance), then drop bird prefabs into the
library.

## Timing

- Context evaluated at the player position every `evaluationInterval`
  (default 0.2 s); applied every frame through framerate-independent
  smoothing (`transitionSpeed` 0.4 → converges over several seconds of
  walking, no visible jumps, no snap on regeneration).
- Regeneration needs no events: the progression is read live from the
  published generation context; smoothing carries the visuals continuously
  into the new world.

## Debug (F7)

The `WorldProgressionDebugPanel` shows a **World Ambience** section when its
`ambienceController` reference is assigned: applier status, `Actual
Depth01`, intents, current → target values, and a **Preview Depth
Override** (slider + presets 0 / .25 / .5 / .75 / 1 + Off). The preview
forces only the *visual* context (`PREVIEW ACTIVE` banner); WorldProgression
and gameplay are never altered.

## Scene setup

1. Add `WorldAmbienceController` + `PBSkyWorldAmbienceApplier` (same
   GameObject is fine).
2. Controller: assign `terrainGenerator`, `player`, `applier`, and
   `DefaultWorldAmbienceConfig`
   (`Assets/Lootbound/ScriptableObjects/World/`).
3. Applier: assign the existing **Global Volume** and the **Directional
   Light**.
4. Optional: assign `ambienceController` on the F7 debug panel.

## Exclusions (V1)

- No vignette.
- No `maxFogDistance` control by default (toggle exists to isolate
  variables if distant silhouettes stay too crisp).
- No sky/cloud parameter driving (`PhysicallyBasedSky`, `VolumetricClouds`
  untouched).
- No day/night cycle (only the `RefreshLightingBaseline` hook).
- No audio ambience, no post-effects beyond saturation/contrast/temperature.
