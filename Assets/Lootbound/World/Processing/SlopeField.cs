using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How steep is the ground here?"
    ///
    /// Unit: degrees. Range: [0, 90). Reads: HeightField (upstream).
    /// V1 algorithm: central finite differences of height over a fixed step,
    /// slope = atan(|gradient|). Swappable behind IWorldField&lt;float&gt;.
    /// Assumptions: local linearity at the sample step; height is C0.
    /// Limits: a step larger/smaller than the relief smooths/aliases the slope;
    /// no sub-cell detail. Future consumers: Cliff, Traversability, Landscape,
    /// vegetation, wildlife, spawn, paths.
    /// </summary>
    public sealed class SlopeField : IWorldField<float>
    {
        private readonly IWorldField<float> _height;
        private readonly float _heightScale;
        private readonly float _step;

        public SlopeField(IWorldField<float> height, float heightScale, float sampleStep)
        {
            _height = height ?? throw new ArgumentNullException(nameof(height));
            _heightScale = heightScale;
            _step = sampleStep <= 0f ? 1f : sampleStep;
        }

        public float Evaluate(WorldCoordinate c)
        {
            double h = _step;
            float hxp = _height.Evaluate(new WorldCoordinate(c.X + h, c.Z)) * _heightScale;
            float hxn = _height.Evaluate(new WorldCoordinate(c.X - h, c.Z)) * _heightScale;
            float hzp = _height.Evaluate(new WorldCoordinate(c.X, c.Z + h)) * _heightScale;
            float hzn = _height.Evaluate(new WorldCoordinate(c.X, c.Z - h)) * _heightScale;

            float twoStep = 2f * (float)h;
            float dx = (hxp - hxn) / twoStep;
            float dz = (hzp - hzn) / twoStep;

            float gradient = MathF.Sqrt(dx * dx + dz * dz);
            return MathF.Atan(gradient) * (180f / MathF.PI);
        }
    }
}
