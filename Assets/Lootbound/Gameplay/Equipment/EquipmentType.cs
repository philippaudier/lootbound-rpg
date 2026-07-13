namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Categories of equipment.
    /// V1 only implements Weapon.
    /// </summary>
    public enum EquipmentType
    {
        /// <summary>
        /// Melee or ranged weapon.
        /// </summary>
        Weapon = 0,

        /// <summary>
        /// Body armor (not implemented in V1).
        /// </summary>
        Armor = 1,

        /// <summary>
        /// Ring, amulet, etc. (not implemented in V1).
        /// </summary>
        Accessory = 2
    }
}
