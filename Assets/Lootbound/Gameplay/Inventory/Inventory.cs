using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Runtime inventory container with a fixed number of slots.
    /// Manages adding, removing, and querying items.
    /// </summary>
    [Serializable]
    public class Inventory
    {
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();
        [SerializeField] private int capacity;

        /// <summary>
        /// Number of slots in this inventory.
        /// </summary>
        public int Capacity => capacity;

        /// <summary>
        /// Read-only access to all slots.
        /// </summary>
        public IReadOnlyList<InventorySlot> Slots => slots;

        /// <summary>
        /// Event fired when inventory contents change.
        /// </summary>
        public event Action OnInventoryChanged;

        /// <summary>
        /// Event fired when a specific slot changes.
        /// </summary>
        public event Action<int> OnSlotChanged;

        public Inventory(int capacity)
        {
            this.capacity = Mathf.Max(1, capacity);
            InitializeSlots();
        }

        private void InitializeSlots()
        {
            slots.Clear();
            for (int i = 0; i < capacity; i++)
            {
                slots.Add(new InventorySlot(i));
            }
        }

        /// <summary>
        /// Get a slot by index.
        /// </summary>
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return null;
            return slots[index];
        }

        /// <summary>
        /// Try to add an item to the inventory.
        /// First tries to stack with existing items, then uses empty slots.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <returns>True if any amount was added.</returns>
        public bool TryAddItem(ItemInstance item)
        {
            if (item == null || !item.IsValid || item.IsEmpty) return false;

            int remaining = item.Quantity;
            bool anyAdded = false;

            // First pass: try to stack with existing items
            if (item.Definition.IsStackable)
            {
                foreach (var slot in slots)
                {
                    if (slot.HasItem && slot.Definition == item.Definition && !slot.Item.IsFull)
                    {
                        int before = remaining;
                        remaining = slot.TryAdd(new ItemInstance(item.Definition, remaining));
                        if (remaining < before)
                        {
                            anyAdded = true;
                            NotifySlotChanged(slot.SlotIndex);
                        }
                        if (remaining <= 0) break;
                    }
                }
            }

            // Second pass: use empty slots
            while (remaining > 0)
            {
                var emptySlot = FindFirstEmptySlot();
                if (emptySlot == null) break;

                int toAdd = Mathf.Min(remaining, item.Definition.MaxStackSize);
                emptySlot.SetItem(new ItemInstance(item.Definition, toAdd));
                remaining -= toAdd;
                anyAdded = true;
                NotifySlotChanged(emptySlot.SlotIndex);
            }

            if (anyAdded)
            {
                OnInventoryChanged?.Invoke();
            }

            return anyAdded;
        }

        /// <summary>
        /// Try to add an item, returning the overflow amount.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <returns>Amount that couldn't be added.</returns>
        public int AddItemWithOverflow(ItemInstance item)
        {
            if (item == null || !item.IsValid || item.IsEmpty) return 0;

            int originalQuantity = item.Quantity;
            TryAddItem(item);
            return originalQuantity - GetItemCount(item.Definition);
        }

        /// <summary>
        /// Remove a quantity of a specific item type.
        /// </summary>
        /// <param name="definition">Item type to remove.</param>
        /// <param name="quantity">Amount to remove.</param>
        /// <returns>Amount actually removed.</returns>
        public int RemoveItem(ItemDefinition definition, int quantity)
        {
            if (definition == null || quantity <= 0) return 0;

            int remaining = quantity;
            int totalRemoved = 0;

            for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = slots[i];
                if (slot.HasItem && slot.Definition == definition)
                {
                    var removed = slot.RemoveQuantity(remaining);
                    if (removed != null)
                    {
                        totalRemoved += removed.Quantity;
                        remaining -= removed.Quantity;
                        NotifySlotChanged(i);
                    }
                }
            }

            if (totalRemoved > 0)
            {
                OnInventoryChanged?.Invoke();
            }

            return totalRemoved;
        }

        /// <summary>
        /// Remove and return item from a specific slot.
        /// </summary>
        public ItemInstance RemoveFromSlot(int slotIndex, int quantity = -1)
        {
            var slot = GetSlot(slotIndex);
            if (slot == null || slot.IsEmpty) return null;

            ItemInstance removed;
            if (quantity < 0 || quantity >= slot.Quantity)
            {
                removed = slot.Clear();
            }
            else
            {
                removed = slot.RemoveQuantity(quantity);
            }

            if (removed != null)
            {
                NotifySlotChanged(slotIndex);
                OnInventoryChanged?.Invoke();
            }

            return removed;
        }

        /// <summary>
        /// Get total count of a specific item type.
        /// </summary>
        public int GetItemCount(ItemDefinition definition)
        {
            if (definition == null) return 0;

            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.HasItem && slot.Definition == definition)
                {
                    count += slot.Quantity;
                }
            }
            return count;
        }

        /// <summary>
        /// Check if inventory contains at least the specified amount.
        /// </summary>
        public bool HasItem(ItemDefinition definition, int quantity = 1)
        {
            return GetItemCount(definition) >= quantity;
        }

        /// <summary>
        /// Find first slot containing the specified item type.
        /// </summary>
        public InventorySlot FindSlotWithItem(ItemDefinition definition)
        {
            foreach (var slot in slots)
            {
                if (slot.HasItem && slot.Definition == definition)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Find first empty slot.
        /// </summary>
        public InventorySlot FindFirstEmptySlot()
        {
            foreach (var slot in slots)
            {
                if (slot.IsEmpty)
                {
                    return slot;
                }
            }
            return null;
        }

        /// <summary>
        /// Count empty slots.
        /// </summary>
        public int GetEmptySlotCount()
        {
            int count = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty) count++;
            }
            return count;
        }

        /// <summary>
        /// Count occupied slots.
        /// </summary>
        public int GetOccupiedSlotCount()
        {
            return capacity - GetEmptySlotCount();
        }

        /// <summary>
        /// Check if inventory is full (no empty slots).
        /// </summary>
        public bool IsFull => GetEmptySlotCount() == 0;

        /// <summary>
        /// Check if inventory is empty.
        /// </summary>
        public bool IsEmpty => GetOccupiedSlotCount() == 0;

        /// <summary>
        /// Swap contents of two slots.
        /// </summary>
        public void SwapSlots(int indexA, int indexB)
        {
            if (indexA == indexB) return;
            if (indexA < 0 || indexA >= slots.Count) return;
            if (indexB < 0 || indexB >= slots.Count) return;

            var slotA = slots[indexA];
            var slotB = slots[indexB];

            var itemA = slotA.Clear();
            var itemB = slotB.Clear();

            if (itemB != null) slotA.SetItem(itemB);
            if (itemA != null) slotB.SetItem(itemA);

            NotifySlotChanged(indexA);
            NotifySlotChanged(indexB);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// Clear all slots.
        /// </summary>
        public void Clear()
        {
            foreach (var slot in slots)
            {
                slot.Clear();
            }
            OnInventoryChanged?.Invoke();
        }

        private void NotifySlotChanged(int index)
        {
            OnSlotChanged?.Invoke(index);
        }

        /// <summary>
        /// Get all unique item types in inventory.
        /// </summary>
        public List<ItemDefinition> GetAllItemTypes()
        {
            var types = new List<ItemDefinition>();
            foreach (var slot in slots)
            {
                if (slot.HasItem && !types.Contains(slot.Definition))
                {
                    types.Add(slot.Definition);
                }
            }
            return types;
        }
    }
}
