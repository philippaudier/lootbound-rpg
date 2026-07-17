namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// States for the attunement ritual presentation sequence.
    /// This controls visual/audio feedback only - not gameplay logic.
    /// </summary>
    public enum AttunementRitualState
    {
        /// <summary>
        /// Table is at rest, no ritual in progress.
        /// </summary>
        Idle,

        /// <summary>
        /// Ritual starting - initial activation feedback.
        /// Duration: ~0.2s
        /// </summary>
        Preparing,

        /// <summary>
        /// Tension building - emission increasing, subtle vibration.
        /// Duration: ~0.5s
        /// </summary>
        Building,

        /// <summary>
        /// Peak moment - determining visual outcome.
        /// Duration: ~0.4s
        /// </summary>
        Resolving,

        /// <summary>
        /// Result revealed - success/failure feedback playing.
        /// Duration: ~0.4s
        /// </summary>
        ShowingResult,

        /// <summary>
        /// Ritual interrupted (scene change, etc).
        /// </summary>
        Cancelling
    }
}
