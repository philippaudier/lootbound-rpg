using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Configuration for weapon wear system.
    /// Defines chances and amounts for each wear cause.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponWearConfig", menuName = "Lootbound/Equipment/Weapon Wear Config")]
    public class WeaponWearConfig : ScriptableObject
    {
        [Header("Successful Hit")]
        [Tooltip("Chance (0-1) to apply wear on a successful hit.")]
        [SerializeField, Range(0f, 1f)] private float successfulHitChance = 0.15f;

        [Tooltip("Durability loss per successful hit when wear is applied.")]
        [SerializeField, Min(0f)] private float successfulHitAmount = 1f;

        [Header("Heavy Target")]
        [Tooltip("HP threshold above which a target is considered 'heavy'.")]
        [SerializeField, Min(0f)] private float heavyTargetHpThreshold = 100f;

        [Tooltip("Chance (0-1) to apply additional wear when hitting a heavy target.")]
        [SerializeField, Range(0f, 1f)] private float heavyTargetHitChance = 0.30f;

        [Tooltip("Additional durability loss when hitting a heavy target.")]
        [SerializeField, Min(0f)] private float heavyTargetHitAmount = 2f;

        [Header("Player Damaged")]
        [Tooltip("Chance (0-1) to apply wear when player takes damage while weapon is equipped.")]
        [SerializeField, Range(0f, 1f)] private float playerDamagedChance = 0.10f;

        [Tooltip("Durability loss when player takes damage.")]
        [SerializeField, Min(0f)] private float playerDamagedAmount = 0.5f;

        [Header("World Impact (Reserved)")]
        [Tooltip("Chance (0-1) to apply wear on world impact. Reserved for future.")]
        [SerializeField, Range(0f, 1f)] private float worldImpactChance = 0f;

        [Tooltip("Durability loss on world impact.")]
        [SerializeField, Min(0f)] private float worldImpactAmount = 0.5f;

        [Header("Debug")]
        [Tooltip("Durability loss for debug wear application.")]
        [SerializeField, Min(0f)] private float debugAmount = 10f;

        /// <summary>
        /// Get the wear chance for a specific cause.
        /// </summary>
        public float GetChance(WeaponWearCause cause)
        {
            return cause switch
            {
                WeaponWearCause.SuccessfulHit => successfulHitChance,
                WeaponWearCause.HeavyTargetHit => heavyTargetHitChance,
                WeaponWearCause.PlayerDamagedWhileEquipped => playerDamagedChance,
                WeaponWearCause.WorldImpact => worldImpactChance,
                WeaponWearCause.Debug => 1f, // Debug always applies
                _ => 0f
            };
        }

        /// <summary>
        /// Get the durability loss amount for a specific cause.
        /// </summary>
        public float GetAmount(WeaponWearCause cause)
        {
            return cause switch
            {
                WeaponWearCause.SuccessfulHit => successfulHitAmount,
                WeaponWearCause.HeavyTargetHit => heavyTargetHitAmount,
                WeaponWearCause.PlayerDamagedWhileEquipped => playerDamagedAmount,
                WeaponWearCause.WorldImpact => worldImpactAmount,
                WeaponWearCause.Debug => debugAmount,
                _ => 0f
            };
        }

        /// <summary>
        /// HP threshold for a target to be considered "heavy".
        /// </summary>
        public float HeavyTargetHpThreshold => heavyTargetHpThreshold;

        /// <summary>
        /// Check if a target with given max HP is considered heavy.
        /// </summary>
        public bool IsHeavyTarget(float targetMaxHp)
        {
            return targetMaxHp >= heavyTargetHpThreshold;
        }

        // Accessors for testing
        public float SuccessfulHitChance => successfulHitChance;
        public float SuccessfulHitAmount => successfulHitAmount;
        public float HeavyTargetHitChance => heavyTargetHitChance;
        public float HeavyTargetHitAmount => heavyTargetHitAmount;
        public float PlayerDamagedChance => playerDamagedChance;
        public float PlayerDamagedAmount => playerDamagedAmount;
    }
}
