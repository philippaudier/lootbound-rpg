using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Progression
{
    /// <summary>
    /// The unique progression context of a world position. Immutable data,
    /// produced only by WorldProgression - every system reads it, none writes
    /// it. Ambience values are parameters for future slices (no rendering is
    /// driven by them yet).
    /// </summary>
    public readonly struct WorldRingContext
    {
        /// <summary>Spatial ring this position belongs to (fine 7-ring partition).</summary>
        public WorldRing Ring { get; }

        /// <summary>Horizontal distance from the Refuge center, in meters.</summary>
        public float DistanceFromRefuge { get; }

        /// <summary>
        /// Global depth of the position: 0 at the Refuge, 1 at the logical
        /// WorldDisc edge, clamped (positions beyond the disc stay at 1).
        /// The continuous backbone of world progression.
        /// </summary>
        public float Depth01 { get; }

        /// <summary>
        /// True while the position lies within the logical WorldDisc radius.
        /// The Void ring is treated as outside the playable disc by default.
        /// </summary>
        public bool IsInsideWorldDisc { get; }

        /// <summary>Expected danger at this depth (0 = Refuge calm, 1 = deepest).</summary>
        public float Difficulty01 { get; }

        /// <summary>Expected loot tier at this depth (0 = common, up to the configured maximum).</summary>
        public int ExpectedLootTier { get; }

        // Ambience parameters (data only; consumed by future slices)
        public float FogDensity01 { get; }
        public float LightAttenuation01 { get; }
        public float Saturation01 { get; }
        public float Temperature01 { get; }

        public WorldRingContext(
            WorldRing ring,
            float distanceFromRefuge,
            float depth01,
            bool isInsideWorldDisc,
            float difficulty01,
            int expectedLootTier,
            float fogDensity01,
            float lightAttenuation01,
            float saturation01,
            float temperature01)
        {
            Ring = ring;
            DistanceFromRefuge = distanceFromRefuge;
            Depth01 = depth01;
            IsInsideWorldDisc = isInsideWorldDisc;
            Difficulty01 = difficulty01;
            ExpectedLootTier = expectedLootTier;
            FogDensity01 = fogDensity01;
            LightAttenuation01 = lightAttenuation01;
            Saturation01 = saturation01;
            Temperature01 = temperature01;
        }

        public override string ToString()
        {
            return $"[{Ring}] depth={Depth01:F2} diff={Difficulty01:F2} loot=T{ExpectedLootTier} " +
                   $"({DistanceFromRefuge:F0}m{(IsInsideWorldDisc ? "" : ", outside disc")})";
        }
    }
}
