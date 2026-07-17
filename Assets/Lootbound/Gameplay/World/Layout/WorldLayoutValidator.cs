using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// Validates radial world layout structure and terrain compatibility.
    /// </summary>
    public static class WorldLayoutValidator
    {
        /// <summary>
        /// Result of layout validation.
        /// </summary>
        public readonly struct ValidationResult
        {
            public bool IsValid { get; }
            public string Error { get; }

            private ValidationResult(bool isValid, string error)
            {
                IsValid = isValid;
                Error = error;
            }

            public static ValidationResult Valid() => new ValidationResult(true, null);
            public static ValidationResult Invalid(string error) => new ValidationResult(false, error);
        }

        /// <summary>
        /// Validate the structural integrity of the radial layout before terrain generation.
        /// </summary>
        public static ValidationResult ValidateStructure(WorldLayoutContext layout)
        {
            if (layout == null)
            {
                return ValidationResult.Invalid("Layout is null");
            }

            // Check refuge exists
            if (layout.RefugeNode == null)
            {
                return ValidationResult.Invalid("No refuge node found");
            }

            // Check at least one radial path exists
            if (layout.RadialPaths.Count == 0)
            {
                return ValidationResult.Invalid("No radial paths found");
            }

            // Check at least one OuterDestination exists
            if (layout.OuterDestinationNodes.Count == 0)
            {
                return ValidationResult.Invalid("No OuterDestination nodes found");
            }

            // Validate each radial path
            foreach (var path in layout.RadialPaths)
            {
                var pathResult = ValidateRadialPath(layout, path);
                if (!pathResult.IsValid)
                {
                    return pathResult;
                }
            }

            // Check all primary path edges are properly marked
            int primaryEdgeCount = 0;
            foreach (var edge in layout.EdgesOrdered)
            {
                if (edge.IsPrimaryPathEdge)
                {
                    primaryEdgeCount++;

                    // Verify edge belongs to a valid radial path
                    if (string.IsNullOrEmpty(edge.RadialPathId))
                    {
                        return ValidationResult.Invalid($"Primary edge {edge.EdgeId} has no RadialPathId");
                    }
                }
            }

            // Each radial path should have (NodesPerPath) edges connecting to/from nodes
            // First edge connects Refuge to first path node, then N-1 edges for remaining nodes
            int expectedPrimaryEdges = 0;
            foreach (var path in layout.RadialPaths)
            {
                expectedPrimaryEdges += path.EdgeCount;
            }

            if (primaryEdgeCount != expectedPrimaryEdges)
            {
                return ValidationResult.Invalid(
                    $"Expected {expectedPrimaryEdges} primary path edges, found {primaryEdgeCount}");
            }

            // Check no isolated nodes using BFS from refuge
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(layout.RefugeNode.NodeId);
            visited.Add(layout.RefugeNode.NodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (!layout.NodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                foreach (var edgeId in node.ConnectedEdgeIds)
                {
                    if (!layout.EdgesById.TryGetValue(edgeId, out var edge))
                    {
                        continue;
                    }

                    var otherNodeId = edge.GetOtherNodeId(nodeId);
                    if (otherNodeId != null && !visited.Contains(otherNodeId))
                    {
                        visited.Add(otherNodeId);
                        queue.Enqueue(otherNodeId);
                    }
                }
            }

            if (visited.Count != layout.NodesOrdered.Count)
            {
                return ValidationResult.Invalid($"Found {layout.NodesOrdered.Count - visited.Count} isolated nodes");
            }

            // Check deterministic IDs are unique
            var nodeIds = new HashSet<string>();
            foreach (var node in layout.NodesOrdered)
            {
                if (!nodeIds.Add(node.NodeId))
                {
                    return ValidationResult.Invalid($"Duplicate node ID: {node.NodeId}");
                }
            }

            var edgeIds = new HashSet<string>();
            foreach (var edge in layout.EdgesOrdered)
            {
                if (!edgeIds.Add(edge.EdgeId))
                {
                    return ValidationResult.Invalid($"Duplicate edge ID: {edge.EdgeId}");
                }
            }

            // Validate radial properties consistency
            var radialResult = ValidateRadialProperties(layout);
            if (!radialResult.IsValid)
            {
                return radialResult;
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validate a single radial path.
        /// </summary>
        private static ValidationResult ValidateRadialPath(WorldLayoutContext layout, RadialPath path)
        {
            if (string.IsNullOrEmpty(path.PathId))
            {
                return ValidationResult.Invalid("RadialPath has empty PathId");
            }

            if (path.NodeCount == 0)
            {
                return ValidationResult.Invalid($"RadialPath {path.PathId} has no nodes");
            }

            // Check OuterDestination is set
            if (string.IsNullOrEmpty(path.OuterDestinationNodeId))
            {
                return ValidationResult.Invalid($"RadialPath {path.PathId} has no OuterDestination");
            }

            // Verify OuterDestination is the last node
            if (path.NodeIds[path.NodeCount - 1] != path.OuterDestinationNodeId)
            {
                return ValidationResult.Invalid(
                    $"RadialPath {path.PathId} OuterDestination is not the last node");
            }

            // Verify OuterDestination exists and has correct type
            if (!layout.NodesById.TryGetValue(path.OuterDestinationNodeId, out var destNode))
            {
                return ValidationResult.Invalid(
                    $"RadialPath {path.PathId} OuterDestination node not found");
            }

            if (destNode.Type != WorldNodeType.OuterDestination)
            {
                return ValidationResult.Invalid(
                    $"RadialPath {path.PathId} terminal node is {destNode.Type}, expected OuterDestination");
            }

            // Verify all path nodes exist
            foreach (var nodeId in path.NodeIds)
            {
                if (!layout.NodesById.ContainsKey(nodeId))
                {
                    return ValidationResult.Invalid(
                        $"RadialPath {path.PathId} references non-existent node {nodeId}");
                }
            }

            // Verify all path edges exist
            foreach (var edgeId in path.EdgeIds)
            {
                if (!layout.EdgesById.ContainsKey(edgeId))
                {
                    return ValidationResult.Invalid(
                        $"RadialPath {path.PathId} references non-existent edge {edgeId}");
                }
            }

            // Verify edge count matches (should be NodeCount edges, since first edge connects Refuge to first node)
            if (path.EdgeCount != path.NodeCount)
            {
                return ValidationResult.Invalid(
                    $"RadialPath {path.PathId} has {path.NodeCount} nodes but {path.EdgeCount} edges");
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validate radial properties are consistent across the layout.
        /// </summary>
        private static ValidationResult ValidateRadialProperties(WorldLayoutContext layout)
        {
            // Refuge should have distance 0 and be in Refuge ring
            var refuge = layout.RefugeNode;
            if (refuge.DistanceFromRefuge != 0f)
            {
                return ValidationResult.Invalid(
                    $"Refuge has DistanceFromRefuge = {refuge.DistanceFromRefuge}, expected 0");
            }

            if (refuge.NormalizedWorldRadius != 0f)
            {
                return ValidationResult.Invalid(
                    $"Refuge has NormalizedWorldRadius = {refuge.NormalizedWorldRadius}, expected 0");
            }

            if (refuge.Ring != WorldRing.Refuge)
            {
                return ValidationResult.Invalid(
                    $"Refuge is in ring {refuge.Ring}, expected Refuge");
            }

            // All nodes should have non-negative distances (except Refuge which is 0)
            foreach (var node in layout.NodesOrdered)
            {
                if (node.Type == WorldNodeType.Refuge) continue;

                if (node.DistanceFromRefuge <= 0f)
                {
                    return ValidationResult.Invalid(
                        $"Node {node.NodeId} has invalid DistanceFromRefuge = {node.DistanceFromRefuge}");
                }

                if (node.NormalizedWorldRadius < 0f)
                {
                    return ValidationResult.Invalid(
                        $"Node {node.NodeId} has negative NormalizedWorldRadius = {node.NormalizedWorldRadius}");
                }
            }

            // Primary path nodes should have valid PathStepIndex
            foreach (var path in layout.RadialPaths)
            {
                for (int i = 0; i < path.NodeIds.Count; i++)
                {
                    if (!layout.NodesById.TryGetValue(path.NodeIds[i], out var node)) continue;

                    if (node.PathStepIndex != i)
                    {
                        return ValidationResult.Invalid(
                            $"Node {node.NodeId} has PathStepIndex = {node.PathStepIndex}, expected {i}");
                    }
                }
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validate that all primary path edges are traversable.
        /// Should be called before terrain corrections.
        /// </summary>
        public static ValidationResult ValidatePrimaryPathTraversability(
            WorldLayoutContext layout,
            float maxSlope)
        {
            if (layout == null)
            {
                return ValidationResult.Invalid("Layout is null");
            }

            foreach (var edge in layout.EdgesOrdered)
            {
                if (!edge.IsPrimaryPathEdge) continue;

                if (!edge.IsTraversable)
                {
                    return ValidationResult.Invalid(
                        $"Primary path edge {edge.EdgeId} is not traversable (max slope: {edge.MaxSlope:F1}° > {maxSlope:F1}°)");
                }
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validate the layout against the final terrain after corrections.
        /// </summary>
        public static ValidationResult ValidateAgainstTerrain(
            WorldLayoutContext layout,
            TerrainGenerationContext terrain,
            float maxPrimaryPathSlope)
        {
            if (layout == null)
            {
                return ValidationResult.Invalid("Layout is null");
            }

            if (terrain == null)
            {
                return ValidationResult.Invalid("Terrain is null");
            }

            // Check primary path edges are still traversable after corrections
            foreach (var edge in layout.EdgesOrdered)
            {
                if (!edge.IsPrimaryPathEdge) continue;

                // Sample slope along control points
                foreach (var point in edge.ControlPoints)
                {
                    float slope = terrain.SampleSlopeAtWorld(point.x, point.z);
                    if (slope > maxPrimaryPathSlope * 1.1f) // Allow 10% tolerance
                    {
                        return ValidationResult.Invalid(
                            $"Primary path edge {edge.EdgeId} has slope {slope:F1}° at ({point.x:F0}, {point.z:F0}) after terrain corrections");
                    }
                }
            }

            // Verify all OuterDestinations are reachable via primary paths
            foreach (var outerDest in layout.OuterDestinationNodes)
            {
                var reachResult = ValidateOuterDestinationReachable(layout, outerDest.NodeId);
                if (!reachResult.IsValid)
                {
                    return reachResult;
                }
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// Validate that a specific OuterDestination is reachable from Refuge via primary paths.
        /// </summary>
        private static ValidationResult ValidateOuterDestinationReachable(
            WorldLayoutContext layout,
            string outerDestinationId)
        {
            var visited = new HashSet<string>();
            var queue = new Queue<string>();
            queue.Enqueue(layout.RefugeNode.NodeId);
            visited.Add(layout.RefugeNode.NodeId);

            while (queue.Count > 0)
            {
                var nodeId = queue.Dequeue();
                if (nodeId == outerDestinationId)
                {
                    return ValidationResult.Valid();
                }

                if (!layout.NodesById.TryGetValue(nodeId, out var node))
                {
                    continue;
                }

                foreach (var edgeId in node.ConnectedEdgeIds)
                {
                    if (!layout.EdgesById.TryGetValue(edgeId, out var edge))
                    {
                        continue;
                    }

                    // Only traverse primary path edges for reachability check
                    if (!edge.IsPrimaryPathEdge) continue;

                    var otherNodeId = edge.GetOtherNodeId(nodeId);
                    if (otherNodeId != null && !visited.Contains(otherNodeId))
                    {
                        visited.Add(otherNodeId);
                        queue.Enqueue(otherNodeId);
                    }
                }
            }

            return ValidationResult.Invalid($"OuterDestination {outerDestinationId} not reachable from refuge via primary path");
        }

        /// <summary>
        /// Check if a proposed edge would exceed terrain correction limits.
        /// Returns true if the edge is TOO steep to be corrected.
        /// </summary>
        public static bool WouldExceedCorrectionLimits(
            ITerrainSampler sampler,
            Vector3 from,
            Vector3 to,
            float maxSlope,
            float maxCorrectionStrength,
            int samplePoints)
        {
            if (samplePoints < 2) samplePoints = 2;

            float maxExcessSlope = 0f;
            int excessCount = 0;
            int samples = 0;

            for (int i = 0; i <= samplePoints; i++)
            {
                float t = i / (float)samplePoints;
                float x = Mathf.Lerp(from.x, to.x, t);
                float z = Mathf.Lerp(from.z, to.z, t);

                float slope = sampler.SampleSlope(x, z);

                if (slope > maxSlope)
                {
                    float excessSlope = slope - maxSlope;
                    if (excessSlope > maxExcessSlope) maxExcessSlope = excessSlope;
                    excessCount++;
                }
                samples++;
            }

            // Only reject if:
            // - More than 50% of points exceed the slope limit
            // - OR any point exceeds the limit by more than 25 degrees (very steep cliff)
            float exceedRatio = excessCount / (float)samples;
            bool tooManySteepPoints = exceedRatio > 0.5f;
            bool hasCliff = maxExcessSlope > 25f;

            return tooManySteepPoints || hasCliff;
        }
    }
}
