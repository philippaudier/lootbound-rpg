# Lootbound

A procedural exploration RPG in first person, developed with Unity 6 and URP.

**Repository:** https://github.com/philippaudier/lootbound-rpg.git

## Vision

Lootbound is built around a core loop:

```
Refuge
-> Preparation
-> Expedition
-> Exploration
-> Encounter
-> Decision
-> Loot
-> Return
-> Repair
-> Upgrade
-> New expedition
```

The player doesn't seek to save the world. They simply go a little further than yesterday. Each return is a victory.

Objects become precious because they accompany the player, wear out, break, get repaired, and carry the history of expeditions.

> We don't explore to become more powerful.
> We explore to return with a new story.

## Development Philosophy

The project is developed through vertical slices. Each system starts as a V1:
- Simple
- Functional
- Testable
- Well-structured
- Extensible enough
- Without over-architecture

A finished, readable feature is worth more than an extremely generic architecture never used.

## Project Structure

### Assemblies

| Assembly | Description |
|----------|-------------|
| `Lootbound.Core` | Core systems (bootstrap, logging, configuration, utilities) |
| `Lootbound.Gameplay` | Gameplay systems (depends on Core) |
| `Lootbound.Debugging` | Debug tools (depends on Core) |
| `Lootbound.Tests.EditMode` | Edit mode tests |
| `Lootbound.Tests.PlayMode` | Play mode tests |

### Scenes

| Scene | Description |
|-------|-------------|
| `00_Boot` | Bootstrap scene, initializes core systems and loads gameplay |
| `10_FoundationSandbox` | Development sandbox for testing foundations |
| `11_CharacterControllerSandbox` | Character controller testing sandbox |
| `12_ProceduralTerrainSandbox` | Procedural terrain testing sandbox |
| `13_InteractionInventorySandbox` | Interaction and inventory testing sandbox |
| `14_CombatSandbox` | Combat system testing sandbox |

### Key Folders

```
Assets/Lootbound/
    Core/           # Core systems
    Gameplay/       # Gameplay systems (future slices)
    Debug/          # Debug tools
    Scenes/         # Game scenes
    Input/          # Input configuration
    Tests/          # Unit and integration tests
```

## Getting Started

1. Open the project in Unity 6 (6000.3.10+)
2. Ensure URP is properly configured
3. Open `Assets/Lootbound/Scenes/00_Boot.unity`
4. Enter Play Mode

### First-Time Setup in Unity Editor

After opening the project for the first time:

1. **Create the Game Config asset:**
   - Right-click in `Assets/Lootbound/ScriptableObjects/`
   - Select `Create > Lootbound > Game Config`
   - Name it `LootboundGameConfig`

2. **Configure the Boot scene:**
   - Open `Assets/Lootbound/Scenes/00_Boot.unity`
   - Select the `GameBootstrap` object
   - Assign the `LootboundGameConfig` asset to the `Game Config` field

3. **Add scenes to Build Settings:**
   - Open `File > Build Settings`
   - Add `00_Boot` as the first scene (index 0)
   - Add `10_FoundationSandbox` as the second scene

## Controls

### Player
| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Move | WASD / Arrows | Left Stick |
| Look | Mouse | Right Stick |
| Jump | Space | A / Cross |
| Sprint | Left Shift | Left Stick Press |
| Crouch | C (hold) | B / Circle |
| Interact | E | Y / Triangle |
| Primary Action | Left Click | X / Square |
| Secondary Action | Right Click | Left Trigger |
| Pause / Unlock Cursor | Escape | Start |

### Combat
| Action | Keyboard | Gamepad |
|--------|----------|---------|
| Attack | Left Click | X / Square |
| Dodge | Left Alt / Left Ctrl / Middle Mouse | Right Shoulder |

### Debug
| Action | Key |
|--------|-----|
| Toggle Debug Overlay | F3 |
| Toggle Movement Debug | F4 |
| Toggle Terrain Debug | F5 |
| Toggle Combat Debug | F6 |

## Current State: Slice 0.7.4 - Repair Fragments V1

### Implemented
- Project folder structure
- Assembly Definitions
- Game configuration (ScriptableObject)
- Logging system
- Bootstrap system
- Scene loading with validation
- Debug overlay (FPS, scene name, version)
- Input Actions (Player, UI, Debug maps)
- **Development scene menu at boot**
- **Pause menu with Escape in sandboxes**
- **Scene catalog (ScriptableObject)**
- **Scene reload and switching**
- Foundation Sandbox scene
- First-person character controller
- Movement (walk, sprint, crouch)
- Jump with coyote time and jump buffer
- Slope handling and step climbing
- FPS camera with mouse look
- Cursor lock/unlock
- Movement debug overlay
- Character Controller Sandbox scene
- Procedural terrain generation
- Deterministic seed-based generation
- Layered noise (macro, valleys, ridges, details)
- Spawn zone with progressive flattening
- Procedural surface painting
- Terrain debug overlay
- Procedural Terrain Sandbox scene
- **Raycast/spherecast interaction system**
- **IInteractable interface with hold-duration support**
- **Slot-based inventory system with stacking**
- **ItemDefinition ScriptableObjects with rarity**
- **UI Toolkit: interaction prompts, inventory panel, notifications**
- **World pickup objects with layer-based detection**
- **Drop functionality with partial pickup support**
- **Interaction & Inventory Sandbox scene**
- **First-person melee combat**
- **Player attack with phases (windup, active, recovery)**
- **Volumetric hit detection (SphereCast)**
- **Player dodge with invulnerability frames**
- **Player stagger on damage**
- **Enemy AI with state machine (NavMesh)**
- **Enemy telegraphed attacks**
- **Enemy line-of-sight detection**
- **Damage system (Health, DamageRequest, DamageResult)**
- **Combat feedback (hitstop, camera shake)**
- **Combat HUD (health bar, damage flash, death panel)**
- **Combat debug overlay (F6)**
- **Combat Sandbox scene**
- **Equipment Identity System (Slice 0.6)**
- **GUID-based unique equipment instances**
- **Rarity system (Common, Uncommon, Rare)**
- **Affix system with stat modifiers**
- **Generated equipment names**
- **Equipment history tracking**
- **Equipment equip/unequip from inventory**
- **Combat integration with resolved stats**
- **Equipment comparison UI**
- **Enemy weapon loot drops**
- **Equipment Condition System (Slice 0.7.1)**
- **Durability tracking (CurrentDurability, MaxDurability)**
- **Condition calculation (Excellent, Good, Worn, Fragile, Broken)**
- **Condition UI display with durability bar**
- **Condition-based colors and tooltips**
- **Weapon Wear System (Slice 0.7.2)**
- **Probabilistic wear on combat hits**
- **Heavy target additional wear**
- **Player damage wear trigger**
- **Attack ID tracking prevents double wear**
- **Condition change notifications**
- **WeaponWearConfig ScriptableObject**
- **Broken Weapons System (Slice 0.7.3)**
- **BrokenWeaponConfig ScriptableObject**
- **Severe combat penalties for broken weapons**
- **Stat resolution: Base → Affixes → Broken penalties**
- **Stats recalculated when condition changes**
- **UI broken warning badge**
- **Stats display with penalty percentages**
- **Distinctive broken notification**
- **Weapon visual feedback when broken**
- **Debug buttons: Break, Restore**
- **Repair Fragments System (Slice 0.7.4)**
- **RepairConfig ScriptableObject**
- **RepairService with preview and atomic transactions**
- **Repair UI panel in inventory**
- **Partial and full repair support**
- **Broken weapon restoration**
- **Equipment identity preserved through repair**
- **Repair debug tools (F6)**

### Not Implemented (Future Slices)
- Repair Station (3D workbench)
- Enhancement system
- Save system
- Refuge
- Stamina
- Multiple weapon types
- Parry/block
- Lock-on targeting

## Completed Slices

| Slice | Focus |
|-------|-------|
| 0.1 | Project Foundation |
| 0.2 | First-Person Character Controller V1 |
| 0.3 | Procedural Terrain V1 |
| 0.4 | Interaction & Inventory V1 |
| 0.5 | Combat V1 |
| 0.6 | Equipment & Loot Identity V1 |
| 0.7.1 | Equipment Condition System V1 |
| 0.7.2 | Weapon Wear System V1 |
| 0.7.3 | Broken Weapons V1 |
| 0.7.4 | Repair Fragments V1 |

## Upcoming Slices

| Slice | Focus |
|-------|-------|
| 0.7.5 | Repair Station V1 |
| 0.8 | Refuge V1 |

## Character Controller

### Components

| Component | Responsibility |
|-----------|----------------|
| `PlayerInputReader` | Reads Unity Input System, exposes movement intentions |
| `FirstPersonMotor` | Calculates and applies movement via CharacterController |
| `PlayerCameraController` | Handles FPS camera rotation (yaw on body, pitch on camera) |
| `PlayerStanceController` | Manages crouching with smooth height transitions |
| `PlayerMovementDebug` | Displays movement state (toggle with F4) |

### Configuration

Movement parameters are configured via `PlayerMovementConfig` ScriptableObject:
- Located at `Assets/Lootbound/ScriptableObjects/Player/DefaultPlayerMovementConfig.asset`
- All values exposed in Inspector for tuning

### Prefab

Player prefab: `Assets/Lootbound/Prefabs/Player/PlayerCharacter.prefab`

Structure:
```
PlayerCharacter
├── CharacterController
├── PlayerInputReader
├── FirstPersonMotor
├── PlayerStanceController
├── PlayerMovementDebug
└── CameraRoot
    └── Camera + PlayerCameraController
```

### V1 Limitations

Not implemented in V1:
- Stamina
- Dodge/Dash
- Slide
- Wall-run
- Climbing
- Swimming
- Head bob
- Footsteps audio
- Visible body/hands

## Procedural Terrain

### Components

| Component | Responsibility |
|-----------|----------------|
| `TerrainGenerationConfig` | ScriptableObject with all generation parameters |
| `ProceduralTerrainGenerator` | Orchestrates generation and applies to Unity Terrain |
| `TerrainHeightGenerator` | Generates heightmap using layered noise |
| `TerrainSpawnPlanner` | Finds valid spawn and flattens the area |
| `TerrainSurfacePainter` | Paints texture layers based on height/slope |
| `TerrainGenerationDebug` | Debug overlay (toggle with F5) |

### Configuration

Terrain parameters are configured via `TerrainGenerationConfig` ScriptableObject:
- Located at `Assets/Lootbound/ScriptableObjects/World/DefaultTerrainGenerationConfig.asset`
- All values exposed in Inspector for tuning

### Generation Pipeline

```
Seed → Offsets → Macro → Valleys → Ridges → Details → Remap
→ Spawn Search → Flatten → Slopes → Apply → Paint → Place Player
```

### V1 Terrain Limitations

Not implemented in V1:
- Infinite terrain / streaming
- Runtime chunk loading
- GPU generation
- Caves / tunnels
- Rivers / water
- Erosion simulation
- Full biome system
- Vegetation
- Points of interest

See `Docs/PROCEDURAL_TERRAIN.md` for detailed documentation.

## Interaction & Inventory

### Components

| Component | Responsibility |
|-----------|----------------|
| `IInteractable` | Interface for interactable objects |
| `PlayerInteractor` | Raycast/spherecast detection and interaction handling |
| `Inventory` | Slot-based container with stacking and events |
| `PlayerInventory` | Player-attached inventory component |
| `ItemDefinition` | ScriptableObject item template |
| `ItemInstance` | Runtime item with mutable quantity |
| `ItemWorldPickup` | IInteractable pickup in world |
| `InventoryUI` | UI Toolkit inventory panel |
| `InteractionPromptUI` | UI Toolkit interaction prompts |
| `NotificationUI` | UI Toolkit pickup notifications |

### Configuration

- `InteractionConfig` - Raycast settings, layer mask, prompt distances
- `InventoryConfig` - Slot count, grid layout
- `ItemDefinition` - Item ID, display name, icon, rarity, stacking

### V1 Limitations

Not implemented in V1:
- Equipment slots
- Item use/consume
- Item combining/crafting
- Drag-and-drop rearrangement
- Item sorting
- Quick-use hotbar
- Container/chest interaction
- Item durability
- Save/load inventory

See `Docs/INTERACTION_INVENTORY.md` for detailed documentation.

## Combat

### Components

| Component | Responsibility |
|-----------|----------------|
| `PlayerHealth` | Player health with IDamageable interface |
| `PlayerDodge` | Dodge with invulnerability frames |
| `PlayerMeleeWeapon` | Attack phases and hit detection |
| `PlayerCombatController` | Coordinates input and combat actions |
| `PlayerStagger` | Brief knockback when hit |
| `MeleeHitDetector` | SphereCast detection with wall blocking |
| `EnemyBrain` | NavMesh-based state machine AI |
| `EnemyCombat` | Enemy attack hit detection |
| `EnemyHealth` | Enemy health with loot spawning |
| `CombatFeedback` | Hitstop effect |
| `PlayerCameraShake` | Camera shake on hit |

### Configuration

- `MeleeAttackConfig` - Attack timing, damage, range
- `EnemyConfig` - Enemy stats, behavior, loot

### V1 Limitations

Not implemented in V1:
- Equipment integration
- Weapon switching
- Heavy attacks / combos
- Parry / block
- Lock-on
- Stamina
- Multiple enemy types
- Audio / particles

See `Docs/COMBAT.md` for detailed documentation.

## Technical Notes

- **Unity Version:** 6000.3.10f1
- **Render Pipeline:** URP 17.3.0
- **Input System:** New Input System 1.18.0
- **Target Platform:** Windows (primary), with potential for other platforms
- **Multiplayer:** None (solo game)
