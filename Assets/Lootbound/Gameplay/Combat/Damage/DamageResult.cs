namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Immutable result of a damage application.
    /// </summary>
    public readonly struct DamageResult
    {
        /// <summary>
        /// True if the damage was successfully applied.
        /// </summary>
        public readonly bool Applied;

        /// <summary>
        /// Actual amount of damage that was dealt (may differ from requested).
        /// </summary>
        public readonly float DamageDealt;

        /// <summary>
        /// True if this damage caused death.
        /// </summary>
        public readonly bool WasFatal;

        /// <summary>
        /// True if the damage was blocked (e.g., by invulnerability).
        /// </summary>
        public readonly bool WasBlocked;

        private DamageResult(bool applied, float damageDealt, bool wasFatal, bool wasBlocked)
        {
            Applied = applied;
            DamageDealt = damageDealt;
            WasFatal = wasFatal;
            WasBlocked = wasBlocked;
        }

        /// <summary>
        /// Creates a result for blocked damage.
        /// </summary>
        public static DamageResult Blocked()
        {
            return new DamageResult(false, 0f, false, true);
        }

        /// <summary>
        /// Creates a result for damage that wasn't applied (target already dead, etc).
        /// </summary>
        public static DamageResult NotApplied()
        {
            return new DamageResult(false, 0f, false, false);
        }

        /// <summary>
        /// Creates a result for successfully applied damage.
        /// </summary>
        public static DamageResult Success(float damageDealt, bool wasFatal)
        {
            return new DamageResult(true, damageDealt, wasFatal, false);
        }
    }
}
