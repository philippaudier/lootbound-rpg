using System;
using UnityEngine;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Runtime instance of an item. Mutable state like quantity lives here.
    /// References an immutable ItemDefinition for static properties.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int quantity;

        /// <summary>
        /// The item definition this instance is based on.
        /// </summary>
        public ItemDefinition Definition => definition;

        /// <summary>
        /// Current stack quantity.
        /// </summary>
        public int Quantity
        {
            get => quantity;
            private set => quantity = Mathf.Clamp(value, 0, MaxStackSize);
        }

        /// <summary>
        /// Maximum stack size from definition.
        /// </summary>
        public int MaxStackSize => definition != null ? definition.MaxStackSize : 1;

        /// <summary>
        /// Whether this instance is valid (has a definition).
        /// </summary>
        public bool IsValid => definition != null;

        /// <summary>
        /// Whether this stack is full.
        /// </summary>
        public bool IsFull => quantity >= MaxStackSize;

        /// <summary>
        /// Whether this stack is empty.
        /// </summary>
        public bool IsEmpty => quantity <= 0;

        /// <summary>
        /// Remaining space in this stack.
        /// </summary>
        public int RemainingSpace => MaxStackSize - quantity;

        /// <summary>
        /// Create a new item instance.
        /// </summary>
        public ItemInstance(ItemDefinition definition, int quantity = 1)
        {
            this.definition = definition;
            this.quantity = definition != null
                ? Mathf.Clamp(quantity, 1, definition.MaxStackSize)
                : 0;
        }

        /// <summary>
        /// Add quantity to this stack.
        /// </summary>
        /// <param name="amount">Amount to add.</param>
        /// <returns>Amount that couldn't be added (overflow).</returns>
        public int Add(int amount)
        {
            if (amount <= 0 || definition == null) return amount;

            int canAdd = Mathf.Min(amount, RemainingSpace);
            quantity += canAdd;
            return amount - canAdd;
        }

        /// <summary>
        /// Remove quantity from this stack.
        /// </summary>
        /// <param name="amount">Amount to remove.</param>
        /// <returns>Amount actually removed.</returns>
        public int Remove(int amount)
        {
            if (amount <= 0) return 0;

            int canRemove = Mathf.Min(amount, quantity);
            quantity -= canRemove;
            return canRemove;
        }

        /// <summary>
        /// Try to merge another instance into this one.
        /// </summary>
        /// <param name="other">Instance to merge from.</param>
        /// <returns>True if any items were transferred.</returns>
        public bool TryMerge(ItemInstance other)
        {
            if (other == null || !other.IsValid) return false;
            if (definition == null) return false;
            if (other.definition != definition) return false;
            if (IsFull) return false;

            int toTransfer = Mathf.Min(other.quantity, RemainingSpace);
            if (toTransfer <= 0) return false;

            quantity += toTransfer;
            other.quantity -= toTransfer;
            return true;
        }

        /// <summary>
        /// Split this stack into two.
        /// </summary>
        /// <param name="splitAmount">Amount to split off.</param>
        /// <returns>New instance with split amount, or null if split failed.</returns>
        public ItemInstance Split(int splitAmount)
        {
            if (definition == null) return null;
            if (splitAmount <= 0 || splitAmount >= quantity) return null;

            quantity -= splitAmount;
            return new ItemInstance(definition, splitAmount);
        }

        /// <summary>
        /// Create a copy of this instance.
        /// </summary>
        public ItemInstance Clone()
        {
            return new ItemInstance(definition, quantity);
        }

        public override string ToString()
        {
            if (definition == null) return "Empty";
            return $"{definition.DisplayName} x{quantity}";
        }
    }
}
