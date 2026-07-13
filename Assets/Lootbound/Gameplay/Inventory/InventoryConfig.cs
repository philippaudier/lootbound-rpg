using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Configuration for player inventory.
    /// </summary>
    [CreateAssetMenu(fileName = "InventoryConfig", menuName = "Lootbound/Inventory/Inventory Config")]
    public class InventoryConfig : ScriptableObject
    {
        [Header("Capacity")]
        [Tooltip("Number of inventory slots.")]
        [SerializeField, Range(1, 100)] private int slotCount = 20;

        [Header("UI Layout")]
        [Tooltip("Number of columns in the inventory grid.")]
        [SerializeField, Range(1, 10)] private int gridColumns = 5;

        [Header("Notifications")]
        [Tooltip("Show pickup notifications.")]
        [SerializeField] private bool showPickupNotifications = true;

        [Tooltip("Duration for pickup notification display.")]
        [SerializeField, Range(1f, 10f)] private float notificationDuration = 3f;

        [Tooltip("Maximum concurrent notifications.")]
        [SerializeField, Range(1, 10)] private int maxNotifications = 5;

        // Public accessors
        public int SlotCount => slotCount;
        public int GridColumns => gridColumns;
        public int GridRows => Mathf.CeilToInt((float)slotCount / gridColumns);
        public bool ShowPickupNotifications => showPickupNotifications;
        public float NotificationDuration => notificationDuration;
        public int MaxNotifications => maxNotifications;

        /// <summary>
        /// Create a new inventory with this configuration.
        /// </summary>
        public Inventory CreateInventory()
        {
            return new Inventory(slotCount);
        }

        /// <summary>
        /// Validate configuration.
        /// </summary>
        public bool Validate(out string error)
        {
            if (slotCount <= 0)
            {
                error = "Slot count must be positive.";
                return false;
            }

            if (gridColumns <= 0)
            {
                error = "Grid columns must be positive.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
