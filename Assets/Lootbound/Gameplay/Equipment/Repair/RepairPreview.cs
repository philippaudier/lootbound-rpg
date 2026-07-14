namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Preview of a repair operation before committing.
    /// Shows what the repair would do without modifying any state.
    /// </summary>
    public readonly struct RepairPreview
    {
        /// <summary>Whether the repair can be performed.</summary>
        public readonly bool CanRepair;

        /// <summary>Reason why repair cannot be performed, if applicable.</summary>
        public readonly RepairFailureReason FailureReason;

        /// <summary>Current durability before repair.</summary>
        public readonly float CurrentDurability;

        /// <summary>Maximum durability of the equipment.</summary>
        public readonly float MaxDurability;

        /// <summary>Durability after repair (if repair proceeds).</summary>
        public readonly float DurabilityAfterRepair;

        /// <summary>Condition before repair.</summary>
        public readonly EquipmentCondition ConditionBefore;

        /// <summary>Condition after repair (if repair proceeds).</summary>
        public readonly EquipmentCondition ConditionAfter;

        /// <summary>Number of fragments available in inventory.</summary>
        public readonly int FragmentsAvailable;

        /// <summary>Fragments needed for full repair.</summary>
        public readonly int FragmentsForFullRepair;

        /// <summary>Fragments that would be consumed for this repair.</summary>
        public readonly int FragmentsToConsume;

        /// <summary>
        /// Create a successful repair preview.
        /// </summary>
        public RepairPreview(
            float currentDurability,
            float maxDurability,
            float durabilityAfterRepair,
            EquipmentCondition conditionBefore,
            EquipmentCondition conditionAfter,
            int fragmentsAvailable,
            int fragmentsForFullRepair,
            int fragmentsToConsume)
        {
            CanRepair = true;
            FailureReason = RepairFailureReason.None;
            CurrentDurability = currentDurability;
            MaxDurability = maxDurability;
            DurabilityAfterRepair = durabilityAfterRepair;
            ConditionBefore = conditionBefore;
            ConditionAfter = conditionAfter;
            FragmentsAvailable = fragmentsAvailable;
            FragmentsForFullRepair = fragmentsForFullRepair;
            FragmentsToConsume = fragmentsToConsume;
        }

        /// <summary>
        /// Create a failed repair preview.
        /// </summary>
        public RepairPreview(RepairFailureReason failureReason, float currentDurability = 0f, float maxDurability = 0f, int fragmentsAvailable = 0)
        {
            CanRepair = false;
            FailureReason = failureReason;
            CurrentDurability = currentDurability;
            MaxDurability = maxDurability;
            DurabilityAfterRepair = currentDurability;
            ConditionBefore = EquipmentConditionHelper.GetCondition(maxDurability > 0f ? currentDurability / maxDurability : 0f);
            ConditionAfter = ConditionBefore;
            FragmentsAvailable = fragmentsAvailable;
            FragmentsForFullRepair = 0;
            FragmentsToConsume = 0;
        }

        /// <summary>
        /// Whether the repair will change the equipment's condition tier.
        /// </summary>
        public bool WillChangeCondition => ConditionBefore != ConditionAfter;

        /// <summary>
        /// Whether this is a full repair (reaching max durability).
        /// </summary>
        public bool IsFullRepair => DurabilityAfterRepair >= MaxDurability;

        /// <summary>
        /// Amount of durability that will be restored.
        /// </summary>
        public float DurabilityRestored => DurabilityAfterRepair - CurrentDurability;
    }
}
