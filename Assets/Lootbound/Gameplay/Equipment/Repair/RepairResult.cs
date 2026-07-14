namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Result of a repair operation after execution.
    /// </summary>
    public readonly struct RepairResult
    {
        /// <summary>Whether the repair was successful.</summary>
        public readonly bool Success;

        /// <summary>Reason for failure, if applicable.</summary>
        public readonly RepairFailureReason FailureReason;

        /// <summary>Name of the equipment that was repaired.</summary>
        public readonly string EquipmentName;

        /// <summary>Durability before repair.</summary>
        public readonly float DurabilityBefore;

        /// <summary>Durability after repair.</summary>
        public readonly float DurabilityAfter;

        /// <summary>Maximum durability of the equipment.</summary>
        public readonly float MaxDurability;

        /// <summary>Condition before repair.</summary>
        public readonly EquipmentCondition ConditionBefore;

        /// <summary>Condition after repair.</summary>
        public readonly EquipmentCondition ConditionAfter;

        /// <summary>Number of fragments consumed.</summary>
        public readonly int FragmentsConsumed;

        /// <summary>
        /// Create a successful repair result.
        /// </summary>
        public RepairResult(
            string equipmentName,
            float durabilityBefore,
            float durabilityAfter,
            float maxDurability,
            EquipmentCondition conditionBefore,
            EquipmentCondition conditionAfter,
            int fragmentsConsumed)
        {
            Success = true;
            FailureReason = RepairFailureReason.None;
            EquipmentName = equipmentName;
            DurabilityBefore = durabilityBefore;
            DurabilityAfter = durabilityAfter;
            MaxDurability = maxDurability;
            ConditionBefore = conditionBefore;
            ConditionAfter = conditionAfter;
            FragmentsConsumed = fragmentsConsumed;
        }

        /// <summary>
        /// Create a failed repair result.
        /// </summary>
        public RepairResult(RepairFailureReason failureReason, string equipmentName = "")
        {
            Success = false;
            FailureReason = failureReason;
            EquipmentName = equipmentName;
            DurabilityBefore = 0f;
            DurabilityAfter = 0f;
            MaxDurability = 0f;
            ConditionBefore = EquipmentCondition.Broken;
            ConditionAfter = EquipmentCondition.Broken;
            FragmentsConsumed = 0;
        }

        /// <summary>
        /// Whether the repair changed the equipment's condition tier.
        /// </summary>
        public bool ConditionChanged => ConditionBefore != ConditionAfter;

        /// <summary>
        /// Amount of durability restored.
        /// </summary>
        public float DurabilityRestored => DurabilityAfter - DurabilityBefore;

        /// <summary>
        /// Whether the equipment was restored from broken state.
        /// </summary>
        public bool RestoredFromBroken => ConditionBefore == EquipmentCondition.Broken && ConditionAfter != EquipmentCondition.Broken;

        /// <summary>
        /// Normalized durability after repair (0-1).
        /// </summary>
        public float NormalizedDurabilityAfter => MaxDurability > 0f ? DurabilityAfter / MaxDurability : 0f;
    }
}
