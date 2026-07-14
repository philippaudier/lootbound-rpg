using UnityEngine;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Context data for a potential wear application.
    /// Passed to WeaponWearSystem to evaluate wear.
    /// </summary>
    public readonly struct WearContext
    {
        /// <summary>
        /// The cause of this potential wear.
        /// </summary>
        public WeaponWearCause Cause { get; }

        /// <summary>
        /// Unique identifier for the current attack.
        /// Used to prevent multiple wear applications per swing.
        /// </summary>
        public int AttackId { get; }

        /// <summary>
        /// Maximum HP of the target (for HeavyTargetHit evaluation).
        /// Zero or negative if not applicable.
        /// </summary>
        public float TargetMaxHp { get; }

        /// <summary>
        /// Source of the wear event (for logging).
        /// </summary>
        public GameObject Source { get; }

        /// <summary>
        /// Create a wear context.
        /// </summary>
        public WearContext(WeaponWearCause cause, int attackId, float targetMaxHp = 0f, GameObject source = null)
        {
            Cause = cause;
            AttackId = attackId;
            TargetMaxHp = targetMaxHp;
            Source = source;
        }

        /// <summary>
        /// Create a context for a successful hit.
        /// </summary>
        public static WearContext SuccessfulHit(int attackId, float targetMaxHp, GameObject source = null)
        {
            return new WearContext(WeaponWearCause.SuccessfulHit, attackId, targetMaxHp, source);
        }

        /// <summary>
        /// Create a context for player taking damage.
        /// </summary>
        public static WearContext PlayerDamaged(GameObject damageSource = null)
        {
            // Attack ID of 0 means this is not tied to an attack cycle
            return new WearContext(WeaponWearCause.PlayerDamagedWhileEquipped, 0, 0f, damageSource);
        }

        /// <summary>
        /// Create a context for debug wear application.
        /// </summary>
        public static WearContext Debug()
        {
            return new WearContext(WeaponWearCause.Debug, -1, 0f, null);
        }
    }
}
