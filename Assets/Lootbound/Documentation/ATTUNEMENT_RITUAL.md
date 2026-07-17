# Attunement Ritual System

Slice 0.8.7 — Attunement Ritual & Polish V1

## Overview

The Attunement Ritual system transforms the attunement attempt from an instant transaction into a memorable "ritual" experience. The ritual provides visual, audio, and timing feedback while the actual mechanics (success chance, protection, costs) remain unchanged.

**Key Principle**: The transaction is resolved BEFORE the ritual starts. The ritual only presents a pre-determined result through a timed presentation.

## Architecture

### Components

```
AttunementRitualState.cs      - Enum defining ritual phases
AttunementRitualConfig.cs     - ScriptableObject configuration
AttunementRitualController.cs - State machine and presentation
AttunementTableUI.cs          - Integration with ritual controller
```

### State Machine

```
Idle → Preparing → Building → Resolving → ShowingResult → Idle
         ↓                                       ↓
      Cancelling ←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←←
```

| Phase | Default Duration | Purpose |
|-------|-----------------|---------|
| Idle | - | No ritual active |
| Preparing | 0.2s | Initial activation, weapon appears |
| Building | 0.5s | Tension rises, emission intensifies |
| Resolving | 0.4s | Peak moment, anticipation |
| ShowingResult | 0.4s | Success/failure revealed |
| Cancelling | instant | Clean-up on cancel |

**Total ritual duration**: ~1.5s (configurable via AttunementRitualConfig)

## Transaction Authority

The ritual is **purely presentational**. The actual attunement logic follows this flow:

1. Player clicks "Deepen Attunement"
2. `AttunementService.TryAttune()` is called immediately
3. Result (success/failure) is stored
4. Ritual starts with the pre-determined result
5. Visual/audio feedback plays
6. When ritual completes, result is revealed to player

This ensures:
- No desync between visual and actual state
- Player can safely close UI during ritual (result already applied)
- Network-ready architecture (if multiplayer is added later)

## Visual Feedback

### Table Emission

Uses `MaterialPropertyBlock` to avoid modifying shared materials.

| Phase | Emission Behavior |
|-------|-------------------|
| Preparing | Fade in from black to base intensity |
| Building | Ramp up to peak intensity |
| Resolving | Peak intensity with subtle pulse |
| ShowingResult (success) | Flash to success color, then fade |
| ShowingResult (failure) | Fade to muted color |

**Emission Property**: `_EmissionColor`

### Weapon Display

The selected weapon can be visually displayed on the table during the ritual:
- Spawned at weapon anchor with configurable height offset
- Subtle float animation (sine wave)
- Slow rotation around vertical axis
- Destroyed when ritual completes or is cancelled

## Audio Feedback

All audio clips are optional. Missing clips log a placeholder message (no error).

| Event | Clip Field | Default Volume |
|-------|------------|----------------|
| Ritual starts | `ritualStartClip` | Master volume |
| Building phase | `ritualBuildClip` (loops) | Master volume |
| Success | `successClip` | Master volume |
| Guaranteed success | `guaranteedSuccessClip` | Master volume |
| Failure | `failureClip` | Master volume |

**Pitch Variation**: Configurable range (default 0.95-1.05) for subtle randomization.

## Configuration

Create via: **Right-click > Create > Lootbound > Equipment > Attunement Ritual Config**

### Phase Durations
- `preparingDuration`: 0.05-0.5s (default 0.2s)
- `buildingDuration`: 0.2-1.0s (default 0.5s)
- `resolvingDuration`: 0.2-0.8s (default 0.4s)
- `showingResultDuration`: 0.2-0.8s (default 0.4s)

### Visual Settings
- `baseEmissionIntensity`: 0.1 (rest state)
- `peakEmissionIntensity`: 1.5 (building/resolving)
- `successFlashIntensity`: 2.5 (success flash)
- `emissionColor`: Warm gold (default)
- `successEmissionColor`: Soft green
- `failureEmissionColor`: Muted gray

### Weapon Display
- `weaponHeightOffset`: Height above anchor (default 0.05)
- `weaponFloatAmplitude`: Float animation range (default 0.02)
- `weaponRotationSpeed`: Degrees per second (default 15)

### Stone Display
- `stonePrefab`: Visual prefab for the attunement stone (assign P_AttunementStone.prefab)
- `stoneHeightOffset`: 0.1
- `stoneFloatAmplitude`: 0.03
- `stoneRotationSpeed`: 30

### Camera (optional, not implemented in V1)
- `enableCameraFocus`: Enable FOV reduction
- `fovReduction`: Degrees to reduce (default 5)
- `cameraTransitionDuration`: Transition time (default 0.25s)
- `enableSuccessShake`: Enable camera shake on success
- `successShakeIntensity`: Shake amount (default 0.02)
- `successShakeDuration`: Shake duration (default 0.15s)

## Integration

### AttunementTableUI Setup

1. Add `AttunementRitualController` to a GameObject (can be same as table or separate)
2. Configure the ritual controller:
   - Assign `AttunementRitualConfig` asset
   - Assign `weaponAnchor` and `stoneAnchor` transforms (from AttunementTable)
   - Assign `tableRenderer` for emission effects
   - AudioSource is auto-created if not assigned
3. In AttunementTableUI, assign the ritual controller reference

### Event Flow

```csharp
// UI calls
ritualController.StartRitual(result.Success, result.WasGuaranteed,
    prevLevel, newLevel, weaponPrefab);

// Controller raises when complete
ritualController.OnRitualComplete += (success, guaranteed, prev, curr) => {
    // Show result panel
};

// Or if cancelled
ritualController.OnRitualCancelled += () => {
    // Clean up UI state
};
```

## Testing

### Editor Testing

The `AttunementRitualController` has context menu items:
- **Test Ritual (Success)**: Simulates +0 → +1 success
- **Test Ritual (Failure)**: Simulates +3 → +3 failure
- **Test Ritual (Guaranteed)**: Simulates +4 → +5 guaranteed success

### Unit Tests

See `AttunementRitualTests.cs`:
- State transition tests
- Cancel behavior
- Progress calculations
- Config validation
- Event callback tests

## UI Blocking During Ritual

During the ritual sequence:
- **Escape key** is blocked (cannot close UI)
- **Close button** is blocked
- **Weapon selection** is blocked (cannot change selected equipment)
- **Attune button** is blocked (no double-clicks)

This ensures the ritual completes properly and the result is shown. The transaction is already resolved, so there's no risk of lost progress.

## V1 Limitations

- Camera focus (FOV reduction) not implemented
- No particle effects
- No screen shake on success (config exists but not wired)
- Looping audio for building phase may need volume fade-out

## Future Enhancements (V2+)

- Camera focus with FOV reduction
- Screen shake on success (using PlayerCameraShake)
- Particle effects (energy gathering, success burst)
- Stone visual dissolving/consuming animation on success
- Different ritual visuals per rarity or weapon type
- Success/failure specific weapon animations (glow, dim)
