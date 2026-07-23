using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Territorial Intelligence, first kernel (PCE 0.4). Question answered:
    /// "What geographic logic governs this place?" - as MEASURES, never names.
    ///
    /// It consumes ONE already-composed cost view (an IWorldField built from a
    /// TraversalProfile), so every measure is perception-relative by
    /// construction and this layer never touches profiles, gameplay or chunks.
    /// V1 algorithm: march K rays outward over the sample radius, integrating
    /// cost x distance along each; then, in a single streaming pass:
    ///   Accessibility = ideal / mean ray cost      (ease of moving around)
    ///   Isolation     = 1 - ideal / best ray cost  (cost of the easiest way out)
    ///   Connectivity  = open rays / K              (how many easy directions)
    /// where ideal = ReferenceCostPerMetre x radius. All components clamp to
    /// 0..1. Boundaries are never exact - influences decay with the terrain
    /// itself (PCE invariant 20). Pure, deterministic, grid-free: cross-chunk
    /// continuity is structural, not stitched. Assumptions: straight-line rays
    /// (no path search - a ray is a probe, not a route). Limits: mid-scale only
    /// (one radius), directional resolution K. Swappable behind IWorldField.
    /// </summary>
    public sealed class TerritorialIdentityField : IWorldField<TerritorialIdentity>
    {
        private readonly IWorldField<float> _cost;
        private readonly TerritorialSettings _s;
        private readonly float[] _dirX;
        private readonly float[] _dirZ;

        public TerritorialIdentityField(IWorldField<float> cost, TerritorialSettings settings)
        {
            _cost = cost ?? throw new ArgumentNullException(nameof(cost));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (_s.DirectionCount < 4) throw new ArgumentOutOfRangeException(nameof(settings), "at least 4 directions");
            if (_s.StepsPerRay < 1) throw new ArgumentOutOfRangeException(nameof(settings), "at least 1 step per ray");

            // Deterministic, immutable direction table (built once, never mutated).
            _dirX = new float[_s.DirectionCount];
            _dirZ = new float[_s.DirectionCount];
            for (int k = 0; k < _s.DirectionCount; k++)
            {
                double angle = (2.0 * Math.PI * k) / _s.DirectionCount;
                _dirX[k] = (float)Math.Cos(angle);
                _dirZ[k] = (float)Math.Sin(angle);
            }
        }

        public TerritorialIdentity Evaluate(WorldCoordinate c)
        {
            int rays = _dirX.Length;
            int steps = _s.StepsPerRay;
            float stepLength = _s.SampleRadius / steps;
            float idealRayCost = _s.ReferenceCostPerMetre * _s.SampleRadius;
            float openThreshold = idealRayCost * _s.OpenRayFactor;

            float sum = 0f;
            float best = float.MaxValue;
            int open = 0;

            for (int k = 0; k < rays; k++)
            {
                float rayCost = 0f;
                for (int s = 1; s <= steps; s++)
                {
                    double distance = stepLength * s;
                    var sample = new WorldCoordinate(c.X + _dirX[k] * distance, c.Z + _dirZ[k] * distance);
                    rayCost += _cost.Evaluate(sample) * stepLength;
                }

                sum += rayCost;
                if (rayCost < best) best = rayCost;
                if (rayCost <= openThreshold) open++;
            }

            float mean = sum / rays;
            float accessibility = Clamp01(idealRayCost / Math.Max(mean, 1e-6f));
            float isolation = 1f - Clamp01(idealRayCost / Math.Max(best, 1e-6f));
            float connectivity = open / (float)rays;

            return new TerritorialIdentity(accessibility, isolation, connectivity);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
