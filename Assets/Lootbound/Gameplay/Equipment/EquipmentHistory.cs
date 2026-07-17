using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Tracks the history of an equipment piece.
    /// Records significant events that create attachment.
    /// </summary>
    [Serializable]
    public class EquipmentHistory
    {
        [SerializeField] private string foundLocation;
        [SerializeField] private long foundTimestamp;
        [SerializeField] private int enemiesDefeated;
        [SerializeField] private int timesEquipped;

        // Repair history
        [SerializeField] private int repairCount;
        [SerializeField] private int repairsFromBroken;
        [SerializeField] private int totalDurabilityRestored;
        [SerializeField] private int totalFragmentsSpent;
        [SerializeField] private long lastRepairTimestamp;
        [SerializeField] private string lastRepairLocation;
        [SerializeField] private EquipmentCondition lastRepairConditionBefore;
        [SerializeField] private EquipmentCondition lastRepairConditionAfter;

        // Attunement history (Slice 0.8.6)
        [SerializeField] private AttunementHistory attunementHistory;

        /// <summary>
        /// Where this equipment was found.
        /// </summary>
        public string FoundLocation => foundLocation;

        /// <summary>
        /// When this equipment was found (Unix timestamp).
        /// </summary>
        public long FoundTimestamp => foundTimestamp;

        /// <summary>
        /// Number of enemies defeated with this equipment.
        /// </summary>
        public int EnemiesDefeated => enemiesDefeated;

        /// <summary>
        /// Number of times this equipment has been equipped.
        /// </summary>
        public int TimesEquipped => timesEquipped;

        /// <summary>
        /// Total number of repairs performed on this equipment.
        /// </summary>
        public int RepairCount => repairCount;

        /// <summary>
        /// Number of repairs performed when equipment was in Broken condition.
        /// </summary>
        public int RepairsFromBroken => repairsFromBroken;

        /// <summary>
        /// Total durability points restored across all repairs.
        /// </summary>
        public int TotalDurabilityRestored => totalDurabilityRestored;

        /// <summary>
        /// Total repair fragments spent on this equipment.
        /// </summary>
        public int TotalFragmentsSpent => totalFragmentsSpent;

        /// <summary>
        /// Timestamp of the last repair (Unix timestamp).
        /// </summary>
        public long LastRepairTimestamp => lastRepairTimestamp;

        /// <summary>
        /// Location where the last repair occurred.
        /// </summary>
        public string LastRepairLocation => lastRepairLocation;

        /// <summary>
        /// Condition before the last repair.
        /// </summary>
        public EquipmentCondition LastRepairConditionBefore => lastRepairConditionBefore;

        /// <summary>
        /// Condition after the last repair.
        /// </summary>
        public EquipmentCondition LastRepairConditionAfter => lastRepairConditionAfter;

        /// <summary>
        /// Whether this equipment has been repaired at least once.
        /// </summary>
        public bool HasBeenRepaired => repairCount > 0;

        /// <summary>
        /// Attunement history for this equipment.
        /// Lazily initialized to avoid null references.
        /// </summary>
        public AttunementHistory Attunement
        {
            get
            {
                attunementHistory ??= new AttunementHistory();
                return attunementHistory;
            }
        }

        /// <summary>
        /// Whether this equipment has any attunement history.
        /// </summary>
        public bool HasAttunementHistory => attunementHistory?.HasAttemptHistory ?? false;

        /// <summary>
        /// Create a new history for equipment found now.
        /// </summary>
        public EquipmentHistory(string location)
        {
            foundLocation = location ?? "Unknown";
            foundTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            enemiesDefeated = 0;
            timesEquipped = 0;

            // Initialize repair fields
            repairCount = 0;
            repairsFromBroken = 0;
            totalDurabilityRestored = 0;
            totalFragmentsSpent = 0;
            lastRepairTimestamp = 0;
            lastRepairLocation = null;
            lastRepairConditionBefore = EquipmentCondition.Excellent;
            lastRepairConditionAfter = EquipmentCondition.Excellent;
        }

        /// <summary>
        /// Create history from serialized data (legacy, no repair history).
        /// </summary>
        public EquipmentHistory(string location, long timestamp, int kills, int equips)
            : this(location, timestamp, kills, equips, 0, 0, 0, 0, 0, null, EquipmentCondition.Excellent, EquipmentCondition.Excellent)
        {
        }

        /// <summary>
        /// Create history from serialized data with full repair history.
        /// </summary>
        public EquipmentHistory(
            string location,
            long timestamp,
            int kills,
            int equips,
            int repairs,
            int fromBroken,
            int durabilityRestored,
            int fragmentsSpent,
            long lastRepairTime,
            string lastRepairLoc,
            EquipmentCondition lastConditionBefore,
            EquipmentCondition lastConditionAfter)
        {
            foundLocation = location ?? "Unknown";
            foundTimestamp = timestamp;
            enemiesDefeated = kills;
            timesEquipped = equips;

            repairCount = repairs;
            repairsFromBroken = fromBroken;
            totalDurabilityRestored = durabilityRestored;
            totalFragmentsSpent = fragmentsSpent;
            lastRepairTimestamp = lastRepairTime;
            lastRepairLocation = lastRepairLoc;
            lastRepairConditionBefore = lastConditionBefore;
            lastRepairConditionAfter = lastConditionAfter;
        }

        /// <summary>
        /// Record an enemy defeated with this equipment.
        /// </summary>
        public void RecordKill()
        {
            enemiesDefeated++;
        }

        /// <summary>
        /// Record that this equipment was equipped.
        /// </summary>
        public void RecordEquip()
        {
            timesEquipped++;
        }

        /// <summary>
        /// Record a repair operation.
        /// </summary>
        /// <param name="result">The repair result containing durability and condition changes.</param>
        /// <param name="location">Where the repair took place.</param>
        public void RecordRepair(RepairResult result, string location = "Refuge Workbench")
        {
            if (!result.Success) return;

            repairCount++;

            if (result.RestoredFromBroken)
            {
                repairsFromBroken++;
            }

            totalDurabilityRestored += Mathf.RoundToInt(result.DurabilityRestored);
            totalFragmentsSpent += result.FragmentsConsumed;
            lastRepairTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            lastRepairLocation = location ?? "Unknown";
            lastRepairConditionBefore = result.ConditionBefore;
            lastRepairConditionAfter = result.ConditionAfter;
        }

        /// <summary>
        /// Get a human-readable summary of the history.
        /// </summary>
        public string GetSummary()
        {
            var foundDate = DateTimeOffset.FromUnixTimeSeconds(foundTimestamp).LocalDateTime;
            string dateStr = foundDate.ToString("MMM d");

            if (enemiesDefeated == 0)
            {
                return $"Found in {foundLocation} on {dateStr}";
            }

            string killWord = enemiesDefeated == 1 ? "enemy" : "enemies";
            return $"Found in {foundLocation} on {dateStr}. {enemiesDefeated} {killWord} defeated.";
        }

        /// <summary>
        /// Create a copy of this history.
        /// </summary>
        public EquipmentHistory Clone()
        {
            var clone = new EquipmentHistory(
                foundLocation,
                foundTimestamp,
                enemiesDefeated,
                timesEquipped,
                repairCount,
                repairsFromBroken,
                totalDurabilityRestored,
                totalFragmentsSpent,
                lastRepairTimestamp,
                lastRepairLocation,
                lastRepairConditionBefore,
                lastRepairConditionAfter);

            // Clone attunement history if present
            if (attunementHistory != null)
            {
                clone.attunementHistory = attunementHistory.Clone();
            }

            return clone;
        }

        /// <summary>
        /// Get repair-focused summary for UI display.
        /// </summary>
        public string GetRepairSummary()
        {
            if (repairCount == 0)
            {
                return "Never repaired";
            }

            string repairWord = repairCount == 1 ? "repair" : "repairs";
            string summary = $"{repairCount} {repairWord}";

            if (repairsFromBroken > 0)
            {
                string brokenWord = repairsFromBroken == 1 ? "time" : "times";
                summary += $" ({repairsFromBroken} {brokenWord} from broken)";
            }

            return summary;
        }

        /// <summary>
        /// Get attunement-focused summary for UI display.
        /// </summary>
        public string GetAttunementSummary()
        {
            return Attunement.GetSummary();
        }
    }
}
