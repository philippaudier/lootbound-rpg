namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Types of stat modifications an affix can apply.
    /// </summary>
    public enum AffixModifierType
    {
        /// <summary>
        /// Percentage bonus to damage.
        /// </summary>
        DamagePercent = 0,

        /// <summary>
        /// Percentage bonus to attack speed.
        /// </summary>
        AttackSpeedPercent = 1,

        /// <summary>
        /// Percentage bonus to range.
        /// </summary>
        RangePercent = 2,

        /// <summary>
        /// Percentage bonus to stagger force.
        /// </summary>
        StaggerPercent = 3
    }
}
