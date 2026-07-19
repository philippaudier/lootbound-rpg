using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Lootbound.Core.Logging;
using Lootbound.Gameplay.World.Ambience;

namespace Lootbound.Rendering.PBSky
{
    /// <summary>
    /// Concrete ambience applier for the PBSkyURP rendering stack.
    ///
    /// Owns a dedicated runtime Volume ("WorldAmbience_RuntimeVolume",
    /// global, priority 10, weight 1) whose in-memory profile contains ONLY
    /// the overrides this system drives: PBSky Fog (meanFreePath + tint, and
    /// maxFogDistance only when explicitly controlled), ColorAdjustments
    /// (saturation + contrast) and WhiteBalance (temperature). Every other
    /// fog/sky/cloud parameter keeps coming from the existing Global Volume -
    /// baseHeight, colorMode, the sky and the clouds are never touched, and
    /// the shared VolumeProfile asset is never modified.
    ///
    /// The fog baseline is READ from the existing global profile so Depth 0
    /// renders exactly like the scene did before this system existed.
    /// Directional/ambient light use captured baselines with multipliers
    /// (day/night friendly; RefreshLightingBaseline is the future hook).
    /// </summary>
    public sealed class PBSkyWorldAmbienceApplier : WorldAmbienceApplierBase
    {
        private const string LogCategory = "WorldAmbience";
        private const string RuntimeVolumeName = "WorldAmbience_RuntimeVolume";

        [Header("Scene References")]
        [SerializeField]
        [Tooltip("The existing Global Volume whose PBSky Fog provides the visual baseline (read-only)")]
        private Volume globalVolume;

        [SerializeField]
        [Tooltip("Scene directional light (intensity driven as baseline x multiplier; color untouched)")]
        private Light directionalLight;

        private GameObject runtimeVolumeObject;
        private VolumeProfile runtimeProfile;
        private global::Fog runtimeFog;
        private ColorAdjustments runtimeColorAdjustments;
        private WhiteBalance runtimeWhiteBalance;

        private float baselineDirectionalIntensity;
        private float baselineAmbientIntensity;
        private bool lightingBaselineCaptured;
        private string fallbackNote = "";

        public override bool TryCaptureBaseline(out WorldAmbienceBaseline baseline)
        {
            fallbackNote = "";

            // Fog baseline: read from the existing shared profile (never written).
            if (globalVolume != null && globalVolume.sharedProfile != null &&
                globalVolume.sharedProfile.TryGet(out global::Fog sceneFog))
            {
                baseline = new WorldAmbienceBaseline(
                    sceneFog.meanFreePath.value,
                    sceneFog.tint.value,
                    sceneFog.maxFogDistance.value);
            }
            else
            {
                baseline = WorldAmbienceBaseline.Default;
                fallbackNote = "no PBSky Fog found on the global volume - using default fog baseline";
                LootboundLog.Warning(LogCategory, fallbackNote);
            }

            RefreshLightingBaseline();
            EnsureRuntimeVolume();
            return true;
        }

        public override void RefreshLightingBaseline()
        {
            baselineDirectionalIntensity = directionalLight != null ? directionalLight.intensity : 1f;
            baselineAmbientIntensity = RenderSettings.ambientIntensity;
            lightingBaselineCaptured = true;
        }

        public override void Apply(in WorldAmbienceState state)
        {
            if (runtimeFog == null)
            {
                EnsureRuntimeVolume();
                if (runtimeFog == null) return;
            }

            runtimeFog.meanFreePath.value = state.MeanFreePath;
            runtimeFog.tint.value = state.FogTint;
            runtimeFog.maxFogDistance.overrideState = state.ControlMaxFogDistance;
            if (state.ControlMaxFogDistance)
            {
                runtimeFog.maxFogDistance.value = state.MaxFogDistance;
            }

            runtimeColorAdjustments.saturation.value = state.SaturationOffset;
            runtimeColorAdjustments.contrast.value = state.ContrastOffset;
            runtimeWhiteBalance.temperature.value = state.TemperatureOffset;

            if (directionalLight != null && lightingBaselineCaptured)
            {
                directionalLight.intensity = baselineDirectionalIntensity * state.DirectionalMultiplier;
            }

            if (lightingBaselineCaptured)
            {
                RenderSettings.ambientIntensity = baselineAmbientIntensity * state.AmbientMultiplier;
            }
        }

        public override void Restore()
        {
            // Owned overrides: destroying the runtime volume removes them.
            if (runtimeVolumeObject != null)
            {
                Destroy(runtimeVolumeObject);
                runtimeVolumeObject = null;
            }

            if (runtimeProfile != null)
            {
                Destroy(runtimeProfile);
                runtimeProfile = null;
            }

            runtimeFog = null;
            runtimeColorAdjustments = null;
            runtimeWhiteBalance = null;

            // External values: written back exactly.
            if (lightingBaselineCaptured)
            {
                if (directionalLight != null)
                {
                    directionalLight.intensity = baselineDirectionalIntensity;
                }
                RenderSettings.ambientIntensity = baselineAmbientIntensity;
            }
        }

        public override string StatusDescription
        {
            get
            {
                string volume = runtimeVolumeObject != null ? "runtime volume OK" : "no runtime volume";
                string light = directionalLight != null ? "light OK" : "no directional light";
                string note = string.IsNullOrEmpty(fallbackNote) ? "" : $"  [{fallbackNote}]";
                return $"{volume}, {light}{note}";
            }
        }

        private void OnDestroy()
        {
            Restore();
        }

        /// <summary>
        /// Create the dedicated runtime volume. Only the parameters this
        /// system drives get their overrideState enabled; everything else
        /// stays inherited from the existing Global Volume.
        /// </summary>
        private void EnsureRuntimeVolume()
        {
            if (runtimeVolumeObject != null)
            {
                return;
            }

            runtimeVolumeObject = new GameObject(RuntimeVolumeName);
            runtimeVolumeObject.transform.SetParent(transform, false);

            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "WorldAmbience_RuntimeProfile";

            runtimeFog = runtimeProfile.Add<global::Fog>(overrides: false);
            runtimeFog.meanFreePath.overrideState = true;
            runtimeFog.tint.overrideState = true;
            // enabled, colorMode, baseHeight, maximumHeight etc. stay inherited.

            runtimeColorAdjustments = runtimeProfile.Add<ColorAdjustments>(overrides: false);
            runtimeColorAdjustments.saturation.overrideState = true;
            runtimeColorAdjustments.contrast.overrideState = true;

            runtimeWhiteBalance = runtimeProfile.Add<WhiteBalance>(overrides: false);
            runtimeWhiteBalance.temperature.overrideState = true;

            var volume = runtimeVolumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.weight = 1f;
            volume.profile = runtimeProfile;
        }
    }
}
