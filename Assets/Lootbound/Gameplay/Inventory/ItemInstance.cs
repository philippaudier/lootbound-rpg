using System;
using UnityEngine;
using Lootbound.Gameplay.Equipment;

namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Runtime instance of an item. Mutable state like quantity lives here.
    /// References an immutable ItemDefinition for static properties.
    /// Equipment items also contain EquipmentData for unique identity.
    /// </summary>
    [Serializable]
    public class ItemInstance
    {
        [SerializeField] private ItemDefinition definition;
        [SerializeField] private int quantity;
        [SerializeField] private EquipmentData equipmentData;

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
        /// Whether this item has equipment data (is a unique equipment piece).
        /// </summary>
        public bool HasEquipmentData => equipmentData != null && equipmentData.IsValid;

        /// <summary>
        /// The equipment data for this item, if any.
        /// </summary>
        public EquipmentData EquipmentData => equipmentData;

        /// <summary>
        /// Whether this equipment is currently equipped.
        /// </summary>
        public bool IsEquipped => equipmentData?.IsEquipped ?? false;

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
            this.equipmentData = null;
        }

        /// <summary>
        /// Create a new item instance with equipment data.
        /// </summary>
        public ItemInstance(ItemDefinition definition, EquipmentData equipmentData)
        {
            this.definition = definition;
            this.quantity = 1; // Equipment is always quantity 1
            this.equipmentData = equipmentData;
        }

        /// <summary>
        /// Set equipment data on this instance.
        /// Should only be called once when the equipment is created.
        /// </summary>
        public void SetEquipmentData(EquipmentData data)
        {
            this.equipmentData = data;
        }

        /// <summary>
        /// Add quantity to this stack.
        /// Equipment items cannot have quantity added.
        /// </summary>
        /// <param name="amount">Amount to add.</param>
        /// <returns>Amount that couldn't be added (overflow).</returns>
        public int Add(int amount)
        {
            if (amount <= 0 || definition == null) return amount;

            // Equipment items cannot be stacked
            if (HasEquipmentData) return amount;

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
        /// Equipment items cannot be merged.
        /// </summary>
        /// <param name="other">Instance to merge from.</param>
        /// <returns>True if any items were transferred.</returns>
        public bool TryMerge(ItemInstance other)
        {
            if (other == null || !other.IsValid) return false;
            if (definition == null) return false;
            if (other.definition != definition) return false;
            if (IsFull) return false;

            // Equipment items cannot be merged
            if (HasEquipmentData || other.HasEquipmentData) return false;

            int toTransfer = Mathf.Min(other.quantity, RemainingSpace);
            if (toTransfer <= 0) return false;

            quantity += toTransfer;
            other.quantity -= toTransfer;
            return true;
        }

        /// <summary>
        /// Split this stack into two.
        /// Equipment items cannot be split.
        /// </summary>
        /// <param name="splitAmount">Amount to split off.</param>
        /// <returns>New instance with split amount, or null if split failed.</returns>
        public ItemInstance Split(int splitAmount)
        {
            if (definition == null) return null;
            if (splitAmount <= 0 || splitAmount >= quantity) return null;

            // Equipment items cannot be split
            if (HasEquipmentData) return null;

            quantity -= splitAmount;
            return new ItemInstance(definition, splitAmount);
        }

        /// <summary>
        /// Create a copy of this instance.
        /// Equipment data is preserved (same GUID reference).
        /// </summary>
        public ItemInstance Clone()
        {
            var clone = new ItemInstance(definition, quantity);
            if (equipmentData != null)
            {
                // Clone preserves the same equipment data (same GUID)
                clone.equipmentData = equipmentData.Clone();
            }
            return clone;
        }

        /// <summary>
        /// Create a copy with new equipment identity.
        /// Only valid for equipment items.
        /// </summary>
        public ItemInstance CloneAsNewEquipment()
        {
            if (equipmentData == null) return Clone();

            var clone = new ItemInstance(definition, quantity);
            clone.equipmentData = equipmentData.CloneWithNewId();
            return clone;
        }

        public override string ToString()
        {
            if (definition == null) return "Empty";

            if (HasEquipmentData)
            {
                return $"{equipmentData.CustomName} ({equipmentData.Rarity})";
            }

            return $"{definition.DisplayName} x{quantity}";
        }
    }
}
