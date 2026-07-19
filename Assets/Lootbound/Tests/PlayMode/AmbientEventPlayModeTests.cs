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

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for the ambient event director on a bare rig: no
    /// AudioSource, no clip, no PBSky, no terrain - proving the foundation
    /// carries zero presentation dependency. Covers the full lifecycle
    /// (spawn, expire, release), concurrency caps and disable cleanup.
    /// </summary>
    public class AmbientEventPlayModeTests
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

        /// <summary>Ambience applier that touches nothing: the director needs no rendering stack.</summary>
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
            public WorldAmbienceController Controller;
            public AmbientEventDirector Director;
            public int SpawnedCount;
            public int ReleasedCount;
            public AmbientEventInstance LastSpawned;
        }

        private Rig CreateRig(List<AmbientEventProfile> profiles)
        {
            var rig = new Rig();

            var playerGo = Track(new GameObject("Test_Player"));
            rig.Player = playerGo.transform;
            rig.Player.position = Vector3.zero;

            var ambienceGo = Track(new GameObject("Test_Ambience"));
            ambienceGo.SetActive(false);
            var applier = ambienceGo.AddComponent<NullAmbienceApplier>();
            rig.Controller = ambienceGo.AddComponent<WorldAmbienceController>();
            var config = TrackAsset(ScriptableObject.CreateInstance<WorldAmbienceConfig>());
            SetField(config, "evaluationInterval", 0.05f);
            SetField(config, "transitionSpeed", 50f);
            SetField(rig.Controller, "player", rig.Player);
            SetField(rig.Controller, "applier", applier);
            SetField(rig.Controller, "config", config);
            rig.Controller.SetProgressionSource(new WorldProgression(
                Vector3.zero, 64f, WorldRingConfig.CreateDefault()));
            ambienceGo.SetActive(true);

            var directorGo = Track(new GameObject("Test_EventDirector"));
            directorGo.SetActive(false);
            rig.Director = directorGo.AddComponent<AmbientEventDirector>();
            SetField(rig.Director, "ambienceController", rig.Controller);
            SetField(rig.Director, "player", rig.Player);
            SetField(rig.Director, "profiles", profiles);
            SetField(rig.Director, "evaluationInterval", 0.05f);
            SetField(rig.Director, "baseChancePerEvaluation", 1f);
            SetField(rig.Director, "minimumSecondsBetweenSpawns", 0f);
            SetField(rig.Director, "randomSeed", 12345);
            rig.Director.OnEventSpawned += instance => { rig.SpawnedCount++; rig.LastSpawned = instance; };
            rig.Director.OnEventReleased += _ => rig.ReleasedCount++;
            directorGo.SetActive(true);

            return rig;
        }

        private AmbientEventProfile CreateProfile(
            string id, Vector2 lifetime, Vector2 cooldown, int maxConcurrent = 1)
        {
            var profile = TrackAsset(ScriptableObject.CreateInstance<AmbientEventProfile>());
            SetField(profile, "eventId", id);
            SetField(profile, "category", AmbientEventCategory.Birds);
            SetField(profile, "weight", 1f);
            SetField(profile, "activityResponse", AnimationCurve.Constant(0f, 1f, 1f));
            SetField(profile, "lifetimeRange", lifetime);
            SetField(profile, "cooldownRange", cooldown);
            SetField(profile, "distanceRange", new Vector2(5f, 12f));
            SetField(profile, "heightOffsetRange", Vector2.zero);
            SetField(profile, "maxConcurrent", maxConcurrent);
            return profile;
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.time + timeoutSeconds;
            while (!condition() && Time.time < deadline)
            {
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Director_WithoutProfiles_RunsClean()
        {
            var rig = CreateRig(new List<AmbientEventProfile>());
            yield return new WaitForSeconds(0.3f);

            Assert.AreEqual(0, rig.Director.ActiveInstances.Count);
            Assert.AreEqual(0, rig.SpawnedCount);
            Assert.IsFalse(rig.Director.HasSpawned);
        }

        [UnityTest]
        public IEnumerator PreviewDepth_ChangesActivities()
        {
            var rig = CreateRig(new List<AmbientEventProfile>());
            yield return null;

            rig.Controller.PreviewDepthOverride = 1f;
            yield return new WaitForSeconds(0.5f);

            var state = rig.Controller.CurrentState;
            Assert.Less(state.BirdActivity, 0.2f, "deep preview must starve bird activity");
            Assert.Less(state.InsectActivity, 0.1f);
            Assert.Greater(state.WindActivity, 0.7f, "deep preview must raise wind activity");
            Assert.Greater(state.RareEventActivity, 0.2f);
        }

        [UnityTest]
        public IEnumerator DebugEvent_SpawnsThenExpires_WithoutOrphans()
        {
            var profile = CreateProfile("debug_chirp", new Vector2(0.3f, 0.3f), new Vector2(30f, 30f));
            var rig = CreateRig(new List<AmbientEventProfile> { profile });

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            Assert.AreEqual(1, rig.SpawnedCount, "the debug event must spawn exactly once (long cooldown)");
            Assert.AreEqual(1, rig.Director.ActiveInstances.Count);
            Assert.IsNotNull(rig.LastSpawned.MarkerTransform, "a live instance owns a marker");
            float distance = Vector2.Distance(
                new Vector2(rig.LastSpawned.Position.x, rig.LastSpawned.Position.z),
                new Vector2(rig.Player.position.x, rig.Player.position.z));
            Assert.GreaterOrEqual(distance, 5f - 0.01f, "never on the player");
            Assert.LessOrEqual(distance, 12f + 0.01f);

            var root = rig.Director.transform.Find("AmbientEvents_Active");
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.childCount);

            yield return WaitUntilOrTimeout(() => rig.ReleasedCount > 0, 3f);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(1, rig.ReleasedCount, "released exactly once");
            Assert.AreEqual(0, rig.Director.ActiveInstances.Count);
            Assert.IsTrue(rig.LastSpawned.MarkerTransform == null, "the marker must be destroyed on expiration");
            Assert.AreEqual(0, root.childCount, "the active root must be empty after cleanup");
        }

        [UnityTest]
        public IEnumerator MaxConcurrent_IsRespected()
        {
            // Zero cooldown but MaxConcurrent 1: concurrency is the only gate.
            var profile = CreateProfile("clingy", new Vector2(10f, 10f), new Vector2(0f, 0f));
            var rig = CreateRig(new List<AmbientEventProfile> { profile });

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(1, rig.SpawnedCount, "MaxConcurrent 1 must block further spawns");
            Assert.AreEqual(1, rig.Director.ActiveInstances.Count);
        }

        [UnityTest]
        public IEnumerator Disable_ReleasesEverything()
        {
            var profile = CreateProfile("fleeting", new Vector2(10f, 10f), new Vector2(30f, 30f));
            var rig = CreateRig(new List<AmbientEventProfile> { profile });

            yield return WaitUntilOrTimeout(() => rig.SpawnedCount > 0, 3f);
            Assert.AreEqual(1, rig.Director.ActiveInstances.Count);

            rig.Director.gameObject.SetActive(false);
            yield return null; // let Destroy() finalize

            Assert.AreEqual(1, rig.ReleasedCount, "disable must release active instances exactly once");
            Assert.AreEqual(0, rig.Director.ActiveInstances.Count);
            Assert.IsNull(rig.Director.transform.Find("AmbientEvents_Active"),
                "the active root must be destroyed on disable");

            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                Assert.IsFalse(transform.name.StartsWith("AmbientEvent_"),
                    $"orphan marker left behind: {transform.name}");
            }
        }
    }
}
