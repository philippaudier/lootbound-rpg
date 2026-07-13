# Lootbound Architecture

This document describes the architecture actually implemented in the project.

## Overview

Lootbound uses a simple, explicit architecture without global service locators or complex dependency injection frameworks. Dependencies are visible and passed explicitly through serialized fields or constructor parameters.

## Assembly Structure

```
Lootbound.Core          (no dependencies on other Lootbound assemblies)
    |
    v
Lootbound.Gameplay      (depends on Core)
    |
    v
Lootbound.Debugging     (depends on Core)
```

Test assemblies:
- `Lootbound.Tests.EditMode` - Editor-only tests
- `Lootbound.Tests.PlayMode` - Runtime tests

## Core Systems

### Bootstrap (`Lootbound.Core.Bootstrap`)

The `GameBootstrap` component handles game initialization:

```csharp
public class GameBootstrap : MonoBehaviour
```

Responsibilities:
- Initialize logging with configured log level
- Mark game as bootstrapped
- Load the default gameplay scene
- Ensure single instance (destroys duplicates)
- Persist across scene loads (`DontDestroyOnLoad`)

The bootstrap is designed to be the single entry point for the game. The `00_Boot` scene should be first in the build order.

If a `developmentMenuPrefab` is assigned, the bootstrap opens a scene selection menu instead of auto-loading a scene.

### Development Menu (`Lootbound.Core.Scenes` + `Lootbound.UI`)

The development menu system provides scene selection and pause functionality during development.

```
DevelopmentSceneEntry     - Serializable scene entry data
DevelopmentSceneCatalog   - ScriptableObject listing dev scenes
DevelopmentMenuController - UI controller (persists with bootstrap)
```

Scene catalog configuration:
- `DisplayName` - Name shown in menu
- `SceneName` - Exact scene name (must match Build Settings)
- `Description` - Shown when selected
- `Visible` - Whether to show in menu

SceneLoader enhancements:
- `CanLoadScene(string)` - Validates scene exists
- `IsLoading` - Prevents concurrent loads

This is a development tool, not the final game menu.

### Configuration (`Lootbound.Core.Configuration`)

The `LootboundGameConfig` ScriptableObject holds central game configuration:

```csharp
[CreateAssetMenu(fileName = "LootboundGameConfig", menuName = "Lootbound/Game Config")]
public class LootboundGameConfig : ScriptableObject
```

Current properties:
- `DefaultGameplayScene` - Scene to load after boot
- `EnableDebugTools` - Whether debug tools are available
- `LogLevel` - Minimum log level to display
- `GameVersion` - Internal version string

This configuration is intentionally minimal. Future systems should have their own configuration assets rather than extending this one.

### Logging (`Lootbound.Core.Logging`)

The `LootboundLog` static class provides consistent logging:

```csharp
LootboundLog.Verbose("Category", "Message");
LootboundLog.Info("Category", "Message");
LootboundLog.Warning("Category", "Message");
LootboundLog.Error("Category", "Message");
```

Output format:
```
[Lootbound][Category] Message
```

Log levels are filtered based on `LootboundGameConfig.LogLevel`. In production builds, verbose logs can be disabled.

### Scene Loading (`Lootbound.Core.Scenes`)

The `SceneLoader` static class wraps Unity's scene management:

```csharp
SceneLoader.LoadScene("SceneName", onComplete);
SceneLoader.LoadSceneAdditive("SceneName", onComplete);
SceneLoader.GetActiveSceneName();
```

All scene loads are asynchronous by default.

## Debug Systems

### Debug Overlay (`Lootbound.Debugging`)

The `DebugOverlay` component displays runtime information:

- FPS counter
- Current scene name
- Unity version
- Bootstrap status
- Game version
- Development build indicator

Toggle with F3 key (configurable).

Uses `OnGUI` for simplicity and reliability. No external dependencies.

## Gameplay Systems

### Player (`Lootbound.Gameplay.Player`)

The player system consists of focused components that work together:

```
PlayerCharacter (Prefab)
├── CharacterController (Unity built-in)
├── PlayerInputReader
├── FirstPersonMotor
├── PlayerStanceController
├── PlayerMovementDebug
└── CameraRoot
    └── Camera + PlayerCameraController
```

#### PlayerMovementConfig

ScriptableObject containing all movement parameters:

```csharp
[CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Lootbound/Player Movement Config")]
public class PlayerMovementConfig : ScriptableObject
```

Parameters include:
- Movement speeds (walk, sprint, crouch)
- Acceleration/deceleration rates
- Jump height, gravity, terminal velocity
- Coyote time, jump buffer
- Stance heights and transition speed
- Camera sensitivity and pitch limits
- CharacterController dimensions

#### PlayerInputReader

Reads from Unity Input System and exposes semantic intentions:

```csharp
public Vector2 MoveInput { get; }
public Vector2 LookInput { get; }
public bool JumpPressedThisFrame { get; }
public bool SprintHeld { get; }
public bool CrouchHeld { get; }
```

Does not process movement - only provides input state.

#### FirstPersonMotor

Core movement logic using CharacterController:

- Ground detection (CharacterController + SphereCast)
- Slope handling with configurable max angle
- Step climbing via CharacterController.stepOffset
- Jump with coyote time and jump buffer
- Air control (reduced but present)
- Gravity with terminal velocity
- Sprint and crouch speed modifiers

Exposes state for other systems:

```csharp
public bool IsGrounded { get; }
public bool IsSprinting { get; }
public bool IsCrouching { get; }
public Vector3 Velocity { get; }
public float CurrentSpeed { get; }
public float GroundAngle { get; }
```

#### PlayerCameraController

FPS camera control:

- Horizontal rotation (yaw) applied to player body
- Vertical rotation (pitch) applied to camera only
- Pitch clamped to configurable limits
- Cursor lock/unlock management

#### PlayerStanceController

Manages standing/crouching:

- Smooth height transitions
- Updates CharacterController height
- Updates camera position
- Headroom check prevents standing under obstacles

#### PlayerMovementDebug

Development overlay (toggle with F4):

- Ground state and angle
- Velocity and speed
- Sprint/crouch state
- Coyote time and jump buffer timers
- Input values

### World (`Lootbound.Gameplay.World`)

The procedural terrain system generates finite, seed-based worlds.

```
ProceduralWorld
├── TerrainGenerator (ProceduralTerrainGenerator)
└── Terrain (Unity Terrain)
```

#### TerrainGenerationConfig

ScriptableObject containing all generation parameters:

```csharp
[CreateAssetMenu(fileName = "TerrainGenerationConfig", menuName = "Lootbound/Terrain Generation Config")]
public class TerrainGenerationConfig : ScriptableObject
```

Parameters include:
- Seed and dimensions
- Macro terrain noise settings
- Valley and ridge features
- Detail noise
- Spawn zone configuration
- Surface classification thresholds

#### TerrainGenerationContext

Runtime data produced during generation:

```csharp
public sealed class TerrainGenerationContext
{
    public int Seed { get; }
    public float[,] HeightMap { get; }
    public float[,] NormalizedHeightMap { get; }
    public float[,] SlopeMap { get; }
    public float[,] MacroMap { get; }
    public Vector3 SpawnPosition { get; set; }
}
```

Not a ScriptableObject - runtime data only.

#### TerrainHeightGenerator

Static class generating heightmap via layered noise:

- Macro terrain (FBM noise)
- Valley features (inverted noise)
- Ridge features (ridged noise)
- Detail noise
- Height remapping
- Slope calculation

Uses `System.Random` with seed for determinism.

#### TerrainSpawnPlanner

Static class finding valid spawn location:

- Searches center region
- Evaluates slope and accessibility
- Applies progressive flattening
- Core zone nearly flat
- Blend zone transitions naturally

#### TerrainSurfacePainter

Static class painting terrain layers:

- Layer 0: Grass (lowlands, gentle slopes)
- Layer 1: DryGround (mid elevations)
- Layer 2: Rock (steep slopes)
- Layer 3: Highland (high elevations)

Uses height, slope, and noise for natural transitions.

#### ProceduralTerrainGenerator

MonoBehaviour orchestrating all generation:

```csharp
public void Generate(int seed);
public void GenerateDefault();
public void GenerateRandom();
public void Regenerate();
public void ClearTerrain();
```

Custom editor provides Inspector buttons for generation control.

#### TerrainGenerationDebug

Development overlay (toggle with F5):

- Seed and status
- Terrain dimensions
- Height statistics
- Spawn position and slope
- Generation timing
- Map visualization (height, slope, macro)

## Namespaces

```
Lootbound.Core
Lootbound.Core.Bootstrap
Lootbound.Core.Configuration
Lootbound.Core.Logging
Lootbound.Core.Scenes
Lootbound.Debugging
Lootbound.Gameplay.Player
Lootbound.Gameplay.World
Lootbound.Gameplay.Interaction
Lootbound.Gameplay.Inventory
Lootbound.UI
```

## Scene Architecture

### 00_Boot

Minimal scene containing only:
- `GameBootstrap` object with the bootstrap component

The bootstrap initializes systems. If `developmentMenuPrefab` is assigned, it displays the scene selection menu. Otherwise, it loads the configured default scene.

### 10_FoundationSandbox

Development scene containing:
- Directional light
- Main camera
- Ground plane
- Sample obstacles
- Debug overlay

Used for testing core systems before gameplay is implemented.

### 11_CharacterControllerSandbox

Character controller test scene containing:
- Player prefab with all movement components
- Flat ground area
- Slopes at various angles (15°, 30°, 45°, 55°)
- Standard and edge-case stairs
- Crouch passages with headroom tests
- Platform drops at various heights
- Narrow passages
- Debug overlay

Used for tuning and validating character movement.

### 12_ProceduralTerrainSandbox

Procedural terrain test scene containing:
- ProceduralWorld with TerrainGenerator and Terrain
- Player prefab positioned at spawn
- Debug overlays (F3, F4, F5)
- Directional light with shadows

Used for testing and tuning terrain generation.

Generation controls available in Inspector on TerrainGenerator object.

### 13_InteractionInventorySandbox

Interaction and inventory test scene containing:
- Player prefab with PlayerInteractor and PlayerInventory
- ItemWorldPickup objects for testing
- InventoryUI, InteractionPromptUI, NotificationUI
- Debug overlays

Used for testing interaction detection and inventory operations.

## Interaction System (`Lootbound.Gameplay.Interaction`)

### IInteractable Interface

```csharp
public interface IInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract { get; }
    string IconId { get; }
    float HoldDuration { get; }
    Transform InteractionTransform { get; }

    void OnInteractionStart(PlayerInteractor interactor);
    void OnInteractionComplete(PlayerInteractor interactor);
    void OnInteractionCancel(PlayerInteractor interactor);
}
```

### PlayerInteractor

Detects interactables via raycast/spherecast from camera:
- Configurable detection distance and sphere radius
- Layer mask filtering
- Hold-duration support for timed interactions
- Events for UI binding: `OnTargetChanged`, `OnHoldProgressChanged`, `OnInteractionCompleted`

## Inventory System (`Lootbound.Gameplay.Inventory`)

### ItemDefinition

ScriptableObject defining item properties:
- Item ID, display name, description
- Icon sprite, world prefab
- Rarity (Common, Uncommon, Rare, Epic, Legendary)
- Stacking settings (max stack size)
- Pickup settings (prompt, hold duration)

### ItemInstance

Runtime item with mutable state:
- References an ItemDefinition
- Tracks current quantity
- Operations: Add, Remove, Split, Clone

### InventorySlot

Container for a single ItemInstance:
- Empty or filled state
- Type checking for stacking
- Quantity management

### Inventory

Collection of slots with operations:
- `TryAddItem` - Add with automatic stacking
- `TryAddItemWithResult` - Returns AddItemResult with detailed info
- `RemoveItem` - Remove by definition and quantity
- Events: `OnInventoryChanged`, `OnSlotChanged`

### AddItemResult

Structured result for add operations:
- `Requested`, `Added`, `Overflow` quantities
- `IsComplete`, `IsPartial`, `IsFailed` status

### PlayerInventory

MonoBehaviour exposing inventory to other systems:
- Events: `OnItemAdded`, `OnItemRemoved`, `OnInventoryFull`
- Wraps Inventory operations with event firing

## World Pickups (`Lootbound.Gameplay.World`)

### ItemWorldPickup

IInteractable pickup in the game world:
- Visual effects (rotation, bobbing)
- Partial pickup support for overflow
- Concurrent interaction prevention
- Static `SpawnPickup` factory method

## UI System (`Lootbound.UI`)

Uses Unity UI Toolkit (UXML + USS + C#).

### InteractionPromptUI

Shows interaction prompt when targeting an interactable:
- Item icon and prompt text
- Hold progress bar
- Opacity based on distance

### InventoryUI

Grid-based inventory panel:
- Slot grid with item icons and quantities
- Item details panel with description
- Drop button for selected items
- Cursor lock/unlock management

### NotificationUI

Temporary notifications for pickups:
- Auto-fade after duration
- Queue system for multiple notifications

### DevelopmentMenuController

Development scene menu (not final game menu):
- Scene selection at boot
- Pause menu via Escape in sandboxes
- Scene reload and switching
- Persists with GameBootstrap
- Manages cursor and time scale

## Design Principles

### Explicit Dependencies

Dependencies are visible in the Inspector or passed as parameters:

```csharp
[SerializeField] private LootboundGameConfig gameConfig;
```

Avoid:
- `FindObjectOfType` in production code
- Global singletons for everything
- Hidden service locators

### Single Responsibility

Each class has one clear purpose:
- `GameBootstrap` - Initialize and start the game
- `SceneLoader` - Load scenes
- `LootboundLog` - Log messages
- `DebugOverlay` - Display debug info

### Minimal Abstraction

Don't create interfaces unless:
- You have multiple implementations
- You need testability with mocks
- The abstraction provides clear value

A direct implementation is preferable to an interface with a single implementation.

## Future Considerations

As new slices are implemented:

1. **New systems** should follow the same patterns
2. **Configuration** should use separate ScriptableObjects per system
3. **Inter-system communication** should use events or direct references, not global event buses
4. **Testing** should focus on pure logic classes, not MonoBehaviour integration

The architecture will evolve, but simplicity remains the priority. Add complexity only when the current approach becomes insufficient.
