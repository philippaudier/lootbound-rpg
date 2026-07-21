using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How bumpy / broken is the ground here?"
    ///
    /// Unit: metres (standard deviation of local height). Range: [0, +inf).
    /// Reads: HeightField (upstream). V1 algorithm: standard deviation of the
    /// centre plus a ring of samples at RoughnessRadius. Swappable behind the
    /// interface. Roughness is DISTINCT from curvature: a smooth mountain has
    /// high curvature but low roughness; a broken plain has low curvature but
    /// high roughness. Assumptions: a single ring at one radius. Limits: V1 uses
    /// RAW local variance, so the slope trend is not removed - a steep but smooth
    /// slope reads as somewhat rough; detrending (subtracting the local plane) is
    /// a future refinement. No multi-scale texture. Future consumers:
    /// Traversability, Landscape, vegetation, wildlife, spawn, visual noise.
    /// </summary>
    public sealed class RoughnessField : IWorldField<float>
    {
        private const int RingSamples = 8;

        private readonly IWorldField<float> _height;
        private readonly float _heightScale;
        private readonly float _radius;

        public RoughnessField(IWorldField<float> height, float heightScale, float radius)
        {
            _height = height ?? throw new ArgumentNullException(nameof(height));
            _heightScale = heightScale;
            _radius = radius <= 0f ? 1f : radius;
        }

        public float Evaluate(WorldCoordinate c)
        {
            Span<float> samples = stackalloc float[RingSamples + 1];
            samples[0] = _height.Evaluate(c) * _heightScale;
            for (int i = 0; i < RingSamples; i++)
            {
                float angle = (i / (float)RingSamples) * (2f * MathF.PI);
                double sx = c.X + MathF.Cos(angle) * _radius;
                double sz = c.Z + MathF.Sin(angle) * _radius;
                samples[i + 1] = _height.Evaluate(new WorldCoordinate(sx, sz)) * _heightScale;
            }

            float mean = 0f;
            for (int i = 0; i < samples.Length; i++) mean += samples[i];
            mean /= samples.Length;

            float variance = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float d = samples[i] - mean;
                variance += d * d;
            }
            variance /= samples.Length;

            return MathF.Sqrt(variance);
        }
    }
}
