using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World.Landmarks;
using Lootbound.Gameplay.World.Layout;
using Lootbound.Gameplay.World.Progression;
using Lootbound.Gameplay.World.Spawning;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Pure tests for the landmark planner: determinism, stable ordering,
    /// defensive registry handling, ring compatibility, weighted selection,
    /// grounded positions, host derivation and identity format.
    /// </summary>
    public class LandmarkPlannerTests
    {
        private const float EPSILON = 0.001f;
        private const float WorldSize = 1024f;
        private const float DiscRadius = 512f;
        private const float GroundHeight = 10f;

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in created)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            created.Clear();
        }

        #region Rig

        /// <summary>Flat, permissive sampler: layout generation always succeeds.</summary>
        private sealed class FlatSampler : ITerrainSampler
        {
            public float SampleHeight(float worldX, float worldZ) => GroundHeight;
            public float SampleSlope(float worldX, float worldZ) => 0f;
            public bool IsWithinBounds(float worldX, float worldZ) =>
                worldX >= 0f && worldX <= WorldSize && worldZ >= 0f && worldZ <= WorldSize;
            public float WorldSize => LandmarkPlannerTests.WorldSize;
            public UnityEngine.Vector3 WorldCenter => new UnityEngine.Vector3(WorldSize * 0.5f, 0f, WorldSize * 0.5f);
            public float TerrainHeight => 150f;
        }

        private static void SetField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private WorldLayoutConfig CreateLayoutConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldLayoutConfig>();
            created.Add(config);
            SetField(config, "maxGenerationAttempts", 10);
            SetField(config, "minimumRadialPathCount", 3);
            SetField(config, "maximumRadialPathCount", 4);
            SetField(config, "nodesPerRadialPath", 4);
            SetField(config, "radialStepMin", 60f);
            SetField(config, "radialStepMax", 90f);
            SetField(config, "radialPathMaxSlope", 40f);
            SetField(config, "edgeSamplePoints", 3);
            SetField(config, "branchCount", 2);
            SetField(config, "branchMaxNodes", 2);
            SetField(config, "branchChance", 0.5f);
            SetField(config, "encounterReservationCount", 3);
            SetField(config, "resourceReservationCount", 2);
            SetField(config, "nodeMinSpacing", 40f);
            SetField(config, "candidatesPerStep", 16);
            SetField(config, "outwardProgressionWeight", 30f);
            SetField(config, "terrainSlopeWeight", 25f);
            SetField(config, "curvaturePenaltyWeight", 15f);
            SetField(config, "corridorWidth", 8f);
            SetField(config, "corridorBlend", 12f);
            SetField(config, "maxCorrectionStrength", 0.25f);
            SetField(config, "refugeFlattenRadius", 20f);
            SetField(config, "clearingFlattenRadius", 12f);
            SetField(config, "refugeMaxCenterOffset", 30f);
            SetField(config, "minimumAngularSeparation", 45f);
            SetField(config, "maxAngularGap", 120f);
            SetField(config, "junctionRadius", 8f);
            SetField(config, "clearingRadius", 15f);
            SetField(config, "viewpointRadius", 6f);
            SetField(config, "refugeRadius", 24f);
            SetField(config, "outerDestinationRadius", 20f);
            return config;
        }

        private WorldLayoutContext GenerateLayout(int seed, out WorldProgression progression, out FlatSampler sampler)
        {
            var config = CreateLayoutConfig();
            var ringConfig = WorldRingConfig.CreateDefault();
            var disc = new WorldDiscDefinition(DiscRadius, ringConfig);
            sampler = new FlatSampler();

            var result = WorldLayoutGenerator.Generate(seed, disc, sampler, config);
            Assert.IsTrue(result.Success, $"Layout generation must succeed. Error: {result.Error}");

            progression = new WorldProgression(result.Layout.RefugePosition, DiscRadius, ringConfig);
            result.Layout.AttachProgression(progression);
            return result.Layout;
        }

        private LandmarkDefinition CreateDefinition(
            string id, WorldRing min = WorldRing.Refuge, WorldRing max = WorldRing.Void,
            float weight = 1f, float discoveryRadius = 100f, GameObject prefab = null)
        {
            var definition = ScriptableObject.CreateInstance<LandmarkDefinition>();
            created.Add(definition);
            SetField(definition, "landmarkId", id);
            SetField(definition, "minimumRing", min);
            SetField(definition, "maximumRing", max);
            SetField(definition, "selectionWeight", weight);
            SetField(definition, "discoveryRadius", discoveryRadius);
            SetField(definition, "landmarkPrefab", prefab);
            return definition;
        }

        private LandmarkRegistry CreateRegistry(params LandmarkDefinition[] definitions)
        {
            var registry = ScriptableObject.CreateInstance<LandmarkRegistry>();
            created.Add(registry);
            SetField(registry, "definitions", new List<LandmarkDefinition>(definitions));
            return registry;
        }

        #endregion

        [Test]
        public void SamePlan_IsDeterministic()
        {
            var layout = GenerateLayout(4242, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("ruin"), CreateDefinition("shrine"));

            var a = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            var b = LandmarkPlanner.Plan(layout, registry, progression, sampler);

            Assert.AreEqual(a.Count, b.Count);
            Assert.Greater(a.Count, 0, "the layout must yield at least one landmark (OuterDestinations exist)");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].LandmarkId, b[i].LandmarkId);
                Assert.AreEqual(a[i].DefinitionId, b[i].DefinitionId);
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Ring, b[i].Ring);
                Assert.AreEqual(a[i].Depth01, b[i].Depth01, EPSILON);
                Assert.AreEqual(a[i].Difficulty01, b[i].Difficulty01, EPSILON);
                Assert.AreEqual(a[i].DiscoveryRadius, b[i].DiscoveryRadius, EPSILON);
            }
        }

        [Test]
        public void Result_IsOrderedByLandmarkIdOrdinal()
        {
            var layout = GenerateLayout(7, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("tower"));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);

            for (int i = 1; i < landmarks.Count; i++)
            {
                Assert.LessOrEqual(
                    string.CompareOrdinal(landmarks[i - 1].LandmarkId, landmarks[i].LandmarkId), 0,
                    "landmarks must be ordered by LandmarkId (ordinal)");
            }
        }

        [Test]
        public void NullOrEmptyRegistry_ProducesNoLandmarks()
        {
            var layout = GenerateLayout(11, out var progression, out var sampler);

            Assert.AreEqual(0, LandmarkPlanner.Plan(layout, null, progression, sampler).Count);
            Assert.AreEqual(0, LandmarkPlanner.Plan(layout, CreateRegistry(), progression, sampler).Count);
        }

        [Test]
        public void IncompatibleRing_ExcludesLandmarks()
        {
            var layout = GenerateLayout(21, out var progression, out var sampler);

            // OuterDestinations sit in the outer rings; a Refuge-only window
            // matches nothing.
            var registry = CreateRegistry(CreateDefinition("refuge_only",
                min: WorldRing.Refuge, max: WorldRing.Refuge));

            Assert.AreEqual(0, LandmarkPlanner.Plan(layout, registry, progression, sampler).Count);
        }

        [Test]
        public void WeightedSelection_IsDeterministicBySeed()
        {
            var layout = GenerateLayout(99, out var progression, out var sampler);
            var registry = CreateRegistry(
                CreateDefinition("heavy", weight: 5f),
                CreateDefinition("light", weight: 1f));

            var first = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            var second = LandmarkPlanner.Plan(layout, registry, progression, sampler);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].DefinitionId, second[i].DefinitionId,
                    "the same seed must select the same definition per landmark");
            }
        }

        [Test]
        public void LandmarkId_FollowsStableFormat()
        {
            var layout = GenerateLayout(33, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("ruin"));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);

            foreach (var landmark in landmarks)
            {
                Assert.AreEqual(
                    $"landmark_{layout.WorldSeed}_{landmark.HostNodeId}_{landmark.Slot}",
                    landmark.LandmarkId);
                Assert.AreEqual(0, landmark.Slot, "V1 uses slot 0");
            }
        }

        [Test]
        public void DiscoveryRadius_ComesFromDefinition()
        {
            var layout = GenerateLayout(45, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("windmill", discoveryRadius: 175f));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);
            foreach (var landmark in landmarks)
            {
                Assert.AreEqual(175f, landmark.DiscoveryRadius, EPSILON);
            }
        }

        [Test]
        public void Position_IsGroundedBySampler()
        {
            var layout = GenerateLayout(51, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("ruin"));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);
            foreach (var landmark in landmarks)
            {
                Assert.AreEqual(GroundHeight, landmark.Position.y, EPSILON,
                    "the landmark height must be sampled from the terrain");
            }
        }

        [Test]
        public void RingDepthDifficulty_MatchProgression()
        {
            var layout = GenerateLayout(63, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("ruin"));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);
            foreach (var landmark in landmarks)
            {
                var ctx = progression.GetContext(landmark.Position);
                Assert.AreEqual(ctx.Ring, landmark.Ring);
                Assert.AreEqual(ctx.Depth01, landmark.Depth01, EPSILON);
                Assert.AreEqual(ctx.Difficulty01, landmark.Difficulty01, EPSILON);
            }
        }

        [Test]
        public void Hosts_AreElevatedOrTerminalNodes()
        {
            var layout = GenerateLayout(72, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("ruin"));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);
            foreach (var landmark in landmarks)
            {
                Assert.IsTrue(layout.NodesById.TryGetValue(landmark.HostNodeId, out var host),
                    "every landmark must reference a real host node");
                Assert.IsTrue(
                    host.Type == WorldNodeType.Viewpoint ||
                    host.Type == WorldNodeType.Landmark ||
                    host.Type == WorldNodeType.OuterDestination,
                    $"host {host.NodeId} of type {host.Type} is not an eligible landmark host");
            }
        }

        [Test]
        public void NullPrefabDefinition_IsStillPlanned()
        {
            // The prefab is a presentation concern; a definition without one
            // still produces a landmark identity (the presenter decides what
            // to show).
            var layout = GenerateLayout(84, out var progression, out var sampler);
            var registry = CreateRegistry(CreateDefinition("prefabless", prefab: null));

            var landmarks = LandmarkPlanner.Plan(layout, registry, progression, sampler);
            Assert.Greater(landmarks.Count, 0);
            foreach (var landmark in landmarks)
            {
                Assert.AreEqual("prefabless", landmark.DefinitionId);
            }
        }
    }
}
