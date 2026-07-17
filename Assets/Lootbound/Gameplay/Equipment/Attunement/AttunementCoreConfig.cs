using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for attunement stat bonuses.
    /// Defines the damage and stat multipliers for each attunement level.
    /// Works with AttunementFoundationConfig for maximum level validation.
    /// </summary>
    [CreateAssetMenu(fileName = "AttunementCoreConfig", menuName = "Lootbound/Equipment/Attunement Core Config")]
    public class AttunementCoreConfig : ScriptableObject
    {
        [Header("Attunement Tiers")]
        [Tooltip("Stat multipliers for each attunement level. Index 0 = +0, Index 10 = +10.")]
        [SerializeField]
        private AttunementTier[] tiers = new AttunementTier[]
        {
            // +0: No bonus
            new AttunementTier { DamageMultiplier = 1.00f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +1
            new AttunementTier { DamageMultiplier = 1.04f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +2
            new AttunementTier { DamageMultiplier = 1.08f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +3
            new AttunementTier { DamageMultiplier = 1.13f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +4
            new AttunementTier { DamageMultiplier = 1.18f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +5
            new AttunementTier { DamageMultiplier = 1.24f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +6
            new AttunementTier { DamageMultiplier = 1.30f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +7
            new AttunementTier { DamageMultiplier = 1.37f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +8
            new AttunementTier { DamageMultiplier = 1.44f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +9
            new AttunementTier { DamageMultiplier = 1.52f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f },
            // +10
            new AttunementTier { DamageMultiplier = 1.60f, AttackSpeedMultiplier = 1f, RangeMultiplier = 1f, StaggerMultiplier = 1f }
        };

        [Header("Foundation Reference")]
        [Tooltip("Reference to the foundation config for maximum level. If null, uses array length.")]
        [SerializeField]
        private AttunementFoundationConfig foundationConfig;

        /// <summary>
        /// Maximum attunement level supported by this configuration.
        /// Uses the foundation config if available, otherwise array length - 1.
        /// </summary>
        public int MaximumLevel
        {
            get
            {
                if (foundationConfig != null)
                {
                    return foundationConfig.MaximumAttunementLevel;
                }
                return Mathf.Max(0, tiers.Length - 1);
            }
        }

        /// <summary>
        /// Number of tiers defined in this configuration.
        /// </summary>
        public int TierCount => tiers?.Length ?? 0;

        /// <summary>
        /// Get the tier for a specific attunement level.
        /// Returns default tier if level is out of bounds or no tiers defined.
        /// </summary>
        public AttunementTier GetTier(int level)
        {
            if (tiers == null || tiers.Length == 0)
            {
                return AttunementTier.Default;
            }

            int clampedLevel = Mathf.Clamp(level, 0, tiers.Length - 1);
            return tiers[clampedLevel];
        }

        /// <summary>
        /// Get the damage multiplier for a specific level.
        /// </summary>
        public float GetDamageMultiplier(int level)
        {
            return GetTier(level).DamageMultiplier;
        }

        /// <summary>
        /// Get the damage bonus percentage for a specific level.
        /// </summary>
        public float GetDamageBonusPercent(int level)
        {
            return GetTier(level).DamageBonusPercent;
        }

        /// <summary>
        /// Apply the attunement multipliers for the given level to stats.
        /// </summary>
        public (float damage, float attackSpeed, float range, float stagger) ApplyMultipliers(
            int level, float damage, float attackSpeed, float range, float stagger)
        {
            var tier = GetTier(level);
            return tier.ApplyMultipliers(damage, attackSpeed, range, stagger);
        }

        /// <summary>
        /// Check if a level has any stat bonuses.
        /// </summary>
        public bool HasBonusAtLevel(int level)
        {
            return GetTier(level).HasAnyBonus;
        }

        private void OnValidate()
        {
            // Ensure we have at least one tier
            if (tiers == null || tiers.Length == 0)
            {
                tiers = new AttunementTier[] { AttunementTier.Default };
            }

            // Ensure level 0 has no bonus (multiplier = 1.0)
            if (tiers.Length > 0)
            {
                var tier0 = tiers[0];
                if (tier0.DamageMultiplier != 1f || tier0.AttackSpeedMultiplier != 1f ||
                    tier0.RangeMultiplier != 1f || tier0.StaggerMultiplier != 1f)
                {
                    Debug.LogWarning($"[AttunementCoreConfig] Level 0 should have no bonus (all multipliers = 1.0)");
                }
            }

            // Warn if tier count doesn't match foundation config
            if (foundationConfig != null && tiers.Length != foundationConfig.MaximumAttunementLevel + 1)
            {
                Debug.LogWarning($"[AttunementCoreConfig] Tier count ({tiers.Length}) doesn't match foundation max level + 1 ({foundationConfig.MaximumAttunementLevel + 1})");
            }
        }
    }
}
