using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Inventory;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the reservation-driven content planner.
    /// The planner is pure C#: reservations in, deterministic SpawnRecipes out.
    /// No prefab is instantiated here (instantiation lives in WorldContentSpawner).
    /// </summary>
    public class WorldContentPlannerTests
    {
        private const float POSITION_EPSILON = 0.001f;

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

        #region Test Helpers

        private sealed class TestTerrainSampler : ITerrainSampler
        {
            public float WorldSize { get; set; } = 1024f;
            public float TerrainHeight { get; set; } = 150f;
            public UnityEngine.Vector3 WorldCenter => new UnityEngine.Vector3(WorldSize * 0.5f, 0f, WorldSize * 0.5f);
            public float Height = 10f;
            public float Slope = 5f;

            public float SampleHeight(float worldX, float worldZ) => Height;
            public float SampleSlope(float worldX, float worldZ) => Slope;

            public bool IsWithinBounds(float worldX, float worldZ)
            {
                return worldX >= 0f && worldX <= WorldSize && worldZ >= 0f && worldZ <= WorldSize;
            }
        }

        private void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private T Track<T>(T obj) where T : Object
        {
            createdObjects.Add(obj);
            return obj;
        }

        private GameObject CreatePrefabStub(string name)
        {
            return Track(new GameObject(name));
        }

        private EncounterDefinition CreateEncounterDefinition(
            string id, GameObject prefab, int minCount = 1, int maxCount = 3,
            WorldRing minimumRing = WorldRing.Nearlands, float spread = 4f)
        {
            var definition = Track(ScriptableObject.CreateInstance<EncounterDefinition>());
            SetField(definition, "encounterId", id);
            SetField(definition, "enemyPrefab", prefab);
            SetField(definition, "minimumEnemyCount", minCount);
            SetField(definition, "maximumEnemyCount", maxCount);
            SetField(definition, "spawnSpreadRadius", spread);
            SetField(definition, "minimumRing", minimumRing);
            return definition;
        }

        private ResourceSpawnDefinition CreateResourceDefinition(
            string id, ItemDefinition item, int minQuantity = 1, int maxQuantity = 3,
            WorldRing minimumRing = WorldRing.Refuge)
        {
            var definition = Track(ScriptableObject.CreateInstance<ResourceSpawnDefinition>());
            SetField(definition, "resourceId", id);
            SetField(definition, "item", item);
            SetField(definition, "minimumQuantity", minQuantity);
            SetField(definition, "maximumQuantity", maxQuantity);
            SetField(definition, "minimumRing", minimumRing);
            return definition;
        }

        private ItemDefinition CreateItemDefinition(string id)
        {
            var item = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            SetField(item, "itemId", id);
            return item;
        }

        private EncounterRegistry CreateEncounterRegistry(params EncounterDefinition[] definitions)
        {
            var registry = Track(ScriptableObject.CreateInstance<EncounterRegistry>());
            SetField(registry, "definitions", new List<EncounterDefinition>(definitions));
            return registry;
        }

        private ResourceSpawnRegistry CreateResourceRegistry(params ResourceSpawnDefinition[] definitions)
        {
            var registry = Track(ScriptableObject.CreateInstance<ResourceSpawnRegistry>());
            SetField(registry, "definitions", new List<ResourceSpawnDefinition>(definitions));
            return registry;
        }

        private EncounterReservation CreateEncounterReservation(
            string id = "encounter_42_0", WorldRing ring = WorldRing.Wildlands,
            Vector3? position = null, string hostNodeId = "node_42_junction_1",
            string radialPathId = "path_42_0")
        {
            var pos = position ?? new Vector3(500f, 10f, 500f);
            return new EncounterReservation(id, hostNodeId, pos, 8f, 300f, 300f / 512f, ring, radialPathId);
        }

        private ResourceReservation CreateResourceReservation(
            string id = "resource_42_0", WorldRing ring = WorldRing.Nearlands,
            Vector3? position = null)
        {
            var pos = position ?? new Vector3(450f, 10f, 520f);
            return new ResourceReservation(id, "node_42_deadend_5", pos, 5f, 120f, 120f / 512f, ring, "path_42_1");
        }

        private WorldContentPlan PlanSingleEncounter(
            int seed, EncounterReservation reservation, EncounterRegistry registry,
            ITerrainSampler sampler = null, WorldContentPlannerSettings settings = null)
        {
            return WorldContentPlanner.Plan(
                seed,
                new[] { reservation },
                null,
                registry,
                null,
                sampler ?? new TestTerrainSampler(),
                settings ?? DefaultSettings());
        }

        private static WorldContentPlannerSettings DefaultSettings()
        {
            return new WorldContentPlannerSettings();
        }

        private static void AssertRecipesIdentical(WorldContentPlan plan1, WorldContentPlan plan2)
        {
            Assert.AreEqual(plan1.Recipes.Count, plan2.Recipes.Count, "Recipe counts should match");
            Assert.AreEqual(plan1.Rejections.Count, plan2.Rejections.Count, "Rejection counts should match");

            for (int i = 0; i < plan1.Recipes.Count; i++)
            {
                var recipe1 = plan1.Recipes[i];
                var recipe2 = plan2.Recipes[i];

                Assert.AreEqual(recipe1.ReservationId, recipe2.ReservationId);
                Assert.AreEqual(recipe1.DefinitionId, recipe2.DefinitionId, $"Recipe {recipe1.ReservationId}: definition should match");
                Assert.AreEqual(recipe1.Entries.Count, recipe2.Entries.Count, $"Recipe {recipe1.ReservationId}: entry count should match");

                for (int e = 0; e < recipe1.Entries.Count; e++)
                {
                    Assert.AreEqual(recipe1.Entries[e].Position.x, recipe2.Entries[e].Position.x, POSITION_EPSILON);
                    Assert.AreEqual(recipe1.Entries[e].Position.y, recipe2.Entries[e].Position.y, POSITION_EPSILON);
                    Assert.AreEqual(recipe1.Entries[e].Position.z, recipe2.Entries[e].Position.z, POSITION_EPSILON);
                    Assert.AreEqual(recipe1.Entries[e].Quantity, recipe2.Entries[e].Quantity);
                    Assert.AreEqual(recipe1.Entries[e].Role, recipe2.Entries[e].Role);
                }
            }
        }

        #endregion

        #region Determinism

        [Test]
        public void SameSeed_ProducesIdenticalPlan()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(
                CreateEncounterDefinition("enc_wolves", prefab),
                CreateEncounterDefinition("enc_scavengers", prefab));
            var reservations = new[]
            {
                CreateEncounterReservation("encounter_42_0"),
                CreateEncounterReservation("encounter_42_1", position: new Vector3(300f, 10f, 700f))
            };

            var plan1 = WorldContentPlanner.Plan(12345, reservations, null, registry, null,
                new TestTerrainSampler(), DefaultSettings());
            var plan2 = WorldContentPlanner.Plan(12345, reservations, null, registry, null,
                new TestTerrainSampler(), DefaultSettings());

            AssertRecipesIdentical(plan1, plan2);
        }

        [Test]
        public void DifferentSeeds_CanProduceDifferentPlans()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(
                CreateEncounterDefinition("enc_a", prefab, 1, 3),
                CreateEncounterDefinition("enc_b", prefab, 1, 3));
            var reservation = CreateEncounterReservation();

            var reference = PlanSingleEncounter(0, reservation, registry);
            Assert.AreEqual(1, reference.Recipes.Count);

            bool anyDifferent = false;
            for (int seed = 1; seed <= 30 && !anyDifferent; seed++)
            {
                var other = PlanSingleEncounter(seed, reservation, registry);
                if (other.Recipes[0].DefinitionId != reference.Recipes[0].DefinitionId ||
                    other.Recipes[0].Entries.Count != reference.Recipes[0].Entries.Count)
                {
                    anyDifferent = true;
                }
            }

            Assert.IsTrue(anyDifferent, "Different seeds should be able to produce different selections or compositions");
        }

        [Test]
        public void UnityRandomState_DoesNotAffectPlan()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(
                CreateEncounterDefinition("enc_a", prefab, 1, 3),
                CreateEncounterDefinition("enc_b", prefab, 1, 3));
            var reservation = CreateEncounterReservation();

            Random.InitState(123);
            var plan1 = PlanSingleEncounter(777, reservation, registry);

            Random.InitState(987654);
            var plan2 = PlanSingleEncounter(777, reservation, registry);

            AssertRecipesIdentical(plan1, plan2);
        }

        [Test]
        public void ReservationOrder_DoesNotChangeResults()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(
                CreateEncounterDefinition("enc_a", prefab, 1, 3),
                CreateEncounterDefinition("enc_b", prefab, 1, 3));

            var a = CreateEncounterReservation("encounter_42_0");
            var b = CreateEncounterReservation("encounter_42_1", position: new Vector3(300f, 10f, 700f));
            var c = CreateEncounterReservation("encounter_42_2", position: new Vector3(700f, 10f, 300f));

            var planForward = WorldContentPlanner.Plan(12345, new[] { a, b, c }, null,
                registry, null, new TestTerrainSampler(), DefaultSettings());
            var planReversed = WorldContentPlanner.Plan(12345, new[] { c, b, a }, null,
                registry, null, new TestTerrainSampler(), DefaultSettings());

            Assert.AreEqual(planForward.Recipes.Count, planReversed.Recipes.Count);

            foreach (var recipe in planForward.Recipes)
            {
                var match = planReversed.Recipes.First(r => r.ReservationId == recipe.ReservationId);
                Assert.AreEqual(recipe.DefinitionId, match.DefinitionId,
                    $"Reservation {recipe.ReservationId}: selection must not depend on iteration order");
                Assert.AreEqual(recipe.Entries.Count, match.Entries.Count,
                    $"Reservation {recipe.ReservationId}: composition must not depend on iteration order");

                for (int e = 0; e < recipe.Entries.Count; e++)
                {
                    Assert.AreEqual(recipe.Entries[e].Position.x, match.Entries[e].Position.x, POSITION_EPSILON);
                    Assert.AreEqual(recipe.Entries[e].Position.z, match.Entries[e].Position.z, POSITION_EPSILON);
                }
            }
        }

        #endregion

        #region Compatibility and rejection

        [Test]
        public void Selection_RespectsMinimumRing()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var farlandsOnly = CreateEncounterDefinition("enc_farlands", prefab, minimumRing: WorldRing.Farlands);
            var registry = CreateEncounterRegistry(farlandsOnly);

            var wildlandsPlan = PlanSingleEncounter(1, CreateEncounterReservation(ring: WorldRing.Wildlands), registry);
            Assert.AreEqual(0, wildlandsPlan.Recipes.Count, "Wildlands reservation must not receive a Farlands-only encounter");
            Assert.AreEqual(1, wildlandsPlan.Rejections.Count);
            Assert.AreEqual(SpawnRejectionReason.NoCompatibleDefinition, wildlandsPlan.Rejections[0].Reason);

            var farlandsPlan = PlanSingleEncounter(1, CreateEncounterReservation(ring: WorldRing.Farlands), registry);
            Assert.AreEqual(1, farlandsPlan.Recipes.Count, "Farlands reservation should receive the compatible definition");
            Assert.AreEqual("enc_farlands", farlandsPlan.Recipes[0].DefinitionId);
        }

        [Test]
        public void Reservation_WithoutCompatibleDefinition_IsRejectedCleanly()
        {
            var registry = CreateEncounterRegistry();
            var plan = PlanSingleEncounter(1, CreateEncounterReservation(), registry);

            Assert.AreEqual(0, plan.Recipes.Count);
            Assert.AreEqual(1, plan.Rejections.Count);
            Assert.AreEqual(SpawnRejectionReason.NoCompatibleDefinition, plan.Rejections[0].Reason);
            Assert.AreEqual("encounter_42_0", plan.Rejections[0].ReservationId);
            Assert.AreEqual(1, plan.TotalReservations, "The reservation is still counted as received");
        }

        [Test]
        public void SteepSlope_ProducesNoRecipe()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab));
            var sampler = new TestTerrainSampler { Slope = 60f };

            var plan = PlanSingleEncounter(1, CreateEncounterReservation(), registry, sampler);

            Assert.AreEqual(0, plan.Recipes.Count);
            Assert.AreEqual(SpawnRejectionReason.SlopeTooSteep, plan.Rejections[0].Reason);
        }

        [Test]
        public void OutOfBounds_ProducesNoRecipe()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab));

            var plan = PlanSingleEncounter(1,
                CreateEncounterReservation(position: new Vector3(-50f, 10f, 500f)), registry);

            Assert.AreEqual(0, plan.Recipes.Count);
            Assert.AreEqual(SpawnRejectionReason.OutOfTerrainBounds, plan.Rejections[0].Reason);
        }

        [Test]
        public void RefugeRing_NeverReceivesEncounter()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            // Deliberately permissive definition claiming Refuge is allowed
            var permissive = CreateEncounterDefinition("enc_permissive", prefab, minimumRing: WorldRing.Refuge);
            var registry = CreateEncounterRegistry(permissive);

            var plan = PlanSingleEncounter(1, CreateEncounterReservation(ring: WorldRing.Refuge), registry);

            Assert.AreEqual(0, plan.Recipes.Count, "The Refuge ring must never host an encounter");
            Assert.AreEqual(SpawnRejectionReason.RefugeExclusion, plan.Rejections[0].Reason);
        }

        [Test]
        public void MissingEnemyPrefab_IsRejected()
        {
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_broken", null));
            var plan = PlanSingleEncounter(1, CreateEncounterReservation(), registry);

            Assert.AreEqual(0, plan.Recipes.Count);
            Assert.AreEqual(SpawnRejectionReason.MissingPrefab, plan.Rejections[0].Reason);
        }

        [Test]
        public void DisabledCategory_IsRejected()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab));
            var settings = new WorldContentPlannerSettings { EncountersEnabled = false };

            var plan = PlanSingleEncounter(1, CreateEncounterReservation(), registry, settings: settings);

            Assert.AreEqual(0, plan.Recipes.Count);
            Assert.AreEqual(SpawnRejectionReason.CategoryDisabled, plan.Rejections[0].Reason);
        }

        #endregion

        #region Recipe content

        [Test]
        public void RadialData_PropagatedToRecipes()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab));
            var reservation = CreateEncounterReservation();

            var plan = PlanSingleEncounter(1, reservation, registry);

            Assert.AreEqual(1, plan.Recipes.Count);
            var recipe = plan.Recipes[0];
            Assert.AreEqual(reservation.ReservationId, recipe.ReservationId);
            Assert.AreEqual(reservation.HostNodeId, recipe.HostNodeId);
            Assert.AreEqual(reservation.DistanceFromRefuge, recipe.DistanceFromRefuge, POSITION_EPSILON);
            Assert.AreEqual(reservation.NormalizedWorldRadius, recipe.NormalizedWorldRadius, POSITION_EPSILON);
            Assert.AreEqual(reservation.Ring, recipe.Ring);
            Assert.AreEqual(reservation.RadialPathId, recipe.RadialPathId);
        }

        [Test]
        public void EncounterComposition_WithinConfiguredBounds()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab, 2, 3, spread: 5f));
            var reservation = CreateEncounterReservation();

            for (int seed = 0; seed < 20; seed++)
            {
                var plan = PlanSingleEncounter(seed, reservation, registry);
                Assert.AreEqual(1, plan.Recipes.Count);
                var recipe = plan.Recipes[0];

                Assert.GreaterOrEqual(recipe.Entries.Count, 2, $"Seed {seed}: at least minimum members");
                Assert.LessOrEqual(recipe.Entries.Count, 3, $"Seed {seed}: at most maximum members");

                foreach (var entry in recipe.Entries)
                {
                    float distance = Vector2.Distance(
                        new Vector2(entry.Position.x, entry.Position.z),
                        new Vector2(recipe.AnchorPosition.x, recipe.AnchorPosition.z));
                    Assert.LessOrEqual(distance, 5f + POSITION_EPSILON,
                        $"Seed {seed}: members stay within the spread radius");
                    Assert.AreEqual(1, entry.Quantity);
                }
            }
        }

        [Test]
        public void ResourceQuantity_WithinConfiguredBounds()
        {
            var item = CreateItemDefinition("item_test");
            var registry = CreateResourceRegistry(CreateResourceDefinition("res_a", item, 2, 4));
            var reservation = CreateResourceReservation();

            for (int seed = 0; seed < 20; seed++)
            {
                var plan = WorldContentPlanner.Plan(seed, null, new[] { reservation },
                    null, registry, new TestTerrainSampler(), DefaultSettings());

                Assert.AreEqual(1, plan.Recipes.Count);
                var entry = plan.Recipes[0].Entries[0];
                Assert.GreaterOrEqual(entry.Quantity, 2);
                Assert.LessOrEqual(entry.Quantity, 4);
            }
        }

        [Test]
        public void BothCategories_Resolvable()
        {
            // Landmarks are no longer content: the planner resolves encounters
            // and resources (landmark planning lives in LandmarkPlannerTests).
            var enemyPrefab = CreatePrefabStub("EnemyStub");
            var item = CreateItemDefinition("item_test");

            var encounterRegistry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", enemyPrefab));
            var resourceRegistry = CreateResourceRegistry(CreateResourceDefinition("res_a", item));

            var plan = WorldContentPlanner.Plan(
                12345,
                new[] { CreateEncounterReservation() },
                new[] { CreateResourceReservation() },
                encounterRegistry,
                resourceRegistry,
                new TestTerrainSampler(),
                DefaultSettings());

            Assert.AreEqual(2, plan.TotalReservations);
            Assert.AreEqual(2, plan.Recipes.Count, "Both categories should resolve");
            Assert.AreEqual(0, plan.Rejections.Count);

            Assert.IsTrue(plan.Recipes.Any(r => r.Category == WorldContentCategory.Encounter));
            Assert.IsTrue(plan.Recipes.Any(r => r.Category == WorldContentCategory.Resource));
        }

        [Test]
        public void EntryHeights_ComeFromTerrainSampler()
        {
            var prefab = CreatePrefabStub("EnemyStub");
            var registry = CreateEncounterRegistry(CreateEncounterDefinition("enc_a", prefab));
            var sampler = new TestTerrainSampler { Height = 42.5f };

            var plan = PlanSingleEncounter(1, CreateEncounterReservation(), registry, sampler);

            Assert.AreEqual(1, plan.Recipes.Count);
            foreach (var entry in plan.Recipes[0].Entries)
            {
                Assert.AreEqual(42.5f, entry.Position.y, POSITION_EPSILON,
                    "Entry heights must be sampled from the terrain, not copied from the reservation");
            }
        }

        #endregion
    }
}
