using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World.Ambience;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Rendering.PBSky;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the ambience chain on the real PBSky applier: the
    /// baseline is captured from an actual global volume, the shared profile
    /// is never written, the runtime volume drives the fog continuously
    /// (no jumps), disabling restores the exact lighting, and re-enabling
    /// never duplicates the runtime volume.
    /// </summary>
    public class WorldAmbiencePlayModeTests
    {
        private const float WORLD_RADIUS = 64f;
        private const float BASELINE_MEAN_FREE_PATH = 400f;
        private const float BASELINE_MAX_FOG_DISTANCE = 5000f;
        private const float BASELINE_LIGHT_INTENSITY = 1.23f;
        private const string RUNTIME_VOLUME_NAME = "WorldAmbience_RuntimeVolume";

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();
        private float savedAmbientIntensity;

        [SetUp]
        public void SetUp()
        {
            savedAmbientIntensity = RenderSettings.ambientIntensity;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();

            foreach (var asset in assets)
            {
                if (asset != null) Object.Destroy(asset);
            }
            assets.Clear();

            RenderSettings.ambientIntensity = savedAmbientIntensity;
        }

        private GameObject Track(GameObject go) { spawned.Add(go); return go; }
        private T TrackAsset<T>(T asset) where T : Object { assets.Add(asset); return asset; }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        #region Setup

        private sealed class Rig
        {
            public Volume GlobalVolume;
            public VolumeProfile SharedProfile;
            public global::Fog SceneFog;
            public Light DirectionalLight;
            public Transform Player;
            public PBSkyWorldAmbienceApplier Applier;
            public WorldAmbienceController Controller;
            public WorldAmbienceConfig Config;
            public WorldProgression Progression;
        }

        /// <summary>
        /// Build a miniature production chain: a global volume carrying a real
        /// PBSky Fog profile (the scene baseline), a directional light, and a
        /// controller + PBSky applier reading a real WorldProgression.
        /// </summary>
        private Rig CreateRig(bool withConfig = true, float transitionSpeed = 5f)
        {
            var rig = new Rig();

            rig.SharedProfile = TrackAsset(ScriptableObject.CreateInstance<VolumeProfile>());
            rig.SceneFog = rig.SharedProfile.Add<global::Fog>(overrides: true);
            rig.SceneFog.meanFreePath.value = BASELINE_MEAN_FREE_PATH;
            rig.SceneFog.tint.value = Color.white;
            rig.SceneFog.maxFogDistance.value = BASELINE_MAX_FOG_DISTANCE;

            var volumeGo = Track(new GameObject("Test_GlobalVolume"));
            rig.GlobalVolume = volumeGo.AddComponent<Volume>();
            rig.GlobalVolume.isGlobal = true;
            rig.GlobalVolume.priority = 0f;
            rig.GlobalVolume.sharedProfile = rig.SharedProfile;

            var lightGo = Track(new GameObject("Test_DirectionalLight"));
            rig.DirectionalLight = lightGo.AddComponent<Light>();
            rig.DirectionalLight.type = LightType.Directional;
            rig.DirectionalLight.intensity = BASELINE_LIGHT_INTENSITY;

            var playerGo = Track(new GameObject("Test_Player"));
            rig.Player = playerGo.transform;

            Vector3 refuge = Vector3.zero;
            rig.Progression = new WorldProgression(refuge, WORLD_RADIUS, WorldRingConfig.CreateDefault());
            rig.Player.position = refuge;

            var ambienceGo = Track(new GameObject("Test_WorldAmbience"));
            ambienceGo.SetActive(false);
            rig.Applier = ambienceGo.AddComponent<PBSkyWorldAmbienceApplier>();
            SetField(rig.Applier, "globalVolume", rig.GlobalVolume);
            SetField(rig.Applier, "directionalLight", rig.DirectionalLight);

            rig.Controller = ambienceGo.AddComponent<WorldAmbienceController>();
            SetField(rig.Controller, "player", rig.Player);
            SetField(rig.Controller, "applier", rig.Applier);

            if (withConfig)
            {
                rig.Config = TrackAsset(ScriptableObject.CreateInstance<WorldAmbienceConfig>());
                SetField(rig.Config, "evaluationInterval", 0.05f);
                SetField(rig.Config, "transitionSpeed", transitionSpeed);
                SetField(rig.Controller, "config", rig.Config);
            }

            rig.Controller.SetProgressionSource(rig.Progression);
            ambienceGo.SetActive(true);
            return rig;
        }

        private static global::Fog FindRuntimeFog(Rig rig)
        {
            var child = rig.Applier.transform.Find(RUNTIME_VOLUME_NAME);
            if (child == null) return null;
            var volume = child.GetComponent<Volume>();
            // The applier assigns Volume.profile (owned in-memory instance);
            // Volume.sharedProfile stays null for runtime-created volumes.
            if (volume == null || volume.profile == null) return null;
            return volume.profile.TryGet(out global::Fog fog) ? fog : null;
        }

        private static int CountRuntimeVolumes(Rig rig)
        {
            int count = 0;
            foreach (Transform child in rig.Applier.transform)
            {
                if (child.name == RUNTIME_VOLUME_NAME) count++;
            }
            return count;
        }

        #endregion

        [UnityTest]
        public IEnumerator Baseline_IsCapturedFromGlobalVolume()
        {
            var rig = CreateRig();
            yield return null; // first Update captures the baseline

            Assert.IsTrue(rig.Controller.IsReady);
            Assert.AreEqual(BASELINE_MEAN_FREE_PATH, rig.Controller.Baseline.MeanFreePath, 0.001f);
            Assert.AreEqual(BASELINE_MAX_FOG_DISTANCE, rig.Controller.Baseline.MaxFogDistance, 0.001f);
            Assert.AreEqual(Color.white, rig.Controller.Baseline.FogTint);
            Assert.AreEqual(1, CountRuntimeVolumes(rig), "one runtime volume must exist");
        }

        [UnityTest]
        public IEnumerator PreviewDepth_DrivesFogTowardConfiguredMinimum()
        {
            var rig = CreateRig(transitionSpeed: 50f); // fast convergence for the test
            yield return null;

            rig.Controller.PreviewDepthOverride = 1f;
            yield return new WaitForSeconds(0.6f);

            var runtimeFog = FindRuntimeFog(rig);
            Assert.IsNotNull(runtimeFog, "runtime fog override must exist");
            Assert.Less(rig.Controller.CurrentState.MeanFreePath, BASELINE_MEAN_FREE_PATH * 0.6f,
                "deep preview must densify the fog well below the baseline");
            Assert.AreEqual(rig.Controller.CurrentState.MeanFreePath, runtimeFog.meanFreePath.value, 0.5f,
                "the runtime volume must carry the smoothed value");
            Assert.GreaterOrEqual(runtimeFog.meanFreePath.value, rig.Config.MinimumMeanFreePath - 0.5f);

            // maxFogDistance must NOT be overridden in the V1 default.
            Assert.IsFalse(runtimeFog.maxFogDistance.overrideState);
        }

        [UnityTest]
        public IEnumerator SharedProfile_IsNeverModified()
        {
            var rig = CreateRig(transitionSpeed: 50f);
            yield return null;

            rig.Controller.PreviewDepthOverride = 1f;
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(BASELINE_MEAN_FREE_PATH, rig.SceneFog.meanFreePath.value, 0.0001f,
                "the shared scene profile must keep its authored fog density");
            Assert.AreEqual(Color.white, rig.SceneFog.tint.value);
            Assert.AreEqual(BASELINE_MAX_FOG_DISTANCE, rig.SceneFog.maxFogDistance.value, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Transition_HasNoValueJumps()
        {
            var rig = CreateRig(transitionSpeed: 0.8f);
            yield return null;

            rig.Controller.PreviewDepthOverride = 1f;

            // Batchmode frames can be sub-millisecond: wait until the next
            // periodic evaluation has actually picked up the preview target.
            float waitUntil = Time.time + 5f;
            while (rig.Controller.TargetState.MeanFreePath >= BASELINE_MEAN_FREE_PATH - 1f && Time.time < waitUntil)
            {
                yield return null;
            }
            Assert.Less(rig.Controller.TargetState.MeanFreePath, BASELINE_MEAN_FREE_PATH - 1f,
                "the target must pick up the preview depth");

            float previous = rig.Controller.CurrentState.MeanFreePath;
            float span = BASELINE_MEAN_FREE_PATH - rig.Config.MinimumMeanFreePath;
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                float current = rig.Controller.CurrentState.MeanFreePath;
                float delta = Mathf.Abs(current - previous);
                // At speed 0.8 a single frame can only cover a small fraction.
                Assert.Less(delta, span * 0.25f, $"frame {frame}: fog moved {delta:F1}m in one frame");
                previous = current;
            }

            Assert.Less(previous, BASELINE_MEAN_FREE_PATH, "the fog must actually be moving");
        }

        [UnityTest]
        public IEnumerator Disable_RestoresLightingAndRemovesRuntimeVolume()
        {
            var rig = CreateRig(transitionSpeed: 50f);
            float ambientBefore = RenderSettings.ambientIntensity;
            yield return null;

            rig.Controller.PreviewDepthOverride = 1f;
            yield return new WaitForSeconds(0.5f);

            Assert.Less(rig.DirectionalLight.intensity, BASELINE_LIGHT_INTENSITY,
                "deep ambience must dim the directional light");

            rig.Controller.gameObject.SetActive(false);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(BASELINE_LIGHT_INTENSITY, rig.DirectionalLight.intensity, 0.0001f,
                "light intensity must be restored exactly");
            Assert.AreEqual(ambientBefore, RenderSettings.ambientIntensity, 0.0001f,
                "ambient intensity must be restored exactly");
            Assert.AreEqual(0, CountRuntimeVolumes(rig), "the runtime volume must be destroyed");
        }

        [UnityTest]
        public IEnumerator Reenable_DoesNotDuplicateRuntimeVolume()
        {
            var rig = CreateRig();
            yield return null;

            rig.Controller.gameObject.SetActive(false);
            yield return null;
            rig.Controller.gameObject.SetActive(true);
            yield return null;
            yield return null;

            Assert.AreEqual(1, CountRuntimeVolumes(rig), "re-enabling must recreate exactly one runtime volume");
            Assert.IsTrue(rig.Controller.IsReady);
        }

        [UnityTest]
        public IEnumerator MissingConfig_FallsBackCleanlyWithoutTouchingVisuals()
        {
            var rig = CreateRig(withConfig: false);
            yield return null;
            yield return null;

            Assert.IsFalse(rig.Controller.IsReady);
            Assert.AreEqual(0, CountRuntimeVolumes(rig), "no runtime volume without a config");
            Assert.AreEqual(BASELINE_LIGHT_INTENSITY, rig.DirectionalLight.intensity, 0.0001f);
            Assert.AreEqual(BASELINE_MEAN_FREE_PATH, rig.SceneFog.meanFreePath.value, 0.0001f);
        }

        [UnityTest]
        public IEnumerator OutsideDisc_ProducesFiniteValues()
        {
            var rig = CreateRig(transitionSpeed: 50f);
            yield return null;

            rig.Player.position = new Vector3(WORLD_RADIUS * 3f, 0f, WORLD_RADIUS * 3f);
            yield return new WaitForSeconds(0.4f);

            var state = rig.Controller.CurrentState;
            Assert.IsFalse(float.IsNaN(state.MeanFreePath));
            Assert.IsFalse(float.IsNaN(state.DirectionalMultiplier));
            Assert.IsFalse(float.IsNaN(state.SaturationOffset));
            Assert.Greater(state.MeanFreePath, 0f);
            Assert.Less(state.MeanFreePath, BASELINE_MEAN_FREE_PATH,
                "beyond the disc the fog must sit at the edge maximum, not explode");
        }
    }
}
