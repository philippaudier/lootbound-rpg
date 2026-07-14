namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Causes of weapon wear during gameplay.
    /// Each cause has its own chance and amount in WeaponWearConfig.
    /// </summary>
    public enum WeaponWearCause
    {
        /// <summary>
        /// Weapon hit a valid target during combat.
        /// </summary>
        SuccessfulHit,

        /// <summary>
        /// Weapon hit an environmental object (wall, ground).
        /// Reserved for future implementation.
        /// </summary>
        WorldImpact,

        /// <summary>
        /// Weapon hit a target with high HP (heavy target).
        /// </summary>
        HeavyTargetHit,

        /// <summary>
        /// Player took damage while weapon was equipped.
        /// </summary>
        PlayerDamagedWhileEquipped,

        /// <summary>
        /// Debug/testing wear application.
        /// </summary>
        Debug
    }
}
