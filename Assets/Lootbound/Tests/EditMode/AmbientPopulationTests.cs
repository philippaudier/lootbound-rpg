using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.Combat;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Population;
using Lootbound.Gameplay.World.Progression;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the ambient population: cell math (relative to the
    /// disc center), namespaced deterministic ids, planner determinism,
    /// structural/transient rejection taxonomy, validator rules with fakes,
    /// and the registry as local world memory.
    /// </summary>
    public class AmbientPopulationTests
    {
        private const float WORLD_RADIUS = 512f;
        private const float CELL_SIZE = 48f;

        private static readonly Vector3 DiscCenter = new Vector3(512f, 10f, 512f);

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
            return new WorldProgression(refuge ?? DiscCenter, WORLD_RADIUS, WorldRingConfig.CreateDefault());
        }

        private AmbientPopulationDefinition CreateDefinition(
            string id, float weight = 1f,
            WorldRing minimumRing = WorldRing.Nearlands, WorldRing maximumRing = WorldRing.Edgelands,
            AnimationCurve weightByDepth = null)
        {
            var definition = Track(ScriptableObject.CreateInstance<AmbientPopulationDefinition>());
            SetField(definition, "populationId", id);
            SetField(definition, "minimumRing", minimumRing);
            SetField(definition, "maximumRing", maximumRing);
            SetField(definition, "selectionWeight", weight);
            if (weightByDepth != null)
            {
                SetField(definition, "weightByDepth", weightByDepth);
            }
            return definition;
        }

        private AmbientPopulationConfig CreateConfig(params AmbientPopulationDefinition[] definitions)
        {
            var config = Track(ScriptableObject.CreateInstance<AmbientPopulationConfig>());
            SetField(config, "definitions", new List<AmbientPopulationDefinition>(definitions));
            SetField(config, "cellSize", CELL_SIZE);
            SetField(config, "maxPlansPerCell", 2);
            SetField(config, "candidatesPerAnchor", 6);
            return config;
        }

        #region Cells

        [Test]
        public void SamePosition_AlwaysSameCell()
        {
            var position = new Vector3(700f, 30f, 430f);

            Assert.AreEqual(
                AmbientPopulationCells.WorldToCell(position, DiscCenter, CELL_SIZE),
                AmbientPopulationCells.WorldToCell(position, DiscCenter, CELL_SIZE));
        }

        [Test]
        public void NeighborCells_AreAdjacent()
        {
            Vector3 justLeft = DiscCenter + new Vector3(-0.1f, 0f, 0.1f);
            Vector3 justRight = DiscCenter + new Vector3(0.1f, 0f, 0.1f);

            var left = AmbientPopulationCells.WorldToCell(justLeft, DiscCenter, CELL_SIZE);
            var right = AmbientPopulationCells.WorldToCell(justRight, DiscCenter, CELL_SIZE);

            Assert.AreEqual(new Vector2Int(-1, 0), left, "Negative local coordinates must floor correctly");
            Assert.AreEqual(new Vector2Int(0, 0), right);
        }

        [Test]
        public void CellCenter_RoundTripsToTheSameCell()
        {
            foreach (var cell in new[] { new Vector2Int(0, 0), new Vector2Int(-3, 7), new Vector2Int(5, -2) })
            {
                Vector3 center = AmbientPopulationCells.CellCenter(cell, DiscCenter, CELL_SIZE);
                Assert.AreEqual(cell, AmbientPopulationCells.WorldToCell(center, DiscCenter, CELL_SIZE),
                    $"Cell center of {cell} must map back to {cell}");
            }
        }

        [Test]
        public void Cells_AreRelativeToTheDiscCenter_NotTheWorldOrigin()
        {
            // The same world position belongs to different cells when the
            // disc center moves: the dependency is explicit, never implicit.
            var position = new Vector3(700f, 0f, 430f);

            var cellA = AmbientPopulationCells.WorldToCell(position, DiscCenter, CELL_SIZE);
            var cellB = AmbientPopulationCells.WorldToCell(position, DiscCenter + new Vector3(100f, 0f, 0f), CELL_SIZE);

            Assert.AreNotEqual(cellA, cellB);
        }

        #endregion

        #region Ids and seeds

        [Test]
        public void AmbientIds_AreNamespaced_AndCannotCollideWithAuthoredIds()
        {
            string planId = AmbientPopulationIds.PlanId(new Vector2Int(12, -4), 1);
            string memberId = AmbientPopulationIds.MemberId(planId, 2);

            StringAssert.StartsWith("ambient_v", planId);
            StringAssert.StartsWith("ambient_v", memberId);
            // Authored ids are "encounter_{seed}_{index}" etc. - different namespace.
            StringAssert.DoesNotStartWith("encounter_", planId);
        }

        [Test]
        public void CellSeed_IsStable_AndSensitiveToEveryComponent()
        {
            var cell = new Vector2Int(3, -7);

            Assert.AreEqual(
                AmbientPopulationIds.CellSeed(42, cell),
                AmbientPopulationIds.CellSeed(42, cell), "Same inputs must give the same seed");

            Assert.AreNotEqual(
                AmbientPopulationIds.CellSeed(42, cell),
                AmbientPopulationIds.CellSeed(43, cell), "World seed must matter");

            Assert.AreNotEqual(
                AmbientPopulationIds.CellSeed(42, cell),
                AmbientPopulationIds.CellSeed(42, new Vector2Int(-7, 3)), "Cell coordinates must matter (and not commute)");
        }

        #endregion

        #region Planner determinism

        [Test]
        public void PlanCell_IsFullyDeterministic()
        {
            var config = CreateConfig(CreateDefinition("walker"));
            var progression = CreateProgression();
            var cell = new Vector2Int(2, 1); // well inside the disc, outside the refuge

            var first = AmbientPopulationPlanner.PlanCell(cell, 12345, progression, config);
            var second = AmbientPopulationPlanner.PlanCell(cell, 12345, progression, config);

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].PlanId, second[i].PlanId);
                Assert.AreEqual(first[i].PopulationId, second[i].PopulationId);
                Assert.AreEqual(first[i].GroupSize, second[i].GroupSize);
                Assert.AreEqual(first[i].StableSeed, second[i].StableSeed);
                Assert.AreEqual(first[i].CandidatePositions.Count, second[i].CandidatePositions.Count);
                for (int c = 0; c < first[i].CandidatePositions.Count; c++)
                {
                    Assert.AreEqual(first[i].CandidatePositions[c], second[i].CandidatePositions[c],
                        "Candidate positions must be bit-identical");
                }
            }
        }

        [Test]
        public void PlanCell_DifferentCellOrSeed_ProducesDifferentCandidates()
        {
            var config = CreateConfig(CreateDefinition("walker"));
            var progression = CreateProgression();

            var baseline = AmbientPopulationPlanner.PlanCell(new Vector2Int(2, 1), 12345, progression, config);
            var otherCell = AmbientPopulationPlanner.PlanCell(new Vector2Int(3, 1), 12345, progression, config);
            var otherSeed = AmbientPopulationPlanner.PlanCell(new Vector2Int(2, 1), 54321, progression, config);

            Assert.Greater(baseline.Count, 0, "Fixture cell should produce at least one plan");
            Assert.AreNotEqual(baseline[0].CandidatePositions[0], otherCell[0].CandidatePositions[0]);
            Assert.AreNotEqual(baseline[0].CandidatePositions[0], otherSeed[0].CandidatePositions[0]);
        }

        [Test]
        public void PlanCell_CandidatesStayInsideTheCell()
        {
            var config = CreateConfig(CreateDefinition("walker"));
            var progression = CreateProgression();
            var cell = new Vector2Int(2, 1);

            var plans = AmbientPopulationPlanner.PlanCell(cell, 7, progression, config);

            foreach (var plan in plans)
            {
                foreach (var candidate in plan.CandidatePositions)
                {
                    Assert.AreEqual(cell, AmbientPopulationCells.WorldToCell(candidate, DiscCenter, CELL_SIZE),
                        "Every candidate must lie inside its cell");
                }
            }
        }

        [Test]
        public void RefugeCell_WithNoCompatibleDefinition_ProducesAQuietCell()
        {
            var config = CreateConfig(CreateDefinition("walker", minimumRing: WorldRing.Nearlands));
            // Small cells so the refuge-corner cell lies ENTIRELY inside the
            // Refuge ring (0..0.05 x 512 = 25.6m; a 16m cell fits, 48m spills).
            SetField(config, "cellSize", 16f);
            var progression = CreateProgression();

            var refugeCell = AmbientPopulationCells.WorldToCell(DiscCenter, DiscCenter, 16f);
            var plans = AmbientPopulationPlanner.PlanCell(refugeCell, 42, progression, config);

            Assert.AreEqual(0, plans.Count, "The Refuge must stay quiet through ring windows, not special cases");
        }

        [Test]
        public void FarOutsideTheDisc_ProducesNoPlans()
        {
            var config = CreateConfig(CreateDefinition("walker", maximumRing: WorldRing.Edgelands));
            var progression = CreateProgression();

            // A cell entirely beyond the disc radius.
            var farCell = new Vector2Int(30, 30); // ~ (1440,1440) local > 512 radius
            var plans = AmbientPopulationPlanner.PlanCell(farCell, 42, progression, config);

            Assert.AreEqual(0, plans.Count, "Void/outside-disc cells must stay empty by default");
        }

        [Test]
        public void ZeroDepthWeight_ExcludesTheDefinition()
        {
            var deepOnly = CreateDefinition("deep", weightByDepth: new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.5f, 0f), new Keyframe(1f, 1f)));
            var config = CreateConfig(deepOnly);
            var progression = CreateProgression();

            // Shallow cell (Nearlands-ish, depth ~0.1): curve is flat zero.
            var shallowCell = new Vector2Int(1, 0);
            var plans = AmbientPopulationPlanner.PlanCell(shallowCell, 42, progression, config);

            Assert.AreEqual(0, plans.Count);
        }

        [Test]
        public void HeavierDefinition_DominatesAcrossManyCells_Deterministically()
        {
            var config = CreateConfig(CreateDefinition("light", 1f), CreateDefinition("heavy", 3f));
            var progression = CreateProgression();

            int light = 0, heavy = 0;
            for (int x = -8; x <= 8; x++)
            {
                for (int y = -8; y <= 8; y++)
                {
                    foreach (var plan in AmbientPopulationPlanner.PlanCell(new Vector2Int(x, y), 42, progression, config))
                    {
                        if (plan.PopulationId == "heavy") heavy++;
                        else light++;
                    }
                }
            }

            int total = light + heavy;
            Assert.Greater(total, 50, "Fixture must produce a meaningful sample");
            float heavyRatio = heavy / (float)total;
            Assert.Greater(heavyRatio, 0.55f, "A 3x weight must clearly dominate (deterministic, wide tolerance)");
            Assert.Less(heavyRatio, 0.95f, "The light definition must still appear");
        }

        #endregion

        #region Rejection taxonomy

        [Test]
        public void RejectionReasons_AreCorrectlyClassified()
        {
            var structural = new[]
            {
                AmbientSpawnRejectionReason.OutsideWorldDisc,
                AmbientSpawnRejectionReason.RingIncompatible,
                AmbientSpawnRejectionReason.SlopeInvalid,
                AmbientSpawnRejectionReason.NoNavMesh,
                AmbientSpawnRejectionReason.TooCloseToRefuge,
                AmbientSpawnRejectionReason.TooCloseToAuthoredContent
            };
            var transient = new[]
            {
                AmbientSpawnRejectionReason.PlayerTooClose,
                AmbientSpawnRejectionReason.InsideCameraFrustum,
                AmbientSpawnRejectionReason.GlobalBudgetReached,
                AmbientSpawnRejectionReason.DefinitionBudgetReached,
                AmbientSpawnRejectionReason.CellBudgetReached,
                AmbientSpawnRejectionReason.NeighborTooClose,
                AmbientSpawnRejectionReason.RuntimeNotReady
            };

            foreach (var reason in structural)
            {
                Assert.AreEqual(AmbientSpawnRejectionKind.Structural, AmbientSpawnRejection.KindOf(reason));
                Assert.IsFalse(AmbientSpawnRejection.IsRetryable(reason), $"{reason} must not be retryable");
            }

            foreach (var reason in transient)
            {
                Assert.AreEqual(AmbientSpawnRejectionKind.Transient, AmbientSpawnRejection.KindOf(reason));
                Assert.IsTrue(AmbientSpawnRejection.IsRetryable(reason), $"{reason} must be retryable");
            }
        }

        #endregion

        #region Validator

        private sealed class FakeTerrainSampler : ITerrainSampler
        {
            public float Slope = 5f;
            public float WorldSize => 1024f;
            public float TerrainHeight => 150f;
            public float SampleHeight(float x, float z) => 10f;
            public float SampleSlope(float x, float z) => Slope;
            public bool IsWithinBounds(float x, float z) => x >= 0f && x <= 1024f && z >= 0f && z <= 1024f;
        }

        private static bool AcceptNavMesh(Vector3 p, float d, out Vector3 r) { r = p; return true; }
        private static bool RejectNavMesh(Vector3 p, float d, out Vector3 r) { r = default; return false; }

        private static AmbientPopulationPlan PlanAt(params Vector3[] candidates)
        {
            return new AmbientPopulationPlan("ambient_v1_test", "walker", new Vector2Int(0, 0), 0, 1, 1234, candidates);
        }

        private AmbientSpawnValidationContext CreateValidationContext(
            AmbientPopulationConfig config, NavigationSampleDelegate navMesh = null,
            Vector3? playerPosition = null, FakeTerrainSampler sampler = null)
        {
            return new AmbientSpawnValidationContext
            {
                Progression = CreateProgression(),
                TerrainSampler = sampler ?? new FakeTerrainSampler(),
                SampleNavMesh = navMesh ?? AcceptNavMesh,
                Registry = new AmbientPopulationRegistry(),
                Config = config,
                PlayerPosition = playerPosition
            };
        }

        // A comfortable mid-world candidate (Wildlands, far from refuge).
        private static readonly Vector3 GoodCandidate = DiscCenter + new Vector3(120f, 0f, 0f);

        [Test]
        public void Validator_OutsideDisc_IsStructural()
        {
            var definition = CreateDefinition("walker", maximumRing: WorldRing.Void);
            var config = CreateConfig(definition);

            var result = AmbientSpawnValidator.Validate(
                PlanAt(DiscCenter + new Vector3(WORLD_RADIUS * 1.5f, 0f, 0f)), definition,
                CreateValidationContext(config));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(AmbientSpawnRejectionReason.OutsideWorldDisc, result.RejectionReason);
            Assert.AreEqual(AmbientSpawnRejectionKind.Structural, result.RejectionKind);
        }

        [Test]
        public void Validator_RingWindow_IsStructural()
        {
            var definition = CreateDefinition("walker", minimumRing: WorldRing.Farlands);
            var config = CreateConfig(definition);

            // Wildlands candidate, Farlands-only definition.
            var result = AmbientSpawnValidator.Validate(
                PlanAt(GoodCandidate), definition, CreateValidationContext(config));

            Assert.AreEqual(AmbientSpawnRejectionReason.RingIncompatible, result.RejectionReason);
        }

        [Test]
        public void Validator_Slope_IsStructural()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);
            var sampler = new FakeTerrainSampler { Slope = 50f };

            var result = AmbientSpawnValidator.Validate(
                PlanAt(GoodCandidate), definition, CreateValidationContext(config, sampler: sampler));

            Assert.AreEqual(AmbientSpawnRejectionReason.SlopeInvalid, result.RejectionReason);
        }

        [Test]
        public void Validator_NoNavMesh_IsStructural()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);

            var result = AmbientSpawnValidator.Validate(
                PlanAt(GoodCandidate), definition, CreateValidationContext(config, RejectNavMesh));

            Assert.AreEqual(AmbientSpawnRejectionReason.NoNavMesh, result.RejectionReason);
        }

        [Test]
        public void Validator_PlayerTooClose_IsTransient()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);

            var result = AmbientSpawnValidator.Validate(
                PlanAt(GoodCandidate), definition,
                CreateValidationContext(config, playerPosition: GoodCandidate + new Vector3(5f, 0f, 0f)));

            Assert.AreEqual(AmbientSpawnRejectionReason.PlayerTooClose, result.RejectionReason);
            Assert.AreEqual(AmbientSpawnRejectionKind.Transient, result.RejectionKind);
        }

        [Test]
        public void Validator_LaterCandidate_CanSucceed()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);

            // First candidate blocked by the player; second far away and valid.
            var plan = PlanAt(GoodCandidate, GoodCandidate + new Vector3(0f, 0f, 80f));
            var result = AmbientSpawnValidator.Validate(
                plan, definition, CreateValidationContext(config, playerPosition: GoodCandidate + new Vector3(3f, 0f, 0f)));

            Assert.IsTrue(result.IsValid, "A single blocked candidate must never condemn the intention");
            Assert.AreEqual(plan.CandidatePositions[1], result.RequestedPosition);
        }

        [Test]
        public void Validator_MixedRejections_StayTransient()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);

            // Candidate 1: transient (player). Candidate 2: structural (outside disc).
            var plan = PlanAt(GoodCandidate, DiscCenter + new Vector3(WORLD_RADIUS * 2f, 0f, 0f));
            var result = AmbientSpawnValidator.Validate(
                plan, definition, CreateValidationContext(config, playerPosition: GoodCandidate + new Vector3(3f, 0f, 0f)));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(AmbientSpawnRejectionKind.Transient, result.RejectionKind,
                "Any transient blocker keeps the intention alive - never exhaust the cell");
        }

        [Test]
        public void Validator_AuthoredExclusion_IsConfigurablePerCategory()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);

            var context = CreateValidationContext(config);
            context.EncounterPositions = new List<Vector3> { GoodCandidate + new Vector3(10f, 0f, 0f) };
            context.ResourcePositions = new List<Vector3> { GoodCandidate + new Vector3(-10f, 0f, 0f) };

            var nearEncounter = AmbientSpawnValidator.Validate(PlanAt(GoodCandidate), definition, context);
            Assert.AreEqual(AmbientSpawnRejectionReason.TooCloseToAuthoredContent, nearEncounter.RejectionReason,
                "Encounters exclude ambient life by default");

            context.EncounterPositions = null;
            var nearResource = AmbientSpawnValidator.Validate(PlanAt(GoodCandidate), definition, context);
            Assert.IsTrue(nearResource.IsValid,
                "A small resource must not empty the area: resource exclusion is off by default");
        }

        [Test]
        public void Validator_NeighborTooClose_IsTransient()
        {
            var definition = CreateDefinition("walker");
            var config = CreateConfig(definition);
            var context = CreateValidationContext(config);

            var record = NewPlannedRecord(context.Registry, new Vector2Int(0, 0));
            context.Registry.RegisterSpawned(record,
                new FakeInstance("ambient_v1_other_m0", "walker",
                    AmbientPopulationCells.WorldToCell(GoodCandidate, DiscCenter, CELL_SIZE),
                    GoodCandidate + new Vector3(4f, 0f, 0f)));

            var result = AmbientSpawnValidator.Validate(PlanAt(GoodCandidate), definition, context);

            Assert.AreEqual(AmbientSpawnRejectionReason.NeighborTooClose, result.RejectionReason);
            Assert.AreEqual(AmbientSpawnRejectionKind.Transient, result.RejectionKind);
        }

        [Test]
        public void Frustum_PointInsideAllPlanes_IsRejectedOnlyThen()
        {
            // Inward-facing planes around the origin: a unit box frustum stand-in.
            var planes = new[]
            {
                new Plane(Vector3.right, Vector3.left * 2f),
                new Plane(Vector3.left, Vector3.right * 2f),
                new Plane(Vector3.forward, Vector3.back * 2f),
                new Plane(Vector3.back, Vector3.forward * 2f),
                new Plane(Vector3.up, Vector3.down * 2f),
                new Plane(Vector3.down, Vector3.up * 2f)
            };

            Assert.IsTrue(AmbientSpawnValidator.IsInsideFrustum(Vector3.zero, planes));
            Assert.IsFalse(AmbientSpawnValidator.IsInsideFrustum(new Vector3(5f, 0f, 0f), planes));
        }

        #endregion

        #region Registry

        private sealed class FakeInstance : IAmbientInstance
        {
            public string MemberId { get; }
            public string PopulationId { get; }
            public Vector2Int Cell { get; }
            public Vector3 Position { get; }

            public FakeInstance(string memberId, string populationId, Vector2Int cell, Vector3 position)
            {
                MemberId = memberId;
                PopulationId = populationId;
                Cell = cell;
                Position = position;
            }
        }

        private static AmbientPopulationRegistry.PlanRecord NewPlannedRecord(
            AmbientPopulationRegistry registry, Vector2Int cell, int groupSize = 2)
        {
            var plan = new AmbientPopulationPlan(
                AmbientPopulationIds.PlanId(cell, 0), "walker", cell, 0, groupSize, 99,
                new[] { Vector3.zero });
            registry.StoreCellPlans(cell, new[] { plan });
            return registry.GetCellPlans(cell)[0];
        }

        [Test]
        public void PlannedCell_IsNeverReRolled()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(1, 1);

            var planA = new AmbientPopulationPlan("ambient_v1_a", "walker", cell, 0, 1, 1, new[] { Vector3.zero });
            var planB = new AmbientPopulationPlan("ambient_v1_b", "walker", cell, 0, 1, 2, new[] { Vector3.one });

            registry.StoreCellPlans(cell, new[] { planA });
            registry.StoreCellPlans(cell, new[] { planB }); // must be a no-op

            Assert.IsTrue(registry.IsCellPlanned(cell));
            Assert.AreEqual("ambient_v1_a", registry.GetCellPlans(cell)[0].Plan.PlanId,
                "Re-planning an already planned cell must never replace its intentions");
        }

        [Test]
        public void RegisterAndCounts_TrackGlobalDefinitionAndCell()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell);

            registry.RegisterSpawned(record, new FakeInstance(
                AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell, Vector3.zero));

            Assert.AreEqual(1, registry.TotalAlive);
            Assert.AreEqual(1, registry.AliveCount("walker"));
            Assert.AreEqual(1, registry.AliveInCell(cell));
            Assert.AreEqual(AmbientPlanState.Alive, record.State);

            // Duplicate member id is guarded.
            registry.RegisterSpawned(record, new FakeInstance(
                AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell, Vector3.one));
            Assert.AreEqual(1, registry.TotalAlive, "Duplicate registration must be ignored");
        }

        [Test]
        public void Despawn_KeepsSnapshot_AndReturnsPlanToPending()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell, groupSize: 1);
            var instance = new FakeInstance(
                AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell, Vector3.zero);

            registry.RegisterSpawned(record, instance);
            registry.UnregisterDespawned(record, instance, new AmbientInstanceSnapshot(instance.MemberId, 0.4f, false));

            Assert.AreEqual(0, registry.TotalAlive);
            Assert.AreEqual(AmbientPlanState.Pending, record.State, "The intention survives the streaming despawn");
            Assert.IsTrue(registry.TryGetSnapshot(instance.MemberId, out var snapshot));
            Assert.AreEqual(0.4f, snapshot.NormalizedHealth, 0.0001f, "The world remembers the wound");
        }

        [Test]
        public void Death_MarksDefeated_AndIsNeverRecreated()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell, groupSize: 1);
            string memberId = AmbientPopulationIds.MemberId(record.Plan.PlanId, 0);
            var instance = new FakeInstance(memberId, "walker", cell, Vector3.zero);

            registry.RegisterSpawned(record, instance);
            registry.MarkMemberDead(record, instance);

            Assert.AreEqual(AmbientPlanState.Defeated, record.State);
            Assert.IsTrue(registry.IsMemberDead(record, memberId));
            Assert.AreEqual(1, registry.TotalDeaths);
        }

        [Test]
        public void PartialGroupDeath_KeepsThePlanAlive_ButRemembersTheDead()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell, groupSize: 2);
            var first = new FakeInstance(AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell, Vector3.zero);
            var second = new FakeInstance(AmbientPopulationIds.MemberId(record.Plan.PlanId, 1), "walker", cell, Vector3.one);

            registry.RegisterSpawned(record, first);
            registry.RegisterSpawned(record, second);
            registry.MarkMemberDead(record, first);

            Assert.AreEqual(AmbientPlanState.Alive, record.State, "One survivor keeps the presence alive");
            Assert.IsTrue(registry.IsMemberDead(record, first.MemberId), "The dead member must stay dead");
        }

        [Test]
        public void Clear_PurgesEverything()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell);
            var instance = new FakeInstance(AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell, Vector3.zero);
            registry.RegisterSpawned(record, instance);
            registry.UnregisterDespawned(record, instance, new AmbientInstanceSnapshot(instance.MemberId, 0.5f, false));

            registry.Clear();

            Assert.AreEqual(0, registry.TotalAlive);
            Assert.AreEqual(0, registry.PlannedCellCount);
            Assert.IsFalse(registry.IsCellPlanned(cell));
            Assert.IsFalse(registry.TryGetSnapshot(instance.MemberId, out _), "No memory survives a regeneration");
        }

        [Test]
        public void NeighborQuery_FindsOnlyNearbyInstances()
        {
            var registry = new AmbientPopulationRegistry();
            var cell = new Vector2Int(0, 0);
            var record = NewPlannedRecord(registry, cell);
            registry.RegisterSpawned(record, new FakeInstance(
                AmbientPopulationIds.MemberId(record.Plan.PlanId, 0), "walker", cell,
                DiscCenter + new Vector3(20f, 0f, 20f)));

            Assert.IsTrue(registry.IsAnyAliveWithin(DiscCenter + new Vector3(25f, 0f, 20f), 10f, DiscCenter, CELL_SIZE));
            Assert.IsFalse(registry.IsAnyAliveWithin(DiscCenter + new Vector3(200f, 0f, 20f), 10f, DiscCenter, CELL_SIZE));
        }

        #endregion
    }
}
