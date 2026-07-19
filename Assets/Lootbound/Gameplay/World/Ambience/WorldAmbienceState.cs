using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience
{
    /// <summary>
    /// The captured visual baseline of the world at Depth 0. The Refuge must
    /// look EXACTLY like the scene did before this system existed: ambience
    /// enriches the existing render as the player leaves, it never replaces
    /// the art direction at the starting point.
    /// </summary>
    public readonly struct WorldAmbienceBaseline
    {
        /// <summary>PBSky fog mean free path in meters (density; lower = denser).</summary>
        public float MeanFreePath { get; }

        /// <summary>Fog tint of the existing global profile.</summary>
        public Color FogTint { get; }

        /// <summary>Existing maximum fog distance (kept fixed unless explicitly controlled).</summary>
        public float MaxFogDistance { get; }

        /// <summary>PBSky zenith tint of the existing global profile.</summary>
        public Color SkyZenithTint { get; }

        /// <summary>PBSky horizon tint of the existing global profile.</summary>
        public Color SkyHorizonTint { get; }

        /// <summary>PBSky sky color saturation (artistic override, 0..1).</summary>
        public float SkyColorSaturation { get; }

        /// <summary>PBSky sky exposure in EV (only driven when explicitly enabled).</summary>
        public float SkyExposure { get; }

        public WorldAmbienceBaseline(float meanFreePath, Color fogTint, float maxFogDistance)
            : this(meanFreePath, fogTint, maxFogDistance, Color.white, Color.white, 1f, 0f)
        {
        }

        public WorldAmbienceBaseline(
            float meanFreePath, Color fogTint, float maxFogDistance,
            Color skyZenithTint, Color skyHorizonTint, float skyColorSaturation, float skyExposure)
        {
            MeanFreePath = meanFreePath > 1f ? meanFreePath : 400f;
            FogTint = fogTint;
            MaxFogDistance = maxFogDistance > 1f ? maxFogDistance : 5000f;
            SkyZenithTint = skyZenithTint;
            SkyHorizonTint = skyHorizonTint;
            SkyColorSaturation = float.IsNaN(skyColorSaturation) ? 1f : Mathf.Clamp01(skyColorSaturation);
            SkyExposure = float.IsNaN(skyExposure) ? 0f : skyExposure;
        }

        /// <summary>Safe defaults matching the shipped global profile.</summary>
        public static WorldAmbienceBaseline Default => new WorldAmbienceBaseline(400f, Color.white, 5000f);
    }

    /// <summary>
    /// Final engine values requested by the world at one instant. Immutable;
    /// produced by the pure evaluator, smoothed over time by the controller,
    /// consumed by the applier. Light values are MULTIPLIERS over the
    /// captured baseline (day/night friendly); grading values are OFFSETS on
    /// overrides this system owns.
    /// </summary>
    public readonly struct WorldAmbienceState
    {
        public float MeanFreePath { get; }
        public Color FogTint { get; }
        public float MaxFogDistance { get; }
        public bool ControlMaxFogDistance { get; }

        public float DirectionalMultiplier { get; }
        public float AmbientMultiplier { get; }

        public float SaturationOffset { get; }
        public float TemperatureOffset { get; }
        public float ContrastOffset { get; }

        // Sky artistic overrides (PBSky; never touch precomputation parameters)
        public Color SkyZenithTint { get; }
        public Color SkyHorizonTint { get; }
        public float SkyColorSaturation { get; }
        public float SkyExposure { get; }
        public bool ControlSkyExposure { get; }

        // Ambient activity intents (0..1) consumed by the ambient event
        // system: they drive the frequency and eligibility of spatial
        // events, never a final audio gain.
        public float BirdActivity { get; }
        public float InsectActivity { get; }
        public float WindActivity { get; }
        public float RareEventActivity { get; }

        public WorldAmbienceState(
            float meanFreePath, Color fogTint, float maxFogDistance, bool controlMaxFogDistance,
            float directionalMultiplier, float ambientMultiplier,
            float saturationOffset, float temperatureOffset, float contrastOffset,
            Color skyZenithTint, Color skyHorizonTint, float skyColorSaturation,
            float skyExposure, bool controlSkyExposure,
            float birdActivity, float insectActivity, float windActivity, float rareEventActivity)
        {
            MeanFreePath = meanFreePath;
            FogTint = fogTint;
            MaxFogDistance = maxFogDistance;
            ControlMaxFogDistance = controlMaxFogDistance;
            DirectionalMultiplier = directionalMultiplier;
            AmbientMultiplier = ambientMultiplier;
            SaturationOffset = saturationOffset;
            TemperatureOffset = temperatureOffset;
            ContrastOffset = contrastOffset;
            SkyZenithTint = skyZenithTint;
            SkyHorizonTint = skyHorizonTint;
            SkyColorSaturation = skyColorSaturation;
            SkyExposure = skyExposure;
            ControlSkyExposure = controlSkyExposure;
            BirdActivity = Mathf.Clamp01(birdActivity);
            InsectActivity = Mathf.Clamp01(insectActivity);
            WindActivity = Mathf.Clamp01(windActivity);
            RareEventActivity = Mathf.Clamp01(rareEventActivity);
        }

        /// <summary>
        /// The neutral state: exactly the captured baseline. Activities start
        /// at their calm neutral (full life, no wind); the first evaluation
        /// smooths them to the refuge values within seconds.
        /// </summary>
        public static WorldAmbienceState AtBaseline(in WorldAmbienceBaseline baseline)
        {
            return new WorldAmbienceState(
                baseline.MeanFreePath, baseline.FogTint, baseline.MaxFogDistance, false,
                1f, 1f, 0f, 0f, 0f,
                baseline.SkyZenithTint, baseline.SkyHorizonTint, baseline.SkyColorSaturation,
                baseline.SkyExposure, false,
                1f, 1f, 0f, 0f);
        }

        /// <summary>Per-field linear interpolation (factor 0 = current, 1 = target).</summary>
        public static WorldAmbienceState Interpolate(in WorldAmbienceState current, in WorldAmbienceState target, float factor)
        {
            float t = Mathf.Clamp01(factor);
            return new WorldAmbienceState(
                Mathf.Lerp(current.MeanFreePath, target.MeanFreePath, t),
                Color.Lerp(current.FogTint, target.FogTint, t),
                Mathf.Lerp(current.MaxFogDistance, target.MaxFogDistance, t),
                target.ControlMaxFogDistance,
                Mathf.Lerp(current.DirectionalMultiplier, target.DirectionalMultiplier, t),
                Mathf.Lerp(current.AmbientMultiplier, target.AmbientMultiplier, t),
                Mathf.Lerp(current.SaturationOffset, target.SaturationOffset, t),
                Mathf.Lerp(current.TemperatureOffset, target.TemperatureOffset, t),
                Mathf.Lerp(current.ContrastOffset, target.ContrastOffset, t),
                Color.Lerp(current.SkyZenithTint, target.SkyZenithTint, t),
                Color.Lerp(current.SkyHorizonTint, target.SkyHorizonTint, t),
                Mathf.Lerp(current.SkyColorSaturation, target.SkyColorSaturation, t),
                Mathf.Lerp(current.SkyExposure, target.SkyExposure, t),
                target.ControlSkyExposure,
                Mathf.Lerp(current.BirdActivity, target.BirdActivity, t),
                Mathf.Lerp(current.InsectActivity, target.InsectActivity, t),
                Mathf.Lerp(current.WindActivity, target.WindActivity, t),
                Mathf.Lerp(current.RareEventActivity, target.RareEventActivity, t));
        }
    }

    /// <summary>
    /// Framerate-independent smoothing toward a target:
    /// factor = 1 - exp(-speed * dt). A null or invalid speed snaps (safe
    /// fallback) instead of freezing the ambience forever.
    /// </summary>
    public static class WorldAmbienceSmoothing
    {
        public static float Factor(float speed, float deltaTime)
        {
            if (speed <= 0f || float.IsNaN(speed) || deltaTime < 0f || float.IsNaN(deltaTime))
            {
                return 1f;
            }

            return Mathf.Clamp01(1f - Mathf.Exp(-speed * deltaTime));
        }
    }
}
