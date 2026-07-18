using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Navigation;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// Targeted PlayMode tests exercising the REAL NavMesh APIs: a runtime
    /// build produces a navigable surface matching the actual terrain, a
    /// rebuild fully replaces the previous data, and invalid positions are
    /// rejected in a controlled way. Small terrains keep builds fast.
    /// </summary>
    public class RuntimeNavigationPlayModeTests
    {
        private const float WORLD_SIZE = 96f;
        private const float TERRAIN_HEIGHT = 30f;
        private const int RESOLUTION = 33;

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null) Object.Destroy(go);
            }
            spawned.Clear();
        }

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        private Terrain CreateFlatTerrain(float normalizedHeight)
        {
            var data = new TerrainData();
            data.heightmapResolution = RESOLUTION;
            data.size = new Vector3(WORLD_SIZE, TERRAIN_HEIGHT, WORLD_SIZE);

            var heights = new float[RESOLUTION, RESOLUTION];
            for (int z = 0; z < RESOLUTION; z++)
            {
                for (int x = 0; x < RESOLUTION; x++)
                {
                    heights[z, x] = normalizedHeight;
                }
            }
            data.SetHeights(0, 0, heights);

            var terrainGo = Track(Terrain.CreateTerrainGameObject(data));
            terrainGo.transform.position = Vector3.zero;
            return terrainGo.GetComponent<Terrain>();
        }

        private RuntimeNavigationBuilder CreateBuilder(Terrain terrain, out NavMeshSurface surface)
        {
            var surfaceGo = Track(new GameObject("Test_NavMeshSurface"));
            surface = surfaceGo.AddComponent<NavMeshSurface>();

            var builderGo = Track(new GameObject("Test_RuntimeNavigationBuilder"));
            var builder = builderGo.AddComponent<RuntimeNavigationBuilder>();
            SetField(builder, "terrain", terrain);
            SetField(builder, "surface", surface);
            return builder;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field '{fieldName}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static TerrainGenerationContext CreateContext(int seed, int generationId)
        {
            return new TerrainGenerationContext(seed, RESOLUTION, WORLD_SIZE, TERRAIN_HEIGHT, generationId);
        }

        [Test]
        public void Build_OnFlatTerrain_ProducesNavigableSurface()
        {
            var terrain = CreateFlatTerrain(0.5f);
            var builder = CreateBuilder(terrain, out _);

            var result = builder.Rebuild(CreateContext(seed: 1, generationId: 1));

            Assert.IsTrue(result.Success, $"Build should succeed: {result.FailureReason}");
            Assert.AreEqual(RuntimeNavigationState.Ready, builder.State);
            Assert.AreEqual(1, result.GenerationId);
            Assert.Greater(result.TriangleCount, 0);
            Assert.AreEqual(1, builder.Stats.BuildCount);

            // A point at the terrain surface must resolve to the NavMesh.
            Vector3 center = new Vector3(WORLD_SIZE * 0.5f, 0.5f * TERRAIN_HEIGHT, WORLD_SIZE * 0.5f);
            Assert.IsTrue(NavMesh.SamplePosition(center, out NavMeshHit hit, 2f, NavMesh.AllAreas),
                "Terrain surface should be navigable after the build");
            Assert.AreEqual(center.y, hit.position.y, 1f,
                "NavMesh should sit on the actual terrain surface");
        }

        [Test]
        public void Rebuild_ReplacesPreviousNavMesh()
        {
            var terrain = CreateFlatTerrain(0.2f);
            var builder = CreateBuilder(terrain, out _);

            var first = builder.Rebuild(CreateContext(seed: 1, generationId: 1));
            Assert.IsTrue(first.Success, $"First build should succeed: {first.FailureReason}");

            float lowY = 0.2f * TERRAIN_HEIGHT;
            float highY = 0.8f * TERRAIN_HEIGHT;
            Vector3 centerXZ = new Vector3(WORLD_SIZE * 0.5f, 0f, WORLD_SIZE * 0.5f);

            // Regenerate the terrain at a different height (like a new seed would)
            var data = terrain.terrainData;
            var heights = new float[RESOLUTION, RESOLUTION];
            for (int z = 0; z < RESOLUTION; z++)
            {
                for (int x = 0; x < RESOLUTION; x++)
                {
                    heights[z, x] = 0.8f;
                }
            }
            data.SetHeights(0, 0, heights);

            var second = builder.Rebuild(CreateContext(seed: 1, generationId: 2));
            Assert.IsTrue(second.Success, $"Second build should succeed: {second.FailureReason}");
            Assert.AreEqual(2, second.GenerationId);
            Assert.AreEqual(2, builder.Stats.BuildCount);

            // New surface responds at the new height...
            Assert.IsTrue(NavMesh.SamplePosition(centerXZ + Vector3.up * highY, out NavMeshHit newHit, 2f, NavMesh.AllAreas),
                "The rebuilt NavMesh should cover the new terrain height");
            Assert.AreEqual(highY, newHit.position.y, 1f);

            // ...and nothing remains at the old height: the old NavMesh is gone.
            Assert.IsFalse(NavMesh.SamplePosition(centerXZ + Vector3.up * lowY, out _, 2f, NavMesh.AllAreas),
                "No NavMesh from the previous generation may remain at the old terrain height");
        }

        [Test]
        public void SamplePosition_FarOutsideWorld_IsRejected()
        {
            var terrain = CreateFlatTerrain(0.5f);
            var builder = CreateBuilder(terrain, out _);

            var result = builder.Rebuild(CreateContext(seed: 1, generationId: 1));
            Assert.IsTrue(result.Success, $"Build should succeed: {result.FailureReason}");

            Vector3 farOutside = new Vector3(WORLD_SIZE * 10f, 0f, WORLD_SIZE * 10f);
            Assert.IsFalse(NavMesh.SamplePosition(farOutside, out _, 4f, NavMesh.AllAreas),
                "A position far outside the world must produce a controlled rejection");
        }

        [Test]
        public void Build_WithoutSurface_FailsExplicitly()
        {
            var terrain = CreateFlatTerrain(0.5f);
            var builderGo = Track(new GameObject("Test_BuilderWithoutSurface"));
            var builder = builderGo.AddComponent<RuntimeNavigationBuilder>();
            SetField(builder, "terrain", terrain);

            var result = builder.Rebuild(CreateContext(seed: 1, generationId: 1));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeNavigationState.Failed, builder.State);
            StringAssert.Contains("NavMeshSurface", result.FailureReason);
        }

        [Test]
        public void Build_WithoutTerrain_FailsExplicitly()
        {
            var surfaceGo = Track(new GameObject("Test_Surface"));
            var surface = surfaceGo.AddComponent<NavMeshSurface>();
            var builderGo = Track(new GameObject("Test_BuilderWithoutTerrain"));
            var builder = builderGo.AddComponent<RuntimeNavigationBuilder>();
            SetField(builder, "surface", surface);

            var result = builder.Rebuild(CreateContext(seed: 1, generationId: 1));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(RuntimeNavigationState.Failed, builder.State);
            StringAssert.Contains("Terrain", result.FailureReason);
        }

        [Test]
        public void BuildBounds_DeriveFromTerrainData_NotFromAPresetConstant()
        {
            var terrain = CreateFlatTerrain(0.5f);
            var builder = CreateBuilder(terrain, out _);

            var result = builder.Rebuild(CreateContext(seed: 1, generationId: 1));

            Assert.IsTrue(result.Success, $"Build should succeed: {result.FailureReason}");
            Assert.GreaterOrEqual(result.BoundsUsed.size.x, WORLD_SIZE,
                "Bounds must cover the whole physical terrain");
            Assert.Less(result.BoundsUsed.size.x, WORLD_SIZE * 2f,
                "Bounds must scale with the actual TerrainData, not a fixed preset size");
        }
    }
}
