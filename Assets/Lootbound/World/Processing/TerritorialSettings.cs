namespace Lootbound.World.Processing
{
    /// <summary>
    /// Measurement parameters of Territorial Intelligence - the SCALE at which
    /// territory is read, never an opinion about it. Normalization constants
    /// (reference cost, open-ray factor) define the measure's units, exactly
    /// like "degrees" defines slope; they judge nothing.
    /// </summary>
    public sealed class TerritorialSettings
    {
        /// <summary>How far (m) around a point the territory is sampled - the "mid scale".</summary>
        public float SampleRadius = 96f;

        /// <summary>Number of outward rays. More = smoother, costlier.</summary>
        public int DirectionCount = 16;

        /// <summary>Cost samples along each ray.</summary>
        public int StepsPerRay = 6;

        /// <summary>
        /// The cost-per-metre treated as "ideal ground" for normalization
        /// (matches the default TraversalProfile.BaseCost).
        /// </summary>
        public float ReferenceCostPerMetre = 1f;

        /// <summary>A ray is an "open direction" if it costs at most this factor of ideal.</summary>
        public float OpenRayFactor = 1.5f;
    }
}
