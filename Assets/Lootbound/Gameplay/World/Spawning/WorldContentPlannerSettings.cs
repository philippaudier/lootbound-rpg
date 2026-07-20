namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Plain settings for the content planner.
    /// Defaults match the layout generator's traversability limits.
    /// </summary>
    public sealed class WorldContentPlannerSettings
    {
        /// <summary>Plan encounter reservations.</summary>
        public bool EncountersEnabled = true;

        /// <summary>Plan resource reservations.</summary>
        public bool ResourcesEnabled = true;

        /// <summary>Maximum terrain slope (degrees) accepted at a spawn position.</summary>
        public float MaxPlacementSlope = 40f;

        /// <summary>
        /// Deterministic candidate offsets tried per encounter member before
        /// falling back to the reservation anchor.
        /// </summary>
        public int PlacementAttemptsPerEntry = 6;
    }
}
