namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Types of nodes in the world layout graph.
    /// </summary>
    public enum WorldNodeType
    {
        /// <summary>
        /// Player's safe starting area (only one per layout).
        /// Center of the WorldDisc.
        /// </summary>
        Refuge,

        /// <summary>
        /// Connection point between paths or branches.
        /// </summary>
        Junction,

        /// <summary>
        /// Open area suitable for encounters or rest.
        /// </summary>
        Clearing,

        /// <summary>
        /// Elevated position with good sightlines.
        /// </summary>
        Viewpoint,

        /// <summary>
        /// Distinctive terrain feature for navigation.
        /// </summary>
        Landmark,

        /// <summary>
        /// Terminal node of a RadialPath.
        /// Represents the local terminus in the current layout prototype.
        /// This is NOT the actual world edge or Void boundary.
        /// </summary>
        OuterDestination,

        /// <summary>
        /// Terminal node of a branch (not a RadialPath terminus).
        /// </summary>
        DeadEnd
    }
}
