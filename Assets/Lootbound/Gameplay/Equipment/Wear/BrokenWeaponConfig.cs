using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for broken weapon stat penalties.
    /// Broken weapons remain usable but with severe combat penalties.
    /// </summary>
    [CreateAssetMenu(fileName = "BrokenWeaponConfig", menuName = "Lootbound/Equipment/Broken Weapon Config")]
    public class BrokenWeaponConfig : ScriptableObject
    {
        [Header("Stat Multipliers")]
        [Tooltip("Damage multiplier when broken (0.30 = 30% of normal damage)")]
        [SerializeField, Range(0.01f, 1f)]
        private float damageMultiplier = 0.30f;

        [Tooltip("Attack speed multiplier when broken (0.55 = 55% of normal speed)")]
        [SerializeField, Range(0.01f, 1f)]
        private float attackSpeedMultiplier = 0.55f;

        [Tooltip("Range multiplier when broken (0.90 = 90% of normal range)")]
        [SerializeField, Range(0.01f, 1f)]
        private float rangeMultiplier = 0.90f;

        [Tooltip("Stagger multiplier when broken (0.20 = 20% of normal stagger)")]
        [SerializeField, Range(0.01f, 1f)]
        private float staggerMultiplier = 0.20f;

        /// <summary>
        /// Damage multiplier when weapon is broken.
        /// </summary>
        public float DamageMultiplier => damageMultiplier;

        /// <summary>
        /// Attack speed multiplier when weapon is broken.
        /// </summary>
        public float AttackSpeedMultiplier => attackSpeedMultiplier;

        /// <summary>
        /// Range multiplier when weapon is broken.
        /// </summary>
        public float RangeMultiplier => rangeMultiplier;

        /// <summary>
        /// Stagger multiplier when weapon is broken.
        /// </summary>
        public float StaggerMultiplier => staggerMultiplier;

        /// <summary>
        /// Apply broken penalties to weapon stats.
        /// </summary>
        /// <param name="damage">Input damage value.</param>
        /// <param name="attackSpeed">Input attack speed value.</param>
        /// <param name="range">Input range value.</param>
        /// <param name="stagger">Input stagger value.</param>
        /// <returns>Tuple of modified stats.</returns>
        public (float damage, float attackSpeed, float range, float stagger) ApplyPenalties(
            float damage,
            float attackSpeed,
            float range,
            float stagger)
        {
            return (
                damage * damageMultiplier,
                attackSpeed * attackSpeedMultiplier,
                range * rangeMultiplier,
                stagger * staggerMultiplier
            );
        }

        /// <summary>
        /// Get penalty percentage for display purposes.
        /// Returns values like -70 for 30% multiplier.
        /// </summary>
        public int GetDamagePenaltyPercent() => Mathf.RoundToInt((1f - damageMultiplier) * -100f);
        public int GetSpeedPenaltyPercent() => Mathf.RoundToInt((1f - attackSpeedMultiplier) * -100f);
        public int GetRangePenaltyPercent() => Mathf.RoundToInt((1f - rangeMultiplier) * -100f);
        public int GetStaggerPenaltyPercent() => Mathf.RoundToInt((1f - staggerMultiplier) * -100f);

        /// <summary>
        /// Validate configuration values.
        /// </summary>
        private void OnValidate()
        {
            damageMultiplier = Mathf.Clamp(damageMultiplier, 0.01f, 1f);
            attackSpeedMultiplier = Mathf.Clamp(attackSpeedMultiplier, 0.01f, 1f);
            rangeMultiplier = Mathf.Clamp(rangeMultiplier, 0.01f, 1f);
            staggerMultiplier = Mathf.Clamp(staggerMultiplier, 0.01f, 1f);
        }
    }
}
