using System;
using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Represents a single slot in an inventory.
    /// Can hold one ItemInstance or be empty.
    /// </summary>
    [Serializable]
    public class InventorySlot
    {
        [SerializeField] private ItemInstance item;
        [SerializeField] private int slotIndex;

        /// <summary>
        /// The item in this slot, or null if empty.
        /// </summary>
        public ItemInstance Item => item;

        /// <summary>
        /// Index of this slot in the inventory.
        /// </summary>
        public int SlotIndex => slotIndex;

        /// <summary>
        /// Whether this slot is empty.
        /// </summary>
        public bool IsEmpty => item == null || !item.IsValid || item.IsEmpty;

        /// <summary>
        /// Whether this slot has a valid item.
        /// </summary>
        public bool HasItem => !IsEmpty;

        /// <summary>
        /// The item definition in this slot, or null if empty.
        /// </summary>
        public ItemDefinition Definition => item?.Definition;

        /// <summary>
        /// Quantity of items in this slot.
        /// </summary>
        public int Quantity => item?.Quantity ?? 0;

        public InventorySlot(int index)
        {
            slotIndex = index;
            item = null;
        }

        /// <summary>
        /// Set the item in this slot.
        /// </summary>
        public void SetItem(ItemInstance newItem)
        {
            item = newItem;
        }

        /// <summary>
        /// Clear this slot.
        /// </summary>
        public ItemInstance Clear()
        {
            var removed = item;
            item = null;
            return removed;
        }

        /// <summary>
        /// Check if this slot can accept the given item.
        /// </summary>
        public bool CanAccept(ItemInstance incoming)
        {
            if (incoming == null || !incoming.IsValid) return false;

            // Empty slot can always accept
            if (IsEmpty) return true;

            // Same item type and stackable - check if there's room
            if (item.Definition == incoming.Definition && !item.IsFull)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try to add an item to this slot.
        /// </summary>
        /// <param name="incoming">Item to add.</param>
        /// <returns>Amount that couldn't be added.</returns>
        public int TryAdd(ItemInstance incoming)
        {
            if (incoming == null || !incoming.IsValid) return 0;

            // Empty slot - place item directly
            if (IsEmpty)
            {
                item = incoming.Clone();
                return 0;
            }

            // Try to merge with existing stack
            if (item.Definition == incoming.Definition)
            {
                int toAdd = Mathf.Min(incoming.Quantity, item.RemainingSpace);
                if (toAdd > 0)
                {
                    item.Add(toAdd);
                    return incoming.Quantity - toAdd;
                }
            }

            return incoming.Quantity;
        }

        /// <summary>
        /// Remove a quantity of items from this slot.
        /// </summary>
        /// <param name="amount">Amount to remove.</param>
        /// <returns>Removed item instance, or null if nothing removed.</returns>
        public ItemInstance RemoveQuantity(int amount)
        {
            if (IsEmpty || amount <= 0) return null;

            int removed = item.Remove(amount);
            if (removed <= 0) return null;

            var result = new ItemInstance(item.Definition, removed);

            // Clear slot if now empty
            if (item.IsEmpty)
            {
                item = null;
            }

            return result;
        }

        public override string ToString()
        {
            return IsEmpty ? $"Slot[{slotIndex}]: Empty" : $"Slot[{slotIndex}]: {item}";
        }
    }
}
