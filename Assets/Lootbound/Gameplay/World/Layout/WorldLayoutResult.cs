namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Result of world layout generation.
    /// </summary>
    public sealed class WorldLayoutResult
    {
        /// <summary>
        /// Whether generation succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// The generated layout (null if failed).
        /// </summary>
        public WorldLayoutContext Layout { get; }

        /// <summary>
        /// Error message if generation failed.
        /// </summary>
        public string Error { get; }

        private WorldLayoutResult(bool success, WorldLayoutContext layout, string error)
        {
            Success = success;
            Layout = layout;
            Error = error;
        }

        /// <summary>
        /// Create a successful result.
        /// </summary>
        public static WorldLayoutResult Succeeded(WorldLayoutContext layout)
        {
            return new WorldLayoutResult(true, layout, null);
        }

        /// <summary>
        /// Create a failed result.
        /// </summary>
        public static WorldLayoutResult Failed(string error)
        {
            return new WorldLayoutResult(false, null, error);
        }
    }
}
