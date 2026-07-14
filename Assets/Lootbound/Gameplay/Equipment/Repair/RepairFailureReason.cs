namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Reasons why a repair operation cannot be performed.
    /// </summary>
    public enum RepairFailureReason
    {
        /// <summary>No failure - repair can proceed.</summary>
        None,

        /// <summary>No equipment is selected for repair.</summary>
        NoEquipmentSelected,

        /// <summary>Equipment is already at maximum durability.</summary>
        AlreadyFullDurability,

        /// <summary>Player does not have any repair fragments.</summary>
        NoFragmentsAvailable,

        /// <summary>Player does not have enough repair fragments for the requested repair.</summary>
        InsufficientFragments,

        /// <summary>Equipment is broken and repair config does not allow broken repair.</summary>
        BrokenRepairNotAllowed,

        /// <summary>Invalid repair configuration.</summary>
        InvalidConfig,

        /// <summary>Fragment removal from inventory failed unexpectedly.</summary>
        InventoryTransactionFailed
    }
}
