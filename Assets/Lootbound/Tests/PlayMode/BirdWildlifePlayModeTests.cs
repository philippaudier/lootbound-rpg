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
using Lootbound.Presentation.Wildlife;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the wildlife presenter on a bare rig (no audio,
    /// no PBSky, no terrain): flock lifecycle, natural finish tombstones,
    /// rescan without duplicates, explicit development fallback, presenter
    /// isolation and leak-free destruction.
    /// </summary>
    public class BirdWildlifePlayModeTests
    {
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

        private static T GetField<T>(object obj, string fieldName) where T : class
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            return field.GetValue(obj) as T;
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
            public AmbientWildlifePresenter Presenter;
            public AmbientEventProfile Profile;
            public int SpawnedCount;
        }

        private Rig CreateRig(
            AmbientEventCategory category,
            BirdVisualLibrary library,
            bool developmentFallback = false,
            float eventLifetime = 12f,
            int maxConcurrent = 1,
            float eventCooldown = 30f)
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

            rig.Profile = TrackAsset(ScriptableObject.CreateInstance<AmbientEventProfile>());
            SetField(rig.Profile, "eventId", "test_bird_flight");
            SetField(rig.Profile, "category", category);
            SetField(rig.Profile, "weight", 1f);
            SetField(rig.Profile, "activityResponse", AnimationCurve.Constant(0f, 1f, 1f));
            SetField(rig.Profile, "lifetimeRange", new Vector2(eventLifetime, eventLifetime));
            SetField(rig.Profile, "cooldownRange", new Vector2(eventCooldown, eventCooldown));
            SetField(rig.Profile, "distanceRange", new Vector2(5f, 12f));
            SetField(rig.Profile, "heightOffsetRange", Vector2.zero);
            SetField(rig.Profile, "maxConcurrent", maxConcurrent);

            var directorGo = Track(new GameObject("Test_EventDirector"));
            directorGo.SetActive(false);
            rig.Director = directorGo.AddComponent<AmbientEventDirector>();
            SetField(rig.Director, "ambienceController", controller);
            SetField(rig.Director, "player", rig.Player);
            SetField(rig.Director, "profiles", new List<AmbientEventProfile> { rig.Profile });
            SetField(rig.Director, "evaluationInterval", 0.05f);
            SetField(rig.Director, "baseChancePerEvaluation", 1f);
            SetField(rig.Director, "minimumSecondsBetweenSpawns", 0f);
            SetField(rig.Director, "randomSeed", 12345);
            rig.Director.OnEventSpawned += _ => rig.SpawnedCount++;

            var presenterGo = Track(new GameObject("Test_WildlifePresenter"));
            presenterGo.SetActive(false);
            rig.Presenter = presenterGo.AddComponent<AmbientWildlifePresenter>();
            SetField(rig.Presenter, "eventDirector", rig.Director);
            SetField(rig.Presenter, "birdLibrary", library);
            SetField(rig.Presenter, "enableDevelopmentFallback", developmentFallback);
            presenterGo.SetActive(true);

            directorGo.SetActive(true);
            return rig;
        }

        private BirdVisualLibrary CreateLibraryWithTemplate(
            out GameObject template, Vector2Int groupSize, Vector2 flightDuration)
        {
            template = Track(new GameObject("Test_BirdTemplate"));
            template.transform.position = new Vector3(-1000f, 0f, -1000f);

            var variant = new BirdVisualVariant();
            SetField(variant, "prefab", template);
            SetField(variant, "weight", 1f);

            var library = TrackAsset(ScriptableObject.CreateInstance<BirdVisualLibrary>());
            SetField(library, "variants", new[] { variant });
            SetField(library, "groupSizeRange", groupSize);
            SetField(library, "flightDurationRange", flightDuration);
            return library;
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline)
            {
                yield return null;
            }
        }

        private static int CountSceneObjectsStartingWith(string prefix)
        {
            int count = 0;
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform.name.StartsWith(prefix)) count++;
            }

            return count;
        }

        [UnityTest]
        public IEnumerator BirdEvent_CreatesExactlyOneFlockWithExactBirds()
        {
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(3, 3), new Vector2(30f, 30f));
            var rig = CreateRig(AmbientEventCategory.Birds, library);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount > 0, 3f);

            Assert.AreEqual(1, rig.Presenter.ActiveFlockCount);
            Assert.AreEqual(3, rig.Presenter.ActiveBirdCount, "group size (3,3) means exactly 3 birds");
            Assert.AreEqual(1, CountSceneObjectsStartingWith("BirdFlock_"));
        }

        [UnityTest]
        public IEnumerator NonBirdEvent_IsIgnored()
        {
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(3, 3), new Vector2(30f, 30f));
            var rig = CreateRig(AmbientEventCategory.Wind, library);

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(0, rig.Presenter.ActiveFlockCount);
        }

        [UnityTest]
        public IEnumerator Rescan_NeverDuplicatesAFlight()
        {
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(2, 2), new Vector2(30f, 30f));
            var rig = CreateRig(AmbientEventCategory.Birds, library);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount > 0, 3f);

            rig.Presenter.gameObject.SetActive(false);
            yield return null;
            rig.Presenter.gameObject.SetActive(true);
            yield return null;

            Assert.AreEqual(1, rig.Presenter.ActiveFlockCount, "re-enable recreates exactly one flight");
            Assert.AreEqual(1, CountSceneObjectsStartingWith("BirdFlock_"));
        }

        [UnityTest]
        public IEnumerator EventRelease_ReleasesFlockWithoutResidue()
        {
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(2, 2), new Vector2(30f, 30f));
            var rig = CreateRig(AmbientEventCategory.Birds, library, eventLifetime: 0.4f);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount > 0, 3f);
            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount == 0, 3f);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(0, rig.Presenter.ActiveFlockCount);
            Assert.AreEqual(0, rig.Presenter.ActiveBirdCount);
            Assert.AreEqual(0, CountSceneObjectsStartingWith("BirdFlock_"));
        }

        [UnityTest]
        public IEnumerator NaturalFinish_TombstonesAndNeverReplays_ThenReleaseCleansUp()
        {
            // Flight ~2s (planner floor); event lives much longer.
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(2, 2), new Vector2(2f, 2f));
            var rig = CreateRig(AmbientEventCategory.Birds, library, eventLifetime: 9f);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount > 0, 3f);
            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount == 0, 8f);
            yield return null;

            Assert.AreEqual(1, rig.Director.ActiveInstances.Count, "the event itself must still be alive");
            Assert.AreEqual(1, rig.Presenter.CompletedInstanceCount, "the finished flight is remembered");
            Assert.AreEqual(0, CountSceneObjectsStartingWith("BirdFlock_"));

            // Disable/re-enable while the event is still active: no replay.
            rig.Presenter.gameObject.SetActive(false);
            yield return null;
            rig.Presenter.gameObject.SetActive(true);
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(0, rig.Presenter.ActiveFlockCount, "a finished flight must never reappear");
            Assert.AreEqual(1, rig.Presenter.CompletedInstanceCount);

            // Event release cleans the tombstone without error.
            yield return WaitUntilOrTimeout(() => rig.Director.ActiveInstances.Count == 0, 12f);
            yield return null;

            Assert.AreEqual(0, rig.Presenter.CompletedInstanceCount, "released events leave no tombstone");
        }

        [UnityTest]
        public IEnumerator EmptyLibrary_FallbackOff_ProducesNothing()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, library: null, developmentFallback: false);

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return new WaitForSeconds(0.3f);

            Assert.Greater(rig.SpawnedCount, 0, "the event itself must spawn");
            Assert.AreEqual(0, rig.Presenter.ActiveFlockCount, "no valid variant + fallback off = nothing");
            Assert.AreEqual(0, CountSceneObjectsStartingWith("BirdFlock_"));
        }

        [UnityTest]
        public IEnumerator Fallback_On_CreatesAndDestroysCleanly()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, library: null, developmentFallback: true);

            yield return WaitUntilOrTimeout(() => rig.Presenter.ActiveFlockCount > 0, 3f);

            Assert.AreEqual(1, rig.Presenter.ActiveFlockCount);
            Assert.GreaterOrEqual(rig.Presenter.ActiveBirdCount, 2);
            Assert.LessOrEqual(rig.Presenter.ActiveBirdCount, 6);

            var material = GetField<Material>(rig.Presenter, "fallbackMaterial");
            Assert.IsNotNull(material, "the presenter owns one shared fallback material");

            Object.Destroy(rig.Presenter.gameObject);
            yield return null;
            yield return null;

            Assert.AreEqual(0, CountSceneObjectsStartingWith("BirdFlock_"), "no presentation residue");
            Assert.IsTrue(material == null, "the presenter destroys its own runtime material");
        }

        [UnityTest]
        public IEnumerator TwoPresenters_DoNotDestroyEachOthersResources()
        {
            var rig = CreateRig(AmbientEventCategory.Birds, library: null, developmentFallback: true);

            // Second presenter observing the same director, own fallback material.
            var secondGo = Track(new GameObject("Test_WildlifePresenter_B"));
            secondGo.SetActive(false);
            var second = secondGo.AddComponent<AmbientWildlifePresenter>();
            SetField(second, "eventDirector", rig.Director);
            SetField(second, "birdLibrary", null);
            SetField(second, "enableDevelopmentFallback", true);
            secondGo.SetActive(true);

            yield return WaitUntilOrTimeout(
                () => rig.Presenter.ActiveFlockCount > 0 && second.ActiveFlockCount > 0, 3f);

            var materialA = GetField<Material>(rig.Presenter, "fallbackMaterial");
            Assert.IsNotNull(materialA);

            Object.Destroy(secondGo);
            yield return null;
            yield return null;

            Assert.AreEqual(1, rig.Presenter.ActiveFlockCount, "presenter A keeps its flight");
            Assert.IsTrue(materialA != null, "destroying presenter B must not destroy A's material");
        }

        [UnityTest]
        public IEnumerator MultipleBirdEvents_OneFlockEach()
        {
            var library = CreateLibraryWithTemplate(out _, new Vector2Int(2, 2), new Vector2(30f, 30f));
            var rig = CreateRig(AmbientEventCategory.Birds, library,
                eventLifetime: 20f, maxConcurrent: 3, eventCooldown: 0f);

            yield return WaitUntilOrTimeout(() => rig.Director.ActiveInstances.Count >= 2, 5f);
            yield return new WaitForSeconds(0.2f);

            Assert.GreaterOrEqual(rig.Director.ActiveInstances.Count, 2);
            Assert.AreEqual(rig.Director.ActiveInstances.Count, rig.Presenter.ActiveFlockCount,
                "exactly one flock per live bird event");
            Assert.AreEqual(rig.Presenter.ActiveFlockCount * 2, rig.Presenter.ActiveBirdCount,
                "group size (2,2) means two birds per flock");
        }
    }
}
