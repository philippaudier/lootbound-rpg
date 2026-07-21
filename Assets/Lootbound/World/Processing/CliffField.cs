using System;
using Lootbound.World.Coordinates;
using Lootbound.World.Layers.Fields;

namespace Lootbound.World.Processing
{
    /// <summary>
    /// Question answered: "Is this a cliff?"
    ///
    /// Unit: boolean. Reads: SlopeField (upstream). A cliff is a CONCEPT, kept
    /// distinct from a raw slope threshold so the rest of the engine references
    /// "is this a cliff" and the definition can grow (overhangs, exposed rock, a
    /// vertical drop) without touching any consumer. V1 algorithm: slope above a
    /// threshold. Assumptions: cliff == steep. Limits: cannot see undercuts,
    /// drop height, or exposed material yet. Future consumers: Traversability,
    /// Landscape, paths (avoid), climbing, waterfalls (later).
    /// </summary>
    public sealed class CliffField : IWorldField<bool>
    {
        private readonly IWorldField<float> _slope;
        private readonly float _slopeThresholdDegrees;

        public CliffField(IWorldField<float> slope, float slopeThresholdDegrees)
        {
            _slope = slope ?? throw new ArgumentNullException(nameof(slope));
            _slopeThresholdDegrees = slopeThresholdDegrees;
        }

        public bool Evaluate(WorldCoordinate c) => _slope.Evaluate(c) > _slopeThresholdDegrees;
    }
}
