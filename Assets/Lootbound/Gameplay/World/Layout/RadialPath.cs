using System.Collections.Generic;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// A terrain-aware path radiating outward from near the Refuge to an OuterDestination.
    ///
    /// RadialPaths are not rigid spokes - they curve naturally following terrain,
    /// with branches extending from their nodes. The path maintains general outward
    /// progression while allowing natural curvature.
    /// </summary>
    public sealed class RadialPath
    {
        /// <summary>
        /// Deterministic path ID: "path_{seed}_{index}"
        /// </summary>
        public string PathId { get; }

        /// <summary>
        /// Ordered list of node IDs along the primary path spine.
        /// First node is near Refuge, last node is the OuterDestination.
        /// Does not include branch nodes.
        /// </summary>
        public IReadOnlyList<string> NodeIds => _nodeIds;
        private readonly List<string> _nodeIds;

        /// <summary>
        /// Ordered list of edge IDs along the primary path spine.
        /// </summary>
        public IReadOnlyList<string> EdgeIds => _edgeIds;
        private readonly List<string> _edgeIds;

        /// <summary>
        /// Initial angular direction from Refuge center (degrees).
        /// 0° = positive X, increases counter-clockwise.
        /// The actual path may curve away from this initial direction.
        /// </summary>
        public float StartAngle { get; }

        /// <summary>
        /// Node ID of the OuterDestination (terminal node of this path).
        /// </summary>
        public string OuterDestinationNodeId { get; private set; }

        /// <summary>
        /// Number of primary nodes along this path (excluding branches).
        /// </summary>
        public int NodeCount => _nodeIds.Count;

        /// <summary>
        /// Number of primary edges along this path.
        /// </summary>
        public int EdgeCount => _edgeIds.Count;

        public RadialPath(string pathId, float startAngle)
        {
            PathId = pathId;
            StartAngle = startAngle;
            _nodeIds = new List<string>();
            _edgeIds = new List<string>();
            OuterDestinationNodeId = null;
        }

        /// <summary>
        /// Add a node to the path (in order from Refuge outward).
        /// </summary>
        internal void AddNode(string nodeId)
        {
            _nodeIds.Add(nodeId);
        }

        /// <summary>
        /// Add an edge to the path.
        /// </summary>
        internal void AddEdge(string edgeId)
        {
            _edgeIds.Add(edgeId);
        }

        /// <summary>
        /// Set the OuterDestination node (should be the last node added).
        /// </summary>
        internal void SetOuterDestination(string nodeId)
        {
            OuterDestinationNodeId = nodeId;
        }

        /// <summary>
        /// Check if a node ID is part of this path's primary spine.
        /// </summary>
        public bool ContainsNode(string nodeId)
        {
            return _nodeIds.Contains(nodeId);
        }

        /// <summary>
        /// Check if an edge ID is part of this path's primary spine.
        /// </summary>
        public bool ContainsEdge(string edgeId)
        {
            return _edgeIds.Contains(edgeId);
        }

        /// <summary>
        /// Get the index of a node within this path.
        /// Returns -1 if not found.
        /// </summary>
        public int GetNodeIndex(string nodeId)
        {
            return _nodeIds.IndexOf(nodeId);
        }

        /// <summary>
        /// Generate a deterministic path ID.
        /// </summary>
        public static string GenerateId(int seed, int pathIndex)
        {
            return $"path_{seed}_{pathIndex}";
        }

        public override string ToString()
        {
            return $"RadialPath[{PathId}] angle={StartAngle:F1}° nodes={NodeCount} dest={OuterDestinationNodeId}";
        }
    }
}
