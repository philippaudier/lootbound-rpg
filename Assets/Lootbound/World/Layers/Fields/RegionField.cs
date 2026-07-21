using Lootbound.World.Coordinates;

namespace Lootbound.World.Layers.Fields
{
    /// <summary>
    /// Classifies a position into a coarse <see cref="WorldRegion"/> from the
    /// height field and two thresholds. Pure and composed over another World
    /// Field - the first example of a field that reads another field, and of a
    /// non-float World Field (IWorldField&lt;WorldRegion&gt;).
    /// </summary>
    public sealed class RegionField : IWorldField<WorldRegion>
    {
        private readonly IWorldField<float> _height;
        private readonly float _lowlandThreshold;
        private readonly float _highlandThreshold;

        public RegionField(IWorldField<float> height, float lowlandThreshold, float highlandThreshold)
        {
            _height = height;
            _lowlandThreshold = lowlandThreshold;
            _highlandThreshold = highlandThreshold;
        }

        public WorldRegion Evaluate(WorldCoordinate coordinate)
        {
            float h = _height.Evaluate(coordinate);
            if (h < _lowlandThreshold) return WorldRegion.Lowland;
            if (h >= _highlandThreshold) return WorldRegion.Highland;
            return WorldRegion.Midland;
        }
    }
}
