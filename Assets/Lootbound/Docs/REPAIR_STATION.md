# Repair Station System (Slice 0.7.5)

This document describes the Repair Station system implemented in Slice 0.7.5.

## Overview

The Repair Station provides a physical location in the world where players can repair their equipment. It replaces the debug UI repair panel with a proper world interaction, making equipment repair feel like a meaningful ritual rather than a menu operation.

## Philosophy

The Repair Station is not a vendor or a magic forge. It is simply an old workbench - a quiet place where the player can take care of their equipment after an expedition.

The station should feel:
- Familiar and reliable
- Like returning home
- Simple and focused
- Part of the refuge experience

## Architecture

```
Gameplay/World/
└── RepairStation.cs          - IInteractable world object

UI/RepairStation/
├── RepairStationUI.cs        - UI controller
├── RepairStation.uxml        - UI layout
└── RepairStation.uss         - UI styles
```

## Components

### RepairStation

The world object that implements `IInteractable`:

```csharp
[RequireComponent(typeof(Collider))]
public class RepairStation : MonoBehaviour, IInteractable
```

Key properties:
- `InteractionPrompt` - "Press E to repair equipment"
- `CanInteract` - True when station is not in use
- `HoldDuration` - 0 (instant interaction)
- `IsInUse` - Whether the station UI is currently open
- `RepairAnchor` - Transform for visual equipment placement (future use)

Methods:
- `SetInUse(bool)` - Called by UI to mark station as in use
- `Close()` - Called by UI when player closes the station

Events:
- `OnInteractionRequested` - Fired when interaction completes (UI subscribes to this)
- `OnStationOpened` - Fired when station is marked as in use
- `OnStationClosed` - Fired when station is closed

### RepairStationUI

The UI controller that manages the repair interface:

```csharp
public class RepairStationUI : MonoBehaviour
```

Key methods:
- `Open(RepairStation station)` - Opens the UI for a specific station
- `Close()` - Closes the UI and restores player control

Features:
- Equipment list showing only items that need repair
- Repair preview with before/after durability
- Cost display (repair fragments)
- Condition change preview
- Full/partial repair support

## Workflow

### Opening the Station

1. Player looks at Repair Station
2. Interaction prompt appears
3. Player presses E
4. `OnInteractionComplete` is called on RepairStation
5. RepairStation fires `OnInteractionRequested` event
6. RepairStationUI (subscribed to all stations) receives event
7. RepairStationUI calls `station.SetInUse(true)`
8. Station UI opens
9. Cursor is unlocked
10. Gameplay input is disabled

### Selecting Equipment

1. Equipment list shows all items needing repair
2. Player clicks an item
3. Preview panel updates with:
   - Equipment name and rarity
   - Current durability and condition
   - After-repair durability and condition
   - Repair cost (fragments required)
   - Available fragments

### Repairing

1. Player clicks "Repair" button
2. `RepairService` executes repair
3. Fragments are consumed from inventory
4. Durability is restored
5. Repair is recorded in equipment history (Slice 0.7.6)
6. Stats are recalculated if condition changes
7. UI refreshes

### History Recording (Slice 0.7.6)

Each successful repair is automatically recorded in the equipment's history:
- Repair count incremented
- Repairs from Broken count incremented (if applicable)
- Total durability restored accumulated
- Total fragments spent accumulated
- Last repair details stored (timestamp, location, condition before/after)

See [EQUIPMENT_HISTORY.md](EQUIPMENT_HISTORY.md) for full documentation.

### Closing the Station

1. Player presses Escape or clicks X
2. Station UI closes
3. Cursor is locked
4. Gameplay input is re-enabled
5. `RepairStation.Close()` is called
6. Station becomes available again

## Integration

### Required Scene Setup

The following must be present in the scene:

1. **RepairStation prefab** - With collider on Interactable layer
2. **RepairStationUI** - With UIDocument referencing RepairStation.uxml
3. **Player** with:
   - PlayerInputReader
   - PlayerCameraController
   - PlayerInventory
   - PlayerEquipment
   - PlayerRepair

### RepairStationUI References

| Field | Description |
|-------|-------------|
| `uiDocument` | UIDocument with RepairStation.uxml |
| `inputReader` | PlayerInputReader for input control |
| `cameraController` | PlayerCameraController for cursor control |
| `playerInventory` | PlayerInventory for equipment list |
| `playerEquipment` | PlayerEquipment for stats recalculation |
| `playerRepair` | PlayerRepair for repair operations |
| `equipmentRegistry` | EquipmentRegistry for affix display |

### Prefab Structure

```
RepairStation
├── Mesh (visual representation)
├── Collider (box or mesh, Interactable layer)
├── InteractionTrigger (optional zone)
├── RepairStation (component)
└── RepairAnchor (transform for equipment preview)
```

## Input Handling

When the station is open:
- Escape closes the station (handled by DevelopmentMenuController)
- Mouse movement is allowed (UI navigation)
- Gameplay input is blocked (movement, combat, interaction, inventory, dodge, jump)

When the station is closed:
- All input returns to normal
- Cursor is locked
- Player regains full control

### Escape Priority

Escape key handling is centralized in `DevelopmentMenuController` with this priority:
1. RepairStationUI open → close it
2. InventoryUI open → close it
3. DevMenu open → navigate/close
4. Nothing open → open pause menu

## UI Structure

### Equipment List (Left Panel)

Shows only equipment that needs repair:
- Icon
- Name (colored by rarity)
- Condition label
- Durability bar

### Preview Panel (Right Panel)

Shown when equipment is selected:
- Equipment name and rarity
- Affixes (if any)
- Current durability bar
- Current condition
- After-repair durability (green)
- After-repair condition
- Repair cost
- Available fragments
- Repair button

### Failure States

When repair is not possible, shows reason:
- "No repair fragments available"
- "Not enough repair fragments"
- "Already at full durability"
- "Cannot repair broken equipment" (if config disallows)

## Tests

EditMode tests in `RepairStationTests.cs`:

### Component Tests
- RepairStation_InitialState_IsNotInUse
- RepairStation_CanInteract_TrueWhenNotInUse
- RepairStation_InteractionPrompt_ContainsExpectedText
- RepairStation_HoldDuration_IsZeroByDefault
- RepairStation_InteractionTransform_ReturnsStationTransform
- RepairStation_RepairAnchor_ReturnsTransformWhenNotSet
- RepairStation_IconId_ReturnsExpectedValue

### State Management Tests
- RepairStation_Close_NotifiesStation
- RepairStation_MultipleCloses_DoNotThrow
- RepairStation_OnInteractionCancel_DoesNotChangeState
- RepairStation_OnInteractionStart_DoesNotOpenUI

### Event Tests
- RepairStation_OnStationOpened_NotNullAfterSubscribe
- RepairStation_OnStationClosed_NotNullAfterSubscribe
- RepairStation_SetInUse_FiresOpenedEvent
- RepairStation_SetInUse_FiresClosedEvent
- RepairStation_SetInUseSameValue_DoesNotFireEvent
- RepairStation_OnInteractionRequested_FiresOnInteraction
- RepairStation_OnInteractionComplete_WhenInUse_DoesNotFireEvent

### RequireComponent Tests
- RepairStation_RequiresCollider

Note: Full UI integration tests require PlayMode tests.

## V1 Limitations

Not implemented in this slice:
- Equipment visual preview on workbench
- Dedicated camera angle
- Repair animation
- Sound effects
- Enhancement system
- NPC artisan
- ~~Repair history display~~ (Added in Slice 0.7.6 - data recording + UI display)
- Multiple repair stations with distinct locations

## Scene Setup Steps

1. Create empty GameObject named "RepairStation"
2. Add BoxCollider component
3. Set layer to "Interactable"
4. Add RepairStation component
5. Create child for visual mesh
6. Create RepairStationUI object with UIDocument
7. Assign RepairStation.uxml to UIDocument
8. Connect all references on RepairStationUI

## Usage Example

```csharp
// Finding repair station in scene
var station = FindFirstObjectByType<RepairStation>();

// Checking if station is available
if (station.CanInteract)
{
    // Player can use the station
}

// Subscribing to station events
station.OnStationOpened += HandleStationOpened;
station.OnStationClosed += HandleStationClosed;
```

## Future Improvements

Potential enhancements for future slices:
- Equipment placed visually on workbench
- Camera focuses on workbench
- Repair animation with particles
- Ambient sound (tools, workbench)
- Enhancement integration
- Batch repair (repair all)
- Repair history panel
