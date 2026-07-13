namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// Phases of a melee attack.
    /// </summary>
    public enum AttackPhase
    {
        /// <summary>
        /// Not attacking, ready to start a new attack.
        /// </summary>
        Ready,

        /// <summary>
        /// Preparing to attack, cannot cancel.
        /// </summary>
        Windup,

        /// <summary>
        /// Attack is active, can hit targets.
        /// </summary>
        Active,

        /// <summary>
        /// Recovering from attack, cannot start new attack.
        /// </summary>
        Recovery
    }
}
