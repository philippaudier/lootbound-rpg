using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How high is this, relative to the world's range?"
    ///
    /// Unit: normalized altitude. Range: [0, 1]. Reads: HeightField (upstream) -
    /// it is the geomorphic name for the raw height, exposed as World Knowledge
    /// so climate/snow/fog consumers depend on a stable concept rather than on
    /// the raw HeightField. V1 algorithm: passthrough. Assumptions/limits: none
    /// beyond the HeightField's. Future consumers: Climate, snow, fog, ambience,
    /// biomes, Landscape.
    /// </summary>
    public sealed class ElevationField : IWorldField<float>
    {
        private readonly IWorldField<float> _height;

        public ElevationField(IWorldField<float> height)
        {
            _height = height ?? throw new ArgumentNullException(nameof(height));
        }

        public float Evaluate(WorldCoordinate c) => _height.Evaluate(c);
    }
}
