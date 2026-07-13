# Development Scene Menu

This document describes the development scene menu system implemented in Slice 0.4.1.

## Overview

The development menu is a **temporary development tool**, not the final game menu. It provides:

- Scene selection at boot (instead of auto-loading a default scene)
- Pause menu accessible via Escape from any sandbox
- Quick scene switching during development
- Reload functionality

## Important Distinction

This is **not** the final Lootbound menu system. It's a development convenience that will be replaced by proper game menus in future slices. The architecture is intentionally simple and focused on developer workflow.

## How It Works

### Boot Flow

```
00_Boot scene loads
→ GameBootstrap initializes
→ DevelopmentMenuController instantiated (child of bootstrap)
→ Scene selection view opens automatically
→ Developer selects a sandbox
→ Selected scene loads
```

### In-Game Flow

```
Escape pressed
→ If inventory open: close inventory first
→ Else if menu open: navigate back or close
→ Else: open pause view
```

## Components

### DevelopmentSceneEntry

Serializable class representing a single scene entry:

```csharp
public sealed class DevelopmentSceneEntry
{
    public string DisplayName;    // Shown in menu
    public string SceneName;      // Exact scene name
    public string Description;    // Shown when selected
    public bool Visible;          // Whether to show in menu
}
```

### DevelopmentSceneCatalog

ScriptableObject containing the list of development scenes:

- Located at `Assets/Lootbound/ScriptableObjects/Development/DevelopmentSceneCatalog.asset`
- Edit this asset to add/remove sandbox scenes
- Validates entries on save (warns about duplicates, missing scenes)

### DevelopmentMenuController

MonoBehaviour controlling the menu UI:

- Persists across scene loads (child of GameBootstrap)
- Manages pause state and cursor
- Handles input priority with inventory
- Uses UI Toolkit (UXML/USS)

### SceneLoader Enhancements

- `CanLoadScene(string)` - Validates scene exists in build
- `IsLoading` - Prevents concurrent loads
- Protection against invalid scene names

## Adding a New Sandbox

1. Create your scene in `Assets/Lootbound/Scenes/`
2. Add scene to Build Settings (File > Build Settings)
3. Open `DevelopmentSceneCatalog.asset`
4. Add a new entry with:
   - Display Name (shown in menu)
   - Scene Name (exact name, must match Build Settings)
   - Description (shown when selected)
   - Visible: true

## Input Priority

When Escape is pressed:

1. **Inventory** - If open, closes (handled by InventoryUI)
2. **Dev Menu** - If open, navigates or closes
3. **Nothing** - Opens pause view

This priority is coordinated through `PlayerCameraController.OnPauseRequested` event.

## Pause Behavior

When pause menu opens:

- `Time.timeScale` saved and set to 0
- Cursor unlocked and visible
- Gameplay input disabled (via event subscription)

When closed/resumed:

- `Time.timeScale` restored
- Cursor locked (in gameplay scenes)
- Normal gameplay resumes

## Configuration

### GameBootstrap

| Field | Description |
|-------|-------------|
| `developmentMenuPrefab` | Prefab with DevelopmentMenuController |
| `loadDefaultSceneOnStart` | Legacy fallback (if no prefab assigned) |

### DevelopmentMenuController

| Field | Description |
|-------|-------------|
| `uiDocument` | UI Toolkit document |
| `catalog` | Scene catalog asset |
| `sortOrder` | UI layering (default: 200) |

## Limitations

This is a V1 development tool. Not implemented:

- Scene thumbnails
- Loading progress bar
- Transition animations
- Last-played scene memory
- Options/settings
- Audio controls
- Save/load slots

## Files

```
Core/
├── Bootstrap/GameBootstrap.cs          (modified)
└── Scenes/
    ├── SceneLoader.cs                  (modified)
    ├── DevelopmentSceneEntry.cs        (new)
    └── DevelopmentSceneCatalog.cs      (new)

UI/
└── DevelopmentMenu/
    ├── DevelopmentMenu.uxml            (new)
    ├── DevelopmentMenu.uss             (new)
    └── DevelopmentMenuController.cs    (new)

Gameplay/
└── Player/
    └── PlayerCameraController.cs       (modified - OnPauseRequested event)

ScriptableObjects/
└── Development/
    └── DevelopmentSceneCatalog.asset   (created in Editor)

Prefabs/
└── UI/
    └── DevelopmentMenu.prefab          (created in Editor)
```

## Editor Setup Required

After pulling these code changes:

1. **Create the catalog asset**:
   - Right-click in `Assets/Lootbound/ScriptableObjects/Development/`
   - Create > Lootbound > Development Scene Catalog
   - Add entries for each sandbox

2. **Create the menu prefab**:
   - Create empty GameObject named "DevelopmentMenu"
   - Add UIDocument component
   - Assign `LootboundPanelSettings` as Panel Settings
   - Assign `DevelopmentMenu.uxml` as Source Asset
   - Add DevelopmentMenuController component
   - Assign UIDocument reference
   - Assign catalog asset
   - Save as prefab in `Assets/Lootbound/Prefabs/UI/`

3. **Update Boot scene**:
   - Open `00_Boot.unity`
   - Select GameBootstrap object
   - Assign the DevelopmentMenu prefab
   - Assign the catalog asset (optional, also set in prefab)

4. **Update Build Settings**:
   - Add `13_InteractionInventorySandbox` to build (was missing)
