using UnityEngine;

namespace Lootbound.Gameplay.World.Ambience
{
    /// <summary>
    /// TRANSLATION of the world's ambience intent into engine parameters.
    /// WorldProgressionConfig stays the intent (curves by depth);
    /// this asset only holds ranges/scalars mapping the normalized intents
    /// to Unity values - never a competing progression curve.
    ///
    /// Contracts of the normalized intents (from WorldRingContext):
    /// - FogDensity01:       0 = clear air, 1 = densest intent
    /// - LightAttenuation01: 0 = full light, 1 = maximum dimming intent
    /// - Saturation01:       1 = fully natural color, 0 = fully desaturated intent
    /// - Temperature01:      1 = warm, 0 = cold
    ///
    /// Note: the default intent curves peak below 1 at the disc edge
    /// (attenuation 0.5, saturation 0.6, temperature 0.25), so the visual
    /// endpoints below are reached through intent x translation. Defaults
    /// are tuned so Edgelands lands near: directional x0.85, ambient x0.80,
    /// saturation -12, temperature -5, fog ~160m.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldAmbienceConfig", menuName = "Lootbound/World/Ambience Config")]
    public class WorldAmbienceConfig : ScriptableObject
    {
        [Header("Fog (PBSky)")]
        [SerializeField, Min(20f)]
        [Tooltip("Mean free path at FULL fog intent (FogDensity01 = 1). Depth 0 always uses the captured baseline (~400m in the current scene). 140m is the available extreme; ~160m already closes a 1024m world significantly.")]
        private float minimumMeanFreePath = 160f;

        [SerializeField]
        [Tooltip("Fog tint drift by depth. Depth 0 is ALWAYS the exact baseline tint; this gradient is only blended in progressively (see influence below).")]
        private Gradient fogTintByDepth = DefaultFogTint();

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly the tint drifts toward the gradient at full depth (0 = never leave baseline)")]
        private float fogTintInfluence = 0.5f;

        [SerializeField]
        [Tooltip("V1 default: off. meanFreePath is the physical density knob; enable only if distant silhouettes stay too crisp, to isolate variables.")]
        private bool controlMaxFogDistance;

        [SerializeField, Min(50f)]
        [Tooltip("Maximum fog distance at full fog intent (only when controlled)")]
        private float minimumMaxFogDistance = 900f;

        [Header("Light (multipliers over the captured baseline)")]
        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Directional multiplier at FULL attenuation intent (LightAttenuation01 = 1). Default intent peaks at 0.5 -> Edgelands lands at ~x0.85.")]
        private float minimumDirectionalMultiplier = 0.70f;

        [SerializeField, Range(0.3f, 1f)]
        [Tooltip("Ambient intensity multiplier at FULL attenuation intent. Default intent peaks at 0.5 -> Edgelands lands at ~x0.80.")]
        private float minimumAmbientMultiplier = 0.60f;

        [Header("Color Grading (offsets on overrides owned by this system)")]
        [SerializeField, Range(-50f, 0f)]
        [Tooltip("URP saturation at FULLY desaturated intent (Saturation01 = 0). Default intent floors at 0.6 -> Edgelands lands at ~-12.")]
        private float minimumSaturationOffset = -30f;

        [SerializeField, Range(-20f, 0f)]
        [Tooltip("White balance temperature at FULL cold intent (Temperature01 = 0). Default intent floors at 0.25 -> Edgelands lands at ~-5.")]
        private float minimumTemperatureOffset = -7f;

        [SerializeField, Range(0f, 20f)]
        [Tooltip("White balance temperature at FULL warm intent (Temperature01 = 1). Default refuge intent 0.7 -> ~-0.7, imperceptible.")]
        private float maximumTemperatureOffset = 2f;

        [SerializeField]
        [Tooltip("Slight contrast increase with depth (presentation only, no dedicated intent)")]
        private bool enableContrast = true;

        [SerializeField, Range(0f, 15f)]
        [Tooltip("URP contrast at Depth01 = 1")]
        private float maximumContrast = 4f;

        [Header("Sky (PBSky artistic overrides only - never precomputation parameters)")]
        [SerializeField]
        [Tooltip("Zenith tint drift target at full depth. Depth 0 is ALWAYS the exact baseline tint.")]
        private Color skyZenithTintTarget = new Color(0.94f, 0.96f, 1.00f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly the zenith tint drifts toward the target at full depth (0 = never leave baseline)")]
        private float skyZenithTintInfluence = 0.35f;

        [SerializeField]
        [Tooltip("Horizon tint drift target at full depth (slightly desaturated grey: a less welcoming horizon)")]
        private Color skyHorizonTintTarget = new Color(0.90f, 0.92f, 0.95f);

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How strongly the horizon tint drifts toward the target at full depth")]
        private float skyHorizonTintInfluence = 0.50f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("PBSky color saturation at FULLY desaturated intent (Saturation01 = 0). Default intent floors at 0.6 -> Edgelands lands at ~0.96: a flatter, less familiar sky.")]
        private float minimumSkyColorSaturation = 0.90f;

        [SerializeField]
        [Tooltip("V1 default: off. When off, the sky exposure override is NOT created and the global profile stays fully in charge. Enable only if the deep world should feel dimmer from the sky itself (composes with the ambient multiplier).")]
        private bool controlSkyExposure;

        [SerializeField, Range(-1f, 0f)]
        [Tooltip("Sky exposure offset in EV at FULL attenuation intent (only when controlled)")]
        private float minimumSkyExposureOffset = -0.20f;

        [Header("Ambient Activities (0..1 intents consumed by the ambient event system)")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Bird activity at Depth01 = 1 (Refuge is always 1). Drives event frequency, never a final gain.")]
        private float birdActivityMinimum = 0.05f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Insect activity at Depth01 = 1 (Refuge is always 1)")]
        private float insectActivityMinimum = 0f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wind activity at Depth01 = 0 (the Refuge keeps a faint breeze)")]
        private float windActivityMinimum = 0.20f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wind activity at Depth01 = 1")]
        private float windActivityMaximum = 0.85f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Rare/environmental event activity at Depth01 = 0 (nearly absent at the Refuge)")]
        private float rareActivityMinimum = 0.02f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Rare/environmental event activity at Depth01 = 1")]
        private float rareActivityMaximum = 0.30f;

        [Header("Timing")]
        [SerializeField, Range(0.05f, 2f)]
        [Tooltip("Seconds between context evaluations at the player position")]
        private float evaluationInterval = 0.2f;

        [SerializeField, Range(0.05f, 5f)]
        [Tooltip("Smoothing speed (1 - exp(-speed*dt)); ~0.4 converges over several seconds of walking")]
        private float transitionSpeed = 0.4f;

        public float MinimumMeanFreePath => minimumMeanFreePath;
        public Gradient FogTintByDepth => fogTintByDepth;
        public float FogTintInfluence => fogTintInfluence;
        public bool ControlMaxFogDistance => controlMaxFogDistance;
        public float MinimumMaxFogDistance => minimumMaxFogDistance;
        public float MinimumDirectionalMultiplier => minimumDirectionalMultiplier;
        public float MinimumAmbientMultiplier => minimumAmbientMultiplier;
        public float MinimumSaturationOffset => minimumSaturationOffset;
        public float MinimumTemperatureOffset => minimumTemperatureOffset;
        public float MaximumTemperatureOffset => maximumTemperatureOffset;
        public bool EnableContrast => enableContrast;
        public float MaximumContrast => maximumContrast;
        public Color SkyZenithTintTarget => skyZenithTintTarget;
        public float SkyZenithTintInfluence => skyZenithTintInfluence;
        public Color SkyHorizonTintTarget => skyHorizonTintTarget;
        public float SkyHorizonTintInfluence => skyHorizonTintInfluence;
        public float MinimumSkyColorSaturation => minimumSkyColorSaturation;
        public bool ControlSkyExposure => controlSkyExposure;
        public float MinimumSkyExposureOffset => minimumSkyExposureOffset;
        public float BirdActivityMinimum => birdActivityMinimum;
        public float InsectActivityMinimum => insectActivityMinimum;
        public float WindActivityMinimum => windActivityMinimum;
        public float WindActivityMaximum => windActivityMaximum;
        public float RareActivityMinimum => rareActivityMinimum;
        public float RareActivityMaximum => rareActivityMaximum;
        public float EvaluationInterval => evaluationInterval;
        public float TransitionSpeed => transitionSpeed;

        private static Gradient DefaultFogTint()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.93f, 0.95f, 0.97f), 0.5f),
                    new GradientColorKey(new Color(0.82f, 0.88f, 0.90f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return gradient;
        }
    }
}
