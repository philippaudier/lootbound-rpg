namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// A snapshot of radial position data relative to the Refuge.
    /// Immutable value type for efficient passing and caching.
    /// </summary>
    public readonly struct WorldRingSample
    {
        /// <summary>
        /// Absolute distance from the Refuge center in world units.
        /// </summary>
        public float DistanceFromRefuge { get; }

        /// <summary>
        /// Normalized distance from Refuge (0.0) to world edge (1.0).
        /// Calculated against the logical WorldDisc radius, not local terrain size.
        /// Values may exceed 1.0 for positions beyond the defined world radius.
        /// </summary>
        public float NormalizedWorldRadius { get; }

        /// <summary>
        /// The WorldRing this position belongs to.
        /// </summary>
        public WorldRing Ring { get; }

        public WorldRingSample(float distanceFromRefuge, float normalizedWorldRadius, WorldRing ring)
        {
            DistanceFromRefuge = distanceFromRefuge;
            NormalizedWorldRadius = normalizedWorldRadius;
            Ring = ring;
        }

        /// <summary>
        /// Sample at the Refuge center.
        /// </summary>
        public static WorldRingSample AtRefuge => new WorldRingSample(0f, 0f, WorldRing.Refuge);

        public override string ToString()
        {
            return $"[{Ring}] dist={DistanceFromRefuge:F1}m, norm={NormalizedWorldRadius:F3}";
        }
    }
}
