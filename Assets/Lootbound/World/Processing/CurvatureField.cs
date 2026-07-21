using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "Is the ground convex or concave here?"
    ///
    /// Unit: signed curvature (metres per metre², relative). Range: unbounded,
    /// centred on 0. Sign: &gt;0 convex (ridge/peak), &lt;0 concave (valley/basin),
    /// ~0 flat. Reads: HeightField (upstream). V1 algorithm: discrete Laplacian
    /// (4-neighbour), negated so convex is positive. Swappable behind the
    /// interface. Assumptions: uniform step; height is C1. Limits: single-scale
    /// (one step); noisy on rough ground - pair with RoughnessField to tell a
    /// bumpy plain from a smooth curve. Future consumers: Landscape, paths,
    /// erosion (later). NOTE: curvature is NOT roughness (a smooth hill is very
    /// curved; a bumpy plain is barely curved).
    /// </summary>
    public sealed class CurvatureField : IWorldField<float>
    {
        private readonly IWorldField<float> _height;
        private readonly float _heightScale;
        private readonly float _step;

        public CurvatureField(IWorldField<float> height, float heightScale, float sampleStep)
        {
            _height = height ?? throw new ArgumentNullException(nameof(height));
            _heightScale = heightScale;
            _step = sampleStep <= 0f ? 1f : sampleStep;
        }

        public float Evaluate(WorldCoordinate c)
        {
            double h = _step;
            float centre = _height.Evaluate(c) * _heightScale;
            float hxp = _height.Evaluate(new WorldCoordinate(c.X + h, c.Z)) * _heightScale;
            float hxn = _height.Evaluate(new WorldCoordinate(c.X - h, c.Z)) * _heightScale;
            float hzp = _height.Evaluate(new WorldCoordinate(c.X, c.Z + h)) * _heightScale;
            float hzn = _height.Evaluate(new WorldCoordinate(c.X, c.Z - h)) * _heightScale;

            float step = (float)h;
            float laplacian = (hxp + hxn + hzp + hzn - 4f * centre) / (step * step);
            return -laplacian; // convex (peak) > 0, concave (valley) < 0
        }
    }
}
