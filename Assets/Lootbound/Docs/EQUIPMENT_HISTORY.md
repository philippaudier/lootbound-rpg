# Equipment History System

This document describes the equipment history tracking system, which creates emotional attachment by recording significant events in an equipment's lifecycle.

## Philosophy

> "Cette arme a deja ete cassee. Je l'ai ramenee. Je l'ai reparee. Et elle est repartie avec moi."

Equipment in Lootbound is not disposable. Each piece carries memories of where it was found, battles fought, and repairs made. The history system makes these memories visible, transforming a sword from "a weapon with +5 damage" into "the blade I found in the Northern Ruins, repaired twice after nearly losing it to wolves."

## Architecture

```
Gameplay/Equipment/
├── EquipmentHistory.cs     - Tracks all history events
├── EquipmentData.cs        - Contains History reference
├── Repair/
│   └── RepairService.cs    - Calls RecordRepair after repair
└── Attunement/
    ├── AttunementHistory.cs  - Tracks attunement attempts (0.8.6)
    └── AttunementService.cs  - Calls RecordAttempt after attunement
```

## EquipmentHistory Fields

### Discovery Tracking

| Field | Type | Description |
|-------|------|-------------|
| `FoundLocation` | string | Where the equipment was discovered |
| `FoundTimestamp` | long | Unix timestamp of discovery |

### Usage Tracking

| Field | Type | Description |
|-------|------|-------------|
| `EnemiesDefeated` | int | Number of kills with this equipment |
| `TimesEquipped` | int | Number of times equipped |

### Repair Tracking (Slice 0.7.6)

| Field | Type | Description |
|-------|------|-------------|
| `RepairCount` | int | Total number of repairs |
| `RepairsFromBroken` | int | Repairs when condition was Broken |
| `TotalDurabilityRestored` | int | Cumulative durability restored |
| `TotalFragmentsSpent` | int | Total repair fragments used |
| `LastRepairTimestamp` | long | Unix timestamp of last repair |
| `LastRepairLocation` | string | Where last repair occurred |
| `LastRepairConditionBefore` | EquipmentCondition | Condition before last repair |
| `LastRepairConditionAfter` | EquipmentCondition | Condition after last repair |

### Attunement Tracking (Slice 0.8.6)

| Field | Type | Description |
|-------|------|-------------|
| `Attunement` | AttunementHistory | Attunement attempt history (see below) |

### Computed Properties

| Property | Description |
|----------|-------------|
| `HasBeenRepaired` | True if repairCount > 0 |
| `HasAttunementHistory` | True if attunement attempts > 0 |

## Recording Events

### Recording a Kill

```csharp
equipmentData.RecordKill();
```

### Recording an Equip

```csharp
equipmentData.RecordEquip();
```

### Recording a Repair

```csharp
// Called automatically by RepairService after successful repair
equipmentData.RecordRepair(repairResult, "Refuge Workbench");

// The repair result contains:
// - DurabilityRestored
// - FragmentsConsumed
// - ConditionBefore
// - ConditionAfter
// - RestoredFromBroken (computed)
```

## Integration with RepairService

RepairService automatically records repairs in equipment history after successful execution:

```csharp
// In RepairService.ExecuteRepair():
var result = new RepairResult(...);
equipment.RecordRepair(result);  // <-- History updated here
OnRepairCompleted?.Invoke(result);
return result;
```

## Display Methods

### GetSummary()

Returns a human-readable summary focused on discovery and combat:

```
"Found in Northern Ruins on Jun 15. 12 enemies defeated."
```

### GetRepairSummary()

Returns a repair-focused summary:

```
"Never repaired"
"1 repair"
"3 repairs (2 times from broken)"
```

## Serialization

EquipmentHistory is fully serializable via Unity's serialization system. All fields use `[SerializeField]` and primitive types that serialize cleanly.

### Constructor for Deserialization

```csharp
// Full constructor for loading from save data
new EquipmentHistory(
    location, timestamp, kills, equips,
    repairs, fromBroken, durabilityRestored, fragmentsSpent,
    lastRepairTime, lastRepairLoc, lastConditionBefore, lastConditionAfter
);
```

### Clone Support

`Clone()` creates a complete copy including all repair history fields.

## Backward Compatibility

Equipment saved before Slice 0.7.6 will load with default repair history values:
- RepairCount: 0
- RepairsFromBroken: 0
- TotalDurabilityRestored: 0
- TotalFragmentsSpent: 0
- LastRepairTimestamp: 0
- LastRepairLocation: null
- LastRepairConditionBefore: Excellent
- LastRepairConditionAfter: Excellent

This is safe because Unity serializes new fields with default values for existing data.

## Tests

EditMode tests in `RepairTests.cs`:

### History State Tests
- EquipmentHistory_NewEquipment_HasNoRepairHistory
- EquipmentHistory_RecordRepair_IncrementsRepairCount
- EquipmentHistory_RecordRepair_TracksTotalDurabilityRestored
- EquipmentHistory_RecordRepair_TracksTotalFragmentsSpent
- EquipmentHistory_RecordRepair_TracksRepairsFromBroken
- EquipmentHistory_RecordRepair_DoesNotIncrementRepairsFromBroken_WhenNotBroken
- EquipmentHistory_RecordRepair_StoresLastRepairDetails
- EquipmentHistory_RecordRepair_AccumulatesMultipleRepairs
- EquipmentHistory_RecordRepair_IgnoresFailedRepairs

### Serialization Tests
- EquipmentHistory_Clone_PreservesRepairHistory

### Display Tests
- EquipmentHistory_GetRepairSummary_ReturnsCorrectText

### Integration Tests
- RepairService_ExecuteRepair_RecordsInHistory
- RepairService_ExecuteRepair_RecordsRepairFromBroken
- RepairService_ExecuteRepair_DefaultLocation_IsRefugeWorkbench

## UI Display

### Inventory Panel
Equipment details show repair history below discovery/usage history:
- Only displayed when `HasBeenRepaired` is true
- Uses `GetRepairSummary()` for text (e.g., "2 repairs (1 time from broken)")

### Repair Station Panel
Selected equipment shows repair history summary:
- Displayed after affixes list
- Hidden for equipment that has never been repaired

### Debug Panel (F6)
Combat debug overlay shows detailed repair fields:
- Repair Count
- From Broken
- Total Restored
- Fragments Used

## Attunement History (Slice 0.8.6)

AttunementHistory is a sub-structure of EquipmentHistory that tracks attunement attempts.

### AttunementHistory Fields

| Field | Type | Description |
|-------|------|-------------|
| `TotalAttempts` | int | Number of resolved attempts |
| `SuccessfulAttempts` | int | Attempts that increased level |
| `FailedAttempts` | int | Attempts that consumed stones but failed |
| `TotalStonesConsumed` | int | Total attunement stones used |
| `HighestAttunementLevelReached` | int | Peak level ever achieved |
| `LongestFailureStreak` | int | Maximum consecutive failures |
| `LastAttemptTimestamp` | long | Unix timestamp of last attempt |
| `LastAttemptLocation` | string | Where last attempt occurred |
| `LastAttemptSuccess` | bool | Whether last attempt succeeded |
| `LastAttemptWasGuaranteed` | bool | Whether resonance guaranteed success |

### Recording an Attunement Attempt

```csharp
// Called automatically by AttunementService after resolved attempts
equipmentData.History.Attunement.RecordAttempt(result, currentStreak, location);
```

Only **resolved** attempts are recorded (where RNG actually rolled). Technical refusals (maximum level, insufficient stones) are not recorded.

### Display Methods

- `GetSummary()` - Returns "N attempts (X successes, Y failures)"
- `FormatTimestamp()` - Returns formatted time ("Today, 21:42")
- `HasAttemptHistory` - True if any attempts have been made

### Lazy Initialization

`EquipmentHistory.Attunement` uses lazy initialization for backward compatibility:

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

Existing equipment without attunement history automatically receives empty history on first access.

See [ATTUNEMENT_HISTORY.md](ATTUNEMENT_HISTORY.md) for complete documentation.

## V1 Limitations

Not implemented in this slice:
- Multiple repair station locations (currently all repairs are "Refuge Workbench")
- Combat encounter memory (specific fights, not just kill count)
- Named equipment event log
- Full repair log with timestamps visible in UI
- Full attunement attempt log (only last attempt details stored)

## Future Improvements

Potential enhancements for future slices:
- Detailed repair log panel with dates and locations
- Custom repair location names per station
- Achievement-style milestones ("Repaired 10 times", "Attuned 5 times")
- Equipment journal/memoir export
- Full attunement attempt log with all timestamps
- Named attunement table locations across the world
