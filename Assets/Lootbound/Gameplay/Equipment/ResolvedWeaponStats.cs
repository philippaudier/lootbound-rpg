namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Computed final weapon stats after applying all modifiers.
    /// Immutable struct used by combat system.
    /// </summary>
    public readonly struct ResolvedWeaponStats
    {
        /// <summary>
        /// Final damage per hit.
        /// </summary>
        public float Damage { get; }

        /// <summary>
        /// Final attack speed (attacks per second).
        /// </summary>
        public float AttackSpeed { get; }

        /// <summary>
        /// Duration multiplier for attack phases.
        /// Lower = faster attacks.
        /// </summary>
        public float DurationMultiplier { get; }

        /// <summary>
        /// Final attack range in meters.
        /// </summary>
        public float Range { get; }

        /// <summary>
        /// Final stagger force (0-1).
        /// </summary>
        public float Stagger { get; }

        /// <summary>
        /// Whether these stats are valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Default stats for when no weapon is equipped.
        /// </summary>
        public static ResolvedWeaponStats Default => new ResolvedWeaponStats(20f, 1f, 1.5f, 0.2f);

        /// <summary>
        /// Invalid stats.
        /// </summary>
        public static ResolvedWeaponStats Invalid => new ResolvedWeaponStats();

        /// <summary>
        /// Create resolved stats.
        /// </summary>
        public ResolvedWeaponStats(float damage, float attackSpeed, float range, float stagger)
        {
            Damage = damage;
            AttackSpeed = attackSpeed;
            DurationMultiplier = attackSpeed > 0f ? 1f / attackSpeed : 1f;
            Range = range;
            Stagger = stagger;
            IsValid = true;
        }

        public override string ToString()
        {
            if (!IsValid) return "Invalid";
            return $"Damage: {Damage:F1}, Speed: {AttackSpeed:F2}, Range: {Range:F2}m, Stagger: {Stagger:F2}";
        }
    }
}
