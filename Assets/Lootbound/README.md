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

### Debug
| Action | Key |
|--------|-----|
| Toggle Debug Overlay | F3 |
| Toggle Movement Debug | F4 |
| Toggle Terrain Debug | F5 |

## Current State: Slice 0.3 - Procedural Terrain V1

### Implemented
- Project folder structure
- Assembly Definitions
- Game configuration (ScriptableObject)
- Logging system
- Bootstrap system
- Scene loading
- Debug overlay (FPS, scene name, version)
- Input Actions (Player, UI, Debug maps)
- Boot scene with auto-load
- Foundation Sandbox scene
- First-person character controller
- Movement (walk, sprint, crouch)
- Jump with coyote time and jump buffer
- Slope handling and step climbing
- FPS camera with mouse look
- Cursor lock/unlock
- Movement debug overlay
- Character Controller Sandbox scene
- **Procedural terrain generation**
- **Deterministic seed-based generation**
- **Layered noise (macro, valleys, ridges, details)**
- **Spawn zone with progressive flattening**
- **Procedural surface painting**
- **Terrain debug overlay**
- **Procedural Terrain Sandbox scene**

### Not Implemented (Future Slices)
- Combat system
- Enemies and AI
- Inventory
- Loot system
- Equipment durability
- Enhancement system
- Save system
- Refuge
- Stamina
- Dodge/Dash

## Upcoming Slices

| Slice | Focus |
|-------|-------|
| 0.4 | Basic interaction system |
| 0.5 | Inventory V1 |
| 0.6 | Simple combat V1 |

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

## Technical Notes

- **Unity Version:** 6000.3.10f1
- **Render Pipeline:** URP 17.3.0
- **Input System:** New Input System 1.18.0
- **Target Platform:** Windows (primary), with potential for other platforms
- **Multiplayer:** None (solo game)
