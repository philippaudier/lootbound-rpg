using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Tests.PlayMode
{
    /// <summary>
    /// PlayMode integration for landmark terrain seating against a REAL Unity
    /// Terrain: the full solver -> planner -> applier -> finalize pipeline runs
    /// on a synthetic context, the stamped heightmap is applied to an actual
    /// TerrainData, and the terrain is queried through Terrain.SampleHeight.
    /// Proves the seat reaches the real mesh, nothing protrudes above it, and
    /// the landmark grounds on it without floating or sinking.
    /// </summary>
    public class LandmarkTerrainPlayModeTests
    {
        private const int Resolution = 129;   // 2^7 + 1, valid Unity heightmap
        private const float WorldSize = 128f;
        private const float TerrainH = 100f;
        private const float CenterXZ = 64f;

        private readonly List<Object> spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in spawned)
            {
                if (o != null) Object.Destroy(o);
            }
            spawned.Clear();
        }

        private static void SetField(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {field} not found on {obj.GetType().Name}");
            f.SetValue(obj, value);
        }

        private static TerrainGenerationContext BuildRippledContext()
        {
            var ctx = new TerrainGenerationContext(7, Resolution, WorldSize, TerrainH, 1);
            var norm = new float[Resolution, Resolution];
            float cell = WorldSize / (Resolution - 1);
            for (int x = 0; x < Resolution; x++)
            {
                for (int z = 0; z < Resolution; z++)
                {
                    float wx = x * cell, wz = z * cell;
                    // ~40 m base with +/-5 m ripples the seat should flatten.
                    norm[x, z] = Mathf.Clamp01(0.4f + 0.05f * Mathf.Sin(wx * 0.5f) + 0.05f * Mathf.Sin(wz * 0.5f));
                }
            }
            ctx.SetNormalizedHeightMap(norm);
            TerrainHeightGenerator.ComputeSlopeMap(ctx);
            return ctx;
        }

        private LandmarkDefinition CreateDefinition()
        {
            var def = ScriptableObject.CreateInstance<LandmarkDefinition>();
            spawned.Add(def);
            SetField(def, "landmarkId", "def_tower");
            SetField(def, "conformingMode", LandmarkTerrainConformingMode.SoftFoundation);
            SetField(def, "foundationShape", FoundationShape.Circle);
            SetField(def, "foundationRadius", 8f);
            SetField(def, "transitionRadius", 10f);
            // Generous limits so the foundation flattens fully regardless of
            // where the median seat lands (cut/fill clamping is covered in
            // EditMode); this keeps the "no relief above the seat" check exact.
            SetField(def, "maxCutDepth", 20f);
            SetField(def, "maxFillHeight", 20f);
            SetField(def, "residualRoughness", 0f);
            SetField(def, "verticalOffset", 0f);
            SetField(def, "foundationPriority", 0);
            return def;
        }

        /// <summary>Runs the full pipeline and materializes the stamped context as a real Terrain.</summary>
        private Terrain BuildStampedTerrain(out float seatHeight, out LandmarkIdentity landmark)
        {
            var ctx = BuildRippledContext();
            var sampler = new TerrainContextSampler(ctx);
            var def = CreateDefinition();
            var placements = new[]
            {
                new LandmarkPlacement("landmark_tower", def, CenterXZ, CenterXZ,
                    WorldRing.Wildlands, 0.5f, 0.5f, "path", "host", 0)
            };

            var stamps = LandmarkTerrainStampPlanner.Plan(placements, sampler);
            Assert.AreEqual(1, stamps.Count, "the conforming landmark must produce one seat");
            seatHeight = stamps[0].SeatHeight;

            LandmarkTerrainStampApplier.Apply(ctx, stamps);
            landmark = LandmarkPlanner.Finalize(placements, sampler)[0];

            var data = new TerrainData
            {
                heightmapResolution = Resolution,
                size = new Vector3(WorldSize, TerrainH, WorldSize)
            };
            data.SetHeights(0, 0, ctx.GetTerrainHeightmapData());
            spawned.Add(data);

            var go = Terrain.CreateTerrainGameObject(data);
            spawned.Add(go);
            return go.GetComponent<Terrain>();
        }

        [UnityTest]
        public IEnumerator StampedContext_MatchesRealTerrain()
        {
            var terrain = BuildStampedTerrain(out float seat, out _);
            yield return null;

            float real = terrain.SampleHeight(new Vector3(CenterXZ, 0f, CenterXZ));
            Assert.AreEqual(seat, real, 1.0f, "the Unity Terrain reflects the seated heightmap at the centre");
        }

        [UnityTest]
        public IEnumerator Foundation_HasNoReliefAboveTheSeat()
        {
            var terrain = BuildStampedTerrain(out float seat, out _);
            yield return null;

            // Sample a grid well inside the foundation (radius 8): the flattened
            // seat must leave nothing rising above the building base.
            for (float dx = -5f; dx <= 5f; dx += 2.5f)
            {
                for (float dz = -5f; dz <= 5f; dz += 2.5f)
                {
                    float h = terrain.SampleHeight(new Vector3(CenterXZ + dx, 0f, CenterXZ + dz));
                    Assert.LessOrEqual(h, seat + 1.0f, $"terrain protrudes above the seat at ({dx},{dz})");
                }
            }
        }

        [UnityTest]
        public IEnumerator Landmark_GroundsOnRealTerrain_NoFloatNoSink()
        {
            var terrain = BuildStampedTerrain(out _, out LandmarkIdentity landmark);
            yield return null;

            float real = terrain.SampleHeight(new Vector3(landmark.Position.x, 0f, landmark.Position.z));
            Assert.AreEqual(real, landmark.Position.y, 1.0f,
                "the landmark sits on the real stamped terrain - neither floating nor sunk");
        }
    }
}
