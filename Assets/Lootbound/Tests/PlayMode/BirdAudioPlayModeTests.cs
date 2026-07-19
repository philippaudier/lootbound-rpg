using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World.Ambience;
using Lootbound.Gameplay.World.Ambience.Events;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Presentation.Audio;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the bird audio presentation: a Birds event grows a
    /// BirdAudioPresentation child under its marker carrying a configured 3D
    /// AudioSource; non-bird events are ignored; release, disable and
    /// re-enable never leak or duplicate. No assertion depends on
    /// AudioSource.isPlaying (audio output is disabled in batchmode).
    /// </summary>
    public class BirdAudioPlayModeTests
    {
        private const string PRESENTATION_NAME = "BirdAudioPresentation";

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<Object> assets = new List<Object>();

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
        }

        private GameObject Track(GameObject go) { spawned.Add(go); return go; }
        private T TrackAsset<T>(T asset) where T : Object { assets.Add(asset); return asset; }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private sealed class NullAmbienceApplier : WorldAmbienceApplierBase
        {
            public override bool TryCaptureBaseline(out WorldAmbienceBaseline baseline)
            {
                baseline = WorldAmbienceBaseline.Default;
                return true;
            }

            public override void Apply(in WorldAmbienceState state) { }
            public override void Restore() { }
            public override void RefreshLightingBaseline() { }
            public override string StatusDescription => "null applier (test)";
        }

        private sealed class Rig
        {
            public Transform Player;
            public AmbientEventDirector Director;
            public AmbientAudioPresenter Presenter;
            public BirdAudioLibrary Library;
            public AmbientEventProfile Profile;
            public int SpawnedCount;
            public AmbientEventInstance LastSpawned;
        }

        private Rig CreateRig(AmbientEventCategory category, bool withClip = true, float lifetime = 10f)
        {
            var rig = new Rig();

            var playerGo = Track(new GameObject("Test_Player"));
            rig.Player = playerGo.transform;

            var ambienceGo = Track(new GameObject("Test_Ambience"));
            ambienceGo.SetActive(false);
            var applier = ambienceGo.AddComponent<NullAmbienceApplier>();
            var controller = ambienceGo.AddComponent<WorldAmbienceController>();
            var config = TrackAsset(ScriptableObject.CreateInstance<WorldAmbienceConfig>());
            SetField(config, "evaluationInterval", 0.05f);
            SetField(config, "transitionSpeed", 50f);
            SetField(controller, "player", rig.Player);
            SetField(controller, "applier", applier);
            SetField(controller, "config", config);
            controller.SetProgressionSource(new WorldProgression(
                Vector3.zero, 64f, WorldRingConfig.CreateDefault()));
            ambienceGo.SetActive(true);

            var profile = rig.Profile = TrackAsset(ScriptableObject.CreateInstance<AmbientEventProfile>());
            SetField(profile, "eventId", "test_event");
            SetField(profile, "category", category);
            SetField(profile, "weight", 1f);
            SetField(profile, "activityResponse", AnimationCurve.Constant(0f, 1f, 1f));
            SetField(profile, "lifetimeRange", new Vector2(lifetime, lifetime));
            SetField(profile, "cooldownRange", new Vector2(30f, 30f));
            SetField(profile, "distanceRange", new Vector2(5f, 12f));
            SetField(profile, "heightOffsetRange", Vector2.zero);
            SetField(profile, "maxConcurrent", 1);

            var directorGo = Track(new GameObject("Test_EventDirector"));
            directorGo.SetActive(false);
            rig.Director = directorGo.AddComponent<AmbientEventDirector>();
            SetField(rig.Director, "ambienceController", controller);
            SetField(rig.Director, "player", rig.Player);
            SetField(rig.Director, "profiles", new List<AmbientEventProfile> { profile });
            SetField(rig.Director, "evaluationInterval", 0.05f);
            SetField(rig.Director, "baseChancePerEvaluation", 1f);
            SetField(rig.Director, "minimumSecondsBetweenSpawns", 0f);
            SetField(rig.Director, "randomSeed", 12345);
            rig.Director.OnEventSpawned += instance => { rig.SpawnedCount++; rig.LastSpawned = instance; };

            rig.Library = TrackAsset(ScriptableObject.CreateInstance<BirdAudioLibrary>());
            if (withClip)
            {
                var clip = TrackAsset(AudioClip.Create("test_chirp", 4410, 1, 44100, false));
                SetField(rig.Library, "clips", new[] { null, clip });
            }

            var presenterGo = Track(new GameObject("Test_AudioPresenter"));
            presenterGo.SetActive(false);
            rig.Presenter = presenterGo.AddComponent<AmbientAudioPresenter>();
            SetField(rig.Presenter, "eventDirector", rig.Director);
            SetField(rig.Presenter, "birdLibrary", rig.Library);
            SetField(rig.Presenter, "randomSeed", 777);
            presenterGo.SetActive(true);

            directorGo.SetActive(true);
            return rig;
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline)
            {
                yield return null;
            }
        }

        private static Transform FindPresentation(AmbientEventInstance instance)
        {
            return instance?.MarkerTransform != null
                ? instance.MarkerTransform.Find(PRESENTATION_NAME)
                : null;
        }

        [UnityTest]
        public IEnumerator BirdEvent_CreatesConfiguredPresentationChild()
        {
            var rig = CreateRig(AmbientEventCategory.Birds);

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return null;

            Assert.AreEqual(1, rig.Presenter.ActiveSourceCount);
            var presentation = FindPresentation(rig.LastSpawned);
            Assert.IsNotNull(presentation, "the marker must own a BirdAudioPresentation child");

            var source = presentation.GetComponent<AudioSource>();
            Assert.IsNotNull(source);
            Assert.IsNotNull(source.clip, "a valid clip must be assigned (never the null entry)");
            Assert.IsFalse(source.loop);
            Assert.IsFalse(source.playOnAwake);
            Assert.AreEqual(0f, source.dopplerLevel, 0.001f);
            Assert.AreEqual(0f, source.spread, 0.001f);
            Assert.AreEqual(1f, source.spatialBlend, 0.001f, "fully 3D: the sound belongs to the world");
            Assert.AreEqual(AudioRolloffMode.Logarithmic, source.rolloffMode);
            Assert.AreEqual(8f, source.minDistance, 0.001f);
            Assert.AreEqual(45f, source.maxDistance, 0.001f);
            Assert.AreEqual(128, source.priority);
            Assert.GreaterOrEqual(source.pitch, 0.95f - 0.001f);
            Assert.LessOrEqual(source.pitch, 1.05f + 0.001f);
            Assert.GreaterOrEqual(source.volume, 0.90f - 0.001f);
            Assert.LessOrEqual(source.volume, 1.00f + 0.001f);
        }

        [UnityTest]
        public IEnumerator NonBirdEvent_IsIgnored()
        {
            var rig = CreateRig(AmbientEventCategory.Wind);

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return new WaitForSeconds(0.2f);

            Assert.Greater(rig.SpawnedCount, 0, "the wind event itself must spawn");
            Assert.AreEqual(0, rig.Presenter.ActiveSourceCount, "non-bird events get no audio");
            Assert.IsNull(FindPresentation(rig.LastSpawned));
        }

        [UnityTest]
        public IEnumerator Release_DestroysPresentationAndDecrementsCount()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, lifetime: 0.3f);

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return null;
            Assert.AreEqual(1, rig.Presenter.ActiveSourceCount);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveSourceCount == 0, 3f);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(0, rig.Presenter.ActiveSourceCount);
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Assert.AreNotEqual(PRESENTATION_NAME, transform.name, "presentation child leaked after release");
            }
        }

        [UnityTest]
        public IEnumerator EmptyOrAllNullLibrary_CreatesNoSource()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, withClip: false);
            SetField(rig.Library, "clips", new AudioClip[] { null, null });

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return new WaitForSeconds(0.2f);

            Assert.Greater(rig.SpawnedCount, 0);
            Assert.AreEqual(0, rig.Presenter.ActiveSourceCount, "no valid clip means no AudioSource at all");
            Assert.IsNull(FindPresentation(rig.LastSpawned));
        }

        [UnityTest]
        public IEnumerator DisablePresenter_RemovesAudioButKeepsMarkers()
        {
            var rig = CreateRig(AmbientEventCategory.Birds);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveSourceCount > 0, 3f);

            rig.Presenter.gameObject.SetActive(false);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(0, rig.Presenter.ActiveSourceCount);
            Assert.AreEqual(1, rig.Director.ActiveInstances.Count, "the director keeps its instance");
            Assert.IsNotNull(rig.LastSpawned.MarkerTransform, "the marker belongs to the director and survives");
            Assert.AreEqual(0, rig.LastSpawned.MarkerTransform.childCount, "only the audio child is destroyed");
        }

        [UnityTest]
        public IEnumerator ReenablePresenter_RecreatesPresentationExactlyOnce()
        {
            var rig = CreateRig(AmbientEventCategory.Birds);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveSourceCount > 0, 3f);

            rig.Presenter.gameObject.SetActive(false);
            yield return null;
            rig.Presenter.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(1, rig.Presenter.ActiveSourceCount,
                "re-enable must give a voice back to the still-active bird");
            Assert.AreEqual(1, rig.LastSpawned.MarkerTransform.childCount,
                "never two presentations for the same instance");
        }

        [UnityTest]
        public IEnumerator MultipleCycles_NeverLeak()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, lifetime: 0.2f);
            // Short cooldown: several spawn/expire cycles within the window.
            SetField(rig.Profile, "cooldownRange", new Vector2(0.1f, 0.1f));

            yield return new WaitForSeconds(2f);
            yield return null;

            Assert.Greater(rig.SpawnedCount, 1, "several cycles must have happened");

            int presentationCount = 0;
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform.name == PRESENTATION_NAME) presentationCount++;
            }

            Assert.AreEqual(rig.Presenter.ActiveSourceCount, presentationCount,
                "scene presentation objects must exactly match the tracked count");
            Assert.LessOrEqual(rig.Presenter.ActiveSourceCount, rig.Director.ActiveInstances.Count,
                "never more voices than live events");
        }
    }
}
