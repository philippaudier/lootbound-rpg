namespace Lootbound.Gameplay.Combat
{
    /// <summary>
    /// States for enemy AI behavior. Single authority: navigation and combat
    /// share this one machine (no parallel navigation-state enum, no state
    /// booleans).
    /// </summary>
    public enum EnemyState
    {
        /// <summary>Resting between wander moves, no target.</summary>
        Idle,

        /// <summary>Moving to a wander destination around HomePosition.</summary>
        Wandering,

        /// <summary>Following an explicit patrol route.</summary>
        Patrolling,

        /// <summary>Player noticed: stopped, orienting, deciding whether to chase.</summary>
        Suspicious,

        /// <summary>Pursuing the player.</summary>
        Chasing,

        /// <summary>Winding up an attack (telegraph).</summary>
        AttackWindup,

        /// <summary>Attack is active, can deal damage.</summary>
        AttackActive,

        /// <summary>Recovering after an attack.</summary>
        AttackRecovery,

        /// <summary>Chase abandoned: walking back to HomePosition.</summary>
        ReturningHome,

        /// <summary>Stunned from taking damage.</summary>
        Stagger,

        /// <summary>Movement recovery failed repeatedly; attempting controlled recovery.</summary>
        Stuck,

        /// <summary>Dead.</summary>
        Dead
    }
}
