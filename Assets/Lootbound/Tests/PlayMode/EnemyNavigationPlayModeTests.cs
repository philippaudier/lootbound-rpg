using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Lootbound.Gameplay.Combat;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// Targeted PlayMode tests for enemy navigation behaviours on a real
    /// runtime NavMesh: territory-bounded wandering, readable perception
    /// (FOV, line of sight, immediate range), leash abandon and warp-free
    /// return home. Accelerated profiles keep every test short.
    /// </summary>
    public class EnemyNavigationPlayModeTests
    {
        private const float WORLD_SIZE = 96f;
        private const float TERRAIN_HEIGHT = 20f;
        private const int RESOLUTION = 33;
        private const float GROUND_Y = 0.5f * TERRAIN_HEIGHT;

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

        #region Setup helpers

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        private T TrackAsset<T>(T asset) where T : Object
        {
            assets.Add(asset);
            return asset;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private void BuildNavigableWorld()
        {
            var data = new TerrainData();
            data.heightmapResolution = RESOLUTION;
            data.size = new Vector3(WORLD_SIZE, TERRAIN_HEIGHT, WORLD_SIZE);

            var heights = new float[RESOLUTION, RESOLUTION];
            for (int z = 0; z < RESOLUTION; z++)
            {
                for (int x = 0; x < RESOLUTION; x++)
                {
                    heights[z, x] = 0.5f;
                }
            }
            data.SetHeights(0, 0, heights);

            var terrainGo = Track(Terrain.CreateTerrainGameObject(data));
            terrainGo.transform.position = Vector3.zero;

            var surfaceGo = Track(new GameObject("Test_NavMeshSurface"));
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(WORLD_SIZE * 0.5f, TERRAIN_HEIGHT * 0.5f, WORLD_SIZE * 0.5f);
            surface.size = new Vector3(WORLD_SIZE + 10f, TERRAIN_HEIGHT + 10f, WORLD_SIZE + 10f);
            surface.BuildNavMesh();
        }

        private EnemyNavigationProfile CreateAcceleratedProfile()
        {
            var profile = TrackAsset(ScriptableObject.CreateInstance<EnemyNavigationProfile>());
            SetField(profile, "wanderRadius", 6f);
            SetField(profile, "idleDurationMin", 0.1f);
            SetField(profile, "idleDurationMax", 0.4f);
            SetField(profile, "arrivalDistance", 0.6f);
            SetField(profile, "immediateDetectionRange", 2.5f);
            SetField(profile, "loseSightDelay", 0.6f);
            SetField(profile, "suspicionDuration", 0.2f);
            SetField(profile, "perceptionInterval", 0.05f);
            SetField(profile, "maxChaseDistanceFromHome", 12f);
            SetField(profile, "leashHysteresis", 1f);
            SetField(profile, "returnCompletionDistance", 1f);
            SetField(profile, "reacquireCooldown", 0.5f);
            return profile;
        }

        private EnemyConfig CreateConfig(EnemyNavigationProfile profile)
        {
            var config = TrackAsset(ScriptableObject.CreateInstance<EnemyConfig>());
            SetField(config, "detectionRange", 12f);
            SetField(config, "fieldOfView", 120f);
            SetField(config, "moveSpeed", 8f);
            SetField(config, "turnSpeed", 720f);
            SetField(config, "attackRange", 0.5f);
            SetField(config, "navigationProfile", profile);
            return config;
        }

        private EnemyBrain CreateEnemy(Vector3 position, EnemyConfig config, Transform target)
        {
            var go = Track(new GameObject("Test_Enemy"));
            go.transform.position = position;

            var agent = go.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;

            var brain = go.AddComponent<EnemyBrain>();
            brain.SetConfig(config);
            brain.SetTarget(target);
            return brain;
        }

        private Transform CreateTargetDummy(Vector3 position)
        {
            var go = Track(new GameObject("Test_Target"));
            go.transform.position = position;
            return go.transform;
        }

        private static Vector3 Ground(float x, float z) => new Vector3(x, GROUND_Y, z);

        private static IEnumerator WaitForState(EnemyBrain brain, EnemyState state, float timeout)
        {
            float deadline = Time.time + timeout;
            while (Time.time < deadline && brain != null && brain.CurrentState != state)
            {
                yield return null;
            }
        }

        #endregion

        [UnityTest]
        public IEnumerator Wander_StaysInsideTerritory_AndActuallyMoves()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            var config = CreateConfig(profile);
            Vector3 home = Ground(48f, 48f);
            var target = CreateTargetDummy(Ground(5f, 5f)); // far, never seen
            var brain = CreateEnemy(home, config, target);

            float maxDistanceFromHome = 0f;
            float traveled = 0f;
            Vector3 previous = brain.transform.position;

            float deadline = Time.time + 4f;
            while (Time.time < deadline)
            {
                yield return null;
                maxDistanceFromHome = Mathf.Max(maxDistanceFromHome,
                    Vector3.Distance(brain.transform.position, home));
                traveled += Vector3.Distance(brain.transform.position, previous);
                previous = brain.transform.position;

                Assert.IsTrue(
                    brain.CurrentState == EnemyState.Idle || brain.CurrentState == EnemyState.Wandering,
                    $"Unexpected state while roaming with no target: {brain.CurrentState}");
            }

            Assert.IsTrue(brain.IsInitialized, "Brain must capture its home after placement");
            Assert.Greater(traveled, 0.5f, "The enemy must wander at least once");
            Assert.LessOrEqual(maxDistanceFromHome, 6f + 1.5f,
                "Wandering must stay inside WanderRadius around home (small agent margin allowed)");
        }

        [UnityTest]
        public IEnumerator VisibleTarget_TriggersSuspicious_ThenChasing()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            SetField(profile, "idleDurationMin", 10f); // no wandering during the test
            SetField(profile, "idleDurationMax", 12f);
            var config = CreateConfig(profile);

            Vector3 home = Ground(48f, 48f);
            var target = CreateTargetDummy(Ground(48f, 56f)); // 8m ahead
            var brain = CreateEnemy(home, config, target);
            brain.transform.rotation = Quaternion.LookRotation(Vector3.forward); // facing the target

            var transitions = new List<EnemyState>();
            brain.OnStateChanged += (_, next) => transitions.Add(next);

            yield return WaitForState(brain, EnemyState.Suspicious, 2f);
            Assert.AreEqual(EnemyState.Suspicious, brain.CurrentState, "Visible target must first raise suspicion");

            float suspicionStart = Time.time;
            yield return WaitForState(brain, EnemyState.Chasing, 2f);
            Assert.AreEqual(EnemyState.Chasing, brain.CurrentState, "Sustained visibility must confirm the chase");
            Assert.GreaterOrEqual(Time.time - suspicionStart, 0.15f,
                "The chase must not start before the suspicion duration elapsed");

            CollectionAssert.AreEqual(new[] { EnemyState.Suspicious, EnemyState.Chasing }, transitions,
                "Expected exactly Suspicious then Chasing");
        }

        [UnityTest]
        public IEnumerator TargetBehindWall_IsNotDetected()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            SetField(profile, "idleDurationMin", 10f);
            SetField(profile, "idleDurationMax", 12f);
            var config = CreateConfig(profile);

            Vector3 home = Ground(48f, 48f);
            var target = CreateTargetDummy(Ground(48f, 56f));
            var brain = CreateEnemy(home, config, target);
            brain.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            var wall = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = Ground(48f, 52f) + Vector3.up * 2f;
            wall.transform.localScale = new Vector3(8f, 6f, 0.4f);

            float deadline = Time.time + 1.5f;
            while (Time.time < deadline)
            {
                yield return null;
                Assert.AreNotEqual(EnemyState.Suspicious, brain.CurrentState, "A blocked line of sight must not detect");
                Assert.AreNotEqual(EnemyState.Chasing, brain.CurrentState, "A blocked line of sight must not chase");
            }
        }

        [UnityTest]
        public IEnumerator TargetBehindEnemy_InsideImmediateRange_IsDetected()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            SetField(profile, "idleDurationMin", 10f);
            SetField(profile, "idleDurationMax", 12f);
            var config = CreateConfig(profile);

            Vector3 home = Ground(48f, 48f);
            var target = CreateTargetDummy(Ground(48f, 46.5f)); // 1.5m BEHIND
            var brain = CreateEnemy(home, config, target);
            brain.transform.rotation = Quaternion.LookRotation(Vector3.forward); // looking away

            yield return WaitForState(brain, EnemyState.Suspicious, 2f);

            Assert.AreEqual(EnemyState.Suspicious, brain.CurrentState,
                "Inside ImmediateDetectionRange the field of view must not protect the player");
        }

        [UnityTest]
        public IEnumerator LeashExceeded_ReturnsHome_WithoutEmergencyWarp()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            SetField(profile, "idleDurationMin", 10f);
            SetField(profile, "idleDurationMax", 12f);
            var config = CreateConfig(profile);

            Vector3 home = Ground(30f, 48f);
            var target = CreateTargetDummy(Ground(30f, 54f));
            var brain = CreateEnemy(home, config, target);
            brain.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            yield return WaitForState(brain, EnemyState.Chasing, 3f);
            Assert.AreEqual(EnemyState.Chasing, brain.CurrentState, "Setup: the chase must start");

            // Player escapes far beyond MaxChaseDistanceFromHome (12m from home).
            target.position = Ground(80f, 48f);

            yield return WaitForState(brain, EnemyState.ReturningHome, 3f);
            Assert.AreEqual(EnemyState.ReturningHome, brain.CurrentState,
                "Crossing the territory leash must abandon the chase");
            Assert.AreEqual(EnemyTransitionReason.LeashExceeded, brain.LastTransitionReason);

            yield return WaitForState(brain, EnemyState.Idle, 8f);
            Assert.AreEqual(EnemyState.Idle, brain.CurrentState, "The enemy must resume its routine at home");
            Assert.LessOrEqual(brain.DistanceFromHome, 1f + 1f,
                "The enemy must be back near its home");
            Assert.AreEqual(0, brain.EmergencyWarpCount, "The normal return must never warp");
        }

        [UnityTest]
        public IEnumerator DestroyedEnemy_CausesNoErrorsAfterwards()
        {
            BuildNavigableWorld();
            var profile = CreateAcceleratedProfile();
            var config = CreateConfig(profile);
            var target = CreateTargetDummy(Ground(50f, 56f));
            var brain = CreateEnemy(Ground(48f, 48f), config, target);

            // Let it initialize and start living.
            yield return new WaitForSeconds(0.6f);

            Object.Destroy(brain.gameObject);

            // Any surviving callback/coroutine touching destroyed objects
            // would raise exceptions here and fail the test.
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }
        }
    }
}
