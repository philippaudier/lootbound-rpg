using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How hard is it for THIS perception to move through
    /// this point?" (PCE 0.3 - the Terrain Cost System's single mechanism.)
    ///
    /// Unit: dimensionless local cost, range [~BaseCost, +inf). Reads upstream
    /// knowledge only (Slope, Cliff, Roughness, RiverMask, optionally
    /// Landscape) THROUGH a <see cref="TraversalProfile"/>. Cost is never
    /// absolute: the terrain is neutral, and no cost exists without a profile
    /// (PCE invariant 16) - the constructor enforces it.
    ///
    /// This is a VIEW, never an asset: knowledge + profile in, evaluation on
    /// demand out. It is a LOCAL cost - it computes no path; a solver
    /// integrates distance x cost later. V1 algorithm: additive penalties in
    /// the historical order (base, slope, cliff, roughness, water), then the
    /// optional landscape multiplier - so a default profile reproduces the
    /// pre-0.3 TraversabilityField bit for bit. Swappable behind IWorldField.
    /// </summary>
    public sealed class TerrainCostField : IWorldField<float>
    {
        private readonly IWorldField<float> _slope;
        private readonly IWorldField<bool> _cliff;
        private readonly IWorldField<float> _roughness;
        private readonly IWorldField<bool> _river;
        private readonly IWorldField<LandscapeType> _landscape;
        private readonly TraversalProfile _profile;

        public TerrainCostField(
            IWorldField<float> slope,
            IWorldField<bool> cliff,
            IWorldField<float> roughness,
            IWorldField<bool> river,
            IWorldField<LandscapeType> landscape,
            TraversalProfile profile)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _cliff = cliff ?? throw new ArgumentNullException(nameof(cliff));
            _roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            _river = river ?? throw new ArgumentNullException(nameof(river));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            if (profile.LandscapeCostMultipliers != null && landscape == null)
            {
                throw new ArgumentNullException(nameof(landscape),
                    "This profile perceives landscapes; a landscape field is required.");
            }
            _landscape = landscape;
        }

        public float Evaluate(WorldCoordinate c)
        {
            // Historical order preserved exactly (golden-guarded).
            float cost = _profile.BaseCost;
            cost += _profile.SlopeCostPerDegree * _slope.Evaluate(c);
            if (_cliff.Evaluate(c)) cost += _profile.CliffCost;
            cost += _profile.RoughnessCostPerMetre * _roughness.Evaluate(c);
            if (_river.Evaluate(c)) cost += _profile.WaterCost;

            float[] multipliers = _profile.LandscapeCostMultipliers;
            if (multipliers != null)
            {
                int landscape = (int)_landscape.Evaluate(c);
                if (landscape >= 0 && landscape < multipliers.Length)
                {
                    cost *= multipliers[landscape];
                }
            }
            return cost;
        }
    }
}
