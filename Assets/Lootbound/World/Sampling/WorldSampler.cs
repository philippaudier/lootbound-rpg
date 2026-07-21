using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Sampling
{
    /// <summary>
    /// The official entry point to the world: query any World Field at any
    /// coordinate, with no grid. From T2 on, downstream systems sample the world
    /// through this - never a heightmap. Every field is an injected
    /// implementation (a Provider), so the sampler knows nothing of Unity, nor of
    /// where a value comes from (procedural, campaign, cached...).
    /// </summary>
    public sealed class WorldSampler
    {
        private readonly IWorldField<float> _height;
        private readonly IWorldField<float> _danger;
        private readonly IWorldField<WorldRegion> _region;

        public WorldSampler(
            IWorldField<float> height,
            IWorldField<float> danger,
            IWorldField<WorldRegion> region)
        {
            _height = height;
            _danger = danger;
            _region = region;
        }

        public float SampleHeight(WorldCoordinate coordinate) => _height.Evaluate(coordinate);
        public float SampleDanger(WorldCoordinate coordinate) => _danger.Evaluate(coordinate);
        public WorldRegion SampleRegion(WorldCoordinate coordinate) => _region.Evaluate(coordinate);
    }
}
