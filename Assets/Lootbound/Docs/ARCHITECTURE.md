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

## Namespaces

```
Lootbound.Core
Lootbound.Core.Bootstrap
Lootbound.Core.Configuration
Lootbound.Core.Logging
Lootbound.Core.Scenes
Lootbound.Debugging
Lootbound.Gameplay.*        (future)
```

## Scene Architecture

### 00_Boot

Minimal scene containing only:
- `GameBootstrap` object with the bootstrap component

The bootstrap initializes systems and loads the configured default scene.

### 10_FoundationSandbox

Development scene containing:
- Directional light
- Main camera
- Ground plane
- Sample obstacles
- Debug overlay

Used for testing core systems before gameplay is implemented.

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
