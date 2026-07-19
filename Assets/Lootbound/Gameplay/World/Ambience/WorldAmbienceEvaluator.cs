using UnityEngine;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Gameplay.World.Ambience
{
    /// <summary>
    /// Pure translation: WorldRingContext (intent) + WorldAmbienceConfig
    /// (translation ranges) + captured baseline -> WorldAmbienceState (engine
    /// values). No time, no scene, no rendering: fully testable.
    ///
    /// Depth 0 reproduces the baseline exactly (fog, light multipliers 1,
    /// grading near zero). Outside the disc, Depth01 is already clamped to 1
    /// by WorldProgression, so the Void inherits the Edgelands-maximum state
    /// without any extrapolation.
    /// </summary>
    public static class WorldAmbienceEvaluator
    {
        public static WorldAmbienceState Evaluate(
            in WorldRingContext context,
            WorldAmbienceConfig config,
            in WorldAmbienceBaseline baseline)
        {
            if (config == null)
            {
                return WorldAmbienceState.AtBaseline(baseline);
            }

            float depth = Sane01(context.Depth01);
            float fogIntent = Sane01(context.FogDensity01);
            float attenuationIntent = Sane01(context.LightAttenuation01);
            float saturationIntent = Sane01(context.Saturation01, fallback: 1f);
            float temperatureIntent = Sane01(context.Temperature01, fallback: 0.5f);

            float meanFreePath = Mathf.Max(20f,
                Mathf.Lerp(baseline.MeanFreePath, config.MinimumMeanFreePath, fogIntent));

            // Depth 0 = exact baseline tint; the gradient only drifts in
            // progressively, scaled by the configured influence.
            Color gradientColor = config.FogTintByDepth != null
                ? config.FogTintByDepth.Evaluate(depth)
                : baseline.FogTint;
            Color fogTint = Color.Lerp(baseline.FogTint, gradientColor, depth * Mathf.Clamp01(config.FogTintInfluence));

            float maxFogDistance = config.ControlMaxFogDistance
                ? Mathf.Max(50f, Mathf.Lerp(baseline.MaxFogDistance, config.MinimumMaxFogDistance, fogIntent))
                : baseline.MaxFogDistance;

            float directional = Mathf.Lerp(1f, config.MinimumDirectionalMultiplier, attenuationIntent);
            float ambient = Mathf.Lerp(1f, config.MinimumAmbientMultiplier, attenuationIntent);

            // Saturation01 = 1 means fully natural -> offset 0.
            float saturation = Mathf.Lerp(config.MinimumSaturationOffset, 0f, saturationIntent);

            // Temperature01 = 1 means warm, 0 means cold.
            float temperature = Mathf.Lerp(config.MinimumTemperatureOffset, config.MaximumTemperatureOffset, temperatureIntent);

            float contrast = config.EnableContrast ? depth * config.MaximumContrast : 0f;

            // Sky artistic overrides: same drift pattern as the fog tint -
            // Depth 0 is the exact baseline, the targets are only blended in
            // progressively, bounded by their configured influences.
            Color skyZenith = Color.Lerp(baseline.SkyZenithTint, config.SkyZenithTintTarget,
                depth * Mathf.Clamp01(config.SkyZenithTintInfluence));
            Color skyHorizon = Color.Lerp(baseline.SkyHorizonTint, config.SkyHorizonTintTarget,
                depth * Mathf.Clamp01(config.SkyHorizonTintInfluence));

            // Saturation01 = 1 means fully natural -> baseline saturation.
            // The floor never exceeds the baseline: depth can only flatten the
            // sky, never make it more saturated than the scene's own look.
            float skySaturationFloor = Mathf.Min(baseline.SkyColorSaturation, config.MinimumSkyColorSaturation);
            float skySaturation = Mathf.Clamp01(
                Mathf.Lerp(skySaturationFloor, baseline.SkyColorSaturation, saturationIntent));

            float skyExposure = config.ControlSkyExposure
                ? baseline.SkyExposure + Mathf.Lerp(0f, config.MinimumSkyExposureOffset, attenuationIntent)
                : baseline.SkyExposure;

            // Ambient activity intents: life fades with depth, wind and rare
            // manifestations grow. Frequency/eligibility drivers only - the
            // final audio gain will come from real 3D sources later.
            float birdActivity = Mathf.Clamp01(Mathf.Lerp(1f, config.BirdActivityMinimum, depth));
            float insectActivity = Mathf.Clamp01(Mathf.Lerp(1f, config.InsectActivityMinimum, depth));
            float windActivity = Mathf.Clamp01(Mathf.Lerp(config.WindActivityMinimum, config.WindActivityMaximum, depth));
            float rareActivity = Mathf.Clamp01(Mathf.Lerp(config.RareActivityMinimum, config.RareActivityMaximum, depth));

            return new WorldAmbienceState(
                meanFreePath, fogTint, maxFogDistance, config.ControlMaxFogDistance,
                directional, ambient, saturation, temperature, contrast,
                skyZenith, skyHorizon, skySaturation, skyExposure, config.ControlSkyExposure,
                birdActivity, insectActivity, windActivity, rareActivity);
        }

        private static float Sane01(float value, float fallback = 0f)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }

            return Mathf.Clamp01(value);
        }
    }
}
