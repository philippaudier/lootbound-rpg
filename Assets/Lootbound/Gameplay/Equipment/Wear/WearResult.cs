namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Result of a wear application attempt.
    /// </summary>
    public readonly struct WearResult
    {
        /// <summary>
        /// Whether wear was actually applied.
        /// </summary>
        public bool WearApplied { get; }

        /// <summary>
        /// Amount of durability lost (0 if wear not applied).
        /// </summary>
        public float DurabilityLost { get; }

        /// <summary>
        /// Condition before wear was applied.
        /// </summary>
        public EquipmentCondition ConditionBefore { get; }

        /// <summary>
        /// Condition after wear was applied.
        /// </summary>
        public EquipmentCondition ConditionAfter { get; }

        /// <summary>
        /// Equipment name for display purposes.
        /// </summary>
        public string EquipmentName { get; }

        /// <summary>
        /// Whether the condition changed as a result of wear.
        /// </summary>
        public bool ConditionChanged => WearApplied && ConditionBefore != ConditionAfter;

        /// <summary>
        /// Whether the equipment is now broken.
        /// </summary>
        public bool NowBroken => WearApplied && ConditionAfter == EquipmentCondition.Broken;

        private WearResult(
            bool wearApplied,
            float durabilityLost,
            EquipmentCondition conditionBefore,
            EquipmentCondition conditionAfter,
            string equipmentName)
        {
            WearApplied = wearApplied;
            DurabilityLost = durabilityLost;
            ConditionBefore = conditionBefore;
            ConditionAfter = conditionAfter;
            EquipmentName = equipmentName;
        }

        /// <summary>
        /// Create a result where no wear was applied.
        /// </summary>
        public static WearResult NoWear(EquipmentCondition currentCondition, string equipmentName)
        {
            return new WearResult(false, 0f, currentCondition, currentCondition, equipmentName);
        }

        /// <summary>
        /// Create a result where wear was applied.
        /// </summary>
        public static WearResult Applied(
            float durabilityLost,
            EquipmentCondition conditionBefore,
            EquipmentCondition conditionAfter,
            string equipmentName)
        {
            return new WearResult(true, durabilityLost, conditionBefore, conditionAfter, equipmentName);
        }

        /// <summary>
        /// Create a result for skipped wear (already processed this attack).
        /// </summary>
        public static WearResult Skipped(EquipmentCondition currentCondition, string equipmentName)
        {
            return new WearResult(false, 0f, currentCondition, currentCondition, equipmentName);
        }
    }
}
