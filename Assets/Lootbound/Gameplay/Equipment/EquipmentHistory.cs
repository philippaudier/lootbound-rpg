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
        /// Create a new history for equipment found now.
        /// </summary>
        public EquipmentHistory(string location)
        {
            foundLocation = location ?? "Unknown";
            foundTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            enemiesDefeated = 0;
            timesEquipped = 0;
        }

        /// <summary>
        /// Create history from serialized data.
        /// </summary>
        public EquipmentHistory(string location, long timestamp, int kills, int equips)
        {
            foundLocation = location ?? "Unknown";
            foundTimestamp = timestamp;
            enemiesDefeated = kills;
            timesEquipped = equips;
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
            return new EquipmentHistory(foundLocation, foundTimestamp, enemiesDefeated, timesEquipped);
        }
    }
}
