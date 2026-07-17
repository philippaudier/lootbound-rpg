using System;
using System.Collections.Generic;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;
using UnityEngine;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Tracks metrics during an expedition.
    /// All values are frozen when the expedition ends (terminal state).
    /// </summary>
    [Serializable]
    public class ExpeditionMetrics
    {
        // Core timing
        [SerializeField] private float duration;
        [SerializeField] private float maxDistance;

        // Combat
        [SerializeField] private int enemiesDefeated;
        private Dictionary<EquipmentData, int> killsByWeapon = new();
        private EquipmentData mainWeapon;
        private int mainWeaponKills;

        // Loot
        [SerializeField] private int itemsAcquired;
        [SerializeField] private int equipmentAcquired;
        [SerializeField] private int resourcesAcquired;
        private Dictionary<string, int> itemCountByType = new();

        // State
        [SerializeField] private bool isFrozen;

        /// <summary>
        /// Total duration of the expedition in seconds.
        /// </summary>
        public float Duration => duration;

        /// <summary>
        /// Maximum horizontal (XZ) distance reached from origin.
        /// </summary>
        public float MaxDistance => maxDistance;

        /// <summary>
        /// Total enemies defeated during this expedition.
        /// </summary>
        public int EnemiesDefeated => enemiesDefeated;

        /// <summary>
        /// Total items acquired (all types).
        /// </summary>
        public int ItemsAcquired => itemsAcquired;

        /// <summary>
        /// Total equipment pieces acquired.
        /// </summary>
        public int EquipmentAcquired => equipmentAcquired;

        /// <summary>
        /// Total resources acquired (consumables, attunement stones, etc.).
        /// </summary>
        public int ResourcesAcquired => resourcesAcquired;

        /// <summary>
        /// The weapon that killed the most enemies during this expedition.
        /// Null if no enemies were killed.
        /// </summary>
        public EquipmentData MainWeapon => mainWeapon;

        /// <summary>
        /// Number of kills with the main weapon.
        /// </summary>
        public int MainWeaponKills => mainWeaponKills;

        /// <summary>
        /// Whether metrics are frozen (expedition ended).
        /// </summary>
        public bool IsFrozen => isFrozen;

        /// <summary>
        /// Duration formatted as MM:SS.
        /// </summary>
        public string DurationFormatted
        {
            get
            {
                int totalSeconds = Mathf.FloorToInt(duration);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return $"{minutes:D2}:{seconds:D2}";
            }
        }

        /// <summary>
        /// Update duration. Called every frame while tracking.
        /// </summary>
        public void UpdateDuration(float deltaTime)
        {
            if (isFrozen) return;
            duration += deltaTime;
        }

        /// <summary>
        /// Update max distance if current position is farther from origin.
        /// </summary>
        /// <param name="position">Current player position.</param>
        /// <param name="origin">Expedition origin position.</param>
        public void UpdateMaxDistance(Vector3 position, Vector3 origin)
        {
            if (isFrozen) return;

            // Horizontal distance only (XZ plane)
            float dx = position.x - origin.x;
            float dz = position.z - origin.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);

            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        /// <summary>
        /// Record an enemy kill, optionally tracking which weapon was used.
        /// </summary>
        /// <param name="weapon">Weapon used for the kill, or null if unknown.</param>
        public void RecordKill(EquipmentData weapon = null)
        {
            if (isFrozen) return;

            enemiesDefeated++;

            if (weapon != null)
            {
                if (!killsByWeapon.TryGetValue(weapon, out int kills))
                {
                    kills = 0;
                }
                kills++;
                killsByWeapon[weapon] = kills;

                // Update main weapon if this weapon has more kills
                if (kills > mainWeaponKills)
                {
                    mainWeapon = weapon;
                    mainWeaponKills = kills;
                }
            }
        }

        /// <summary>
        /// Record an item acquisition.
        /// </summary>
        /// <param name="definition">Item definition acquired.</param>
        /// <param name="quantity">Amount acquired.</param>
        /// <param name="isEquipment">Whether this is an equipment piece.</param>
        public void RecordItemAcquired(ItemDefinition definition, int quantity, bool isEquipment)
        {
            if (isFrozen) return;
            if (definition == null || quantity <= 0) return;

            itemsAcquired += quantity;

            if (isEquipment)
            {
                equipmentAcquired += quantity;
            }
            else
            {
                resourcesAcquired += quantity;
            }

            // Track by type
            string typeId = definition.ItemId;
            if (!itemCountByType.TryGetValue(typeId, out int count))
            {
                count = 0;
            }
            itemCountByType[typeId] = count + quantity;
        }

        /// <summary>
        /// Record an equipment acquisition.
        /// </summary>
        public void RecordEquipmentAcquired()
        {
            if (isFrozen) return;

            itemsAcquired++;
            equipmentAcquired++;
        }

        /// <summary>
        /// Get count of a specific item type acquired during this expedition.
        /// </summary>
        public int GetItemCountByType(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return itemCountByType.TryGetValue(itemId, out int count) ? count : 0;
        }

        /// <summary>
        /// Get kills for a specific weapon.
        /// </summary>
        public int GetKillsForWeapon(EquipmentData weapon)
        {
            if (weapon == null) return 0;
            return killsByWeapon.TryGetValue(weapon, out int kills) ? kills : 0;
        }

        /// <summary>
        /// Freeze metrics. Called when expedition ends.
        /// </summary>
        public void Freeze()
        {
            isFrozen = true;
        }

        /// <summary>
        /// Reset all metrics for a new expedition.
        /// </summary>
        public void Reset()
        {
            duration = 0f;
            maxDistance = 0f;
            enemiesDefeated = 0;
            itemsAcquired = 0;
            equipmentAcquired = 0;
            resourcesAcquired = 0;
            mainWeapon = null;
            mainWeaponKills = 0;
            killsByWeapon.Clear();
            itemCountByType.Clear();
            isFrozen = false;
        }

        public override string ToString()
        {
            return $"[Metrics] {DurationFormatted}, {maxDistance:F1}m, {enemiesDefeated} kills, {itemsAcquired} items";
        }
    }
}
