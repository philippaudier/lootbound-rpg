namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Slots where equipment can be placed.
    /// V1 only implements MainHand.
    /// </summary>
    public enum EquipmentSlot
    {
        /// <summary>
        /// Primary weapon slot.
        /// </summary>
        MainHand = 0,

        /// <summary>
        /// Secondary weapon or shield (not implemented in V1).
        /// </summary>
        OffHand = 1,

        /// <summary>
        /// Body armor slot (not implemented in V1).
        /// </summary>
        Body = 2,

        /// <summary>
        /// Accessory slot 1 (not implemented in V1).
        /// </summary>
        Accessory1 = 3,

        /// <summary>
        /// Accessory slot 2 (not implemented in V1).
        /// </summary>
        Accessory2 = 4
    }
}
