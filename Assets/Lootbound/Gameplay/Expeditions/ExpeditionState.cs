namespace Lootbound.Gameplay.Expeditions
{
    /// <summary>
    /// Represents the current phase of an expedition.
    /// </summary>
    /// <remarks>
    /// State transitions:
    /// None → Preparing → Departing → Active → Returning → Completed
    ///                                   ↓           ↓
    ///                                Failed ←←←← Cancelled
    ///
    /// Terminal states: Completed, Failed, Cancelled
    /// </remarks>
    public enum ExpeditionState
    {
        /// <summary>
        /// No expedition active. This is sandbox/testing mode.
        /// </summary>
        None = 0,

        /// <summary>
        /// Player is in the refuge, preparing for departure.
        /// Equipment can be changed, items can be organized.
        /// </summary>
        Preparing = 1,

        /// <summary>
        /// Player has committed to leave. Snapshot is captured.
        /// Brief transition state before Active.
        /// </summary>
        Departing = 2,

        /// <summary>
        /// Expedition is in progress. Metrics are being tracked.
        /// Player is exploring the world.
        /// </summary>
        Active = 3,

        /// <summary>
        /// Player is heading back to the refuge.
        /// Still tracking metrics, but intent is to return safely.
        /// </summary>
        Returning = 4,

        /// <summary>
        /// Expedition completed successfully. Player returned safely.
        /// Terminal state - metrics are frozen.
        /// </summary>
        Completed = 5,

        /// <summary>
        /// Expedition failed due to player death.
        /// Terminal state - metrics are frozen.
        /// </summary>
        Failed = 6,

        /// <summary>
        /// Expedition was cancelled by the player.
        /// Terminal state - metrics are frozen.
        /// </summary>
        Cancelled = 7
    }

    /// <summary>
    /// Extension methods for ExpeditionState.
    /// </summary>
    public static class ExpeditionStateExtensions
    {
        /// <summary>
        /// Returns true if the state is a terminal state (Completed, Failed, or Cancelled).
        /// </summary>
        public static bool IsTerminal(this ExpeditionState state)
        {
            return state == ExpeditionState.Completed
                || state == ExpeditionState.Failed
                || state == ExpeditionState.Cancelled;
        }

        /// <summary>
        /// Returns true if the expedition is actively tracking metrics.
        /// </summary>
        public static bool IsTracking(this ExpeditionState state)
        {
            return state == ExpeditionState.Active || state == ExpeditionState.Returning;
        }

        /// <summary>
        /// Returns true if the expedition has started (past Preparing phase).
        /// </summary>
        public static bool HasStarted(this ExpeditionState state)
        {
            return state >= ExpeditionState.Departing && state != ExpeditionState.None;
        }
    }
}
