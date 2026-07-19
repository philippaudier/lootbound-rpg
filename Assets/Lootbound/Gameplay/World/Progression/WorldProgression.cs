using UnityEngine;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Gameplay.World.Progression
{
    /// <summary>
    /// Single authority turning a world position into its progression context.
    /// Pure C# (no singleton, no scene lookup): constructed once per validated
    /// layout and carried by WorldLayoutContext; consumers receive it through
    /// existing references or explicit injection.
    ///
    /// Geometry (distance, normalized radius, ring assignment) stays the
    /// responsibility of WorldRingEvaluator; this class maps that geometry to
    /// gameplay/ambience expectations through WorldProgressionConfig curves.
    /// Rings never depend on the seed: for a given distance and world radius
    /// the result is always identical.
    /// </summary>
    public sealed class WorldProgression
    {
        private readonly Vector3 refugePosition;
        private readonly float worldDiscRadius;
        private readonly WorldRingConfig ringConfig;
        private readonly WorldProgressionConfig config;

        public Vector3 RefugePosition => refugePosition;
        public float WorldDiscRadius => worldDiscRadius;

        public WorldProgression(
            Vector3 refugePosition,
            float worldDiscRadius,
            WorldRingConfig ringConfig,
            WorldProgressionConfig config = null)
        {
            this.refugePosition = refugePosition;
            this.worldDiscRadius = worldDiscRadius;
            this.ringConfig = ringConfig;
            this.config = config;
        }

        /// <summary>
        /// The unique progression context of a world position.
        /// </summary>
        public WorldRingContext GetContext(Vector3 position)
        {
            var sample = WorldRingEvaluator.Evaluate(position, refugePosition, worldDiscRadius, ringConfig);
            return BuildContext(sample);
        }

        /// <summary>
        /// Context from a pre-computed distance to the Refuge (reservations,
        /// recipes and nodes already carry it).
        /// </summary>
        public WorldRingContext GetContextFromDistance(float distanceFromRefuge)
        {
            var sample = WorldRingEvaluator.EvaluateFromDistance(distanceFromRefuge, worldDiscRadius, ringConfig);
            return BuildContext(sample);
        }

        private WorldRingContext BuildContext(in WorldRingSample sample)
        {
            float depth = Mathf.Clamp01(sample.NormalizedWorldRadius);
            bool insideDisc = sample.NormalizedWorldRadius <= 1f;

            float difficulty;
            int lootTier;
            float fog, light, saturation, temperature;

            if (config != null)
            {
                difficulty = Mathf.Clamp01(config.DifficultyByDepth.Evaluate(depth));
                lootTier = TierFromCurve(config.LootTierByDepth.Evaluate(depth), config.MaxLootTier);
                fog = Mathf.Clamp01(config.FogDensityByDepth.Evaluate(depth));
                light = Mathf.Clamp01(config.LightAttenuationByDepth.Evaluate(depth));
                saturation = Mathf.Clamp01(config.SaturationByDepth.Evaluate(depth));
                temperature = Mathf.Clamp01(config.TemperatureByDepth.Evaluate(depth));
            }
            else
            {
                // Built-in linear defaults: usable without any authored asset.
                difficulty = depth;
                lootTier = TierFromCurve(depth, maxTier: 4);
                fog = depth * 0.8f;
                light = depth * 0.5f;
                saturation = Mathf.Lerp(1f, 0.6f, depth);
                temperature = Mathf.Lerp(0.7f, 0.25f, depth);
            }

            return new WorldRingContext(
                sample.Ring,
                sample.DistanceFromRefuge,
                depth,
                insideDisc,
                difficulty,
                lootTier,
                fog,
                light,
                saturation,
                temperature);
        }

        private static int TierFromCurve(float normalizedTier, int maxTier)
        {
            float clamped = Mathf.Clamp01(normalizedTier);
            // Even tier bands over 0-1; exactly 1 stays in the top tier.
            return Mathf.Min(maxTier, Mathf.FloorToInt(clamped * (maxTier + 1)));
        }
    }
}
