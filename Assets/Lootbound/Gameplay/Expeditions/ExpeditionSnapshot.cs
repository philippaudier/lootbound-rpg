using System;
using System.Collections.Generic;
using Lootbound.Gameplay.Equipment;
using Lootbound.Gameplay.Inventory;
using UnityEngine;

namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Captures the state at expedition departure.
    /// Immutable after creation.
    /// </summary>
    [Serializable]
    public class ExpeditionSnapshot
    {
        [SerializeField] private string equippedWeaponId;
        [SerializeField] private string equippedWeaponName;
        [SerializeField] private int equippedWeaponAttunement;

        // Resource counts at departure
        private readonly Dictionary<string, int> resourceCounts = new();

        /// <summary>
        /// Instance ID of the equipped weapon at departure.
        /// Empty if no weapon was equipped.
        /// </summary>
        public string EquippedWeaponId => equippedWeaponId;

        /// <summary>
        /// Display name of the equipped weapon at departure.
        /// </summary>
        public string EquippedWeaponName => equippedWeaponName;

        /// <summary>
        /// Attunement level of the equipped weapon at departure.
        /// </summary>
        public int EquippedWeaponAttunement => equippedWeaponAttunement;

        /// <summary>
        /// Whether a weapon was equipped at departure.
        /// </summary>
        public bool HadWeaponEquipped => !string.IsNullOrEmpty(equippedWeaponId);

        private ExpeditionSnapshot() { }

        /// <summary>
        /// Capture the current state for an expedition snapshot.
        /// </summary>
        /// <param name="equipment">Player's equipment component.</param>
        /// <param name="inventory">Player's inventory component.</param>
        /// <returns>New snapshot instance.</returns>
        public static ExpeditionSnapshot Capture(PlayerEquipment equipment, PlayerInventory inventory)
        {
            var snapshot = new ExpeditionSnapshot();

            // Capture equipped weapon
            if (equipment != null && equipment.CurrentEquipment != null)
            {
                var weapon = equipment.CurrentEquipment;
                snapshot.equippedWeaponId = weapon.InstanceId;
                snapshot.equippedWeaponName = weapon.CustomName;
                snapshot.equippedWeaponAttunement = weapon.AttunementLevel;
            }
            else
            {
                snapshot.equippedWeaponId = string.Empty;
                snapshot.equippedWeaponName = string.Empty;
                snapshot.equippedWeaponAttunement = 0;
            }

            // Capture resource counts from inventory
            // Note: In V1, we just track that a snapshot was taken.
            // Detailed resource tracking can be added in V2 if needed.
            if (inventory?.Inventory != null)
            {
                foreach (var slot in inventory.Inventory.Slots)
                {
                    if (slot.HasItem && slot.Definition != null)
                    {
                        string id = slot.Definition.ItemId;
                        if (!snapshot.resourceCounts.ContainsKey(id))
                        {
                            snapshot.resourceCounts[id] = 0;
                        }
                        snapshot.resourceCounts[id] += slot.Quantity;
                    }
                }
            }

            return snapshot;
        }

        /// <summary>
        /// Create an empty snapshot (for testing or when player has nothing).
        /// </summary>
        public static ExpeditionSnapshot Empty()
        {
            return new ExpeditionSnapshot
            {
                equippedWeaponId = string.Empty,
                equippedWeaponName = string.Empty,
                equippedWeaponAttunement = 0
            };
        }

        /// <summary>
        /// Get resource count at departure.
        /// </summary>
        public int GetResourceCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return resourceCounts.TryGetValue(itemId, out int count) ? count : 0;
        }

        public override string ToString()
        {
            if (HadWeaponEquipped)
            {
                string attunement = equippedWeaponAttunement > 0 ? $" +{equippedWeaponAttunement}" : "";
                return $"[Snapshot] Weapon: {equippedWeaponName}{attunement}";
            }
            return "[Snapshot] No weapon equipped";
        }
    }
}
