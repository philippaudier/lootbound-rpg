namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// Footprint shape of a landmark's terrain foundation.
    ///
    /// V1 implements <see cref="Circle"/> only. The other values exist so
    /// authored assets stay forward-compatible; the applier falls back to a
    /// circle (with a one-time warning) until they are implemented. The
    /// oriented shapes will use the landmark's Y rotation for their footprint.
    /// </summary>
    public enum FoundationShape
    {
        /// <summary>Radial footprint. The only shape implemented in V1.</summary>
        Circle,

        /// <summary>Reserved. Oriented rectangular footprint (cabins, farms).</summary>
        Rectangle,

        /// <summary>Reserved. Oriented elliptical footprint (arches).</summary>
        Ellipse,

        /// <summary>Reserved. Author-provided custom footprint.</summary>
        Custom
    }
}
