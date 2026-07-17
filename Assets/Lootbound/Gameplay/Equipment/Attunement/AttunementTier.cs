using System;
using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Defines the stat multipliers for a specific attunement level.
    /// Used by AttunementCoreConfig to configure progression.
    /// </summary>
    [Serializable]
    public struct AttunementTier
    {
        [Tooltip("Multiplier applied to damage (1.0 = no change).")]
        [Range(0.5f, 3f)]
        public float DamageMultiplier;

        [Tooltip("Multiplier applied to attack speed (1.0 = no change).")]
        [Range(0.5f, 3f)]
        public float AttackSpeedMultiplier;

        [Tooltip("Multiplier applied to range (1.0 = no change).")]
        [Range(0.5f, 3f)]
        public float RangeMultiplier;

        [Tooltip("Multiplier applied to stagger (1.0 = no change).")]
        [Range(0.5f, 3f)]
        public float StaggerMultiplier;

        /// <summary>
        /// Default tier with no bonuses (all multipliers = 1.0).
        /// </summary>
        public static AttunementTier Default => new AttunementTier
        {
            DamageMultiplier = 1f,
            AttackSpeedMultiplier = 1f,
            RangeMultiplier = 1f,
            StaggerMultiplier = 1f
        };

        /// <summary>
        /// Whether this tier provides a damage bonus.
        /// </summary>
        public bool HasDamageBonus => DamageMultiplier > 1f;

        /// <summary>
        /// Whether this tier provides any bonus.
        /// </summary>
        public bool HasAnyBonus => DamageMultiplier > 1f || AttackSpeedMultiplier > 1f ||
                                   RangeMultiplier > 1f || StaggerMultiplier > 1f;

        /// <summary>
        /// Damage bonus as a percentage (e.g., 1.10 returns 10).
        /// </summary>
        public float DamageBonusPercent => (DamageMultiplier - 1f) * 100f;

        /// <summary>
        /// Attack speed bonus as a percentage.
        /// </summary>
        public float AttackSpeedBonusPercent => (AttackSpeedMultiplier - 1f) * 100f;

        /// <summary>
        /// Range bonus as a percentage.
        /// </summary>
        public float RangeBonusPercent => (RangeMultiplier - 1f) * 100f;

        /// <summary>
        /// Stagger bonus as a percentage.
        /// </summary>
        public float StaggerBonusPercent => (StaggerMultiplier - 1f) * 100f;

        /// <summary>
        /// Apply this tier's multipliers to the given stats.
        /// </summary>
        public (float damage, float attackSpeed, float range, float stagger) ApplyMultipliers(
            float damage, float attackSpeed, float range, float stagger)
        {
            return (
                damage * DamageMultiplier,
                attackSpeed * AttackSpeedMultiplier,
                range * RangeMultiplier,
                stagger * StaggerMultiplier
            );
        }
    }
}
