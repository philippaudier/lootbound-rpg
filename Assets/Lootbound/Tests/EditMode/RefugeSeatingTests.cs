using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for RefugeSeating: the refuge CARVES a natural basin
    /// (never a raised mesa), deeper reference is dug down, the cut limit is
    /// respected, and the terrain is untouched beyond the bowl. Runs on a
    /// synthetic context - no Unity Terrain, no scene.
    /// </summary>
    public class RefugeSeatingTests
    {
        private const int Resolution = 101;   // cellWorld = 1 m
        private const float WorldSize = 100f;
        private const float TerrainH = 100f;
        private const float Center = 50f;

        private static TerrainGenerationContext Flat(float worldHeight)
        {
            var ctx = new TerrainGenerationContext(1, Resolution, WorldSize, TerrainH, 1);
            var norm = new float[Resolution, Resolution];
            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                    norm[x, z] = Mathf.Clamp01(worldHeight / TerrainH);
            ctx.SetNormalizedHeightMap(norm);
            TerrainHeightGenerator.ComputeSlopeMap(ctx);
            return ctx;
        }

        private static void SetField(object obj, string name, object value)
        {
            var f = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {name} not found");
            f.SetValue(obj, value);
        }

        private static TerrainGenerationConfig Config(
            float foundation = 10f, float transition = 10f, float carveDepth = 15f,
            float maxCut = 60f, float maxFill = 2f, float residual = 0f)
        {
            var c = ScriptableObject.CreateInstance<TerrainGenerationConfig>();
            SetField(c, "refugeFoundationRadius", foundation);
            SetField(c, "refugeTransitionRadius", transition);
            SetField(c, "refugeCarveDepth", carveDepth);
            SetField(c, "refugeMaxCut", maxCut);
            SetField(c, "refugeMaxFill", maxFill);
            SetField(c, "refugeResidualRoughness", residual);
            return c;
        }

        private static float WorldAt(TerrainGenerationContext ctx, float wx, float wz)
            => ctx.SampleHeightAtWorld(wx, wz);

        [Test]
        public void Carve_DigsABasinBelowTheOriginal()
        {
            var ctx = Flat(50f);
            RefugeSeating.Carve(ctx, new TerrainContextSampler(ctx), new Vector3(Center, 0f, Center), Config(carveDepth: 15f));

            Assert.AreEqual(35f, WorldAt(ctx, Center, Center), 0.3f, "median 50 - carve 15 = 35");
        }

        [Test]
        public void Carve_DigsDeeperOnHigherGround()
        {
            var high = Flat(80f);
            RefugeSeating.Carve(high, new TerrainContextSampler(high), new Vector3(Center, 0f, Center), Config(carveDepth: 15f));
            // 80 -> 65: still carved down by the same depth (a hollow into the hill).
            Assert.AreEqual(65f, WorldAt(high, Center, Center), 0.3f);
            Assert.Less(WorldAt(high, Center, Center), 80f, "the refuge is a hollow, never a raised platform");
        }

        [Test]
        public void Carve_RespectsMaxCut()
        {
            var ctx = Flat(50f);
            // Ask to carve 100 m but cap the cut at 20 m -> floor at 30, not -50.
            RefugeSeating.Carve(ctx, new TerrainContextSampler(ctx), new Vector3(Center, 0f, Center), Config(carveDepth: 100f, maxCut: 20f));

            Assert.AreEqual(30f, WorldAt(ctx, Center, Center), 0.3f, "carve clamped to maxCut");
            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                    Assert.LessOrEqual(50f - ctx.NormalizedHeightMap[x, z] * TerrainH, 20f + 0.1f, $"cut exceeded at {x},{z}");
        }

        [Test]
        public void Carve_NeverRaisesAMesa()
        {
            var ctx = Flat(50f);
            RefugeSeating.Carve(ctx, new TerrainContextSampler(ctx), new Vector3(Center, 0f, Center), Config(carveDepth: 15f));

            // No cell may rise above the original terrain (it only digs down).
            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                    Assert.LessOrEqual(ctx.NormalizedHeightMap[x, z] * TerrainH, 50f + 0.1f, $"terrain rose above original at {x},{z}");
        }

        [Test]
        public void Carve_LeavesTerrainUntouchedBeyondTheBowl()
        {
            var ctx = Flat(50f);
            // foundation 10 + transition 10 = outer 20.
            RefugeSeating.Carve(ctx, new TerrainContextSampler(ctx), new Vector3(Center, 0f, Center), Config(foundation: 10f, transition: 10f));

            Assert.AreEqual(50f, WorldAt(ctx, Center + 25f, Center), 1e-4f, "well beyond the bowl the terrain is untouched");
        }
    }
}
