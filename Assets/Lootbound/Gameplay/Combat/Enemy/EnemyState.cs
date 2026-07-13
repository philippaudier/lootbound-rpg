namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// States for enemy AI behavior.
    /// </summary>
    public enum EnemyState
    {
        /// <summary>
        /// No target detected, waiting.
        /// </summary>
        Idle,

        /// <summary>
        /// Moving toward the player.
        /// </summary>
        Chase,

        /// <summary>
        /// Winding up an attack (telegraph).
        /// </summary>
        AttackWindup,

        /// <summary>
        /// Attack is active, can deal damage.
        /// </summary>
        AttackActive,

        /// <summary>
        /// Recovering after an attack.
        /// </summary>
        AttackRecovery,

        /// <summary>
        /// Stunned from taking damage.
        /// </summary>
        Stagger,

        /// <summary>
        /// Dead.
        /// </summary>
        Dead
    }
}
