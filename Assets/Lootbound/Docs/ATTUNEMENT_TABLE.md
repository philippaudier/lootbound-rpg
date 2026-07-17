# Attunement Table (Slice 0.8.4)

This document describes the Attunement Table (Table d'Accord) UI implemented in Slice 0.8.4.

## Overview

The Attunement Table is a physical world object where players can deepen equipment attunement by consuming Attunement Stones. Unlike the Repair Station which restores what equipment was, the Attunement Table reveals what equipment can become.

## Philosophy

The Attunement Table is:
- Silent and ancient
- A place of decision, not a slot machine
- Focused on the ritual of choosing which weapon to trust with a stone

The Table does NOT:
- Use flashy animations (V1)
- Play dramatic sounds (V1)
- Involve NPCs or spirits
- Function as a shop or crafting station

## User Flow

```
Approach Table
→ Press E to interact
→ UI opens, controls blocked, cursor unlocked
→ Select a weapon from inventory
→ View current level, stats, and preview
→ See cost (1 Attunement Stone) and available stones
→ Click "Deepen Attunement"
→ Stone consumed, level increases
→ Stats update immediately
→ Result shown in overlay
→ Continue or close with Escape
```

## Architecture

### World Object

**AttunementTable.cs** (`Gameplay/World/`)
- Implements `IInteractable`
- Instant interaction (no hold required)
- Manages in-use state
- Raises events for UI subscription

```csharp
public class AttunementTable : MonoBehaviour, IInteractable
{
    public bool IsInUse { get; }
    public Transform WeaponAnchor { get; }  // Future visual placement
    public Transform StoneAnchor { get; }   // Future visual placement

    public event Action<AttunementTable> OnInteractionRequested;
    public event Action<AttunementTable> OnTableOpened;
    public event Action<AttunementTable> OnTableClosed;

    public void SetInUse(bool inUse);
    public void Close();
}
```

### UI Controller

**AttunementTableUI.cs** (`UI/AttunementTable/`)
- Subscribes to all AttunementTable objects in scene
- Opens UI when interaction is requested
- Manages input blocking and cursor
- Displays equipment list and preview
- Handles attunement attempts via AttunementService

```csharp
public class AttunementTableUI : MonoBehaviour
{
    public bool IsOpen { get; }
    public event Action OnClosed;

    public void Open(AttunementTable table);
    public void Close();
}
```

### UI Toolkit Files

```
UI/AttunementTable/
├── AttunementTable.uxml    - Layout structure
└── AttunementTable.uss     - Styles (purple/mystical theme)
```

## UI Layout

```
AttunementTablePanel
├── Header
│   ├── Title: "Attunement Table"
│   └── Close Button
│
├── Content
│   ├── Equipment List (left)
│   │   └── Scrollable list of weapons
│   │
│   └── Preview Panel (right)
│       ├── Equipment Info (name, rarity, affixes)
│       ├── Condition + Broken Warning
│       ├── Current Attunement (+N / +10)
│       ├── On Success Preview (+N+1)
│       ├── Success Chance (100% in V1)
│       ├── Stat Preview (Damage: X → Y)
│       ├── Cost (1 Attunement Stone)
│       ├── Stones Available
│       └── Deepen Attunement Button
│
├── Result Panel (overlay)
│   ├── Title (success/failure)
│   ├── Message
│   ├── Stats change
│   └── Continue Button
│
└── Footer
    └── "Press Escape to close"
```

## Equipment List

### What's Shown
- All equipment (weapons) in player inventory
- Both attuneable and maximum-level items
- Equipped weapon highlighted with badge

### What's NOT Shown
- Potions, materials, consumables
- Repair Fragments
- Attunement Stones themselves
- Stackable items

### Sorting Order
1. Currently equipped weapon
2. Non-maximum weapons (can be attuned)
3. By attunement level (descending)
4. By name (alphabetical)

### Visual States
- `.equipment-selected` - Currently selected
- `.equipment-equipped` - Currently equipped weapon
- `.equipment-maximum` - At +10 (slightly dimmed)

## Preview Panel

### Always Shown
- Equipment name (with +N suffix if attuned)
- Rarity with color
- Affixes
- Condition
- History (if available)

### Attunement Info
- Current level: "+N"
- Maximum: "/ +10"
- On success: "+N+1" or "Maximum"

### Success Chance
- V1: Always displays "100%"
- Color: Green (100%), Yellow (<100%)

### Stat Preview
Only shows stats that change:
```
Damage
24.0 → 25.4
```

### Cost
```
Cost
1 Attunement Stone
Available: 3
```

## Button States

### Enabled (can attempt)
- Valid weapon selected
- Not at maximum
- Has enough stones
- No attempt in progress

Text: "Deepen Attunement - 1 Stone"

### Disabled States
| Condition | Button Text |
|-----------|-------------|
| At maximum | "Maximum Reached" |
| No stones | "Not Enough Stones" |
| Other failure | Standard disabled style |

## Broken Weapons

Broken weapons can be attuned:
- Warning displayed: "This weapon is Broken..."
- Attunement proceeds normally
- Broken penalty still applies to stats
- Attunement level is preserved

## Input Handling

### On Open
- `PlayerInputReader.SetInputEnabled(false)`
- `PlayerCameraController.UnlockCursor()`
- `root.pickingMode = PickingMode.Position`

### On Close
- `PlayerInputReader.SetInputEnabled(true)`
- `PlayerCameraController.LockCursor()`
- `root.pickingMode = PickingMode.Ignore`

### Escape Key
- Checked every frame via `inputReader.PausePressedThisFrame`
- Closes the table UI

## Transaction Flow

1. Click "Deepen Attunement"
2. Disable button (prevent double-click)
3. Verify equipment still in inventory
4. Get preview from AttunementService
5. If preview.CanAttempt is false → show failure
6. Call `service.TryAttune(equipment)`
7. Show result panel
8. Refresh equipment list
9. Update stone count
10. Re-select equipment if still valid

## Result Panel

### Success
```
Title: "Attunement Deepened" (green)
Message: "Honed Blade reached +4."
Stats: "Damage: 24.0 → 25.4"
Button: "Continue"
```

### Failure
```
Title: "Cannot Attune" (red)
Message: Failure reason text
Stats: (hidden)
Button: "Continue"
```

## Integration with AttunementService

The UI does NOT contain business logic. It delegates to:

```csharp
// Preview without mutation
AttunementAttemptPreview preview = service.PreviewAttempt(equipment);

// Execute attempt
AttunementAttemptResult result = service.TryAttune(equipment);
```

## Coexistence with Repair Station

- Only one station UI can be open at a time
- Each UI manages its own input blocking
- No shared callbacks
- Independent cursor management
- Escape priority handled by each UI

## Prefab Setup

### P_AttunementTable.prefab

```
AttunementTable (root)
├── Visuals (model)
├── BoxCollider (Layer: Interactable)
├── WeaponAnchor (Transform, future use)
├── StoneAnchor (Transform, future use)
└── AttunementTable component
    ├── Interaction Prompt: "Use Attunement Table"
    └── Hold Duration: 0 (instant)
```

### AttunementTableUI (in scene)

Requires in scene:
```
AttunementTableUI
├── UIDocument
│   ├── Source Asset: AttunementTable.uxml
│   └── Sort Order: 110
├── PlayerInputReader reference
├── PlayerCameraController reference
├── PlayerInventory reference
├── PlayerEquipment reference
└── EquipmentRegistry reference
```

## Scene Setup

### Adding AttunementTableUI to a Scene

1. Create empty GameObject named "AttunementTableUI"
2. Add UIDocument component
3. Assign AttunementTable.uxml
4. Set Sort Order: 110
5. Add AttunementTableUI script
6. Assign player references (auto-find works in editor)

### Placing the Table

1. Drag P_AttunementTable prefab into scene
2. Position in refuge/test area
3. Ensure it's on an interactable layer
4. Verify collider is appropriate size

## Testing Scenarios

### Basic Flow
1. Approach table with weapon in inventory
2. Press E to interact
3. Select weapon
4. Click Deepen Attunement
5. Verify level increases
6. Close with Escape

### No Stones
1. Have weapon but no stones
2. Open table
3. Verify button is disabled
4. Verify "Not Enough Stones" text

### Maximum Level
1. Have +10 weapon
2. Open table
3. Verify "Maximum Reached" state
4. Verify no stone consumption

### Equipped Weapon
1. Equip a weapon
2. Open table
3. Verify equipped weapon is selected by default
4. Attune it
5. Verify it remains equipped
6. Attack enemy to verify new damage

### Multiple Attempts
1. Have multiple stones
2. Open table
3. Attune once
4. Verify result shows
5. Dismiss result
6. Attune again
7. Verify correct stone count

### Broken Weapon
1. Have broken weapon
2. Open table
3. Verify warning shows
4. Attune successfully
5. Verify weapon remains broken

## V1 Limitations

NOT implemented in this slice:
- Success/failure chances (always 100%)
- Variable stone costs per level
- Pity system
- Protection items
- Animation/VFX
- Sound effects
- Weapon/stone visual placement on table
- Camera changes
- Level loss on failure

## Files Created

```
Gameplay/World/
└── AttunementTable.cs

UI/AttunementTable/
├── AttunementTableUI.cs
├── AttunementTable.uxml
└── AttunementTable.uss

Tests/EditMode/
└── AttunementTableTests.cs

Docs/
└── ATTUNEMENT_TABLE.md
```

## Next Steps (Future Slices)

Possible future enhancements:
- Success/failure chances per level
- Visual feedback (weapon glow, stone consume effect)
- Sound design
- Camera focus on table during attunement
- Variable stone costs
- Pity system tracking
- Protection items

These are NOT implemented and should NOT be assumed to exist.
