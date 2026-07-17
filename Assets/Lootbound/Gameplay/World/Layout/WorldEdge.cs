using System.Collections.Generic;
using UnityEngine;

namespace Lootbound.Gameplay.World.Layout
{
    /// <summary>
    /// An edge connecting two nodes in the world layout graph.
    /// </summary>
    public sealed class WorldEdge
    {
        /// <summary>
        /// Deterministic ID: "edge_{seed}_{nodeAIndex}_{nodeBIndex}".
        /// </summary>
        public string EdgeId { get; }

        /// <summary>
        /// ID of the first connected node.
        /// </summary>
        public string NodeAId { get; }

        /// <summary>
        /// ID of the second connected node.
        /// </summary>
        public string NodeBId { get; }

        /// <summary>
        /// Length of this edge in meters.
        /// </summary>
        public float Length { get; }

        #region Path Membership

        /// <summary>
        /// ID of the RadialPath this edge belongs to.
        /// Null for edges connecting different paths or inter-path connections.
        /// Branch edges inherit the RadialPathId of their host path.
        /// </summary>
        public string RadialPathId { get; }

        /// <summary>
        /// Whether this edge is part of a primary path spine.
        /// True = main radial path edge, False = branch edge.
        /// </summary>
        public bool IsPrimaryPathEdge { get; }

        #endregion

        #region Geometry

        /// <summary>
        /// Control points defining the path shape.
        /// Includes start, intermediate sample points, and end.
        /// </summary>
        public IReadOnlyList<Vector3> ControlPoints => _controlPoints;
        private readonly List<Vector3> _controlPoints;

        #endregion

        #region Terrain Evaluation

        /// <summary>
        /// Average slope along the edge in degrees.
        /// </summary>
        public float AverageSlope { get; }

        /// <summary>
        /// Maximum slope along the edge in degrees.
        /// </summary>
        public float MaxSlope { get; }

        /// <summary>
        /// Whether this edge is traversable based on slope constraints.
        /// </summary>
        public bool IsTraversable { get; }

        #endregion

        public WorldEdge(
            string edgeId,
            string nodeAId,
            string nodeBId,
            float length,
            string radialPathId,
            bool isPrimaryPathEdge,
            List<Vector3> controlPoints,
            float averageSlope,
            float maxSlope,
            bool isTraversable)
        {
            EdgeId = edgeId;
            NodeAId = nodeAId;
            NodeBId = nodeBId;
            Length = length;
            RadialPathId = radialPathId;
            IsPrimaryPathEdge = isPrimaryPathEdge;
            _controlPoints = controlPoints ?? new List<Vector3>();
            AverageSlope = averageSlope;
            MaxSlope = maxSlope;
            IsTraversable = isTraversable;
        }

        /// <summary>
        /// Generate a deterministic edge ID.
        /// </summary>
        public static string GenerateId(int seed, int nodeAIndex, int nodeBIndex)
        {
            return $"edge_{seed}_{nodeAIndex}_{nodeBIndex}";
        }

        /// <summary>
        /// Get the other node ID connected by this edge.
        /// </summary>
        public string GetOtherNodeId(string nodeId)
        {
            if (nodeId == NodeAId) return NodeBId;
            if (nodeId == NodeBId) return NodeAId;
            return null;
        }

        /// <summary>
        /// Check if this edge belongs to a specific radial path.
        /// </summary>
        public bool BelongsToPath(string pathId)
        {
            return RadialPathId == pathId;
        }

        public override string ToString()
        {
            string pathInfo = RadialPathId != null
                ? $"path={RadialPathId} primary={IsPrimaryPathEdge}"
                : "no-path";
            return $"WorldEdge[{EdgeId}] {NodeAId}↔{NodeBId} {Length:F1}m {pathInfo}";
        }
    }
}
