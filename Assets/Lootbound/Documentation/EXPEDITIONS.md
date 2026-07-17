# Expedition Lifecycle System

Slice 0.9.1 — Expedition Lifecycle V1

## Overview

The Expedition Lifecycle system provides the runtime foundation for formal expeditions in Lootbound. It distinguishes between sandbox/testing mode (no expedition) and actual expeditions that track metrics and have consequences.

**Key Principle**: An expedition is a formal gameplay session with a beginning, middle, and end. Metrics are tracked, outcomes matter, and player death has meaning.

## Architecture

### Components

```
ExpeditionState.cs           - Enum defining expedition phases
ExpeditionOutcome.cs         - Enum defining final outcomes
ExpeditionMetrics.cs         - Runtime metrics tracking
ExpeditionSnapshot.cs        - Departure state capture
ExpeditionSession.cs         - Session container with ID, timestamps, metrics
ExpeditionLifecycle.cs       - MonoBehaviour service managing state machine
RefugeZone.cs                - Defines the safe refuge area
ExpeditionBoundary.cs        - Detects crossing between refuge and outside
ExpeditionDebugPanel.cs      - Debug UI (F8)
```

See also: [REFUGE.md](../Docs/REFUGE.md) for refuge system details.

### State Machine

```
None → Preparing → Departing → Active → Returning → Completed
                                  ↓           ↓
                               Failed ←←←← Cancelled
```

| State | Description | Tracking |
|-------|-------------|----------|
| None | No expedition active (sandbox mode) | No |
| Preparing | Player in refuge, organizing equipment | No |
| Departing | Commitment made, snapshot captured | No |
| Active | Expedition in progress | Yes |
| Returning | Heading back to refuge | Yes |
| Completed | Safe return (terminal) | Frozen |
| Failed | Player died (terminal) | Frozen |
| Cancelled | Debug cancel (terminal) | Frozen |

### State Transitions

| From | To | Trigger |
|------|----|---------|
| None | Preparing | `StartExpedition()` |
| Preparing | Departing | `Depart()` |
| Departing | Active | Automatic |
| Active | Returning | `BeginReturn()` |
| Active/Returning | Completed | `CompleteExpedition()` |
| Any (non-terminal) | Failed | Player death |
| Any (non-terminal) | Cancelled | `CancelExpedition()` |

## Metrics Tracked

### Duration
- Tracked in seconds while in Active or Returning state
- Updated every frame via `Time.deltaTime`
- Formatted as MM:SS for display

### Max Distance
- Horizontal (XZ) distance from expedition origin
- Sampled every 0.5 seconds
- Only the maximum value is stored

### Enemies Defeated
- Incremented when any enemy dies during tracking
- Tracked via `EnemyHealth.OnAnyEnemyDied` static event

### Items Acquired
- `ItemsAcquired`: Total count of all items picked up
- `EquipmentAcquired`: Equipment pieces only
- `ResourcesAcquired`: Non-equipment items (stones, etc.)
- Tracked via `PlayerInventory.OnItemAdded` and `OnEquipmentAdded`

### Main Weapon
- The weapon with the most kills during this expedition
- Tracked by associating kills with currently equipped weapon
- Includes kill count for display

## Departure Snapshot

When departing, the system captures:
- Equipped weapon (ID, name, attunement level)
- Resource counts in inventory

This snapshot is immutable and can be compared to end state.

## Integration Points

### Player Death
```csharp
// Subscribes to PlayerHealth.OnDied
// Automatically fails the expedition if Active or Returning
```

### Item Pickup
```csharp
// Subscribes to PlayerInventory.OnItemAdded and OnEquipmentAdded
// Records metrics for each pickup
```

### Enemy Death
```csharp
// Subscribes to EnemyHealth.OnAnyEnemyDied (static event)
// Gets currently equipped weapon from PlayerEquipment
// Associates kill with that weapon
```

### World Seed
```csharp
// Reads from ProceduralTerrainGenerator.CurrentSeed
// Stored in session for reproducibility
```

## Debug Panel (F8)

The expedition debug panel shows:
- Current state with color coding
- World seed
- Session ID (truncated)
- Control buttons:
  - Start / Depart / Complete / Return
  - Cancel / Clear
- Live metrics (duration, distance, kills, items)
- Departure snapshot (weapon info)

## Usage Example

```csharp
// Start an expedition
expeditionLifecycle.StartExpedition();

// Player organizes equipment...

// Player leaves refuge
expeditionLifecycle.Depart();

// Tracking is now active
// Player explores, fights, loots...

// Player reaches refuge
expeditionLifecycle.CompleteExpedition();

// View final metrics
var metrics = expeditionLifecycle.CurrentSession.Metrics;
Debug.Log($"Duration: {metrics.DurationFormatted}");
Debug.Log($"Kills: {metrics.EnemiesDefeated}");

// Clear session when done viewing
expeditionLifecycle.ClearSession();
```

## Events

```csharp
// State change notification
OnStateChanged(ExpeditionState oldState, ExpeditionState newState)

// Expedition started (entered Preparing)
OnExpeditionStarted(ExpeditionSession session)

// Expedition ended (terminal state reached)
OnExpeditionEnded(ExpeditionSession session, ExpeditionOutcome outcome)
```

## Setup

1. Add `ExpeditionLifecycle` component to a persistent GameObject
2. Assign references (or let it auto-find):
   - PlayerHealth
   - PlayerInventory
   - PlayerEquipment
   - Player Transform
   - ProceduralTerrainGenerator

3. Add `ExpeditionDebugPanel` to a debug UI object (optional)

## V1 Limitations

- No persistence (session lost on scene reload)
- No expedition history
- No XP/rewards integration
- Weapon tracking assumes equipped weapon = killing weapon
- Cancel only available for debug (no UI button)
- No expedition summary screen
- Returning state transition is manual (no trigger zone)

## Future Enhancements (V2+)

- `ExpeditionHistory` for persistent records
- Integration with reward/XP systems
- Automatic refuge detection (trigger zone)
- Expedition summary UI
- Equipment loss on death
- Resource loss on death
- Expedition statistics and achievements
