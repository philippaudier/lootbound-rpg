namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// A request to repair equipment using a specified number of fragments.
    /// </summary>
    public readonly struct RepairRequest
    {
        /// <summary>The equipment to repair.</summary>
        public readonly EquipmentData Equipment;

        /// <summary>Number of fragments to consume. Use 0 for maximum available repair.</summary>
        public readonly int FragmentCount;

        /// <summary>Whether to use all available fragments for maximum repair.</summary>
        public readonly bool UseMaximumFragments;

        /// <summary>
        /// Create a repair request for a specific number of fragments.
        /// </summary>
        public RepairRequest(EquipmentData equipment, int fragmentCount)
        {
            Equipment = equipment;
            FragmentCount = fragmentCount;
            UseMaximumFragments = false;
        }

        /// <summary>
        /// Create a repair request that uses maximum available fragments.
        /// </summary>
        public RepairRequest(EquipmentData equipment)
        {
            Equipment = equipment;
            FragmentCount = 0;
            UseMaximumFragments = true;
        }

        /// <summary>
        /// Create a full repair request (uses as many fragments as needed).
        /// </summary>
        public static RepairRequest FullRepair(EquipmentData equipment)
        {
            return new RepairRequest(equipment);
        }

        /// <summary>
        /// Create a partial repair request with a specific fragment count.
        /// </summary>
        public static RepairRequest PartialRepair(EquipmentData equipment, int fragmentCount)
        {
            return new RepairRequest(equipment, fragmentCount);
        }
    }
}
