namespace Lootbound.Gameplay.Inventory
{
    /// <summary>
    /// Result of an inventory add operation.
    /// Provides detailed information about what was added and what overflowed.
    /// </summary>
    public readonly struct AddItemResult
    {
        /// <summary>
        /// Quantity requested to add.
        /// </summary>
        public int Requested { get; }

        /// <summary>
        /// Quantity actually added to inventory.
        /// </summary>
        public int Added { get; }

        /// <summary>
        /// Quantity that could not be added (overflow).
        /// </summary>
        public int Overflow { get; }

        /// <summary>
        /// True if all requested items were added.
        /// </summary>
        public bool IsComplete => Overflow == 0 && Added > 0;

        /// <summary>
        /// True if some but not all items were added.
        /// </summary>
        public bool IsPartial => Added > 0 && Overflow > 0;

        /// <summary>
        /// True if no items could be added (inventory full).
        /// </summary>
        public bool IsFailed => Added == 0;

        /// <summary>
        /// True if at least one item was added.
        /// </summary>
        public bool AnyAdded => Added > 0;

        public AddItemResult(int requested, int added)
        {
            Requested = requested;
            Added = added;
            Overflow = requested - added;
        }

        public override string ToString()
        {
            return $"AddItemResult(Requested={Requested}, Added={Added}, Overflow={Overflow})";
        }
    }
}
