using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.Gameplay.World.Providers
{
    /// <summary>
    /// Presents any <see cref="IWorldHeightSampler"/> (typically the generator's
    /// FINAL relief: base field plus refuge basin, corridor flattening and
    /// landmark seats) as a normalized <see cref="IWorldField{T}"/>, so the
    /// World Knowledge analyzers can be built on the ground the player actually
    /// walks - closing gap G4 of PCE 0.2 (knowledge used to see only the base
    /// HeightField, making the carved Refuge invisible to it).
    ///
    /// Composition seam only: the World layer never learns where the heights
    /// come from. Determinism is inherited from the sampler (final in-region
    /// relief is deterministic per generation).
    /// </summary>
    public sealed class SampledWorldHeightField : IWorldField<float>
    {
        private readonly IWorldHeightSampler _sampler;
        private readonly float _invHeight;

        public SampledWorldHeightField(IWorldHeightSampler sampler)
        {
            _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
            float terrainHeight = sampler.TerrainHeight;
            _invHeight = terrainHeight > 0f ? 1f / terrainHeight : 0f;
        }

        /// <summary>Normalized height (metres / TerrainHeight), like HeightField.</summary>
        public float Evaluate(WorldCoordinate coordinate)
        {
            return _sampler.SampleHeight(coordinate.X, coordinate.Z) * _invHeight;
        }
    }
}
