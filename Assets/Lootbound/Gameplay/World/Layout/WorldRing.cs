namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Concentric zones radiating outward from the Refuge.
    /// Names are internal identifiers, not visible to the player by default.
    /// </summary>
    public enum WorldRing
    {
        /// <summary>
        /// Central safe zone around the player's home.
        /// </summary>
        Refuge,

        /// <summary>
        /// First steps outside the Refuge. Familiar territory.
        /// </summary>
        Nearlands,

        /// <summary>
        /// Intermediate wild territory.
        /// </summary>
        Wildlands,

        /// <summary>
        /// Distant regions requiring preparation.
        /// </summary>
        Farlands,

        /// <summary>
        /// Outer reaches of the known world.
        /// </summary>
        Outerlands,

        /// <summary>
        /// Border zone before the Void.
        /// </summary>
        Edgelands,

        /// <summary>
        /// Final playable region. Not implemented in current prototype.
        /// The Void is NOT inaccessible by design - it is a future gameplay region.
        /// </summary>
        Void
    }
}
