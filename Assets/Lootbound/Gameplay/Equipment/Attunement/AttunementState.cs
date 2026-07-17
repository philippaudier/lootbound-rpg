namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Represents the attunement state of equipment.
    /// Derived from the attunement level, not serialized.
    /// </summary>
    public enum AttunementState
    {
        /// <summary>
        /// Equipment has not been attuned (level 0).
        /// </summary>
        Unattuned,

        /// <summary>
        /// Equipment has been attuned but not to maximum (level 1-4).
        /// </summary>
        Attuned,

        /// <summary>
        /// Equipment has reached maximum attunement (level 5).
        /// </summary>
        Maximum
    }
}
