namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// How strongly the terrain conforms to seat a landmark. The intensity is
    /// an authoring intent; the actual numbers (radii, cut/fill, residual) live
    /// on the definition. The terrain adapts to the landmark, never the reverse.
    /// </summary>
    public enum LandmarkTerrainConformingMode
    {
        /// <summary>No terrain modification. The landmark simply grounds on the natural surface.</summary>
        None,

        /// <summary>
        /// A clear but gentle seat: the ground is drawn toward a representative
        /// height, keeping a faint relief and a smooth transition.
        /// </summary>
        SoftFoundation,

        /// <summary>
        /// A very natural stabilization: most of the relief is preserved, the
        /// terrain is only lightly settled. Used for Great Oak, Stone Arch...
        /// </summary>
        NaturalIntegration
    }
}
