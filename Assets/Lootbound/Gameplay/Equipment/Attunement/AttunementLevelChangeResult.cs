namespace Lootbound.Gameplay.Equipment
{
    /// <summary>
    /// Result of an attunement level change operation.
    /// Used for debug, tests, and future attunement attempt mechanics.
    /// </summary>
    public readonly struct AttunementLevelChangeResult
    {
        /// <summary>
        /// Attunement level before the change.
        /// </summary>
        public int PreviousLevel { get; }

        /// <summary>
        /// Attunement level after the change.
        /// </summary>
        public int CurrentLevel { get; }

        /// <summary>
        /// Whether the level actually changed.
        /// </summary>
        public bool Changed { get; }

        /// <summary>
        /// Whether the requested level was clamped to valid bounds.
        /// </summary>
        public bool WasClamped { get; }

        /// <summary>
        /// Attunement state before the change.
        /// </summary>
        public AttunementState PreviousState { get; }

        /// <summary>
        /// Attunement state after the change.
        /// </summary>
        public AttunementState CurrentState { get; }

        /// <summary>
        /// Create a result for a successful change.
        /// </summary>
        public AttunementLevelChangeResult(
            int previousLevel,
            int currentLevel,
            bool wasClamped,
            AttunementState previousState,
            AttunementState currentState)
        {
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            Changed = previousLevel != currentLevel;
            WasClamped = wasClamped;
            PreviousState = previousState;
            CurrentState = currentState;
        }

        /// <summary>
        /// Create a result indicating no change occurred.
        /// </summary>
        public static AttunementLevelChangeResult NoChange(int level, AttunementState state)
        {
            return new AttunementLevelChangeResult(level, level, false, state, state);
        }
    }
}
