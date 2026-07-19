using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Population;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// Targeted PlayMode tests for the ambient population on a real terrain
    /// and a real runtime NavMesh: creatures spawn on the mesh with a valid
    /// home, respect the anti-pop and refuge rules, stream out and back as
    /// the SAME wounded presence, never return after death, and vanish
    /// entirely on world purge.
    /// </summary>
    public class AmbientPopulationPlayModeTests
    {
        private const float WORLD_SIZE = 128f;
        private const float TERRAIN_HEIGHT = 20f;
        private const int RESOLUTION = 33;
        private const float GROUND_Y = 0.5f * TERRAIN_HEIGHT;
        private const int SEED = 42;

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

        #region World setup

        private TerrainGenerationContext BuildWorld(out WorldProgression progression)
        {
            // Flat terrain + runtime NavMesh (production build path).
            var data = new TerrainData();
            data.heightmapResolution = RESOLUTION;
            data.size = new Vector3(WORLD_SIZE, TERRAIN_HEIGHT, WORLD_SIZE);
            var heights = new float[RESOLUTION, RESOLUTION];
            for (int z = 0; z < RESOLUTION; z++)
                for (int x = 0; x < RESOLUTION; x++)
                    heights[z, x] = 0.5f;
            data.SetHeights(0, 0, heights);

            var terrainGo = Track(Terrain.CreateTerrainGameObject(data));
            terrainGo.transform.position = Vector3.zero;

            var surfaceGo = Track(new GameObject("Test_NavMeshSurface"));
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(WORLD_SIZE * 0.5f, TERRAIN_HEIGHT * 0.5f, WORLD_SIZE * 0.5f);
            surface.size = new Vector3(WORLD_SIZE + 10f, TERRAIN_HEIGHT + 10f, WORLD_SIZE + 10f);
            surface.BuildNavMesh();

            // Generation context with a matching flat height space, plus a
            // published layout carrying the progression authority.
            var context = new TerrainGenerationContext(SEED, RESOLUTION, WORLD_SIZE, TERRAIN_HEIGHT, generationId: 1);
            var normalized = new float[RESOLUTION, RESOLUTION];
            for (int x = 0; x < RESOLUTION; x++)
                for (int z = 0; z < RESOLUTION; z++)
                    normalized[x, z] = 0.5f;
            context.SetHeightMap(normalized);
            context.SetNormalizedHeightMap((float[,])normalized.Clone());
            context.SetSlopeMap(new float[RESOLUTION, RESOLUTION]);

            Vector3 refuge = new Vector3(WORLD_SIZE * 0.5f, GROUND_Y, WORLD_SIZE * 0.5f);
            var ringConfig = WorldRingConfig.CreateDefault();
            var layout = new WorldLayoutContext(SEED, 0, SEED, WORLD_SIZE * 0.5f, refuge, ringConfig);
            progression = new WorldProgression(refuge, WORLD_SIZE * 0.5f, ringConfig);
            layout.AttachProgression(progression);
            context.LayoutContext = layout;

            return context;
        }

        private GameObject CreateCreatureTemplate(bool withBrain)
        {
            var template = Track(new GameObject("Test_AmbientCreatureTemplate"));
            template.transform.position = new Vector3(-500f, 0f, -500f); // far out of the way

            var agent = template.AddComponent<NavMeshAgent>();
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.enabled = false; // template only; instances re-enable via Warp

            var enemyConfig = TrackAsset(ScriptableObject.CreateInstance<EnemyConfig>());
            SetField(enemyConfig, "maxHealth", 50f);
            SetField(enemyConfig, "maxPoise", 1000f);
            SetField(enemyConfig, "detectionRange", 8f);
            SetField(enemyConfig, "moveSpeed", 3f);

            var health = template.AddComponent<EnemyHealth>();
            health.SetConfig(enemyConfig);

            if (withBrain)
            {
                var navProfile = TrackAsset(ScriptableObject.CreateInstance<EnemyNavigationProfile>());
                SetField(navProfile, "idleDurationMin", 0.2f);
                SetField(navProfile, "idleDurationMax", 0.5f);
                SetField(enemyConfig, "navigationProfile", navProfile);

                var brain = template.AddComponent<EnemyBrain>();
                SetField(brain, "config", enemyConfig);
            }

            return template;
        }

        private AmbientPopulationConfig CreateTestConfig(GameObject prefab, float minPlayerDistance)
        {
            var definition = TrackAsset(ScriptableObject.CreateInstance<AmbientPopulationDefinition>());
            SetField(definition, "populationId", "test_walker");
            SetField(definition, "prefab", prefab);
            SetField(definition, "minimumRing", WorldRing.Nearlands);
            SetField(definition, "maximumRing", WorldRing.Edgelands);
            SetField(definition, "maxAliveGlobally", 8);
            SetField(definition, "maxAlivePerCell", 3);
            SetField(definition, "minimumDistanceFromAuthoredContent", 0f);

            var config = TrackAsset(ScriptableObject.CreateInstance<AmbientPopulationConfig>());
            SetField(config, "definitions", new List<AmbientPopulationDefinition> { definition });
            SetField(config, "cellSize", 24f);
            SetField(config, "maxPlansPerCell", 2);
            SetField(config, "candidatesPerAnchor", 6);
            SetField(config, "evaluationInterval", 0.1f);
            SetField(config, "maxCellActivationsPerTick", 10);
            SetField(config, "spawnAttemptsPerTick", 10);
            SetField(config, "spawnRadiusMin", 5f);
            SetField(config, "spawnRadiusMax", 60f);
            SetField(config, "despawnRadius", 80f);
            SetField(config, "minimumDistanceFromPlayer", minPlayerDistance);
            SetField(config, "minimumDistanceBetweenIndividuals", 3f);
            SetField(config, "minimumDistanceFromRefuge", 4f);
            SetField(config, "globalPopulationBudget", 10);
            SetField(config, "despawnGraceDuration", 0.4f);
            SetField(config, "rejectInsideCameraFrustum", false); // no camera in tests
            return config;
        }

        private AmbientPopulationController CreateController(
            AmbientPopulationConfig config, Transform player)
        {
            var go = Track(new GameObject("Test_AmbientController"));
            var controller = go.AddComponent<AmbientPopulationController>();
            SetField(controller, "config", config);
            SetField(controller, "player", player);
            return controller;
        }

        private Transform CreatePlayer(Vector3 position)
        {
            var go = Track(new GameObject("Test_Player"));
            go.transform.position = position;
            return go.transform;
        }

        private static Vector3 Ground(float x, float z) => new Vector3(x, GROUND_Y, z);

        private static IEnumerator WaitUntil(System.Func<bool> condition, float timeout)
        {
            float deadline = Time.time + timeout;
            while (Time.time < deadline && !condition())
            {
                yield return null;
            }
        }

        #endregion

        [UnityTest]
        public IEnumerator Population_SpawnsOnNavMesh_WithValidHome()
        {
            var context = BuildWorld(out var progression);
            var template = CreateCreatureTemplate(withBrain: true);
            var config = CreateTestConfig(template, minPlayerDistance: 0f);
            var player = CreatePlayer(Ground(64f, 64f));
            var controller = CreateController(config, player);

            controller.BeginPopulation(context);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);

            Assert.Greater(controller.Registry.TotalAlive, 0, "Ambient creatures must exist outside any reservation");

            // Let brains initialize and capture their homes.
            yield return new WaitForSeconds(0.4f);

            foreach (var instance in controller.Registry.AliveInstances)
            {
                var ambient = (AmbientPopulationInstance)instance;
                var agent = ambient.GameObject.GetComponent<NavMeshAgent>();
                Assert.IsTrue(agent.isOnNavMesh, $"{ambient.MemberId} must stand on the runtime NavMesh");

                var brain = ambient.Brain;
                Assert.IsNotNull(brain);
                Assert.IsTrue(brain.IsInitialized, "HomePosition must be captured after final placement");
                Assert.Less(Vector3.Distance(brain.HomePosition, ambient.Position), 3f,
                    "Home must match the resolved spawn position");

                var ringContext = progression.GetContext(ambient.Position);
                Assert.AreNotEqual(WorldRing.Refuge, ringContext.Ring, "No ambient creature inside the Refuge ring");
            }
        }

        [UnityTest]
        public IEnumerator Population_NeverSpawnsCloseToThePlayer()
        {
            var context = BuildWorld(out _);
            var template = CreateCreatureTemplate(withBrain: false);
            var config = CreateTestConfig(template, minPlayerDistance: 25f);
            var player = CreatePlayer(Ground(64f, 64f));
            var controller = CreateController(config, player);

            controller.BeginPopulation(context);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);

            foreach (var instance in controller.Registry.AliveInstances)
            {
                Assert.GreaterOrEqual(Vector3.Distance(instance.Position, player.position), 25f - 0.5f,
                    "The anti-pop distance is an absolute rule");
            }
        }

        [UnityTest]
        public IEnumerator StreamedOutCreature_ReturnsAsTheSameWoundedPresence()
        {
            var context = BuildWorld(out _);
            var template = CreateCreatureTemplate(withBrain: false); // health-only: no defensive chase blocking despawn
            var config = CreateTestConfig(template, minPlayerDistance: 0f);
            var player = CreatePlayer(Ground(64f, 64f));
            var controller = CreateController(config, player);

            controller.BeginPopulation(context);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);
            Assert.Greater(controller.Registry.TotalAlive, 0);

            // Wound one creature.
            AmbientPopulationInstance wounded = null;
            foreach (var instance in controller.Registry.AliveInstances)
            {
                wounded = (AmbientPopulationInstance)instance;
                break;
            }
            string woundedId = wounded.MemberId;
            wounded.Health.TakeDamage(new DamageRequest(player.gameObject, 20f, wounded.Position, Vector3.forward, 0f));
            float healthAfterHit = wounded.Health.NormalizedHealth;
            Assert.Less(healthAfterHit, 1f);

            // Walk far away: everything streams out (plans survive).
            player.position = Ground(64f, 64f) + new Vector3(500f, 0f, 500f);
            yield return WaitUntil(() => controller.Registry.TotalAlive == 0, 6f);
            Assert.AreEqual(0, controller.Registry.TotalAlive, "Far, unengaged creatures must stream out");

            // Come back: the SAME presence returns, still wounded.
            player.position = Ground(64f, 64f);
            yield return WaitUntil(() => controller.Registry.IsMemberAlive(woundedId), 6f);
            Assert.IsTrue(controller.Registry.IsMemberAlive(woundedId), "The same identity must return (no re-roll)");

            int countWithId = 0;
            AmbientPopulationInstance returned = null;
            foreach (var instance in controller.Registry.AliveInstances)
            {
                if (instance.MemberId == woundedId)
                {
                    countWithId++;
                    returned = (AmbientPopulationInstance)instance;
                }
            }
            Assert.AreEqual(1, countWithId, "Leaving and returning must never duplicate a presence");
            Assert.AreEqual(healthAfterHit, returned.Health.NormalizedHealth, 0.01f,
                "Streaming must never heal a wounded creature");
        }

        [UnityTest]
        public IEnumerator KilledCreature_NeverReturnsThisSession()
        {
            var context = BuildWorld(out _);
            var template = CreateCreatureTemplate(withBrain: false);
            var config = CreateTestConfig(template, minPlayerDistance: 0f);
            var player = CreatePlayer(Ground(64f, 64f));
            var controller = CreateController(config, player);

            controller.BeginPopulation(context);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);

            AmbientPopulationInstance victim = null;
            foreach (var instance in controller.Registry.AliveInstances)
            {
                victim = (AmbientPopulationInstance)instance;
                break;
            }
            string victimId = victim.MemberId;
            int aliveBefore = controller.Registry.TotalAlive;

            victim.Health.TakeDamage(new DamageRequest(player.gameObject, 9999f, victim.Position, Vector3.forward, 0f));
            yield return null;

            Assert.AreEqual(aliveBefore - 1, controller.Registry.TotalAlive, "Death must unregister the instance");
            Assert.AreEqual(1, controller.Registry.TotalDeaths);

            // Leave and return: the defeated presence must not reappear.
            player.position = Ground(64f, 64f) + new Vector3(500f, 0f, 500f);
            yield return WaitUntil(() => controller.Registry.TotalAlive == 0, 6f);
            player.position = Ground(64f, 64f);
            yield return new WaitForSeconds(1.5f);

            Assert.IsFalse(controller.Registry.IsMemberAlive(victimId),
                "A defeated presence never returns during the session");
        }

        [UnityTest]
        public IEnumerator Purge_RemovesEveryInstanceAndAllMemory()
        {
            var context = BuildWorld(out _);
            var template = CreateCreatureTemplate(withBrain: false);
            var config = CreateTestConfig(template, minPlayerDistance: 0f);
            var player = CreatePlayer(Ground(64f, 64f));
            var controller = CreateController(config, player);

            controller.BeginPopulation(context);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);
            Assert.Greater(controller.Registry.TotalAlive, 0);

            controller.PurgeAll();
            yield return null;

            Assert.AreEqual(0, controller.Registry.TotalAlive);
            Assert.AreEqual(0, controller.Registry.PlannedCellCount, "No plan survives a world regeneration");
            Assert.IsNull(GameObject.Find("AmbientPopulation_Spawned"), "The spawn root must be destroyed");

            // A fresh generation repopulates cleanly.
            var newContext = BuildWorld(out _);
            controller.BeginPopulation(newContext);
            yield return WaitUntil(() => controller.Registry.TotalAlive > 0, 4f);
            Assert.Greater(controller.Registry.TotalAlive, 0, "A new world must repopulate from scratch");
        }
    }
}
