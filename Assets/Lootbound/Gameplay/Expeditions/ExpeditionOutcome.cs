namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Final outcome of a completed expedition.
    /// </summary>
    public enum ExpeditionOutcome
    {
        /// <summary>
        /// Expedition is still in progress or hasn't started.
        /// </summary>
        None = 0,

        /// <summary>
        /// Player returned safely to the refuge.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Player died during the expedition.
        /// </summary>
        PlayerDeath = 2,

        /// <summary>
        /// Player cancelled the expedition (debug/dev only in V1).
        /// </summary>
        Cancelled = 3
    }
}
