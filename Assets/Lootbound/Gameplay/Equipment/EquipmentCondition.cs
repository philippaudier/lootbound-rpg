namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Condition states for equipment based on durability percentage.
    /// </summary>
    public enum EquipmentCondition
    {
        /// <summary>
        /// Equipment is in pristine condition (80-100%).
        /// </summary>
        Excellent,

        /// <summary>
        /// Equipment shows signs of regular use (60-79%).
        /// </summary>
        Good,

        /// <summary>
        /// Equipment has seen many journeys (35-59%).
        /// </summary>
        Worn,

        /// <summary>
        /// Equipment may not endure many more battles (1-34%).
        /// </summary>
        Fragile,

        /// <summary>
        /// Equipment is broken and needs repair (0%).
        /// </summary>
        Broken
    }
}
