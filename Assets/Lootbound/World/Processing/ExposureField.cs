using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "Which way does this slope face?"
    ///
    /// Unit: compass bearing in degrees, 0 = North (+Z), 90 = East (+X),
    /// clockwise. Range: [0, 360), or -1 when the ground is effectively flat
    /// (no meaningful aspect). Reads: HeightField (upstream). V1 algorithm:
    /// central-difference gradient, bearing of the DOWNHILL direction. The full
    /// angle is kept (a consumer can bucket it via <see cref="AspectClassifier"/>).
    /// Assumptions: local linearity. Limits: undefined on flats (returns -1);
    /// single-scale. Future consumers: Climate, snow, moisture, biomes,
    /// vegetation.
    /// </summary>
    public sealed class ExposureField : IWorldField<float>
    {
        private const float FlatGradientEpsilon = 1e-4f;

        private readonly IWorldField<float> _height;
        private readonly float _heightScale;
        private readonly float _step;

        public ExposureField(IWorldField<float> height, float heightScale, float sampleStep)
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

            if (MathF.Sqrt(dx * dx + dz * dz) < FlatGradientEpsilon)
            {
                return -1f; // flat: no aspect
            }

            // Downhill direction = -gradient. Bearing 0=N(+Z), clockwise to E(+X).
            float bearing = MathF.Atan2(-dx, -dz) * (180f / MathF.PI);
            if (bearing < 0f) bearing += 360f;
            return bearing;
        }
    }
}
