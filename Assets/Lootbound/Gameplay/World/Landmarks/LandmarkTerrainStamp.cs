namespace Lootbound.Gameplay.World.Landmarks
{
    /// <summary>
    /// A DESCRIPTION of the seat a landmark asks the terrain for - never an
    /// instruction. It states where the place is, how wide its foundation and
    /// transition are, the representative seat height, how far the ground may
    /// be corrected, how much original relief to keep, and how overlaps are
    /// arbitrated. It says nothing about HOW to realize it.
    ///
    /// A consumer decides how to apply this description. In V1 that consumer is
    /// <see cref="LandmarkTerrainStampApplier"/> writing a heightmap; a future
    /// GPU / voxel / adaptive-terrain backend would be a different consumer of
    /// the very same data. Pure, immutable, backend-agnostic.
    /// </summary>
    public sealed class LandmarkTerrainStamp
    {
        /// <summary>Identity of the landmark this seat belongs to (also the overlap tiebreaker).</summary>
        public string LandmarkId { get; }

        /// <summary>Foundation footprint. V1 realizes Circle only.</summary>
        public FoundationShape Shape { get; }

        /// <summary>Conforming intent (also carried for observability / future differentiation).</summary>
        public LandmarkTerrainConformingMode Mode { get; }

        /// <summary>World X of the seat center (the landmark's frozen XZ).</summary>
        public float CenterX { get; }

        /// <summary>World Z of the seat center.</summary>
        public float CenterZ { get; }

        /// <summary>Representative seat height in world meters (robust reference + authored offset).</summary>
        public float SeatHeight { get; }

        /// <summary>Radius (m) of the fully seated foundation.</summary>
        public float FoundationRadius { get; }

        /// <summary>Width (m) of the smooth transition ring beyond the foundation.</summary>
        public float TransitionRadius { get; }

        /// <summary>Maximum downward correction (m) allowed at any point.</summary>
        public float MaxCutDepth { get; }

        /// <summary>Maximum upward correction (m) allowed at any point.</summary>
        public float MaxFillHeight { get; }

        /// <summary>Fraction (0..1) of the original relief kept inside the foundation.</summary>
        public float ResidualRoughness { get; }

        /// <summary>Overlap arbitration priority: higher wins where footprints overlap.</summary>
        public int Priority { get; }

        /// <summary>Total influence radius = foundation + transition.</summary>
        public float OuterRadius => FoundationRadius + TransitionRadius;

        public LandmarkTerrainStamp(
            string landmarkId,
            FoundationShape shape,
            LandmarkTerrainConformingMode mode,
            float centerX,
            float centerZ,
            float seatHeight,
            float foundationRadius,
            float transitionRadius,
            float maxCutDepth,
            float maxFillHeight,
            float residualRoughness,
            int priority)
        {
            LandmarkId = landmarkId;
            Shape = shape;
            Mode = mode;
            CenterX = centerX;
            CenterZ = centerZ;
            SeatHeight = seatHeight;
            FoundationRadius = foundationRadius;
            TransitionRadius = transitionRadius;
            MaxCutDepth = maxCutDepth;
            MaxFillHeight = maxFillHeight;
            ResidualRoughness = residualRoughness;
            Priority = priority;
        }
    }
}
