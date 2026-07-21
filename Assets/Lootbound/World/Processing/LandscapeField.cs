using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "What kind of place is this?"
    ///
    /// Unit: a <see cref="LandscapeType"/>. Reads: Elevation, Slope, Curvature,
    /// Cliff, RiverMask (upstream). PURELY GEOMORPHOLOGICAL - it never looks at
    /// world position, distance from the refuge, danger or any gameplay data, so
    /// geography and gameplay never mix. It also never reads Traversability (its
    /// sibling): the two are independent.
    ///
    /// V1 algorithm: a small thresholded decision tree - the engine DEDUCES the
    /// label from the shape and water, it does not stamp it. Swappable behind the
    /// interface. Assumptions/limits: a first robust pass, not perfect. Ridge and
    /// Pass are ROUGH approximations - a true ridge/saddle needs directional
    /// curvature and neighbourhood analysis (a later refinement). Thresholds are
    /// hand-tuned. Future consumers: encounters, wildlife, World Structures,
    /// quest generation, ambience.
    /// </summary>
    public sealed class LandscapeField : IWorldField<LandscapeType>
    {
        private readonly IWorldField<float> _elevation;
        private readonly IWorldField<float> _slope;
        private readonly IWorldField<float> _curvature;
        private readonly IWorldField<bool> _cliff;
        private readonly IWorldField<bool> _river;
        private readonly WorldKnowledgeSettings _s;

        public LandscapeField(
            IWorldField<float> elevation,
            IWorldField<float> slope,
            IWorldField<float> curvature,
            IWorldField<bool> cliff,
            IWorldField<bool> river,
            WorldKnowledgeSettings settings)
        {
            _elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _curvature = curvature ?? throw new ArgumentNullException(nameof(curvature));
            _cliff = cliff ?? throw new ArgumentNullException(nameof(cliff));
            _river = river ?? throw new ArgumentNullException(nameof(river));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public LandscapeType Evaluate(WorldCoordinate c)
        {
            if (_cliff.Evaluate(c)) return LandscapeType.Cliff;

            float slope = _slope.Evaluate(c);
            float elev = _elevation.Evaluate(c);
            float curv = _curvature.Evaluate(c);
            bool river = _river.Evaluate(c);

            bool steep = slope >= _s.SteepSlopeMin;
            bool flat = slope <= _s.FlatSlopeMax;
            bool high = elev >= _s.HighElevation;
            bool low = elev <= _s.LowElevation;
            bool concave = curv < -_s.CurvatureFlat;

            // Steep ground: a high summit reads as Mountain, otherwise a Ridge
            // (rough - dry steep uplands).
            if (steep) return high ? LandscapeType.Mountain : LandscapeType.Ridge;

            // Flat high ground is a Plateau.
            if (high && flat) return LandscapeType.Plateau;

            // Water carves valleys.
            if (river) return LandscapeType.Valley;

            // Concave dry ground: low = a closed Basin, mid = a rough Pass.
            if (concave) return low ? LandscapeType.Basin : LandscapeType.Pass;

            // Everything else: a Plain.
            return LandscapeType.Plain;
        }
    }
}
