using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Centralized helper for equipment condition calculations.
    /// Contains thresholds, colors, and tooltip text.
    /// </summary>
    public static class EquipmentConditionHelper
    {
        // Condition thresholds (percentage values)
        private const float ExcellentThreshold = 0.80f;  // 80-100%
        private const float GoodThreshold = 0.60f;       // 60-79%
        private const float WornThreshold = 0.35f;       // 35-59%
        private const float FragileThreshold = 0.01f;    // 1-34%
        // Below 1% or exactly 0% = Broken

        // Condition colors (sober, not saturated MMO colors)
        private static readonly Color ExcellentColor = new Color(0.7f, 0.75f, 0.85f);   // Slightly bluish gray
        private static readonly Color GoodColor = new Color(0.55f, 0.75f, 0.55f);       // Soft green
        private static readonly Color WornColor = new Color(0.8f, 0.7f, 0.45f);         // Ochre
        private static readonly Color FragileColor = new Color(0.9f, 0.6f, 0.35f);      // Orange
        private static readonly Color BrokenColor = new Color(0.65f, 0.25f, 0.25f);     // Deep red

        /// <summary>
        /// Calculate condition from normalized durability (0-1).
        /// </summary>
        public static EquipmentCondition GetCondition(float normalizedDurability)
        {
            if (normalizedDurability <= 0f)
                return EquipmentCondition.Broken;
            if (normalizedDurability < FragileThreshold)
                return EquipmentCondition.Broken;
            if (normalizedDurability < WornThreshold)
                return EquipmentCondition.Fragile;
            if (normalizedDurability < GoodThreshold)
                return EquipmentCondition.Worn;
            if (normalizedDurability < ExcellentThreshold)
                return EquipmentCondition.Good;

            return EquipmentCondition.Excellent;
        }

        /// <summary>
        /// Calculate condition from current and max durability.
        /// </summary>
        public static EquipmentCondition GetCondition(float current, float max)
        {
            if (max <= 0f)
                return EquipmentCondition.Broken;

            return GetCondition(current / max);
        }

        /// <summary>
        /// Get the display color for a condition.
        /// </summary>
        public static Color GetConditionColor(EquipmentCondition condition)
        {
            return condition switch
            {
                EquipmentCondition.Excellent => ExcellentColor,
                EquipmentCondition.Good => GoodColor,
                EquipmentCondition.Worn => WornColor,
                EquipmentCondition.Fragile => FragileColor,
                EquipmentCondition.Broken => BrokenColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// Get the tooltip text for a condition.
        /// </summary>
        public static string GetConditionTooltip(EquipmentCondition condition)
        {
            return condition switch
            {
                EquipmentCondition.Excellent => "This equipment is in excellent condition.",
                EquipmentCondition.Good => "Shows signs of regular use.",
                EquipmentCondition.Worn => "The equipment has seen many journeys.",
                EquipmentCondition.Fragile => "It may not endure many more battles.",
                EquipmentCondition.Broken => "This equipment is broken and needs repair.",
                _ => ""
            };
        }

        /// <summary>
        /// Get the minimum percentage for a condition.
        /// </summary>
        public static float GetConditionMinPercentage(EquipmentCondition condition)
        {
            return condition switch
            {
                EquipmentCondition.Excellent => ExcellentThreshold,
                EquipmentCondition.Good => GoodThreshold,
                EquipmentCondition.Worn => WornThreshold,
                EquipmentCondition.Fragile => FragileThreshold,
                EquipmentCondition.Broken => 0f,
                _ => 0f
            };
        }

        /// <summary>
        /// Check if condition allows combat use.
        /// All conditions allow combat use, but Broken equipment has severe penalties.
        /// </summary>
        public static bool CanUseInCombat(EquipmentCondition condition)
        {
            // All conditions allow combat use
            // Broken weapons are still usable but with severe stat penalties
            return true;
        }

        /// <summary>
        /// Check if the condition indicates the equipment is broken.
        /// </summary>
        public static bool IsBroken(EquipmentCondition condition)
        {
            return condition == EquipmentCondition.Broken;
        }
    }
}
