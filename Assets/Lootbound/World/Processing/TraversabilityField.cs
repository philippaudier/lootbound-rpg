using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "How hard is it to move THROUGH this point?" - for
    /// the DEFAULT perception.
    ///
    /// Since PCE 0.3 this is a thin wrapper: the mechanism lives in
    /// <see cref="TerrainCostField"/>, parameterized by a
    /// <see cref="TraversalProfile"/>. This class is exactly "the terrain cost
    /// through the profile built from WorldKnowledgeSettings" - the historical
    /// generic-humanoid perception - kept so existing composition and tests
    /// are untouched. Unit, range, assumptions and limits: see
    /// <see cref="TerrainCostField"/>. It computes no path; a solver
    /// integrates distance x cost later.
    /// </summary>
    public sealed class TraversabilityField : IWorldField<float>
    {
        private readonly TerrainCostField _cost;

        public TraversabilityField(
            IWorldField<float> slope,
            IWorldField<bool> cliff,
            IWorldField<float> roughness,
            IWorldField<bool> river,
            WorldKnowledgeSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _cost = new TerrainCostField(
                slope, cliff, roughness, river, landscape: null,
                TraversalProfile.FromSettings(settings));
        }

        public float Evaluate(WorldCoordinate c) => _cost.Evaluate(c);
    }
}
