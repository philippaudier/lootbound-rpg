namespace Lootbound.World.Processing
{
    /// <summary>
    /// Plain, Unity-free tunables for the Domain Processing analyzers. Kept in one
    /// place so the analyzers stay pure functions of (source fields, settings).
    /// Every value has a clear unit; none is baked into an algorithm.
    /// </summary>
    public sealed class WorldKnowledgeSettings
    {
        /// <summary>World metres per normalized height unit (= terrain height).</summary>
        public float HeightScale = 260f;

        /// <summary>Horizontal step (world metres) for finite-difference sampling.</summary>
        public float SampleStep = 2f;

        /// <summary>Radius (world metres) of the local window for roughness variance.</summary>
        public float RoughnessRadius = 6f;

        /// <summary>Slope (degrees) above which a point is considered a cliff.</summary>
        public float CliffSlopeThreshold = 60f;

        // --- Traversability weights (local cost, dimensionless) ---
        public float TraversalBaseCost = 1f;
        public float SlopeCostPerDegree = 0.05f;
        public float CliffCost = 100f;
        public float RoughnessCostPerMetre = 0.5f;
        public float WaterCost = 25f;

        // --- Landscape classification thresholds ---
        public float FlatSlopeMax = 8f;       // degrees: below = flat
        public float SteepSlopeMin = 30f;     // degrees: above = steep
        public float HighElevation = 0.6f;    // normalized
        public float LowElevation = 0.35f;    // normalized
        public float CurvatureFlat = 5f;      // |curvature| below = flat (metres/m² scaled)
    }
}
