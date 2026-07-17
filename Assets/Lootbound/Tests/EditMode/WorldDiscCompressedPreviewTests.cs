using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Lootbound.Gameplay.World;
using Lootbound.Gameplay.World.Layout;

namespace Lootbound.Tests.EditMode
{
    /// <summary>
    /// Regression tests for WorldDiscDefinition compressed preview mode.
    ///
    /// The runtime currently constructs a 1:1 disc (terrain radius == logical radius),
    /// so any code that wrongly normalizes a distance against the terrain size instead
    /// of the logical WorldRadius would go unnoticed. These tests use a disc where the
    /// two radii differ by ~4x so such a mistake changes the resulting ring.
    ///
    /// Rule under test (Docs/WORLD_RINGS.md, WORLD_DISC_AND_STREAMING_VISION.md):
    /// NormalizedWorldRadius is absolute — always distance / logical WorldRadius,
    /// never affected by the loaded/preview terrain extent.
    /// </summary>
    public class WorldDiscCompressedPreviewTests
    {
        private const float LOGICAL_WORLD_RADIUS = 2000f;
        private const float EPSILON = 0.0001f;
        private const float POSITION_EPSILON = 0.001f;

        #region Test Helpers

        private WorldLayoutConfig CreateTestConfig()
        {
            var config = ScriptableObject.CreateInstance<WorldLayoutConfig>();

            // Use reflection to set private serialized fields (same values as WorldLayoutTests)
            SetField(config, "maxGenerationAttempts", 10);
            SetField(config, "minimumRadialPathCount", 3);
            SetField(config, "maximumRadialPathCount", 4);
            SetField(config, "nodesPerRadialPath", 4);
            SetField(config, "radialStepMin", 80f);
            SetField(config, "radialStepMax", 150f);
            SetField(config, "primaryPathMaxSlope", 40f);
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

        private TerrainGenerationConfig CreateTerrainConfig()
        {
            var config = ScriptableObject.CreateInstance<TerrainGenerationConfig>();

            SetField(config, "worldSize", 1024f);
            SetField(config, "terrainHeight", 150f);
            SetField(config, "heightmapResolution", 129);
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
        /// Context-backed sampler, generated the same way the runtime pipeline
        /// does before layout generation (unified height space).
        /// </summary>
        private ITerrainSampler CreateSampler(int seed)
        {
            var terrainConfig = CreateTerrainConfig();
            var context = new TerrainGenerationContext(
                seed,
                terrainConfig.HeightmapResolution,
                terrainConfig.WorldSize,
                terrainConfig.TerrainHeight);
            TerrainHeightGenerator.Generate(context, terrainConfig);
            return new TerrainContextSampler(context);
        }

        private WorldRingConfig CreateTestRingConfig()
        {
            return WorldRingConfig.CreateDefault();
        }

        /// <summary>
        /// Compressed disc: the preview terrain radius (sampler.WorldSize * 0.5 = 512)
        /// covers only part of the logical world (radius 2000).
        /// </summary>
        private WorldDiscDefinition CreateCompressedDisc(ITerrainSampler sampler, WorldRingConfig ringConfig)
        {
            return new WorldDiscDefinition(LOGICAL_WORLD_RADIUS, sampler.WorldSize * 0.5f, ringConfig);
        }

        /// <summary>
        /// 1:1 disc matching what ProceduralTerrainGenerator currently builds.
        /// </summary>
        private WorldDiscDefinition CreateOneToOneDisc(ITerrainSampler sampler, WorldRingConfig ringConfig)
        {
            return new WorldDiscDefinition(sampler.WorldSize * 0.5f, ringConfig);
        }

        #endregion

        #region Definition Tests

        [Test]
        public void CompressedConstructor_SetsCompressionProperties()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(LOGICAL_WORLD_RADIUS, 512f, ringConfig);

            Assert.IsTrue(disc.IsCompressedPreview, "Disc with previewTerrainRadius < worldRadius should be compressed");
            Assert.AreEqual(LOGICAL_WORLD_RADIUS, disc.WorldRadius, EPSILON);
            Assert.AreEqual(512f, disc.PreviewTerrainRadius, EPSILON);
            Assert.AreEqual(512f / LOGICAL_WORLD_RADIUS, disc.CompressionRatio, EPSILON);
        }

        [Test]
        public void CompressedConstructor_EqualRadii_IsNotCompressed()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(512f, 512f, ringConfig);

            Assert.IsFalse(disc.IsCompressedPreview, "Equal radii should not be flagged as compressed preview");
            Assert.AreEqual(1f, disc.CompressionRatio, EPSILON);
        }

        [Test]
        public void OneToOneConstructor_IsNotCompressed()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(512f, ringConfig);

            Assert.IsFalse(disc.IsCompressedPreview);
            Assert.AreEqual(1f, disc.CompressionRatio, EPSILON);
            Assert.AreEqual(disc.WorldRadius, disc.PreviewTerrainRadius, EPSILON);
        }

        [Test]
        public void DistanceConversion_RoundTripsExactly()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(LOGICAL_WORLD_RADIUS, 512f, ringConfig);

            // The 512/2000 ratio is not exactly representable in binary floating point,
            // so the round trip is only exact to a few ulp. The tolerance must scale
            // with the magnitude of the distance (relative error, not absolute).
            float[] logicalDistances = { 0f, 1f, 100f, 512f, 1250f, LOGICAL_WORLD_RADIUS };
            foreach (float logical in logicalDistances)
            {
                float preview = disc.LogicalToPreviewDistance(logical);
                float backToLogical = disc.PreviewToLogicalDistance(preview);
                float tolerance = Mathf.Max(EPSILON, logical * 1e-6f);
                Assert.AreEqual(logical, backToLogical, tolerance,
                    $"Logical→preview→logical round trip should preserve {logical}");
            }

            // Endpoints must map onto each other
            Assert.AreEqual(disc.PreviewTerrainRadius, disc.LogicalToPreviewDistance(disc.WorldRadius),
                Mathf.Max(EPSILON, disc.PreviewTerrainRadius * 1e-6f),
                "Full logical radius should map to the preview terrain radius");
            Assert.AreEqual(disc.WorldRadius, disc.PreviewToLogicalDistance(disc.PreviewTerrainRadius),
                Mathf.Max(EPSILON, disc.WorldRadius * 1e-6f),
                "Preview terrain edge should map to the full logical radius");
        }

        [Test]
        public void DistanceConversion_OneToOneDisc_IsIdentity()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(512f, ringConfig);

            Assert.AreEqual(300f, disc.LogicalToPreviewDistance(300f), EPSILON);
            Assert.AreEqual(300f, disc.PreviewToLogicalDistance(300f), EPSILON);
        }

        [Test]
        public void EvaluateAt_NormalizesAgainstLogicalRadius()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(LOGICAL_WORLD_RADIUS, 512f, ringConfig);
            Vector3 refuge = Vector3.zero;

            // 400m from refuge: 400/2000 = 0.20 → Wildlands.
            // A terrain-size normalization would give 400/512 = 0.78 → Edgelands.
            var sample = disc.EvaluateAt(new Vector3(400f, 0f, 0f), refuge);
            Assert.AreEqual(400f, sample.DistanceFromRefuge, POSITION_EPSILON);
            Assert.AreEqual(400f / LOGICAL_WORLD_RADIUS, sample.NormalizedWorldRadius, EPSILON,
                "NormalizedWorldRadius must be distance / logical WorldRadius, not distance / terrain radius");
            Assert.AreEqual(WorldRing.Wildlands, sample.Ring);
            Assert.AreNotEqual(WorldRing.Edgelands, sample.Ring,
                "Ring must come from the logical radius; Edgelands would mean terrain-size normalization");

            // Preview terrain edge (512m) is still deep inside the logical world (0.256 → Wildlands, not Void).
            var edgeSample = disc.EvaluateAt(new Vector3(0f, 0f, 512f), refuge);
            Assert.AreEqual(512f / LOGICAL_WORLD_RADIUS, edgeSample.NormalizedWorldRadius, EPSILON);
            Assert.AreEqual(WorldRing.Wildlands, edgeSample.Ring,
                "A position at the preview terrain edge is NOT at the logical world edge");

            // Evaluation is valid beyond the preview terrain: 1900/2000 = 0.95 → Void.
            var farSample = disc.EvaluateAt(new Vector3(1900f, 0f, 0f), refuge);
            Assert.AreEqual(0.95f, farSample.NormalizedWorldRadius, EPSILON);
            Assert.AreEqual(WorldRing.Void, farSample.Ring);
        }

        [Test]
        public void PlayableAndPreviewBounds_AreIndependent()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(LOGICAL_WORLD_RADIUS, 512f, ringConfig);
            Vector3 refuge = Vector3.zero;
            var beyondPreview = new Vector3(600f, 0f, 0f);

            Assert.IsTrue(disc.IsWithinPlayableRadius(beyondPreview, refuge),
                "600m is inside the 2000m logical world");
            Assert.IsFalse(disc.IsWithinPreviewTerrain(beyondPreview, refuge),
                "600m is outside the 512m preview terrain");
        }

        #endregion

        #region Layout Generation Tests (Compressed Disc)

        [Test]
        public void Generation_Succeeds_WithCompressedDisc()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(12345);
            var disc = CreateCompressedDisc(sampler, CreateTestRingConfig());

            var result = WorldLayoutGenerator.Generate(12345, disc, sampler, config);

            Assert.IsTrue(result.Success, $"Generation should succeed with compressed disc. Error: {result.Error}");
            Assert.IsNotNull(result.Layout.RefugeNode);
            Assert.Greater(result.Layout.OuterDestinationNodes.Count, 0);
        }

        [Test]
        public void Nodes_NormalizedAgainstLogicalRadius()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(12345);
            var ringConfig = CreateTestRingConfig();
            var disc = CreateCompressedDisc(sampler, ringConfig);
            float previewRadius = disc.PreviewTerrainRadius;

            var result = WorldLayoutGenerator.Generate(12345, disc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            Vector3 refugePos = result.Layout.RefugeNode.Position;

            foreach (var node in result.Layout.NodesOrdered)
            {
                float expectedNormalized = node.DistanceFromRefuge / disc.WorldRadius;

                Assert.AreEqual(expectedNormalized, node.NormalizedWorldRadius, EPSILON,
                    $"Node {node.NodeId}: NormalizedWorldRadius must use logical WorldRadius ({disc.WorldRadius})");
                Assert.AreEqual(ringConfig.GetRingAt(expectedNormalized), node.Ring,
                    $"Node {node.NodeId}: Ring must match the logical normalized radius");

                // Guard against terrain-size normalization (values differ by ~3.9x here)
                if (node.DistanceFromRefuge > 1f)
                {
                    float terrainNormalized = node.DistanceFromRefuge / previewRadius;
                    Assert.AreNotEqual(terrainNormalized, node.NormalizedWorldRadius,
                        $"Node {node.NodeId}: NormalizedWorldRadius must not be normalized against the preview terrain radius");
                }

                // EvaluateAt on the disc must agree with the values stored on the node
                var sample = disc.EvaluateAt(node.Position, refugePos);
                Assert.AreEqual(node.NormalizedWorldRadius, sample.NormalizedWorldRadius, EPSILON,
                    $"Node {node.NodeId}: EvaluateAt should agree with stored NormalizedWorldRadius");
                Assert.AreEqual(node.Ring, sample.Ring,
                    $"Node {node.NodeId}: EvaluateAt should agree with stored Ring");
            }
        }

        [Test]
        public void OuterDestinations_UseLogicalRing_NotTerrainRing()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(12345);
            var ringConfig = CreateTestRingConfig();
            var disc = CreateCompressedDisc(sampler, ringConfig);

            var result = WorldLayoutGenerator.Generate(12345, disc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            foreach (var node in result.Layout.OuterDestinationNodes)
            {
                // OuterDestinations sit near the preview terrain edge. Normalized against
                // the terrain radius they would land in Outerlands/Edgelands/Void; against
                // the logical radius they stay in the inner rings. The two must differ.
                WorldRing terrainRing = ringConfig.GetRingAt(node.DistanceFromRefuge / disc.PreviewTerrainRadius);
                Assert.AreNotEqual(terrainRing, node.Ring,
                    $"OuterDestination {node.NodeId} (distance {node.DistanceFromRefuge:F0}m): " +
                    "logical ring must differ from terrain-normalized ring in compressed mode");

                Assert.AreEqual(ringConfig.GetRingAt(node.DistanceFromRefuge / disc.WorldRadius), node.Ring,
                    $"OuterDestination {node.NodeId}: Ring must come from the logical world radius");
            }
        }

        [Test]
        public void Reservations_NormalizedAgainstLogicalRadius()
        {
            var config = CreateTestConfig();
            var sampler = CreateSampler(12345);
            var ringConfig = CreateTestRingConfig();
            var disc = CreateCompressedDisc(sampler, ringConfig);

            var result = WorldLayoutGenerator.Generate(12345, disc, sampler, config);
            Assert.IsTrue(result.Success, $"Generation should succeed. Error: {result.Error}");

            foreach (var reservation in result.Layout.EncounterReservations)
            {
                AssertReservationUsesLogicalRadius(
                    reservation.ReservationId, reservation.DistanceFromRefuge,
                    reservation.NormalizedWorldRadius, reservation.Ring, disc, ringConfig);
            }

            foreach (var reservation in result.Layout.ResourceReservations)
            {
                AssertReservationUsesLogicalRadius(
                    reservation.ReservationId, reservation.DistanceFromRefuge,
                    reservation.NormalizedWorldRadius, reservation.Ring, disc, ringConfig);
            }

            foreach (var reservation in result.Layout.LandmarkReservations)
            {
                AssertReservationUsesLogicalRadius(
                    reservation.ReservationId, reservation.DistanceFromRefuge,
                    reservation.NormalizedWorldRadius, reservation.Ring, disc, ringConfig);
            }
        }

        private void AssertReservationUsesLogicalRadius(
            string reservationId,
            float distanceFromRefuge,
            float normalizedWorldRadius,
            WorldRing ring,
            WorldDiscDefinition disc,
            WorldRingConfig ringConfig)
        {
            float expectedNormalized = distanceFromRefuge / disc.WorldRadius;

            Assert.AreEqual(expectedNormalized, normalizedWorldRadius, EPSILON,
                $"Reservation {reservationId}: NormalizedWorldRadius must use logical WorldRadius");
            Assert.AreEqual(ringConfig.GetRingAt(expectedNormalized), ring,
                $"Reservation {reservationId}: Ring must match the logical normalized radius");
        }

        #endregion

        #region Determinism Tests (1:1 vs Compressed)

        [Test]
        public void SameSeed_OneToOneAndCompressed_ProduceSameLogicalStructure()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var oneToOneDisc = CreateOneToOneDisc(sampler1, CreateTestRingConfig());
            var compressedDisc = CreateCompressedDisc(sampler2, CreateTestRingConfig());

            var result1 = WorldLayoutGenerator.Generate(12345, oneToOneDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, compressedDisc, sampler2, config);

            Assert.IsTrue(result1.Success, $"1:1 generation should succeed. Error: {result1.Error}");
            Assert.IsTrue(result2.Success, $"Compressed generation should succeed. Error: {result2.Error}");

            // Nodes: same IDs, positions, and physical distances
            Assert.AreEqual(result1.Layout.NodesOrdered.Count, result2.Layout.NodesOrdered.Count,
                "Both modes should produce the same node count");

            for (int i = 0; i < result1.Layout.NodesOrdered.Count; i++)
            {
                var node1 = result1.Layout.NodesOrdered[i];
                var node2 = result2.Layout.NodesOrdered[i];

                Assert.AreEqual(node1.NodeId, node2.NodeId, $"Node IDs at index {i} should match");
                Assert.AreEqual(node1.Type, node2.Type, $"Node {node1.NodeId}: type should match");
                Assert.AreEqual(node1.Position.x, node2.Position.x, POSITION_EPSILON, $"Node {node1.NodeId}: X should match");
                Assert.AreEqual(node1.Position.y, node2.Position.y, POSITION_EPSILON, $"Node {node1.NodeId}: Y should match");
                Assert.AreEqual(node1.Position.z, node2.Position.z, POSITION_EPSILON, $"Node {node1.NodeId}: Z should match");
                Assert.AreEqual(node1.DistanceFromRefuge, node2.DistanceFromRefuge, POSITION_EPSILON,
                    $"Node {node1.NodeId}: physical distance from refuge should match");
            }

            // Edges: same IDs and endpoints
            Assert.AreEqual(result1.Layout.EdgesOrdered.Count, result2.Layout.EdgesOrdered.Count);
            for (int i = 0; i < result1.Layout.EdgesOrdered.Count; i++)
            {
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].EdgeId, result2.Layout.EdgesOrdered[i].EdgeId);
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].NodeAId, result2.Layout.EdgesOrdered[i].NodeAId);
                Assert.AreEqual(result1.Layout.EdgesOrdered[i].NodeBId, result2.Layout.EdgesOrdered[i].NodeBId);
            }

            // Radial paths: same IDs, angles, destinations
            Assert.AreEqual(result1.Layout.RadialPaths.Count, result2.Layout.RadialPaths.Count);
            for (int i = 0; i < result1.Layout.RadialPaths.Count; i++)
            {
                Assert.AreEqual(result1.Layout.RadialPaths[i].PathId, result2.Layout.RadialPaths[i].PathId);
                Assert.AreEqual(result1.Layout.RadialPaths[i].StartAngle, result2.Layout.RadialPaths[i].StartAngle, 0.001f);
                Assert.AreEqual(result1.Layout.RadialPaths[i].OuterDestinationNodeId, result2.Layout.RadialPaths[i].OuterDestinationNodeId);
            }

            // Reservations: same hosts in the same order
            Assert.AreEqual(result1.Layout.EncounterReservations.Count, result2.Layout.EncounterReservations.Count);
            for (int i = 0; i < result1.Layout.EncounterReservations.Count; i++)
            {
                Assert.AreEqual(result1.Layout.EncounterReservations[i].HostNodeId,
                    result2.Layout.EncounterReservations[i].HostNodeId,
                    $"Encounter reservation {i} host should match between modes");
            }

            Assert.AreEqual(result1.Layout.ResourceReservations.Count, result2.Layout.ResourceReservations.Count);
            for (int i = 0; i < result1.Layout.ResourceReservations.Count; i++)
            {
                Assert.AreEqual(result1.Layout.ResourceReservations[i].HostNodeId,
                    result2.Layout.ResourceReservations[i].HostNodeId,
                    $"Resource reservation {i} host should match between modes");
            }

            Assert.AreEqual(result1.Layout.LandmarkReservations.Count, result2.Layout.LandmarkReservations.Count);
            for (int i = 0; i < result1.Layout.LandmarkReservations.Count; i++)
            {
                Assert.AreEqual(result1.Layout.LandmarkReservations[i].HostNodeId,
                    result2.Layout.LandmarkReservations[i].HostNodeId,
                    $"Landmark reservation {i} host should match between modes");
            }
        }

        [Test]
        public void SameSeed_NormalizedRadii_ScaleByCompressionRatio()
        {
            var config = CreateTestConfig();
            var sampler1 = CreateSampler(12345);
            var sampler2 = CreateSampler(12345);
            var oneToOneDisc = CreateOneToOneDisc(sampler1, CreateTestRingConfig());
            var compressedDisc = CreateCompressedDisc(sampler2, CreateTestRingConfig());

            var result1 = WorldLayoutGenerator.Generate(12345, oneToOneDisc, sampler1, config);
            var result2 = WorldLayoutGenerator.Generate(12345, compressedDisc, sampler2, config);

            Assert.IsTrue(result1.Success);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result1.Layout.NodesOrdered.Count, result2.Layout.NodesOrdered.Count);

            // Same physical layout, different logical radius:
            // normalizedCompressed = distance / WorldRadius = normalized1to1 * CompressionRatio
            for (int i = 0; i < result1.Layout.NodesOrdered.Count; i++)
            {
                var node1 = result1.Layout.NodesOrdered[i];
                var node2 = result2.Layout.NodesOrdered[i];

                Assert.AreEqual(node1.NormalizedWorldRadius * compressedDisc.CompressionRatio,
                    node2.NormalizedWorldRadius, EPSILON,
                    $"Node {node1.NodeId}: compressed NormalizedWorldRadius should equal 1:1 value scaled by CompressionRatio");
            }
        }

        #endregion

        #region Ring Boundary Tests (Compressed Mode)

        // Power-of-two compression (2048 → 512, ratio 0.25) keeps the preview↔logical
        // conversion bit-exact: a preview distance converted to logical space lands
        // exactly on the intended normalized radius. This allows asserting the strict
        // [min, max) boundary rule at the exact threshold, with no tolerance.
        private const float BOUNDARY_LOGICAL_RADIUS = 2048f;
        private const float BOUNDARY_PREVIEW_RADIUS = 512f;

        // Offset in normalized space for the "just before" / "just after" cases.
        // Far above float precision, far below the smallest gap between thresholds (0.05).
        private const float BOUNDARY_OFFSET = 0.0001f;

        /// <summary>
        /// All ring thresholds sorted by ascending minimum normalized radius.
        /// Read from the config (not hardcoded) so the tests follow threshold changes.
        /// </summary>
        private List<(WorldRing ring, float min)> GetRingThresholdsAscending(WorldRingConfig ringConfig)
        {
            var thresholds = new List<(WorldRing ring, float min)>();
            foreach (WorldRing ring in System.Enum.GetValues(typeof(WorldRing)))
            {
                thresholds.Add((ring, ringConfig.GetMinimumRadius(ring)));
            }
            thresholds.Sort((a, b) => a.min.CompareTo(b.min));
            return thresholds;
        }

        /// <summary>
        /// Evaluate a ring starting from a preview-space distance:
        /// convert to logical space first, then evaluate against the logical WorldRadius.
        /// </summary>
        private WorldRing EvaluateRingFromPreviewDistance(WorldDiscDefinition disc, float previewDistance)
        {
            float logicalDistance = disc.PreviewToLogicalDistance(previewDistance);
            return WorldRingEvaluator.EvaluateFromDistance(logicalDistance, disc.WorldRadius, disc.RingConfig).Ring;
        }

        [Test]
        public void RingBoundaries_Compressed_JustBeforeThreshold_ReturnsPreviousRing()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, BOUNDARY_PREVIEW_RADIUS, ringConfig);
            var thresholds = GetRingThresholdsAscending(ringConfig);

            // Skip index 0 (Refuge starts at 0; there is no ring before it)
            for (int i = 1; i < thresholds.Count; i++)
            {
                float previewDistance = (thresholds[i].min - BOUNDARY_OFFSET) * disc.PreviewTerrainRadius;
                WorldRing ring = EvaluateRingFromPreviewDistance(disc, previewDistance);

                Assert.AreEqual(thresholds[i - 1].ring, ring,
                    $"Just before the {thresholds[i].ring} threshold ({thresholds[i].min}), " +
                    $"the previous ring {thresholds[i - 1].ring} must still apply (max is exclusive)");
            }
        }

        [Test]
        public void RingBoundaries_Compressed_ExactlyAtThreshold_ReturnsNewRing()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, BOUNDARY_PREVIEW_RADIUS, ringConfig);
            var thresholds = GetRingThresholdsAscending(ringConfig);

            // Includes index 0: exactly at 0 → Refuge
            for (int i = 0; i < thresholds.Count; i++)
            {
                float previewDistance = thresholds[i].min * disc.PreviewTerrainRadius;
                WorldRing ring = EvaluateRingFromPreviewDistance(disc, previewDistance);

                Assert.AreEqual(thresholds[i].ring, ring,
                    $"Exactly at the {thresholds[i].ring} threshold ({thresholds[i].min}), " +
                    $"the new ring must apply (min is inclusive)");
            }
        }

        [Test]
        public void RingBoundaries_Compressed_JustAfterThreshold_ReturnsNewRing()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, BOUNDARY_PREVIEW_RADIUS, ringConfig);
            var thresholds = GetRingThresholdsAscending(ringConfig);

            for (int i = 0; i < thresholds.Count; i++)
            {
                float previewDistance = (thresholds[i].min + BOUNDARY_OFFSET) * disc.PreviewTerrainRadius;
                WorldRing ring = EvaluateRingFromPreviewDistance(disc, previewDistance);

                Assert.AreEqual(thresholds[i].ring, ring,
                    $"Just after the {thresholds[i].ring} threshold ({thresholds[i].min}), " +
                    $"the new ring must apply");
            }
        }

        [Test]
        public void RingBoundaries_Compressed_VoidHasNoUpperBound()
        {
            var ringConfig = CreateTestRingConfig();
            var disc = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, BOUNDARY_PREVIEW_RADIUS, ringConfig);

            // Void owns [min, +∞]: beyond the preview edge and even beyond the logical radius
            WorldRing atLogicalEdge = EvaluateRingFromPreviewDistance(disc, 1.0f * disc.PreviewTerrainRadius);
            Assert.AreEqual(WorldRing.Void, atLogicalEdge,
                "Preview terrain edge maps to the logical world edge (normalized 1.0) → Void");

            WorldRing beyondLogicalEdge = EvaluateRingFromPreviewDistance(disc, 1.5f * disc.PreviewTerrainRadius);
            Assert.AreEqual(WorldRing.Void, beyondLogicalEdge,
                "Beyond the logical radius (normalized 1.5) is still Void (no upper bound)");
        }

        [Test]
        public void RingBoundaries_SameRing_InOneToOneAndCompressedModes()
        {
            var ringConfig = CreateTestRingConfig();
            var compressed = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, BOUNDARY_PREVIEW_RADIUS, ringConfig);
            var oneToOne = new WorldDiscDefinition(BOUNDARY_LOGICAL_RADIUS, ringConfig);
            var thresholds = GetRingThresholdsAscending(ringConfig);

            // The logical structure must be identical between modes: the same logical
            // point yields the same ring whether reached directly (1:1) or through
            // the preview transform (compressed).
            float[] offsets = { -BOUNDARY_OFFSET, 0f, BOUNDARY_OFFSET };

            foreach (var threshold in thresholds)
            {
                foreach (float offset in offsets)
                {
                    float normalized = threshold.min + offset;
                    if (normalized < 0f) continue; // No position before the Refuge center

                    float logicalDistance = normalized * BOUNDARY_LOGICAL_RADIUS;

                    // 1:1 mode: distances are already logical
                    WorldRing oneToOneRing = WorldRingEvaluator.EvaluateFromDistance(
                        logicalDistance, oneToOne.WorldRadius, ringConfig).Ring;

                    // Compressed mode: same logical point seen through the preview transform
                    float previewDistance = compressed.LogicalToPreviewDistance(logicalDistance);
                    WorldRing compressedRing = EvaluateRingFromPreviewDistance(compressed, previewDistance);

                    Assert.AreEqual(oneToOneRing, compressedRing,
                        $"Ring at normalized {normalized} (threshold {threshold.ring} {offset:+0.0000;-0.0000;+0}) " +
                        "must be identical in 1:1 and compressed modes");
                }
            }
        }

        #endregion
    }
}
