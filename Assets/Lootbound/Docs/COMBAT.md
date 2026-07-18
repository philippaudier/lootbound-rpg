# Combat System (Slice 0.5)

This document describes the combat system implemented in Slice 0.5.

> Since slice 0.9.6, enemy navigation behaviours (wander, patrol, suspicion,
> chase leash, return home) are documented in `ENEMY_NAVIGATION.md`. The
> `EnemyState` machine now covers both navigation and combat states.

## Overview

The combat system provides first-person melee combat with:
- Player attacks with phases (windup, active, recovery)
- Volumetric hit detection (SphereCast)
- Dodge with invulnerability frames
- Enemy AI with telegraphed attacks
- Damage system with stagger
- Combat feedback (hitstop, camera shake)

## Architecture

```
Combat/
├── Damage/
│   ├── IDamageable          - Interface for damageable entities
│   ├── DamageRequest        - Immutable damage data
│   ├── DamageResult         - Immutable result data
│   └── Health               - Pure C# health logic
├── Player/
│   ├── PlayerHealth         - Player IDamageable implementation
│   ├── PlayerDodge          - Dodge with i-frames
│   ├── PlayerMeleeWeapon    - Attack phases and timing
│   ├── PlayerCombatController - Coordinates input and actions
│   └── PlayerStagger        - Brief knockback when hit
├── Melee/
│   ├── MeleeAttackConfig    - ScriptableObject for attack parameters
│   ├── AttackPhase          - Enum (Ready, Windup, Active, Recovery)
│   └── MeleeHitDetector     - SphereCast detection with HashSet tracking
├── Enemy/
│   ├── EnemyConfig          - ScriptableObject for enemy parameters
│   ├── EnemyState           - Enum (Idle, Chase, AttackWindup, etc.)
│   ├── EnemyHealth          - Enemy IDamageable with loot spawning
│   ├── EnemyBrain           - NavMeshAgent state machine AI
│   └── EnemyCombat          - Enemy attack hit detection
└── Feedback/
    ├── CombatFeedback       - Hitstop coordination
    └── PlayerCameraShake    - Camera shake on hit
```

## Damage System

### DamageRequest

Immutable struct containing damage information:

```csharp
public readonly struct DamageRequest
{
    public readonly GameObject Source;
    public readonly float Amount;
    public readonly Vector3 HitPoint;
    public readonly Vector3 HitDirection;
    public readonly float StaggerForce;
}
```

### DamageResult

Immutable struct with factory methods:

```csharp
DamageResult.Success(damageDealt, wasFatal);
DamageResult.Blocked();      // For i-frames
DamageResult.NotApplied();   // For invalid requests
```

### IDamageable

Interface implemented by PlayerHealth and EnemyHealth:

```csharp
public interface IDamageable
{
    bool IsDead { get; }
    DamageResult TakeDamage(DamageRequest request);
}
```

### Health

Pure C# class for health logic (not MonoBehaviour):

- Prevents negative health
- Prevents damage after death
- Fires events: `OnHealthChanged`, `OnDamaged`, `OnDied`
- Death is idempotent

## Player Combat

### PlayerMeleeWeapon

Manages attack phases with configurable timing:

| Phase | Description |
|-------|-------------|
| Ready | Can start new attack |
| Windup | Brief pause before damage |
| Active | Damage window - SphereCast active |
| Recovery | Cooldown before next attack |

Configuration via `MeleeAttackConfig` ScriptableObject.

### PlayerDodge

Short dash with invulnerability frames:

- Direction based on movement input (or backward if no input)
- Uses CharacterController.Move for wall collision
- Configurable i-frame window within dodge duration
- Cooldown prevents spam

### PlayerStagger

Applied when player takes damage:

- Brief knockback push via CharacterController
- Movement speed reduction during stagger
- Subscribes to `PlayerHealth.OnDamaged`

### MeleeHitDetector

Volumetric attack detection:

- SphereCast during active window
- HashSet prevents hitting same target twice per attack
- Line-of-sight raycast prevents attacks through walls
- Configurable target and obstacle layer masks

## Enemy System

### EnemyBrain

NavMeshAgent-based state machine:

| State | Behavior |
|-------|----------|
| Idle | Waits, checks CanSeeTarget() |
| Chase | Moves toward player, stops at attack range |
| AttackWindup | Telegraph phase - face player, no movement |
| AttackActive | Damage window |
| AttackRecovery | Brief pause after attack |
| Stagger | Interrupted by player hit |
| Dead | Disables NavMeshAgent |

Detection includes:
- Distance check
- Field of view check
- Line-of-sight raycast (sees around corners fixed)

### EnemyCombat

Attack hit detection during AttackActive state:

- SphereCast toward player
- Single hit per attack
- Respects player i-frames

### EnemyHealth

IDamageable with loot spawning:

- On death, spawns ItemWorldPickup
- Uses EnemyConfig for loot configuration
- Fires `OnStagger` and `OnDied` events

## Combat Feedback

### CombatFeedback

Coordinates hitstop effect:

- Brief Time.timeScale reduction on hit
- Configurable duration and scale

### PlayerCameraShake

Camera shake on both dealing and receiving damage:

- Perlin noise-based shake
- Configurable intensity and duration
- Does not use full rotation to avoid nausea

## Configuration

### MeleeAttackConfig

ScriptableObject for player attack:

| Parameter | Description |
|-----------|-------------|
| Damage | Damage per hit |
| ActiveWindowStart | When damage window begins |
| ActiveWindowEnd | When damage window ends |
| TotalDuration | Full attack duration |
| Range | Attack reach |
| TraceRadius | SphereCast radius |
| StaggerForce | Force applied to enemies |

### EnemyConfig

ScriptableObject for enemy behavior:

| Parameter | Description |
|-----------|-------------|
| MaxHealth | Starting health |
| DetectionRange | Distance to detect player |
| AttackRange | Distance to start attack |
| FieldOfView | Detection cone angle |
| MoveSpeed | Chase speed |
| AttackWindup | Telegraph duration |
| AttackActive | Damage window duration |
| AttackRecovery | Post-attack pause |
| AttackCooldown | Time between attacks |
| StaggerDuration | How long stagger lasts |
| LootItems | Items to drop on death |

## Input

Uses existing Input System actions:

| Action | Binding | Purpose |
|--------|---------|---------|
| Attack (PrimaryAction) | Left Click | Start attack |
| Dodge | Left Alt, Left Ctrl, Middle Mouse | Dodge roll |

Combat input blocked when:
- Player is dead
- Inventory is open (via InputReader.inputEnabled)

## Layers

Required physics layers:

| Layer | Purpose |
|-------|---------|
| Player | Player character |
| Enemy | Enemy characters |
| Default | Walls/obstacles for line-of-sight |

Configure in PlayerMeleeWeapon:
- Target Layers: Enemy
- Obstacle Layers: Default

## UI

### CombatHUD (UI Toolkit)

- Health bar with low-health visual
- Damage flash overlay
- Death panel with restart prompt (R key)
- Optional enemy health tracking

### CombatDebugOverlay (OnGUI)

Toggle with F6:
- Player health and state
- Attack phase and timer
- Dodge state and cooldown
- Enemy state and distance

## Tests

EditMode tests in `CombatTests.cs`:

- Health initialization and damage
- Death triggers once
- Damage blocked when dead
- Heal clamping
- DamageRequest validation
- DamageResult factory methods
- Invulnerability blocking
- HashSet hit tracking
- Dodge timing calculations
- Attack phase timing

## Scene Setup

### Combat Sandbox (14_CombatSandbox)

Required setup:

1. Ground with NavMesh baked
2. Player with combat components
3. Enemy prefab with EnemyBrain, EnemyCombat, EnemyHealth
4. CombatHUD UIDocument
5. CombatDebugOverlay

### Player Prefab Additions

```
PlayerCharacter
├── ... (existing components)
├── PlayerHealth
├── PlayerDodge
├── PlayerStagger
├── PlayerCombatController
├── CombatFeedback
└── Camera
    ├── ... (existing)
    ├── PlayerCameraShake
    └── PlayerMeleeWeapon
```

## V1 Limitations

Not implemented:

- Equipment system integration
- Weapon switching
- Heavy attacks / combos
- Parry / block
- Lock-on targeting
- Stamina
- Multiple enemy types
- Boss behaviors
- Audio feedback
- Particle effects
- Animation integration

## Next Steps

Slice 0.6 will add:
- Equipment slots
- Weapon as equippable item
- Loot identity and stats
