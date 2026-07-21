using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode coverage for the landmark terrain-integration pipeline: the
    /// seat solver (robust reference), the stamp planner, and the applier
    /// (cut/fill limits, transition continuity, residual relief, deterministic
    /// order-independent overlap), plus the grounding contract (no float/sink).
    /// Everything runs on synthetic contexts - no Unity Terrain, no scene.
    /// </summary>
    public class LandmarkTerrainStampTests
    {
        private const int Resolution = 101;   // cellWorld = 1 m
        private const float WorldSize = 100f;
        private const float TerrainH = 100f;  // normalized * 100 = world metres

        #region Rig

        private static TerrainGenerationContext BuildContext(System.Func<float, float, float> normAtWorld)
        {
            var ctx = new TerrainGenerationContext(1, Resolution, WorldSize, TerrainH, 1);
            var norm = new float[Resolution, Resolution];
            float cell = WorldSize / (Resolution - 1);
            for (int x = 0; x < Resolution; x++)
            {
                for (int z = 0; z < Resolution; z++)
                {
                    norm[x, z] = Mathf.Clamp01(normAtWorld(x * cell, z * cell));
                }
            }
            ctx.SetNormalizedHeightMap(norm);
            TerrainHeightGenerator.ComputeSlopeMap(ctx);
            return ctx;
        }

        private static TerrainGenerationContext Flat(float worldHeight) =>
            BuildContext((_, __) => worldHeight / TerrainH);

        private static LandmarkTerrainStamp MakeStamp(
            string id, float cx, float cz, float seatWorld,
            float foundation = 6f, float transition = 8f,
            float maxCut = 8f, float maxFill = 8f, float residual = 0f,
            int priority = 0, FoundationShape shape = FoundationShape.Circle,
            LandmarkTerrainConformingMode mode = LandmarkTerrainConformingMode.SoftFoundation)
        {
            return new LandmarkTerrainStamp(id, shape, mode, cx, cz, seatWorld,
                foundation, transition, maxCut, maxFill, residual, priority);
        }

        private static float WorldAt(TerrainGenerationContext ctx, float wx, float wz) =>
            ctx.SampleHeightAtWorld(wx, wz);

        private static void SetField(object obj, string field, object value)
        {
            var f = obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field {field} not found on {obj.GetType().Name}");
            f.SetValue(obj, value);
        }

        private static LandmarkDefinition CreateDefinition(
            LandmarkTerrainConformingMode mode, float foundation = 6f, float transition = 8f,
            float maxCut = 8f, float maxFill = 8f, float residual = 0f, float vOffset = 0f,
            int priority = 0, FoundationShape shape = FoundationShape.Circle)
        {
            var def = ScriptableObject.CreateInstance<LandmarkDefinition>();
            SetField(def, "landmarkId", "def_test");
            SetField(def, "conformingMode", mode);
            SetField(def, "foundationShape", shape);
            SetField(def, "foundationRadius", foundation);
            SetField(def, "transitionRadius", transition);
            SetField(def, "maxCutDepth", maxCut);
            SetField(def, "maxFillHeight", maxFill);
            SetField(def, "residualRoughness", residual);
            SetField(def, "verticalOffset", vOffset);
            SetField(def, "foundationPriority", priority);
            return def;
        }

        private static LandmarkPlacement PlacementAt(LandmarkDefinition def, string id, float x, float z) =>
            new LandmarkPlacement(id, def, x, z, WorldRing.Wildlands, 0.5f, 0.5f, "path", "host", 0);

        #endregion

        [Test]
        public void Applier_IsDeterministic()
        {
            var stamp = MakeStamp("a", 50f, 50f, 60f);
            var c1 = Flat(50f); var c2 = Flat(50f);
            LandmarkTerrainStampApplier.Apply(c1, new[] { stamp });
            LandmarkTerrainStampApplier.Apply(c2, new[] { stamp });

            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                    Assert.AreEqual(c1.NormalizedHeightMap[x, z], c2.NormalizedHeightMap[x, z], 1e-6f);
        }

        [Test]
        public void Applier_LeavesTerrainUntouchedBeyondOuterRadius()
        {
            var stamp = MakeStamp("a", 50f, 50f, 70f, foundation: 6f, transition: 8f); // outer = 14
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            // A cell 20 m away is well beyond the 14 m outer radius.
            Assert.AreEqual(0.5f, WorldAt(ctx, 30f, 50f) / TerrainH, 1e-5f);
            Assert.AreEqual(0.5f, WorldAt(ctx, 50f, 80f) / TerrainH, 1e-5f);
        }

        [Test]
        public void Applier_RespectsFillLimit()
        {
            // Seat 20 m above ground, fill capped at 8 -> centre rises by 8 only.
            var stamp = MakeStamp("a", 50f, 50f, 70f, maxFill: 8f, residual: 0f);
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            Assert.AreEqual(58f, WorldAt(ctx, 50f, 50f), 0.2f);
            // No cell anywhere may exceed the fill limit above the original 50.
            AssertNoCellExceeds(ctx, original: 50f, maxUp: 8f, maxDown: 0f);
        }

        [Test]
        public void Applier_RespectsCutLimit()
        {
            var stamp = MakeStamp("a", 50f, 50f, 20f, maxCut: 8f, residual: 0f);
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            Assert.AreEqual(42f, WorldAt(ctx, 50f, 50f), 0.2f);
            AssertNoCellExceeds(ctx, original: 50f, maxUp: 0f, maxDown: 8f);
        }

        [Test]
        public void Applier_TransitionHasNoCliff()
        {
            var stamp = MakeStamp("a", 50f, 50f, 58f, foundation: 6f, transition: 8f, maxFill: 8f, residual: 0f);
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            // Max smoothstep gradient is 1.5; per-cell jump bound (+ margin).
            float deltaWorld = 8f;
            float bound = deltaWorld * 1.5f * (1f / 8f) * 1.5f; // ~2.25 m
            float maxJump = MaxAdjacentJump(ctx);
            Assert.Less(maxJump, bound, "the seat must feather, never form a step");
        }

        [Test]
        public void Applier_ResidualKeepsMostReliefForNaturalIntegration()
        {
            // Sloped terrain; high residual should keep the ground close to original.
            var ctx = BuildContext((wx, _) => 0.3f + 0.4f * (wx / WorldSize));
            float before = WorldAt(ctx, 50f, 50f);
            var stamp = MakeStamp("a", 50f, 50f, before + 6f, maxFill: 8f, residual: 0.9f,
                mode: LandmarkTerrainConformingMode.NaturalIntegration);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            float after = WorldAt(ctx, 50f, 50f);
            Assert.Less(Mathf.Abs(after - before), 1.0f, "residual 0.9 keeps most of the original relief");
        }

        [Test]
        public void Applier_OverlapIsOrderIndependent()
        {
            var a = MakeStamp("a", 48f, 50f, 60f, priority: 5);
            var b = MakeStamp("b", 52f, 50f, 40f, priority: 1);

            var c1 = Flat(50f); var c2 = Flat(50f);
            LandmarkTerrainStampApplier.Apply(c1, new[] { a, b });
            LandmarkTerrainStampApplier.Apply(c2, new[] { b, a });

            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                    Assert.AreEqual(c1.NormalizedHeightMap[x, z], c2.NormalizedHeightMap[x, z], 1e-6f);
        }

        [Test]
        public void Applier_HigherPriorityWinsSharedCell()
        {
            // Both fully cover the midpoint; higher priority (a, seat 60) must win.
            var a = MakeStamp("a", 49f, 50f, 60f, foundation: 6f, priority: 10);
            var b = MakeStamp("b", 51f, 50f, 40f, foundation: 6f, priority: 1);
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { a, b });

            Assert.Greater(WorldAt(ctx, 50f, 50f), 54f, "priority 10 seat (raise) dominates the overlap");
        }

        [Test]
        public void Applier_RelativeFloorPreventsGrazingHighPriorityOverride()
        {
            // 'far' has huge priority but only grazes near's footprint; 'near'
            // strongly covers it. The near seat (raise) must win the whole
            // overlap - no seam where far's weak tail would otherwise cut in.
            var near = MakeStamp("near", 50f, 50f, 62f, foundation: 6f, transition: 8f, priority: 0);
            var far = MakeStamp("far", 36f, 50f, 20f, foundation: 6f, transition: 8f, priority: 999);
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { near, far });

            // Where far is only a relatively negligible tail (< half near's
            // influence), it cannot override near's seat.
            Assert.Greater(WorldAt(ctx, 50f, 50f), 55f, "near strongly covers the centre and must win");
            Assert.Greater(WorldAt(ctx, 49f, 50f), 55f, "grazing high-priority tail cannot cut here");
            Assert.Greater(WorldAt(ctx, 48f, 50f), 55f, "grazing high-priority tail cannot cut here");
        }

        [Test]
        public void Applier_UniformPlateauIsNumericallyStable()
        {
            var stamp = MakeStamp("a", 50f, 50f, 50f, residual: 0f); // seat == ground
            var ctx = Flat(50f);
            LandmarkTerrainStampApplier.Apply(ctx, new[] { stamp });

            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                {
                    float h = ctx.NormalizedHeightMap[x, z];
                    Assert.IsFalse(float.IsNaN(h) || float.IsInfinity(h));
                    Assert.AreEqual(0.5f, h, 1e-5f);
                }
        }

        [Test]
        public void Planner_NoneModeProducesNoStamp()
        {
            var def = CreateDefinition(LandmarkTerrainConformingMode.None);
            var ctx = Flat(50f);
            var sampler = new TerrainContextSampler(ctx);
            var placements = new[] { PlacementAt(def, "a", 50f, 50f) };

            var stamps = LandmarkTerrainStampPlanner.Plan(placements, sampler);
            Assert.AreEqual(0, stamps.Count);
        }

        [Test]
        public void Planner_OrdersStampsByLandmarkId()
        {
            var def = CreateDefinition(LandmarkTerrainConformingMode.SoftFoundation);
            var ctx = Flat(50f);
            var sampler = new TerrainContextSampler(ctx);
            var placements = new[]
            {
                PlacementAt(def, "landmark_z", 30f, 30f),
                PlacementAt(def, "landmark_a", 60f, 60f),
            };

            var stamps = LandmarkTerrainStampPlanner.Plan(placements, sampler);
            Assert.AreEqual(2, stamps.Count);
            Assert.AreEqual("landmark_a", stamps[0].LandmarkId);
            Assert.AreEqual("landmark_z", stamps[1].LandmarkId);
        }

        [Test]
        public void Solver_ReferenceHeightIsRobustToAnOutlier()
        {
            // Flat 50 everywhere except a tall spike exactly at the centre.
            var ctx = BuildContext((wx, wz) =>
                (Mathf.Abs(wx - 50f) < 0.5f && Mathf.Abs(wz - 50f) < 0.5f) ? 0.9f : 0.5f);
            var sampler = new TerrainContextSampler(ctx);
            var def = CreateDefinition(LandmarkTerrainConformingMode.SoftFoundation, foundation: 6f);

            Assert.IsTrue(LandmarkSeatSolver.TrySolve(PlacementAt(def, "a", 50f, 50f), sampler, out var stamp));
            Assert.AreEqual(50f, stamp.SeatHeight, 1.0f, "median ignores the lone centre spike");
        }

        [Test]
        public void Solver_AppliesVerticalOffsetToSeat()
        {
            var ctx = Flat(50f);
            var sampler = new TerrainContextSampler(ctx);
            var def = CreateDefinition(LandmarkTerrainConformingMode.SoftFoundation, vOffset: -3f);

            Assert.IsTrue(LandmarkSeatSolver.TrySolve(PlacementAt(def, "a", 50f, 50f), sampler, out var stamp));
            Assert.AreEqual(47f, stamp.SeatHeight, 1e-3f);
        }

        [Test]
        public void Finalize_GroundsLandmarkOnStampedTerrain_NoFloatNoSink()
        {
            var def = CreateDefinition(LandmarkTerrainConformingMode.SoftFoundation, maxFill: 8f, residual: 0f);
            var ctx = BuildContext((wx, _) => 0.3f + 0.3f * (wx / WorldSize)); // gentle slope
            var sampler = new TerrainContextSampler(ctx);

            var placements = LandmarkPlannerPlacements(def, "landmark_a", 50f, 50f);
            var stamps = LandmarkTerrainStampPlanner.Plan(placements, sampler);
            LandmarkTerrainStampApplier.Apply(ctx, stamps);
            var identities = LandmarkPlanner.Finalize(placements, sampler);

            Assert.AreEqual(1, identities.Count);
            float groundAtCenter = sampler.SampleHeight(50f, 50f);
            Assert.AreEqual(groundAtCenter, identities[0].Position.y, 1e-3f,
                "the landmark grounds exactly on the seated terrain");
        }

        #region Helpers

        private static System.Collections.Generic.IReadOnlyList<LandmarkPlacement> LandmarkPlannerPlacements(
            LandmarkDefinition def, string id, float x, float z) =>
            new[] { PlacementAt(def, id, x, z) };

        private static void AssertNoCellExceeds(TerrainGenerationContext ctx, float original, float maxUp, float maxDown)
        {
            for (int x = 0; x < Resolution; x++)
                for (int z = 0; z < Resolution; z++)
                {
                    float world = ctx.NormalizedHeightMap[x, z] * TerrainH;
                    Assert.LessOrEqual(world - original, maxUp + 0.05f, $"fill exceeded at {x},{z}");
                    Assert.LessOrEqual(original - world, maxDown + 0.05f, $"cut exceeded at {x},{z}");
                }
        }

        private static float MaxAdjacentJump(TerrainGenerationContext ctx)
        {
            float max = 0f;
            for (int x = 0; x < Resolution - 1; x++)
                for (int z = 0; z < Resolution - 1; z++)
                {
                    float h = ctx.NormalizedHeightMap[x, z] * TerrainH;
                    max = Mathf.Max(max, Mathf.Abs(ctx.NormalizedHeightMap[x + 1, z] * TerrainH - h));
                    max = Mathf.Max(max, Mathf.Abs(ctx.NormalizedHeightMap[x, z + 1] * TerrainH - h));
                }
            return max;
        }

        #endregion
    }
}
