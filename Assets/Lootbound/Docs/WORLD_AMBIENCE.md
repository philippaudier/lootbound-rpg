# World Ambience (slices 0.9.9 / 0.9.9.1)

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
