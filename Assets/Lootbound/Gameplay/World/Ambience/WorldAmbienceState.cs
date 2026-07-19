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

        public WorldAmbienceBaseline(float meanFreePath, Color fogTint, float maxFogDistance)
        {
            MeanFreePath = meanFreePath > 1f ? meanFreePath : 400f;
            FogTint = fogTint;
            MaxFogDistance = maxFogDistance > 1f ? maxFogDistance : 5000f;
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

        public WorldAmbienceState(
            float meanFreePath, Color fogTint, float maxFogDistance, bool controlMaxFogDistance,
            float directionalMultiplier, float ambientMultiplier,
            float saturationOffset, float temperatureOffset, float contrastOffset)
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
        }

        /// <summary>The neutral state: exactly the captured baseline.</summary>
        public static WorldAmbienceState AtBaseline(in WorldAmbienceBaseline baseline)
        {
            return new WorldAmbienceState(
                baseline.MeanFreePath, baseline.FogTint, baseline.MaxFogDistance, false,
                1f, 1f, 0f, 0f, 0f);
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
                Mathf.Lerp(current.ContrastOffset, target.ContrastOffset, t));
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
