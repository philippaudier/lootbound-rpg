using Lootbound.Gameplay.Inventory;

namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Interface for resolving equipment definitions at runtime.
    /// Allows AffixInstance and EquipmentData to look up definitions without
    /// tight coupling to a specific registry implementation.
    /// </summary>
    public interface IEquipmentRegistry
    {
        /// <summary>
        /// Get a weapon definition by ID.
        /// </summary>
        WeaponDefinition GetWeaponDefinition(string definitionId);

        /// <summary>
        /// Get an affix definition by ID.
        /// </summary>
        AffixDefinition GetAffixDefinition(string affixId);

        /// <summary>
        /// Get an item definition by ID.
        /// </summary>
        ItemDefinition GetItemDefinition(string itemId);
    }
}
