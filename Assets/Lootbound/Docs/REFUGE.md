# Refuge System

Slice 0.9.2 — Refuge Departure & Return

## Philosophy

The refuge is not a spawn point. It is **home**.

When the player leaves the refuge, they are making a choice:

> I accept the risk. I'm going out there.

When they return:

> I made it back.

This boundary is the first real **threshold** in the game. Crossing it transforms a casual walk into a formal expedition with stakes, tracking, and meaning.

## Architecture

### Components

```
Gameplay/Expeditions/
├── RefugeZone.cs           — Defines the safe area
├── ExpeditionBoundary.cs   — Detects crossing direction
```

### RefugeZone

Defines a circular safe area around a center point.

**Configuration:**
- `centerOffset` — Local offset from transform position
- `radius` — Radius of the safe zone
- `refugeName` — Display name (e.g., "The Refuge")

**Runtime State:**
- `IsPlayerInside` — Whether player is currently in zone
- `OnPlayerEntered` — Event fired when player enters
- `OnPlayerExited` — Event fired when player exits

**Usage:**
```csharp
// Check if a position is safe
bool safe = refugeZone.IsPositionInside(position);

// Get distance from refuge center
float distance = refugeZone.GetDistanceFromCenter(playerPosition);
```

### ExpeditionBoundary

A trigger-based boundary that detects direction of crossing.

**Configuration:**
- `refugeDirection` — Direction pointing toward refuge (local space)
- `crossingCooldown` — Minimum time between valid crossings (anti-spam)
- `minimumCrossDistance` — Minimum distance past boundary to count as crossed
- `autoStartExpedition` — Auto-start expedition when leaving refuge

**Runtime State:**
- `CurrentSide` — Which side of boundary player is on (Refuge/Outside/Unknown)
- `LastConfirmedSide` — Side before entering trigger
- `CooldownRemaining` — Time until next crossing allowed
- `CrossingCount` — Total crossings since scene load

**Events:**
- `OnDeparture` — Fired when player crosses toward outside
- `OnReturn` — Fired when player crosses toward refuge

## State Transitions

### Departure (Refuge → Outside)

```
Player in refuge
    ↓
Crosses boundary outward
    ↓
If state == None && autoStart:
    StartExpedition() → Preparing
    ↓
If state == Preparing:
    Depart() → Departing → Active
    ↓
Expedition tracking begins
```

### Return (Outside → Refuge)

```
Player outside
    ↓
Crosses boundary inward
    ↓
If state == Active:
    BeginReturn() → Returning
    ↓
CompleteExpedition() → Completed
    ↓
"Safe Return" notification
```

## Direction Detection

The boundary uses a **signed distance** calculation:

```
          ← Refuge Direction

    [REFUGE]  |  [OUTSIDE]
              ↑
          Boundary

Signed distance > 0 = Refuge side
Signed distance < 0 = Outside side
```

**How crossing is detected:**
1. Player enters trigger → Record current side
2. Player exits trigger → Calculate new side
3. If side changed and cooldown elapsed → Valid crossing

## Anti-Spam Protection

Two mechanisms prevent rapid re-triggering:

1. **Cooldown Timer** — After a valid crossing, additional crossings are blocked for `crossingCooldown` seconds (default: 1s)

2. **Minimum Cross Distance** — Player must move at least `minimumCrossDistance` meters past the boundary plane

This prevents:
- Oscillating on the boundary edge
- Accidental double-triggers
- Exploit of rapid crossing

## UI Notifications

On state transitions, the notification system displays:

| Transition | Message | Color |
|------------|---------|-------|
| Departing → Active | "Expedition Started" | Warm yellow |
| → Completed | "Safe Return" | Green |
| → Failed | "Expedition Failed" | Red |

Notifications are:
- Discrete (not blocking gameplay)
- Auto-fade after a few seconds
- Stacked with other notifications

## Debug Panel (F8)

The expedition debug panel includes refuge information:

**Refuge Section:**
- Inside Refuge: Yes/No with color coding
- Boundary Side: Refuge/Outside/Unknown
- Cooldown: Time remaining
- Crossings: Total count

**Debug Actions:**
- "To Refuge" — Teleport player to refuge center
- "To Outside" — Teleport player outside refuge radius

## Setup Instructions

### 1. Create RefugeZone

1. Create empty GameObject at refuge center
2. Add `RefugeZone` component
3. Configure:
   - `Radius` — Size of safe zone
   - `RefugeName` — Display name
4. Adjust `centerOffset` if needed

### 2. Create ExpeditionBoundary

1. Create empty GameObject at boundary location (arch, door, path)
2. Add `ExpeditionBoundary` component (auto-adds Collider)
3. Add/configure trigger collider (BoxCollider recommended):
   - Size: Wide enough for player path
   - `Is Trigger`: True (auto-set by component)
4. Configure:
   - `Refuge Direction`: Point toward refuge (usually -Z or custom)
   - `Auto Start Expedition`: True for typical gameplay
5. Optionally assign references (auto-finds if not set)

### 3. Visual Setup (Optional)

Place the boundary at a meaningful location:
- Stone archway
- Wooden gate
- Bridge crossing
- Path marker

The boundary should feel like a natural transition point.

## Gizmos

**RefugeZone:**
- Green circle showing safe zone radius
- Label with refuge name

**ExpeditionBoundary:**
- Yellow line showing boundary plane
- Green arrow pointing to refuge
- Red arrow pointing outside
- Yellow lines showing minimum cross distance

## V1 Limitations

- Single refuge only (no multiple refuges)
- No persistence (refuge state lost on scene reload)
- No visual feedback at boundary (sound, particles, etc.)
- No refuge "warmth" indicator (distance-based comfort)
- Return side detection is basic (no "almost home" state)

## Future Enhancements (V2+)

### Visual Identity

Consider adding recognizable refuge landmarks visible from distance:
- Warm lantern light
- Rising smoke/steam
- Distinctive tree silhouette
- Unique terrain feature

The player should **recognize** their refuge, not rely on UI markers.

### Sensory Feedback

- Sound: Ambient change when crossing boundary
- Music: Subtle shift in tone
- Visual: Slight color grading change
- Particle: Dust motes or fireflies near boundary

### Comfort System

- Track "time since last refuge visit"
- Visual/audio feedback when far from refuge
- Relief effect when approaching refuge

### Multiple Refuges

- RefugeRegistry for tracking discovered refuges
- Unique identity per refuge
- "Home" vs "waystation" distinction
