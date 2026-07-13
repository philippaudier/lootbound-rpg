# Slice 0.4: Interaction & Inventory System

## Overview

This slice implements the core interaction and inventory systems for Lootbound, including:
- Raycast-based interaction detection
- Item definitions and runtime instances
- Slot-based inventory management
- UI Toolkit interface (prompts, inventory panel, notifications)
- World pickup objects

## Architecture

### Interaction System

Located in `Gameplay/Interaction/`:

| File | Description |
|------|-------------|
| `IInteractable.cs` | Interface for interactable objects |
| `InteractionConfig.cs` | ScriptableObject configuration |
| `PlayerInteractor.cs` | Raycast detection and interaction handling |

#### IInteractable Interface

```csharp
public interface IInteractable
{
    string InteractionPrompt { get; }  // Text shown in prompt
    bool CanInteract { get; }          // Dynamic enable/disable
    string IconId { get; }             // Optional icon identifier
    float HoldDuration { get; }        // 0 for instant, >0 for hold
    Transform InteractionTransform { get; }

    void OnInteractionStart(PlayerInteractor interactor);
    void OnInteractionComplete(PlayerInteractor interactor);
    void OnInteractionCancel(PlayerInteractor interactor);
}
```

### Inventory System

Located in `Gameplay/Inventory/`:

| File | Description |
|------|-------------|
| `ItemRarity.cs` | Enum for item rarity levels |
| `ItemDefinition.cs` | ScriptableObject item template |
| `ItemInstance.cs` | Runtime item with mutable state |
| `InventorySlot.cs` | Single slot container |
| `Inventory.cs` | Full inventory with slots |
| `InventoryConfig.cs` | Configuration ScriptableObject |
| `PlayerInventory.cs` | Player-attached inventory component |

#### Item Definition vs Instance

- **ItemDefinition**: Immutable ScriptableObject asset defining item properties
- **ItemInstance**: Runtime object with mutable quantity, references a definition

### World Pickups

Located in `Gameplay/World/`:

| File | Description |
|------|-------------|
| `ItemWorldPickup.cs` | IInteractable pickup in world |

### UI Toolkit

Located in `UI/`:

| Folder | Contents |
|--------|----------|
| `InteractionPrompt/` | Prompt UI (UXML, USS, C#) |
| `Inventory/` | Inventory panel (UXML, USS, C#) |
| `Notifications/` | Pickup notifications (UXML, USS, C#) |

## Input Actions

Added to `InputSystem_Actions.inputactions`:

| Action | Bindings | Description |
|--------|----------|-------------|
| Interact | E (keyboard), Y (gamepad) | Start/hold interaction |
| Inventory | Tab/I (keyboard), Select (gamepad) | Toggle inventory panel |

## Setup Guide

### Creating the Sandbox Scene

1. Create new scene: `Assets/Lootbound/Scenes/13_InteractionInventorySandbox.unity`
2. Add Terrain (from 12_ProceduralTerrainSandbox or new)
3. Add Player prefab with components:
   - PlayerInputReader
   - PlayerCameraController
   - FirstPersonMotor
   - **PlayerInteractor** (new)
   - **PlayerInventory** (new)

### Creating Configuration Assets

1. **InteractionConfig**: `Create > Lootbound > Interaction > Interaction Config`
   - Set raycast distance, spherecast radius
   - Configure layer mask for interactables

2. **InventoryConfig**: `Create > Lootbound > Inventory > Inventory Config`
   - Set slot count (default: 20)
   - Configure grid columns
   - Set notification preferences

3. **ItemDefinition**: `Create > Lootbound > Inventory > Item Definition`
   - Set item ID, display name, description
   - Assign icon sprite
   - Configure stacking behavior

### Setting Up UI

1. Create **UI Toolkit Panel Settings**: `Create > UI Toolkit > Panel Settings Asset`
   - Configure screen scale mode
   - Set reference resolution

2. Create **UIDocument** GameObjects:
   - Interaction Prompt UI (assign `InteractionPrompt.uxml`)
   - Inventory UI (assign `Inventory.uxml`)
   - Notification UI (assign `Notifications.uxml`)

3. Wire up references in Inspector

### Creating Test Items

1. Create ItemDefinition assets for test items:
   - `Item_HealthPotion`
   - `Item_Sword`
   - `Item_GoldCoin`

2. Create world pickups in scene using ItemWorldPickup component

## Testing

### EditMode Tests

Run via Test Runner (`Window > General > Test Runner`):
- `InventoryTests.cs` - Tests for item instances, slots, and inventory operations

### Manual Testing Checklist

- [ ] Look at ItemWorldPickup, see prompt appear
- [ ] Press E to pick up item
- [ ] Hold E for hold-duration items
- [ ] See pickup notification
- [ ] Press Tab/I to open inventory
- [ ] Cursor unlocks when inventory open
- [ ] Press Tab/I or X to close inventory
- [ ] Cursor locks when inventory closed
- [ ] Items stack correctly
- [ ] Full inventory shows feedback

## Events

### PlayerInventory Events

```csharp
event Action<ItemDefinition, int> OnItemAdded;
event Action<ItemDefinition, int> OnItemRemoved;
event Action<ItemDefinition, int> OnInventoryFull;
```

### Inventory Events

```csharp
event Action OnInventoryChanged;
event Action<int> OnSlotChanged;
```

### PlayerInteractor Events

```csharp
event Action<IInteractable> OnTargetChanged;
event Action<float> OnHoldProgressChanged;
event Action<IInteractable> OnInteractionCompleted;
```

## File Structure

```
Assets/Lootbound/
├── Gameplay/
│   ├── Interaction/
│   │   ├── IInteractable.cs
│   │   ├── InteractionConfig.cs
│   │   └── PlayerInteractor.cs
│   ├── Inventory/
│   │   ├── Inventory.cs
│   │   ├── InventoryConfig.cs
│   │   ├── InventorySlot.cs
│   │   ├── ItemDefinition.cs
│   │   ├── ItemInstance.cs
│   │   ├── ItemRarity.cs
│   │   └── PlayerInventory.cs
│   ├── Player/
│   │   └── PlayerInputReader.cs (modified)
│   └── World/
│       └── ItemWorldPickup.cs
├── Input/
│   └── InputSystem_Actions.inputactions (modified)
├── UI/
│   ├── InteractionPrompt/
│   │   ├── InteractionPrompt.uxml
│   │   ├── InteractionPrompt.uss
│   │   └── InteractionPromptUI.cs
│   ├── Inventory/
│   │   ├── Inventory.uxml
│   │   ├── Inventory.uss
│   │   └── InventoryUI.cs
│   └── Notifications/
│       ├── Notifications.uxml
│       ├── Notifications.uss
│       └── NotificationUI.cs
├── Tests/EditMode/
│   └── InventoryTests.cs
└── Docs/
    └── INTERACTION_INVENTORY.md
```

## Dependencies

- Unity Input System package
- UI Toolkit (built-in Unity 6)
- Lootbound.Core assembly
- Lootbound.Gameplay assembly
