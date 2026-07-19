using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the world progression authority: context stability,
    /// seed independence of rings, depth clamping, definition ring windows
    /// (inclusive), and deterministic weighted selection in the planner.
    /// </summary>
    public class WorldProgressionTests
    {
        private const float WORLD_RADIUS = 512f;
        private const float EPSILON = 0.0001f;

        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            createdObjects.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static WorldProgression CreateProgression(Vector3? refuge = null)
        {
            return new WorldProgression(
                refuge ?? new Vector3(512f, 10f, 512f),
                WORLD_RADIUS,
                WorldRingConfig.CreateDefault());
        }

        #region Context stability and seed independence

        [Test]
        public void SamePosition_AlwaysSameContext()
        {
            var progression = CreateProgression();
            var position = new Vector3(700f, 25f, 430f);

            var a = progression.GetContext(position);
            var b = progression.GetContext(position);

            Assert.AreEqual(a.Ring, b.Ring);
            Assert.AreEqual(a.Depth01, b.Depth01, EPSILON);
            Assert.AreEqual(a.Difficulty01, b.Difficulty01, EPSILON);
            Assert.AreEqual(a.ExpectedLootTier, b.ExpectedLootTier);
            Assert.AreEqual(a.FogDensity01, b.FogDensity01, EPSILON);
            Assert.AreEqual(a.IsInsideWorldDisc, b.IsInsideWorldDisc);
        }

        [Test]
        public void SameDistance_UnderDifferentSeeds_SameRingAndDepth()
        {
            // Different seeds move the refuge, never the ring rule: for a
            // given DISTANCE the ring and depth are identical.
            var seedA = CreateProgression(new Vector3(500f, 10f, 520f));
            var seedB = CreateProgression(new Vector3(530f, 12f, 495f));

            float[] distances = { 0f, 30f, 90f, 200f, 350f, 480f, 511f, 600f };
            foreach (float distance in distances)
            {
                var a = seedA.GetContextFromDistance(distance);
                var b = seedB.GetContextFromDistance(distance);

                Assert.AreEqual(a.Ring, b.Ring, $"Ring must not depend on the seed (distance {distance})");
                Assert.AreEqual(a.Depth01, b.Depth01, EPSILON, $"Depth must not depend on the seed (distance {distance})");
                Assert.AreEqual(a.Difficulty01, b.Difficulty01, EPSILON);
            }
        }

        [Test]
        public void EquidistantPositions_AroundEachRefuge_SameRing()
        {
            var seedA = CreateProgression(new Vector3(500f, 10f, 520f));
            var seedB = CreateProgression(new Vector3(530f, 12f, 495f));

            var a = seedA.GetContext(seedA.RefugePosition + new Vector3(250f, 40f, 0f));
            var b = seedB.GetContext(seedB.RefugePosition + new Vector3(0f, 5f, 250f));

            Assert.AreEqual(a.Ring, b.Ring, "Equal distance to the respective refuge must yield the same ring");
        }

        [Test]
        public void Depth_IsMonotonicWithDistance()
        {
            var progression = CreateProgression();

            float previousDepth = -1f;
            for (float distance = 0f; distance <= WORLD_RADIUS * 1.5f; distance += 16f)
            {
                float depth = progression.GetContextFromDistance(distance).Depth01;
                Assert.GreaterOrEqual(depth, previousDepth, $"Depth01 must never decrease (distance {distance})");
                previousDepth = depth;
            }
        }

        [Test]
        public void BeyondTheDisc_DepthClampsToOne_AndIsOutside()
        {
            var progression = CreateProgression();

            var beyond = progression.GetContextFromDistance(WORLD_RADIUS * 1.3f);
            Assert.AreEqual(1f, beyond.Depth01, EPSILON, "Depth beyond the disc must clamp to 1");
            Assert.IsFalse(beyond.IsInsideWorldDisc);
            Assert.AreEqual(WorldRing.Void, beyond.Ring);

            var atEdge = progression.GetContextFromDistance(WORLD_RADIUS);
            Assert.IsTrue(atEdge.IsInsideWorldDisc, "The disc edge itself is still inside");
            Assert.AreEqual(1f, atEdge.Depth01, EPSILON);
        }

        [Test]
        public void RingBoundaries_MatchRingConfig_MinInclusive()
        {
            // The spatial thresholds keep their min-inclusive rule through
            // the progression authority.
            var ringConfig = WorldRingConfig.CreateDefault();
            var progression = new WorldProgression(Vector3.zero, WORLD_RADIUS, ringConfig);

            foreach (WorldRing ring in System.Enum.GetValues(typeof(WorldRing)))
            {
                float min = ringConfig.GetMinimumRadius(ring);
                var context = progression.GetContextFromDistance(min * WORLD_RADIUS);
                Assert.AreEqual(ring, context.Ring,
                    $"Exactly at its minimum radius, the position must belong to {ring}");
            }
        }

        #endregion

        #region Defaults without config

        [Test]
        public void WithoutConfig_DifficultyEqualsDepth_AndTiersProgress()
        {
            var progression = CreateProgression();

            var refuge = progression.GetContextFromDistance(0f);
            Assert.AreEqual(0f, refuge.Difficulty01, EPSILON);
            Assert.AreEqual(0, refuge.ExpectedLootTier);

            var mid = progression.GetContextFromDistance(WORLD_RADIUS * 0.5f);
            Assert.AreEqual(0.5f, mid.Difficulty01, EPSILON, "Built-in default difficulty is linear with depth");

            var edge = progression.GetContextFromDistance(WORLD_RADIUS);
            Assert.AreEqual(1f, edge.Difficulty01, EPSILON);
            Assert.AreEqual(4, edge.ExpectedLootTier, "Built-in default max loot tier is 4 at full depth");
        }

        [Test]
        public void LootTier_NeverDecreasesWithDepth()
        {
            var progression = CreateProgression();

            int previousTier = -1;
            for (float distance = 0f; distance <= WORLD_RADIUS; distance += 8f)
            {
                int tier = progression.GetContextFromDistance(distance).ExpectedLootTier;
                Assert.GreaterOrEqual(tier, previousTier, $"Loot tier must never decrease (distance {distance})");
                previousTier = tier;
            }
        }

        #endregion

        #region Definition ring windows (inclusive) and weights

        [Test]
        public void CompatibilityWindow_IsInclusiveOnBothEnds()
        {
            Assert.IsTrue(WorldContentCompatibility.Evaluate(
                WorldRing.Nearlands, 0.2f, WorldRing.Nearlands, WorldRing.Farlands, 1f, null, out _, out _),
                "MinimumRing itself must be compatible (inclusive)");

            Assert.IsTrue(WorldContentCompatibility.Evaluate(
                WorldRing.Farlands, 0.5f, WorldRing.Nearlands, WorldRing.Farlands, 1f, null, out _, out _),
                "MaximumRing itself must be compatible (inclusive)");

            Assert.IsFalse(WorldContentCompatibility.Evaluate(
                WorldRing.Refuge, 0f, WorldRing.Nearlands, WorldRing.Farlands, 1f, null, out _, out string below));
            StringAssert.Contains("below minimum", below);

            Assert.IsFalse(WorldContentCompatibility.Evaluate(
                WorldRing.Outerlands, 0.8f, WorldRing.Nearlands, WorldRing.Farlands, 1f, null, out _, out string above));
            StringAssert.Contains("above maximum", above);
        }

        [Test]
        public void Void_RequiresExplicitOptIn()
        {
            // Default definition window ends at Edgelands: Void is outside
            // the playable disc unless a definition opts in explicitly.
            Assert.IsFalse(WorldContentCompatibility.Evaluate(
                WorldRing.Void, 1f, WorldRing.Refuge, WorldRing.Edgelands, 1f, null, out _, out string reason));
            StringAssert.Contains("above maximum", reason);

            Assert.IsTrue(WorldContentCompatibility.Evaluate(
                WorldRing.Void, 1f, WorldRing.Refuge, WorldRing.Void, 1f, null, out _, out _));
        }

        [Test]
        public void ZeroWeight_IsExcluded_WithReason()
        {
            Assert.IsFalse(WorldContentCompatibility.Evaluate(
                WorldRing.Wildlands, 0.3f, WorldRing.Refuge, WorldRing.Edgelands, 0f, null,
                out _, out string reason));
            StringAssert.Contains("zero weight", reason);
        }

        [Test]
        public void WeightByDepth_ModulatesTheGlobalDepth()
        {
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            Assert.IsFalse(WorldContentCompatibility.Evaluate(
                WorldRing.Nearlands, 0f, WorldRing.Refuge, WorldRing.Edgelands, 2f, curve, out _, out _),
                "A curve worth 0 at this depth must exclude the definition");

            Assert.IsTrue(WorldContentCompatibility.Evaluate(
                WorldRing.Wildlands, 0.5f, WorldRing.Refuge, WorldRing.Edgelands, 2f, curve,
                out float weight, out _));
            Assert.AreEqual(1f, weight, EPSILON, "Effective weight = selectionWeight x curve(Depth01)");
        }

        #endregion

        #region Planner: deterministic weighted selection

        private sealed class TestTerrainSampler : ITerrainSampler
        {
            public float WorldSize => 1024f;
            public float TerrainHeight => 150f;
            public float SampleHeight(float worldX, float worldZ) => 10f;
            public float SampleSlope(float worldX, float worldZ) => 5f;
            public bool IsWithinBounds(float worldX, float worldZ) =>
                worldX >= 0f && worldX <= 1024f && worldZ >= 0f && worldZ <= 1024f;
        }

        private EncounterDefinition CreateEncounter(string id, float weight,
            WorldRing minimumRing = WorldRing.Nearlands, WorldRing maximumRing = WorldRing.Edgelands)
        {
            var definition = Track(ScriptableObject.CreateInstance<EncounterDefinition>());
            SetField(definition, "encounterId", id);
            SetField(definition, "enemyPrefab", Track(new GameObject($"prefab_{id}")));
            SetField(definition, "minimumRing", minimumRing);
            SetField(definition, "maximumRing", maximumRing);
            SetField(definition, "selectionWeight", weight);
            return definition;
        }

        private EncounterRegistry CreateRegistry(params EncounterDefinition[] definitions)
        {
            var registry = Track(ScriptableObject.CreateInstance<EncounterRegistry>());
            SetField(registry, "definitions", new List<EncounterDefinition>(definitions));
            return registry;
        }

        private static List<EncounterReservation> CreateReservations(int count, WorldRing ring, float normalized)
        {
            var reservations = new List<EncounterReservation>(count);
            for (int i = 0; i < count; i++)
            {
                reservations.Add(new EncounterReservation(
                    $"encounter_test_{i}", $"node_{i}", new Vector3(500f, 10f, 500f), 8f,
                    normalized * WORLD_RADIUS, normalized, ring, "path_0"));
            }
            return reservations;
        }

        [Test]
        public void WeightedSelection_IsFullyDeterministic()
        {
            var registry = CreateRegistry(CreateEncounter("light", 1f), CreateEncounter("heavy", 3f));
            var reservations = CreateReservations(40, WorldRing.Wildlands, 0.3f);
            var sampler = new TestTerrainSampler();

            var plan1 = WorldContentPlanner.Plan(12345, reservations, null, null, registry, null, null, sampler);
            var plan2 = WorldContentPlanner.Plan(12345, reservations, null, null, registry, null, null, sampler);

            Assert.AreEqual(plan1.Recipes.Count, plan2.Recipes.Count);
            for (int i = 0; i < plan1.Recipes.Count; i++)
            {
                Assert.AreEqual(plan1.Recipes[i].DefinitionId, plan2.Recipes[i].DefinitionId,
                    "Weighted selection must be bit-identical across runs");
            }
        }

        [Test]
        public void WeightedSelection_FavoursHeavierDefinitions()
        {
            // Fully deterministic (fixed seed, fixed reservation ids): the
            // wide tolerance documents the intent without being fragile.
            var registry = CreateRegistry(CreateEncounter("light", 1f), CreateEncounter("heavy", 3f));
            var reservations = CreateReservations(200, WorldRing.Wildlands, 0.3f);
            var sampler = new TestTerrainSampler();

            var plan = WorldContentPlanner.Plan(12345, reservations, null, null, registry, null, null, sampler);

            int heavy = 0;
            foreach (var recipe in plan.Recipes)
            {
                if (recipe.DefinitionId == "heavy") heavy++;
            }

            float heavyRatio = heavy / (float)plan.Recipes.Count;
            Assert.Greater(heavyRatio, 0.55f, "A 3x weight must clearly dominate a 1x weight");
            Assert.Less(heavyRatio, 0.95f, "The 1x definition must still appear");
        }

        [Test]
        public void VoidReservations_AreRejected_UnlessDefinitionOptsIn()
        {
            var sampler = new TestTerrainSampler();
            var reservations = CreateReservations(1, WorldRing.Void, 1.05f);

            var defaultRegistry = CreateRegistry(CreateEncounter("normal", 1f));
            var rejectedPlan = WorldContentPlanner.Plan(1, reservations, null, null, defaultRegistry, null, null, sampler);
            Assert.AreEqual(0, rejectedPlan.Recipes.Count, "Void must be outside the default window");
            Assert.AreEqual(SpawnRejectionReason.NoCompatibleDefinition, rejectedPlan.Rejections[0].Reason);

            var voidRegistry = CreateRegistry(CreateEncounter("voidwalker", 1f, maximumRing: WorldRing.Void));
            var acceptedPlan = WorldContentPlanner.Plan(1, reservations, null, null, voidRegistry, null, null, sampler);
            Assert.AreEqual(1, acceptedPlan.Recipes.Count, "Explicit MaximumRing = Void must opt in");
        }

        [Test]
        public void WeightByDepth_ShiftsContentWithDepth()
        {
            // "Crystal" only exists deep, "wood" everywhere: near the refuge
            // the crystal curve is worth 0 and wood always wins; deep, both
            // are eligible.
            var wood = CreateEncounter("wood", 1f, WorldRing.Nearlands);
            var crystal = CreateEncounter("crystal", 1f, WorldRing.Nearlands);
            // Flat zero until mid-depth, then rising: truly absent near the
            // refuge (a merely small weight would still be eligible).
            SetField(crystal, "weightByDepth", new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f)));
            var registry = CreateRegistry(wood, crystal);
            var sampler = new TestTerrainSampler();

            var shallow = WorldContentPlanner.Plan(7,
                CreateReservations(30, WorldRing.Nearlands, 0.08f), null, null, registry, null, null, sampler);
            foreach (var recipe in shallow.Recipes)
            {
                Assert.AreEqual("wood", recipe.DefinitionId,
                    "Near the refuge the depth curve must exclude the deep-only content");
            }

            var deep = WorldContentPlanner.Plan(7,
                CreateReservations(60, WorldRing.Edgelands, 0.9f), null, null, registry, null, null, sampler);
            bool crystalAppeared = false;
            foreach (var recipe in deep.Recipes)
            {
                if (recipe.DefinitionId == "crystal") crystalAppeared = true;
            }
            Assert.IsTrue(crystalAppeared, "Deep in the world the depth-gated content must appear");
        }

        [Test]
        public void InjectedProgression_MatchesReservationDepthFallback()
        {
            // With a progression whose radius matches the reservations, the
            // injected path and the recorded-normalized-radius fallback pick
            // identically (same depth, same draws).
            var registry = CreateRegistry(CreateEncounter("light", 1f), CreateEncounter("heavy", 3f));
            var reservations = CreateReservations(40, WorldRing.Wildlands, 0.3f);
            var sampler = new TestTerrainSampler();
            var progression = CreateProgression();

            var withProgression = WorldContentPlanner.Plan(99, reservations, null, null, registry, null, null,
                sampler, null, progression);
            var withoutProgression = WorldContentPlanner.Plan(99, reservations, null, null, registry, null, null,
                sampler);

            Assert.AreEqual(withoutProgression.Recipes.Count, withProgression.Recipes.Count);
            for (int i = 0; i < withProgression.Recipes.Count; i++)
            {
                Assert.AreEqual(withoutProgression.Recipes[i].DefinitionId, withProgression.Recipes[i].DefinitionId);
            }
        }

        #endregion
    }
}
