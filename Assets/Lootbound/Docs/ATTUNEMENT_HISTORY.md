# Attunement History V1

## Overview

The Attunement History system records the trace of attunement attempts on equipment. Each piece of equipment remembers what it has been through during the attunement process.

**Philosophy:** History is truthful data, not artificial statistics. Equipment accumulates real memories from attunement attempts.

---

## Data Recorded

### Counters

| Field | Description |
|-------|-------------|
| `TotalAttempts` | Number of resolved attunement attempts |
| `SuccessfulAttempts` | Number of attempts that increased level |
| `FailedAttempts` | Number of attempts that consumed stones but failed |
| `TotalStonesConsumed` | Total attunement stones used |

### Progress Tracking

| Field | Description |
|-------|-------------|
| `HighestAttunementLevelReached` | Maximum level this equipment ever reached |
| `LongestFailureStreak` | Maximum consecutive failures encountered |

### Last Attempt Details

| Field | Description |
|-------|-------------|
| `LastAttemptTimestamp` | Unix timestamp of last resolved attempt |
| `LastAttemptLocation` | Where the attempt occurred |
| `LastAttemptSuccess` | Whether the last attempt succeeded |
| `LastAttemptWasGuaranteed` | Whether resonance guaranteed success |

---

## Write Authority

**Single Authority:** `AttunementService` is the only system that records attunement history.

```
Player → AttunementTableUI → AttunementService.TryAttune()
                                    ↓
                            RecordAttunementHistory()
                                    ↓
                            EquipmentData.History.Attunement
```

No other system may directly modify attunement history.

---

## Resolved Attempt Definition

An attempt is **resolved** when the RNG roll actually happened:

```csharp
bool WasAttemptResolved => Success || WasRngFailure;
```

### Resolved Attempts (Recorded)
- **Success:** Level increased, stones consumed
- **RNG Failure:** Level unchanged, stones consumed, protection gained

### Technical Refusals (Not Recorded)
- Already at maximum level
- Insufficient attunement stones
- Equipment is broken

Technical refusals do not affect history counters.

---

## Successes and Failures

**Success:** `AttunementAttemptResult.Success == true`
- Increments `TotalAttempts` and `SuccessfulAttempts`
- Updates `HighestAttunementLevelReached` if new level is higher
- Records last attempt as successful

**Failure:** `AttunementAttemptResult.WasRngFailure == true`
- Increments `TotalAttempts` and `FailedAttempts`
- Updates `LongestFailureStreak` if current streak exceeds record
- Records last attempt as failed

---

## Stones Consumed

`TotalStonesConsumed` accumulates from `AttunementAttemptResult.StonesConsumed`.

This tracks the total investment in this equipment across all attempts.

---

## Failure Streaks

### Source of Truth

| Data | Location | Purpose |
|------|----------|---------|
| `ConsecutiveAttunementFailures` | `EquipmentData` | Gameplay (resonance calculation) |
| `LongestFailureStreak` | `AttunementHistory` | Historical record |

### Update Logic

```csharp
// After a failure
longestFailureStreak = Math.Max(longestFailureStreak, currentStreak);

// After a success
// Streak resets in EquipmentData, but history preserves the longest ever
```

---

## Guaranteed Success (Resonance)

`LastAttemptWasGuaranteed` records whether the last attempt benefited from 100% resonance protection.

This is determined by checking if `AttemptedChance >= 1.0f` at the time of the attempt.

---

## Maximum Level Tracking

`HighestAttunementLevelReached` captures the peak attunement level this equipment ever achieved.

This value never decreases, even if the equipment is reset or loses levels through future mechanics.

---

## Timestamp

`LastAttemptTimestamp` uses Unix time (seconds since epoch):

```csharp
DateTimeOffset.UtcNow.ToUnixTimeSeconds()
```

### Why Unix Time?
- Serialization-safe
- Timezone-independent
- Compact storage
- Easy comparison

### Display

Use `FormatTimestamp()` for human-readable time display:
- "Today, 21:42"
- "Yesterday, 18:15"
- "14 July"
- "14 July 2025"

---

## Location

`LastAttemptLocation` records where the attunement occurred.

### Source
`AttunementTable.LocationName` provides the location string.

### Default
`AttunementService.DefaultAttunementLocation = "Attunement Table"`

### Future Use
- Named attunement tables in different areas
- Special attunement locations with bonuses
- History display showing where equipment was enhanced

---

## Preservation

Attunement history is preserved through:

| Operation | Preserved? |
|-----------|------------|
| Equipment drop/pickup | ✓ |
| Inventory transfer | ✓ |
| Equipment break | ✓ |
| Equipment repair | ✓ |
| Equipment clone | ✓ |
| Save/Load | ✓ |

The `Clone()` method performs a deep copy of attunement history.

---

## UI Display

### Inventory Details Panel

When equipment has attunement history, displays:
- Summary: "N attempts (X successes, Y failures)"
- Stones consumed total
- Last attempt location and time

### Attunement Table UI

History section shows:
- Attempt statistics
- Success/failure counts
- Last attempt result

---

## Debug Controls

### CombatDebugOverlay (F6)

History section displays:
- Att/Suc/Fail counters
- Stones/High/LStrk values
- Last attempt details
- Reset History button

### Reset Functionality

`AttunementHistory.Reset()` clears all history data for testing purposes.

---

## Defensive Migration

`AttunementHistory` uses lazy initialization:

```csharp
public AttunementHistory Attunement
{
    get
    {
        attunementHistory ??= new AttunementHistory();
        return attunementHistory;
    }
}
```

Existing equipment without history automatically receives empty history on first access.

---

## Limits

### V1 Scope
- Single location string (no location history)
- No partial attempt recording
- No attempt-by-attempt log
- No undo capability

### Not Tracked
- Individual attempt details beyond the last one
- Time spent between attempts
- Player who performed the attempt (single-player)

---

## Related Documentation

- [ATTUNEMENT.md](ATTUNEMENT.md) — Attunement system overview
- [EQUIPMENT_HISTORY.md](EQUIPMENT_HISTORY.md) — Equipment history system
- [EQUIPMENT_CONDITION.md](EQUIPMENT_CONDITION.md) — Condition and repair system

---

## Implementation Files

| File | Purpose |
|------|---------|
| `AttunementHistory.cs` | History data structure |
| `EquipmentHistory.cs` | Parent container |
| `AttunementService.cs` | Recording authority |
| `AttunementAttemptResult.cs` | WasAttemptResolved property |
| `AttunementTable.cs` | Location provider |
| `AttunementTableUI.cs` | UI integration |
| `InventoryUI.cs` | History display |
| `CombatDebugOverlay.cs` | Debug display and reset |
