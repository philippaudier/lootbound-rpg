using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Spawning
{
    /// <summary>
    /// Single source of truth for "may this definition appear here, and with
    /// what weight". Used by the planner for selection and by the F7 debug
    /// panel to explain compatibility. Ring windows are INCLUSIVE on both
    /// ends (unlike the spatial ring thresholds, which stay min-inclusive /
    /// max-exclusive); the weight curve is evaluated at the GLOBAL world
    /// depth (Depth01: 0 = Refuge, 1 = disc edge).
    /// </summary>
    public static class WorldContentCompatibility
    {
        /// <summary>
        /// Evaluate a definition's ring window and effective weight at a
        /// position's (ring, depth). Returns false with a human-readable
        /// reason when the definition cannot appear there.
        /// </summary>
        public static bool Evaluate(
            WorldRing ring,
            float depth01,
            WorldRing minimumRing,
            WorldRing maximumRing,
            float selectionWeight,
            AnimationCurve weightByDepth,
            out float effectiveWeight,
            out string incompatibilityReason)
        {
            effectiveWeight = 0f;

            if (ring < minimumRing)
            {
                incompatibilityReason = $"ring {ring} below minimum {minimumRing}";
                return false;
            }

            if (ring > maximumRing)
            {
                incompatibilityReason = $"ring {ring} above maximum {maximumRing}";
                return false;
            }

            float depthMultiplier = weightByDepth != null
                ? Mathf.Max(0f, weightByDepth.Evaluate(Mathf.Clamp01(depth01)))
                : 1f;
            effectiveWeight = Mathf.Max(0f, selectionWeight) * depthMultiplier;

            if (effectiveWeight <= 0f)
            {
                incompatibilityReason = "zero weight at this depth";
                return false;
            }

            incompatibilityReason = null;
            return true;
        }

        public static bool Evaluate(EncounterDefinition definition, WorldRing ring, float depth01,
            out float effectiveWeight, out string incompatibilityReason)
        {
            return Evaluate(ring, depth01, definition.MinimumRing, definition.MaximumRing,
                definition.SelectionWeight, definition.WeightByDepth, out effectiveWeight, out incompatibilityReason);
        }

        public static bool Evaluate(ResourceSpawnDefinition definition, WorldRing ring, float depth01,
            out float effectiveWeight, out string incompatibilityReason)
        {
            return Evaluate(ring, depth01, definition.MinimumRing, definition.MaximumRing,
                definition.SelectionWeight, definition.WeightByDepth, out effectiveWeight, out incompatibilityReason);
        }

        public static bool Evaluate(LandmarkDefinition definition, WorldRing ring, float depth01,
            out float effectiveWeight, out string incompatibilityReason)
        {
            return Evaluate(ring, depth01, definition.MinimumRing, definition.MaximumRing,
                definition.SelectionWeight, definition.WeightByDepth, out effectiveWeight, out incompatibilityReason);
        }
    }
}
