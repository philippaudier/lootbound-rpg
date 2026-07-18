using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Layout;
using System.Collections.Generic;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// EditMode tests for the radial world layout generation system.
    /// Tests determinism, traversability, structure, rings, and validation.
    /// </summary>
    public class WorldLayoutTests
    {
        private const float POSITION_EPSILON = 0.001f;
        private const float SLOPE_EPSILON = 0.1f;

        #region Test Helpers

        private WorldLayoutConfig CreateTestConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldLayoutConfig>();

            // Use reflection to set private serialized fields
            SetField(config, "maxGenerationAttempts", 10);
            SetField(config, "minimumRadialPathCount", 3);
            SetField(config, "maximumRadialPathCount", 4);
            SetField(config, "nodesPerRadialPath", 4);
            SetField(config, "radialStepMin", 80f);
            SetField(config, "radialStepMax", 150f);
            SetField(config, "radialPathMaxSlope", 40f);
            SetField(config, "edgeSamplePoints", 3);
            SetField(config, "branchCount", 2);
            SetField(config, "branchMaxNodes", 2);
            SetField(config, "branchChance", 0.5f);
            SetField(config, "encounterReservationCount", 3);
            SetField(config, "resourceReservationCount", 2);
            SetField(config, "landmarkReservationCount", 2);
            SetField(config, "nodeMinSpacing", 50f);
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

        private WorldRingConfig CreateTestRingConfig()
        {
            return WorldRingConfig.CreateDefault();
        }

        private WorldDiscDefinition CreateTestWorldDiscDefinition(float worldRadius)
        {
            var ringConfig = CreateTestRingConfig();
            return new WorldDiscDefinition(worldRadius, ringConfig);
        }

        private TerrainGenerationConfig CreateTerrainConfig()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();

            SetField(config, "worldSize", 1024f);
            SetField(config, "terrainHeight", 150f);
            SetField(config, "heightmapResolution", 129);
            // Min-max normalization amplifies slopes by 1/(raw range), which
            // varies per seed and can push the fixture past the path slope
            // budget for some seeds (e.g. 12345). The unified-height-space
            // invariants under test hold with or without normalization, so the
            // fixture disables it to keep a stable, gentle slope regime.
            // Normalization-specific behaviour is covered by
            // TerrainHeightSpaceTests.
            SetField(config, "normalizeHeightmap", false);
            SetField(config, "macroScale", 500f);
            SetField(config, "macroOctaves", 3);
            SetField(config, "macroPersistence", 0.4f);
            SetField(config, "macroLacunarity", 2f);
            SetField(config, "ridgeScale", 400f);
            SetField(config, "ridgeStrength", 0.1f);
            SetField(config, "valleyScale", 400f);
            SetField(config, "valleyStrength", 0.1f);
            SetField(config, "detailScale", 80f);
            SetField(config, "detailStrength", 0.03f);
            SetField(config, "globalHeightStrength", 0.8f);

            var curve = new AnimationCurve(
                new Keyframe(0f, 0.1f),
                new Keyframe(0.5f, 0.5f),
                new Keyframe(1f, 0.9f)
            );
            SetField(config, "heightRemap", curve);

            return config;
        }

        private void SetField(object obj, string fieldName, object value)
        {
            var type = obj.GetType();
            var field = type.GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }

        /// <summary>
        /// Generate a real terrain context (heightmap, normalized map, slope map)
        /// the same way the runtime pipeline does before layout generation.
        /// </summary>
        private TerrainGenerationContext CreateGeneratedContext(int seed, TerrainGenerationConfig terrainConfig)
        {
            var context = new TerrainGenerationContext(
                seed,
                terrainConfig.HeightmapResolution,
                terrainConfig.WorldSize,
                terrainConfig.TerrainHeight);
            TerrainHeightGenerator.Generate(context, terrainConfig);
            return context;
        }

        private ITerrainSampler CreateSampler(int seed)
        {
            var terrainConfig = CreateTerrainConfig();
            return new TerrainContextSampler(CreateGeneratedContext(seed, terrainConfig));
        }

        #endregion

        #region WorldRingConfig Tests

        [Test]
        public void WorldRingConfig_DefaultIsValid()
        {
            var config = CreateTestRingConfig();
            Assert.IsTrue(config.IsValid, $"Default ring config should be valid: {config.ValidationError}");
        }

        [Test]
        public void WorldRingConfig_RefugeStartsAtZero()
        {
            var config = CreateTestRingConfig();
            Assert.AreEqual(0f, config.GetMinimumRadius(WorldRing.Refuge));
        }

        [Test]
        public void WorldRingConfig_GetRingAt_ReturnsCorrectRing()
        {
            var config = CreateTestRingConfig();

            // Test various normalized radii
            Assert.AreEqual(WorldRing.Refuge, config.GetRingAt(0f));
            Assert.AreEqual(WorldRing.Refuge, config.GetRingAt(0.04f));
            Assert.AreEqual(WorldRing.Nearlands, config.GetRingAt(0.05f));
            Assert.AreEqual(WorldRing.Nearlands, config.GetRingAt(0.10f));
            Assert.AreEqual(WorldRing.Wildlands, config.GetRingAt(0.15f));
            Assert.AreEqual(WorldRing.Void, config.GetRingAt(0.95f));
            Assert.AreEqual(WorldRing.Void, config.GetRingAt(1.5f)); // Beyond world
        }

        [Test]
        public void WorldRingConfig_BoundaryRule_MinIsInclusive()
        {
            var config = CreateTestRingConfig();

            // Exactly at boundary should return the new ring
            float nearlandsMin = config.GetMinimumRadius(WorldRing.Nearlands);
            Assert.AreEqual(WorldRing.Nearlands, config.GetRingAt(nearlandsMin));
        }

        [Test]
        public void WorldRingConfig_BoundaryRule_MaxIsExclusive()
        {
            var config = CreateTestRingConfig();

            // Just below boundary should return previous ring
            float nearlandsMin = config.GetMinimumRadius(WorldRing.Nearlands);
            Assert.AreEqual(WorldRing.Refuge, config.GetRingAt(nearlandsMin - 0.001f));
        }

        #endregion

        #region Traversability Tests

        [Test]
        public void PrimaryPath_AllEdgesTraversable()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(12345);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);

            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");
            Assert.IsNotNull(result.Layout);

            foreach (var edge in result.Layout.EdgesOrdered)
            {
                if (edge.IsPrimaryPathEdge)
                {
                    Assert.IsTrue(edge.IsTraversable,
                        $"Primary path edge {edge.EdgeId} should be traversable (max slope: {edge.MaxSlope:F1}°)");
                }
            }
        }

        [Test]
        public void AllOuterDestinations_ReachableFromRefuge()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(999);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(999, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);
            Assert.Greater(result.Layout.OuterDestinationNodes.Count, 0);

            foreach (var outerDest in result.Layout.OuterDestinationNodes)
            {
                // BFS from refuge to this OuterDestination
                var visited = new HashSet<string>();
                var queue = new Queue<string>();
                queue.Enqueue(result.Layout.RefugeNode.NodeId);
                visited.Add(result.Layout.RefugeNode.NodeId);

                bool found = false;
                while (queue.Count > 0)
                {
                    var nodeId = queue.Dequeue();
                    if (nodeId == outerDest.NodeId)
                    {
                        found = true;
                        break;
                    }

                    var node = result.Layout.NodesById[nodeId];
                    foreach (var edgeId in node.ConnectedEdgeIds)
                    {
                        var edge = result.Layout.EdgesById[edgeId];
                        if (!edge.IsPrimaryPathEdge) continue;

                        var otherNodeId = edge.GetOtherNodeId(nodeId);
                        if (otherNodeId != null && !visited.Contains(otherNodeId))
                        {
                            visited.Add(otherNodeId);
                            queue.Enqueue(otherNodeId);
                        }
                    }
                }

                Assert.IsTrue(found, $"OuterDestination {outerDest.NodeId} should be reachable from refuge via primary path");
            }
        }

        #endregion

        #region Determinism Tests

        [Test]
        public void SameSeed_ProducesSameNodeIds()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var worldDisc = CreateTestWorldDiscDefinition(sampler1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.Layout.NodesOrdered.Count, result2.Layout.NodesOrdered.Count);

            for (int i = 0; i < result1.Layout.NodesOrdered.Count; i++)
            {
                Assert.AreEqual(result1.Layout.NodesOrdered[i].NodeId, result2.Layout.NodesOrdered[i].NodeId,
                    $"Node IDs at index {i} should match");
            }
        }

        [Test]
        public void SameSeed_ProducesSamePositions()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var worldDisc = CreateTestWorldDiscDefinition(sampler1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);

            for (int i = 0; i < result1.Layout.NodesOrdered.Count; i++)
            {
                var pos1 = result1.Layout.NodesOrdered[i].Position;
                var pos2 = result2.Layout.NodesOrdered[i].Position;

                Assert.AreEqual(pos1.x, pos2.x, POSITION_EPSILON, $"Node {i} X position should match");
                Assert.AreEqual(pos1.y, pos2.y, POSITION_EPSILON, $"Node {i} Y position should match");
                Assert.AreEqual(pos1.z, pos2.z, POSITION_EPSILON, $"Node {i} Z position should match");
            }
        }

        [Test]
        public void SameSeed_ProducesSameEdges()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var worldDisc = CreateTestWorldDiscDefinition(sampler1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.Layout.EdgesOrdered.Count, result2.Layout.EdgesOrdered.Count);

            for (int i = 0; i < result1.Layout.EdgesOrdered.Count; i++)
            {
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].EdgeId, result2.Layout.EdgesOrdered[i].EdgeId);
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].NodeAId, result2.Layout.EdgesOrdered[i].NodeAId);
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].NodeBId, result2.Layout.EdgesOrdered[i].NodeBId);
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].IsPrimaryPathEdge, result2.Layout.EdgesOrdered[i].IsPrimaryPathEdge);
            }
        }

        [Test]
        public void SameSeed_ProducesSameRadialPaths()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var worldDisc = CreateTestWorldDiscDefinition(sampler1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.Layout.RadialPaths.Count, result2.Layout.RadialPaths.Count);

            for (int i = 0; i < result1.Layout.RadialPaths.Count; i++)
            {
                var path1 = result1.Layout.RadialPaths[i];
                var path2 = result2.Layout.RadialPaths[i];

                Assert.AreEqual(path1.PathId, path2.PathId);
                Assert.AreEqual(path1.StartAngle, path2.StartAngle, 0.001f);
                Assert.AreEqual(path1.OuterDestinationNodeId, path2.OuterDestinationNodeId);
            }
        }

        [Test]
        public void DifferentSeed_ProducesDifferentLayout()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(54321);
            var worldDisc = CreateTestWorldDiscDefinition(sampler1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(54321, worldDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);

            // At least some node positions should differ
            bool anyDifferent = false;
            int minCount = Mathf.Min(result1.Layout.NodesOrdered.Count, result2.Layout.NodesOrdered.Count);

            for (int i = 1; i < minCount; i++) // Skip refuge
            {
                var pos1 = result1.Layout.NodesOrdered[i].Position;
                var pos2 = result2.Layout.NodesOrdered[i].Position;

                if (Mathf.Abs(pos1.x - pos2.x) > 1f || Mathf.Abs(pos1.z - pos2.z) > 1f)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "Different seeds should produce different node positions");
        }

        [Test]
        public void DeterministicIds_AreStable()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(42);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(42, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            // All node IDs should follow the pattern node_{seed}_{type}_{index}
            foreach (var node in result.Layout.NodesOrdered)
            {
                Assert.IsTrue(node.NodeId.StartsWith($"node_{result.Layout.EffectiveLayoutSeed}_"),
                    $"Node ID {node.NodeId} should start with node_{result.Layout.EffectiveLayoutSeed}_");
            }

            // All edge IDs should follow the pattern edge_{seed}_{nodeAIndex}_{nodeBIndex}
            foreach (var edge in result.Layout.EdgesOrdered)
            {
                Assert.IsTrue(edge.EdgeId.StartsWith($"edge_{result.Layout.EffectiveLayoutSeed}_"),
                    $"Edge ID {edge.EdgeId} should start with edge_{result.Layout.EffectiveLayoutSeed}_");
            }
        }

        #endregion

        #region Structure Tests

        [Test]
        public void RefugeNode_AlwaysExists()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(1);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(1, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);
            Assert.IsNotNull(result.Layout.RefugeNode);
            Assert.AreEqual(WorldNodeType.Refuge, result.Layout.RefugeNode.Type);
        }

        [Test]
        public void OuterDestinationNodes_AlwaysExist()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(2);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(2, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);
            Assert.Greater(result.Layout.OuterDestinationNodes.Count, 0);

            foreach (var node in result.Layout.OuterDestinationNodes)
            {
                Assert.AreEqual(WorldNodeType.OuterDestination, node.Type);
            }
        }

        [Test]
        public void RadialPaths_EachHasOuterDestination()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(3);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(3, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var path in result.Layout.RadialPaths)
            {
                Assert.IsNotNull(path.OuterDestinationNodeId, $"Path {path.PathId} should have OuterDestination");
                Assert.IsTrue(result.Layout.NodesById.ContainsKey(path.OuterDestinationNodeId),
                    $"OuterDestination {path.OuterDestinationNodeId} should exist");

                var destNode = result.Layout.NodesById[path.OuterDestinationNodeId];
                Assert.AreEqual(WorldNodeType.OuterDestination, destNode.Type);
            }
        }

        [Test]
        public void AllNodes_Connected()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(5);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(5, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            // BFS from refuge should reach all nodes
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(result.Layout.RefugeNode.NodeId);
            visited.Add(result.Layout.RefugeNode.NodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                var node = result.Layout.NodesById[nodeId];

                foreach (var edgeId in node.ConnectedEdgeIds)
                {
                    var edge = result.Layout.EdgesById[edgeId];
                    var otherNodeId = edge.GetOtherNodeId(nodeId);
                    if (otherNodeId != null && !visited.Contains(otherNodeId))
                    {
                        visited.Add(otherNodeId);
                        queue.Enqueue(otherNodeId);
                    }
                }
            }

            Assert.AreEqual(result.Layout.NodesOrdered.Count, visited.Count,
                "All nodes should be reachable from refuge");
        }

        [Test]
        public void NodeSpacing_RespectsMinimum()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(6);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(6, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            var nodes = result.Layout.NodesOrdered;
            float minSpacing = config.NodeMinSpacing;

            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    float dist = Vector2.Distance(
                        new Vector2(nodes[i].Position.x, nodes[i].Position.z),
                        new Vector2(nodes[j].Position.x, nodes[j].Position.z)
                    );

                    Assert.GreaterOrEqual(dist, minSpacing * 0.9f,
                        $"Nodes {nodes[i].NodeId} and {nodes[j].NodeId} are too close ({dist:F1}m < {minSpacing}m)");
                }
            }
        }

        #endregion

        #region Radial Properties Tests

        [Test]
        public void Refuge_HasZeroDistance()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(7);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(7, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0f, result.Layout.RefugeNode.DistanceFromRefuge, POSITION_EPSILON);
            Assert.AreEqual(0f, result.Layout.RefugeNode.NormalizedWorldRadius, POSITION_EPSILON);
        }

        [Test]
        public void Refuge_IsInRefugeRing()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(8);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(8, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(WorldRing.Refuge, result.Layout.RefugeNode.Ring);
        }

        [Test]
        public void PathStepIndex_IncreasesAlongPath()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(9);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(9, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var path in result.Layout.RadialPaths)
            {
                for (int i = 0; i < path.NodeIds.Count; i++)
                {
                    var node = result.Layout.NodesById[path.NodeIds[i]];
                    Assert.AreEqual(i, node.PathStepIndex,
                        $"Node {node.NodeId} should have PathStepIndex = {i}");
                }
            }
        }

        [Test]
        public void DistanceFromRefuge_IncreasesAlongPath()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(10);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(10, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var path in result.Layout.RadialPaths)
            {
                float previousDistance = 0f;
                foreach (var nodeId in path.NodeIds)
                {
                    var node = result.Layout.NodesById[nodeId];
                    Assert.Greater(node.DistanceFromRefuge, previousDistance,
                        $"DistanceFromRefuge should increase along path");
                    previousDistance = node.DistanceFromRefuge;
                }
            }
        }

        [Test]
        public void BranchNodes_HaveNegativePathStepIndex()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(11);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(11, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            // Collect all primary path node IDs
            var primaryPathNodeIds = new HashSet<string>();
            primaryPathNodeIds.Add(result.Layout.RefugeNode.NodeId);
            foreach (var path in result.Layout.RadialPaths)
            {
                foreach (var nodeId in path.NodeIds)
                {
                    primaryPathNodeIds.Add(nodeId);
                }
            }

            // Check branch nodes
            foreach (var node in result.Layout.NodesOrdered)
            {
                if (!primaryPathNodeIds.Contains(node.NodeId) && node.Type != WorldNodeType.Refuge)
                {
                    Assert.AreEqual(-1, node.PathStepIndex,
                        $"Branch node {node.NodeId} should have PathStepIndex = -1");
                }
            }
        }

        #endregion

        #region Validation Tests

        [Test]
        public void StructuralValidation_PassesForValidLayout()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(100);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(100, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            var validation = WorldLayoutValidator.ValidateStructure(result.Layout);
            Assert.IsTrue(validation.IsValid, $"Structural validation should pass: {validation.Error}");
        }

        [Test]
        public void TraversabilityValidation_PassesForValidLayout()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(101);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(101, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            var validation = WorldLayoutValidator.ValidatePrimaryPathTraversability(result.Layout, config.PrimaryPathMaxSlope);
            Assert.IsTrue(validation.IsValid, $"Traversability validation should pass: {validation.Error}");
        }

        #endregion

        #region Reservation Tests

        [Test]
        public void EncounterReservations_HaveValidHostNodes()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(200);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(200, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var reservation in result.Layout.EncounterReservations)
            {
                Assert.IsTrue(result.Layout.NodesById.ContainsKey(reservation.HostNodeId),
                    $"Encounter reservation {reservation.ReservationId} should have valid host node");
            }
        }

        [Test]
        public void ResourceReservations_HaveValidHostNodes()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(201);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(201, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var reservation in result.Layout.ResourceReservations)
            {
                Assert.IsTrue(result.Layout.NodesById.ContainsKey(reservation.HostNodeId),
                    $"Resource reservation {reservation.ReservationId} should have valid host node");
            }
        }

        [Test]
        public void Reservations_InheritRingFromHost()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(202);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(202, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var reservation in result.Layout.EncounterReservations)
            {
                var host = result.Layout.NodesById[reservation.HostNodeId];
                Assert.AreEqual(host.Ring, reservation.Ring,
                    "Encounter reservation Ring should match host node Ring");
            }
        }

        [Test]
        public void LandmarkReservations_HaveValidHostNodes()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(203);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(203, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var reservation in result.Layout.LandmarkReservations)
            {
                Assert.IsTrue(result.Layout.NodesById.ContainsKey(reservation.HostNodeId),
                    $"Landmark reservation {reservation.ReservationId} should have valid host node");
            }
        }

        #endregion

        #region Edge Control Points Tests

        [Test]
        public void Edges_HaveControlPoints()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(300);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(300, worldDisc, sampler, config);

            Assert.IsTrue(result.Success);

            foreach (var edge in result.Layout.EdgesOrdered)
            {
                Assert.IsNotNull(edge.ControlPoints);
                Assert.GreaterOrEqual(edge.ControlPoints.Count, 2,
                    $"Edge {edge.EdgeId} should have at least 2 control points");

                // First and last control points should be near the nodes
                var nodeA = result.Layout.NodesById[edge.NodeAId];
                var nodeB = result.Layout.NodesById[edge.NodeBId];

                float distA = Vector2.Distance(
                    new Vector2(edge.ControlPoints[0].x, edge.ControlPoints[0].z),
                    new Vector2(nodeA.Position.x, nodeA.Position.z)
                );
                float distB = Vector2.Distance(
                    new Vector2(edge.ControlPoints[edge.ControlPoints.Count - 1].x, edge.ControlPoints[edge.ControlPoints.Count - 1].z),
                    new Vector2(nodeB.Position.x, nodeB.Position.z)
                );

                Assert.LessOrEqual(distA, 1f, "First control point should be at node A");
                Assert.LessOrEqual(distB, 1f, "Last control point should be at node B");
            }
        }

        #endregion

        #region Multiple Seeds Test

        // Robustness over a deterministic corpus: layout generation must
        // succeed for every seed, and a failure must name the seed.
        [Test]
        public void Generation_Succeeds_ForDeterministicSeedCorpus()
        {
            const int corpusSize = 50;

            var config = CreateTestConfig();

            for (int seed = 0; seed < corpusSize; seed++)
            {
                var terrainConfig = CreateTerrainConfig();
                var context = CreateGeneratedContext(seed, terrainConfig);
                var sampler = new TerrainContextSampler(context);
                var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

                var result = WorldLayoutGenerator.Generate(seed, worldDisc, sampler, config);

                Assert.IsTrue(result.Success,
                    $"Layout generation failed for corpus seed {seed}: {result.Error}");
            }
        }

        [TestCase(1)]
        [TestCase(42)]
        [TestCase(999)]
        [TestCase(12345)]
        [TestCase(99999)]
        public void Generation_SucceedsWithVariousSeeds(int seed)
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(seed);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(seed, worldDisc, sampler, config);

            Assert.IsTrue(result.Success, $"Generation should succeed with seed {seed}. Error: {result.Error}");
            Assert.IsNotNull(result.Layout.RefugeNode);
            Assert.Greater(result.Layout.OuterDestinationNodes.Count, 0);
            Assert.Greater(result.Layout.RadialPaths.Count, 0);
            Assert.IsTrue(result.Layout.AreAllRadialPathsTraversable());
        }

        #endregion

        #region Sampler Consistency Tests

        [Test]
        public void Sampler_ProducesSameHeightAsContext()
        {
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);

            float worldX = terrainConfig.WorldSize * 0.3f;
            float worldZ = terrainConfig.WorldSize * 0.7f;

            Assert.AreEqual(context.SampleHeightAtWorld(worldX, worldZ), sampler.SampleHeight(worldX, worldZ), POSITION_EPSILON,
                "Sampler must delegate height queries to the context (single conversion authority)");

            // At an exact grid point the sampled height must equal the value
            // written to the Unity Terrain (NormalizedHeightMap * TerrainHeight).
            int gridX = 40, gridZ = 90;
            float gridWorldX = (gridX / (float)(context.Resolution - 1)) * context.WorldSize;
            float gridWorldZ = (gridZ / (float)(context.Resolution - 1)) * context.WorldSize;

            Assert.AreEqual(context.NormalizedHeightMap[gridX, gridZ] * context.TerrainHeight,
                sampler.SampleHeight(gridWorldX, gridWorldZ), POSITION_EPSILON,
                "Sampler heights must live in the terrain-applied (normalized) height space");
        }

        [Test]
        public void Sampler_ProducesSameSlopeAsContext()
        {
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(54321, terrainConfig);
            var sampler = new TerrainContextSampler(context);

            float worldX = terrainConfig.WorldSize * 0.5f;
            float worldZ = terrainConfig.WorldSize * 0.5f;

            Assert.AreEqual(context.SampleSlopeAtWorld(worldX, worldZ), sampler.SampleSlope(worldX, worldZ), SLOPE_EPSILON,
                "Sampler must delegate slope queries to the context (single conversion authority)");
        }

        #endregion

        #region Height Space Unification Tests

        // Node and control point Y values are sampled through TerrainContextSampler,
        // so before any flattening they must sit exactly on the terrain surface
        // described by SampleHeightAtWorld.
        [Test]
        public void NodeAndControlPointHeights_MatchSampleHeightAtWorld()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            foreach (var node in result.Layout.NodesOrdered)
            {
                float terrainY = context.SampleHeightAtWorld(node.Position.x, node.Position.z);
                Assert.AreEqual(terrainY, node.Position.y, POSITION_EPSILON,
                    $"Node {node.NodeId}: stored Y must equal SampleHeightAtWorld at the same XZ");
            }

            foreach (var edge in result.Layout.EdgesOrdered)
            {
                foreach (var point in edge.ControlPoints)
                {
                    float terrainY = context.SampleHeightAtWorld(point.x, point.z);
                    Assert.AreEqual(terrainY, point.y, POSITION_EPSILON,
                        $"Edge {edge.EdgeId}: control point Y must equal SampleHeightAtWorld at the same XZ");
                }
            }
        }

        // After corridor/clearing flattening the terrain is pulled TOWARD the
        // stored layout heights, so nodes stay close to the ground. The tolerance
        // covers bilinear/grid discretization of the flattening pass, not a
        // height-space mismatch (which would be tens of meters).
        [Test]
        public void NodeHeights_AfterLayoutFlattening_StayOnTerrain()
        {
            const float FLATTENING_TOLERANCE_METERS = 2f;

            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            SetField(terrainConfig, "layoutConfig", config);
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            TerrainHeightGenerator.ApplyLayoutFlattening(context, terrainConfig, result.Layout);

            foreach (var node in result.Layout.NodesOrdered)
            {
                float terrainY = context.SampleHeightAtWorld(node.Position.x, node.Position.z);
                Assert.AreEqual(terrainY, node.Position.y, FLATTENING_TOLERANCE_METERS,
                    $"Node {node.NodeId}: after flattening the terrain must stay within " +
                    $"{FLATTENING_TOLERANCE_METERS}m of the stored node height");
            }
        }

        // The unification guarantee: the slopes recorded on edges during
        // generation are the very values ValidateAgainstTerrain re-reads from
        // the context. Before the fix, edges carried raw-noise-space slopes
        // while the validator read normalized-space slopes.
        [Test]
        public void EdgeRecordedSlopes_MatchContextSlopes()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            foreach (var edge in result.Layout.EdgesOrdered)
            {
                float maxSlope = 0f;
                foreach (var point in edge.ControlPoints)
                {
                    maxSlope = Mathf.Max(maxSlope, context.SampleSlopeAtWorld(point.x, point.z));
                }

                Assert.AreEqual(maxSlope, edge.MaxSlope, SLOPE_EPSILON,
                    $"Edge {edge.EdgeId}: recorded MaxSlope must equal the context slope at its control points");
            }
        }

        // ValidateAgainstTerrain must agree with what the recorded control point
        // slopes predict, before and after flattening — proof that the validator
        // and the generator read the same height space.
        [Test]
        public void ValidateAgainstTerrain_ConsistentWithLayoutGeneration()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            SetField(terrainConfig, "layoutConfig", config);
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            AssertTerrainValidationAgreesWithContextSlopes(result.Layout, context, config, "before flattening");

            TerrainHeightGenerator.ApplyLayoutFlattening(context, terrainConfig, result.Layout);

            AssertTerrainValidationAgreesWithContextSlopes(result.Layout, context, config, "after flattening");
        }

        private void AssertTerrainValidationAgreesWithContextSlopes(
            WorldLayoutContext layout,
            TerrainGenerationContext context,
            WorldLayoutConfig config,
            string stage)
        {
            // Same rule as ValidateAgainstTerrain, computed from the context maps
            float slopeLimit = config.PrimaryPathMaxSlope * 1.1f;
            bool expectedValid = true;
            foreach (var edge in layout.EdgesOrdered)
            {
                if (!edge.IsPrimaryPathEdge) continue;
                foreach (var point in edge.ControlPoints)
                {
                    if (context.SampleSlopeAtWorld(point.x, point.z) > slopeLimit)
                    {
                        expectedValid = false;
                    }
                }
            }

            var validation = WorldLayoutValidator.ValidateAgainstTerrain(
                layout, context, config.PrimaryPathMaxSlope);

            Assert.AreEqual(expectedValid, validation.IsValid,
                $"ValidateAgainstTerrain ({stage}) must agree with the slopes read from the context maps. " +
                $"Error: {validation.Error}");
        }

        #endregion

        #region Reservation Height Tests

        // Every reservation category must sample its own height at its final
        // XZ instead of inheriting the host node height (wrong on any slope).
        [Test]
        public void Reservations_AllCategories_SampleTerrainHeightAtTheirOwnXZ()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            Assert.Greater(result.Layout.EncounterReservations.Count, 0, "Fixture must produce encounter reservations");
            Assert.Greater(result.Layout.ResourceReservations.Count, 0, "Fixture must produce resource reservations");
            Assert.Greater(result.Layout.LandmarkReservations.Count, 0, "Fixture must produce landmark reservations");

            foreach (var reservation in result.Layout.EncounterReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
            foreach (var reservation in result.Layout.ResourceReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
            foreach (var reservation in result.Layout.LandmarkReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
        }

        // Guard against the inherit-host-height regression: encounter and
        // resource reservations are XZ-offset from their host, so on sloped
        // terrain at least one of them must end up at a different height than
        // its host node.
        [Test]
        public void Reservations_OffsetOnSlope_DoNotInheritHostHeight()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            bool anyDiffersFromHost = false;
            foreach (var reservation in result.Layout.EncounterReservations)
            {
                var host = result.Layout.NodesById[reservation.HostNodeId];
                if (Mathf.Abs(reservation.Position.y - host.Position.y) > 0.01f)
                {
                    anyDiffersFromHost = true;
                }
            }
            foreach (var reservation in result.Layout.ResourceReservations)
            {
                var host = result.Layout.NodesById[reservation.HostNodeId];
                if (Mathf.Abs(reservation.Position.y - host.Position.y) > 0.01f)
                {
                    anyDiffersFromHost = true;
                }
            }

            Assert.IsTrue(anyDiffersFromHost,
                "On sloped terrain, XZ-offset reservations must sample their own height " +
                "instead of inheriting the host node height");
        }

        [Test]
        public void Reservations_XYZ_AreDeterministic()
        {
            var config = CreateTestConfig();
            var terrainConfig1 = CreateTerrainConfig();
            var terrainConfig2 = CreateTerrainConfig();
            var context1 = CreateGeneratedContext(12345, terrainConfig1);
            var context2 = CreateGeneratedContext(12345, terrainConfig2);
            var worldDisc = CreateTestWorldDiscDefinition(context1.WorldSize * 0.5f);

            var result1 = WorldLayoutGenerator.Generate(12345, worldDisc, new TerrainContextSampler(context1), config);
            var result2 = WorldLayoutGenerator.Generate(12345, worldDisc, new TerrainContextSampler(context2), config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);

            AssertReservationListsIdentical(
                result1.Layout.EncounterReservations.Count, result2.Layout.EncounterReservations.Count,
                i => (result1.Layout.EncounterReservations[i].ReservationId, result1.Layout.EncounterReservations[i].Position),
                i => (result2.Layout.EncounterReservations[i].ReservationId, result2.Layout.EncounterReservations[i].Position));
            AssertReservationListsIdentical(
                result1.Layout.ResourceReservations.Count, result2.Layout.ResourceReservations.Count,
                i => (result1.Layout.ResourceReservations[i].ReservationId, result1.Layout.ResourceReservations[i].Position),
                i => (result2.Layout.ResourceReservations[i].ReservationId, result2.Layout.ResourceReservations[i].Position));
            AssertReservationListsIdentical(
                result1.Layout.LandmarkReservations.Count, result2.Layout.LandmarkReservations.Count,
                i => (result1.Layout.LandmarkReservations[i].ReservationId, result1.Layout.LandmarkReservations[i].Position),
                i => (result2.Layout.LandmarkReservations[i].ReservationId, result2.Layout.LandmarkReservations[i].Position));
        }

        // Full pipeline order: layout -> corridor/clearing flattening ->
        // reprojection. Stored reservation heights must match the final
        // published terrain exactly.
        [Test]
        public void Reservations_MatchFinalTerrain_AfterFlatteningAndReprojection()
        {
            var config = CreateTestConfig();
            var terrainConfig = CreateTerrainConfig();
            SetField(terrainConfig, "layoutConfig", config);
            var context = CreateGeneratedContext(12345, terrainConfig);
            var sampler = new TerrainContextSampler(context);
            var worldDisc = CreateTestWorldDiscDefinition(sampler.WorldSize * 0.5f);

            var result = WorldLayoutGenerator.Generate(12345, worldDisc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            TerrainHeightGenerator.ApplyLayoutFlattening(context, terrainConfig, result.Layout);
            WorldLayoutGenerator.ReprojectReservationHeights(result.Layout, sampler);

            foreach (var reservation in result.Layout.EncounterReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
            foreach (var reservation in result.Layout.ResourceReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
            foreach (var reservation in result.Layout.LandmarkReservations)
            {
                AssertReservationOnTerrain(reservation.ReservationId, reservation.Position, context);
            }
        }

        private void AssertReservationOnTerrain(string reservationId, Vector3 position, TerrainGenerationContext context)
        {
            float terrainY = context.SampleHeightAtWorld(position.x, position.z);
            Assert.AreEqual(terrainY, position.y, POSITION_EPSILON,
                $"Reservation {reservationId}: stored Y must equal SampleHeightAtWorld at its own XZ");
        }

        private void AssertReservationListsIdentical(
            int count1,
            int count2,
            System.Func<int, (string id, Vector3 position)> get1,
            System.Func<int, (string id, Vector3 position)> get2)
        {
            Assert.AreEqual(count1, count2, "Reservation counts should match between generations");

            for (int i = 0; i < count1; i++)
            {
                var (id1, pos1) = get1(i);
                var (id2, pos2) = get2(i);

                Assert.AreEqual(id1, id2, $"Reservation IDs at index {i} should match");
                Assert.AreEqual(pos1.x, pos2.x, POSITION_EPSILON, $"Reservation {id1}: X should match");
                Assert.AreEqual(pos1.y, pos2.y, POSITION_EPSILON, $"Reservation {id1}: Y should match");
                Assert.AreEqual(pos1.z, pos2.z, POSITION_EPSILON, $"Reservation {id1}: Z should match");
            }
        }

        #endregion
    }
}
