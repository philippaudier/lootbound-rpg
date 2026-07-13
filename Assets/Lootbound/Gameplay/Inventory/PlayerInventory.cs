using System;
using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Player inventory component. Manages the player's inventory state
    /// and provides the interface for item operations.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private InventoryConfig config;

        private Inventory inventory;

        /// <summary>
        /// The player's inventory instance.
        /// </summary>
        public Inventory Inventory => inventory;

        /// <summary>
        /// Configuration for this inventory.
        /// </summary>
        public InventoryConfig Config => config;

        /// <summary>
        /// Event fired when an item is added.
        /// </summary>
        public event Action<ItemDefinition, int> OnItemAdded;

        /// <summary>
        /// Event fired when an item is removed.
        /// </summary>
        public event Action<ItemDefinition, int> OnItemRemoved;

        /// <summary>
        /// Event fired when inventory could not accept an item (full).
        /// </summary>
        public event Action<ItemDefinition, int> OnInventoryFull;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[PlayerInventory] InventoryConfig is not assigned!");
                return;
            }

            inventory = config.CreateInventory();
        }

        /// <summary>
        /// Try to add an item to the inventory.
        /// </summary>
        /// <param name="definition">Item type to add.</param>
        /// <param name="quantity">Amount to add.</param>
        /// <returns>Amount actually added.</returns>
        public int AddItem(ItemDefinition definition, int quantity = 1)
        {
            if (definition == null || quantity <= 0) return 0;
            if (inventory == null) return 0;

            int before = inventory.GetItemCount(definition);
            var instance = definition.CreateInstance(quantity);
            inventory.TryAddItem(instance);
            int after = inventory.GetItemCount(definition);

            int added = after - before;
            int overflow = quantity - added;

            if (added > 0)
            {
                OnItemAdded?.Invoke(definition, added);
            }

            if (overflow > 0)
            {
                OnInventoryFull?.Invoke(definition, overflow);
            }

            return added;
        }

        /// <summary>
        /// Try to add an item instance to the inventory.
        /// </summary>
        /// <param name="item">Item instance to add.</param>
        /// <returns>True if any amount was added.</returns>
        public bool AddItem(ItemInstance item)
        {
            if (item == null || !item.IsValid) return false;
            if (inventory == null) return false;

            int added = AddItem(item.Definition, item.Quantity);
            return added > 0;
        }

        /// <summary>
        /// Remove an item from the inventory.
        /// </summary>
        /// <param name="definition">Item type to remove.</param>
        /// <param name="quantity">Amount to remove.</param>
        /// <returns>Amount actually removed.</returns>
        public int RemoveItem(ItemDefinition definition, int quantity = 1)
        {
            if (definition == null || quantity <= 0) return 0;
            if (inventory == null) return 0;

            int removed = inventory.RemoveItem(definition, quantity);

            if (removed > 0)
            {
                OnItemRemoved?.Invoke(definition, removed);
            }

            return removed;
        }

        /// <summary>
        /// Check if inventory has the specified item.
        /// </summary>
        public bool HasItem(ItemDefinition definition, int quantity = 1)
        {
            if (inventory == null) return false;
            return inventory.HasItem(definition, quantity);
        }

        /// <summary>
        /// Get count of a specific item type.
        /// </summary>
        public int GetItemCount(ItemDefinition definition)
        {
            if (inventory == null) return 0;
            return inventory.GetItemCount(definition);
        }

        /// <summary>
        /// Check if inventory is full.
        /// </summary>
        public bool IsFull => inventory?.IsFull ?? true;

        /// <summary>
        /// Check if inventory is empty.
        /// </summary>
        public bool IsEmpty => inventory?.IsEmpty ?? true;

        /// <summary>
        /// Get number of empty slots.
        /// </summary>
        public int EmptySlotCount => inventory?.GetEmptySlotCount() ?? 0;
    }
}
