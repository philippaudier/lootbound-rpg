using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How hard is it to move THROUGH this point?"
    ///
    /// Unit: dimensionless local cost. Range: [base, +inf). Reads: SlopeField,
    /// CliffField, RoughnessField, RiverMaskField (upstream). This is a LOCAL
    /// COST only - it computes no path. A path planner (T3.1) integrates
    /// distance x cost; distance is the planner's job, never the field's.
    /// V1 algorithm: base + weighted slope + cliff penalty + weighted roughness +
    /// water penalty. Swappable behind the interface. Assumptions: penalties are
    /// additive and independent. Limits: elevation change is represented via
    /// slope (no separate term in V1); weights are hand-tuned. Future consumers:
    /// Procedural Paths (T3.1), enemy AI, wildlife movement.
    /// </summary>
    public sealed class TraversabilityField : IWorldField<float>
    {
        private readonly IWorldField<float> _slope;
        private readonly IWorldField<bool> _cliff;
        private readonly IWorldField<float> _roughness;
        private readonly IWorldField<bool> _river;
        private readonly WorldKnowledgeSettings _s;

        public TraversabilityField(
            IWorldField<float> slope,
            IWorldField<bool> cliff,
            IWorldField<float> roughness,
            IWorldField<bool> river,
            WorldKnowledgeSettings settings)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _cliff = cliff ?? throw new ArgumentNullException(nameof(cliff));
            _roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            _river = river ?? throw new ArgumentNullException(nameof(river));
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public float Evaluate(WorldCoordinate c)
        {
            float cost = _s.TraversalBaseCost;
            cost += _s.SlopeCostPerDegree * _slope.Evaluate(c);
            if (_cliff.Evaluate(c)) cost += _s.CliffCost;
            cost += _s.RoughnessCostPerMetre * _roughness.Evaluate(c);
            if (_river.Evaluate(c)) cost += _s.WaterCost;
            return cost;
        }
    }
}
